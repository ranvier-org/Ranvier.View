module Ranvier.View.WebCore.Tests.LatestPublicationTests

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support
open Responsiveness.SemanticPublication

[<Tests>]
let tests =
    testList "Latest publication" [
        hostCase "revision checks reject stale workers even when cancellation loses" (fun host ->
            let initial = { DocumentId = Guid.NewGuid(); TextRevision = 1L; ProjectRevision = 1L; RequestId = 1L }
            let mutable current = initial
            let applied = ResizeArray<Revision> ()
            use slot = new LatestPublication<Result>(host, (fun result -> if isCurrent current result then applied.Add result.Revision))
            let complete revision =
                Task.Run(fun () -> slot.Publish { Revision = revision; VisibleColor = "red" } |> ignore).GetAwaiter().GetResult()
                host.Cycle ()
            current <- { current with RequestId = 2L }
            complete current
            complete initial
            Expect.sequenceEqual applied [current] "out-of-order old request ignored"
            current <- { current with TextRevision = 2L }
            complete initial
            current <- { current with ProjectRevision = 2L }
            complete { current with ProjectRevision = 1L }
            let closed = current
            current <- { initial with DocumentId = Guid.NewGuid() }
            complete closed
            Expect.equal applied.Count 1 "text project and open-document generations validated"
            complete current
            Expect.equal applied.Count 2 "current document accepted")
        hostCase "release failure cannot strand a pending delivery" (fun host ->
            let applied = ResizeArray<int> ()
            use slot = new LatestPublication<int>(host, applied.Add, release = (fun value -> if value = 1 then failwith "expected release failure"))
            slot.Publish 1 |> ignore
            Expect.throws (fun () -> slot.Publish 2 |> ignore) "publisher sees release error"
            host.Cycle ()
            Expect.sequenceEqual applied [2] "scheduled before fallible release")
        hostCase "disposing from apply releases claimed and pending values once" (fun host ->
            let released = ResizeArray<int> ()
            let mutable close = ignore
            let mutable publish = fun (_: int) -> false
            use slot = new LatestPublication<int>(host, (fun _ -> publish 2 |> ignore; close()), release = released.Add)
            close <- slot.Dispose
            publish <- slot.Publish
            slot.Publish 1 |> ignore
            host.Cycle ()
            Expect.sequenceEqual released [2;1] "pending released by close then borrowed value released"
            Expect.isFalse host.HasPendingWork "no spurious delivery")
        hostCase "ten thousand results retain one delivery and release every payload" (fun host ->
            let released = ConcurrentDictionary<int, int> ()
            let applied = ResizeArray<int> ()
            use slot = new LatestPublication<int> (host, applied.Add, release = (fun value -> released.AddOrUpdate(value, 1, fun _ count -> count + 1) |> ignore))
            for value in 1..10000 do slot.Publish value |> ignore
            Expect.equal released.Count 9999 "superseded values released"
            let result = host.CycleInteractive { MaxCallbacks = 1; MaxMilliseconds = 10000.0 }
            Expect.equal result.Executed 1 "one delivery"
            Expect.isFalse result.Remaining "no callback flood"
            Expect.sequenceEqual applied [10000] "latest value"
            Expect.equal released.Count 10000 "applied value released too"
            Expect.isTrue (released.Values |> Seq.forall ((=) 1)) "exactly once")
        hostCase "publication during apply reschedules despite an apply exception" (fun host ->
            let applied = ResizeArray<int> ()
            let released = ResizeArray<int> ()
            let mutable publish = fun (_: int) -> false
            use slot = new LatestPublication<int> (host, (fun value ->
                applied.Add value
                if value = 1 then
                    publish 2 |> ignore
                    failwith "expected apply failure"), release = released.Add)
            publish <- slot.Publish
            slot.Publish 1 |> ignore
            let first = host.CycleInteractive { MaxCallbacks = 1; MaxMilliseconds = 10000.0 }
            Expect.isTrue first.Remaining "follow-up scheduled"
            host.Cycle ()
            Expect.sequenceEqual applied [1;2] "follow-up survives"
            Expect.sequenceEqual released [1;2] "release survives exception"
            Expect.equal host.Errors.Count 1 "apply error reported"
            host.Errors.Clear ())
        hostCase "concurrent publication racing disposal releases exactly once" (fun host ->
            let released = ConcurrentDictionary<int, int> ()
            let mutable applied = 0
            use slot = new LatestPublication<int> (host, (fun _ -> applied <- applied + 1), release = (fun value -> released.AddOrUpdate(value, 1, fun _ count -> count + 1) |> ignore))
            let worker = Task.Run (fun () -> Parallel.For(0, 10000, fun value -> slot.Publish value |> ignore) |> ignore)
            slot.Dispose ()
            worker.GetAwaiter().GetResult()
            host.Cycle ()
            Expect.equal applied 0 "disposed before owner delivery"
            Expect.equal released.Count 10000 "all values released"
            Expect.isTrue (released.Values |> Seq.forall ((=) 1)) "exactly once")
        hostCase "host disposal retires publication slot" (fun host ->
            let released = ResizeArray<int> ()
            use slot = new LatestPublication<int> (host, (fun _ -> failwith "disposed delivery"), release = released.Add)
            slot.Publish 1 |> ignore
            (host :> IDisposable).Dispose ()
            Expect.isFalse (slot.Publish 2) "closed admission"
            Expect.sequenceEqual released [1;2] "pending and rejected released")
    ]
