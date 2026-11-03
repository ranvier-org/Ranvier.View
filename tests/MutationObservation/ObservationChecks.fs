module Ranvier.View.WebCore.Tests.ObservationChecks

open System
open System.Runtime.CompilerServices
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore
open RanvierWebCoreAbi

module Adapter = Ranvier.View.WebCore.MutationObservation

type private ManualAbort() =
    let gate = obj ()
    let listeners = ResizeArray<AbortReason -> unit>()
    let mutable aborted = false

    member _.Abort() =
        let snapshot =
            lock gate (fun () ->
                aborted <- true
                listeners.ToArray ())

        for listener in snapshot do
            listener AbortReason.Abort

    member _.Signal =
        { new AbortSignal with
            member _.Aborted = lock gate (fun () -> aborted)

            member _.Reason =
                lock gate (fun () -> if aborted then Some AbortReason.Abort else None)

            member _.ThrowIfAborted() =
                if lock gate (fun () -> aborted) then
                    failwith "aborted"

            member _.OnAbort callback =
                lock gate (fun () -> listeners.Add callback)

                { new IDisposable with
                    member _.Dispose() =
                        lock gate (fun () -> listeners.Remove callback |> ignore)
                }
        }

let private pointer (node: Node) =
    (box node :?> NativeObject).Handle

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private capturingObserver (service: MutationObservationService) =
    let payload = obj ()
    let observer = service.Create (fun _ _ -> GC.KeepAlive payload)
    observer, WeakReference payload

let runMarshalling () =
    let mutable checks = 0

    let check condition detail =
        if not condition then
            failwith detail

        checks <- checks + 1

    use scope = new InputScope ()

    let options =
        Adapter.inputOptions
            scope
            { MutationObserverOptions.Default with
                Attributes = Some false
                AttributeFilter = Some [||]
            }

    check
        (options.attributes.present = 1
         && options.attributes.value = 0)
        "Explicit false is present"

    check (options.character_data.present = 0) "An omitted boolean stays absent"

    check
        (options.attribute_filter.present = 1
         && options.attribute_filter.count = 0)
        "An empty filter is present"

    let mutable text = Native.MutationString ()
    text.length <- -1
    check (Adapter.outputString text = None) "Null differs from empty"
    text.length <- 0
    check (Adapter.outputString text = Some "") "Empty is present"
    let units = [| char 0; char 0xD800; 'x'; char 0xDC00 |]
    text.data <- scope.Pin units
    text.length <- units.Length
    check ((Adapter.outputString text).Value.ToCharArray() = units) "Copied UTF-16 preserves NUL and lone surrogates"
    checks

