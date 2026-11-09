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
type internal Presenter(renderer: nativeptr<SDL_Renderer>, host: WebCoreHost, egl: EglRenderer option, swapChain: D3D11SwapChain option) =
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

    // The frame on screen or being presented through the swap chain, released once its present has finished.
    let mutable heldFrame = 0n

    let device () =
        match swapChain with
        | Some chain -> chain.Device
        | None ->
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

    let releaseHeldFrame () =
        if heldFrame <> 0n then
            let released = RanvierWebCoreAbi.Native.rv_gpu_frame_release heldFrame

            if released <> 0 then
                raise (
                    InvalidOperationException (
                        "GPU frame release failed: "
                        + released.ToString ()
                    )
                )

            heldFrame <- 0n

    // True when the finished present lost the device.
    let settleSwapChain (chain: D3D11SwapChain) =
        let lost = chain.Settle ()

        if not chain.Presenting then
            releaseHeldFrame ()

        lost

    let waitSwapChain (chain: D3D11SwapChain) =
        chain.WaitIdle ()
        settleSwapChain chain |> ignore

    // Acquires the composited frame on the swap chain's device: its texture and size, with the frame in heldFrame.
    let acquireForSwapChain () =
        let mutable frame = 0n

        if
            gpuEnabled
            && RanvierWebCoreAbi.Native.rv_host_acquire_d3d11_frame (host.Handle, device (), &frame) = 0
        then
            let mutable resource = 0n
            let mutable width, height, format = 0, 0, 0

            if
                RanvierWebCoreAbi.Native.rv_gpu_frame_info (frame, &resource, &width, &height, &format) = 0
                && resource <> 0n
                && width > 0
                && height > 0
                && format = 1
            then
                heldFrame <- frame
                ValueSome (struct (resource, width, height))
            else
                RanvierWebCoreAbi.Native.rv_gpu_frame_release frame
                |> ignore

                ValueNone
        else
            ValueNone

    // Paints into pixels when the page changed; the returned flag says whether it did.
    let paintForSwapChain () =
        let struct (width, height) = host.PaintSize

        if width > 0 && height > 0 then
            if width <> cpuWidth || height <> cpuHeight then
                cpuWidth <- width
                cpuHeight <- height
                pixels <- Array.zeroCreate (width * height * 4)
                fullPaint <- true

            if fullPaint then
                host.Paint pixels
                fullPaint <- false
                true
            else
                (host.PaintDirty pixels).IsSome
        else
            false

    let presentOnSwapChain (chain: D3D11SwapChain) (exposed: bool) =
        waitSwapChain chain

        if not (chain.Prepare ()) then
            false
        else
            match acquireForSwapChain () with
            | ValueSome (struct (texture, width, height)) ->
                chain.Submit (texture, width, height)
                gpuFrames <- gpuFrames + 1L
                lastGpu <- true
                fullPaint <- true
                true
            | ValueNone ->
                cpuFrames <- cpuFrames + 1L
                lastGpu <- false

                let painted = paintForSwapChain ()

                if painted || (exposed && cpuWidth > 0) then
                    chain.Submit (chain.UploadCpu (pixels, cpuWidth, cpuHeight), cpuWidth, cpuHeight)

                true

    // Top-down RGBA of the page. A GPU frame reads as opaque, as it appears composited over the window's black.
    let readFromSwapChain (chain: D3D11SwapChain) =
        waitSwapChain chain

        if not (chain.Prepare ()) then
            invalidOp "The window's D3D11 device is lost"

        let gpu = if lastGpu then acquireForSwapChain () else ValueNone

        match gpu with
        | ValueSome (struct (texture, width, height)) ->
            try
                let bytes = chain.Read (texture, width, height)

                for index in 3..4 .. bytes.Length - 1 do
                    bytes[index] <- 255uy

                struct (width, height, bytes)
            finally
                releaseHeldFrame ()
        | ValueNone ->
            if lastGpu then
                fullPaint <- true

            paintForSwapChain () |> ignore
            struct (cpuWidth, cpuHeight, chain.Read (chain.UploadCpu (pixels, cpuWidth, cpuHeight), cpuWidth, cpuHeight))

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

    /// <summary>A presenter without a D3D11 swap chain.</summary>
    new(renderer: nativeptr<SDL_Renderer>, host: WebCoreHost, egl: EglRenderer option) = new Presenter (renderer, host, egl, None)

    /// <summary>Whether a present is running on the swap chain's thread; always false without a swap chain.</summary>
    member _.Presenting =
        match swapChain with
        | Some chain -> chain.Presenting
        | None -> false

    /// <summary>Takes a finished present's outcome: true when the window must be drawn again because the device was lost.</summary>
    member _.Settle() =
        match swapChain with
        | Some chain -> settleSwapChain chain
        | None -> false

    /// <summary>Sets the swap chain's back buffer size for the next present.</summary>
    member _.Resized(width: int, height: int) =
        swapChain
        |> Option.iter (fun chain -> chain.Resize (width, height))

    /// <summary>Sets how later swap chain frames reach the window.</summary>
    member _.Pace(visible: bool, refreshMs: int) =
        swapChain
        |> Option.iter (fun chain -> chain.Pace (visible, refreshMs))

    /// <summary>The swap chain frames go through, for failure controls.</summary>
    member _.SwapChain = swapChain

    /// <summary>Retries retained GPU frames before disposing their consumer; failure preserves ownership.</summary>
    member _.ReleasePending() =
        swapChain |> Option.iter waitSwapChain
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

    /// <summary>
    /// Presents the updated page, with CPU painting when shared texture import is unavailable. Through a swap chain the
    /// present runs on its own thread after this returns. False when a lost device left nothing to draw to.
    /// </summary>
    member this.Present(exposed: bool) =
        match swapChain with
        | Some chain -> presentOnSwapChain chain exposed
        | None ->
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

            true

    /// <summary>Captures the rendered page as physical dimensions and top-down straight RGBA bytes.</summary>
    member this.ReadPixels() =
        match swapChain with
        | Some chain -> readFromSwapChain chain
        | None ->
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
            swapChain |> Option.iter waitSwapChain
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

            swapChain
            |> Option.iter (fun chain -> (chain :> IDisposable).Dispose())

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
