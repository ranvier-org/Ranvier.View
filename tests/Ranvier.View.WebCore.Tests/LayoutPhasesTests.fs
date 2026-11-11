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
            testCase "horizontal projection bounds shaping and preserves logical columns" (fun _ ->
                let window = Responsiveness.HorizontalLine.project 100000 true 8. 700 400000.
                Expect.isLessThanOrEqual window.Length 2048 "bounded shaping payload"
                Expect.isLessThan window.Start 50000 "left overscan"
                Expect.isGreaterThan (window.Start + window.Length) 50087 "right overscan"
                Expect.equal (window.LogicalColumn (window.LocalColumn 50020)) 50020 "logical caret survives projection"

                let last =
                    Responsiveness.HorizontalLine.project 100000 true 8. 700 System.Double.MaxValue

                Expect.equal (last.Start + last.Length) 100000 "far scroll stays inside line"

                for text in [ "abc\tdef"; "fi العربية"; "a\u0301"; "😀"; "a\u202eb" ] do
                    Expect.isFalse (Responsiveness.HorizontalLine.eligible text) "contextual text keeps whole-line rendering"

                for pitch in [ 0.; System.Double.NaN; System.Double.PositiveInfinity ] do
                    Expect.equal (Responsiveness.HorizontalLine.project 100000 true pitch 700 0.).Length 100000 "invalid metrics fall back"

                Expect.equal (Responsiveness.HorizontalLine.project 100000 false 8. 700 0.).Length 100000 "unsafe line falls back")
            hostCase "extreme ASCII rows shape only the horizontal window and restore contextual text" (fun host ->
                let original = String.replicate 10000 "abcdef "
                let buffer = Responsiveness.DocumentModel.DocumentBuffer original
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement

                use editor =
                    new EditorViewport (host, container, 1, document = buffer, horizontalCulling = true)

                let row = container.querySelector ".editor-line"
                Expect.isLessThanOrEqual row.textContent.Length 2048 "bounded DOM text"
                editor.MoveHorizontal 20000
                Expect.isLessThanOrEqual row.textContent.Length 2048 "far scroll remains bounded"
                Expect.isGreaterThan container.scrollLeft 19000 "logical line width is retained"
                Expect.equal (buffer.Snapshot.Line 0).Text original "document retains full text"
                let writes = editor.TextWrites
                editor.MoveHorizontal 20000
                Expect.equal editor.TextWrites writes "unchanged window performs no text write"
                editor.PinComposition ()
                Expect.equal row.textContent original "composition keeps full context"
                editor.EndComposition ()
                Expect.isLessThanOrEqual row.textContent.Length 2048 "projection resumes"
                editor.Edit (original + " العربية")
                Expect.equal row.textContent (original + " العربية") "whole-line classification catches offscreen bidi"
                editor.Edit original
                editor.Configure (300, 200, 1., true)
                Expect.equal row.textContent original "wrapped line remains complete")
            hostCase "horizontal projection matches full-line pixels at distant offsets" (fun host ->
                let text = String.replicate 10000 "fi!<>abc09 "

                let capture culling offset =
                    host.Document.body.innerHTML <- "<div id='viewport'></div>"
                    let container = host.Document.getElementById "viewport" :?> HTMLElement

                    use editor =
                        new EditorViewport (
                            host,
                            container,
                            1,
                            document = Responsiveness.DocumentModel.DocumentBuffer text,
                            horizontalCulling = culling
                        )

                    editor.Configure (300, 200, 1., false)
                    editor.MoveHorizontal offset
                    let struct (w, h) = host.PaintSize
                    let pixels = Array.zeroCreate<byte>(w * h * 4)
                    host.Paint pixels
                    pixels

                for offset in [ 0; 1003; 20000; 200003; 700000 ] do
                    let full = capture false offset
                    let projected = capture true offset
                    Expect.isTrue (Array.forall2 (=) projected full) $"pixel parity at {offset}")
            hostCase "offscreen extreme edits invalidate logical width measurements" (fun host ->
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 1, horizontalCulling = true)
                let text = String.replicate 10000 "abcdef "
                editor.Edit text
                editor.Measure ()
                let reads = editor.WidthReads
                let writes = editor.TextWrites
                editor.Edit (text + "a")
                editor.Measure ()
                Expect.equal editor.TextWrites writes "visible substring is unchanged"
                Expect.equal editor.WidthReads (reads + 1L) "logical extent changes outside the window")
            hostCase "horizontal projection declares writes before measurement" (fun host ->
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement

                use editor =
                    new EditorViewport (host, container, 1, textKind = "extreme-line", horizontalCulling = true)

                let phases = Frames.layoutForHost host
                editor.Measure ()
                phases.Present host.Frame
                host.SetSchedulingTrace true
                editor.MoveHorizontal 20000
                editor.Measure ()
                let struct (events, _) = host.DrainSchedulingTrace ()

                Expect.isTrue
                    (events
                     |> Array.exists (fun e ->
                         e.Phase = SchedulingPhase.Measurement
                         && e.Pending = 1))
                    "projection writes are reported")
            hostCase "empty document lines retain line height" (fun host ->
                let buffer = Responsiveness.DocumentModel.DocumentBuffer "\n\ntext"
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 3, document = buffer)
                let row = container.querySelector ".editor-line" :?> HTMLElement
                Expect.equal row.offsetHeight 20 "empty logical line has a line box")
            hostCase "wrapped rows use measured heights for the viewport anchor" (fun host ->
                let buffer =
                    Responsiveness.DocumentModel.DocumentBuffer (String.concat "\n" (Array.create 200 (String.replicate 20 "word ")))

                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 200, document = buffer)
                editor.Configure (100, 500, 1., true)
                editor.Measure ()
                Expect.isGreaterThan container.scrollTop 1000 "wrapped overscan contributes actual height"
                editor.MoveTo 199
                editor.Measure ()
                Expect.equal editor.First 199 "last wrapped line is reachable"
                editor.MoveTo 50
                editor.Configure (700, 500, 1., false)
                editor.Measure ()
                Expect.equal container.scrollTop 1000 "unwrapped fixed height restored")
            hostCase "document edits retain viewport anchor and immutable worker input" (fun host ->
                let buffer =
                    Responsiveness.DocumentModel.DocumentBuffer (String.concat "\n" [ for i in 0..199 -> $"row {i}" ])

                let before = buffer.Snapshot
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 200, document = buffer)
                let anchor = (buffer.Snapshot.Line editor.First).Id
                buffer.Replace ({ Line = 0; Column = 0 }, { Line = 0; Column = 0 }, "inserted\n")
                Expect.equal (buffer.Snapshot.Line editor.First).Id anchor "insertion above preserves anchor"
                editor.Edit "changed"
                Expect.equal (buffer.Snapshot.Line editor.First).Text "changed" "edit reaches buffer"
                Expect.equal (before.Line 50).Text "row 50" "worker snapshot stays immutable"
                let anchored = editor.First
                Expect.isTrue (buffer.Undo ()) "undo edit"
                Expect.equal editor.First anchored "undo does not jump to start"
                Expect.isTrue (buffer.Undo ()) "undo inserted line"
                Expect.equal (buffer.Snapshot.Line editor.First).Id anchor "undo above preserves identity"
                editor.MoveTo 199
                editor.PinComposition ()

                buffer.Replace (
                    { Line = 0; Column = 0 },
                    {
                        Line = buffer.Snapshot.LineCount - 1
                        Column = (buffer.Snapshot.Line (buffer.Snapshot.LineCount - 1)).Text.Length
                    },
                    "small"
                )

                Expect.equal editor.First 0 "deletion clamps viewport"
                editor.Edit "small"
                editor.EndComposition ()
                editor.Measure ()
                Expect.equal (container.querySelector ".editor-line").textContent "small" "short document paints")
            hostCase "viewport updates only entering rows and measures only changed identities" (fun host ->
                host.Document.body.innerHTML <- "<div id='viewport'></div>"
                let container = host.Document.getElementById "viewport" :?> HTMLElement
                use editor = new EditorViewport (host, container, 1000000, textKind = "long-line")
                editor.Measure ()
                let writes, reads = editor.TextWrites, editor.WidthReads
                editor.MoveTo (editor.First)
                editor.Measure ()
                Expect.equal (editor.TextWrites, editor.WidthReads) (writes, reads) "unchanged refresh does no work"
                editor.MoveTo (editor.First + 1)
                editor.Measure ()
                Expect.equal (editor.TextWrites - writes) 1L "one entering row"
                Expect.equal (editor.WidthReads - reads) 1L "only entering row measured"
                let beforeEdit = editor.TextWrites
                editor.Edit "changed"
                editor.Measure ()
                Expect.equal (editor.TextWrites - beforeEdit) 1L "single edited row"
                Expect.isLessThanOrEqual editor.CacheCount 250 "bounded measurement keys")
            hostCase "viewport boundaries edit the displayed logical row" (fun host ->
                for total in [ 10; 1000 ] do
                    host.Document.body.innerHTML <- "<div id='viewport'></div>"
                    let container = host.Document.getElementById "viewport" :?> HTMLElement
                    use editor = new EditorViewport (host, container, total)

                    for requested in [ 0; 7; total - 1 ] do
                        editor.MoveTo requested
                        let first = min (max 0 (total - 25)) requested
                        Expect.equal editor.First first "first row clamps to the last full viewport"
                        editor.Edit "changed"
                        let rows = container.querySelectorAll ".editor-line"
                        let start = min (max 0 (total - editor.Count)) (max 0 (first - 50))
                        Expect.equal (rows.item (uint32 (first - start))).textContent $"line {first}: changed" "edit targets visible row"
                        Expect.equal container.scrollTop ((first - start) * 20) "scroll matches mounted offset")
            testCase "measurement eviction retains other live keys" (fun _ ->
                let cache = MeasurementCache 2

                let key text =
                    {
                        Text = text
                        Font = "mono"
                        Width = 700
                        StyleRevision = 0L
                    }

                cache.Measure (key "a", fun () -> 1.) |> ignore
                cache.Measure (key "b", fun () -> 2.) |> ignore
                cache.Measure (key "c", fun () -> 3.) |> ignore
                Expect.equal (cache.Measure (key "b", fun () -> failwith "live key evicted")) 2. "evict only oldest")
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
