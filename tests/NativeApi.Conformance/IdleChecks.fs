module internal NativeApiConformance.IdleChecks

open System
open System.Collections.Generic
open Ranvier
open Ranvier.Dom

/// <summary>Advances one host: an idle period, the next animation frame, or elapsed time with no idle period.</summary>
type Driver =
    {
        Idle: unit -> Async<unit>
        NextFrame: unit -> Async<unit>
        Starve: float -> Async<unit>
    }

/// <summary>Checks request order, nested requests, frame-before-idle order, timeouts, cancellation, owner disposal
/// and scheduler disposal over one idle scheduler and the frame scheduler of the same host.</summary>
let run (create: unit -> IdleScheduler * FrameScheduler) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let check label condition =
            equal label true condition

        let log = List<string>()

        let drain () =
            let items = log.ToArray ()
            log.Clear ()
            List.ofArray items

        use graph = new Graph ()
        use _ = graph.Activate ()
        let idle, frames = create ()

        // Request order and nested requests.
        let mutable remaining = -1.0
        let mutable timedOut = true

        idle.Idle (
            None,
            fun deadline ->
                remaining <- deadline.TimeRemaining ()
                timedOut <- deadline.DidTimeout
                log.Add "a"

                idle.Idle (None, fun _ -> log.Add "nested")
                |> ignore
        )
        |> ignore

        idle.Idle (None, fun _ -> log.Add "b") |> ignore
        do! driver.Idle ()
        equal "idle callbacks run in request order" [ "a"; "b" ] (drain ())
        check "deadline within the 50 ms idle period" (remaining >= 0.0 && remaining <= 50.0)
        check "an idle period callback has not timed out" (not timedOut)
        do! driver.Idle ()
        equal "a nested request runs in a later idle period" [ "nested" ] (drain ())

        // Cancellation.
        let cancelled = idle.Idle (None, fun _ -> log.Add "cancelled")
        cancelled.Dispose ()
        cancelled.Dispose ()
        do! driver.Idle ()
        equal "cancellation is idempotent" [] (drain ())

        // A pending frame runs first.
        frames.Frame (fun _ -> log.Add "frame") |> ignore

        idle.Idle (None, fun _ -> log.Add "idle")
        |> ignore

        do! driver.NextFrame ()
        do! driver.Idle ()
        equal "a pending frame runs before the idle callback" [ "frame"; "idle" ] (drain ())

        // Timeout.
        let mutable expired = Unchecked.defaultof<IdleDeadline>
        let mutable expiredRemaining = -1.0

        idle.Idle (
            Some 20.0,
            fun deadline ->
                expired <- deadline
                expiredRemaining <- deadline.TimeRemaining ()
                log.Add "timeout"
        )
        |> ignore

        do! driver.Starve 60.0
        equal "a timeout runs the callback without an idle period" [ "timeout" ] (drain ())
        check "a timed-out callback reports DidTimeout" expired.DidTimeout
        equal "a timed-out callback has no time remaining" 0.0 expiredRemaining

        // Owner-bound idle callbacks.
        let root, inner =
            createRoot (fun root ->
                let inner = ref Unchecked.defaultof<Owner>

                Ranvier.View.Dom.Frames.idle idle None (fun _ ->
                    inner.Value <- getOwner ()
                    log.Add "owned idle")
                |> ignore

                root, (fun () -> inner.Value))

        let disposedRoot =
            createRoot (fun root ->
                Ranvier.View.Dom.Frames.idle idle None (fun _ -> log.Add "disposed owner idle")
                |> ignore

                root)

        disposedRoot.Dispose ()
        do! driver.Idle ()
        equal "owner disposal cancels; the owned callback runs" [ "owned idle" ] (drain ())
        check "the owned callback runs in a child scope" (not (obj.ReferenceEquals (inner (), root)))
        check "the child scope is disposed after the callback" (inner ()).IsDisposed
        root.Dispose ()

        // Scheduler disposal.
        idle.Idle (None, fun _ -> log.Add "disposed idle")
        |> ignore

        idle.Idle (Some 5.0, fun _ -> log.Add "disposed timeout")
        |> ignore

        idle.Dispose ()
        idle.Dispose ()
        do! driver.Starve 20.0
        do! driver.Idle ()
        equal "scheduler disposal cancels pending callbacks" [] (drain ())

        let rejected =
            try
                idle.Idle (None, ignore) |> ignore
                false
            with _ ->
                true

        check "requests after disposal raise" rejected
        frames.Dispose ()
        return checks
    }
