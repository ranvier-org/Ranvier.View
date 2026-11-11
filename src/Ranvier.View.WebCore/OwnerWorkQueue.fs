namespace Ranvier.View.WebCore

open System
open System.Threading

/// <summary>Whether a callback entered the owner queue, hit its admission limit, or found a closed host.</summary>
type OwnerPostAdmission =
    | Accepted
    | Full
    | Closed

    override this.ToString() =
        match this with
        | Accepted -> "Accepted"
        | Full -> "Full"
        | Closed -> "Closed"

type internal OwnerWorkQueue(?timestamp: unit -> int64, ?dequeued: int64 -> unit) =
    let gate = obj ()
    let items = Collections.Generic.Queue<struct (SendOrPostCallback * obj * int64)>()
    let mutable stopped = false
    let mutable wake: (unit -> unit) option = None
    let mutable failures = 0L
    let mutable rejected = 0L
    let mutable discarded = 0L
    let mutable peak = 0

    let signal callback =
        match callback with
        | Some action ->
            try
                action ()
            with _ ->
                Interlocked.Increment (&failures) |> ignore
        | None -> ()

    member _.TryEnqueue(callback, state, capacity: int) =
        if capacity < 1 then
            invalidArg "capacity" "positive capacity required"

        let accepted, notify =
            lock gate (fun () ->
                if stopped then
                    OwnerPostAdmission.Closed, None
                elif items.Count >= capacity then
                    rejected <- rejected + 1L
                    OwnerPostAdmission.Full, None
                else
                    let empty = items.Count = 0

                    items.Enqueue (
                        struct (callback,
                                state,
                                timestamp
                                |> Option.map (fun clock -> clock ())
                                |> Option.defaultValue 0L)
                    )

                    peak <- max peak items.Count
                    OwnerPostAdmission.Accepted, (if empty then wake else None))

        signal notify
        accepted

    member this.Enqueue(callback, state) =
        this.TryEnqueue (callback, state, Int32.MaxValue) = OwnerPostAdmission.Accepted

    member _.Rejected = lock gate (fun () -> rejected)
    member _.Discarded = lock gate (fun () -> discarded)
    member _.Peak = lock gate (fun () -> peak)

    member _.TryDequeue(item: byref<struct (SendOrPostCallback * obj)>) =
        let result =
            lock gate (fun () ->
                if items.Count = 0 then
                    ValueNone
                else
                    ValueSome (items.Dequeue ()))

        match result with
        | ValueNone -> false
        | ValueSome struct (callback, state, started) ->
            item <- struct (callback, state)

            if started <> 0L then
                dequeued
                |> Option.iter (fun observe -> observe started)

            true

    member _.Count = lock gate (fun () -> items.Count)
    member _.IsStopped = lock gate (fun () -> stopped)
    member _.WakeFailures = Interlocked.Read (&failures)

    member _.SetWakeCallback callback =
        let notify =
            lock gate (fun () ->
                if stopped then
                    None
                else
                    wake <- callback
                    if items.Count > 0 then callback else None)

        signal notify

    member _.Stop() =
        lock gate (fun () ->
            stopped <- true
            wake <- None
            discarded <- discarded + int64 items.Count
            items.Clear ())
