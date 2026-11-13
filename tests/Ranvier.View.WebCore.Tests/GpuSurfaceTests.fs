module Ranvier.View.WebCore.Tests.GpuSurfaceTests

open System
open System.Runtime.InteropServices
open System.Threading
open Expecto
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private OpenSharedResource1Delegate = delegate of nativeint * nativeint * byref<Guid> * byref<nativeint> -> int

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private UpdateSubresourceDelegate = delegate of nativeint * nativeint * uint32 * nativeint * nativeint * uint32 * uint32 -> unit

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private FlushContextDelegate = delegate of nativeint -> unit

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private CreateFenceDelegate = delegate of nativeint * uint64 * uint32 * byref<Guid> * byref<nativeint> -> int

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private FenceCreateSharedHandleDelegate = delegate of nativeint * nativeint * uint32 * nativeint * byref<nativeint> -> int

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type private ContextSignalFenceDelegate = delegate of nativeint * nativeint * uint64 -> int

[<DllImport("kernel32.dll", SetLastError = true)>]
extern bool private CloseHandle(nativeint hObject)

let private comSlot<'T when 'T :> Delegate> (comObj: nativeint) (slot: int) =
    Marshal.GetDelegateForFunctionPointer<'T>(Marshal.ReadIntPtr (Marshal.ReadIntPtr comObj, slot * IntPtr.Size))

let private rgbaAt (struct (width: int, _: int, bytes: byte array)) x y =
    let at = (y * width + x) * 4
    bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]

let private rgbaSpanAt (width: int) (bytes: byte array) x y =
    let at = (y * width + x) * 4
    bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]

let private solidPixels width height (r: byte, g: byte, b: byte, a: byte) =
    let bytes = Array.zeroCreate<byte>(width * height * 4)

    for i in 0 .. (width * height) - 1 do
        let at = i * 4
        bytes[at] <- r
        bytes[at + 1] <- g
        bytes[at + 2] <- b
        bytes[at + 3] <- a

    bytes

let private windowId (window: Window) =
    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

let private pushEvent (event: SDL_Event) =
    let mutable copy = event

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&copy): bool) then
        invalidOp ("SDL_PushEvent failed: " + SDL3.SDL_GetError ())

let private mouseButton (window: Window) (down: bool) (x: float32) (y: float32) =
    let mutable event = SDL_Event ()

    event.button.``type`` <-
        if down then
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
        else
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP

    event.button.windowID <- windowId window
    event.button.button <- byte SDL3.SDL_BUTTON_LEFT
    event.button.down <- SDLBool.op_Implicit down
    event.button.clicks <- 1uy
    event.button.x <- x
    event.button.y <- y
    pushEvent event

let private mouseMove (window: Window) (x: float32) (y: float32) =
    let mutable event = SDL_Event ()
    event.motion.``type`` <- SDL_EventType.SDL_EVENT_MOUSE_MOTION
    event.motion.windowID <- windowId window
    event.motion.state <- SDL_MouseButtonFlags.SDL_BUTTON_LMASK
    event.motion.x <- x
    event.motion.y <- y
    pushEvent event

let private settle (window: Window) =
    let timer = Diagnostics.Stopwatch.StartNew ()

    while timer.ElapsedMilliseconds < 40L do
        window.Step 10 |> ignore

