namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore

module private MetalNative =
    [<DllImport("/System/Library/Frameworks/CoreVideo.framework/CoreVideo", CallingConvention = CallingConvention.Cdecl)>]
    extern int CVPixelBufferCreateWithIOSurface(nativeint allocator, nativeint surface, nativeint attributes, nativeint& pixelBuffer)

    [<DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)>]
    extern void CFRelease(nativeint value)

    [<DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint objc_getClass(string name)

    [<DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint sel_registerName(string name)

    [<DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint send(nativeint target, nativeint selector)

    [<DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)>]
    extern void sendVoid(nativeint target, nativeint selector)

    let ok (value: SDLBool) =
        SDLBool.op_Implicit value

    let utf8 (value: string) =
        Encoding.UTF8.GetBytes (value + "\000")

    let failure operation =
        InvalidOperationException (operation + " failed: " + SDL3.SDL_GetError ())

    let pointer renderer key =
        use name = fixed utf8 key
        SDL3.SDL_GetPointerProperty (SDL3.SDL_GetRendererProperties renderer, name, 0n)

    let completion queue =
        let start () =
            let pool = send (objc_getClass "NSAutoreleasePool", sel_registerName "new")

            try
                let buffer = send (queue, sel_registerName "commandBuffer")

                if buffer = 0n then
                    invalidOp "The Metal command queue could not create a completion fence"

                sendVoid (buffer, sel_registerName "retain")
                sendVoid (buffer, sel_registerName "commit")
                buffer
            finally
                sendVoid (pool, sel_registerName "drain")

        let ready buffer =
            match send (buffer, sel_registerName "status") with
            | 4n -> true
            | 5n -> invalidOp "The Metal command buffer failed"
            | _ -> false

        CompletionFence (
            start,
            ready,
            (fun buffer -> sendVoid (buffer, sel_registerName "waitUntilCompleted")),
            (fun buffer -> sendVoid (buffer, sel_registerName "release"))
        )

    let finish queue =
        let pool = send (objc_getClass "NSAutoreleasePool", sel_registerName "new")

        try
            let buffer = send (queue, sel_registerName "commandBuffer")

            if buffer = 0n then
                invalidOp "The Metal command queue could not create a completion fence"

            sendVoid (buffer, sel_registerName "commit")
            sendVoid (buffer, sel_registerName "waitUntilCompleted")

            if send (buffer, sel_registerName "status") <> 4n then
                invalidOp "The Metal completion fence did not complete successfully"
        finally
            sendVoid (pool, sel_registerName "drain")

/// <summary>Imports completed IOSurface frames into the SDL Metal renderer on its owner thread.</summary>
type internal MetalRenderer private (renderer: nativeptr<SDL_Renderer>, queue: nativeint) =
    let mutable pendingFrame = 0n
    let mutable submitted = false
    let mutable complete = fun () -> MetalNative.finish queue
    let mutable release = RanvierWebCoreAbi.Native.rv_gpu_frame_release
    let fence = MetalNative.completion queue
    let mutable pipelineFrame = false

    /// <summary>Creates a consumer only for a Metal renderer and a native runtime advertising IOSurface frames.</summary>
    static member TryCreate(renderer: nativeptr<SDL_Renderer>) =
        if
            OperatingSystem.IsMacOS ()
            && WebCoreHost.Has "iosurface-frames"
        then
            let queue = MetalNative.pointer renderer "SDL.renderer.metal.command_queue"

            if queue <> 0n then
                Some (new MetalRenderer (renderer, queue))
            else
                None
        else
            None

    /// <summary>Acquires an independent native surface whose producer GPU work has completed.</summary>
    member _.Acquire(host: nativeint, frame: byref<nativeint>) =
        if pendingFrame <> 0n then
            invalidOp "An IOSurface frame is awaiting consumer completion"

        let status = RanvierWebCoreAbi.Native.rv_host_acquire_iosurface_frame (host, &frame)

        if status = 0 then
            pendingFrame <- frame
            submitted <- false
            pipelineFrame <- false

        status

    /// <summary>The renderer's queue identity used to configure its native frame pool.</summary>
    member _.Queue = queue

    /// <summary>Retains an already-completed producer frame until Metal consumption finishes.</summary>
    member _.Adopt(frame: nativeint) =
        if pendingFrame <> 0n then
            invalidOp "An IOSurface frame is awaiting consumer completion"

        pendingFrame <- frame
        submitted <- false
        pipelineFrame <- true

    /// <summary>Polls consumer completion without waiting; the caller retries on its next scheduled frame.</summary>
    member _.TryReleasePending(retireTexture: unit -> unit) =
        if pendingFrame = 0n then
            true
        elif not submitted then
            false
        elif not (fence.TryComplete ()) then
            false
        else
            retireTexture ()
            let status = release pendingFrame

            if status <> 0 then
                invalidOp ("IOSurface frame release failed: " + string status)

            pendingFrame <- 0n
            pipelineFrame <- false
            true

    /// <summary>Imports premultiplied BGRA8 sRGB storage without copying pixels through the CPU.</summary>
    member _.Import(surface: nativeint, width: int, height: int) =
        let mutable pixelBuffer = 0n

        if
            MetalNative.CVPixelBufferCreateWithIOSurface (0n, surface, 0n, &pixelBuffer)
            <> 0
            || pixelBuffer = 0n
        then
            if pixelBuffer <> 0n then
                MetalNative.CFRelease pixelBuffer

            NativePtr.nullPtr<SDL_Texture>
        else
            try
                let properties = SDL3.SDL_CreateProperties ()

                if properties = LanguagePrimitives.EnumOfValue 0u then
                    NativePtr.nullPtr<SDL_Texture>
                else
                    try
                        let number key value =
                            use name = fixed MetalNative.utf8 key
                            MetalNative.ok (SDL3.SDL_SetNumberProperty (properties, name, value))

                        use key = fixed MetalNative.utf8 "SDL.texture.create.metal.pixelbuffer"

                        if
                            number "SDL.texture.create.width" (int64 width)
                            && number "SDL.texture.create.height" (int64 height)
                            && number "SDL.texture.create.format" (int64 SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888)
                            && MetalNative.ok (SDL3.SDL_SetPointerProperty (properties, key, pixelBuffer))
                        then
                            SDL3.SDL_CreateTextureWithProperties (renderer, properties)
                        else
                            NativePtr.nullPtr<SDL_Texture>
                    finally
                        SDL3.SDL_DestroyProperties properties
            finally
                MetalNative.CFRelease pixelBuffer

    /// <summary>Submits the SDL command buffer, including when a hidden window cannot display its drawable.</summary>
    member _.Present() =
        let result = MetalNative.ok (SDL3.SDL_RenderPresent renderer)
        submitted <- true
        result

    /// <summary>Records the completed submission performed by SDL's Metal readback operation.</summary>
    member _.ReadbackCompleted() =
        submitted <- true

    /// <summary>Completes consumer GPU work before releasing the surface; failure retains the native frame for retry.</summary>
    member _.ReleasePending(retireTexture: unit -> unit) =
        if pendingFrame <> 0n then
            if not submitted then
                if not (MetalNative.ok (SDL3.SDL_FlushRenderer renderer)) then
                    raise (MetalNative.failure "SDL_FlushRenderer")
                // Metal RenderPresent commits even when the drawable is unavailable.
                SDL3.SDL_RenderPresent renderer |> ignore
                submitted <- true

            if pipelineFrame then fence.Complete () else complete ()
            retireTexture ()
            let status = release pendingFrame

            if status <> 0 then
                invalidOp ("IOSurface frame release failed: " + string status)

            pendingFrame <- 0n

    /// <summary>The frame retained until Metal completion and native release succeed.</summary>
    member _.PendingFrame = pendingFrame

    /// <summary>Wraps actual queue completion for per-renderer failure and retry controls.</summary>
    member _.SetCompletion(wrapper: (unit -> unit) -> unit) =
        complete <- fun () -> wrapper (fun () -> MetalNative.finish queue)

    /// <summary>Wraps native release for per-renderer failure and retry controls.</summary>
    member _.SetFrameRelease(releaseFrame: nativeint -> int) =
        release <- releaseFrame
