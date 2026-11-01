module internal NativeApiConformance.CanvasPixelChecks

open System
open Ranvier.Dom

/// <summary>
/// Checks ImageData construction, layout and bytes, createImageData, getImageData and putImageData round trips with a
/// dirty rectangle, and drawImage from a canvas, on one provider; the result is the assertion count. The expected
/// values are Chromium 156's (Ranvier.WebCore docs/canvas-pixels-evidence/red-chromium.json).
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>IndexSizeError</c>.</param>
let run (createContexts: unit -> CanvasContextService) (createData: unit -> ImageDataService) (document: Document) (errorName: exn -> string) =
    let mutable checks = 0

    let text (values: byte array) =
        String.Join (",", values |> Array.map string)

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

    let bytes label (expected: byte array) (actual: byte array) =
        checks <- checks + 1

        if text actual <> text expected then
            failwith (String.Concat ([| label; ": expected "; text expected; ", got "; text actual |]))

    let fails label (expected: string) (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                "no exception"
            with error ->
                errorName error

        if name <> expected then
            failwith (String.Concat ([| label; ": expected "; expected; ", got "; name |]))

    let layout label (expected: ImageDataLayout) (actual: ImageDataLayout) =
        checks <- checks + 1

        if actual <> expected then
            failwith (label + ": unexpected layout")

    let red = [| 255uy; 0uy; 0uy; 255uy |]
    let blue = [| 0uy; 0uy; 255uy; 255uy |]
    let clear = [| 0uy; 0uy; 0uy; 0uy |]

    use contexts = createContexts ()
    use data = createData ()

    let canvas (width: uint32) (height: uint32) =
        let element = unbox<HTMLCanvasElement>(document.createElement "canvas")
        element.width <- width
        element.height <- height
        document.body.appendChild element |> ignore

        match contexts.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
        | CanvasContextOutcome.Context context -> element, context
        | _ -> failwith "a 2D context"

    let pixels (ctx: CanvasRenderingContext2D) x y w h =
        let image = ctx.getImageData (x, y, w, h)
        data.Read (image, 0, data.Layout(image).ByteLength)

    // Construction and layout.
    let blank = data.Create (2, 3)
    equal "width" 2u blank.width
    equal "height" 3u blank.height

    layout
        "layout"
        {
            Width = 2
            Height = 3
            ColorSpace = CanvasColorSpace.Srgb
            PixelFormat = ImageDataPixelFormat.RgbaUnorm8
            BytesPerPixel = 4
            RowBytes = 8
            ByteLength = 24
        }
        (data.Layout blank)

    bytes "a new ImageData is transparent black" (Array.zeroCreate 24) (data.Read (blank, 0, 24))
    fails "zero width" "IndexSizeError" (fun () -> data.Create (0, 2) |> ignore)
    fails "zero height" "IndexSizeError" (fun () -> data.Create (2, 0) |> ignore)

    fails "length not a multiple of 4" "InvalidStateError" (fun () ->
        data.CreateFrom (Array.zeroCreate 6, 1, 1)
        |> ignore)

    fails "length not a multiple of a row" "IndexSizeError" (fun () ->
        data.CreateFrom (Array.zeroCreate 12, 2, 1)
        |> ignore)

    fails "height mismatch" "IndexSizeError" (fun () ->
        data.CreateFrom (Array.zeroCreate 16, 2, 3)
        |> ignore)

    fails "empty data" "InvalidStateError" (fun () -> data.CreateFrom ([||], 1, 1) |> ignore)
    let fromData = data.CreateFrom ([| 1uy .. 8uy |], 1, 2)
    equal "from data height" 2u fromData.height
    bytes "from data bytes" [| 1uy .. 8uy |] (data.Read (fromData, 0, 8))

    // Byte ranges.
    data.Write (blank, 4, [| 9uy; 8uy; 7uy; 6uy |])
    bytes "write at an offset" [| 9uy; 8uy; 7uy; 6uy |] (data.Read (blank, 4, 4))
    fails "write past the end" "RangeError" (fun () -> data.Write (blank, 21, [| 1uy; 1uy; 1uy; 1uy |]))
    bytes "a write past the end writes nothing" clear (data.Read (blank, 20, 4))
    fails "read past the end" "RangeError" (fun () -> data.Read (blank, 22, 4) |> ignore)
    fails "negative offset" "RangeError" (fun () -> data.Read (blank, -1, 1) |> ignore)
    bytes "an empty read at the end" [||] (data.Read (blank, 24, 0))

    // createImageData, putImageData and getImageData.
    let first, ctx = canvas 8u 8u
    let created = ctx.createImageData (-3, 2)
    equal "createImageData(-3, 2) width" 3u created.width
    fails "createImageData(0, 1)" "IndexSizeError" (fun () -> ctx.createImageData (0, 1) |> ignore)
    let like = ctx.createImageData fromData
    bytes "createImageData(imagedata) is transparent" (Array.zeroCreate 8) (data.Read (like, 0, 8))

    let source =
        [|
            255uy
            0uy
            0uy
            255uy
            0uy
            255uy
            0uy
            255uy
            0uy
            0uy
            255uy
            255uy
            10uy
            20uy
            30uy
            0uy
            255uy
            255uy
            255uy
            128uy
            0uy
            0uy
            0uy
            255uy
            200uy
            100uy
            50uy
            255uy
            1uy
            2uy
            3uy
            255uy
        |]

    ctx.putImageData (data.CreateFrom (source, 4, 2), 1, 1)
    let expected = Array.copy source
    Array.fill expected 12 4 0uy
    bytes "round trip" expected (pixels ctx 1 1 4 2)
    bytes "outside reads transparent" (Array.zeroCreate 16) (pixels ctx (-1) (-1) 2 2)
    bytes "a negative rectangle" (Array.append red [| 0uy; 255uy; 0uy; 255uy |]) (pixels ctx 3 2 (-2) (-1))
    fails "getImageData zero width" "IndexSizeError" (fun () -> ctx.getImageData (0, 0, 0, 1) |> ignore)
    ctx.clearRect (0.0, 0.0, 8.0, 8.0)
    let solid = data.CreateFrom (Array.concat (Array.replicate 9 blue), 3, 3)
    ctx.putImageData (solid, 2, 2, 1, 1, 1, 1)
    bytes "dirty inside" blue (pixels ctx 3 3 1 1)
    bytes "dirty corner" clear (pixels ctx 2 2 1 1)

    // drawImage from a canvas.
    let second, source2d = canvas 4u 4u
    source2d.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#ff0000"
    source2d.fillRect (0.0, 0.0, 2.0, 4.0)
    source2d.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#0000ff"
    source2d.fillRect (2.0, 0.0, 2.0, 4.0)
    ctx.clearRect (0.0, 0.0, 8.0, 8.0)
    ctx.drawImage (second, 0.0, 0.0)
    bytes "drawImage left" red (pixels ctx 0 0 1 1)
    bytes "drawImage right" blue (pixels ctx 3 0 1 1)
    bytes "drawImage beyond" clear (pixels ctx 5 0 1 1)
    ctx.clearRect (0.0, 0.0, 8.0, 8.0)
    ctx.drawImage (second, 0.0, 0.0, 8.0, 8.0)
    bytes "scaled left" red (pixels ctx 1 6 1 1)
    bytes "scaled right" blue (pixels ctx 6 6 1 1)
    ctx.clearRect (0.0, 0.0, 8.0, 8.0)
    ctx.drawImage (second, 2.0, 0.0, 2.0, 4.0, 0.0, 0.0, 2.0, 4.0)
    bytes "sub-rectangle" blue (pixels ctx 0 0 1 1)
    bytes "sub-rectangle beyond" clear (pixels ctx 2 0 1 1)
    let empty = unbox<HTMLCanvasElement>(document.createElement "canvas")
    empty.width <- 0u
    fails "a zero-size canvas" "InvalidStateError" (fun () -> ctx.drawImage (empty, 0.0, 0.0))

    first.remove ()
    second.remove ()
    bytes "ImageData outlives its canvas" [| 1uy .. 8uy |] (data.Read (fromData, 0, 8))
    checks
