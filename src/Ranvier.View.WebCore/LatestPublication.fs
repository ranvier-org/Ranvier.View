namespace Ranvier.View.WebCore

open System

/// <summary>One replaceable detached result waiting for a host-thread application callback.</summary>
/// <remarks>
/// Create on the host thread. Publish and Dispose may run on any thread. Each Publish transfers
/// ownership, including rejected values; release runs exactly once after application or replacement.
/// The apply callback borrows the value. Release must be thread-safe and must not touch live native
/// handles. A delivery already claimed may finish after Dispose. Reusing an owned value in multiple
/// publications requires separate ownership. Use core async/stream primitives for reactive state.
/// </remarks>
type LatestPublication<'T>(host: WebCoreHost, apply: 'T -> unit, ?release: 'T -> unit) as this =
    let gate = obj()
    let release = defaultArg release ignore
    let mutable pending: 'T voption = ValueNone
    let mutable scheduled = false
    let mutable stopped = false
    let mutable subscription: IDisposable option = None
    let releaseValue value = match value with ValueSome item -> release item | ValueNone -> ()
    do
        host.EnsureActive()
        subscription <- Some(host.Disposed.Subscribe(fun () -> this.Dispose()))
    member private _.Schedule() =
        if not(host.TryPostToOwner this.Deliver) then this.Dispose()
    member private _.Deliver() =
        let value = lock gate (fun () ->
            let value = pending
            pending <- ValueNone
            value)
        try
            match value with
            | ValueSome item ->
                try apply item
                finally release item
            | ValueNone -> ()
        finally
            let again = lock gate (fun () ->
                if stopped || pending.IsNone then
                    scheduled <- false
                    false
                else true)
            if again then this.Schedule()
    /// <summary>Transfers a detached value; returns false when the slot has already stopped.</summary>
    member _.Publish(value: 'T) =
        let replaced, schedule, accepted = lock gate (fun () ->
            if stopped then ValueSome value, false, false
            else
                let old = pending
                pending <- ValueSome value
                let schedule = not scheduled
                scheduled <- true
                old, schedule, true)
        try
            if schedule then this.Schedule()
        finally releaseValue replaced
        accepted
    /// <summary>Stops admission and releases the pending value without waiting for a claimed delivery.</summary>
    member _.Dispose() =
        let value, listener = lock gate (fun () ->
            stopped <- true
            let value = pending
            pending <- ValueNone
            let listener = subscription
            subscription <- None
            value, listener)
        try releaseValue value
        finally listener |> Option.iter (fun item -> item.Dispose())
    interface IDisposable with
        member _.Dispose() = this.Dispose()
