namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvImageBitmapOptions</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal NativeImageBitmapOptions =
    val mutable Size: int
    val mutable Crop: int
    val mutable X: int
    val mutable Y: int
    val mutable Width: int
    val mutable Height: int
    val mutable Orientation: int
    val mutable PremultiplyAlpha: int
    val mutable ColorSpaceConversion: int
    val mutable ResizeWidth: uint32
    val mutable ResizeHeight: uint32
    val mutable ResizeQuality: int

/// <summary>Canvas export and createImageBitmap of a native host.</summary>
module CanvasExport =
    [<Literal>]
    let private Unsupported = -9

    [<Literal>]
    let private BufferTooSmall = -6

    /// <summary>Completions of native bitmap operations, keyed by the context value passed to the host.</summary>
    type private Completions() =
        let gate = obj ()
        let handlers = Dictionary<int64, int -> unit>()
        let mutable next = 0L

        let callback =
            Native.ResourceCompletion (fun context outcome _ _ ->
                let handler =
                    lock gate (fun () ->
                        match handlers.TryGetValue (int64 context) with
                        | true, handler ->
                            handlers.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome))

        member _.Pointer =
            Marshal.GetFunctionPointerForDelegate<Native.ResourceCompletion> callback

        member _.Add(handler) =
            lock gate (fun () ->
                next <- next + 1L
                handlers[next] <- handler
                next)

        member _.Remove(key: int64) =
            lock gate (fun () -> handlers.Remove key |> ignore)

    let private completions = Completions ()

    // The native encodings are the RV_IMAGE_ENCODING_* values.
    let private nativeEncoding =
        function
        | ImageEncoding.Rgba8 -> 0
        | ImageEncoding.Png -> 1
        | ImageEncoding.Jpeg -> 2
        | ImageEncoding.Webp -> 3

    /// <summary>A canvas export service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the canvas export functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : CanvasExportService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "canvas-export"
                && WebCoreHost.Has "image-bitmaps"
            )
        then
            raise (NotSupportedException "The native library has no canvas export functions")

        let session = host.Session
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof CanvasExportService))

            host.EnsureActive ()

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle value
            | null -> nullArg name
            | _ -> invalidArg name $"The {name} belongs to another provider"

        let supported (encoding: ImageEncoding) (status: int) =
            if status = Unsupported then
                raise (
                    NotSupportedException (
                        "The native engine does not encode "
                        + CanvasExportContract.mimeType encoding
                    )
                )

            status

        let encode canvas encoding quality =
            check ()
            CanvasExportContract.mimeType encoding |> ignore
            let handle = native (box canvas) "canvas"
            let quality = CanvasExportContract.quality quality
            let mutable length = 0L

            let status =
                Native.rv_canvas_encode (handle, nativeEncoding encoding, quality, null, 0L, &length)
                |> supported encoding

            if status = 0 then
                [||]
            else
                if status <> BufferTooSmall then
                    Interop.check "HTMLCanvasElement.toBlob" status

                let bytes = Array.zeroCreate<byte>(int length)

                Native.rv_canvas_encode (handle, nativeEncoding encoding, quality, bytes, int64 bytes.Length, &length)
                |> Interop.check "HTMLCanvasElement.toBlob"

                bytes

        let options (value: ImageBitmapOptions) =
            CanvasExportContract.checkOptions value
            let mutable result = NativeImageBitmapOptions ()
            result.Size <- sizeof<NativeImageBitmapOptions>

            match value.Crop with
            | Some crop ->
                result.Crop <- 1
                result.X <- crop.X
                result.Y <- crop.Y
                result.Width <- crop.Width
                result.Height <- crop.Height
            | None -> ()

            result.Orientation <- int value.Orientation
            result.PremultiplyAlpha <- int value.PremultiplyAlpha
            result.ColorSpaceConversion <- int value.ColorSpaceConversion
            result.ResizeWidth <- uint32 (defaultArg value.ResizeWidth 0)
            result.ResizeHeight <- uint32 (defaultArg value.ResizeHeight 0)
            result.ResizeQuality <- int value.ResizeQuality
            result

        { new CanvasExportService with
            member _.Encode(canvas, encoding, quality) =
                encode canvas encoding quality

            member _.ToDataUrl(canvas, encoding, quality) =
                check ()
                CanvasExportContract.mimeType encoding |> ignore
                let handle = native (box canvas) "canvas"

                Interop.text "HTMLCanvasElement.toDataURL" (fun buffer capacity length ->
                    Native.rv_canvas_to_data_url (handle, nativeEncoding encoding, CanvasExportContract.quality quality, buffer, capacity, length)
                    |> supported encoding)

            member _.ToBlob(canvas, encoding, quality) =
                check ()
                CanvasExportContract.mimeType encoding |> ignore
                let handle = native (box canvas) "canvas"
                let result = [| 0n |]

                Native.rv_canvas_to_blob (handle, nativeEncoding encoding, CanvasExportContract.quality quality, result)
                |> supported encoding
                |> Interop.check "HTMLCanvasElement.toBlob"

                if result[0] = 0n then
                    None
                else
                    session.Object (
                        result[0],
                        Kind.Blob,
                        unbox<NativeObject>(box canvas),
                        Func<Session, nativeint, NativeObject>(fun s h -> BlobObject (s, h))
                    )
                    |> unbox<Blob>
                    |> Some

            member _.BitmapRendererContext canvas =
                check ()
                let handle = native (box canvas) "canvas"
                let result = [| 0n |]

                Native.rv_canvas_get_bitmap_context (handle, result)
                |> Interop.check "HTMLCanvasElement.getContext"

                if result[0] = 0n then
                    None
                else
                    session.Object (
                        result[0],
                        Kind.ImageBitmapRenderingContext,
                        unbox<NativeObject>(box canvas),
                        Func<Session, nativeint, NativeObject>(fun s h -> ImageBitmapRenderingContextObject (s, h))
                    )
                    |> unbox<ImageBitmapRenderingContext>
                    |> Some

            member _.CreateImageBitmap(source, requested, terminal) =
                check ()

                let struct (kind, handle) =
                    match source with
                    | ImageBitmapSource.Image image -> struct (0, native (box image) "source")
                    | ImageBitmapSource.SvgImage image -> struct (0, native (box image) "source")
                    | ImageBitmapSource.Canvas canvas -> struct (0, native (box canvas) "source")
                    | ImageBitmapSource.Bitmap bitmap -> struct (1, native (box bitmap) "source")
                    | ImageBitmapSource.ImageData data -> struct (2, native (box data) "source")
                    | ImageBitmapSource.Blob blob -> struct (3, native (box blob) "source")

                let mutable value = options requested
                let mutable operation = 0UL

                let release () =
                    if operation <> 0UL then
                        Native.rv_resource_release operation |> ignore
                        operation <- 0UL

                let mutable arbiter = Unchecked.defaultof<ResourceArbiter<ImageBitmap>>

                arbiter <-
                    new ResourceArbiter<ImageBitmap> (
                        host.PostToOwner,
                        (fun outcome ->
                            release ()
                            terminal outcome),
                        (fun () ->
                            if operation <> 0UL then
                                Native.rv_resource_cancel operation |> ignore),
                        ignore
                    )

                let key =
                    completions.Add (fun outcome ->
                        if outcome = 0 then
                            let result = [| 0n |]

                            if Native.rv_image_bitmap_result (operation, result) = 0 then
                                let bitmap = session.Track (ImageBitmapObject (session, result[0])) :> ImageBitmap

                                arbiter.Settle (ResourceOutcome.Succeeded bitmap)
                                |> ignore
                            else
                                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.DecodeFailed "the bitmap is unavailable"))
                                |> ignore
                        elif outcome = 3 then
                            arbiter.Settle (ResourceOutcome.Failed ResourceFailure.Cancelled)
                            |> ignore
                        elif outcome = 2 then
                            let name =
                                try
                                    Interop.text "createImageBitmap" (fun buffer capacity length ->
                                        Native.rv_image_bitmap_error (operation, buffer, capacity, length))
                                with _ ->
                                    "UnknownError"

                            arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.DecodeFailed name))
                            |> ignore
                        else
                            arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.Invalidated "the document changed"))
                            |> ignore)

                let status =
                    Native.rv_image_bitmap_create (
                        host.Handle,
                        kind,
                        handle,
                        NativePtr.toNativeInt &&value,
                        completions.Pointer,
                        nativeint key,
                        &operation
                    )

                if status <> 0 then
                    completions.Remove key

                    if status = Unsupported then
                        raise (NotSupportedException "The native engine creates no ImageBitmap from this source or options")

                    Interop.check "createImageBitmap" status

                arbiter :> ResourceOperation<ImageBitmap>

          interface IDisposable with
              member _.Dispose() =
                  disposed.Value <- true
        }
