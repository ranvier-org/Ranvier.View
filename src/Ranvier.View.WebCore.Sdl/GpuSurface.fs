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

    let dxgiKeyedMutexId = Guid "9d8e1289-d7b3-465f-8126-250e349af85d"
    let dxgiResource1Id = Guid "30961379-4609-4a41-998e-54fe567ee0c1"
    let d3d11Device5Id = Guid "8ffde202-a0e7-45df-9e01-e837801b5ea0"
    let d3d11Context4Id = Guid "917600da-f58c-4c33-98d8-3e15b390fa24"
    let d3d11FenceId = Guid "affde9d1-1df7-4bb7-8a34-0f46251dab80"

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

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type CreateTexture = delegate of nativeint * byref<TextureDesc> * nativeint * byref<nativeint> -> int

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type CopyResource = delegate of nativeint * nativeint * nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type UpdateResource = delegate of nativeint * nativeint * uint32 * nativeint * nativeint * uint32 * uint32 -> unit

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type ContextCall = delegate of nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type AcquireSync = delegate of nativeint * uint64 * uint32 -> int

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type ReleaseSync = delegate of nativeint * uint64 -> int

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type CreateSharedHandle = delegate of nativeint * nativeint * uint32 * nativeint * byref<nativeint> -> int

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type OpenSharedFence = delegate of nativeint * nativeint * byref<Guid> * byref<nativeint> -> int

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type ContextWaitFence = delegate of nativeint * nativeint * uint64 -> int

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

/// <summary>The transport backing an element's composited GPU surface.</summary>
[<Struct; RequireQualifiedAccess>]
type GpuSurfaceKind =
    /// <summary>A Windows D3D11 NT-shared keyed-mutex texture composited directly by WebCore.</summary>
    | D3D11SharedTexture
    /// <summary>A macOS <c>IOSurfaceRef</c> composited directly by WebCore and Metal.</summary>
    | MetalIOSurface
    /// <summary>A Linux <c>EGLImage</c> composited directly by WebCore on the process EGL display.</summary>
    | EglImage
    /// <summary>Tightly packed RGBA8 pixels drawn into a DOM <c>canvas</c> element.</summary>
    | CpuFallback

    /// <summary>Formats the transport name as a static identifier.</summary>
    override this.ToString() =
        match this with
        | D3D11SharedTexture -> "D3D11SharedTexture"
        | MetalIOSurface -> "MetalIOSurface"
        | EglImage -> "EglImage"
        | CpuFallback -> "CpuFallback"

