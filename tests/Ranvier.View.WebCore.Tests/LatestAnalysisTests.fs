module Ranvier.View.WebCore.Tests.LatestAnalysisTests

open System
open System.Threading
open Expecto
open Responsiveness.LatestAnalysis

[<Tests>]
let retainedRequests =
    testCase "analysis releases processed requests before shutdown" (fun () ->
        use delivered = new AutoResetEvent (false)

        use worker =
            new LatestAnalysis<byte array, unit> ((fun _ _ -> ()), (fun () -> delivered.Set () |> ignore))

        let references =
            Array.init 1000 (fun _ ->
                let payload = Array.zeroCreate<byte> 4096
                let reference = WeakReference (payload)
                worker.Publish payload |> ignore
                Expect.isTrue (delivered.WaitOne 5000) "request processed"
                reference)

        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        GC.Collect ()

        Expect.isLessThan
            (references
             |> Array.filter (fun reference -> reference.IsAlive)
             |> Array.length)
            8
            "processed payloads are not a task chain"

        worker.Complete ()
        Expect.isTrue (worker.Completion.Wait 5000) "drained")

[<Tests>]
let tests =
    testCase "analysis keeps one pending request and suppresses superseded results" (fun () ->
        use started = new ManualResetEventSlim (false)
        use release = new ManualResetEventSlim (false)
        use delivered = new ManualResetEventSlim (false)
        let results = Collections.Concurrent.ConcurrentQueue<int>()
        let mutable calls = 0

        use worker =
            new LatestAnalysis<int, int> (
                (fun request _ ->
                    Interlocked.Increment (&calls) |> ignore

                    if request = 0 then
                        started.Set ()

                        if not (release.Wait 5000) then
                            failwith "test worker timeout"

                    request),
                (fun result ->
                    results.Enqueue result
                    delivered.Set ())
            )

        worker.Publish 0 |> ignore
        Expect.isTrue (started.Wait 5000) "first request executing"

        for request in 1..1000 do
            worker.Publish request |> ignore

        release.Set ()
        Expect.isTrue (delivered.Wait 5000) "latest result delivered"
        (worker :> IDisposable).Dispose()
        Expect.isTrue (worker.Completion.Wait 5000) "worker shutdown completes"
        Expect.sequenceEqual (results.ToArray ()) [| 1000 |] "superseded computation cannot publish"
        Expect.equal calls 2 "one active plus one latest pending"

        Expect.equal
            worker.Submitted
            (worker.Completed
             + worker.Cancelled
             + worker.Superseded
             + worker.Failures)
            "every submitted request accounted for"

        Expect.isFalse (worker.Publish 1001) "closed worker rejects admission")
