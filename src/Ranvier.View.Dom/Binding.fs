module Ranvier.View.Dom.Binding

open System
open Ranvier.View
open Ranvier
open Ranvier.View.Dom.Scheduling

[<Sealed>]
type private QueuedBinding<'T>
    (
        scope: ViewScope,
        settingUp: unit -> bool,
        isLive: unit -> bool,
        queue: Queue,
        slot: Slot,
        equals: Func<'T, 'T, bool>,
        read: unit -> 'T,
        write: 'T -> unit,
        afterCommit: unit -> unit
    ) =
    let mutable first = true
    let mutable version = 0
    let mutable commitVersion = 0
    let mutable commitOwner: Owner = Unchecked.defaultof<_>
    let mutable pendingValue = Unchecked.defaultof<'T>
    let mutable hasCommitted = false
    let mutable committed = Unchecked.defaultof<'T>
    let mutable cancelDelegate: unit -> unit = Unchecked.defaultof<_>
    let mutable commitDelegate: unit -> unit = Unchecked.defaultof<_>
    let mutable ownedWriteDelegate: unit -> unit = Unchecked.defaultof<_>
    let mutable untrackedWriteDelegate: unit -> unit = Unchecked.defaultof<_>

    member _.Cancel() =
        pendingValue <- Unchecked.defaultof<'T>
        queue.Cancel slot

    member _.UntrackedWrite() =
        let expected = commitVersion
        let owner = commitOwner
        let value = pendingValue
        pendingValue <- Unchecked.defaultof<'T>

        let changed =
            not hasCommitted
            || not (equals.Invoke (committed, value))

        if
            changed
            && isLive ()
            && not owner.IsDisposed
            && version = expected
        then
            write value

            if version = expected then
                committed <- value
                hasCommitted <- true

    member _.Commit() =
        let expected = commitVersion
        let owner = commitOwner

        if
            isLive ()
            && not owner.IsDisposed
            && version = expected
        then
            Api.runWithOwner owner ownedWriteDelegate

            if not (isNull (box afterCommit)) then
                afterCommit ()
        elif version = expected then
            pendingValue <- Unchecked.defaultof<'T>

    member this.RunEffect() =
        version <- version + 1
        let current = version

        if not first then
            this.Cancel ()

        Api.onCleanup cancelDelegate
        let value = read ()
        commitVersion <- current
        commitOwner <- Api.getOwner ()
        pendingValue <- value

        if first then
            first <- false

            if isNull (box scope) then
                this.Commit ()
            else
                try
                    this.Commit ()
                with error when not (settingUp ()) ->
                    scope.Fail [ error ]
        else
            queue.Enqueue (slot, commitDelegate)

    member this.Start(registerCleanup: (unit -> unit) -> unit) =
        let cancelFn = fun () -> this.Cancel ()
        let untrackedFn = fun () -> this.UntrackedWrite ()
        cancelDelegate <- cancelFn
        untrackedWriteDelegate <- untrackedFn
        ownedWriteDelegate <- fun () -> Api.untrack untrackedFn
        commitDelegate <- fun () -> this.Commit ()
        registerCleanup cancelFn
        Api.createEffect (fun () -> this.RunEffect ())

/// <summary>Creates an owned effect whose updates are committed through the supplied queue, comparing values with <c>equals</c>.</summary>
let internal startQueuedFunc
    (scope: ViewScope)
    (settingUp: unit -> bool)
    registerCleanup
    isLive
    (queue: Queue)
    (equals: Func<'T, 'T, bool>)
    (read: unit -> 'T)
    (write: 'T -> unit)
    (afterCommit: unit -> unit)
    =
    let slot = queue.Reserve scope
    slot.Dropped <- afterCommit

    let state =
        QueuedBinding<'T>(scope, settingUp, isLive, queue, slot, equals, read, write, afterCommit)

    state.Start registerCleanup

/// <summary>Creates an owned text effect whose updates are committed through the supplied queue.</summary>
/// <remarks>The first settled value is written immediately. A suspended or failed read cancels any older queued write.
/// A failed immediate write after setup goes to <c>scope</c>; a failed queued write goes to the queue, which reports it
/// to <c>scope</c> after the flush. <c>afterCommit</c>, when not null, runs after each commit of a current value, written
/// or equal to the committed one, and when a flush stopped by <c>FailFastCancel</c> cancels that commit. The enclosing
/// owner disposes the effect.</remarks>
let internal startQueuedIn
    (scope: ViewScope)
    (settingUp: unit -> bool)
    registerCleanup
    isLive
    (queue: Queue)
    equals
    read
    write
    (afterCommit: unit -> unit)
    =
    startQueuedFunc scope settingUp registerCleanup isLive queue (Func<_, _, bool>(fun left right -> equals left right)) read write afterCommit

/// <summary>Creates an owned text effect whose updates are committed through the supplied queue, with no scope.</summary>
let internal startQueued registerCleanup isLive (queue: Queue) equals read write =
    startQueuedIn Unchecked.defaultof<_> (fun () -> false) registerCleanup isLive queue equals read write Unchecked.defaultof<_>
