namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Mutation observation through the native engine's notification checkpoint.</summary>
module MutationObservation =
    let private check operation status =
        if status = -12 then
            raise (MutationObserverException ("InvalidStateError", "The mutation observer's creation context is closed"))
        elif status = -3 then
            match Interop.failure operation status with
            | WebCoreDomException (name, detail) -> raise (MutationObserverException (name, detail))
            | error -> raise error
        else
            Interop.check operation status

    let private optionalBool value =
        let mutable result = Native.MutationOptionalBool ()

        value
        |> Option.iter (fun enabled ->
            result.present <- 1
            result.value <- if enabled then 1 else 0)

        result

    let private inputString (scope: InputScope) (value: string) =
        let mutable result = Native.MutationString ()
        result.data <- scope.Pin (value.ToCharArray ())
        result.length <- value.Length
        result

    /// <summary>Pins copied UTF-16 filters and preserves each optional field's presence.</summary>
    let internal inputOptions (scope: InputScope) (value: MutationObserverOptions) =
        let mutable result = Native.MutationObserverOptions ()
        result.size <- uint32 (Marshal.SizeOf<Native.MutationObserverOptions>())
        result.child_list <- if value.ChildList then 1 else 0
        result.subtree <- if value.Subtree then 1 else 0
        result.attributes <- optionalBool value.Attributes
        result.character_data <- optionalBool value.CharacterData
        result.attribute_old_value <- optionalBool value.AttributeOldValue
        result.character_data_old_value <- optionalBool value.CharacterDataOldValue

        value.AttributeFilter
        |> Option.iter (fun filter ->
            let strings = Array.map (inputString scope) filter
            let mutable optional = Native.MutationOptionalStrings ()
            optional.present <- 1
            optional.items <- scope.Pin strings
            optional.count <- strings.Length
            result.attribute_filter <- optional)

        result

    /// <summary>Copies raw UTF-16 units, retaining null separately from an empty value.</summary>
    let internal outputString (value: Native.MutationString) =
        match value.length with
        | -1 -> None
        | 0 -> Some ""
        | length when length > 0 && value.data <> 0n -> Some (Marshal.PtrToStringUni (value.data, length))
        | _ -> invalidOp "The engine returned an invalid mutation UTF-16 span"

    type private State(host: WebCoreHost, deliver: MutationRecord array -> unit, onClosed: unit -> unit) =
        let session = host.Session
        let observed = Dictionary<nativeint, WeakReference<NativeObject>>()
        let documents = Dictionary<nativeint, WeakReference<Session>>()
        let mutable token = 0n
        let mutable closing = 0
        let mutable delivery = Some deliver
        let mutable notifyClosed = Some onClosed

        let liveWrapper pointer =
            match observed.TryGetValue pointer with
            | true, weak ->
                match weak.TryGetTarget () with
                | true, value when
                    value.Handle = pointer
                    && not value.Session.IsDisposed
                    ->
                    Some value
                | _ -> None
            | _ -> None

        let documentSession pointer =
            match documents.TryGetValue pointer with
            | true, weak ->
                match weak.TryGetTarget () with
                | true, value when not value.IsDisposed -> Some value
                | _ -> Session.TryDocumentOwner pointer
            | _ -> Session.TryDocumentOwner pointer

        let rememberDocument pointer (owner: Session) =
            if pointer <> 0n && not owner.IsDisposed then
                documents[pointer] <- WeakReference<Session>(owner)

        let origin pointer =
            let fromDocument document =
                match documentSession document with
                | Some owner ->
                    rememberDocument document owner
                    owner
                | None -> invalidOp "The mutation node has no known live originating native session"

            if Native.rv_node_type pointer = 9 then
                fromDocument pointer
            else
                let mutable document = 0n

                NativeDom.rv_dom_node_get_owner_document (pointer, &document)
                |> check "Node.ownerDocument"

                try
                    fromDocument document
                finally
                    if document <> 0n then
                        Native.rv_node_release document

        let materialize batch =
            let created = ResizeArray<NativeObject>()

            let node pointer =
                if pointer = 0n then
                    invalidOp "The engine returned a null required mutation node"

                let known =
                    match liveWrapper pointer with
                    | Some value -> Some value
                    | None -> Session.TryKnownNode pointer

                match known with
                | Some value -> box value :?> Node
                | None ->
                    let owner = origin pointer

                    if owner.IsDisposed then
                        invalidOp "The mutation node's originating session is closed"

                    let retained = Native.rv_node_retain pointer
                    let mutable owned = true

                    try
                        let wrapped = owner.Node retained
                        owned <- false
                        created.Add wrapped
                        box wrapped :?> Node
                    finally
                        if owned then
                            Native.rv_node_release retained

            let optionalNode pointer =
                if pointer = 0n then None else Some (node pointer)

            let nodes (value: Native.MutationNodes) =
                if
                    value.count < 0
                    || value.count > Int32.MaxValue / IntPtr.Size
                    || (value.count > 0 && value.items = 0n)
                then
                    invalidOp "The engine returned an invalid mutation node span"

                Array.init value.count (fun index -> node (Marshal.ReadIntPtr (value.items, index * IntPtr.Size)))

            try
                let mutable count = 0

                Native.rv_mutation_record_batch_get_count (batch, &count)
                |> check "MutationRecordBatch.count"

                if count < 0 then
                    invalidOp "The engine returned a negative mutation record count"

                Array.init count (fun index ->
                    let mutable value = Native.MutationRecordView ()
                    value.size <- uint32 (Marshal.SizeOf<Native.MutationRecordView>())

                    Native.rv_mutation_record_batch_get_record (batch, index, &value)
                    |> check "MutationRecordBatch.record"

                    {
                        Type =
                            match outputString value.``type`` with
                            | Some "childList" -> MutationType.ChildList
                            | Some "attributes" -> MutationType.Attributes
                            | Some "characterData" -> MutationType.CharacterData
                            | _ -> invalidOp "The engine returned an unknown mutation record type"
                        Target = node value.target
                        AddedNodes = nodes value.added_nodes
                        RemovedNodes = nodes value.removed_nodes
                        PreviousSibling = optionalNode value.previous_sibling
                        NextSibling = optionalNode value.next_sibling
                        AttributeName = outputString value.attribute_name
                        AttributeNamespace = outputString value.attribute_namespace
                        OldValue = outputString value.old_value
                    })
            with _ ->
                for value in created do
                    try
                        value.Detach ()
                    with error ->
                        session.ListenerErrors.Add error

                reraise ()

        member _.Session = session

        member _.ReleaseOnOwner() =
            if not session.IsOwnerThread then
                invalidOp "Mutation observers are released on their host thread"

            Interlocked.Exchange (&closing, 1) |> ignore
            let owned = Interlocked.Exchange (&token, 0n)

            if owned <> 0n then
                let status = Native.rv_mutation_observer_release owned

                if status <> 0 then
                    Interlocked.CompareExchange (&token, owned, 0n)
                    |> ignore

                    check "MutationObserver.release" status

        member this.Release() =
            Interlocked.Exchange (&closing, 1) |> ignore

            if session.IsOwnerThread then
                this.ReleaseOnOwner ()
            else
                // A rejected admission retains the token for native creation-context invalidation.
                session.QueueOwnerCleanup this.ReleaseOnOwner
                |> ignore

        member this.ClosedByNative() =
            Interlocked.Exchange (&closing, 1) |> ignore
            let owned = Interlocked.Exchange (&token, 0n)
            let notify = notifyClosed
            delivery <- None
            notifyClosed <- None
            observed.Clear ()
            documents.Clear ()

            try
                notify
                |> Option.iter (fun callback -> callback ())
            finally
                if owned <> 0n then
                    Native.rv_mutation_observer_release owned
                    |> check "MutationObserver.release"

        member this.Attach(value) =
            token <- value

            if Volatile.Read (&closing) <> 0 then
                this.ReleaseOnOwner ()

        member _.Dispatch(batch) =
            if Volatile.Read (&closing) = 0 then
                let copied = materialize batch

                delivery
                |> Option.iter (fun callback -> callback copied)

        member private _.Active() =
            if
                Volatile.Read (&closing) <> 0
                || token = 0n
                || session.IsDisposed
            then
                raise (MutationObserverException ("InvalidStateError", "The mutation observer is closed"))

            host.EnsureActive ()
            token

        member this.Handle =
            {
                Observe =
                    fun target options ->
                        let observer = this.Active ()
                        let pointer = Interop.handle (box target)
                        let wrapper = box target :?> NativeObject

                        if
                            wrapper.Session.IsDisposed
                            || not wrapper.Session.IsOwnerThread
                        then
                            invalidArg "target" "The target must belong to a live native session on this host thread"

                        let document =
                            if target.nodeType = 9us then
                                pointer
                            else
                                let owner = target.ownerDocument
                                if isNull owner then 0n else Interop.handle (box owner)

                        let owner = documentSession document
                        use scope = new InputScope ()
                        let mutable nativeOptions = inputOptions scope options

                        Native.rv_mutation_observer_observe (observer, pointer, &nativeOptions)
                        |> check "MutationObserver.observe"

                        observed[pointer] <- WeakReference<NativeObject>(wrapper)
                        owner |> Option.iter (rememberDocument document)
                Disconnect =
                    fun () ->
                        Native.rv_mutation_observer_disconnect (this.Active ())
                        |> check "MutationObserver.disconnect"

                        observed.Clear ()
                        documents.Clear ()
                TakeRecords =
                    fun () ->
                        let mutable records = 0n

                        Native.rv_mutation_observer_take_records (this.Active (), &records)
                        |> check "MutationObserver.takeRecords"

                        if records = 0n then
                            invalidOp "The engine returned a null mutation record batch"

                        try
                            materialize records
                        finally
                            Native.rv_mutation_record_batch_release records
                            |> check "MutationRecordBatch.release"
                Release = this.Release
            }

    let private run =
        Native.MutationCallback (fun context _ records ->
            let mutable state: State option = None

            try
                let value = GCHandle.FromIntPtr(context).Target :?> State
                state <- Some value
                let session = value.Session
                session.ScriptDepth <- session.ScriptDepth + 1

                try
                    value.Dispatch records
                finally
                    session.ScriptDepth <- session.ScriptDepth - 1

                    if
                        session.ScriptDepth = 0
                        && not session.IsDisposed
                        && not (isNull session.AfterListener)
                    then
                        session.AfterListener.Invoke ()
            with error ->
                state
                |> Option.iter (fun value -> value.Session.ListenerErrors.Add error))

    let private release =
        Native.MutationContextRelease (fun context ->
            let mutable state: State option = None

            try
                let handle = GCHandle.FromIntPtr context
                let value = handle.Target :?> State
                state <- Some value

                try
                    value.ClosedByNative ()
                finally
                    handle.Free ()
                    GC.KeepAlive value
            with error ->
                state
                |> Option.iter (fun value -> value.Session.ListenerErrors.Add error))

    let private runPointer =
        Marshal.GetFunctionPointerForDelegate<Native.MutationCallback> run

    let private releasePointer =
        Marshal.GetFunctionPointerForDelegate<Native.MutationContextRelease> release

    /// <summary>A mutation service for each observer's creation document in host.</summary>
    /// <remarks>DOM operations use the host thread. Disposal requests from other threads queue owner cleanup;
    /// host replacement/destruction automatically closes observers after their final in-flight callback.</remarks>
    let forHost (host: WebCoreHost) : MutationObservationService =
        if isNull (box host) then
            nullArg "host"

        let ensureContext () =
            if host.Session.IsDisposed then
                raise (MutationObserverException ("InvalidStateError", "The observer's native host is closed"))

            host.EnsureActive ()

        ensureContext ()

        if not (WebCoreHost.Has "mutation-observation") then
            raise (NotSupportedException "The native library has no mutation-observation functions")

        Ranvier.Dom.MutationObservation.create
            {
                Create =
                    fun deliver onClosed ->
                        ensureContext ()
                        let state = State (host, deliver, onClosed)
                        let context = GCHandle.Alloc state
                        let mutable observer = 0n

                        let status =
                            try
                                Native.rv_host_create_mutation_observer (
                                    host.Handle,
                                    runPointer,
                                    GCHandle.ToIntPtr context,
                                    releasePointer,
                                    &observer
                                )
                            with _ ->
                                context.Free ()
                                reraise ()

                        if status <> 0 then
                            context.Free ()
                            check "MutationObserver.create" status

                        if observer = 0n then
                            invalidOp "The engine returned a null mutation observer"

                        state.Attach observer
                        state.Handle
            }
