namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvCanvas2DSettings</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal NativeCanvas2DSettings =
    val mutable Size: int
    val mutable Alpha: int
    val mutable Desynchronized: int
    val mutable WillReadFrequently: int
    val mutable ColorSpace: int
    val mutable ColorType: int

/// <summary>HTMLCanvasElement.getContext and CanvasRenderingContext2D.getContextAttributes of a native host.</summary>
module Canvas =
    [<Literal>]
    let private Unsupported = -9

    let private flag (value: bool) = if value then 1 else 0

    /// <summary>A canvas context service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>
    /// The native host runs no page script; a canvas with a context renders its bitmap, and a canvas without one renders
    /// its fallback content. Raises NotSupportedException when the native library lacks the canvas functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : CanvasContextService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "canvas-context") then
            raise (NotSupportedException "The native library has no canvas context functions")

        let session = host.Session
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof CanvasContextService))

            host.EnsureActive ()

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle value
            | null -> nullArg name
            | _ -> invalidArg name $"The {name} belongs to another provider"

        { new CanvasContextService with
            member _.GetContext(canvas, contextType, settings) =
                check ()
                let canvasHandle = native (box canvas) "canvas"

                match CanvasContract.unsupported contextType settings with
                | Some reason -> CanvasContextOutcome.Unsupported reason
                | None ->
                    let mutable value = NativeCanvas2DSettings ()
                    value.Size <- sizeof<NativeCanvas2DSettings>
                    value.Alpha <- 1
                    value.Desynchronized <- flag settings.Desynchronized
                    value.WillReadFrequently <- flag settings.WillReadFrequently
                    value.ColorSpace <- int settings.ColorSpace
                    value.ColorType <- int settings.ColorType
                    let mutable result = 0n
                    let status = Native.rv_canvas_get_context (canvasHandle, int contextType, NativePtr.toNativeInt &&value, &result)

                    if status = Unsupported then
                        CanvasContextOutcome.Unsupported "the native engine renders no such context"
                    else
                        Interop.check "HTMLCanvasElement.getContext" status

                        if result = 0n then
                            CanvasContextOutcome.OtherContext
                        else
                            let owner = unbox<NativeObject> (box canvas)

                            session.Object (
                                result,
                                Kind.CanvasRenderingContext2D,
                                owner,
                                Func<Session, nativeint, NativeObject>(fun s h -> CanvasRenderingContext2DObject (s, h))
                            )
                            |> unbox<CanvasRenderingContext2D>
                            |> CanvasContextOutcome.Context

            member _.Attributes context =
                check ()
                let handle = native (box context) "context"
                let mutable value = NativeCanvas2DSettings ()
                value.Size <- sizeof<NativeCanvas2DSettings>

                Native.rv_canvas_context_2d_attributes (handle, NativePtr.toNativeInt &&value)
                |> Interop.check "CanvasRenderingContext2D.getContextAttributes"

                {
                    Alpha = value.Alpha <> 0
                    Desynchronized = value.Desynchronized <> 0
                    WillReadFrequently = value.WillReadFrequently <> 0
                    ColorSpace = enum<CanvasColorSpace> value.ColorSpace
                    ColorType = enum<CanvasColorType> value.ColorType
                }

          interface IDisposable with
              member _.Dispose() = disposed.Value <- true }
