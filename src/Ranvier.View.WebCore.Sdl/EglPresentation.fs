namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore

module private EglNative =
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type ImageTarget = delegate of uint32 * nativeint -> unit

    [<DllImport("libGLESv2.so.2", CallingConvention = CallingConvention.Cdecl)>]
    extern void glBindTexture(uint32 target, uint32 texture)

    [<DllImport("libGLESv2.so.2", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 glGetError()

module private EglCohort =
    // WebCore's display, contexts and texture pool persist after the last window.
    // One VIDEO and GL driver reference prevent SDL from terminating that display.
    // The OS reclaims this single cohort at exit; per-window resources are disposed normally.
    let mutable display = 0n

    let configure candidate =
        if display <> 0n then
            display = candidate
        elif not (SDLBool.op_Implicit (SDL3.SDL_InitSubSystem SDL_InitFlags.SDL_INIT_VIDEO)) then
            false
        else
            let mutable driverLoaded = false
            let mutable committed = false

            try
                driverLoaded <- SDLBool.op_Implicit (SDL3.SDL_GL_LoadLibrary NativePtr.nullPtr<byte>)

                if
                    driverLoaded
                    && RanvierWebCoreAbi.Native.rv_configure_egl_display candidate = 0
                then
                    display <- candidate
                    committed <- true

                committed
            finally
                if not committed then
                    if driverLoaded then
                        SDL3.SDL_GL_UnloadLibrary ()

                    SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO

module private FailedConstruction =
    // A failed activation cannot safely delete GL objects. Block new allocations
    // until this single per-window owner can make its context current and clean up.
    let mutable cleanup: (int * (unit -> bool)) option = None

    let retry () =
        match cleanup with
        | Some (thread, _) when thread <> Environment.CurrentManagedThreadId -> invalidOp "SDL EGL construction cleanup must run on its owner thread"
        | Some (_, pending) when pending () -> cleanup <- None
        | Some _ -> invalidOp "An SDL EGL window is awaiting construction cleanup"
        | None -> ()

    let retain pending =
        if cleanup.IsSome then
            invalidOp "An SDL EGL construction cleanup owner already exists"

        cleanup <- Some (Environment.CurrentManagedThreadId, pending)

/// <summary>Imports Linux EGL frames into an SDL GLES renderer on its owner thread.</summary>
/// <remarks>The shared EGL cohort lives until process exit; this window's renderer owns its context and textures.</remarks>
type internal EglRenderer private (renderer: nativeptr<SDL_Renderer>, window: nativeptr<SDL_Window>, context: nativeptr<SDL_GLContextState>) =
    let mutable releaseNative = RanvierWebCoreAbi.Native.rv_gpu_frame_release

    let activate () =
        // WebCore switches raw EGL contexts without updating SDL's cached current context.
        SDLBool.op_Implicit (SDL3.SDL_GL_MakeCurrent (window, NativePtr.nullPtr<SDL_GLContextState>))
        && SDLBool.op_Implicit (SDL3.SDL_GL_MakeCurrent (window, context))

    /// <summary>Restores the actual consumer context after native WebCore calls.</summary>
    member _.Activate() =
        activate ()

    /// <summary>The borrowed EGL consumer context used to configure its native frame pool.</summary>
    member _.Context = NativePtr.toNativeInt context

    /// <summary>The configured process EGL display handle, or zero before configuration.</summary>
    static member CurrentDisplay = EglCohort.display

    /// <summary>Finishes the single pending constructor cleanup before allocating another window.</summary>
    static member RetryFailedConstruction() =
        FailedConstruction.retry ()

    /// <summary>Retains one failed constructor's SDL resources until its context can be activated.</summary>
    static member RetainFailedConstruction(cleanup: unit -> bool) =
        FailedConstruction.retain cleanup

    /// <summary>Acquires a native frame with this renderer's consumer context current.</summary>
    member _.Acquire(host: nativeint, frame: byref<nativeint>) =
        if activate () then
            RanvierWebCoreAbi.Native.rv_host_acquire_egl_frame (host, &frame)
        else
            -9

    /// <summary>Imports the borrowed EGLImage into a new SDL texture, or returns a null texture on failure.</summary>
    member _.Import(image: nativeint, width: int, height: int) =
        if not (activate ()) then
            NativePtr.nullPtr<SDL_Texture>
        else
            let texture =
                SDL3.SDL_CreateTexture (renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, width, height)

            if NativePtr.isNullPtr texture then
                texture
            else
                let number =
                    SDL3.SDL_GetNumberProperty (SDL3.SDL_GetTextureProperties texture, "SDL.texture.opengles2.texture", 0L)

                let proc = SDL3.SDL_GL_GetProcAddress "glEGLImageTargetTexture2DOES"

                if number = 0L || proc = 0n then
                    SDL3.SDL_DestroyTexture texture
                    NativePtr.nullPtr
                else
                    let target = Marshal.GetDelegateForFunctionPointer<EglNative.ImageTarget> proc
                    EglNative.glBindTexture (0x0DE1u, uint32 number)
                    target.Invoke (0x0DE1u, image)

                    if EglNative.glGetError () = 0u then
                        texture
                    else
                        SDL3.SDL_DestroyTexture texture
                        NativePtr.nullPtr

    /// <summary>Releases after all imported textures are destroyed and this consumer's GPU work is submitted.</summary>
    member _.Release(frame: nativeint) =
        if activate () then releaseNative frame else -5

    /// <summary>Wraps native frame release for per-renderer failure and retry controls.</summary>
    member _.SetFrameRelease(release: nativeint -> int) =
        releaseNative <- release

    /// <summary>Requests EGL for Linux GPU windows before SDL selects its GL backend.</summary>
    static member Prepare() =
        if
            OperatingSystem.IsLinux ()
            && WebCoreHost.Has "presentation-egl"
        then
            SDL3.SDL_SetHint ("SDL_VIDEO_FORCE_EGL", "1")
            |> ignore

    /// <summary>Captures a GLES renderer for cleanup without committing the process EGL cohort.</summary>
    static member Capture(renderer: nativeptr<SDL_Renderer>) =
        if
            OperatingSystem.IsLinux ()
            && WebCoreHost.Has "presentation-egl"
            && SDL3.SDL_GetRendererName renderer = "opengles2"
        then
            let display = SDL3.SDL_EGL_GetCurrentDisplay ()
            let window = SDL3.SDL_GL_GetCurrentWindow ()
            let context = SDL3.SDL_GL_GetCurrentContext ()

            if
                display <> 0n
                && not (NativePtr.isNullPtr window)
                && not (NativePtr.isNullPtr context)
            then
                Some (new EglRenderer (renderer, window, context))
            else
                None
        else
            None

    /// <summary>Uses the process EGL cohort; the first registration must precede host creation.</summary>
    static member TryCreate(renderer: nativeptr<SDL_Renderer>) =
        match EglRenderer.Capture renderer with
        | Some consumer when EglCohort.configure (SDL3.SDL_EGL_GetCurrentDisplay ()) -> Some consumer
        | _ -> None
