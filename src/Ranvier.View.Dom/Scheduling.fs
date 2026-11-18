module Ranvier.View.Dom.Scheduling

open Ranvier.View
open System
open Ranvier

#if FABLE_COMPILER
open Fable.Core
#endif

#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type CommitFailure =
    | CollectAndContinue
    | FailFastCancel

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: NativeDisplay.Context, depth: int, precedence: int) =
        NativeDisplay.union context depth precedence (box (this)) (fun n0 ->
            match this with
            | CommitFailure.CollectAndContinue -> "CollectAndContinue", []
            | CommitFailure.FailFastCancel -> "FailFastCancel", [])

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

type Slot internal (owner: obj, scope: ViewScope) =
    member internal _.Owner = owner
    member internal _.Scope = scope
    member val internal Version = 0 with get, set
    member val internal Listed = false with get, set
    member val internal Action: unit -> unit = Unchecked.defaultof<_> with get, set
    /// <summary>Runs, when not null, after a flush stopped by <c>FailFastCancel</c> cancels the slot's current commit.</summary>
    member val internal Dropped: unit -> unit = Unchecked.defaultof<_> with get, set

#if FABLE_COMPILER
[<Fable.Core.Emit("$0.map($1)")>]
let private snapshot (pending: ResizeArray<Slot>) (project: Slot -> Slot * int * (unit -> unit)) : (Slot * int * (unit -> unit)) array = jsNative
#endif

type Queue(graph: Graph, failure: CommitFailure, post: (unit -> unit) -> unit) =
    let pending = ResizeArray<Slot>()
#if !FABLE_COMPILER
    let mutable work: struct (Slot * int * (unit -> unit)) array = [||]
#endif
    let mutable flushDelegate: unit -> unit = Unchecked.defaultof<_>
    let mutable flushFailures: ResizeArray<exn> = null
    let mutable flushFailedSlots: ResizeArray<Slot> = null
    let mutable disposed = false
    let mutable flushing = false
    let mutable scheduled = false
    let mutable generation = 0
    let mutable errors: exn array = [||]

    member private this.EnsureOwned(slot: Slot) =
        if not (obj.ReferenceEquals (slot.Owner, this)) then
            invalidArg "slot" "Commit slot belongs to another queue"

    member private this.Request() =
        if
            not disposed
            && not flushing
            && not scheduled
            && pending.Count > 0
        then
            scheduled <- true
            let ticket = generation

            try
                post (fun () ->
                    if not disposed && ticket = generation then
                        this.Flush ())
            with _ ->
                scheduled <- false
                reraise ()

    member this.Reserve() =
        if disposed then
            raise (InvalidOperationException "Commit queue is disposed")

        Slot (this, Unchecked.defaultof<_>)

    /// <summary>Reserves a slot whose commit failures go to <c>scope</c> after each flush.</summary>
    member internal this.Reserve(scope: ViewScope) =
        if disposed then
            raise (InvalidOperationException "Commit queue is disposed")

        Slot (this, scope)

    member this.Cancel(slot: Slot) =
        this.EnsureOwned slot
        slot.Version <- slot.Version + 1
        slot.Action <- Unchecked.defaultof<_>

    member this.Enqueue(slot: Slot, action: unit -> unit) =
        this.EnsureOwned slot

        if not disposed then
            slot.Version <- slot.Version + 1
            slot.Action <- action

            if not slot.Listed then
                slot.Listed <- true
                pending.Add slot

            this.Request ()

    member private this.FlushCore() =
        Api.flush ()

#if FABLE_COMPILER
        let staged = snapshot pending (fun slot -> slot, slot.Version, slot.Action)
        pending.Clear ()

        for slot, _, _ in staged do
            slot.Listed <- false
            slot.Action <- Unchecked.defaultof<_>

        let mutable stopped = false

        for slot, version, commit in staged do
            let hasCommit = not (isNull (box commit))

            if stopped then
                let dropped =
                    (hasCommit && slot.Version = version)
                    || not (isNull (box slot.Action))

                this.Cancel slot

                if dropped && not (isNull (box slot.Dropped)) then
                    slot.Dropped ()
            elif
                not disposed
                && slot.Version = version
                && hasCommit
            then
                try
                    Api.untrack commit
                with error ->
                    if isNull flushFailures then
                        flushFailures <- ResizeArray<exn>()

                    flushFailures.Add error

                    if isNull flushFailedSlots then
                        flushFailedSlots <- ResizeArray<Slot>()

                    flushFailedSlots.Add slot
                    stopped <- failure = FailFastCancel