/// <summary>
/// Windows D3D11 shared-texture presentation capabilities bound to a <see cref="T:Ranvier.View.WebCore.Sdl.GpuSurface" />.
/// </summary>
[<Sealed>]
type D3D11GpuSurface
    internal
    (
        device: nativeint,
        context: nativeint,
        adapterVendorDevice: struct (uint32 * uint32) voption,
        adapterLuid: int64 voption,
        supportsSharedFence: bool,
        withTarget: int -> int -> (nativeint -> nativeint -> nativeint -> unit) -> unit,
        acquireStaging: int -> int -> nativeint,
        openFence: nativeint -> nativeint,
        commitStaging: nativeint -> uint64 -> unit
    ) =
    /// <summary>The producer <c>ID3D11Device*</c> handle created on the window's active DXGI adapter.</summary>
    member _.Device = device

    /// <summary>The producer immediate <c>ID3D11DeviceContext*</c> handle on the window's active DXGI adapter.</summary>
    member _.Context = context

    /// <summary>The PCI vendor and device IDs of the window's DXGI adapter when reported by DXGI.</summary>
    member _.AdapterVendorDevice = adapterVendorDevice

    /// <summary>The 64-bit locally unique identifier (LUID) of the window's DXGI adapter when reported by DXGI.</summary>
    member _.AdapterLuid = adapterLuid

    /// <summary>True when <c>ID3D11Device5</c> and <c>ID3D11DeviceContext4</c> shared GPU fences are supported.</summary>
    member _.SupportsSharedFence = supportsSharedFence

    /// <summary>
    /// Acquires the keyed-mutex D3D11 target texture at <c>width</c> by <c>height</c>, invokes
    /// <c>render device context texture</c>, flushes and releases the mutex, and schedules WebCore composition.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread or D3D11 allocation failed.</exception>
    member _.WithTarget(width: int, height: int, render: nativeint -> nativeint -> nativeint -> unit) =
        withTarget width height render

    /// <summary>
    /// Allocates or reuses an NT-shared <c>R8G8B8A8_UNORM</c> staging texture on the window's adapter and returns its
    /// Win32 <c>HANDLE</c> for <c>ID3D12Device::OpenSharedHandle</c> or Vulkan external memory import.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread or handle export failed.</exception>
    member _.AcquireSharedStagingHandle(width: int, height: int) : nativeint =
        acquireStaging width height

    /// <summary>
    /// Opens a shared NT fence handle (<c>D3D12_FENCE_FLAG_SHARED</c>) as an <c>ID3D11Fence*</c> on the producer device,
    /// or returns zero when shared fences are unavailable. Release the returned pointer with
    /// <see cref="M:Ranvier.View.WebCore.Sdl.D3D11GpuSurface.ReleaseFence(System.IntPtr)" />.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member _.OpenSharedFence(fenceNtHandle: nativeint) : nativeint =
        openFence fenceNtHandle

    /// <summary>
    /// Releases an <c>ID3D11Fence*</c> previously returned by
    /// <see cref="M:Ranvier.View.WebCore.Sdl.D3D11GpuSurface.OpenSharedFence(System.IntPtr)" />.
    /// </summary>
    member _.ReleaseFence(d3d11Fence: nativeint) =
        GpuSurfaceD3D11.drop d3d11Fence

    /// <summary>
    /// Copies the NT-shared staging texture into the keyed-mutex WebCore target texture on the GPU and schedules composition.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread or before acquiring a staging handle.</exception>
    member _.CommitSharedStaging() =
        commitStaging 0n 0UL

    /// <summary>
    /// Queues a GPU-side wait on <c>d3d11Fence</c> for <c>fenceValue</c> before copying the NT-shared staging texture into
    /// the keyed-mutex WebCore target texture and scheduling composition.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><c>d3d11Fence</c> is zero.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread or fence wait failed.</exception>
    member _.CommitSharedStagingAfterFence(d3d11Fence: nativeint, fenceValue: uint64) =
        if d3d11Fence = 0n then
            invalidArg (nameof d3d11Fence) "Shared D3D11 fence handle must be non-zero"

        commitStaging d3d11Fence fenceValue

    /// <summary>Formats the capability identifier.</summary>
    override _.ToString() =
        "D3D11GpuSurface"

/// <summary>
/// macOS Metal <c>IOSurfaceRef</c> presentation capabilities bound to a <see cref="T:Ranvier.View.WebCore.Sdl.GpuSurface" />.
/// </summary>
[<Sealed>]
type MetalGpuSurface internal (commandQueue: nativeint, publish: nativeint -> int -> int -> bool -> unit) =
    /// <summary>The window's <c>MTLCommandQueue</c> handle when Metal presentation is active, or zero.</summary>
    member _.CommandQueue = commandQueue

    /// <summary>
    /// Publishes a completed macOS <c>IOSurfaceRef</c> holding nonplanar premultiplied BGRA8 sRGB pixels to the bound element.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><c>ioSurface</c> is zero.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member _.PublishIOSurface(ioSurface: nativeint, width: int, height: int, ?flipY: bool) =
        publish ioSurface width height (defaultArg flipY false)

    /// <summary>Formats the capability identifier.</summary>
    override _.ToString() =
        "MetalGpuSurface"

/// <summary>
/// Linux <c>EGLImage</c> presentation capabilities bound to a <see cref="T:Ranvier.View.WebCore.Sdl.GpuSurface" />.
/// </summary>
[<Sealed>]
type EglGpuSurface internal (display: nativeint, activate: unit -> bool, publish: nativeint -> int -> int -> bool -> unit) =
    /// <summary>The configured process <c>EGLDisplay</c> handle.</summary>
    member _.Display = display

    /// <summary>Restores the window's EGL consumer context on the current thread.</summary>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member _.ActivateContext() =
        activate ()

    /// <summary>
    /// Publishes a completed Linux <c>EGLImage</c> holding premultiplied RGBA8 sRGB pixels to the bound element.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><c>eglImage</c> is zero.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member _.PublishEglImage(eglImage: nativeint, width: int, height: int, ?flipY: bool) =
        publish eglImage width height (defaultArg flipY false)

    /// <summary>Formats the capability identifier.</summary>
    override _.ToString() =
        "EglGpuSurface"

