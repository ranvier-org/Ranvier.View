namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>Annotates synchronous application mutation, measurement and presentation phases.</summary>
/// <remarks>No work is queued and no getter is intercepted. Wrap existing reactive batches in Mutate.
/// Enable the host scheduling trace for diagnostics: Measurement.Pending is one after declared writes;
/// Count is actual main-frame layout passes, or -1 when unavailable. A warning does not imply a layout ran.
/// Measurements must stay within one document lifetime.</remarks>
type LayoutPhases internal (host: WebCoreHost) =
    let mutable writes = false

    /// <summary>Runs an application's mutation batch immediately.</summary>
    member _.Mutate(action: unit -> 'T) : 'T =
        host.EnsureActive ()
        writes <- true
        let trace = host.SchedulingTrace
        let started = trace.Start ()

        try
            action ()
        finally
            trace.Finish (SchedulingPhase.Mutation, started, 0)

    /// <summary>Runs a synchronous measurement and records whether it forced layout after writes.</summary>
    member _.Measure(action: unit -> 'T) : 'T =
        host.EnsureActive ()
        let pending = if writes then 1 else 0
        writes <- false
        let trace = host.SchedulingTrace
        let started = trace.Start ()
        let before = if trace.Enabled then host.LayoutPassCount else ValueNone

        try
            action ()
        finally
            let after =
                if trace.Enabled then
                    try
                        host.LayoutPassCount
                    with :? ObjectDisposedException ->
                        ValueNone
                else
                    ValueNone

            let passes =
                match before, after with
                | ValueSome a, ValueSome b when b >= a && b - a <= uint32 Int32.MaxValue -> int (b - a)
                | _ -> -1

            trace.Finish (SchedulingPhase.Measurement, started, pending, processed = passes)

    /// <summary>Runs presentation immediately and ends the preceding mutation phase.</summary>
    member _.Present(action: unit -> 'T) : 'T =
        host.EnsureActive ()
        writes <- false
        let trace = host.SchedulingTrace
        let started = trace.Start ()

        try
            action ()
        finally
            trace.Finish (SchedulingPhase.Presentation, started, 0)

/// <summary>Animation frames, timers and microtasks of a native host.</summary>
module Frames =

    /// <summary>Creates owner-thread phase annotations for one application's layout work.</summary>
    let layoutForHost (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        LayoutPhases host

    /// <summary>A scheduler over <c>host</c>'s frame, timer and microtask queues; <c>Now</c> is the host's timing
    /// clock, which frame times also use.</summary>
    /// <remarks>Frames run at <c>WebCoreHost.Frame</c>, timers at <c>Cycle</c>, microtasks at each checkpoint.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : FrameScheduler =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        let clock = host.TimingClock
        let mutable disposed = false
        let mutable next = 0
        let pending = Dictionary<int, unit -> unit>()

        let schedule (start: (unit -> unit) -> (unit -> unit)) (run: unit -> unit) =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException (nameof FrameScheduler))

            next <- next + 1
            let id = next

            let invoke () =
                if pending.Remove id then
                    run ()

            pending[id] <- start invoke

            { new IDisposable with
                member _.Dispose() =
                    match pending.TryGetValue id with
                    | true, cancel ->
                        pending.Remove id |> ignore
                        cancel ()
                    | _ -> ()
            }

        { new FrameScheduler with
            member _.Now() =
                clock.Now ()

            member _.Frame callback =
                let time = ref 0.0

                schedule
                    (fun invoke ->
                        let id =
                            host.RequestAnimationFrame (fun t ->
                                time.Value <- t
                                invoke ())

                        fun () -> host.CancelAnimationFrame id)
                    (fun () -> callback time.Value)

            member _.Timeout(milliseconds, callback) =
                schedule
                    (fun invoke ->
                        let id = host.SetTimeout (invoke, max 0.0 milliseconds)
                        fun () -> host.ClearTimeout id)
                    callback

            member _.Microtask callback =
                schedule
                    (fun invoke ->
                        host.QueueMicrotask invoke
                        ignore)
                    callback

            member _.Dispose() =
                if not disposed then
                    disposed <- true

                    for cancel in Seq.toArray pending.Values do
                        cancel ()

                    pending.Clear ()
        }

    /// <summary>Idle callbacks of <c>host</c>, in the idle periods of <c>WebCoreHost.Cycle</c>.</summary>
    /// <remarks>A period starts in a <c>Cycle</c> with no frame requested; its deadline is the earlier of 50 ms on the
    /// host's timing clock and the next timer's due time.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let idleForHost (host: WebCoreHost) : IdleScheduler =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        let mutable disposed = false
        let pending = HashSet<int>()

        { new IdleScheduler with
            member _.Idle(timeout, callback) =
                host.EnsureActive ()

                if disposed then
                    raise (ObjectDisposedException (nameof IdleScheduler))

                let id = ref 0

                id.Value <-
                    match timeout with
                    | Some milliseconds ->
                        host.RequestIdleCallback (
                            (fun deadline ->
                                pending.Remove id.Value |> ignore
                                callback deadline),
                            milliseconds
                        )
                    | None ->
                        host.RequestIdleCallback (fun deadline ->
                            pending.Remove id.Value |> ignore
                            callback deadline)

                pending.Add id.Value |> ignore

                { new IDisposable with
                    member _.Dispose() =
                        if pending.Remove id.Value then
                            host.CancelIdleCallback id.Value
                }

            member _.Dispose() =
                if not disposed then
                    disposed <- true

                    for id in Seq.toArray pending do
                        host.CancelIdleCallback id

                    pending.Clear ()
        }
