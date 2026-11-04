namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore

module private PresentationNative =
    let utf8 (value: string) =
        Encoding.UTF8.GetBytes (value + "\000")

    let ok (value: SDLBool) =
        SDLBool.op_Implicit value

    let failure (operation: string) =
        InvalidOperationException (operation + " failed: " + SDL3.SDL_GetError ())

/// <summary>CPU and shared GPU textures presented on the host and SDL owner thread.</summary>
type internal Presenter(renderer: nativeptr<SDL_Renderer>, host: WebCoreHost, egl: EglRenderer option) =
    let metal = MetalRenderer.TryCreate renderer
    let mutable cpuTexture = NativePtr.nullPtr<SDL_Texture>
    let mutable cpuWidth = 0
    let mutable cpuHeight = 0
    let mutable pixels: byte array = [||]
    let mutable fullPaint = true
    let mutable gpuTexture = NativePtr.nullPtr<SDL_Texture>
    let mutable gpuResource = 0n
    let mutable gpuWidth = 0
    let mutable gpuHeight = 0
    let mutable gpuFrames = 0L
    let mutable cpuFrames = 0L
    let mutable lastGpu = false
    let mutable pendingEglFrame = 0n

    let device () =
        use key = fixed PresentationNative.utf8 "SDL.renderer.d3d11.device"
        SDL3.SDL_GetPointerProperty (SDL3.SDL_GetRendererProperties renderer, key, 0n)

    let gpuEnabled =
        ((OperatingSystem.IsWindows ()
          && WebCoreHost.Has "presentation-d3d11"
          && device () <> 0n)
         || egl.IsSome
         || metal.IsSome)
        && RanvierWebCoreAbi.Native.rv_host_set_accelerated_compositing (host.Handle, 1) = 0

    let draw texture width height =
        if not (PresentationNative.ok (SDL3.SDL_RenderClear renderer)) then
            raise (PresentationNative.failure "SDL_RenderClear")

        if not (NativePtr.isNullPtr texture) then
            let mutable target = SDL_FRect ()
            target.w <- float32 width
            target.h <- float32 height

            if not (PresentationNative.ok (SDL3.SDL_RenderTexture (renderer, texture, NativePtr.nullPtr, &&target))) then
                raise (PresentationNative.failure "SDL_RenderTexture")

    let importTexture resource width height =
        let properties = SDL3.SDL_CreateProperties ()

        let number key value =
            use name = fixed PresentationNative.utf8 key
            PresentationNative.ok (SDL3.SDL_SetNumberProperty (properties, name, value))

        try
            use key = fixed PresentationNative.utf8 "SDL.texture.create.d3d11.texture"

            if
                number "SDL.texture.create.width" (int64 width)
                && number "SDL.texture.create.height" (int64 height)
                && number "SDL.texture.create.format" (int64 SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888)
                && PresentationNative.ok (SDL3.SDL_SetPointerProperty (properties, key, resource))
            then
                SDL3.SDL_CreateTextureWithProperties (renderer, properties)
            else
                NativePtr.nullPtr<SDL_Texture>
        finally
            SDL3.SDL_DestroyProperties properties

    let updateCpu () =
        let struct (width, height) = host.PaintSize

        if width > 0 && height > 0 then
            if
                width <> cpuWidth
                || height <> cpuHeight
                || NativePtr.isNullPtr cpuTexture
            then
                if not (NativePtr.isNullPtr cpuTexture) then
                    SDL3.SDL_DestroyTexture cpuTexture

                cpuTexture <-
                    SDL3.SDL_CreateTexture (
                        renderer,
                        SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888,
                        SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                        width,
                        height
                    )

                if NativePtr.isNullPtr cpuTexture then
                    raise (PresentationNative.failure "SDL_CreateTexture")

                SDL3.SDL_SetTextureBlendMode (cpuTexture, SDL_BlendMode.SDL_BLENDMODE_NONE)
                |> ignore

                cpuWidth <- width
                cpuHeight <- height
                pixels <- Array.zeroCreate (width * height * 4)
                fullPaint <- true

            let painted =
                if fullPaint then
                    host.Paint pixels
                    fullPaint <- false
                    true
                else
                    (host.PaintDirty pixels).IsSome

            if painted then
                use start = fixed pixels

                if not (PresentationNative.ok (SDL3.SDL_UpdateTexture (cpuTexture, NativePtr.nullPtr, NativePtr.toNativeInt start, width * 4))) then
                    raise (PresentationNative.failure "SDL_UpdateTexture")

            painted
        else
            false

    let capture () =
        let surface = SDL3.SDL_RenderReadPixels (renderer, NativePtr.nullPtr)

        if not (NativePtr.isNullPtr surface) then
            metal
            |> Option.iter (fun consumer -> consumer.ReadbackCompleted ())

        if NativePtr.isNullPtr surface then
            raise (PresentationNative.failure "SDL_RenderReadPixels")

        let converted =
            try
                SDL3.SDL_ConvertSurface (surface, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888)
            finally
                SDL3.SDL_DestroySurface surface

        if NativePtr.isNullPtr converted then
            raise (PresentationNative.failure "SDL_ConvertSurface")

        try
            let image = NativePtr.read converted
            let bytes = Array.zeroCreate<byte>(image.w * image.h * 4)

            for row in 0 .. image.h - 1 do
                Marshal.Copy (image.pixels + nativeint (row * image.pitch), bytes, row * image.w * 4, image.w * 4)

            struct (image.w, image.h, bytes)
        finally
            SDL3.SDL_DestroySurface converted

    let releasePendingEglFrame () =
        if pendingEglFrame <> 0n then
            match egl with
            | Some consumer ->
                if not (consumer.Activate ()) then
                    raise (PresentationNative.failure "SDL_GL_MakeCurrent")

                let flushed = PresentationNative.ok (SDL3.SDL_FlushRenderer renderer)

                if not (NativePtr.isNullPtr gpuTexture) then
                    SDL3.SDL_DestroyTexture gpuTexture
                    gpuTexture <- NativePtr.nullPtr
                    gpuResource <- 0n

                let released = consumer.Release pendingEglFrame

                if released = 0 then
                    pendingEglFrame <- 0n

                if not flushed then
                    raise (PresentationNative.failure "SDL_FlushRenderer")

                if released <> 0 then
                    raise (
                        InvalidOperationException (
                            "GPU frame release failed: "
                            + released.ToString ()
                        )
                    )
            | None -> invalidOp "An EGL frame requires its consumer renderer"

    let releasePendingMetalFrame () =
        metal
        |> Option.iter (fun consumer ->
            consumer.ReleasePending (fun () ->
                if not (NativePtr.isNullPtr gpuTexture) then
                    SDL3.SDL_DestroyTexture gpuTexture
                    gpuTexture <- NativePtr.nullPtr
                    gpuResource <- 0n))

    let present () =
        match metal with
        | Some consumer -> consumer.Present ()
        | None -> PresentationNative.ok (SDL3.SDL_RenderPresent renderer)

    member private _.WithGpu(action: nativeptr<SDL_Texture> -> int -> int -> 'T) : 'T voption =
        releasePendingEglFrame ()
        releasePendingMetalFrame ()
        let mutable frame = 0n

        let acquired =
            if not gpuEnabled then
                -9
            else
                match egl, metal with
                | Some consumer, _ -> consumer.Acquire (host.Handle, &frame)
                | _, Some consumer -> consumer.Acquire (host.Handle, &frame)
                | None, None -> RanvierWebCoreAbi.Native.rv_host_acquire_d3d11_frame (host.Handle, device (), &frame)

        if acquired <> 0 then
            ValueNone
        else
            if egl.IsSome then
                pendingEglFrame <- frame

            try
                let mutable resource = 0n
                let mutable width, height, format = 0, 0, 0

                if
                    RanvierWebCoreAbi.Native.rv_gpu_frame_info (frame, &resource, &width, &height, &format)
                    <> 0
                    || resource = 0n
                    || width <= 0
                    || height <= 0
                    || format <> (if metal.IsSome then 2 else 1)
                then
                    ValueNone
                else
                    if
                        resource <> gpuResource
                        || width <> gpuWidth
                        || height <> gpuHeight
                        || NativePtr.isNullPtr gpuTexture
                    then
                        if not (NativePtr.isNullPtr gpuTexture) then
                            SDL3.SDL_DestroyTexture gpuTexture

                        gpuTexture <-
                            match egl, metal with
                            | Some consumer, _ -> consumer.Import (resource, width, height)
                            | _, Some consumer -> consumer.Import (resource, width, height)
                            | None, None -> importTexture resource width height

                        gpuResource <- resource
                        gpuWidth <- width
                        gpuHeight <- height

                        if not (NativePtr.isNullPtr gpuTexture) then
                            if
                                not (
                                    PresentationNative.ok (SDL3.SDL_SetTextureBlendMode (gpuTexture, SDL_BlendMode.SDL_BLENDMODE_BLEND_PREMULTIPLIED))
                                )
                            then
                                SDL3.SDL_DestroyTexture gpuTexture
                                gpuTexture <- NativePtr.nullPtr

                    if NativePtr.isNullPtr gpuTexture then
                        ValueNone
                    else
                        ValueSome (action gpuTexture width height)
            finally
                match egl, metal with
                | Some _, _ -> releasePendingEglFrame ()
                | _, Some _ -> releasePendingMetalFrame ()
                | None, None ->
                    let flushed = PresentationNative.ok (SDL3.SDL_FlushRenderer renderer)
                    let released = RanvierWebCoreAbi.Native.rv_gpu_frame_release frame

                    if not flushed then
                        raise (PresentationNative.failure "SDL_FlushRenderer")

                    if released <> 0 then
                        raise (
                            InvalidOperationException (
                                "GPU frame release failed: "
                                + released.ToString ()
                            )
                        )

    /// <summary>Retries retained GPU frames before disposing their consumer; failure preserves ownership.</summary>
    member _.ReleasePending() =
        releasePendingEglFrame ()
        releasePendingMetalFrame ()

    /// <summary>The retained native EGL frame, or zero after successful release.</summary>
    member _.PendingEglFrame = pendingEglFrame

    /// <summary>The IOSurface frame retained until consumer completion and native release succeed.</summary>
    member _.PendingIOSurfaceFrame =
        metal
        |> Option.map (fun consumer -> consumer.PendingFrame)
        |> Option.defaultValue 0n

    /// <summary>Wraps actual Metal completion for per-renderer failure controls; false means another transport.</summary>
    member _.SetIOSurfaceCompletion(wrapper: (unit -> unit) -> unit) =
        match metal with
        | Some consumer ->
            consumer.SetCompletion wrapper
            true
        | None -> false

    /// <summary>Wraps native IOSurface release for per-renderer failure controls; false means another transport.</summary>
    member _.SetIOSurfaceFrameRelease(release: nativeint -> int) =
        match metal with
        | Some consumer ->
            consumer.SetFrameRelease release
            true
        | None -> false

    /// <summary>Requires a full CPU repaint after viewport or display-scale changes.</summary>
    member _.Invalidate() =
        fullPaint <- true

    /// <summary>The number of shared GPU frames presented to this renderer.</summary>
    member _.GpuFramesPresented = gpuFrames

    /// <summary>The number of presentation attempts that took the CPU path.</summary>
    member _.CpuFramesPresented = cpuFrames

    /// <summary>Presents the updated page, with CPU painting when shared texture import is unavailable.</summary>
    member this.Present(exposed: bool) =
        match
            this.WithGpu (fun texture width height ->
                draw texture width height

                if not (present ()) then
                    raise (PresentationNative.failure "SDL_RenderPresent"))
        with
        | ValueSome () ->
            gpuFrames <- gpuFrames + 1L
            lastGpu <- true
            fullPaint <- true
        | ValueNone ->
            cpuFrames <- cpuFrames + 1L
            lastGpu <- false
            let painted = updateCpu ()

            if painted || exposed then
                draw cpuTexture cpuWidth cpuHeight

                if not (PresentationNative.ok (SDL3.SDL_RenderPresent renderer)) then
                    raise (PresentationNative.failure "SDL_RenderPresent")

    /// <summary>Captures the rendered page as physical dimensions and top-down straight RGBA bytes.</summary>
    member this.ReadPixels() =
        match
            (if lastGpu then
                 this.WithGpu (fun texture width height ->
                     draw texture width height
                     capture ())
             else
                 ValueNone)
        with
        | ValueSome pixels -> pixels
        | ValueNone ->
            if lastGpu then
                fullPaint <- true

            updateCpu () |> ignore
            draw cpuTexture cpuWidth cpuHeight
            capture ()

    interface IDisposable with
        member _.Dispose() =
            releasePendingEglFrame ()
            releasePendingMetalFrame ()

            egl
            |> Option.iter (fun consumer ->
                if not (consumer.Activate ()) then
                    raise (PresentationNative.failure "SDL_GL_MakeCurrent"))

            if not (NativePtr.isNullPtr gpuTexture) then
                SDL3.SDL_DestroyTexture gpuTexture
                gpuTexture <- NativePtr.nullPtr

            if not (NativePtr.isNullPtr cpuTexture) then
                SDL3.SDL_DestroyTexture cpuTexture
                cpuTexture <- NativePtr.nullPtr

/// <summary>Attaches the native Windows UI Automation tree to an SDL window on its owner thread.</summary>
module internal WindowAccessibility =
    /// <summary>Attaches a supported bridge and returns whether it is active.</summary>
    let attach (host: WebCoreHost) (window: nativeptr<SDL_Window>) =
        if
            OperatingSystem.IsWindows ()
            && WebCoreHost.Has "accessibility-uia"
        then
            use key = fixed PresentationNative.utf8 "SDL.window.win32.hwnd"

            let hwnd =
                SDL3.SDL_GetPointerProperty (SDL3.SDL_GetWindowProperties window, key, 0n)

            let result = RanvierWebCoreAbi.Native.rv_host_attach_uia (host.Handle, hwnd)

            if result = 0 then
                true
            elif result = -9 then
                false
            else
                raise (
                    InvalidOperationException (
                        "UI Automation attachment failed: "
                        + result.ToString ()
                    )
                )
        else
            false

    /// <summary>Detaches before disposing the page or destroying its native window.</summary>
    let detach (host: WebCoreHost) =
        if not host.Session.IsDisposed then
            let result = RanvierWebCoreAbi.Native.rv_host_detach_uia host.Handle

            if result <> 0 then
                raise (
                    InvalidOperationException (
                        "UI Automation detachment failed: "
                        + result.ToString ()
                    )
                )