#else
        let count = pending.Count

        if count > 0 then
            let mutable buf = work

            if buf.Length < count then
                let nextCap =
                    let doubled = if buf.Length = 0 then 16 else buf.Length * 2
                    if doubled < count then count else doubled

                buf <- Array.zeroCreate<struct (Slot * int * (unit -> unit))> nextCap
                work <- buf

            for index = 0 to count - 1 do
                let slot = pending[index]
                buf[index] <- struct (slot, slot.Version, slot.Action)
                slot.Listed <- false
                slot.Action <- Unchecked.defaultof<_>

            pending.Clear ()
            let mutable stopped = false

            for index = 0 to count - 1 do
                let struct (slot, version, commit) = buf[index]
                buf[index] <- Unchecked.defaultof<_>
                let hasCommit = not (isNull (box commit))

                if stopped then
                    let dropped =
                        (hasCommit && slot.Version = version)
                        || not (isNull (box slot.Action))

                    this.Cancel slot

                    if dropped && not (isNull (box slot.Dropped)) then
                        slot.Dropped ()
                elif
                    not disposed
                    && slot.Version = version
                    && hasCommit
                then
                    try
                        Api.untrack commit
                    with error ->
                        if isNull flushFailures then
                            flushFailures <- ResizeArray<exn>()

                        flushFailures.Add error

                        if isNull flushFailedSlots then
                            flushFailedSlots <- ResizeArray<Slot>()

                        flushFailedSlots.Add slot
                        stopped <- failure = FailFastCancel
#endif

        if
            not (isNull flushFailures)
            && flushFailures.Count > 0
        then
            Queue.Report (flushFailures, flushFailedSlots)

    member this.Flush() =
        if flushing then
            raise (InvalidOperationException "Recursive commit flushing is prohibited")

        if not disposed then
            generation <- generation + 1
            scheduled <- false
            flushing <- true
            flushFailures <- null
            flushFailedSlots <- null

            if isNull (box flushDelegate) then
                flushDelegate <- fun () -> this.FlushCore ()

            try
                graph.Run flushDelegate
            finally
                let captured = flushFailures
                flushFailures <- null
                flushFailedSlots <- null
                errors <- if isNull captured then [||] else captured.ToArray ()
                flushing <- false
                this.Request ()

    member _.Errors = Array.copy errors

#if RANVIER_VIEW_INSPECT || !FABLE_COMPILER
    /// <summary>Whether the queue is running its pending commits.</summary>
    member internal _.Flushing = flushing
#endif

    /// <summary>Sends each scope the failures of its slots in one call, in commit order.</summary>
    static member private Report(failures: ResizeArray<exn>, slots: ResizeArray<Slot>) =
        let reporterErrors = ResizeArray<exn>()

        for index in 0 .. slots.Count - 1 do
            let scope = slots[index].Scope
            let mutable first = not (isNull (box scope))
            let mutable earlier = 0

            while first && earlier < index do
                if obj.ReferenceEquals (slots[earlier].Scope, scope) then
                    first <- false

                earlier <- earlier + 1

            if first then
                let errors =
                    [
                        for later in index .. slots.Count - 1 do
                            if obj.ReferenceEquals (slots[later].Scope, scope) then
                                failures[later]
                    ]

                try
                    scope.Fail errors
                with error ->
                    reporterErrors.Add error

        if reporterErrors.Count = 1 then
            raise reporterErrors[0]
        elif reporterErrors.Count > 1 then
            raise (AggregateException ("Commit failure reporting failed", reporterErrors))

    member this.Dispose() =
        if not disposed then
            disposed <- true
            generation <- generation + 1
            scheduled <- false

            for slot in pending do
                slot.Listed <- false
                this.Cancel slot

            pending.Clear ()
#if !FABLE_COMPILER
            work <- [||]
#endif

#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type Scheduling =
    | Synchronous
    | Queued of Queue

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: NativeDisplay.Context, depth: int, precedence: int) =
        NativeDisplay.union context depth precedence (box (this)) (fun n0 ->
            match this with
            | Scheduling.Synchronous -> "Synchronous", []
            | Scheduling.Queued (v0_0) ->
                "Queued",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.Dom.Scheduling.Queue>" (box (v0_0)))
                ])

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type CommitMode =
    | SynchronousCommits
    | QueuedCommits of CommitFailure * ((unit -> unit) -> unit)

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: NativeDisplay.Context, depth: int, precedence: int) =
        NativeDisplay.union context depth precedence (box (this)) (fun n0 ->
            match this with
            | CommitMode.SynchronousCommits -> "SynchronousCommits", []
            | CommitMode.QueuedCommits (v0_0, v0_1) ->
                "QueuedCommits",
                [
                    Some (fun p -> (NativeDisplay.known context n0 (v0_0) (fun retained -> retained.NativeDisplay (context, n0, p))))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_1)))
                ])

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif
