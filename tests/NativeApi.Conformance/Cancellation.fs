module internal NativeApiConformance.Cancellation

open System
open System.Threading
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Checks abort-once, reasons, timeouts, composition, listener signals and token mapping on one provider.</summary>
/// <remarks><c>turn</c> completes after the provider has run at least one later host turn; no check waits on wall-clock time.</remarks>
let run (document: Document) (create: unit -> CancellationService) (turn: unit -> Task<unit>) =
    task {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let raises label (expected: exn -> bool) (action: unit -> unit) =
            checks <- checks + 1

            let raised =
                try
                    action ()
                    false
                with error ->
                    if not (expected error) then
                        failwith $"{label}: unexpected {error.Message}"

                    true

            if not raised then
                failwith $"{label}: expected an exception"

        let argument (error: exn) =
            error :? ArgumentException

        let disposed (error: exn) =
#if FABLE_COMPILER
            error.Message.Contains "CancellationService"
#else
            error :? ObjectDisposedException
#endif

        let timing (error: exn) =
            match error with
            | TimingException ("DataCloneError", _) -> true
            | _ -> false

        let same label (expected: obj) (actual: obj) =
            checks <- checks + 1

            if not (obj.ReferenceEquals (expected, actual)) then
                failwith $"{label}: expected the same reason instance"

        let inner (signal: AbortSignal) : obj =
            match signal.Reason with
            | Some (AbortReason.Custom detail) -> box detail
            | other -> failwith $"Expected a custom reason, got {other}"

        let dispatch (target: Element) eventType =
            let event = document.createEvent "Event"
            event.initEvent (eventType, false, false)
            target.dispatchEvent event |> ignore

        let element () =
            let value = document.createElement "div"
            document.body.appendChild value |> ignore
            value

        use service = create ()
        use peer = create ()

        // Abort once, callbacks in registration order, unregistration and reason stability.
        let controller = service.CreateController ()
        same "signal identity" controller.Signal controller.Signal
        equal "fresh signal" false controller.Signal.Aborted
        equal "fresh reason" None controller.Signal.Reason
        controller.Signal.ThrowIfAborted ()
        let order = ResizeArray<string>()
        use _first = controller.Signal.OnAbort (fun reason -> order.Add $"first:{reason}")
        use _second = controller.Signal.OnAbort (fun _ -> order.Add "second")
        let removed = controller.Signal.OnAbort (fun _ -> order.Add "removed")
        removed.Dispose ()
        controller.Abort ()
        equal "aborted" true controller.Signal.Aborted
        equal "default reason" (Some AbortReason.Abort) controller.Signal.Reason
        equal "callback order" [ "first:Abort"; "second" ] (List.ofSeq order)
        controller.Abort (AbortReason.Custom (TimingDetail.Text "later"))
        equal "abort once keeps reason" (Some AbortReason.Abort) controller.Signal.Reason
        equal "abort once runs callbacks once" 2 order.Count
        use _late = controller.Signal.OnAbort (fun _ -> order.Add "late")
        do! turn ()
        equal "aborted signal never runs a new callback" 2 order.Count

        try
            controller.Signal.ThrowIfAborted ()
            failwith "Expected AbortedException"
        with AbortedException reason ->
            equal "thrown reason" AbortReason.Abort reason

        // Custom reasons are copied, keep their instance and reject cycles.
        let fields = [| "code", TimingDetail.Number 7.0 |]
        let custom = service.CreateController ()
        custom.Abort (AbortReason.Custom (TimingDetail.Object fields))
        fields[0] <- ("code", TimingDetail.Number 8.0)
        equal "custom reason copied" (Some (AbortReason.Custom (TimingDetail.Object [| "code", TimingDetail.Number 7.0 |]))) custom.Signal.Reason
        same "custom reason instance" (inner custom.Signal) (inner custom.Signal)
        let cyclic = [| TimingDetail.Null |]
        cyclic[0] <- TimingDetail.Array cyclic
        let rejected = service.CreateController ()
        raises "cyclic reason" timing (fun () -> rejected.Abort (AbortReason.Custom (TimingDetail.Array cyclic)))
        equal "rejected reason leaves signal" false rejected.Signal.Aborted
        raises "null reason" argument (fun () -> rejected.Abort Unchecked.defaultof<AbortReason>)

        // Already-aborted statics.
        let already = service.Abort AbortReason.Timeout
        equal "abort static" (Some AbortReason.Timeout) already.Reason
        let mutable alreadyRuns = 0
        use _already = already.OnAbort (fun _ -> alreadyRuns <- alreadyRuns + 1)

        // Timeouts run on a later turn.
        raises "negative timeout" argument (fun () -> service.Timeout -1.0 |> ignore)
        raises "NaN timeout" argument (fun () -> service.Timeout Double.NaN |> ignore)
        raises "infinite timeout" argument (fun () -> service.Timeout Double.PositiveInfinity |> ignore)
        let soon = service.Timeout 0.0
        let distant = service.Timeout 1.0e9
        let mutable timeoutRuns = 0

        use _soon =
            soon.OnAbort (fun reason ->
                if reason = AbortReason.Timeout then
                    timeoutRuns <- timeoutRuns + 1)

        equal "timeout waits for a turn" false soon.Aborted
        let mutable turns = 0

        while not soon.Aborted && turns < 100 do
            do! turn ()
            turns <- turns + 1

        equal "zero timeout aborts" (Some AbortReason.Timeout) soon.Reason
        equal "timeout callback" 1 timeoutRuns
        equal "distant timeout pending" false distant.Aborted
        equal "already-aborted callback never runs" 0 alreadyRuns

        // Composition: first source wins, nested composites flatten, dependents abort with the source.
        let left = service.CreateController ()
        let right = service.CreateController ()
        let both = service.Any [| left.Signal; right.Signal |]
        let nested = service.Any [| both |]
        let composed = ResizeArray<string>()

        use _right =
            right.Signal.OnAbort (fun _ ->
                composed.Add (
                    if both.Aborted then
                        "source:dependent-aborted"
                    else
                        "source:dependent-pending"
                ))

        use _both = both.OnAbort (fun _ -> composed.Add "both")
        use _nested = nested.OnAbort (fun _ -> composed.Add "nested")
        equal "composite pending" false both.Aborted
        right.Abort (AbortReason.Custom (TimingDetail.Object [| "side", TimingDetail.Text "right" |]))
        equal "composite reason" right.Signal.Reason both.Reason
        same "composite reason instance" (inner right.Signal) (inner both)
        same "flattened reason instance" (inner right.Signal) (inner nested)
        equal "source callbacks before dependents" [ "source:dependent-aborted"; "both"; "nested" ] (List.ofSeq composed)
        left.Abort AbortReason.Timeout
        equal "later source leaves composite" (Some (AbortReason.Custom (TimingDetail.Object [| "side", TimingDetail.Text "right" |]))) both.Reason
        let pending = service.CreateController ()
        let early = service.Any [| pending.Signal; already |]
        equal "already-aborted source" (Some AbortReason.Timeout) early.Reason
        equal "empty composite" false (service.Any [||]).Aborted

        raises "null source" argument (fun () ->
            service.Any [| pending.Signal; Unchecked.defaultof<AbortSignal> |]
            |> ignore)

        raises "foreign source" argument (fun () ->
            service.Any [| (peer.CreateController ()).Signal |]
            |> ignore)

        // Listener signals.
        let target = element ()
        let mutable plain = 0
        let plainListener: EventListener = fun _ -> plain <- plain + 1
        let listened = service.CreateController ()
        service.Listen (target, "plain", plainListener, null, listened.Signal)
        dispatch target "plain"
        equal "signalled listener runs" 1 plain
        listened.Abort ()
        dispatch target "plain"
        equal "abort removes listener" 1 plain

        let mutable skipped = 0
        service.Listen (target, "skipped", (fun _ -> skipped <- skipped + 1), null, already)
        dispatch target "skipped"
        equal "already-aborted signal adds nothing" 0 skipped

        let mutable duplicate = 0
        let duplicateListener: EventListener = fun _ -> duplicate <- duplicate + 1
        target.addEventListener ("duplicate", duplicateListener)
        let duplicateController = service.CreateController ()
        service.Listen (target, "duplicate", duplicateListener, null, duplicateController.Signal)
        duplicateController.Abort ()
        dispatch target "duplicate"
        equal "duplicate keeps existing registration" 1 duplicate

        let mutable readded = 0
        let readdedListener: EventListener = fun _ -> readded <- readded + 1
        let readdedController = service.CreateController ()
        service.Listen (target, "readded", readdedListener, null, readdedController.Signal)
        target.removeEventListener ("readded", readdedListener)
        target.addEventListener ("readded", readdedListener)
        readdedController.Abort ()
        dispatch target "readded"
        equal "abort removes the matching registration" 0 readded

        let mutable once = 0
        let onceController = service.CreateController ()
        service.Listen (target, "once", (fun _ -> once <- once + 1), AddEventListenerOptions (once = true), onceController.Signal)
        dispatch target "once"
        dispatch target "once"
        onceController.Abort ()
        equal "once with signal" 1 once

        let mutable captured = 0
        let captureListener: EventListener = fun _ -> captured <- captured + 1
        let captureController = service.CreateController ()
        service.Listen (target, "capture", captureListener, AddEventListenerOptions (capture = true), captureController.Signal)
        target.removeEventListener ("capture", captureListener)
        dispatch target "capture"
        equal "bubble removal leaves capture registration" 1 captured
        captureController.Abort ()
        dispatch target "capture"
        equal "abort removes capture registration" 1 captured

        let mutable beforeCallback = 0
        let ordering = service.CreateController ()
        service.Listen (target, "ordering", (fun _ -> beforeCallback <- beforeCallback + 1), null, ordering.Signal)
        use _ordering = ordering.Signal.OnAbort (fun _ -> dispatch target "ordering")
        ordering.Abort ()
        equal "listener removed before abort callbacks" 0 beforeCallback
        raises "foreign listener signal" argument (fun () -> service.Listen (target, "x", plainListener, null, (peer.CreateController ()).Signal))
        raises "null target" argument (fun () -> service.Listen (Unchecked.defaultof<EventTarget>, "x", plainListener, null, pending.Signal))

        // Token mapping.
        let mapped = service.CreateController ()
        let token = service.ToCancellationToken mapped.Signal
        equal "token stable" false (service.ToCancellationToken mapped.Signal).IsCancellationRequested
        equal "token pending" false token.IsCancellationRequested
        mapped.Abort ()
        equal "abort cancels token" true token.IsCancellationRequested
        equal "aborted signal token" true (service.ToCancellationToken already).IsCancellationRequested
        use source = new CancellationTokenSource ()
        let fromToken = service.FromCancellationToken source.Token
        source.Cancel ()
        equal "token cancellation is not synchronous" false fromToken.Aborted
        turns <- 0

        while not fromToken.Aborted && turns < 100 do
            do! turn ()
            turns <- turns + 1

        equal "token cancellation aborts on a later turn" (Some AbortReason.Abort) fromToken.Reason
        use cancelled = new CancellationTokenSource ()
        cancelled.Cancel ()
        equal "cancelled token" true (service.FromCancellationToken cancelled.Token).Aborted
        use idle = new CancellationTokenSource ()
        let never = service.FromCancellationToken idle.Token
        do! turn ()
        equal "uncancelled token" false never.Aborted

        // Disposal.
        let survivor = service.CreateController ()
        service.Dispose ()
        raises "disposed service" disposed (fun () -> service.CreateController () |> ignore)
        raises "disposed signal" disposed (fun () -> survivor.Signal.Aborted |> ignore)
        equal "peer survives" false (peer.CreateController ()).Signal.Aborted
        target.remove ()
        return checks
    }
