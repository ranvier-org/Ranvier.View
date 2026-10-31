/// <summary>Frames, timers and microtasks owned by the current reactive scope.</summary>
/// <remarks>
/// Each scheduled callback gets a child scope of the current owner. The callback runs under that scope inside a batch,
/// and the scope is disposed after it returns. Disposing the returned handle, or any ancestor scope, cancels a
/// callback that has not started.
/// </remarks>
module Ranvier.View.Dom.Frames

open System
open Ranvier
open global.Ranvier.Dom

let internal owned (start: (unit -> unit) -> IDisposable) (run: unit -> unit) : IDisposable =
    createRoot (fun owner ->
        let handle =
            start (fun () ->
                if not owner.IsDisposed then
                    try
                        runWithOwner owner (fun () -> batch run)
                    finally
                        owner.Dispose ())

        onCleanup handle.Dispose
        owner :> IDisposable)

/// <summary>Runs <c>callback</c> with the frame time at the next animation frame of <c>scheduler</c>.</summary>
let frame (scheduler: FrameScheduler) (callback: float -> unit) : IDisposable =
    let time = ref 0.0

    owned
        (fun run ->
            scheduler.Frame (fun t ->
                time.Value <- t
                run ()))
        (fun () -> callback time.Value)

/// <summary>Runs <c>callback</c> once <c>milliseconds</c> have elapsed on <c>scheduler</c>'s clock.</summary>
let timeout (scheduler: FrameScheduler) (milliseconds: float) (callback: unit -> unit) : IDisposable =
    owned (fun run -> scheduler.Timeout (milliseconds, run)) callback

/// <summary>Runs <c>callback</c> at the next microtask checkpoint of <c>scheduler</c>.</summary>
let microtask (scheduler: FrameScheduler) (callback: unit -> unit) : IDisposable =
    owned scheduler.Microtask callback

/// <summary>The clock of <c>scheduler</c> for timed reactive values, with timers on its timeouts.</summary>
let clock (scheduler: FrameScheduler) : TimedClock =
    { new TimedClock() with
        member _.NowMilliseconds = scheduler.Now ()

        member _.CreateTimer callback =
            let pending: IDisposable ref = ref null

            let disarm () =
                if not (isNull pending.Value) then
                    pending.Value.Dispose ()
                    pending.Value <- null

            { new TimedTimer() with
                member _.Arm delay =
                    disarm ()

                    pending.Value <-
                        scheduler.Timeout (
                            delay.TotalMilliseconds,
                            fun () ->
                                pending.Value <- null
                                callback.Invoke ()
                        )

                member _.Disarm() = disarm ()
                member _.Dispose() = disarm ()
            }
    }

/// <summary>Runs <c>callback</c> in a later idle period of <c>scheduler</c>; with <c>timeout</c>, no later than
/// <c>timeout</c> milliseconds from now.</summary>
let idle (scheduler: IdleScheduler) (timeout: float option) (callback: IdleDeadline -> unit) : IDisposable =
    let deadline: IdleDeadline ref = ref Unchecked.defaultof<_>

    owned
        (fun run ->
            scheduler.Idle (
                timeout,
                fun d ->
                    deadline.Value <- d
                    run ()
            ))
        (fun () -> callback deadline.Value)
