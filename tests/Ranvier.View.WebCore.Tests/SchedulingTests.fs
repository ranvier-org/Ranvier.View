module Ranvier.View.WebCore.Tests.SchedulingTests

open System
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

[<Tests>]
let tests =
    testList "Host scheduling" [
        hostCase "interactive exceptions preserve checkpoint and following work" (fun host ->
            let mutable checkpoint = false
            host.PostToOwner (fun () ->
                host.QueueMicrotask (fun () -> checkpoint <- true)
                failwith "expected callback failure")
            host.PostToOwner (fun () -> Expect.isTrue checkpoint "checkpoint survived exception")
            let result = host.CycleInteractive { MaxCallbacks = 2; MaxMilliseconds = 10000.0 }
            Expect.equal result.Executed 2 "both attempted"
            Expect.equal host.Errors.Count 1 "reported on owner"
            host.Errors.Clear ())
        hostCase "interactive disposal does not run queued callbacks" (fun host ->
            let mutable ran = false
            host.PostToOwner (fun () -> (host :> IDisposable).Dispose ())
            host.PostToOwner (fun () -> ran <- true)
            let result = host.CycleInteractive OwnerTurnBudget.Default
            Expect.isFalse ran "disposed queue dropped"
            Expect.isFalse result.Remaining "no retained callbacks")
        hostCase "interactive turns preserve checkpoints and FIFO" (fun host ->
            let order = ResizeArray<int> ()
            for i in 1..100 do
                host.PostToOwner (fun () ->
                    order.Add (i * 2)
                    host.QueueMicrotask (fun () -> order.Add (i * 2 + 1)))
            let result = host.CycleInteractive { MaxCallbacks = 64; MaxMilliseconds = 10000.0 }
            Expect.equal result.Executed 64 "callback cap"
            Expect.isTrue result.Remaining "work remains"
            Expect.isTrue result.BudgetExceeded "cap reached"
            Expect.sequenceEqual order [| for i in 1..64 do yield i*2; yield i*2+1 |] "whole checkpoints"
            host.Cycle ()
            Expect.equal order.Count 200 "public Cycle still drains")
        hostCase "self posting work yields and suppresses idle" (fun host ->
            let mutable calls = 0
            let rec callback () =
                calls <- calls + 1
                if calls < 1000 then host.PostToOwner callback
            host.PostToOwner callback
            let result = host.CycleInteractive { MaxCallbacks = 3; MaxMilliseconds = 10000.0 }
            Expect.equal calls 3 "self post bounded"
            Expect.isTrue result.Remaining "next turn required")
        testCase "time budget uses injected monotonic clock" (fun () ->
            let mutable ticks = 0L
            let limit = TurnLimit (OwnerTurnBudget.Default, (fun () -> ticks), 1000L)
            Expect.isFalse limit.Exceeded "initial"
            limit.Complete ()
            ticks <- 2L
            Expect.isTrue limit.Exceeded "time reached after complete checkpoint"
            Expect.equal limit.Executed 1 "single long callback accounted"
            Expect.throws (fun () -> { OwnerTurnBudget.Default with MaxMilliseconds = Double.NaN }.Validate ()) "invalid budget")
        hostCase "trace records posted work microtasks and frames in order" (fun host ->
            host.SetSchedulingTrace true
            host.PostToOwner (fun () -> host.QueueMicrotask (fun () -> ()))
            host.Cycle ()
            host.Frame ()
            let struct (events, dropped) = host.DrainSchedulingTrace ()
            Expect.equal dropped 0L "no loss"
            Expect.isTrue (events |> Array.exists (fun e -> e.Phase = SchedulingPhase.Posted)) "posted callback"
            Expect.isTrue (events |> Array.exists (fun e -> e.Phase = SchedulingPhase.Microtasks)) "checkpoint"
            Expect.isTrue (events |> Array.exists (fun e -> e.Phase = SchedulingPhase.Frame && e.FrameId = 1L)) "frame identity"
            Expect.isTrue (events |> Array.pairwise |> Array.forall (fun (a,b) -> a.Sequence < b.Sequence)) "sequence"
            Expect.isTrue (events |> Array.forall (fun e -> e.Duration >= 0L)) "durations"
            let struct (empty, lost) = host.DrainSchedulingTrace ()
            Expect.isEmpty empty "drained"
            Expect.equal lost 0L "drop count drained")
        testCase "trace capacity and disabled state" (fun () ->
            let trace = HostSchedulingTrace ()
            trace.Record (SchedulingPhase.Posted, 1L, 1L, 0)
            let struct (disabled, _) = trace.Drain ()
            Expect.isEmpty disabled "disabled"
            trace.SetEnabled true
            for i in 1..4100 do trace.Record (SchedulingPhase.Posted, int64 i, 1L, i)
            let struct (events, lost) = trace.Drain ()
            Expect.equal events.Length 4096 "bounded"
            Expect.equal lost 4L "explicit loss"
            let other = HostSchedulingTrace ()
            other.SetEnabled true
            other.Record (SchedulingPhase.Posted, 1L, 1L, 0)
            let struct (second, _) = other.Drain ()
            Expect.notEqual second[0].HostId events[0].HostId "hosts distinct"
            trace.SetEnabled false
            let struct (empty, _) = trace.Drain ()
            Expect.isEmpty empty "disable clears")
    ]