let runNative () =
    let mutable checks = 0

    let check condition detail =
        if not condition then
            failwith detail

        checks <- checks + 1

    let named expected action =
        let mutable name = ""

        try
            action ()
        with MutationObserverException (value, _) ->
            name <- value

        check (name = expected) ("Expected named " + expected)

    let pump (hosts: WebCoreHost array) ready =
        let mutable turns = 0

        while not (ready ()) && turns < 32 do
            for host in hosts do
                host.Cycle ()

            turns <- turns + 1

        check (ready ()) "A normal owner-loop checkpoint delivers the mutation"

    check (WebCoreHost.Has "mutation-observation") "The production D02 native capability is required"
    use host = WebCoreHost.Create (160, 120)
    use foreign = WebCoreHost.Create (160, 120)
    use service = Adapter.forHost host
    let batches = ResizeArray<MutationRecord array>()
    let mutable callbackIdentity = Unchecked.defaultof<MutationObserver>

    use observer =
        service.Create (fun records self ->
            callbackIdentity <- self
            batches.Add records)

    let target = foreign.Document.createElement "section"
    foreign.Document.body.appendChild target |> ignore

    let attributes =
        { MutationObserverOptions.Default with
            Attributes = Some true
            AttributeOldValue = Some true
        }

    named "TypeError" (fun () -> observer.Observe (target, MutationObserverOptions.Default))

    named "TypeError" (fun () ->
        observer.Observe (
            target,
            { attributes with
                Attributes = Some false
            }
        ))

    observer.Observe (target, attributes)
    target.setAttribute ("data-value", "")
    target.setAttribute ("data-value", "a\000b")
    target.setAttribute ("data-value", "last")
    let drained = observer.TakeRecords ()
    check (drained.Length = 3 && batches.Count = 0) "TakeRecords drains without callback"

    check
        (drained
         |> Array.forall (fun record -> obj.ReferenceEquals (record.Target, target)))
        "A foreign observed target keeps its supplied wrapper"

    check
        (drained
         |> Array.map (fun record -> record.OldValue)
            =
            [| None; Some ""; Some "a\000b" |])
        "Old values preserve missing, empty and embedded NUL"

    check
        (drained
         |> Array.forall (fun record ->
             record.Type = MutationType.Attributes
             && record.AttributeName = Some "data-value"
             && record.AttributeNamespace = None))
        "Attribute metadata is copied"

    check (observer.TakeRecords().Length = 0) "An empty owned record batch drains safely"

    observer.Observe (
        target,
        { attributes with
            AttributeFilter = Some [||]
        }
    )

    target.setAttribute ("data-value", "filtered")
    check (observer.TakeRecords().Length = 0) "A present empty filter observes no attributes"
    let filter = [| "data-value" |]

    observer.Observe (
        target,
        { attributes with
            Attributes = None
            AttributeFilter = Some filter
        }
    )

    filter[0] <- "changed-after-observe"
    target.setAttribute ("ignored", "x")
    target.setAttribute ("data-value", "queued")
    pump [| host; foreign |] (fun () -> batches.Count = 1)
    check (obj.ReferenceEquals (observer, callbackIdentity)) "The callback receives the public observer itself"

    let delivered = batches[0]

    check
        (delivered.Length = 1
         && delivered[0].OldValue = Some "filtered")
        "The filter is copied before Observe returns"

    observer.Disconnect ()
    target.setAttribute ("data-value", "disconnected")
    check (observer.TakeRecords().Length = 0) "Disconnect discards future records"

    let child = foreign.Document.createElement "b"
    let sibling = foreign.Document.createElement "i"

    observer.Observe (
        target,
        { attributes with
            ChildList = true
            Subtree = true
        }
    )

    target.appendChild child |> ignore
    target.appendChild sibling |> ignore
    target.removeChild child |> ignore
    child.setAttribute ("data-removed", "transient")
    let transient = observer.TakeRecords ()
    check (transient.Length = 4) "Removed descendants remain transiently observed before checkpoint"
    check (obj.ReferenceEquals (transient[0].AddedNodes[0], child)) "Added nodes use a known live wrapper"

    check
        (transient[1].PreviousSibling
         |> Option.exists (fun value -> obj.ReferenceEquals (value, child)))
        "Previous sibling identity survives copying"

    check
        (transient[2].NextSibling
         |> Option.exists (fun value -> obj.ReferenceEquals (value, sibling)))
        "Next sibling identity survives copying"

    check
        (obj.ReferenceEquals (transient[2].RemovedNodes[0], child)
         && obj.ReferenceEquals (transient[3].Target, child))
        "Removed nodes and transient targets retain identity"

    let text = foreign.Document.createTextNode ""
    observer.Disconnect ()

    observer.Observe (
        text,
        { MutationObserverOptions.Default with
            CharacterDataOldValue = Some true
        }
    )

    text.data <- "first"
    text.data <- "second"
    let characters = observer.TakeRecords ()

    check
        (characters.Length = 2
         && characters[0].OldValue = Some ""
         && characters[1].OldValue = Some "first")
        "Omitted characterData is implied by old-value presence"

    check
        (characters
         |> Array.forall (fun record ->
             record.Type = MutationType.CharacterData
             && obj.ReferenceEquals (record.Target, text)))
        "Character-data targets and type survive copying"

    observer.Disconnect ()
    let foreignDocument = foreign.Document
    check (isNull foreignDocument.ownerDocument) "A Document target has no ownerDocument"

    observer.Observe (
        foreignDocument,
        { MutationObserverOptions.Default with
            ChildList = true
        }
    )

    let comment = foreignDocument.createComment "document-target"
    foreignDocument.appendChild comment |> ignore
    let documentRecords = observer.TakeRecords ()

    check
        (documentRecords.Length = 1
         && obj.ReferenceEquals (documentRecords[0].Target, foreignDocument))
        "Document routing preserves the supplied wrapper without querying a null owner"

    foreignDocument.removeChild comment |> ignore
    observer.Disconnect ()

    let duplicate =
        host.Session.Node (Native.rv_node_retain (pointer child)) :> obj :?> Node

    check (not (obj.ReferenceEquals (duplicate, child))) "The fixture retains the preexisting cross-session duplicate limitation"
    observer.Observe (duplicate, attributes)
    observer.Observe (child, attributes)
    host.Document.adoptNode child |> ignore
    host.Document.body.appendChild child |> ignore
    child.setAttribute ("adopted", "forward")
    let forward = observer.TakeRecords ()

    check
        (forward.Length = 1
         && obj.ReferenceEquals (forward[0].Target, child))
        "The latest Observe wrapper wins over an older per-session duplicate after adoption"

    foreign.Document.adoptNode child |> ignore
    foreign.Document.body.appendChild child |> ignore
    child.setAttribute ("adopted", "back")

    let back = observer.TakeRecords ()

    check
        (back.Length = 1
         && back
            |> Array.forall (fun record -> obj.ReferenceEquals (record.Target, child)))
        "Adoption back preserves the explicitly observed wrapper"

    let rawHost = Native.rv_host_create (80, 60)
    check (rawHost <> 0n) "The unknown-origin control uses a real native host"

    try
        let rawBody = Native.rv_document_body rawHost

        try
            let rawWrapper = host.Session.Node (Native.rv_node_retain rawBody)

            use unknown =
                service.Create (fun _ _ -> failwith "An unknown origin was silently routed")

            unknown.Observe (
                box rawWrapper :?> Node,
                { MutationObserverOptions.Default with
                    ChildList = true
                }
            )

            let rawChild = Native.rv_document_create_element (rawHost, "span")

            try
                Native.rv_node_append_child (rawBody, rawChild)
                |> Interop.check "unknown-origin append"
            finally
                Native.rv_node_release rawChild

            let mutable rejected = false

            try
                unknown.TakeRecords () |> ignore
            with :? InvalidOperationException as error ->
                rejected <- error.Message.Contains "no known live originating"

            check rejected "Unknown document ownership is explicit instead of guessing the observing session"
            check (unknown.TakeRecords().Length = 0) "A failed materialization still drains and releases its owned batch"
            unknown.Dispose ()
            rawWrapper.Detach ()
        finally
            Native.rv_node_release rawBody
    finally
        Native.rv_host_destroy rawHost

    let callbackError = InvalidOperationException "mutation callback"
    use failing = service.Create (fun _ _ -> raise callbackError)
    failing.Observe (target, attributes)
    target.setAttribute ("callback-error", "x")
    pump [| host; foreign |] (fun () -> host.Errors.Count > 0)
    check (obj.ReferenceEquals (host.Errors[0], callbackError)) "User exceptions use the existing host error route"
    host.Errors.Clear ()
    failing.Dispose ()

    let abort = ManualAbort ()

    use aborted =
        service.Create ((fun _ _ -> failwith "An aborted observer delivered"), signal = abort.Signal)

    aborted.Observe (target, attributes)
    target.setAttribute ("before-abort", "queued")
    let requester = Thread (ThreadStart abort.Abort)
    requester.Start ()
    check (requester.Join 5000) "Off-thread Abort returns without touching native DOM"
    check (aborted.TakeRecords().Length = 0) "Off-thread cleanup suppresses delivery immediately"
    host.Cycle ()
    check (host.Errors.Count = 0) "Owner queue releases the aborted native observer"

    use detachedService = Adapter.forHost host
    let retainedObserver, payload = capturingObserver detachedService
    retainedObserver.Observe (target, attributes)
    host.LoadSvg "<svg xmlns='http://www.w3.org/2000/svg'/>"
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    check (retainedObserver.TakeRecords().Length = 0) "Page replacement automatically closes a retained observer"
    named "InvalidStateError" (fun () -> retainedObserver.Observe (target, attributes))
    check (not payload.IsAlive) "Automatic closure drops the retained user callback"
    retainedObserver.Dispose ()

    use closing = WebCoreHost.Create (80, 60)
    use closingService = Adapter.forHost closing
    let mutable returned = false

    use selfDestroy =
        closingService.Create (fun _ self ->
            self.Dispose ()
            (closing :> IDisposable).Dispose()
            GC.Collect ()
            returned <- true)

    selfDestroy.Observe (
        closing.Document.body,
        { MutationObserverOptions.Default with
            ChildList = true
        }
    )

    closing.Document.body.appendChild (closing.Document.createElement "span")
    |> ignore

    closing.Cycle ()
    check returned "Self-disposal and host destruction preserve the running callback lease"

    check
        (selfDestroy.TakeRecords().Length = 0
         && closing.Errors.Count = 0)
        "No callback continuation touches the destroyed host"

    for _ in 1..8 do
        let retiring = WebCoreHost.Create (80, 60)
        use retiringService = Adapter.forHost retiring

        use retained =
            retiringService.Create (fun _ _ -> failwith "A closing host delivered")

        retained.Observe (
            retiring.Document.body,
            { MutationObserverOptions.Default with
                ChildList = true
            }
        )

        use start = new ManualResetEventSlim ()

        let disposer =
            Thread (
                ThreadStart (fun () ->
                    start.Wait ()
                    retained.Dispose ())
            )

        disposer.Start ()
        start.Set ()
        (retiring :> IDisposable).Dispose()
        check (disposer.Join 5000) "Concurrent Dispose request terminates"

        check
            (retained.TakeRecords().Length = 0
             && retiring.Errors.Count = 0)
            "Shutdown drains or invalidates the retained cleanup token"

        named "InvalidStateError" (fun () -> retiringService.Create (fun _ _ -> ()) |> ignore)

    let retired =
        Collections.Generic.Dictionary<nativeint, WeakReference<NativeObject>>()

    let mutable reused = 0

    for _ in 1..32 do
        let recycling = WebCoreHost.Create (40, 40)
        let wrapper = box recycling.Document.body :?> NativeObject
        let address = wrapper.Handle

        match retired.TryGetValue address with
        | true, weak ->
            reused <- reused + 1

            match weak.TryGetTarget () with
            | true, previous -> check (not (obj.ReferenceEquals (previous, wrapper))) "A recycled address never resurrects a disposed wrapper"
            | _ -> ()
        | _ -> ()

        check
            (Session.TryKnownNode address
             |> Option.exists (fun value -> obj.ReferenceEquals (value, wrapper)))
            "The registry resolves the current live wrapper"

        (recycling :> IDisposable).Dispose()

        check
            (wrapper.Handle = 0n
             && (Session.TryKnownNode address).IsNone)
            "Disposed sessions are removed before later address reuse"

        retired[address] <- WeakReference<NativeObject>(wrapper)

    Console.WriteLine (
        "Native node addresses actually reused="
        + reused.ToString ()
    )

    (foreign :> IDisposable).Dispose()

    check
        (drained[1].OldValue = Some ""
         && drained[2].OldValue = Some "a\000b")
        "Copied record strings survive originating host disposal"

    check (pointer drained[0].Target = 0n) "Retained record nodes follow their originating session's ordinary lifetime"
    GC.KeepAlive drained
    check (host.Errors.Count = 0 && foreign.Errors.Count = 0) "All remaining host error routes are empty"
    checks
