module Ranvier.View.WebCore.Tests.BackgroundSnapshotTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Expecto
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private backgroundCase name body =
    hostCase name (fun host ->
        if not (WebCoreHost.Has "detached-image-encoding-v1") then
            if Environment.GetEnvironmentVariable "RANVIER_REQUIRE_BACKGROUND_SNAPSHOTS" = "1" then
                failtest "Detached encoding capability required by exact-runtime qualification"

            skiptest "runtime predates detached image encoding"

        body host)

let private encodedOptions =
    { SnapshotContract.defaults with
        Encoding = ImageEncoding.Png
    }

[<Tests>]
let tests =
    testList
        "Background snapshots"
        [
            backgroundCase "default budget lets native capture clip a large crop" (fun host ->
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue

                let options =
                    { SnapshotContract.defaults with
                        Scale = Some 1.0
                        Crop =
                            Some
                                {
                                    X = 0.
                                    Y = 0.
                                    Width = 8192.
                                    Height = 2048.
                                }
                    }

                use captured = service.Capture (SnapshotSubject.Viewport, options, ignore)
                host.Cycle ()

                match captured.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Succeeded image) ->
                    Expect.equal image.Data.Length (320 * 240 * 4) "native clips the crop to the viewport"
                | state -> failtestf "Expected capture at native ceiling, got %A" state)
            backgroundCase "fractional device scale cannot underestimate explicit capture scale" (fun host ->
                host.Resize (101, 101)
                host.SetDeviceScale 1.25f
                let workers = Queue<unit -> unit>()

                use service =
                    BackgroundSnapshots.createWithBudget host workers.Enqueue (807L * 807L * 4L)

                use captured =
                    service.Capture (SnapshotSubject.Viewport, { encodedOptions with Scale = Some 8.0 }, ignore)

                host.Cycle ()
                Expect.equal workers.Count 0 "rejected before encoding"

                match captured.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Failed (ResourceFailure.LoadFailed message)) ->
                    Expect.stringContains message "byte budget" "uses authoritative CSS dimensions"
                | state -> failtestf "Expected rejection, got %A" state)
            backgroundCase "queued preview rechecks byte budget after resize" (fun host ->
                let workers = Queue<unit -> unit>()

                use service =
                    BackgroundSnapshots.createWithBudget host workers.Enqueue (320L * 240L * 4L)

                use active = service.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)
                host.Cycle ()
                use pending = service.Preview (SnapshotSubject.Viewport, encodedOptions, ignore)
                host.Resize (640, 480)
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                Expect.equal workers.Count 0 "resized capture rejected before allocation/worker"

                match pending.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Failed (ResourceFailure.LoadFailed message)) ->
                    Expect.stringContains message "byte budget" "rechecked at start"
                | state -> failtestf "Expected rejection, got %A" state)
            backgroundCase "byte budget rejects before capture and raw output bypasses encoding" (fun host ->
                let workers = Queue<unit -> unit>()
                use small = BackgroundSnapshots.createWithBudget host workers.Enqueue 4L

                use rejected = small.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)

                host.Cycle ()
                Expect.equal workers.Count 0 "over-budget capture never reaches worker"

                match rejected.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Failed (ResourceFailure.LoadFailed message)) ->
                    Expect.stringContains message "byte budget" "explicit rejection"
                | state -> failtestf "Expected rejection, got %A" state

                use raw = BackgroundSnapshots.create host workers.Enqueue

                use captured =
                    raw.Capture (
                        SnapshotSubject.Viewport,
                        { encodedOptions with
                            Encoding = ImageEncoding.Rgba8
                        },
                        ignore
                    )

                host.Cycle ()
                Expect.equal workers.Count 0 "raw pixels need no detached encoder"

                match captured.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Succeeded image) ->
                    Expect.equal image.Data.Length (image.PixelWidth * image.PixelHeight * 4) "owned exact pixels"
                | state -> failtestf "Expected raw capture, got %A" state)
            backgroundCase "oversized capture rejects before worker admission and capacity remains usable" (fun host ->
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue
                let element = host.Document.createElement "div"
                element.setAttribute ("style", "position:absolute;width:4096px;height:4096px;background:red")
                host.Document.body.appendChild element |> ignore
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                let options = { encodedOptions with Scale = Some 2.0 }

                use rejected =
                    service.Capture (SnapshotSubject.Element element, options, outcomes.Add)

                host.Cycle ()
                Expect.equal workers.Count 0 "oversized pixels never reach worker"

                match rejected.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Failed (ResourceFailure.LoadFailed message)) ->
                    Expect.stringContains message "capture limits" "bounded capture rejected"
                | state -> failtestf "Expected bounded rejection, got %A" state

                use next = service.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)

                host.Cycle ()
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                Expect.equal outcomes.Count 1 "one terminal rejection")
            backgroundCase "cancellation before the first cycle releases admission without encoding" (fun host ->
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                use operation =
                    service.Capture (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                operation.Cancel ()
                host.Cycle ()
                Expect.equal workers.Count 0 "no worker needed for cancelled captured pixels"
                Expect.sequenceEqual outcomes [ ResourceOutcome.Failed ResourceFailure.Cancelled ] "one cancellation"

                use next = service.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)

                host.Cycle ()
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ())
            backgroundCase "capacity is shared and only pending previews are replaced" (fun host ->
                let workers = Queue<unit -> unit>()
                use first = BackgroundSnapshots.create host workers.Enqueue
                use second = BackgroundSnapshots.create host workers.Enqueue
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                let options =
                    { encodedOptions with
                        Encoding = ImageEncoding.Png
                    }

                use active = first.Capture (SnapshotSubject.Viewport, options, outcomes.Add)
                host.Cycle ()
                Expect.equal workers.Count 1 "one detached worker"

                Expect.throws
                    (fun () ->
                        second.Capture (SnapshotSubject.Viewport, options, ignore)
                        |> ignore)
                    "export cannot replace active work"

                use replaced = first.Preview (SnapshotSubject.Viewport, options, outcomes.Add)
                use latest = second.Preview (SnapshotSubject.Viewport, options, outcomes.Add)
                Expect.equal outcomes.Count 1 "replaced preview terminates once"
                Expect.equal replaced.Snapshot (ResourceState.Settled (ResourceOutcome.Failed ResourceFailure.Cancelled)) "replacement cancels"
                active.Cancel ()
                Expect.equal outcomes.Count 2 "active cancellation delivered"
                host.Cycle ()
                Expect.equal workers.Count 1 "cancelled codec still occupies capacity"
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                host.Cycle ()
                Expect.equal workers.Count 1 "latest preview starts after codec exits"
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                Expect.equal outcomes.Count 3 "every admitted request terminates exactly once"

                match latest.Snapshot with
                | ResourceState.Settled (ResourceOutcome.Succeeded image) ->
                    Expect.equal image.Format ImageEncoding.Png "requested format"
                    Expect.sequenceEqual image.Data[0..7] [| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |] "encoded PNG"
                | other -> failtestf "Expected encoded result, got %A" other)
            backgroundCase "disposed service suppresses a completed worker awaiting owner delivery" (fun host ->
                let workers = Queue<unit -> unit>()
                let service = BackgroundSnapshots.create host workers.Enqueue
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                use operation =
                    service.Capture (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                host.Cycle ()
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                service.Dispose ()
                host.Cycle ()
                Expect.sequenceEqual outcomes [ ResourceOutcome.Failed ResourceFailure.Cancelled ] "no stale success")
            backgroundCase "document replacement cancels active and pending requests" (fun host ->
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                use active =
                    service.Capture (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                host.Cycle ()

                use pending =
                    service.Preview (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                host.LoadSvg "<svg xmlns='http://www.w3.org/2000/svg' width='320' height='240'/>"

                Expect.sequenceEqual
                    outcomes
                    [
                        ResourceOutcome.Failed ResourceFailure.Cancelled
                        ResourceOutcome.Failed ResourceFailure.Cancelled
                    ]
                    "both cancelled"

                use replacement = BackgroundSnapshots.create host workers.Enqueue

                Expect.throws
                    (fun () ->
                        replacement.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)
                        |> ignore)
                    "old codec retains per-host capacity"

                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                Expect.equal outcomes.Count 2 "worker cannot redeliver"

                use fresh = replacement.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)

                host.Cycle ()
                Expect.equal workers.Count 1 "new document can capture after codec exits"
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ())
            backgroundCase "host disposal cancels detached encoding exactly once" (fun host ->
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                use active =
                    service.Capture (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                host.Cycle ()
                (host :> IDisposable).Dispose()
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                Expect.sequenceEqual outcomes [ ResourceOutcome.Failed ResourceFailure.Cancelled ] "owner teardown wins")
            backgroundCase "failed worker dispatch releases capacity and cancelled pending work never starts" (fun host ->
                use broken = BackgroundSnapshots.create host (fun _ -> failwith "dispatch failed")
                let outcomes = ResizeArray<ResourceOutcome<SnapshotImage>>()

                use failed = broken.Capture (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                host.Cycle ()
                Expect.sequenceEqual outcomes [ ResourceOutcome.Failed (ResourceFailure.LoadFailed "dispatch failed") ] "failure delivered"
                let workers = Queue<unit -> unit>()
                use service = BackgroundSnapshots.create host workers.Enqueue

                use active = service.Capture (SnapshotSubject.Viewport, encodedOptions, ignore)

                host.Cycle ()

                use pending =
                    service.Preview (SnapshotSubject.Viewport, encodedOptions, outcomes.Add)

                pending.Cancel ()
                Task.Run(workers.Dequeue ()).GetAwaiter().GetResult()
                host.Cycle ()
                host.Cycle ()
                Expect.equal workers.Count 0 "cancelled preview releases its pending slot"
                Expect.equal outcomes.Count 2 "single cancellation callback")
        ]
