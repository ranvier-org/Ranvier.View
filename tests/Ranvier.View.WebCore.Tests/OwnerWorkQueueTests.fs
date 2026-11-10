module Ranvier.View.WebCore.Tests.OwnerWorkQueueTests

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier.View.WebCore

[<Tests>]
let tests =
    testList "Owner work queue" [
        testCase "coalesces wakes and rearms after empty" (fun () ->
            let queue = OwnerWorkQueue ()
            let mutable wakes = 0
            queue.SetWakeCallback (Some (fun () -> wakes <- wakes + 1))
            for i in 1..1000 do queue.Enqueue (SendOrPostCallback ignore, box i) |> ignore
            Expect.equal wakes 1 "one notification"
            let mutable item = Unchecked.defaultof<_>
            for i in 1..1000 do
                Expect.isTrue (queue.TryDequeue &item) "queued"
                let struct (_, state) = item
                Expect.equal (unbox<int> state) i "FIFO"
            queue.Enqueue (SendOrPostCallback ignore, null) |> ignore
            Expect.equal wakes 2 "rearmed")
        testCase "wake can reenter queue and failure retains work" (fun () ->
            let queue = OwnerWorkQueue ()
            queue.SetWakeCallback (Some (fun () ->
                Expect.equal queue.Count 1 "enqueue before wake"
                failwith "signal unavailable"))
            queue.Enqueue (SendOrPostCallback ignore, null) |> ignore
            Expect.equal queue.Count 1 "not dropped"
            Expect.equal queue.WakeFailures 1L "failure reported"
            queue.Stop ()
            Expect.isFalse (queue.Enqueue (SendOrPostCallback ignore, null)) "stopped"
            Expect.equal queue.Count 0 "references cleared")
        testCase "simultaneous producers and stop" (fun () ->
            let queue = OwnerWorkQueue ()
            let mutable wakes = 0
            queue.SetWakeCallback (Some (fun () -> Interlocked.Increment &wakes |> ignore))
            Parallel.For (0, 10000, fun i -> queue.Enqueue (SendOrPostCallback ignore, box i) |> ignore) |> ignore
            Expect.equal queue.Count 10000 "all accepted"
            Expect.equal wakes 1 "coalesced across producers"
            Parallel.Invoke ([| Action(fun () -> queue.Stop ()); Action(fun () ->
                for i in 1..1000 do queue.Enqueue (SendOrPostCallback ignore, box i) |> ignore) |])
            Expect.equal queue.Count 0 "stop wins admission race")
        testCase "registration observes existing work" (fun () ->
            let queue = OwnerWorkQueue ()
            queue.Enqueue (SendOrPostCallback ignore, null) |> ignore
            let mutable woke = false
            queue.SetWakeCallback (Some (fun () -> woke <- true))
            Expect.isTrue woke "cannot sleep through prior post")
    ]
