namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open Ranvier.View.WebCore

#nowarn "9" "51"

module private GpuSurfaceD3D11 =
    [<Literal>]
    let FormatRgba8 = 28u

    [<Literal>]
    let BindShaderAndRenderTarget = 0x28u // D3D11_BIND_SHADER_RESOURCE (0x8) | D3D11_BIND_RENDER_TARGET (0x20)

    [<Literal>]
    let MiscShared = 0x2u // D3D11_RESOURCE_MISC_SHARED

    [<Literal>]
    let MiscSharedNtHandle = 0x800u // D3D11_RESOURCE_MISC_SHARED_NTHANDLE

    [<Literal>]
    let MiscSharedKeyedMutex = 0x100u // D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX

    [<Literal>]
    let GenericAll = 0x10000000u

    [<Literal>]
    let WaitTimeout = 0x00000102

    let dxgiDeviceId = Guid "77db970f-6276-48ba-ba28-070143b4392c"
    let dxgiKeyedMutexId = Guid "9d8e1289-d7b3-465f-8126-250e349af85d"
    let dxgiResource1Id = Guid "30961379-4609-4a41-998e-54fe567ee0c1"

    [<Struct; StructLayout(LayoutKind.Sequential); NoEquality; NoComparison>]
    type TextureDesc =
        {
            Width: uint32
            Height: uint32
            MipLevels: uint32
            ArraySize: uint32
            Format: uint32
            SampleCount: uint32
            SampleQuality: uint32
            Usage: int
            BindFlags: uint32
            CpuAccessFlags: uint32
            MiscFlags: uint32
        }

        override _.ToString() =
            "TextureDesc"

    type GetAdapter = delegate of nativeint * byref<nativeint> -> int
    type CreateTexture = delegate of nativeint * byref<TextureDesc> * nativeint * byref<nativeint> -> int
    type CopyResource = delegate of nativeint * nativeint * nativeint -> unit
    type UpdateResource = delegate of nativeint * nativeint * uint32 * nativeint * nativeint * uint32 * uint32 -> unit
    type ContextCall = delegate of nativeint -> unit
    type AcquireSync = delegate of nativeint * uint64 * uint32 -> int
    type ReleaseSync = delegate of nativeint * uint64 -> int
    type CreateSharedHandle = delegate of nativeint * nativeint * uint32 * nativeint * byref<nativeint> -> int

    [<DllImport("d3d11.dll")>]
    extern int D3D11CreateDevice(
        nativeint adapter,
        int driverType,
        nativeint software,
        uint32 flags,
        nativeint featureLevels,
        uint32 featureLevelCount,
        uint32 sdkVersion,
        nativeint& device,
        int& featureLevel,
        nativeint& context
    )

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CloseHandle(nativeint hObject)

    let inline comMethod<'Method when 'Method :> Delegate> (this: nativeint) (slot: int) : 'Method =
        Marshal.GetDelegateForFunctionPointer<'Method>(Marshal.ReadIntPtr (Marshal.ReadIntPtr this, slot * IntPtr.Size))

    let drop (value: nativeint) =
        if value <> 0n then
            Marshal.Release value |> ignore

    let closeNtHandle (handle: nativeint) =
        if handle <> 0n then
            CloseHandle handle |> ignore

    let tryCreateProducerDevice (consumerDevice: nativeint) =
        let mutable dxgiDevice = 0n
        let mutable adapter = 0n

        try
            let mutable id = dxgiDeviceId

            if
                consumerDevice <> 0n
                && Marshal.QueryInterface (consumerDevice, &id, &dxgiDevice)
                   >= 0
            then
                (comMethod<GetAdapter> dxgiDevice 7).Invoke(dxgiDevice, &adapter)
                |> ignore

            let mutable created = 0n
            let mutable immediate = 0n
            let mutable level = 0
            // D3D_DRIVER_TYPE_UNKNOWN (0) is required when an explicit IDXGIAdapter is supplied; 1 is HARDWARE.
            let driverType = if adapter <> 0n then 0 else 1

            if
                D3D11CreateDevice (adapter, driverType, 0n, 0u, 0n, 0u, 7u, &created, &level, &immediate)
                >= 0
            then
                ValueSome (struct (created, immediate))
            else
                ValueNone
        finally
            drop adapter
            drop dxgiDevice

