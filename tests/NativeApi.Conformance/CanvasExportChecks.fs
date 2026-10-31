module internal NativeApiConformance.CanvasExportChecks

open System
open Ranvier.Dom

/// <summary>Task delivery for pending bitmap operations.</summary>
type Driver =
    {
        /// <summary>Lets queued work run, so pending bitmaps deliver.</summary>
        Settle: unit -> Async<unit>
    }

let private pngSignature =
    [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x0Duy; 0x0Auy; 0x1Auy; 0x0Auy |]

/// <summary>Whether the action raises <c>'E</c>; under Fable, whether it raises at all.</summary>
let private raisesOf<'E when 'E :> exn> (action: unit -> unit) =
    try
        action ()
        false
    with error ->
#if FABLE_COMPILER
        ignore error
        true
#else
        error :? 'E
#endif

/// <summary>
/// Checks canvas export (encoded bytes, data URLs, Blobs, unsupported encodings, a zero-size canvas), createImageBitmap
/// from a canvas with crop, flip and resize, close, the failure names, and the bitmaprenderer context's
/// transferFromImageBitmap, on one provider; the result is the assertion count. The expected values are Chromium 156's
/// (Ranvier.WebCore docs/canvas-export-evidence/red-chromium.json), except that an unsupported encoding raises
/// NotSupportedException where Chromium substitutes PNG.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>SecurityError</c>.</param>
let run
    (createContexts: unit -> CanvasContextService)
    (createData: unit -> ImageDataService)
    (createExport: unit -> CanvasExportService)
    (document: Document)
    (driver: Driver)
    =
    async {
        let mutable checks = 0

        let expect label condition =
            checks <- checks + 1

            if not condition then
                failwith label

        let text (values: byte array) =
            String.Join (",", values |> Array.map string)

        use contexts = createContexts ()
        use data = createData ()
        use export = createExport ()

        let canvas (width: uint32) (height: uint32) =
            let element = unbox<HTMLCanvasElement>(document.createElement "canvas")
            element.width <- width
            element.height <- height
            document.body.appendChild element |> ignore
            element

        let context2d (element: HTMLCanvasElement) =
            match contexts.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
            | CanvasContextOutcome.Context context -> context
            | _ -> failwith "a 2D context"

        let pixels (ctx: CanvasRenderingContext2D) x y w h =
            let image = ctx.getImageData (x, y, w, h)
            text (data.Read (image, 0, data.Layout(image).ByteLength))

        let red = [| 255uy; 0uy; 0uy; 255uy |]
        let blue = [| 0uy; 0uy; 255uy; 255uy |]
        let source = canvas 4u 2u
        let ctx = context2d source
        ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#ff0000"
        ctx.fillRect (0.0, 0.0, 2.0, 2.0)
        ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#0000ff"
        ctx.fillRect (2.0, 0.0, 2.0, 2.0)

        // Export.
        let png = export.Encode (source, ImageEncoding.Png, None)
        expect "PNG signature" (png.Length > 8 && png[0..7] = pngSignature)
        let url = export.ToDataUrl (source, ImageEncoding.Png, None)
        expect "PNG data URL prefix" (url.StartsWith "data:image/png;base64,")
        expect "the data URL holds the PNG bytes" (Convert.FromBase64String (url.Substring 22) = png)
        let jpeg = export.Encode (source, ImageEncoding.Jpeg, Some 0.9)

        expect
            "JPEG signature"
            (jpeg.Length > 3
             && jpeg[0..2] = [| 0xFFuy; 0xD8uy; 0xFFuy |])

        expect "JPEG data URL prefix" ((export.ToDataUrl (source, ImageEncoding.Jpeg, Some 0.9)).StartsWith "data:image/jpeg;base64,")

        expect
            "RGBA8 is not an export encoding"
            (raisesOf<NotSupportedException>(fun () ->
                export.Encode (source, ImageEncoding.Rgba8, None)
                |> ignore))

        expect
            "RGBA8 has no data URL"
            (raisesOf<NotSupportedException>(fun () ->
                export.ToDataUrl (source, ImageEncoding.Rgba8, None)
                |> ignore))

        let empty = canvas 0u 2u
        expect "a zero-size canvas encodes nothing" ((export.Encode (empty, ImageEncoding.Png, None)).Length = 0)
        expect "a zero-size canvas data URL" (export.ToDataUrl (empty, ImageEncoding.Png, None) = "data:,")
        expect "a zero-size canvas has no Blob" (export.ToBlob (empty, ImageEncoding.Png, None)).IsNone

        let blob =
            match export.ToBlob (source, ImageEncoding.Png, None) with
            | Some value -> value
            | None -> failwith "toBlob"

        expect "the Blob type" (blob.``type`` = "image/png")
        expect "the Blob size" (int blob.size = png.Length)

        // createImageBitmap.
        let target = canvas 8u 8u
        let tctx = context2d target

        let create label bitmapSource options =
            async {
                let mutable outcome = None

                use operation =
                    export.CreateImageBitmap (bitmapSource, options, (fun value -> outcome <- Some value))

                expect (label + ": not delivered inside the call") outcome.IsNone
                let mutable turns = 0

                while outcome.IsNone && turns < 400 do
                    do! driver.Settle ()
                    turns <- turns + 1

                return outcome
            }

        let bitmap label bitmapSource options =
            async {
                match! create label bitmapSource options with
                | Some (ResourceOutcome.Succeeded value) -> return value
                | other -> return failwith (label + ": " + string (box other))
            }

        let failure label bitmapSource options =
            async {
                match! create label bitmapSource options with
                | Some (ResourceOutcome.Failed (ResourceFailure.DecodeFailed name)) -> return name
                | _ -> return failwith (label + ": no DecodeFailed")
            }

        let drawn (value: ImageBitmap) w h =
            tctx.clearRect (0.0, 0.0, 8.0, 8.0)
            tctx.drawImage (value, 0.0, 0.0)
            pixels tctx 0 0 w h

        let! whole =
            bitmap "canvas" (ImageBitmapSource.Canvas source) ImageBitmapOptions.Default

        expect "bitmap size" (whole.width = 4u && whole.height = 2u)
        expect "drawImage(bitmap)" (drawn whole 4 1 = text (Array.concat [ red; red; blue; blue ]))

        let! crop =
            bitmap
                "crop"
                (ImageBitmapSource.Canvas source)
                { ImageBitmapOptions.Default with
                    Crop = Some { X = 1; Y = 0; Width = 2; Height = 2 }
                }

        expect "crop pixels" (drawn crop 2 1 = text (Array.append red blue))
        let column = data.CreateFrom (Array.append red blue, 1, 2)

        let! flipped =
            bitmap
                "flipY"
                (ImageBitmapSource.ImageData column)
                { ImageBitmapOptions.Default with
                    Orientation = ImageBitmapOrientation.FlipY
                }

        expect "flipY pixels" (drawn flipped 1 2 = text (Array.append blue red))

        let! resized =
            bitmap
                "resize"
                (ImageBitmapSource.Canvas source)
                { ImageBitmapOptions.Default with
                    ResizeWidth = Some 8
                    ResizeHeight = Some 4
                    ResizeQuality = ImageBitmapResizeQuality.Pixelated
                }

        expect "resize" (resized.width = 8u && resized.height = 4u)
        let! decoded = bitmap "Blob" (ImageBitmapSource.Blob blob) ImageBitmapOptions.Default
        expect "a decoded Blob draws the canvas" (drawn decoded 4 1 = text (Array.concat [ red; red; blue; blue ]))

        let! zeroCrop =
            failure
                "zero crop"
                (ImageBitmapSource.Canvas source)
                { ImageBitmapOptions.Default with
                    Crop = Some { X = 0; Y = 0; Width = 0; Height = 2 }
                }

        expect "a zero crop is a RangeError" (zeroCrop = "RangeError")

        let! zeroCanvas =
            failure "zero canvas" (ImageBitmapSource.Canvas empty) ImageBitmapOptions.Default

        expect "a zero-size canvas is an InvalidStateError" (zeroCanvas = "InvalidStateError")

        let! closing =
            bitmap "closing" (ImageBitmapSource.Canvas source) ImageBitmapOptions.Default

        closing.close ()
        expect "a closed bitmap is 0x0" (closing.width = 0u && closing.height = 0u)

        let! closed =
            failure "closed source" (ImageBitmapSource.Bitmap closing) ImageBitmapOptions.Default

        expect "a closed source is an InvalidStateError" (closed = "InvalidStateError")

        // Cancel.
        let mutable cancelled = []

        let pending =
            export.CreateImageBitmap (ImageBitmapSource.Blob blob, ImageBitmapOptions.Default, (fun value -> cancelled <- value :: cancelled))

        pending.Cancel ()
        do! driver.Settle ()
        expect "cancel settles Cancelled once" (cancelled = [ ResourceOutcome.Failed ResourceFailure.Cancelled ])
        pending.Dispose ()

        // bitmaprenderer.
        let renderer = canvas 4u 2u

        let bctx =
            match export.BitmapRendererContext renderer with
            | Some value -> value
            | None -> failwith "bitmaprenderer"

        expect "the same bitmaprenderer context" (obj.ReferenceEquals ((export.BitmapRendererContext renderer).Value, bctx))
        expect "a 2D canvas has no bitmaprenderer context" (export.BitmapRendererContext source).IsNone

        match contexts.GetContext (renderer, CanvasContextType.TwoD, Canvas2DSettings.Default) with
        | CanvasContextOutcome.OtherContext -> expect "a bitmaprenderer canvas has no 2D context" true
        | _ -> expect "a bitmaprenderer canvas has no 2D context" false

        let! moving =
            bitmap "moving" (ImageBitmapSource.Canvas source) ImageBitmapOptions.Default

        bctx.transferFromImageBitmap moving
        expect "transfer detaches the bitmap" (moving.width = 0u && moving.height = 0u)
        expect "the canvas shows the transferred pixels" (export.ToDataUrl (renderer, ImageEncoding.Png, None) = url)
        expect "a detached bitmap cannot transfer" (raisesOf<exn>(fun () -> bctx.transferFromImageBitmap moving))
        return checks
    }
