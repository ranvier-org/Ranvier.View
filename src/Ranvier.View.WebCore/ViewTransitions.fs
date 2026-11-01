namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Same-document view transitions driven by a GPU native host's run loop.</summary>
module ViewTransitions =
    type private Observation(session: Session, callback: ViewTransitionSettlement -> unit) =
        member _.Session = session
        member _.Callback = callback
        member val Live = true with get, set

    let private invoke (session: Session) action =
        session.ScriptDepth <- session.ScriptDepth + 1

        try
            try
                action ()
                0
            with error ->
                session.ListenerErrors.Add error
                1
        finally
            session.ScriptDepth <- session.ScriptDepth - 1

            if
                session.ScriptDepth = 0
                && not (isNull session.AfterListener)
            then
                session.AfterListener.Invoke ()

    let private settledDelegate =
        Native.ViewTransitionSettled (fun context promise errorName ->
            let observation = GCHandle.FromIntPtr(context).Target :?> Observation

            if observation.Live then
                invoke observation.Session (fun () ->
                    let kind =
                        match promise with
                        | 0 -> ViewTransitionPromise.UpdateCallbackDone
                        | 1 -> ViewTransitionPromise.Ready
                        | 2 -> ViewTransitionPromise.Finished
                        | _ -> invalidOp "WebCore returned an unknown view-transition promise"

                    observation.Callback
                        {
                            Promise = kind
                            ErrorName =
                                if errorName = 0n then
                                    None
                                else
                                    Some (Marshal.PtrToStringUTF8 errorName)
                        })
                |> ignore)

    let private settledPointer =
        Marshal.GetFunctionPointerForDelegate<Native.ViewTransitionSettled> settledDelegate

    /// <summary>A provider of the live native document; raises NotSupportedException for CPU packages.</summary>
    let forDocument (document: Document) : ViewTransitionService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        if not (WebCoreHost.Has "view-transitions") then
            raise (NotSupportedException "Same-document view transitions require the GPU native package")

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false
        let registrations = ResizeArray<IDisposable>()

        let nameBytes (value: string) =
            Encoding.UTF8.GetBytes (if isNull value then "null" else value)

        let owningThread () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A view-transition provider requires its document's owning thread"

        let active () =
            owningThread ()

            if disposed then
                raise (ObjectDisposedException "ViewTransitionService")

            let handle = Interop.handle (box owner)
            let current = Native.rv_host_document session.Host

            try
                if current <> handle then
                    invalidOp "The view-transition document is no longer active"
            finally
                Native.rv_node_release current

            handle

        { new ViewTransitionService with
            member _.Start(?update, ?types) =
                let documentHandle = active ()
                let callback = ViewTransitionCallbacks.create session (defaultArg update ignore)

                let mutable live = true
                let observers = ResizeArray<IDisposable>()

                let revoke () =
                    if live then
                        live <- false

                        Native.rv_view_transition_update_callback_revoke callback
                        |> Interop.check "ViewTransition.revoke"

                        NativeDom.rv_dom_view_transition_update_callback_release callback

                let mutable handle = 0n

                try
                    use scope = new InputScope ()
                    let mutable options = NativeDom.RvDomStartViewTransitionOptions ()
                    options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomStartViewTransitionOptions>())
                    options.update <- callback

                    match types with
                    | Some names when not (isNull names) ->
                        let spans = names |> Array.map scope.TextSpan
                        let mutable sequence = NativeDom.RvDomSequenceDOMStringNullable ()
                        sequence.present <- 1
                        sequence.count <- spans.Length
                        sequence.items <- scope.Pin spans
                        options.types <- scope.Pin [| sequence |]
                    | _ -> ()

                    NativeDom.rv_dom_document_start_view_transition (documentHandle, [| options |], &handle)
                    |> Interop.check "Document.startViewTransition"
                with _ ->
                    revoke ()
                    reraise ()

                if handle = 0n then
                    revoke ()
                    invalidOp "Document.startViewTransition returned NULL"

                let transition =
                    session.Object (
                        handle,
                        Kind.ViewTransition,
                        owner,
                        Func<Session, nativeint, NativeObject>(fun s h -> ViewTransitionObject (s, h))
                    )
                    |> unbox<ViewTransition>

                let ensure () =
                    active () |> ignore

                    if not live then
                        raise (ObjectDisposedException "ViewTransitionRegistration")

                    Interop.handle (box transition)

                let withTypes action =
                    let transitionHandle = ensure ()
                    let mutable result = 0n

                    NativeDom.rv_dom_view_transition_get_types (transitionHandle, &result)
                    |> Interop.check "ViewTransition.types"

                    try
                        action result
                    finally
                        NativeDom.rv_dom_view_transition_type_set_release result

                let typeSet =
                    { new ViewTransitionTypes with
                        member _.Count =
                            withTypes (fun h ->
                                let mutable result = 0u

                                NativeDom.rv_dom_view_transition_type_set_size (h, &result)
                                |> Interop.check "ViewTransition.types.size"

                                result)

                        member _.Has value =
                            withTypes (fun h ->
                                let bytes = nameBytes value
                                let mutable result = 0

                                NativeDom.rv_dom_view_transition_type_set_has (h, bytes, bytes.Length, &result)
                                |> Interop.check "ViewTransition.types.has"

                                result <> 0)

                        member _.Values() =
                            withTypes (fun h ->
                                let mutable result = NativeDom.RvDomSequenceDOMString ()

                                try
                                    NativeDom.rv_dom_view_transition_type_set_values (h, &result)
                                    |> Interop.check "ViewTransition.types.values"

                                    Array.init result.count (fun i ->
                                        Marshal.PtrToStructure<NativeDom.RvDomUtf8StringSpan>(
                                            result.items
                                            + nativeint (
                                                i
                                                * Marshal.SizeOf<NativeDom.RvDomUtf8StringSpan>()
                                            )
                                        )
                                        |> Interop.spanText)
                                finally
                                    NativeDom.rv_dom_sequence_dom_string_release &result)

                        member _.Add value =
                            withTypes (fun h ->
                                let bytes = nameBytes value

                                NativeDom.rv_dom_view_transition_type_set_add (h, bytes, bytes.Length)
                                |> Interop.check "ViewTransition.types.add")

                        member _.Delete value =
                            withTypes (fun h ->
                                let bytes = nameBytes value
                                let mutable result = 0

                                NativeDom.rv_dom_view_transition_type_set_delete (h, bytes, bytes.Length, &result)
                                |> Interop.check "ViewTransition.types.delete"

                                result <> 0)

                        member _.Clear() =
                            withTypes (fun h ->
                                NativeDom.rv_dom_view_transition_type_set_clear h
                                |> Interop.check "ViewTransition.types.clear")
                    }

                let mutable owned: IDisposable option = None

                let registration =
                    { new ViewTransitionRegistration with
                        member _.Transition =
                            ensure () |> ignore
                            transition

                        member _.Types =
                            ensure () |> ignore
                            typeSet

                        member _.Observe callback =
                            let transitionHandle = ensure ()

                            if isNull (box callback) then
                                nullArg (nameof callback)

                            let observation = Observation (session, callback)
                            let context = GCHandle.Alloc observation
                            let mutable observer = 0n

                            try
                                Native.rv_view_transition_observe (
                                    session.Host,
                                    transitionHandle,
                                    settledPointer,
                                    GCHandle.ToIntPtr context,
                                    &observer
                                )
                                |> Interop.check "ViewTransition.observe"
                            with _ ->
                                context.Free ()
                                reraise ()

                            let token =
                                { new IDisposable with
                                    member _.Dispose() =
                                        owningThread ()

                                        if observation.Live then
                                            observation.Live <- false
                                            Native.rv_view_transition_observer_release observer
                                            context.Free ()
                                }

                            observers.Add token
                            token

                        member _.Dispose() =
                            owningThread ()

                            if live then
                                for observer in observers do
                                    observer.Dispose ()

                                observers.Clear ()
                                let value = unbox<NativeObject>(box transition)

                                if owner.Handle <> 0n && value.Handle <> 0n then
                                    NativeDom.rv_dom_view_transition_skip_transition value.Handle
                                    |> Interop.check "ViewTransition.skipTransition"

                                revoke ()

                                owned
                                |> Option.iter (fun token -> registrations.Remove token |> ignore)
                    }

                owned <- Some registration
                registrations.Add registration
                registration

            member _.Dispose() =
                owningThread ()

                if not disposed then
                    for registration in registrations.ToArray () do
                        registration.Dispose ()

                    registrations.Clear ()
                    disposed <- true
        }

    /// <summary>A borrowed provider of the host's current document.</summary>
    let forHost (host: WebCoreHost) : ViewTransitionService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