/// <summary>The transport backing an element's external GPU surface.</summary>
[<RequireQualifiedAccess>]
type GpuSurfaceKind =
    /// <summary>A Windows D3D11 NT-shared keyed-mutex texture composited directly by WebCore.</summary>
    | D3D11SharedTexture
    /// <summary>A macOS <c>IOSurfaceRef</c> composited directly by WebCore and Metal.</summary>
    | MetalIOSurface
    /// <summary>A Linux <c>EGLImage</c> composited directly by WebCore on the process EGL display.</summary>
    | EglImage
    /// <summary>Tightly packed RGBA pixels drawn into a DOM <c>&lt;canvas&gt;</c> when GPU element input is unavailable.</summary>
    | CpuFallback

/// <summary>
/// Binds an external GPU renderer (or pixel producer) to a composited DOM element in an SDL WebCore window so HTML, CSS,
/// and SVG layers composite naturally over it while disabling drag-selection highlighting by default.
/// </summary>
[<Sealed>]
type GpuSurface internal (host: WebCoreHost, presenter: Presenter, element: Element, preventSelection: bool, onDisposed: GpuSurface -> unit) =
    do
        if isNull (box element) then
            nullArg "element"

        if host.Session.IsDisposed then
            raise (ObjectDisposedException "Window")

        if not host.Session.IsOwnerThread then
            invalidOp "A GPU surface must be bound on the window's owner thread"

    let mutable disposed = false
    let mutable curWidth = 0
    let mutable curHeight = 0
    let mutable framesPublished = 0L

    // Windows D3D11 shared texture state
    let mutable d3dDevice = 0n
    let mutable d3dContext = 0n
    let mutable createTex: GpuSurfaceD3D11.CreateTexture = null
    let mutable copyRes: GpuSurfaceD3D11.CopyResource = null
    let mutable updateRes: GpuSurfaceD3D11.UpdateResource = null
    let mutable flushCtx: GpuSurfaceD3D11.ContextCall = null
    let mutable targetTexture = 0n
    let mutable targetMutex = 0n
    let mutable boundD3D11Texture = 0n
    let mutable stagingTexture = 0n
    let mutable stagingNtHandle = 0n
    let mutable stagingWidth = 0
    let mutable stagingHeight = 0

    // macOS / Linux native surface tracking
    let mutable nativeLayer: Element option = None
    let mutable ownsNativeLayer = false
    let mutable boundIOSurface = 0n
    let mutable boundIOSurfaceFlipY = false
    let mutable boundEglImage = 0n
    let mutable boundEglFlipY = false

    // CPU fallback state
    let mutable fallbackCanvas: HTMLCanvasElement option = None
    let mutable fallbackCanvasSvc: CanvasContextService option = None
    let mutable fallbackPixelSvc: ImageDataService option = None
    let mutable fallbackCtx: CanvasRenderingContext2D option = None
    let mutable fallbackImageData: ImageData option = None
    let mutable fallbackScratch: byte array = Array.empty
    let mutable ownsFallbackCanvas = false

    let kind =
        if
            OperatingSystem.IsWindows ()
            && presenter.GpuCompositingEnabled
            && WebCoreHost.Has "texture-input-d3d11"
            && presenter.D3D11Device <> 0n
        then
            match GpuSurfaceD3D11.tryCreateProducerDevice presenter.D3D11Device with
            | ValueSome (struct (dev, ctx)) ->
                d3dDevice <- dev
                d3dContext <- ctx
                createTex <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.CreateTexture> dev 5
                copyRes <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.CopyResource> ctx 47
                updateRes <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.UpdateResource> ctx 48
                flushCtx <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.ContextCall> ctx 111
                GpuSurfaceKind.D3D11SharedTexture
            | ValueNone -> GpuSurfaceKind.CpuFallback
        elif
            OperatingSystem.IsMacOS ()
            && presenter.GpuCompositingEnabled
            && WebCoreHost.Has "iosurface-input"
        then
            GpuSurfaceKind.MetalIOSurface
        elif
            OperatingSystem.IsLinux ()
            && presenter.GpuCompositingEnabled
            && WebCoreHost.Has "texture-input-egl"
            && presenter.Egl.IsSome
        then
            GpuSurfaceKind.EglImage
        else
            GpuSurfaceKind.CpuFallback

    // Promote element to composited layer and prevent accidental drag-highlighting by default.
    let listenerRemovers = ResizeArray<unit -> unit>()

    do
        let existingStyle =
            match element.getAttribute "style" with
            | null -> ""
            | s -> s.Trim ()

        let lowerStyle = existingStyle.ToLowerInvariant ()
        let additions = ResizeArray<string>()

        if not (lowerStyle.Contains "will-change") then
            additions.Add "will-change:transform"

        if
            not (box element :? HTMLCanvasElement)
            && not (lowerStyle.Contains "position:")
            && not (lowerStyle.Contains "position :")
        then
            additions.Add "position:relative"

        if preventSelection then
            if not (lowerStyle.Contains "-webkit-user-select") then
                additions.Add "-webkit-user-select:none"

            if
                not (
                    lowerStyle.Contains ";user-select"
                    || lowerStyle.StartsWith "user-select"
                )
            then
                additions.Add "user-select:none"

            if not (lowerStyle.Contains "-webkit-user-drag") then
                additions.Add "-webkit-user-drag:none"

            if not (lowerStyle.Contains "touch-action") then
                additions.Add "touch-action:none"

        if additions.Count > 0 then
            let extra = String.concat ";" additions

            let merged =
                if String.IsNullOrEmpty existingStyle then extra
                elif existingStyle.EndsWith ";" then existingStyle + extra
                else existingStyle + ";" + extra

            element.setAttribute ("style", merged)

        if preventSelection then
            if String.IsNullOrEmpty (element.getAttribute "draggable") then
                element.setAttribute ("draggable", "false")

            let registerPrevent (eventName: string) (listener: EventListener) =
                element.addEventListener (eventName, listener)
                listenerRemovers.Add (fun () -> element.removeEventListener (eventName, listener))

            registerPrevent "selectstart" (fun ev -> ev.preventDefault ())
            registerPrevent "dragstart" (fun ev -> ev.preventDefault ())

            registerPrevent "mousedown" (fun ev ->
                ev.preventDefault ()

                match box element with
                | :? HTMLElement as html when html.tabIndex >= 0 -> html.focus ()
                | _ -> ())

    let checkActive () =
        if disposed then
            raise (ObjectDisposedException (nameof GpuSurface))

        if host.Session.IsDisposed then
            raise (ObjectDisposedException "Window")

        if not host.Session.IsOwnerThread then
            invalidOp "A GPU surface must be used on the window's owner thread"

    let releaseStaging () =
        GpuSurfaceD3D11.closeNtHandle stagingNtHandle
        stagingNtHandle <- 0n
        GpuSurfaceD3D11.drop stagingTexture
        stagingTexture <- 0n
        stagingWidth <- 0
        stagingHeight <- 0

    let ensureNativeTarget () =
        match nativeLayer with
        | Some existing -> existing
        | None ->
            if box element :? HTMLCanvasElement then
                ownsNativeLayer <- false
                nativeLayer <- Some element
                element
            else
                let created = host.Document.createElement "div"
                created.setAttribute ("data-ranvier-gpu-surface", "layer")

                created.setAttribute (
                    "style",
                    "position:absolute;left:0;top:0;width:100%;height:100%;pointer-events:none;z-index:-1;will-change:transform;-webkit-user-select:none;user-select:none"
                )

                created.setAttribute ("draggable", "false")

                element.insertBefore (created, element.firstChild)
                |> ignore

                ownsNativeLayer <- true
                nativeLayer <- Some created
                created

    let releaseD3D11Target (unbindFromHost: bool) =
        if
            unbindFromHost
            && boundD3D11Texture <> 0n
            && not host.Session.IsDisposed
        then
            let target = defaultArg nativeLayer element

            try
                host.BindD3D11Texture (target, 0n)
            with _ ->
                ()

        boundD3D11Texture <- 0n
        GpuSurfaceD3D11.drop targetMutex
        targetMutex <- 0n
        GpuSurfaceD3D11.drop targetTexture
        targetTexture <- 0n

    let ensureD3D11Target (width: int) (height: int) =
        let w = max 1 width
        let h = max 1 height

        if
            targetTexture = 0n
            || w <> curWidth
            || h <> curHeight
        then
            let mutable desc: GpuSurfaceD3D11.TextureDesc =
                {
                    Width = uint32 w
                    Height = uint32 h
                    MipLevels = 1u
                    ArraySize = 1u
                    Format = GpuSurfaceD3D11.FormatRgba8
                    SampleCount = 1u
                    SampleQuality = 0u
                    Usage = 0
                    BindFlags = GpuSurfaceD3D11.BindShaderAndRenderTarget
                    CpuAccessFlags = 0u
                    MiscFlags =
                        GpuSurfaceD3D11.MiscSharedNtHandle
                        ||| GpuSurfaceD3D11.MiscSharedKeyedMutex
                }

            let mutable createdTex = 0n

            if
                createTex.Invoke (d3dDevice, &desc, 0n, &createdTex) < 0
                || createdTex = 0n
            then
                invalidOp "ID3D11Device::CreateTexture2D failed allocating shared keyed-mutex texture"

            let mutable mutexId = GpuSurfaceD3D11.dxgiKeyedMutexId
            let mutable createdMutex = 0n

            if
                Marshal.QueryInterface (createdTex, &mutexId, &createdMutex) < 0
                || createdMutex = 0n
            then
                GpuSurfaceD3D11.drop createdTex
                invalidOp "QueryInterface(IDXGIKeyedMutex) failed on shared texture"

            // Keep the old bound texture alive in WebCore until the first frame on createdTex binds over it,
            // then drop our local references to the previous texture/mutex.
            GpuSurfaceD3D11.drop targetMutex
            GpuSurfaceD3D11.drop targetTexture
            targetTexture <- createdTex
            targetMutex <- createdMutex
            curWidth <- w
            curHeight <- h

    let acquireProducerMutex (mutex: nativeint) =
        let acquire = GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.AcquireSync> mutex 8
        // Fast path: key 0 is available (newly created texture, or returned by WebCore composition).
        let first = acquire.Invoke (mutex, 0UL, 0u)

        if first = 0 then
            0UL
        elif first = GpuSurfaceD3D11.WaitTimeout then
            // If the producer published a previous frame before WebCore's compositor ran, the mutex sits idle at key 1.
            if acquire.Invoke (mutex, 1UL, 0u) = 0 then
                1UL
            elif acquire.Invoke (mutex, 0UL, 5000u) = 0 then
                0UL
            else
                invalidOp "IDXGIKeyedMutex::AcquireSync timed out waiting for WebCore composition"
        else
            invalidOp (
                "IDXGIKeyedMutex::AcquireSync failed: 0x"
                + first.ToString "X8"
            )

    let releaseProducerMutex (mutex: nativeint) (key: uint64) =
        let release = GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.ReleaseSync> mutex 9
        let hr = release.Invoke (mutex, key)

        if hr < 0 then
            invalidOp (
                "IDXGIKeyedMutex::ReleaseSync failed: 0x"
                + hr.ToString "X8"
            )

    let publishBoundD3D11 () =
        let target = ensureNativeTarget ()

        if boundD3D11Texture <> targetTexture then
            host.BindD3D11Texture (target, targetTexture)
            boundD3D11Texture <- targetTexture
        else
            host.NativeTextureChanged target

        framesPublished <- framesPublished + 1L

    let ensureFallbackCanvas (width: int) (height: int) =
        let w = max 1 width
        let h = max 1 height

        let canvasSvc =
            match fallbackCanvasSvc with
            | Some s -> s
            | None ->
                let s = Canvas.forHost host
                fallbackCanvasSvc <- Some s
                s

        let pixelSvc =
            match fallbackPixelSvc with
            | Some s -> s
            | None ->
                let s = CanvasPixels.forHost host
                fallbackPixelSvc <- Some s
                s

        let canvas =
            match fallbackCanvas with
            | Some existing -> existing
            | None ->
                match box element with
                | :? HTMLCanvasElement as direct ->
                    ownsFallbackCanvas <- false
                    fallbackCanvas <- Some direct
                    direct
                | _ ->
                    let created = host.Document.createElement "canvas" :?> HTMLCanvasElement
                    created.setAttribute ("data-ranvier-gpu-surface", "fallback")

                    created.setAttribute (
                        "style",
                        "position:absolute;left:0;top:0;width:100%;height:100%;pointer-events:none;z-index:-1;-webkit-user-select:none;user-select:none"
                    )

                    created.setAttribute ("draggable", "false")

                    element.insertBefore (created, element.firstChild)
                    |> ignore

                    ownsFallbackCanvas <- true
                    fallbackCanvas <- Some created
                    created

        let ctx2d =
            match fallbackCtx with
            | Some existing -> existing
            | None ->
                canvas.width <- uint32 w
                canvas.height <- uint32 h

                match canvasSvc.GetContext (canvas, CanvasContextType.TwoD, Canvas2DSettings.Default) with
                | CanvasContextOutcome.Context created ->
                    fallbackCtx <- Some created
                    created
                | CanvasContextOutcome.Unsupported reason ->
                    raise (
                        NotSupportedException (
                            "Fallback 2D canvas context unsupported: "
                            + reason
                        )
                    )
                | CanvasContextOutcome.OtherContext -> invalidOp "Bound canvas element already owns an incompatible rendering context"

        let imageData =
            match fallbackImageData with
            | Some existing when curWidth = w && curHeight = h -> existing
            | _ ->
                if int canvas.width <> w then
                    canvas.width <- uint32 w

                if int canvas.height <> h then
                    canvas.height <- uint32 h

                let created = pixelSvc.Create (w, h)
                fallbackImageData <- Some created
                curWidth <- w
                curHeight <- h
                created

        struct (ctx2d, pixelSvc, imageData)

    /// <summary>The bound DOM element whose composited layer displays this surface.</summary>
    member _.Element = element

    /// <summary>The active presentation transport selected for this window and runtime.</summary>
    member _.Kind = kind

    /// <summary>True when frames composite directly from shared GPU memory without CPU canvas paints.</summary>
    member _.IsZeroCopyGpu = kind <> GpuSurfaceKind.CpuFallback

    /// <summary>The current backing pixel width, or zero before the first frame.</summary>
    member _.Width = curWidth

    /// <summary>The current backing pixel height, or zero before the first frame.</summary>
    member _.Height = curHeight

    /// <summary>The total number of frames published to this surface.</summary>
    member _.FramesPublished = framesPublished

    /// <summary>Whether this surface has been disposed.</summary>
    member _.IsDisposed = disposed

    /// <summary>The producer <c>ID3D11Device*</c> when <c>Kind</c> is <c>D3D11SharedTexture</c>, or zero.</summary>
    member _.D3D11Device = d3dDevice

    /// <summary>The immediate <c>ID3D11DeviceContext*</c> when <c>Kind</c> is <c>D3D11SharedTexture</c>, or zero.</summary>
    member _.D3D11Context = d3dContext

    /// <summary>The window's <c>MTLCommandQueue</c> handle when <c>Kind</c> is <c>MetalIOSurface</c>, or zero.</summary>
    member _.MetalCommandQueue = presenter.MetalQueue

    /// <summary>The configured process <c>EGLDisplay</c> when <c>Kind</c> is <c>EglImage</c>, or zero.</summary>
    member _.EglDisplay = EglRenderer.CurrentDisplay

    /// <summary>Restores the window's EGL consumer context on Linux when <c>Kind</c> is <c>EglImage</c>.</summary>
    member _.ActivateEglContext() =
        checkActive ()

        match presenter.Egl with
        | Some consumer -> consumer.Activate ()
        | None -> false

    /// <summary>
    /// Acquires the shared D3D11 target texture on the window's adapter, runs <c>render(device, context, texture)</c>,
    /// flushes and releases the keyed mutex, and notifies WebCore to composite the updated element.
    /// </summary>
    member _.WithD3D11Target(width: int, height: int, render: nativeint -> nativeint -> nativeint -> unit) =
        checkActive ()

        if kind <> GpuSurfaceKind.D3D11SharedTexture then
            raise (NotSupportedException "This window is not using D3D11 shared texture input")

        ensureD3D11Target width height
        let heldKey = acquireProducerMutex targetMutex
        let mutable succeeded = false

        try
            render d3dDevice d3dContext targetTexture
            flushCtx.Invoke d3dContext
            succeeded <- true
        finally
            releaseProducerMutex targetMutex (if succeeded then 1UL else heldKey)

        publishBoundD3D11 ()

    /// <summary>
    /// Allocates (or reuses) a non-mutex NT-shared <c>R8G8B8A8_UNORM</c> staging texture on the window's GPU adapter
    /// and returns its Win32 <c>HANDLE</c> for <c>ID3D12Device::OpenSharedHandle</c> or Vulkan external memory import.
    /// Call <c>CommitSharedStaging</c> once the external GPU queue has finished writing to the texture.
    /// </summary>
    member _.AcquireSharedStagingHandle(width: int, height: int) : nativeint =
        checkActive ()

        if kind <> GpuSurfaceKind.D3D11SharedTexture then
            raise (NotSupportedException "Shared NT staging handles require D3D11 shared texture input")

        let w = max 1 width
        let h = max 1 height
        ensureD3D11Target w h

        if
            stagingTexture = 0n
            || stagingNtHandle = 0n
            || stagingWidth <> w
            || stagingHeight <> h
        then
            releaseStaging ()

            let mutable desc: GpuSurfaceD3D11.TextureDesc =
                {
                    Width = uint32 w
                    Height = uint32 h
                    MipLevels = 1u
                    ArraySize = 1u
                    Format = GpuSurfaceD3D11.FormatRgba8
                    SampleCount = 1u
                    SampleQuality = 0u
                    Usage = 0
                    BindFlags = GpuSurfaceD3D11.BindShaderAndRenderTarget
                    CpuAccessFlags = 0u
                    MiscFlags =
                        GpuSurfaceD3D11.MiscShared
                        ||| GpuSurfaceD3D11.MiscSharedNtHandle
                }

            let mutable createdTex = 0n

            if
                createTex.Invoke (d3dDevice, &desc, 0n, &createdTex) < 0
                || createdTex = 0n
            then
                invalidOp "ID3D11Device::CreateTexture2D failed allocating NT-shared staging texture"

            let mutable res1Id = GpuSurfaceD3D11.dxgiResource1Id
            let mutable res1 = 0n

            try
                if
                    Marshal.QueryInterface (createdTex, &res1Id, &res1) < 0
                    || res1 = 0n
                then
                    GpuSurfaceD3D11.drop createdTex
                    invalidOp "QueryInterface(IDXGIResource1) failed on staging texture"

                let createHandle =
                    GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.CreateSharedHandle> res1 13

                let mutable ntHandle = 0n

                if
                    createHandle.Invoke (res1, 0n, GpuSurfaceD3D11.GenericAll, 0n, &ntHandle) < 0
                    || ntHandle = 0n
                then
                    GpuSurfaceD3D11.drop createdTex
                    invalidOp "IDXGIResource1::CreateSharedHandle failed"

                stagingTexture <- createdTex
                stagingNtHandle <- ntHandle
                stagingWidth <- w
                stagingHeight <- h
            finally
                GpuSurfaceD3D11.drop res1

        stagingNtHandle

    /// <summary>
    /// Copies the NT-shared staging texture into the keyed-mutex WebCore target texture purely in VRAM and schedules
    /// composition on the host.
    /// </summary>
    member _.CommitSharedStaging() =
        checkActive ()

        if
            kind <> GpuSurfaceKind.D3D11SharedTexture
            || stagingTexture = 0n
        then
            invalidOp "No active D3D11 shared staging texture to commit"

        let heldKey = acquireProducerMutex targetMutex
        let mutable succeeded = false

        try
            copyRes.Invoke (d3dContext, targetTexture, stagingTexture)
            flushCtx.Invoke d3dContext
            succeeded <- true
        finally
            releaseProducerMutex targetMutex (if succeeded then 1UL else heldKey)

        publishBoundD3D11 ()

    /// <summary>Publishes a completed macOS <c>IOSurfaceRef</c> (nonplanar premultiplied BGRA8 sRGB) to the element.</summary>
    member _.PublishIOSurface(ioSurface: nativeint, width: int, height: int, ?flipY: bool) =
        checkActive ()

        if kind <> GpuSurfaceKind.MetalIOSurface then
            raise (NotSupportedException "This window is not using macOS IOSurface input")

        if ioSurface = 0n then
            invalidArg "ioSurface" "Use Dispose to unbind a GPU surface"

        let flip = defaultArg flipY false
        curWidth <- max 1 width
        curHeight <- max 1 height
        let target = ensureNativeTarget ()

        if
            boundIOSurface <> ioSurface
            || boundIOSurfaceFlipY <> flip
        then
            host.BindIOSurface (target, ioSurface, flip)
            boundIOSurface <- ioSurface
            boundIOSurfaceFlipY <- flip
        else
            host.NativeTextureChanged target

        framesPublished <- framesPublished + 1L

    /// <summary>Publishes a completed Linux <c>EGLImage</c> (premultiplied RGBA8 sRGB) to the element.</summary>
    member _.PublishEglImage(eglImage: nativeint, width: int, height: int, ?flipY: bool) =
        checkActive ()

        if kind <> GpuSurfaceKind.EglImage then
            raise (NotSupportedException "This window is not using Linux EGLImage input")

        if eglImage = 0n then
            invalidArg "eglImage" "Use Dispose to unbind a GPU surface"

        let flip = defaultArg flipY false
        curWidth <- max 1 width
        curHeight <- max 1 height
        let target = ensureNativeTarget ()

        if boundEglImage <> eglImage || boundEglFlipY <> flip then
            host.BindEglImage (target, eglImage, flip)
            boundEglImage <- eglImage
            boundEglFlipY <- flip
        else
            host.NativeTextureChanged target

        framesPublished <- framesPublished + 1L

    /// <summary>
    /// Presents tightly packed top-down RGBA8 pixels (<c>width * height * 4</c> bytes) into the surface, uploading into
    /// the shared D3D11 texture when active or drawing into the fallback DOM <c>&lt;canvas&gt;</c> otherwise.
    /// </summary>
    member this.PresentPixels(rgba: ReadOnlySpan<byte>, width: int, height: int) =
        checkActive ()

        if width <= 0 then
            invalidArg "width" "Width must be positive"

        if height <= 0 then
            invalidArg "height" "Height must be positive"

        if rgba.Length < width * height * 4 then
            invalidArg "rgba" "Pixel span is smaller than width * height * 4 bytes"

        if kind = GpuSurfaceKind.D3D11SharedTexture then
            use pinned = fixed &MemoryMarshal.GetReference rgba
            let ptr = NativeInterop.NativePtr.toNativeInt pinned

            this.WithD3D11Target (width, height, fun _ ctx tex -> updateRes.Invoke (ctx, tex, 0u, 0n, ptr, uint32 (width * 4), 0u))
        else
            let struct (ctx2d, pixelSvc, imageData) = ensureFallbackCanvas width height
            let byteCount = width * height * 4

            if fallbackScratch.Length <> byteCount then
                fallbackScratch <- Array.zeroCreate<byte> byteCount

            rgba.Slice(0, byteCount).CopyTo(Span<byte> fallbackScratch)
            pixelSvc.Write (imageData, 0, fallbackScratch)
            ctx2d.putImageData (imageData, 0, 0)
            framesPublished <- framesPublished + 1L

    /// <summary>Presents tightly packed top-down RGBA8 pixels (<c>width * height * 4</c> bytes) into the surface.</summary>
    member this.PresentPixels(rgba: byte array, width: int, height: int) =
        if isNull rgba then
            nullArg "rgba"

        if
            kind <> GpuSurfaceKind.D3D11SharedTexture
            && width > 0
            && height > 0
            && rgba.Length = width * height * 4
        then
            checkActive ()
            let struct (ctx2d, pixelSvc, imageData) = ensureFallbackCanvas width height
            pixelSvc.Write (imageData, 0, rgba)
            ctx2d.putImageData (imageData, 0, 0)
            framesPublished <- framesPublished + 1L
        else
            this.PresentPixels (ReadOnlySpan<byte> rgba, width, height)

    member internal _.PrepareForComposition() =
        if
            not disposed
            && boundD3D11Texture <> 0n
            && targetMutex <> 0n
        then
            let acquire = GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.AcquireSync> targetMutex 8

            if acquire.Invoke (targetMutex, 0UL, 0u) = 0 then
                releaseProducerMutex targetMutex 1UL

    member internal this.DisposeCore(fromWindowTeardown: bool) =
        if not disposed then
            if
                not fromWindowTeardown
                && not host.Session.IsDisposed
                && not host.Session.IsOwnerThread
            then
                invalidOp "A GPU surface must be disposed on the window's owner thread"

            disposed <- true

            if not fromWindowTeardown then
                onDisposed this

            if not host.Session.IsDisposed then
                for remove in listenerRemovers do
                    try
                        remove ()
                    with _ ->
                        ()

            listenerRemovers.Clear ()

            fallbackPixelSvc
            |> Option.iter (fun svc ->
                try
                    svc.Dispose ()
                with _ ->
                    ())

            fallbackCanvasSvc
            |> Option.iter (fun svc ->
                try
                    svc.Dispose ()
                with _ ->
                    ())

            fallbackPixelSvc <- None
            fallbackCanvasSvc <- None
            fallbackCtx <- None
            fallbackImageData <- None
            fallbackScratch <- Array.empty

            match fallbackCanvas with
            | Some canvas when not host.Session.IsDisposed ->
                try
                    if ownsFallbackCanvas then
                        (canvas :> ChildNode).remove()
                    else
                        canvas.width <- 0u
                        canvas.height <- 0u
                with _ ->
                    ()
            | _ -> ()

            fallbackCanvas <- None

            let target = defaultArg nativeLayer element

            if
                boundIOSurface <> 0n
                && not host.Session.IsDisposed
            then
                try
                    host.BindIOSurface (target, 0n, false)
                with _ ->
                    ()

            boundIOSurface <- 0n

            if boundEglImage <> 0n && not host.Session.IsDisposed then
                try
                    host.BindEglImage (target, 0n, false)
                with _ ->
                    ()

            boundEglImage <- 0n

            releaseStaging ()
            releaseD3D11Target true

            match nativeLayer with
            | Some layer when ownsNativeLayer && not host.Session.IsDisposed ->
                try
                    (layer :> ChildNode).remove()
                with _ ->
                    ()
            | _ -> ()

            nativeLayer <- None
            ownsNativeLayer <- false
            GpuSurfaceD3D11.drop d3dContext
            d3dContext <- 0n
            GpuSurfaceD3D11.drop d3dDevice
            d3dDevice <- 0n

    interface IDisposable with
        member this.Dispose() =
            this.DisposeCore false
