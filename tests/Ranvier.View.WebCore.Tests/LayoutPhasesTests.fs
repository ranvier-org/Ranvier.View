module Ranvier.View.WebCore.Tests.LayoutPhasesTests

open Expecto
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support
open Responsiveness.ViewportModel
open Responsiveness.EditorViewport

[<Tests>]
let tests =
    testList
        "Layout phases"
        [
            testCase "viewport and measurement memory do not grow with document size" (fun _ ->
                Expect.equal (viewportKeys 100000 500 25).Length 125 "viewport plus overscan"
                Expect.equal (viewportKeys 10000000 500 25).Length 125 "larger document"
                let cache = MeasurementCache 125

                let environment =
                    {
                        FontRevision = 0L
                        DeviceScale = 1.
                        Wrap = false
                        Width = 700
                        Height = 500
                    }

                let key =
                    {
                        Text = "line"
                        Font = "monospace"
                        Width = 700
                        StyleRevision = 0L
                    }

                cache.Configure environment
                cache.Measure (key, fun () -> 10.) |> ignore
                Expect.equal (cache.Measure (key, fun () -> failwith "unexpected miss")) 10. "cache hit"

                for changed in
                    [
                        { environment with FontRevision = 1L }
                        { environment with DeviceScale = 2. }
                        { environment with Wrap = true }
                        { environment with Width = 701 }
                        { environment with Height = 501 }
                    ] do
                    cache.Configure environment
                    cache.Measure (key, fun () -> 10.) |> ignore
                    cache.Configure changed
                    Expect.equal cache.Count 0 "environment change invalidates"

                for i in 1..10000 do
                    cache.Measure ({ key with Text = string i }, fun () -> float i)
                    |> ignore

                    Expect.isLessThanOrEqual cache.Count 125 "bounded cache")
            hostCase "row recycling preserves selection and pins composition until commit or cancel" (fun host ->
                host.Resize (800, 600)
                host.Document.body.innerHTML <- "<textarea id='input'>abc</textarea><div id='viewport'></div>"
                let input = host.Document.getElementById "input" :?> HTMLTextAreaElement
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 10000000)
                input.focus ()
                host.SetFocused true
                input.setSelectionRange (1u, 2u)
                let firstRow = container.querySelector ".editor-line"
                editor.MoveTo 5000
                Expect.equal input.selectionStart 1u "selection start survives recycling"
                Expect.equal input.selectionEnd 2u "selection end survives recycling"
                Expect.isTrue (firstRow.isSameNode (container.querySelector ".editor-line")) "core keyed slot is reused"

                for commit in [ false; true ] do
                    editor.PinComposition ()
                    let pinned = editor.First
                    host.SetComposition ("日本", 2, 2)
                    editor.MoveTo (pinned + 1000)
                    Expect.equal editor.First pinned "composition region remains mounted"

                    if commit then
                        host.CommitComposition "日本"
                    else
                        host.CancelComposition ()

                    editor.EndComposition ()
                    Expect.equal editor.First (pinned + 1000) "pending viewport applies after composition"
                    Expect.equal (container.querySelectorAll ".editor-line").length 125u "bounded DOM"

                editor.Measure ()
                Expect.isGreaterThan container.clientWidth 0 "real geometry"
                host.Frame ()
                let pixels = Array.zeroCreate<byte>(800 * 600 * 4)
                host.Paint pixels
                Expect.isTrue (pixels |> Array.exists ((<>) 0uy)) "viewport paints")
            hostCase "measurement stays synchronous and distinguishes clean checks" (fun host ->
                host.Document.body.innerHTML <- "<div id='line' style='width:20px'>text</div>"
                let line = host.Document.getElementById "line" :?> HTMLElement
                let phases = Frames.layoutForHost host
                phases.Present host.Frame
                host.SetSchedulingTrace true
                Expect.equal (phases.Measure (fun () -> line.offsetWidth)) 20 "clean geometry"
                phases.Mutate (fun () -> line.setAttribute ("style", "width:40px"))
                Expect.equal (phases.Measure (fun () -> line.offsetWidth)) 40 "fresh geometry after write"
                Expect.equal (phases.Measure (fun () -> line.offsetWidth)) 40 "second read remains clean"
                let struct (events, _) = host.DrainSchedulingTrace ()

                let reads =
                    events
                    |> Array.filter (fun e -> e.Phase = SchedulingPhase.Measurement)

                Expect.equal reads.Length 3 "all reads recorded"
                Expect.equal (reads |> Array.map (fun e -> e.Pending)) [| 0; 1; 0 |] "warn once after declared writes"

                if WebCoreHost.Has "host-layout-metrics" then
                    Expect.equal (reads |> Array.map (fun e -> e.Count)) [| 0; 1; 0 |] "actual passes, not getter count")
            hostCase "failed mutations still warn and measurement exceptions propagate" (fun host ->
                let phases = Frames.layoutForHost host
                host.SetSchedulingTrace true
                Expect.throws (fun () -> phases.Mutate (fun () -> failwith "write failed")) "original failure"
                Expect.throws (fun () -> phases.Measure (fun () -> failwith "read failed")) "original read failure"
                let struct (events, _) = host.DrainSchedulingTrace ()

                Expect.isTrue
                    (events
                     |> Array.exists (fun e ->
                         e.Phase = SchedulingPhase.Measurement
                         && e.Pending = 1))
                    "diagnostic survives exception")
        ]
