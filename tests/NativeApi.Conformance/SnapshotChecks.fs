module internal NativeApiConformance.SnapshotChecks

open System
open Ranvier.Dom

/// <summary>Task delivery and the document the fixture captures.</summary>
type Driver =
    {
        /// <summary>Lets queued work run, so pending snapshots deliver.</summary>
        Settle: unit -> Async<unit>
        /// <summary>A 20 by 10 CSS pixel element painted rgb(255, 0, 0).</summary>
        Red: Element
        /// <summary>The host's device scale.</summary>
        DeviceScale: float
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
/// Captures on one provider; the result is the assertion count. A provider without DOM capture, as an ordinary browser
/// context is, reports itself unavailable and raises NotSupportedException without painting another subject; a
/// provider with capture checks scale, crop, RGBA8 pixels, the PNG, JPEG and WEBP signatures, the argument errors and
/// cancellation.
/// </summary>
let run (create: unit -> SnapshotService) (driver: Driver) =
    async {
        let mutable checks = 0

        let expect label condition =
            checks <- checks + 1

            if not condition then
                failwith label

        use service = create ()
        let element = SnapshotSubject.Element driver.Red

        if not service.Available then
            expect "no color space without capture" (not (service.SupportsColorSpace SnapshotColorSpace.Srgb))
            expect "no encoding without capture" (not (service.SupportsEncoding ImageEncoding.Png))
            let mutable delivered = false

            for subject in [ SnapshotSubject.Viewport; element ] do
                expect
                    "capture unavailable"
                    (raisesOf<NotSupportedException>(fun () ->
                        service.Capture (subject, SnapshotContract.defaults, (fun _ -> delivered <- true))
                        |> ignore))

            do! driver.Settle ()
            expect "nothing delivered without capture" (not delivered)
        else
            let await label subject options =
                async {
                    let mutable outcome = None

                    use operation =
                        service.Capture (subject, options, (fun value -> outcome <- Some value))

                    expect (label + ": not delivered inside the call") outcome.IsNone
                    let mutable turns = 0

                    while outcome.IsNone && turns < 400 do
                        do! driver.Settle ()
                        turns <- turns + 1

                    match outcome with
                    | Some (ResourceOutcome.Succeeded image) -> return image
                    | _ -> return failwith (label + ": no successful capture")
                }

            let solid (image: SnapshotImage) =
                image.Data.Length = image.PixelWidth * image.PixelHeight * 4
                && Seq.forall
                    (fun index -> image.Data[index * 4 .. index * 4 + 3] = [| 255uy; 0uy; 0uy; 255uy |])
                    (seq { 0 .. image.PixelWidth * image.PixelHeight - 1 })

            let! image = await "element default" element SnapshotContract.defaults

            let size (scale: float) =
                int (floor (20.0 * scale)), int (floor (10.0 * scale))

            expect "default scale is the device scale" ((image.PixelWidth, image.PixelHeight) = size driver.DeviceScale)
            expect "element RGBA8 pixels" (solid image)

            for scale in [ 1.0; 2.0; 0.5 ] do
                let! image =
                    await
                        $"element at {scale}"
                        element
                        { SnapshotContract.defaults with
                            Scale = Some scale
                        }

                expect $"element size at {scale}" ((image.PixelWidth, image.PixelHeight) = size scale)

            let! crop =
                await
                    "element crop"
                    element
                    { SnapshotContract.defaults with
                        Scale = Some 1.0
                        Crop =
                            Some
                                {
                                    X = 5.0
                                    Y = 2.0
                                    Width = 10.0
                                    Height = 5.0
                                }
                    }

            expect "crop size" ((crop.PixelWidth, crop.PixelHeight) = (10, 5))
            expect "crop pixels" (solid crop)

            if service.SupportsEncoding ImageEncoding.Png then
                let! png =
                    await
                        "PNG"
                        SnapshotSubject.Viewport
                        { SnapshotContract.defaults with
                            Encoding = ImageEncoding.Png
                        }

                expect
                    "PNG signature"
                    (png.Data.Length > 8
                     && png.Data[0..7] = pngSignature)

            if service.SupportsEncoding ImageEncoding.Jpeg then
                let! jpeg =
                    await
                        "JPEG"
                        SnapshotSubject.Viewport
                        { SnapshotContract.defaults with
                            Encoding = ImageEncoding.Jpeg
                            Quality = Some 0.5
                        }

                expect "JPEG signature" (jpeg.Data[0] = 0xFFuy && jpeg.Data[1] = 0xD8uy)

            if service.SupportsEncoding ImageEncoding.Webp then
                let! webp =
                    await
                        "WEBP"
                        SnapshotSubject.Viewport
                        { SnapshotContract.defaults with
                            Encoding = ImageEncoding.Webp
                        }

                expect "WEBP signature" (webp.Data[8..11] = [| 0x57uy; 0x45uy; 0x42uy; 0x50uy |])

            if not (service.SupportsColorSpace SnapshotColorSpace.DisplayP3) then
                expect
                    "display-p3 without the color space"
                    (raisesOf<NotSupportedException>(fun () ->
                        service.Capture (
                            element,
                            { SnapshotContract.defaults with
                                ColorSpace = SnapshotColorSpace.DisplayP3
                            },
                            ignore
                        )
                        |> ignore))

            expect
                "scale out of range"
                (raisesOf<ArgumentOutOfRangeException>(fun () ->
                    service.Capture (
                        element,
                        { SnapshotContract.defaults with
                            Scale = Some 9.0
                        },
                        ignore
                    )
                    |> ignore))

            expect
                "crop outside the subject"
                (raisesOf<ArgumentOutOfRangeException>(fun () ->
                    service.Capture (
                        element,
                        { SnapshotContract.defaults with
                            Crop =
                                Some
                                    {
                                        X = 100.0
                                        Y = 0.0
                                        Width = 5.0
                                        Height = 5.0
                                    }
                        },
                        ignore
                    )
                    |> ignore))

            let mutable cancelled = []

            let operation =
                service.Capture (element, SnapshotContract.defaults, (fun value -> cancelled <- value :: cancelled))

            operation.Cancel ()

            for _ in 1..5 do
                do! driver.Settle ()

            expect "cancel settles Cancelled once" (cancelled = [ ResourceOutcome.Failed ResourceFailure.Cancelled ])
            operation.Dispose ()

        service.Dispose ()

        expect
            "a disposed service rejects captures"
            (raisesOf<ObjectDisposedException>(fun () ->
                service.Capture (SnapshotSubject.Viewport, SnapshotContract.defaults, ignore)
                |> ignore))

        return checks
    }
