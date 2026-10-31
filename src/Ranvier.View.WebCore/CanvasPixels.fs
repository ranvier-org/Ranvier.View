namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvImageDataLayout</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal NativeImageDataLayout =
    val mutable Size: int
    val mutable Width: uint32
    val mutable Height: uint32
    val mutable ColorSpace: int
    val mutable PixelFormat: int
    val mutable BytesPerPixel: int
    val mutable RowBytes: int64
    val mutable ByteLength: int64

/// <summary>ImageData construction and pixel bytes of a native host.</summary>
module CanvasPixels =
    /// <summary>An ImageData service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the canvas pixel functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ImageDataService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "canvas-pixels") then
            raise (NotSupportedException "The native library has no canvas pixel functions")

        let session = host.Session
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof ImageDataService))

            host.EnsureActive ()

        let native (data: ImageData) =
            match box data with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle (box data)
            | null -> nullArg "data"
            | _ -> invalidArg "data" "The ImageData belongs to another provider"

        let created (status: int) (result: nativeint array) =
            Interop.check "new ImageData" status
            session.Track (ImageDataObject (session, result[0])) |> unbox<ImageData>

        { new ImageDataService with
            member _.Create(width, height) =
                check ()
                ImageDataContract.checkDimensions width height
                let result = [| 0n |]
                created (Native.rv_image_data_create (uint32 width, uint32 height, null, 0L, 0, 0, result)) result

            member _.CreateFrom(pixels, width, height) =
                check ()

                if isNull pixels then
                    nullArg (nameof pixels)

                ImageDataContract.checkDimensions width height
                let result = [| 0n |]
                created (Native.rv_image_data_create (uint32 width, uint32 height, pixels, int64 pixels.Length, 0, 0, result)) result

            member _.Layout data =
                check ()
                let handle = native data
                let mutable value = NativeImageDataLayout ()
                value.Size <- sizeof<NativeImageDataLayout>

                Native.rv_image_data_layout (handle, NativePtr.toNativeInt &&value)
                |> Interop.check "ImageData layout"

                {
                    Width = int value.Width
                    Height = int value.Height
                    ColorSpace = enum<CanvasColorSpace> value.ColorSpace
                    PixelFormat = enum<ImageDataPixelFormat> value.PixelFormat
                    BytesPerPixel = value.BytesPerPixel
                    RowBytes = int value.RowBytes
                    ByteLength = int value.ByteLength
                }

            member _.Read(data, offset, count) =
                check ()
                let handle = native data

                ImageDataContract.checkCount count
                let buffer = Array.zeroCreate<byte> count

                Native.rv_image_data_read (handle, int64 offset, buffer, int64 count)
                |> Interop.check "ImageData read"

                buffer

            member _.Write(data, offset, bytes) =
                check ()
                let handle = native data

                if isNull bytes then
                    nullArg (nameof bytes)

                Native.rv_image_data_write (handle, int64 offset, bytes, int64 bytes.Length)
                |> Interop.check "ImageData write"

          interface IDisposable with
              member _.Dispose() = disposed.Value <- true }