[<Tests>]
let tests =
    testSequenced
    <| testList
        "GpuSurface"
        [
            testCase "bound surface suppresses stage drag-highlighting while preserving child button clicks and unmount symmetry"
            <| fun () ->
                onHostThread (fun () ->
                    use window =
                        Window.Create ("Ranvier GpuSurface drag", 240, 160, hidden = true, renderer = "software")

                    window.Host.SetFocused true

                    window.Host.Document.body.innerHTML <-
                        "<style>html,body{margin:0;background:white}</style>"
                        + "<div id='stage' style='position:absolute;left:0;top:0;width:140px;height:120px'>"
                        + "  Stage text"
                        + "  <button id='hud-btn' style='position:absolute;left:8px;top:8px;width:44px;height:24px'>Act</button>"
                        + "</div>"
                        + "<canvas id='lent-canvas' width='64' height='48'></canvas>"

                    let stage = window.Host.Document.getElementById "stage"
                    let hudBtn = window.Host.Document.getElementById "hud-btn"

                    let lentCanvas =
                        window.Host.Document.getElementById "lent-canvas" :?> HTMLCanvasElement

                    let initialStageStyle = stage.getAttribute "style"

                    let surface = stage |> GpuSurface.bind window
                    Expect.equal (surface.Kind.ToString ()) "CpuFallback" "struct kind formats statically"
                    Expect.equal (surface.Backend.ToString ()) "CpuCanvas" "backend formats statically"

                    let styleAttr = stage.getAttribute "style"
                    Expect.stringContains styleAttr "position:absolute" "caller inline style is preserved"
                    Expect.isFalse (styleAttr.Contains "will-change:transform") "CPU fallback avoids offscreen GL layer promotion"
                    Expect.stringContains styleAttr "isolation:isolate" "stacking context isolation is applied"
                    Expect.stringContains styleAttr "-webkit-user-select:none" "webkit selection is disabled"
                    Expect.stringContains styleAttr "user-select:none" "standard selection is disabled"
                    Expect.stringContains styleAttr "-webkit-user-drag:none" "element drag is disabled"
                    Expect.equal (stage.getAttribute "draggable") "false" "draggable attribute defaults to false"

                    let mutable pointerDowns = 0
                    let mutable pointerMoves = 0
                    let mutable stageMousedownPrevented = false
                    let mutable buttonMousedownPrevented = true
                    let mutable buttonClicks = 0

                    stage.addEventListener ("pointerdown", fun (_: Event) -> pointerDowns <- pointerDowns + 1)

                    window.Host.Document.addEventListener ("pointermove", fun (_: Event) -> pointerMoves <- pointerMoves + 1)

                    stage.addEventListener (
                        "mousedown",
                        fun (ev: Event) ->
                            if obj.ReferenceEquals (ev.target, stage) then
                                stageMousedownPrevented <- ev.defaultPrevented
                            elif obj.ReferenceEquals (ev.target, hudBtn) then
                                buttonMousedownPrevented <- ev.defaultPrevented
                    )

                    hudBtn.addEventListener ("click", fun (_: Event) -> buttonClicks <- buttonClicks + 1)

                    window.Present ()
                    // Click interactive child button inside stage
                    mouseButton window true 20.0f 18.0f
                    settle window
                    mouseButton window false 20.0f 18.0f
                    settle window

                    Expect.equal buttonClicks 1 "interactive child button receives click"
                    Expect.isFalse buttonMousedownPrevented "interactive child button mousedown is not preventDefault-blocked"

                    // Drag directly on the stage background
                    mouseButton window true 80.0f 80.0f
                    settle window
                    mouseMove window 110.0f 85.0f
                    settle window
                    mouseMove window 175.0f 30.0f
                    settle window
                    mouseButton window false 175.0f 30.0f
                    settle window

                    Expect.isGreaterThanOrEqual pointerDowns 2 "pointerdown reaches the application"
                    Expect.isGreaterThan pointerMoves 0 "pointermove reaches the application during drag"
                    Expect.isTrue stageMousedownPrevented "stage mousedown selection initiation is suppressed"
                    Expect.isEmpty (List.ofSeq window.Host.Errors) "no host listener errors"

                    // Unmount symmetry on container element
                    GpuSurface.dispose surface
                    Expect.equal (stage.getAttribute "style") initialStageStyle "unmount restores original style attribute"
                    Expect.isNull (stage.getAttribute "draggable") "unmount removes added draggable attribute"

                    // Unmount symmetry via GpuSurface.lend on a lent <canvas>
                    let mutable userCleanedUp = false

                    let unmountCanvas =
                        lentCanvas
                        |> GpuSurface.lend window (fun lentSurf ->
                            let px = solidPixels 32 24 (200uy, 100uy, 50uy, 255uy)
                            lentSurf |> GpuSurface.presentPixelArray 32 24 px
                            Expect.equal (int lentCanvas.width, int lentCanvas.height) (32, 24) "lent canvas backing resized while active"
                            fun () -> userCleanedUp <- true)

                    unmountCanvas ()
                    Expect.isTrue userCleanedUp "lend unmounter runs caller cleanup"
                    Expect.equal (int lentCanvas.width, int lentCanvas.height) (64, 48) "lent canvas original dimensions restored on unmount"
                    Expect.isNull (lentCanvas.getAttribute "style") "lent canvas added style removed on unmount"
                    Expect.isNull (lentCanvas.getAttribute "draggable") "lent canvas added draggable removed on unmount")

            testCase "DOM and SVG overlays composite naturally over the surface across resize, ReadPixelsInto, and unbind"
            <| fun () ->
                onHostThread (fun () ->
                    use window =
                        Window.Create ("Ranvier GpuSurface overlay", 200, 150, hidden = true, renderer = "software")

                    window.Host.Document.body.innerHTML <-
                        "<style>html,body{margin:0;background:rgb(255,255,255)}</style>"
                        + "<div id='stage' style='position:absolute;left:0;top:0;width:120px;height:90px'>"
                        + "  <div id='badge' style='position:absolute;left:16px;top:16px;width:32px;height:32px;background:rgb(255,0,0);z-index:2'></div>"
                        + "  <svg style='position:absolute;left:60px;top:16px;width:32px;height:32px;z-index:2' viewBox='0 0 32 32'>"
                        + "    <rect x='0' y='0' width='32' height='32' fill='rgb(0,255,0)'/>"
                        + "  </svg>"
                        + "</div>"

                    let stage = window.Host.Document.getElementById "stage"
                    use surface = stage |> GpuSurface.bind window
                    Expect.equal surface.Kind GpuSurfaceKind.CpuFallback "software window selects CPU fallback"

                    let blue = solidPixels 120 90 (0uy, 0uy, 255uy, 255uy)

                    surface
                    |> GpuSurface.presentPixelArray 120 90 blue

                    window.Present ()

                    let struct (pw, ph) = window.PixelSize
                    let readBuf = Array.zeroCreate<byte>(pw * ph * 4)
                    let struct (rw, rh) = window.ReadPixelsInto (Span<byte> readBuf)
                    Expect.equal (rw, rh) (pw, ph) "ReadPixelsInto reports window pixel size"
                    Expect.equal (rgbaSpanAt rw readBuf 100 70) (0uy, 0uy, 255uy, 255uy) "uncovered 3D surface shows blue"
                    Expect.equal (rgbaSpanAt rw readBuf 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay composites on top"
                    Expect.equal (rgbaSpanAt rw readBuf 72 24) (0uy, 255uy, 0uy, 255uy) "SVG overlay composites on top"
                    Expect.equal (rgbaSpanAt rw readBuf 170 120) (255uy, 255uy, 255uy, 255uy) "outside stage stays white"

                    stage.setAttribute ("style", "position:absolute;left:0;top:0;width:160px;height:120px;isolation:isolate")

                    let cyan = solidPixels 160 120 (0uy, 200uy, 255uy, 255uy)
                    surface.PresentPixels (ReadOnlySpan<byte> cyan, 160, 120)
                    window.Present ()

                    let resized = window.ReadPixels ()
                    Expect.equal (surface.Width, surface.Height) (160, 120) "surface tracks resized pixel bounds"
                    Expect.equal (rgbaAt resized 140 105) (0uy, 200uy, 255uy, 255uy) "resized region receives frame"
                    Expect.equal (rgbaAt resized 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay remains on top after resize"

                    surface.Dispose ()
                    Expect.stringContains (stage.getAttribute "style") "width:160px" "caller resize edits on style survive unmount"
                    window.Present ()
                    let unbound = window.ReadPixels ()
                    Expect.equal (rgbaAt unbound 140 105) (255uy, 255uy, 255uy, 255uy) "disposal unbinds surface backing"
                    Expect.equal (rgbaAt unbound 24 24) (255uy, 0uy, 0uy, 255uy) "DOM children remain intact after unbind")

            testCase "surface enforces owner-thread and disposal contracts and survives document replacement and window disposal"
            <| fun () ->
                onHostThread (fun () ->
                    let window =
                        Window.Create ("Ranvier GpuSurface lifetime", 100, 80, hidden = true, renderer = "software")

                    let target = window.Host.Document.createElement "div"

                    window.Host.Document.body.appendChild target
                    |> ignore

                    let surface = window.BindGpuSurface target
                    let pixels = solidPixels 16 16 (10uy, 20uy, 30uy, 255uy)

                    let mutable foreignError: exn option = None

                    let worker =
                        Thread (
                            ThreadStart (fun () ->
                                try
                                    surface.PresentPixels (pixels, 16, 16)
                                with ex ->
                                    foreignError <- Some ex)
                        )

                    worker.Start ()
                    worker.Join ()

                    Expect.isTrue
                        (foreignError
                         |> Option.exists (fun ex -> ex :? InvalidOperationException))
                        "foreign thread publication is rejected"

                    surface.Dispose ()
                    surface.Dispose ()

                    Expect.throwsT<ObjectDisposedException> (fun () -> surface.PresentPixels (pixels, 16, 16)) "disposed surface rejects publication"

                    // Document replacement invalidates detached DOM references cleanly before Dispose
                    if WebCoreHost.Has "svg-document" then
                        let docTarget = window.Host.Document.createElement "div"

                        window.Host.Document.body.appendChild docTarget
                        |> ignore

                        let replacedSurface = window.BindGpuSurface docTarget
                        replacedSurface.PresentPixels (pixels, 16, 16)

                        window.Host.LoadSvg
                            "<svg xmlns='http://www.w3.org/2000/svg' width='160' height='120'><foreignObject width='160' height='120'><div xmlns='http://www.w3.org/1999/xhtml' id='fresh'></div></foreignObject></svg>"

                        replacedSurface.Dispose ()

                        let secondTarget = window.Host.Document.getElementById "fresh"
                        let liveSurface = window.BindGpuSurface secondTarget
                        liveSurface.PresentPixels (pixels, 16, 16)

                        // Window disposal cleans up any still-attached surface cleanly, and later surface Dispose is a no-op.
                        (window :> IDisposable).Dispose()
                        Expect.isTrue liveSurface.IsDisposed "window disposal disposes attached surfaces"
                        liveSurface.Dispose ()
                    else
                        let secondTarget = window.Host.Document.createElement "div"

                        window.Host.Document.body.appendChild secondTarget
                        |> ignore

                        let liveSurface = window.BindGpuSurface secondTarget
                        liveSurface.PresentPixels (pixels, 16, 16)
                        (window :> IDisposable).Dispose()
                        Expect.isTrue liveSurface.IsDisposed "window disposal disposes attached surfaces"
                        liveSurface.Dispose ())

            testCase "hardware D3D11 capability backend shares producer device, synchronizes via GPU shared fence, and composites zero-CPU frames"
            <| fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "D3D11 shared texture surface is Windows only"

                onHostThread (fun () ->
                    use window = Window.Create ("Ranvier D3D11 GpuSurface", 200, 150, hidden = true)

                    match window.SwapChain with
                    | None when Environment.GetEnvironmentVariable "RANVIER_REQUIRE_SWAP_CHAIN" = "1" ->
                        failtest "RANVIER_REQUIRE_SWAP_CHAIN is set, and this window has no D3D11 swap chain"
                    | None -> skiptest "the machine has no D3D11 hardware device in this environment"
                    | Some _ ->
                        if not (WebCoreHost.Has "texture-input-d3d11") then
                            skiptest "runtime lacks texture-input-d3d11"

                        Expect.isTrue window.GpuAdapterVendorDevice.IsSome "window exposes D3D11 adapter vendor/device IDs"
                        Expect.isTrue window.GpuAdapterLuid.IsSome "window exposes D3D11 adapter LUID"

                        window.Host.Document.body.innerHTML <-
                            "<style>html,body{margin:0;background:white}</style>"
                            + "<div id='stage' style='position:absolute;left:0;top:0;width:120px;height:90px'>"
                            + "  <div id='badge' style='position:absolute;left:16px;top:16px;width:32px;height:32px;background:rgb(255,0,0)'></div>"
                            + "</div>"
                            + "<div id='aux' style='position:absolute;left:130px;top:0;width:40px;height:40px'></div>"

                        let stage = window.Host.Document.getElementById "stage"
                        let aux = window.Host.Document.getElementById "aux"
                        let badge = window.Host.Document.getElementById "badge"
                        use surface = stage |> GpuSurface.bind window
                        use auxSurface = aux |> GpuSurface.bind window
                        Expect.equal surface.Kind GpuSurfaceKind.D3D11SharedTexture "hardware window binds D3D11 shared texture"
                        Expect.isTrue surface.IsHardwareComposited "hardware composited GPU transport active"
                        Expect.stringContains (stage.getAttribute "style") "will-change:transform" "hardware surface promotes composited layer"

                        let d3d =
                            match surface.Backend, auxSurface.Backend with
                            | GpuSurfaceBackend.D3D11 primary, GpuSurfaceBackend.D3D11 secondary ->
                                Expect.equal primary.Device secondary.Device "multiple GpuSurfaces share one producer ID3D11Device"
                                Expect.equal primary.AdapterLuid window.GpuAdapterLuid "D3D11GpuSurface reports window adapter LUID"
                                primary
                            | other, _ ->
                                failtest (
                                    "expected GpuSurfaceBackend.D3D11, got "
                                    + other.ToString ()
                                )

                        let blue = solidPixels 120 90 (0uy, 0uy, 255uy, 255uy)
                        surface.PresentPixels (blue, 120, 90)
                        window.Present ()

                        let struct (pw, ph) = window.PixelSize
                        let pixelBuf = Array.zeroCreate<byte>(pw * ph * 4)
                        let struct (rw, _) = window.ReadPixelsInto (Span<byte> pixelBuf)
                        Expect.equal (rgbaSpanAt rw pixelBuf 90 70) (0uy, 0uy, 255uy, 255uy) "D3D11 texture composites in page"
                        Expect.equal (rgbaSpanAt rw pixelBuf 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay composites over D3D11 texture"

                        // Mutate HTML overlay alone without republishing 3D; retained GPU layer stays visible!
                        badge.setAttribute ("style", "position:absolute;left:16px;top:16px;width:32px;height:32px;background:rgb(255,255,0)")
                        window.Present ()
                        let struct (rw2, _) = window.ReadPixelsInto (Span<byte> pixelBuf)
                        Expect.equal surface.FramesPublished 1L "HTML-only update requires no 3D republication"
                        Expect.equal (rgbaSpanAt rw2 pixelBuf 90 70) (0uy, 0uy, 255uy, 255uy) "retained D3D11 texture remains visible"

                        Expect.equal
                            (rgbaSpanAt rw2 pixelBuf 24 24)
                            (255uy, 255uy, 0uy, 255uy)
                            "updated HTML overlay composites over retained D3D11 texture"

                        // Import AcquireSharedStagingHandle on ID3D11Device1 (slot 48 = OpenSharedResource1) and synchronize via shared GPU fence
                        let handle = d3d.AcquireSharedStagingHandle (120, 90)
                        Expect.notEqual handle 0n "NT shared staging handle is created for DX12/Vulkan interop"

                        let mutable dev5Id = Guid "8ffde202-a0e7-45df-9e01-e837801b5ea0"
                        let mutable ctx4Id = Guid "917600da-f58c-4c33-98d8-3e15b390fa24"
                        let mutable fenceId = Guid "affde9d1-1df7-4bb7-8a34-0f46251dab80"
                        let mutable tex2dId = Guid "6f15aaf2-d208-4e89-9ab4-489535d34f9c"
                        let mutable dev5 = 0n
                        let mutable ctx4 = 0n
                        let mutable openedTex = 0n
                        let mutable producerFence = 0n
                        let mutable fenceNtHandle = 0n
                        let mutable importedConsumerFence = 0n

                        try
                            let qiHr = Marshal.QueryInterface (d3d.Device, &dev5Id, &dev5)
                            Expect.isTrue (qiHr >= 0 && dev5 <> 0n) "QueryInterface(ID3D11Device5) succeeds"
                            let qiCtxHr = Marshal.QueryInterface (d3d.Context, &ctx4Id, &ctx4)
                            Expect.isTrue (qiCtxHr >= 0 && ctx4 <> 0n) "QueryInterface(ID3D11DeviceContext4) succeeds"

                            let openShared = comSlot<OpenSharedResource1Delegate> dev5 48
                            let openHr = openShared.Invoke (dev5, handle, &tex2dId, &openedTex)
                            Expect.isTrue (openHr >= 0 && openedTex <> 0n) "OpenSharedResource1 imports NT shared staging handle"

                            let green = solidPixels 120 90 (0uy, 255uy, 0uy, 255uy)
                            use pinnedGreen = fixed green
                            let updateSub = comSlot<UpdateSubresourceDelegate> d3d.Context 48
                            updateSub.Invoke (d3d.Context, openedTex, 0u, 0n, NativePtr.toNativeInt pinnedGreen, 120u * 4u, 0u)

                            if d3d.SupportsSharedFence then
                                // D3D11_FENCE_FLAG_SHARED = 0x2u, GENERIC_ALL = 0x10000000u
                                let createFence = comSlot<CreateFenceDelegate> dev5 68
                                let cfHr = createFence.Invoke (dev5, 0UL, 0x2u, &fenceId, &producerFence)
                                Expect.isTrue (cfHr >= 0 && producerFence <> 0n) "ID3D11Device5::CreateFence succeeds"

                                let createFenceHandle = comSlot<FenceCreateSharedHandleDelegate> producerFence 7

                                let chHr =
                                    createFenceHandle.Invoke (producerFence, 0n, 0x10000000u, 0n, &fenceNtHandle)

                                Expect.isTrue (chHr >= 0 && fenceNtHandle <> 0n) "ID3D11Fence::CreateSharedHandle succeeds"

                                importedConsumerFence <- d3d.OpenSharedFence fenceNtHandle
                                Expect.notEqual importedConsumerFence 0n "D3D11GpuSurface.OpenSharedFence imports shared NT fence"

                                let signalFence = comSlot<ContextSignalFenceDelegate> ctx4 147
                                Expect.isTrue (signalFence.Invoke (ctx4, producerFence, 1UL) >= 0) "ID3D11DeviceContext4::Signal succeeds"
                                (comSlot<FlushContextDelegate> d3d.Context 111).Invoke d3d.Context
                                d3d.CommitSharedStagingAfterFence (importedConsumerFence, 1UL)
                            else
                                (comSlot<FlushContextDelegate> d3d.Context 111).Invoke d3d.Context
                                d3d.CommitSharedStaging ()
                        finally
                            d3d.ReleaseFence importedConsumerFence

                            if fenceNtHandle <> 0n then
                                CloseHandle fenceNtHandle |> ignore

                            if producerFence <> 0n then
                                Marshal.Release producerFence |> ignore

                            if openedTex <> 0n then
                                Marshal.Release openedTex |> ignore

                            if ctx4 <> 0n then
                                Marshal.Release ctx4 |> ignore

                            if dev5 <> 0n then
                                Marshal.Release dev5 |> ignore

                        window.Present ()

                        let stagedImage = window.ReadPixels ()
                        Expect.equal (rgbaAt stagedImage 90 70) (0uy, 255uy, 0uy, 255uy) "NT shared staging commit updates D3D11 texture in VRAM"

                        Expect.equal
                            (rgbaAt stagedImage 24 24)
                            (255uy, 255uy, 0uy, 255uy)
                            "HTML overlay remains composited over committed staging frame"

                        Expect.equal window.CpuFramesPresented 0L "zero CPU presentation frames")
        ]
