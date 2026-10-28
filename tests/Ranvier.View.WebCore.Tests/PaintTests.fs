module Ranvier.View.WebCore.Tests.PaintTests

open Expecto
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore.Tests.Support

let private label =
    Component.define (fun _ (text: Signal<string>) -> text) (fun t ->
        t.div
            [|
                t.Attrs.attribute ("style", "font-size:96px;line-height:120px;color:black")
            |]
            [| t.text (fun (text: Signal<string>) -> text.Value) |])

let private differing (a: byte array) (b: byte array) =
    let mutable count = 0

    for i in 0 .. a.Length - 1 do
        if a[i] <> b[i] then
            count <- count + 1

    count

let private paintCase (scale: float32) name =
    hostCase name
    <| fun host ->
        host.SetDeviceScale scale

        let blank =
            let struct (width, height) = host.PaintSize
            let pixels = Array.zeroCreate<byte>(width * height * 4)
            host.Paint pixels
            pixels

        use graph = new Graph ()
        let text = graph.Run (fun () -> Api.createSignal "A")
        use mounted = mount host graph label text
        let struct (width, height) = host.PaintSize
        let first = Array.zeroCreate<byte>(width * height * 4)
        host.Paint first
        Expect.isGreaterThan (differing blank first) 100 "the component painted"

        mounted.Act (host, fun () -> text.Value <- "W")
        Expect.isTrue host.FrameRequested "the write asked for a frame"
        host.Frame ()
        Expect.isTrue host.DirtyRect.IsSome "the frame invalidated an area"
        let dirty = host.DirtyRect
        let second = Array.copy first
        Expect.equal (host.PaintDirty second) dirty "returns the painted region"
        Expect.isGreaterThan (differing first second) 100 "the dirty repaint changed the pixels"

        let full = Array.zeroCreate<byte>(width * height * 4)
        host.Paint full

        let differences =
            seq {
                for i in 0 .. second.Length - 1 do
                    if second[i] <> full[i] then
                        yield (i / 4 % width, i / 4 / width, i % 4, second[i], full[i])
            }
            |> Seq.truncate 16
            |> Seq.toArray

        Expect.equal (differing second full) 0 (sprintf "dirty %A differences (x,y,channel,partial,full): %A" dirty differences)

[<Tests>]
let tests =
    testList
        "Paint"
        [
            paintCase 1.0f "a mounted component paints and a signal write changes the pixels"
            paintCase 1.25f "a fractional-scale dirty repaint clears the previous glyph's overhang"
            paintCase 1.2f "a non-grid fractional-scale repaint matches the full canvas"

            hostCase "conservative repaint damage stays inside a resized fractional-scale viewport"
            <| fun host ->
                host.SetDeviceScale 1.25f
                host.Resize (127, 173)
                host.Frame ()
                Expect.equal host.DirtyRect (ValueSome (struct (0, 0, 127, 173))) "the repaint covers the resized viewport"
        ]