/// <summary>The platform-specific GPU presentation capability bound to a <see cref="T:Ranvier.View.WebCore.Sdl.GpuSurface" />.</summary>
[<RequireQualifiedAccess>]
type GpuSurfaceBackend =
    /// <summary>Windows D3D11 NT-shared texture presentation on the window's adapter.</summary>
    | D3D11 of D3D11GpuSurface
    /// <summary>macOS <c>IOSurfaceRef</c> presentation on Metal.</summary>
    | Metal of MetalGpuSurface
    /// <summary>Linux <c>EGLImage</c> presentation on the process EGL display.</summary>
    | Egl of EglGpuSurface
    /// <summary>DOM <c>canvas</c> pixel presentation when direct GPU element import is unavailable.</summary>
    | CpuCanvas

    /// <summary>Formats the active backend variant name.</summary>
    override this.ToString() =
        match this with
        | D3D11 _ -> "D3D11"
        | Metal _ -> "Metal"
        | Egl _ -> "Egl"
        | CpuCanvas -> "CpuCanvas"

/// <summary>
/// Binds an external GPU renderer or pixel producer to a composited DOM element in an SDL WebCore window so HTML, CSS,
/// and SVG layers composite naturally over it while suppressing accidental drag-selection highlighting by default.
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
    let mutable documentReplaced = false
    let mutable curWidth = 0
    let mutable curHeight = 0
    let mutable framesPublished = 0L

    // Windows D3D11 shared texture state (borrows shared producer device/context from Presenter)
    let mutable d3dDevice = 0n
    let mutable d3dContext = 0n
    let mutable d3dDevice5 = 0n
    let mutable d3dContext4 = 0n
    let mutable createTex: GpuSurfaceD3D11.CreateTexture = null
    let mutable copyRes: GpuSurfaceD3D11.CopyResource = null
    let mutable updateRes: GpuSurfaceD3D11.UpdateResource = null
    let mutable flushCtx: GpuSurfaceD3D11.ContextCall = null
    let mutable openFenceCall: GpuSurfaceD3D11.OpenSharedFence = null
    let mutable waitFenceCall: GpuSurfaceD3D11.ContextWaitFence = null
    let mutable targetTexture = 0n
    let mutable targetMutex = 0n
    let mutable targetAcquireSync: GpuSurfaceD3D11.AcquireSync = null
    let mutable targetReleaseSync: GpuSurfaceD3D11.ReleaseSync = null
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
    let mutable priorCanvasSize: struct (uint32 * uint32) voption = ValueNone

    let kind =
        if
            OperatingSystem.IsWindows ()
            && presenter.GpuCompositingEnabled
            && WebCoreHost.Has "texture-input-d3d11"
            && presenter.D3D11Device <> 0n
        then
            match presenter.AcquireSharedProducerD3D11 () with
            | ValueSome (struct (dev, ctx)) ->
                d3dDevice <- dev
                d3dContext <- ctx
                createTex <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.CreateTexture> dev 5
                copyRes <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.CopyResource> ctx 47
                updateRes <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.UpdateResource> ctx 48
                flushCtx <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.ContextCall> ctx 111

                let mutable dev5Id = GpuSurfaceD3D11.d3d11Device5Id
                let mutable ctx4Id = GpuSurfaceD3D11.d3d11Context4Id
                let mutable queriedDev5 = 0n
                let mutable queriedCtx4 = 0n

                if
                    Marshal.QueryInterface (dev, &dev5Id, &queriedDev5)
                    >= 0
                    && queriedDev5 <> 0n
                    && Marshal.QueryInterface (ctx, &ctx4Id, &queriedCtx4)
                       >= 0
                    && queriedCtx4 <> 0n
                then
                    d3dDevice5 <- queriedDev5
                    d3dContext4 <- queriedCtx4
                    openFenceCall <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.OpenSharedFence> queriedDev5 67
                    waitFenceCall <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.ContextWaitFence> queriedCtx4 148
                else
                    GpuSurfaceD3D11.drop queriedCtx4
                    GpuSurfaceD3D11.drop queriedDev5

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

    // Save prior attributes for symmetric unmount restoration and apply composited layer + selection suppression.
    let listenerRemovers = ResizeArray<unit -> unit>()
    let rawPriorStyle = element.getAttribute "style"
    let hadPriorStyle = not (isNull rawPriorStyle)
    let priorStyle = if hadPriorStyle then rawPriorStyle else ""
    let mutable appliedAdditions: string array = Array.empty
    let mutable appliedStyle: string option = None
    let mutable setDraggableFalse = false

    let isInteractiveDescendant (evTarget: EventTarget) =
        let mutable node =
            match box evTarget with
            | :? Element as el -> el
            | _ -> null

        let mutable interactive = false

        while not (isNull (box node))
              && not (obj.ReferenceEquals (node, element))
              && not interactive do
            let tag =
                match node.tagName with
                | null -> ""
                | s -> s.ToUpperInvariant ()

            match tag with
            | "BUTTON"
            | "INPUT"
            | "SELECT"
            | "TEXTAREA"
            | "A"
            | "LABEL"
            | "SUMMARY"
            | "OPTION" -> interactive <- true
            | _ ->
                let editable = node.getAttribute "contenteditable"

                if
                    not (isNull editable)
                    && not (String.Equals (editable, "false", StringComparison.OrdinalIgnoreCase))
                then
                    interactive <- true

            node <- node.parentElement

        interactive

    let documentSub =
        host.DocumentReplaced.Subscribe (fun _ ->
            documentReplaced <- true
            listenerRemovers.Clear ()
            nativeLayer <- None
            ownsNativeLayer <- false
            boundD3D11Texture <- 0n
            boundIOSurface <- 0n
            boundEglImage <- 0n
            fallbackCanvas <- None
            fallbackCtx <- None
            fallbackImageData <- None
            ownsFallbackCanvas <- false
            priorCanvasSize <- ValueNone)

    do
        let trimmedStyle = priorStyle.Trim ()
        let lowerStyle = trimmedStyle.ToLowerInvariant ()
        let additions = ResizeArray<string>()

        if
            kind <> GpuSurfaceKind.CpuFallback
            && not (
                lowerStyle.Contains "will-change:transform"
                || lowerStyle.Contains "will-change: transform"
            )
        then
            additions.Add "will-change:transform"

        if not (box element :? HTMLCanvasElement) then
            if
                not (lowerStyle.Contains "position:")
                && not (lowerStyle.Contains "position :")
            then
                additions.Add "position:relative"

            if
                not (lowerStyle.Contains "isolation:")
                && not (lowerStyle.Contains "isolation :")
            then
                additions.Add "isolation:isolate"

        if preventSelection then
            if not (lowerStyle.Contains "-webkit-user-select") then
                additions.Add "-webkit-user-select:none"

            if
                not (
                    lowerStyle.Contains ";user-select"
                    || lowerStyle.Contains "; user-select"
                    || lowerStyle.StartsWith "user-select"
                )
            then
                additions.Add "user-select:none"

            if not (lowerStyle.Contains "-webkit-user-drag") then
                additions.Add "-webkit-user-drag:none"

            if not (lowerStyle.Contains "touch-action") then
                additions.Add "touch-action:none"

        if additions.Count > 0 then
            appliedAdditions <- additions.ToArray ()
            let extra = String.concat ";" appliedAdditions

            let merged =
                if String.IsNullOrEmpty trimmedStyle then extra
                elif trimmedStyle.EndsWith ";" then trimmedStyle + extra
                else trimmedStyle + ";" + extra

            element.setAttribute ("style", merged)
            appliedStyle <- Some merged

        if preventSelection then
            if String.IsNullOrEmpty (element.getAttribute "draggable") then
                element.setAttribute ("draggable", "false")
                setDraggableFalse <- true

            let registerPrevent (eventName: string) (listener: EventListener) =
                element.addEventListener (eventName, listener)
                listenerRemovers.Add (fun () -> element.removeEventListener (eventName, listener))

            registerPrevent "selectstart" (fun ev ->
                if not (isInteractiveDescendant ev.target) then
                    ev.preventDefault ())

            registerPrevent "dragstart" (fun ev ->
                if not (isInteractiveDescendant ev.target) then
                    ev.preventDefault ())

            registerPrevent "mousedown" (fun ev ->
                if not (isInteractiveDescendant ev.target) then
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
            && not documentReplaced
            && boundD3D11Texture <> 0n
            && not host.Session.IsDisposed
        then
            let target = defaultArg nativeLayer element

            try
                host.BindD3D11Texture (target, 0n)
            with _ ->
                ()

        boundD3D11Texture <- 0n
        targetAcquireSync <- null
        targetReleaseSync <- null
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
            targetAcquireSync <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.AcquireSync> createdMutex 8
            targetReleaseSync <- GpuSurfaceD3D11.comMethod<GpuSurfaceD3D11.ReleaseSync> createdMutex 9
            curWidth <- w
            curHeight <- h

    let acquireProducerMutex () =
        // Fast path: key 0 is available (newly created texture, or returned by WebCore composition).
        let first = targetAcquireSync.Invoke (targetMutex, 0UL, 0u)

        if first = 0 then
            0UL
        elif first = GpuSurfaceD3D11.WaitTimeout then
            // If the producer published a previous frame before WebCore's compositor ran, the mutex sits idle at key 1.
            if targetAcquireSync.Invoke (targetMutex, 1UL, 0u) = 0 then
                1UL
            elif targetAcquireSync.Invoke (targetMutex, 0UL, 5000u) = 0 then
                0UL
            else
                invalidOp "IDXGIKeyedMutex::AcquireSync timed out waiting for WebCore composition"
        else
            invalidOp (
                "IDXGIKeyedMutex::AcquireSync failed: 0x"
                + first.ToString "X8"
            )

    let releaseProducerMutex (key: uint64) =
        let hr = targetReleaseSync.Invoke (targetMutex, key)

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

    let withD3D11TargetImpl (width: int) (height: int) (render: nativeint -> nativeint -> nativeint -> unit) =
        checkActive ()
        ensureD3D11Target width height
        let heldKey = acquireProducerMutex ()
        let mutable succeeded = false

        try
            render d3dDevice d3dContext targetTexture
            flushCtx.Invoke d3dContext
            succeeded <- true
        finally
            releaseProducerMutex (if succeeded then 1UL else heldKey)

        publishBoundD3D11 ()

    let acquireSharedStagingImpl (width: int) (height: int) : nativeint =
        checkActive ()
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

    let openSharedFenceImpl (fenceNtHandle: nativeint) : nativeint =
        checkActive ()

        if
            fenceNtHandle = 0n
            || d3dDevice5 = 0n
            || isNull (box openFenceCall)
        then
            0n
        else
            let mutable iid = GpuSurfaceD3D11.d3d11FenceId
            let mutable d3d11Fence = 0n

            if
                openFenceCall.Invoke (d3dDevice5, fenceNtHandle, &iid, &d3d11Fence)
                >= 0
            then
                d3d11Fence
            else
                0n

    let commitSharedStagingImpl (d3d11Fence: nativeint) (fenceValue: uint64) =
        checkActive ()

        if stagingTexture = 0n then
            invalidOp "No active D3D11 shared staging texture to commit"

        if d3d11Fence <> 0n then
            if d3dContext4 = 0n || isNull (box waitFenceCall) then
                invalidOp "ID3D11DeviceContext4 shared fence wait is unavailable on this device"

            let waitHr = waitFenceCall.Invoke (d3dContext4, d3d11Fence, fenceValue)

            if waitHr < 0 then
                invalidOp (
                    "ID3D11DeviceContext4::Wait failed: 0x"
                    + waitHr.ToString "X8"
                )

        let heldKey = acquireProducerMutex ()
        let mutable succeeded = false

        try
            copyRes.Invoke (d3dContext, targetTexture, stagingTexture)
            flushCtx.Invoke d3dContext
            succeeded <- true
        finally
            releaseProducerMutex (if succeeded then 1UL else heldKey)

        publishBoundD3D11 ()

    let publishIOSurfaceImpl (ioSurface: nativeint) (width: int) (height: int) (flip: bool) =
        checkActive ()

        if ioSurface = 0n then
            invalidArg "ioSurface" "Use Dispose to unbind a GPU surface"

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

    let activateEglImpl () =
        checkActive ()

        match presenter.Egl with
        | Some consumer -> consumer.Activate ()
        | None -> false

    let publishEglImageImpl (eglImage: nativeint) (width: int) (height: int) (flip: bool) =
        checkActive ()

        if eglImage = 0n then
            invalidArg "eglImage" "Use Dispose to unbind a GPU surface"

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

    let backend =
        match kind with
        | GpuSurfaceKind.D3D11SharedTexture ->
            GpuSurfaceBackend.D3D11 (
                new D3D11GpuSurface (
                    d3dDevice,
                    d3dContext,
                    presenter.GpuAdapterVendorDevice,
                    presenter.GpuAdapterLuid,
                    (d3dDevice5 <> 0n && d3dContext4 <> 0n),
                    withD3D11TargetImpl,
                    acquireSharedStagingImpl,
                    openSharedFenceImpl,
                    commitSharedStagingImpl
                )
            )
        | GpuSurfaceKind.MetalIOSurface -> GpuSurfaceBackend.Metal (new MetalGpuSurface (presenter.MetalQueue, publishIOSurfaceImpl))
        | GpuSurfaceKind.EglImage -> GpuSurfaceBackend.Egl (new EglGpuSurface (EglRenderer.CurrentDisplay, activateEglImpl, publishEglImageImpl))
        | GpuSurfaceKind.CpuFallback -> GpuSurfaceBackend.CpuCanvas

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
                    priorCanvasSize <- ValueSome (struct (direct.width, direct.height))
                    fallbackCanvas <- Some direct
                    direct
                | _ ->
                    let created =
                        match box (host.Document.createElement "canvas") with
                        | :? HTMLCanvasElement as htmlCanvas -> htmlCanvas
                        | _ -> host.Document.createElementNS ("http://www.w3.org/1999/xhtml", "canvas") :?> HTMLCanvasElement

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

    let restoreElementAttributes () =
        match appliedStyle with
        | Some merged ->
            let current = element.getAttribute "style"

            if current = merged then
                if hadPriorStyle then
                    element.setAttribute ("style", priorStyle)
                else
                    element.removeAttribute "style"
            elif
                not (isNull current)
                && appliedAdditions.Length > 0
            then
                let remaining =
                    current.Split (';', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.filter (fun part ->
                        let trimmed = part.Trim ()
                        not (Array.exists (fun added -> String.Equals (trimmed, added, StringComparison.OrdinalIgnoreCase)) appliedAdditions))

                if remaining.Length = 0 && not hadPriorStyle then
                    element.removeAttribute "style"
                else
                    element.setAttribute ("style", String.concat ";" remaining)
        | None -> ()

        if
            setDraggableFalse
            && element.getAttribute "draggable" = "false"
        then
            element.removeAttribute "draggable"

    /// <summary>The bound DOM element whose composited layer displays this surface.</summary>
    member _.Element = element

    /// <summary>The platform-specific GPU presentation capability active on this surface.</summary>
    member _.Backend = backend

    /// <summary>The active presentation transport selected for this window and runtime.</summary>
    member _.Kind = kind

    /// <summary>True when frames composite directly through a shared GPU surface transport.</summary>
    member _.IsHardwareComposited = kind <> GpuSurfaceKind.CpuFallback

    /// <summary>The current backing pixel width, or zero before the first frame.</summary>
    member _.Width = curWidth

    /// <summary>The current backing pixel height, or zero before the first frame.</summary>
    member _.Height = curHeight

    /// <summary>The total number of frames published to this surface.</summary>
    member _.FramesPublished = framesPublished

    /// <summary>Whether this surface has been disposed.</summary>
    member _.IsDisposed = disposed

    /// <summary>
    /// Presents tightly packed top-down RGBA8 pixels (<c>width * height * 4</c> bytes) into the surface, uploading into
    /// the shared D3D11 texture when active or drawing into the fallback DOM <c>canvas</c> element otherwise.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">Dimensions are non-positive or <c>rgba</c> is smaller than <c>width * height * 4</c>.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member _.PresentPixels(rgba: ReadOnlySpan<byte>, width: int, height: int) =
        checkActive ()

        if width <= 0 then
            invalidArg (nameof width) "Width must be positive"

        if height <= 0 then
            invalidArg (nameof height) "Height must be positive"

        if rgba.Length < width * height * 4 then
            invalidArg (nameof rgba) "Pixel span is smaller than width * height * 4 bytes"

        if kind = GpuSurfaceKind.D3D11SharedTexture then
            use pinned = fixed &MemoryMarshal.GetReference rgba
            let ptr = NativeInterop.NativePtr.toNativeInt pinned

            withD3D11TargetImpl width height (fun _ ctx tex -> updateRes.Invoke (ctx, tex, 0u, 0n, ptr, uint32 (width * 4), 0u))
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
    /// <exception cref="T:System.ArgumentNullException"><c>rgba</c> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Dimensions are non-positive or <c>rgba</c> is smaller than <c>width * height * 4</c>.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The surface or window has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread.</exception>
    member this.PresentPixels(rgba: byte array, width: int, height: int) =
        if isNull rgba then
            nullArg (nameof rgba)

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

    /// <summary>Prepares a retained D3D11 keyed-mutex surface for WebCore re-composition on DOM-only frames.</summary>
    member internal _.PrepareForComposition() =
        if
            not disposed
            && boundD3D11Texture <> 0n
            && targetMutex <> 0n
            && not (isNull (box targetAcquireSync))
        then
            if targetAcquireSync.Invoke (targetMutex, 0UL, 0u) = 0 then
                releaseProducerMutex 1UL

    /// <summary>Unbinds native GPU resources, removes owned child elements, and restores element attributes.</summary>
    member internal this.DisposeCore(fromWindowTeardown: bool) =
        if not disposed then
            if
                not fromWindowTeardown
                && not host.Session.IsDisposed
                && not host.Session.IsOwnerThread
            then
                invalidOp "A GPU surface must be disposed on the window's owner thread"

            disposed <- true
            documentSub.Dispose ()

            if not fromWindowTeardown then
                onDisposed this

            if
                not documentReplaced
                && not host.Session.IsDisposed
            then
                for remove in listenerRemovers do
                    try
                        remove ()
                    with _ ->
                        ()

            listenerRemovers.Clear ()

            match fallbackCanvas, fallbackCtx with
            | Some canvas, _ when
                not documentReplaced
                && not host.Session.IsDisposed
                ->
                try
                    if ownsFallbackCanvas then
                        (canvas :> ChildNode).remove()
                    else
                        fallbackCtx
                        |> Option.iter (fun ctx -> ctx.clearRect (0.0, 0.0, float canvas.width, float canvas.height))

                        match priorCanvasSize with
                        | ValueSome (struct (origW, origH)) ->
                            canvas.width <- origW
                            canvas.height <- origH
                        | ValueNone -> ()
                with _ ->
                    ()
            | _ -> ()

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
            fallbackCanvas <- None
            priorCanvasSize <- ValueNone

            let target = defaultArg nativeLayer element

            if
                not documentReplaced
                && boundIOSurface <> 0n
                && not host.Session.IsDisposed
            then
                try
                    host.BindIOSurface (target, 0n, false)
                with _ ->
                    ()

            boundIOSurface <- 0n

            if
                not documentReplaced
                && boundEglImage <> 0n
                && not host.Session.IsDisposed
            then
                try
                    host.BindEglImage (target, 0n, false)
                with _ ->
                    ()

            boundEglImage <- 0n

            releaseStaging ()
            releaseD3D11Target true

            match nativeLayer with
            | Some layer when
                ownsNativeLayer
                && not documentReplaced
                && not host.Session.IsDisposed
                ->
                try
                    (layer :> ChildNode).remove()
                with _ ->
                    ()
            | _ -> ()

            nativeLayer <- None
            ownsNativeLayer <- false

            if
                not fromWindowTeardown
                && not documentReplaced
                && not host.Session.IsDisposed
            then
                try
                    restoreElementAttributes ()
                with _ ->
                    ()

            openFenceCall <- null
            waitFenceCall <- null
            GpuSurfaceD3D11.drop d3dContext4
            d3dContext4 <- 0n
            GpuSurfaceD3D11.drop d3dDevice5
            d3dDevice5 <- 0n
            // d3dDevice and d3dContext belong to Presenter's shared producer slot and are released with Presenter.
            d3dContext <- 0n
            d3dDevice <- 0n

    /// <summary>Unbinds this GPU surface, removes owned backing elements, and restores mutated attributes on the element.</summary>
    /// <exception cref="T:System.InvalidOperationException">Called off the window owner thread while the window is live.</exception>
    member this.Dispose() =
        this.DisposeCore false

    /// <summary>Formats the surface status and active transport.</summary>
    override _.ToString() =
        if disposed then
            "GpuSurface(Disposed)"
        else
            "GpuSurface(" + kind.ToString () + ")"

    interface IDisposable with
        member this.Dispose() =
            this.DisposeCore false
