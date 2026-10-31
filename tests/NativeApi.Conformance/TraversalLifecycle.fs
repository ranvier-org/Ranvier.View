module internal NativeApiConformance.TraversalLifecycle

open System
open System.Runtime.CompilerServices
open Ranvier.Dom
open Ranvier.View.WebCore

let private collect (session: Session) =
    for _ in 1..3 do
        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        session.Drain ()

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private walkerWith (document: Document) (root: Node) (result: NodeFilterResult) =
    let token = obj ()

    let filter: NodeFilter =
        fun _ ->
            GC.KeepAlive token
            result

    let walker = document.createTreeWalker (root, 1u, filter)
    walker.filter |> ignore
    WeakReference (box filter), walker

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private throwingIterator (document: Document) (root: Node) =
    let failure = InvalidOperationException "lifetime filter failed"
    let filter: NodeFilter = fun _ -> raise failure
    let iterator = document.createNodeIterator (root, 1u, filter)

    let rethrown =
        try
            iterator.nextNode () |> ignore
            false
        with error ->
            obj.ReferenceEquals (error, failure)

    WeakReference (box filter), WeakReference (box iterator), rethrown

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private heldWalker (document: Document) (root: Node) (session: Session) (expect: bool -> string -> unit) =
    let weakFilter, walker = walkerWith document root NodeFilterResult.Accept
    collect session
    expect weakFilter.IsAlive "the walker keeps its filter function"
    expect (walker.firstChild () |> isNull |> not) "the held filter runs after collection"

    expect
        (match walker.filter with
         | Some filter -> obj.ReferenceEquals (filter, weakFilter.Target)
         | None -> false)
        "the filter getter returns the held function"

    weakFilter, WeakReference (box walker)

/// <summary>
/// Checks native NodeFilter and traversal object lifetime: the filter function is released with the last walker or
/// iterator, after a filter failure, and at host disposal. Returns the assertion count.
/// </summary>
let run () =
    let mutable checks = 0

    let expect condition message =
        checks <- checks + 1

        if not condition then
            failwith message

    let host = WebCoreHost.Create (64, 64)
    let session = host.Session
    let document = host.Document
    let root = document.createElement "div"
    root.innerHTML <- "<p></p><p></p>"
    document.body.appendChild root |> ignore

    // Held by the walker, released with it.
    let weakFilter, weakWalker = heldWalker document root session expect
    collect session
    expect (not weakWalker.IsAlive) "the walker is collected"
    expect (not weakFilter.IsAlive) "the filter function is released with the walker"

    // A throwing filter is released with its iterator.
    let weakThrowing, weakIterator, rethrown = throwingIterator document root
    expect rethrown "the iterator rethrows the filter exception"
    collect session
    expect (not weakIterator.IsAlive) "the failed iterator is collected"
    expect (not weakThrowing.IsAlive) "the throwing filter is released with the iterator"

    // A result outside NodeFilterResult fails the traversal.
    let _, invalid = walkerWith document root (enum<NodeFilterResult> 7)

    let outOfRange =
        try
            invalid.firstChild () |> ignore
            false
        with :? ArgumentOutOfRangeException ->
            true

    expect outOfRange "an invalid filter result raises ArgumentOutOfRangeException"

    // Wrong thread.
    let wrongThread =
        Threading.Tasks.Task
            .Run(fun () ->
                try
                    invalid.nextNode () |> ignore
                    false
                with :? InvalidOperationException ->
                    true)
            .GetAwaiter()
            .GetResult()

    expect wrongThread "a traversal off the host thread raises InvalidOperationException"

    // Host disposal releases live walkers and their filters.
    let weakLive, live = walkerWith document root NodeFilterResult.Accept
    let iterator = document.createNodeIterator root
    (host :> IDisposable).Dispose()
    expect ((box live :?> NativeObject).Handle = 0n) "host disposal detaches the walker"
    expect ((box iterator :?> NativeObject).Handle = 0n) "host disposal detaches the iterator"

    let disposed =
        try
            live.nextNode () |> ignore
            false
        with :? ObjectDisposedException ->
            true

    expect disposed "a walker after host disposal raises ObjectDisposedException"
    GC.KeepAlive live
    GC.KeepAlive invalid
    collect session
    expect (not weakLive.IsAlive) "host disposal releases the filter function"
    expect (session.LiveObjects = 0) $"traversal fixture leaked {session.LiveObjects} managed native objects"
    checks
