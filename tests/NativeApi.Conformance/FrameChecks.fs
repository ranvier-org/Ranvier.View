module internal NativeApiConformance.FrameChecks

open System
open System.Collections.Generic
open Ranvier
open Ranvier.Dom

/// <summary>Advances one host: a microtask checkpoint, elapsed timer time, or the next animation frame.</summary>
type Driver =
    {
        Checkpoint: unit -> Async<unit>
        Elapse: float -> Async<unit>
        NextFrame: unit -> Async<unit>
    }

/// <summary>Checks one-shot and rescheduled frames, timers and microtasks, cancellation, scheduler disposal, owner
/// disposal and the timed clock over one scheduler.</summary>
let run (create: unit -> FrameScheduler) (driver: Driver) =
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
        let scheduler = create ()
        let start = scheduler.Now ()

        // Microtasks.
        scheduler.Microtask (fun () -> log.Add "microtask")
        |> ignore

        let cancelledMicrotask =
            scheduler.Microtask (fun () -> log.Add "cancelled microtask")

        cancelledMicrotask.Dispose ()
        cancelledMicrotask.Dispose ()
        equal "microtask waits for a checkpoint" [] (drain ())
        do! driver.Checkpoint ()
        equal "microtask at checkpoint" [ "microtask" ] (drain ())

        // Timers.
        scheduler.Timeout (10.0, fun () -> log.Add "timer")
        |> ignore

        let cancelledTimer = scheduler.Timeout (10.0, fun () -> log.Add "cancelled timer")
        cancelledTimer.Dispose ()
        do! driver.Checkpoint ()
        equal "timer waits for its delay" [] (drain ())
        do! driver.Elapse 30.0
        equal "timer after its delay" [ "timer" ] (drain ())
        check "clock advances" (scheduler.Now () >= start + 10.0)

        // Frames, one-shot and rescheduled.
        let before = scheduler.Now ()
        let times = List<float>()

        let rec repeat remaining (time: float) =
            times.Add time
            log.Add $"frame {remaining}"

            if remaining > 1 then
                scheduler.Frame (repeat (remaining - 1)) |> ignore

        scheduler.Frame (repeat 2) |> ignore
        let cancelledFrame = scheduler.Frame (fun _ -> log.Add "cancelled frame")
        cancelledFrame.Dispose ()
        equal "frame waits for the next frame" [] (drain ())
        do! driver.NextFrame ()
        equal "first frame" [ "frame 2" ] (drain ())
        do! driver.NextFrame ()
        equal "rescheduled frame runs at the next frame" [ "frame 1" ] (drain ())
        check "frame time on the scheduler clock" (times[0] >= before && times[1] >= times[0])

        // Owner-bound scheduling.
        let root, inner =
            createRoot (fun root ->
                let inner = ref Unchecked.defaultof<Owner>

                Ranvier.View.Dom.Frames.frame scheduler (fun _ ->
                    inner.Value <- getOwner ()
                    log.Add "owned frame")
                |> ignore

                root, (fun () -> inner.Value))

        do! driver.NextFrame ()
        equal "owned frame runs" [ "owned frame" ] (drain ())
        check "owned frame runs in a child scope" (not (obj.ReferenceEquals (inner (), root)))
        check "child scope disposed after the callback" (inner ()).IsDisposed

        let disposedRoot =
            createRoot (fun root ->
                Ranvier.View.Dom.Frames.frame scheduler (fun _ -> log.Add "disposed owner frame")
                |> ignore

                Ranvier.View.Dom.Frames.timeout scheduler 5.0 (fun () -> log.Add "disposed owner timer")
                |> ignore

                Ranvier.View.Dom.Frames.microtask scheduler (fun () -> log.Add "disposed owner microtask")
                |> ignore

                root)

        disposedRoot.Dispose ()
        root.Dispose ()

        let explicitRoot, handle =
            createRoot (fun root -> root, Ranvier.View.Dom.Frames.timeout scheduler 5.0 (fun () -> log.Add "explicit timer"))

        handle.Dispose ()
        handle.Dispose ()
        do! driver.Checkpoint ()
        do! driver.Elapse 20.0
        do! driver.NextFrame ()
        equal "owner disposal and handle disposal cancel" [] (drain ())
        explicitRoot.Dispose ()

        // The timed clock.
        let clock = Ranvier.View.Dom.Frames.clock scheduler
        check "timed clock reads the scheduler clock" (clock.NowMilliseconds >= scheduler.Now () - 1.0)
        let timer = clock.CreateTimer (Action (fun () -> log.Add "timed"))
        timer.Arm (TimeSpan.FromMilliseconds 5.0)
        timer.Arm (TimeSpan.FromMilliseconds 5.0)
        do! driver.Elapse 20.0
        equal "re-armed timed timer fires once" [ "timed" ] (drain ())
        timer.Arm (TimeSpan.FromMilliseconds 5.0)
        timer.Disarm ()
        do! driver.Elapse 20.0
        equal "disarmed timed timer" [] (drain ())
        timer.Dispose ()
        timer.Dispose ()

        // Scheduler disposal.
        scheduler.Frame (fun _ -> log.Add "disposed frame")
        |> ignore

        scheduler.Timeout (5.0, fun () -> log.Add "disposed timer")
        |> ignore

        scheduler.Microtask (fun () -> log.Add "disposed microtask")
        |> ignore

        scheduler.Dispose ()
        scheduler.Dispose ()
        do! driver.Checkpoint ()
        do! driver.Elapse 20.0
        do! driver.NextFrame ()
        equal "scheduler disposal cancels pending callbacks" [] (drain ())

        let rejected =
            try
                scheduler.Frame ignore |> ignore
                false
            with _ ->
                true

        check "scheduling after disposal raises" rejected
        return checks
    }
