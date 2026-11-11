namespace Ranvier.View.WebCore

open System
open Microsoft.Win32.SafeHandles
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom

#nowarn "9"

type internal SnapshotPixels private (handle, width, height, colorSpace) =
    inherit SafeHandleZeroOrMinusOneIsInvalid(true)
    do base.SetHandle handle

    override this.ReleaseHandle() =
        OptionalNative.rv_snapshot_pixels_release this.handle
        true

    member this.Read(options: SnapshotOptions) =
        let mutable retained = false
        let mutable encoded = 0n

        try
            this.DangerousAddRef &retained
            let handle = this.DangerousGetHandle ()
            let mutable length = 0L

            let status =
                if options.Encoding = ImageEncoding.Rgba8 then
                    OptionalNative.rv_snapshot_pixels_copy (handle, null, 0L, &length)
                else
                    OptionalNative.rv_snapshot_pixels_encode (
                        handle,
                        NativeImageEncoding.code options.Encoding,
                        defaultArg options.Quality -1.,
                        &encoded
                    )
                    |> Interop.check "rv_snapshot_pixels_encode"

                    OptionalNative.rv_encoded_image_bytes (encoded, null, 0L, &length)

            if status <> -6 then
                Interop.check "snapshot pixel length" status

            if length < 0L || length > int64 Int32.MaxValue then
                invalidOp "Invalid native snapshot byte count"

            let data = Array.zeroCreate<byte>(int length)

            (if options.Encoding = ImageEncoding.Rgba8 then
                 OptionalNative.rv_snapshot_pixels_copy (handle, data, length, &length)
             else
                 OptionalNative.rv_encoded_image_bytes (encoded, data, length, &length))
            |> Interop.check "snapshot pixel copy"

            {
                PixelWidth = width
                PixelHeight = height
                PixelColorSpace = colorSpace
                Format = options.Encoding
                Data = data
            }
        finally
            if encoded <> 0n then
                OptionalNative.rv_encoded_image_release encoded

            if retained then
                this.DangerousRelease ()

    static member Capture(host: WebCoreHost, subject: SnapshotSubject, options: SnapshotOptions) =
        let element =
            match subject with
            | SnapshotSubject.Viewport -> 0n
            | SnapshotSubject.Element element -> Interop.handle (box element)

        let mutable value = SnapshotOptionsNative ()
        value.Size <- sizeof<SnapshotOptionsNative>

        value.ColorSpace <-
            if options.ColorSpace = SnapshotColorSpace.DisplayP3 then
                1
            else
                0

        value.Scale <- defaultArg options.Scale 0.

        match options.Crop with
        | Some crop ->
            value.Crop <- 1
            value.X <- crop.X
            value.Y <- crop.Y
            value.Width <- crop.Width
            value.Height <- crop.Height
        | None -> ()

        let mutable handle = 0n
        let mutable width, height = 0, 0

        let status =
            OptionalNative.rv_host_snapshot_pixels (host.Handle, element, NativePtr.toNativeInt &&value, &handle, &width, &height)

        match status with
        | -9 -> raise (ArgumentOutOfRangeException ("options", "The crop lies outside the subject or exceeds capture limits"))
        | -12 -> invalidArg "subject" "The element belongs to another document"
        | -5 -> invalidOp "The element has no box to paint"
        | _ -> Interop.check "rv_host_snapshot_pixels" status

        new SnapshotPixels (handle, width, height, options.ColorSpace)
