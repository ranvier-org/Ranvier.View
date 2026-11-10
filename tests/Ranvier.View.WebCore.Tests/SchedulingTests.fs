module Ranvier.View.WebCore.Tests.SchedulingTests

open System
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

[<Tests>]
let tests =
    testList "Host scheduling" [
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
