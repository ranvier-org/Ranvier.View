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

    let timestampFn: unit -> int64 =
        match timestamp with
        | Some fn -> fn
        | None -> Unchecked.defaultof<_>

    let dequeuedFn: int64 -> unit =
        match dequeued with
        | Some fn -> fn
        | None -> Unchecked.defaultof<_>

    let hasTimestamp = not (isNull (box timestampFn))
    let hasDequeued = not (isNull (box dequeuedFn))
    let mutable stopped = false
    let mutable wakeFn: unit -> unit = Unchecked.defaultof<_>
    let mutable failures = 0L
    let mutable rejected = 0L
    let mutable discarded = 0L
    let mutable peak = 0

    let signal (action: unit -> unit) =
        if not (isNull (box action)) then
            try
                action ()
            with _ ->
                Interlocked.Increment (&failures) |> ignore

    member _.TryEnqueue(callback, state, capacity: int) =
        if capacity < 1 then
            invalidArg "capacity" "positive capacity required"

        let mutable accepted = OwnerPostAdmission.Closed
        let mutable notify: unit -> unit = Unchecked.defaultof<_>
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)

            if stopped then
                accepted <- OwnerPostAdmission.Closed
            elif items.Count >= capacity then
                rejected <- rejected + 1L
                accepted <- OwnerPostAdmission.Full
            else
                let count = items.Count
                let started = if hasTimestamp then timestampFn () else 0L
                items.Enqueue (struct (callback, state, started))
                let nextCount = count + 1

                if nextCount > peak then
                    peak <- nextCount

                if count = 0 then
                    notify <- wakeFn

                accepted <- OwnerPostAdmission.Accepted
        finally
            if taken then
                Monitor.Exit gate

        signal notify
        accepted

    member this.Enqueue(callback, state) =
        this.TryEnqueue (callback, state, Int32.MaxValue) = OwnerPostAdmission.Accepted

    member _.Rejected =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            rejected
        finally
            if taken then
                Monitor.Exit gate

    member _.Discarded =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            discarded
        finally
            if taken then
                Monitor.Exit gate

    member _.Peak =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            peak
        finally
            if taken then
                Monitor.Exit gate

    member _.TryDequeue(item: byref<struct (SendOrPostCallback * obj)>) =
        let mutable found = false
        let mutable entry = Unchecked.defaultof<struct (SendOrPostCallback * obj * int64)>
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)

            if items.Count > 0 then
                entry <- items.Dequeue ()
                found <- true
        finally
            if taken then
                Monitor.Exit gate

        if found then
            let struct (callback, state, started) = entry
            item <- struct (callback, state)

            if started <> 0L && hasDequeued then
                dequeuedFn started

            true
        else
            false

    member _.Count =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            items.Count
        finally
            if taken then
                Monitor.Exit gate

    member _.IsStopped =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            stopped
        finally
            if taken then
                Monitor.Exit gate

    member _.WakeFailures = Interlocked.Read (&failures)

    member _.SetWakeCallback(callback: (unit -> unit) option) =
        let nextWake: unit -> unit =
            match callback with
            | Some fn -> fn
            | None -> Unchecked.defaultof<_>

        let mutable notify: unit -> unit = Unchecked.defaultof<_>
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)

            if not stopped then
                wakeFn <- nextWake

                if items.Count > 0 then
                    notify <- nextWake
        finally
            if taken then
                Monitor.Exit gate

        signal notify

    member _.Stop() =
        let mutable taken = false

        try
            Monitor.Enter (gate, &taken)
            stopped <- true
            wakeFn <- Unchecked.defaultof<_>
            discarded <- discarded + int64 items.Count
            items.Clear ()
        finally
            if taken then
                Monitor.Exit gate
