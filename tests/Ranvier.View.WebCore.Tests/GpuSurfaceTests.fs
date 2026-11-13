module Ranvier.View.WebCore.Tests.GpuSurfaceTests

open System
open System.Threading
open Expecto
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

type private OpenSharedResource1Delegate = delegate of nativeint * nativeint * byref<Guid> * byref<nativeint> -> int
type private UpdateSubresourceDelegate = delegate of nativeint * nativeint * uint32 * nativeint * nativeint * uint32 * uint32 -> unit
type private FlushContextDelegate = delegate of nativeint -> unit

let private rgbaAt (struct (width: int, _: int, bytes: byte array)) x y =
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
        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

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
            testCase "bound surface promotes compositing and prevents drag-highlighting by default while preserving pointer input"
            <| fun () ->
                onHostThread (fun () ->
                    use window =
                        Window.Create ("Ranvier GpuSurface drag", 240, 160, hidden = true, renderer = "software")

                    window.Host.SetFocused true

                    window.Host.Document.body.innerHTML <-
                        "<style>html,body{margin:0;background:white}</style>"
                        + "<div id='stage' style='position:absolute;left:0;top:0;width:140px;height:120px'>Stage text</div>"
                        + "<div id='neighbor' style='position:absolute;left:145px;top:10px;width:90px;height:80px'>Neighbor text</div>"

                    let stage = window.Host.Document.getElementById "stage"
                    use surface = window.BindGpuSurface stage

                    let styleAttr = stage.getAttribute "style"
                    Expect.stringContains styleAttr "position:absolute" "caller inline style is preserved"
                    Expect.stringContains styleAttr "will-change:transform" "composited layer promotion is applied"
                    Expect.stringContains styleAttr "-webkit-user-select:none" "webkit selection is disabled"
                    Expect.stringContains styleAttr "user-select:none" "standard selection is disabled"
                    Expect.stringContains styleAttr "-webkit-user-drag:none" "element drag is disabled"
                    Expect.equal (stage.getAttribute "draggable") "false" "draggable attribute defaults to false"

                    let mutable pointerDowns = 0
                    let mutable pointerMoves = 0
                    let mutable mousedownPrevented = false

                    stage.addEventListener ("pointerdown", fun (_: Event) -> pointerDowns <- pointerDowns + 1)

                    window.Host.Document.addEventListener ("pointermove", fun (_: Event) -> pointerMoves <- pointerMoves + 1)

                    stage.addEventListener ("mousedown", fun (ev: Event) -> mousedownPrevented <- ev.defaultPrevented)

                    window.Present ()
                    mouseButton window true 40.0f 40.0f
                    settle window
                    mouseMove window 90.0f 50.0f
                    settle window
                    mouseMove window 175.0f 30.0f
                    settle window
                    mouseButton window false 175.0f 30.0f
                    settle window

                    Expect.equal pointerDowns 1 "pointerdown reaches the application"
                    Expect.isGreaterThan pointerMoves 0 "pointermove reaches the application during drag"
                    Expect.isTrue mousedownPrevented "mousedown default selection initiation is suppressed"
                    Expect.isEmpty (List.ofSeq window.Host.Errors) "no host listener errors")

            testCase "DOM and SVG overlays composite naturally over the surface across resize and unbind"
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
                    use surface = window.BindGpuSurface stage
                    Expect.equal surface.Kind GpuSurfaceKind.CpuFallback "software window selects CPU fallback"

                    let blue = solidPixels 120 90 (0uy, 0uy, 255uy, 255uy)
                    surface.PresentPixels (blue, 120, 90)
                    window.Present ()

                    let first = window.ReadPixels ()
                    Expect.equal (rgbaAt first 100 70) (0uy, 0uy, 255uy, 255uy) "uncovered 3D surface shows blue"
                    Expect.equal (rgbaAt first 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay composites on top"
                    Expect.equal (rgbaAt first 72 24) (0uy, 255uy, 0uy, 255uy) "SVG overlay composites on top"
                    Expect.equal (rgbaAt first 170 120) (255uy, 255uy, 255uy, 255uy) "outside stage stays white"

                    stage.setAttribute ("style", "position:absolute;left:0;top:0;width:160px;height:120px;will-change:transform")

                    let cyan = solidPixels 160 120 (0uy, 200uy, 255uy, 255uy)
                    surface.PresentPixels (cyan, 160, 120)
                    window.Present ()

                    let resized = window.ReadPixels ()
                    Expect.equal (surface.Width, surface.Height) (160, 120) "surface tracks resized pixel bounds"
                    Expect.equal (rgbaAt resized 140 105) (0uy, 200uy, 255uy, 255uy) "resized region receives frame"
                    Expect.equal (rgbaAt resized 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay remains on top after resize"

                    (surface :> IDisposable).Dispose()
                    window.Present ()
                    let unbound = window.ReadPixels ()
                    Expect.equal (rgbaAt unbound 140 105) (255uy, 255uy, 255uy, 255uy) "disposal unbinds surface backing"
                    Expect.equal (rgbaAt unbound 24 24) (255uy, 0uy, 0uy, 255uy) "DOM children remain intact after unbind")

            testCase "surface enforces owner-thread and disposal contracts and survives window disposal"
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

                    (surface :> IDisposable).Dispose()
                    (surface :> IDisposable).Dispose()

                    Expect.throwsT<ObjectDisposedException> (fun () -> surface.PresentPixels (pixels, 16, 16)) "disposed surface rejects publication"

                    let secondTarget = window.Host.Document.createElement "div"

                    window.Host.Document.body.appendChild secondTarget
                    |> ignore

                    let liveSurface = window.BindGpuSurface secondTarget
                    liveSurface.PresentPixels (pixels, 16, 16)

                    // Window disposal cleans up any still-attached surface cleanly, and later surface Dispose is a no-op.
                    (window :> IDisposable).Dispose()
                    Expect.isTrue liveSurface.IsDisposed "window disposal disposes attached surfaces"
                    (liveSurface :> IDisposable).Dispose())

            testCase "hardware D3D11 shared texture surface composites DOM overlays with zero CPU frames"
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

                        window.Host.Document.body.innerHTML <-
                            "<style>html,body{margin:0;background:white}</style>"
                            + "<div id='stage' style='position:absolute;left:0;top:0;width:120px;height:90px'>"
                            + "  <div id='badge' style='position:absolute;left:16px;top:16px;width:32px;height:32px;background:rgb(255,0,0)'></div>"
                            + "</div>"

                        let stage = window.Host.Document.getElementById "stage"
                        let badge = window.Host.Document.getElementById "badge"
                        use surface = window.BindGpuSurface stage
                        Expect.equal surface.Kind GpuSurfaceKind.D3D11SharedTexture "hardware window binds D3D11 shared texture"
                        Expect.isTrue surface.IsZeroCopyGpu "zero-copy GPU transport active"

                        let blue = solidPixels 120 90 (0uy, 0uy, 255uy, 255uy)
                        surface.PresentPixels (blue, 120, 90)
                        window.Present ()

                        let firstImage = window.ReadPixels ()
                        Expect.equal (rgbaAt firstImage 90 70) (0uy, 0uy, 255uy, 255uy) "D3D11 texture composites in page"
                        Expect.equal (rgbaAt firstImage 24 24) (255uy, 0uy, 0uy, 255uy) "HTML overlay composites over D3D11 texture"

                        // Mutate HTML overlay alone without republishing 3D; retained GPU layer stays visible!
                        badge.setAttribute ("style", "position:absolute;left:16px;top:16px;width:32px;height:32px;background:rgb(255,255,0)")
                        window.Present ()
                        let retainedImage = window.ReadPixels ()
                        Expect.equal surface.FramesPublished 1L "HTML-only update requires no 3D republication"
                        Expect.equal (rgbaAt retainedImage 90 70) (0uy, 0uy, 255uy, 255uy) "retained D3D11 texture remains visible"

                        Expect.equal
                            (rgbaAt retainedImage 24 24)
                            (255uy, 255uy, 0uy, 255uy)
                            "updated HTML overlay composites over retained D3D11 texture"

                        // Import AcquireSharedStagingHandle on ID3D11Device1 (slot 48 = OpenSharedResource1) and upload green pixels
                        let handle = surface.AcquireSharedStagingHandle (120, 90)
                        Expect.notEqual handle 0n "NT shared staging handle is created for DX12/Vulkan interop"

                        let mutable dev1Id = Guid "a04bfb29-08ef-43d6-a49c-a9bdbdcbe686"
                        let mutable tex2dId = Guid "6f15aaf2-d208-4e89-9ab4-489535d34f9c"
                        let mutable dev1 = 0n
                        let mutable openedTex = 0n

                        try
                            let qiHr =
                                System.Runtime.InteropServices.Marshal.QueryInterface (surface.D3D11Device, &dev1Id, &dev1)

                            Expect.isTrue (qiHr >= 0 && dev1 <> 0n) "QueryInterface(ID3D11Device1) succeeds"

                            let openSharedPtr =
                                System.Runtime.InteropServices.Marshal.ReadIntPtr (
                                    System.Runtime.InteropServices.Marshal.ReadIntPtr dev1,
                                    48 * IntPtr.Size
                                )

                            let openShared =
                                System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<OpenSharedResource1Delegate> openSharedPtr

                            let openHr = openShared.Invoke (dev1, handle, &tex2dId, &openedTex)
                            Expect.isTrue (openHr >= 0 && openedTex <> 0n) "OpenSharedResource1 imports NT shared staging handle"

                            let green = solidPixels 120 90 (0uy, 255uy, 0uy, 255uy)
                            use pinnedGreen = fixed green

                            let updatePtr =
                                System.Runtime.InteropServices.Marshal.ReadIntPtr (
                                    System.Runtime.InteropServices.Marshal.ReadIntPtr surface.D3D11Context,
                                    48 * IntPtr.Size
                                )

                            let updateSub =
                                System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<UpdateSubresourceDelegate> updatePtr

                            let flushPtr =
                                System.Runtime.InteropServices.Marshal.ReadIntPtr (
                                    System.Runtime.InteropServices.Marshal.ReadIntPtr surface.D3D11Context,
                                    111 * IntPtr.Size
                                )

                            let flushCtx =
                                System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<FlushContextDelegate> flushPtr

                            updateSub.Invoke (surface.D3D11Context, openedTex, 0u, 0n, NativePtr.toNativeInt pinnedGreen, 120u * 4u, 0u)
                            flushCtx.Invoke surface.D3D11Context
                        finally
                            if openedTex <> 0n then
                                System.Runtime.InteropServices.Marshal.Release openedTex
                                |> ignore

                            if dev1 <> 0n then
                                System.Runtime.InteropServices.Marshal.Release dev1
                                |> ignore

                        surface.CommitSharedStaging ()
                        window.Present ()

                        let stagedImage = window.ReadPixels ()
                        Expect.equal (rgbaAt stagedImage 90 70) (0uy, 255uy, 0uy, 255uy) "NT shared staging commit updates D3D11 texture in VRAM"

                        Expect.equal
                            (rgbaAt stagedImage 24 24)
                            (255uy, 255uy, 0uy, 255uy)
                            "HTML overlay remains composited over committed staging frame"

                        Expect.equal window.CpuFramesPresented 0L "zero CPU presentation frames")
        ]
