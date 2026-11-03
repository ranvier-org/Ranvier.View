module Ranvier.View.WebCore.Tests.CleanupChecks

open System
open System.Runtime.CompilerServices
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open RanvierWebCoreAbi

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private failPhase (error: exn) =
    raise error

let runPure () =
    let mutable checks = 0

    let check condition detail =
        if not condition then
            failwith detail

        checks <- checks + 1

    let names = [| "UIA"; "feed"; "graph"; "presenter"; "host" |]

    for failed in 0 .. names.Length - 1 do
        let reached = ResizeArray<string>()
        let expected = InvalidOperationException names[failed]
        let mutable closed = 0

        let phases =
            names
            |> Array.mapi (fun index name ->
                fun () ->
                    reached.Add name

                    if index = failed then
                        failPhase expected)

        let mutable actual: exn = null

        try
            PageCleanup.run phases (fun () -> closed <- closed + 1)
        with error ->
            actual <- error

        check (obj.ReferenceEquals (expected, actual)) "The original phase failure survives"
        check (actual.StackTrace.Contains "failPhase") "The original throwing stack survives"
        check (Seq.toArray reached = names) "Every later page phase still runs"
        check (closed = 1) "The page marker runs exactly once"

    let first = InvalidOperationException "first"
    let second = InvalidOperationException "second"
    let cleanup = Cleanup ()
    let mutable final = 0
    cleanup.Attempt (fun () -> failPhase first)
    cleanup.Attempt (fun () -> failPhase second)
    cleanup.Attempt (fun () -> final <- final + 1)
    let mutable raised: exn = null

    try
        cleanup.Finish ()
    with error ->
        raised <- error

    check (obj.ReferenceEquals (first, raised) && final = 1) "Later failure cannot replace the first or stop cleanup"

    use session = new Session (0n)
    let queuedFailure = InvalidOperationException "queued"
    let mutable executed = 0
    let mutable wrongThread: exn = null

    let worker =
        Thread (
            ThreadStart (fun () ->
                session.QueueOwnerCleanup (fun () -> failPhase queuedFailure)
                |> ignore

                session.QueueOwnerCleanup (fun () -> executed <- executed + 1)
                |> ignore

                try
                    session.Dispose ()
                with error ->
                    wrongThread <- error)
        )

    worker.Start ()
    check (worker.Join 5000) "The off-thread cleanup requester terminates"

    check
        (wrongThread :? InvalidOperationException
         && not session.IsDisposed)
        "Wrong-thread Dispose cannot change session admission"

    session.Drain ()
    check (executed = 1) "One failed queued action cannot strand the next"

    check
        (session.ListenerErrors.Count = 1
         && obj.ReferenceEquals (session.ListenerErrors[0], queuedFailure))
        "Queued failure uses the host error route"

    session.Dispose ()
    check (not (session.QueueOwnerCleanup ignore)) "Closed-session admission rejects without taking ownership"
    session.Dispose ()

    for _ in 1..32 do
        use racing = new Session (0n)
        use start = new ManualResetEventSlim ()
        let mutable accepted = false
        let mutable ran = 0

        let requester =
            Thread (
                ThreadStart (fun () ->
                    start.Wait ()
                    accepted <- racing.QueueOwnerCleanup (fun () -> ran <- ran + 1))
            )

        requester.Start ()
        start.Set ()
        racing.Dispose ()
        check (requester.Join 5000) "The concurrent shutdown requester terminates"
        check (ran = if accepted then 1 else 0) "Shutdown either drains the accepted action or leaves ownership with the rejected caller"

    checks
