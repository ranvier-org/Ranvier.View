module Ranvier.View.WebCore.Tests.IOSurfaceTests

open System
open System.Threading
open System.Runtime.InteropServices
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

module private MetalInput =
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Create = delegate of int * int * int -> nativeint

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Surface = delegate of nativeint -> nativeint

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Publish = delegate of nativeint * int -> int

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Destroy = delegate of nativeint -> unit

    let pixel (struct (width: int, _: int, pixels: byte array)) x y =
        let index = (y * width + x) * 4
        pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]

[<Tests>]
let tests =
    testList
        "IOSurface transport"
        [
            hostCase "unavailable IOSurface input rejects before resolving its native endpoint"
            <| fun host ->
                if WebCoreHost.Has "iosurface-input" then
                    skiptest "requires a runtime without IOSurface input"

                let target = host.Document.createElement "div"
                Expect.throwsT<NotSupportedException> (fun () -> host.BindIOSurface (target, 0n, false)) "explicit unsupported capability"

            testCase "IOSurface input rejects foreign-thread and disposed host admission"
            <| fun () ->
                onHostThread (fun () ->
                    use host = WebCoreHost.Create (100, 80)
                    let target = host.Document.createElement "div"
                    let mutable failure: exn option = None

                    let worker =
                        Thread (
                            ThreadStart (fun () ->
                                try
                                    host.BindIOSurface (target, 0n, false)
                                with error ->
                                    failure <- Some error)
                        )

                    worker.Start ()
                    worker.Join ()

                    Expect.isTrue
                        (failure
                         |> Option.exists (fun error -> error :? InvalidOperationException))
                        "owner check precedes transport access"

                    (host :> IDisposable).Dispose()

                    Expect.throwsT<ObjectDisposedException>
                        (fun () -> host.BindIOSurface (target, 0n, false))
                        "disposed check precedes target access")

            testCase "Metal presents BGRA IOSurface frames at fractional backing scales"
            <| fun () ->
                onHostThread (fun () ->
                    if
                        not (
                            OperatingSystem.IsMacOS ()
                            && WebCoreHost.Has "iosurface-frames"
                        )
                    then
                        skiptest "requires the macOS IOSurface GPU runtime"

                    use window =
                        Window.Create ("Ranvier IOSurface transport", 320, 240, renderer = "metal")

                    window.Host.Document.body.innerHTML <-
                        "<style>html,body{margin:0;background:white}#probe{width:40px;height:40px;background:red;will-change:transform}</style><div id=probe></div>"

                    Expect.isTrue (WebCoreHost.Has "iosurface-input") "matching macOS input endpoint"
                    let input = window.Host.Document.createElement "div"
                    input.setAttribute ("style", "position:absolute;left:50px;top:0;width:20px;height:20px;will-change:transform")

                    window.Host.Document.body.appendChild input
                    |> ignore

                    window.Host.BindIOSurface (input, 0n, false)
                    Expect.throwsT<NotSupportedException> (fun () -> window.Host.NativeTextureChanged input) "unbound surface rejects publication"

                    for scale in [ 1.0f; 1.5f; 2.0f ] do
                        window.Host.SetDeviceScale scale
                        window.Host.Resize (81, 61)

                        Expect.equal
                            window.Host.PaintSize
                            (struct (int (MathF.Floor (81.0f * scale)), int (MathF.Floor (61.0f * scale))))
                            "physical backing uses floor at odd CSS dimensions"

                        window.Present ()
                        let struct (width, height, pixels) = window.ReadPixels ()
                        let x = int (5.0f * scale)
                        let y = int (5.0f * scale)
                        Expect.isGreaterThan width x "physical output width"
                        Expect.isGreaterThan height y "physical output height"
                        let index = (y * width + x) * 4

                        Expect.equal
                            (pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3])
                            (255uy, 0uy, 0uy, 255uy)
                            "BGRA import produces red RGBA readback"

                        Expect.equal window.PendingIOSurfaceFrame 0n "GPU completion releases the native frame"

                    Expect.isGreaterThan window.GpuFramesPresented 0L "shared GPU output was presented"
                    Expect.equal window.CpuFramesPresented 0L "no CPU presentation fallback")
            testCase "public IOSurface binding presents completed native Metal input and retains its storage"
            <| fun () ->
                onHostThread (fun () ->
                    if
                        not (
                            OperatingSystem.IsMacOS ()
                            && WebCoreHost.Has "iosurface-frames"
                            && WebCoreHost.Has "iosurface-input"
                        )
                    then
                        skiptest "requires the macOS IOSurface GPU runtime"

                    let helper = Environment.GetEnvironmentVariable "RANVIER_MAC_INPUT_HELPER"

                    if String.IsNullOrWhiteSpace helper then
                        skiptest "set RANVIER_MAC_INPUT_HELPER to the tests/mac/build_metal_input.py output"

                    let library = NativeLibrary.Load helper

                    try
                        let create =
                            Marshal.GetDelegateForFunctionPointer<MetalInput.Create>(
                                NativeLibrary.GetExport (library, "ranvier_test_metal_input_create")
                            )

                        let surface =
                            Marshal.GetDelegateForFunctionPointer<MetalInput.Surface>(
                                NativeLibrary.GetExport (library, "ranvier_test_metal_input_surface")
                            )

                        let publish =
                            Marshal.GetDelegateForFunctionPointer<MetalInput.Publish>(
                                NativeLibrary.GetExport (library, "ranvier_test_metal_input_publish")
                            )

                        let destroy =
                            Marshal.GetDelegateForFunctionPointer<MetalInput.Destroy>(
                                NativeLibrary.GetExport (library, "ranvier_test_metal_input_destroy")
                            )

                        use window =
                            Window.Create ("Ranvier native Metal input", 320, 240, renderer = "metal")

                        window.Host.Document.body.innerHTML <- "<style>html,body{margin:0;background:white}</style>"
                        window.Host.SetDeviceScale 1.0f
                        window.Host.Resize (161, 121)
                        let target = window.Host.Document.createElement "div"

                        target.setAttribute (
                            "style",
                            "position:absolute;left:20px;top:10px;width:80px;height:60px;will-change:transform,opacity;transform:translateX(5px)"
                        )

                        window.Host.Document.body.appendChild target
                        |> ignore

                        let input = create.Invoke (16, 16, 0)
                        Expect.notEqual input 0n "actual native Metal producer"

                        try
                            Expect.equal (publish.Invoke (input, 0)) 0 "GPU producer completes its red render pass"
                            window.Host.BindIOSurface (target, surface.Invoke input, false)
                            window.Present ()

                            Expect.equal
                                (MetalInput.pixel (window.ReadPixels ()) 30 20)
                                (255uy, 0uy, 0uy, 255uy)
                                "public wrapper imports native Metal pixels"

                            Expect.equal (publish.Invoke (input, 1)) 0 "GPU producer completes its blue render pass"
                            window.Host.NativeTextureChanged target
                            window.Present ()

                            Expect.equal
                                (MetalInput.pixel (window.ReadPixels ()) 30 20)
                                (0uy, 0uy, 255uy, 255uy)
                                "publication updates retained native input"
                        finally
                            destroy.Invoke input

                        window.Present ()

                        Expect.equal
                            (MetalInput.pixel (window.ReadPixels ()) 30 20)
                            (0uy, 0uy, 255uy, 255uy)
                            "host retains storage after producer ownership ends"

                        let invalid = create.Invoke (17, 13, 1)
                        Expect.notEqual invalid 0n "unsupported-format fixture storage"

                        try
                            Expect.throwsT<NotSupportedException>
                                (fun () -> window.Host.BindIOSurface (target, surface.Invoke invalid, false))
                                "failed replacement is explicit"
                        finally
                            destroy.Invoke invalid

                        window.Present ()

                        Expect.equal
                            (MetalInput.pixel (window.ReadPixels ()) 30 20)
                            (0uy, 0uy, 255uy, 255uy)
                            "failed replacement preserves prior binding"

                        window.Host.BindIOSurface (target, 0n, false)
                        window.Present ()

                        Expect.equal
                            (MetalInput.pixel (window.ReadPixels ()) 30 20)
                            (255uy, 255uy, 255uy, 255uy)
                            "explicit unbind releases composited input"

                        Expect.throwsT<NotSupportedException> (fun () -> window.Host.NativeTextureChanged target) "unbound publication is rejected"
                        Expect.equal window.CpuFramesPresented 0L "native input stays on the shared GPU presentation path"
                        GC.KeepAlive create
                        GC.KeepAlive surface
                        GC.KeepAlive publish
                        GC.KeepAlive destroy
                    finally
                        NativeLibrary.Free library)

            testCase "Metal completion and native release failures preserve a real frame for owner cleanup"
            <| fun () ->
                onHostThread (fun () ->
                    if
                        not (
                            OperatingSystem.IsMacOS ()
                            && WebCoreHost.Has "iosurface-frames"
                        )
                    then
                        skiptest "requires the macOS IOSurface GPU runtime"

                    for failCompletion in [ true; false ] do
                        use window = Window.Create ("Ranvier IOSurface retry", 160, 120, renderer = "metal")
                        window.Host.Document.body.innerHTML <- "<style>body{margin:0;background:red}</style>retry"
                        let mutable first = true
                        let mutable completions = 0
                        let mutable releaseAttempts = 0
                        let mutable forwardedReleases = 0

                        Expect.isTrue
                            (window.SetIOSurfaceCompletion (fun finish ->
                                completions <- completions + 1

                                if failCompletion && first then
                                    first <- false
                                    invalidOp "test-only Metal completion failure"

                                finish ()))
                            "real Metal consumer"

                        Expect.isTrue
                            (window.SetIOSurfaceFrameRelease (fun frame ->
                                releaseAttempts <- releaseAttempts + 1

                                if not failCompletion && first then
                                    first <- false
                                    -5
                                else
                                    forwardedReleases <- forwardedReleases + 1
                                    RanvierWebCoreAbi.Native.rv_gpu_frame_release frame))
                            "real native release"

                        Expect.throwsT<InvalidOperationException> (fun () -> window.Present ()) "completion/release failure reaches the caller"
                        let retained = window.PendingIOSurfaceFrame
                        Expect.notEqual retained 0n "actual acquired frame remains owned"
                        let mutable wrongThread: exn option = None

                        let worker =
                            Thread (
                                ThreadStart (fun () ->
                                    try
                                        (window :> IDisposable).Dispose()
                                    with error ->
                                        wrongThread <- Some error)
                            )

                        worker.Start ()
                        worker.Join ()

                        Expect.isTrue
                            (wrongThread
                             |> Option.exists (fun error -> error :? InvalidOperationException))
                            "foreign disposal is rejected"

                        Expect.equal window.PendingIOSurfaceFrame retained "foreign disposal preserves the lease"
                        (window :> IDisposable).Dispose()
                        Expect.equal window.PendingIOSurfaceFrame 0n "owner cleanup retries the frame before host destruction"
                        Expect.equal completions 2 "one failed delivery plus one cleanup completion"
                        Expect.equal releaseAttempts (if failCompletion then 1 else 2) "completion failure never forwards release early"
                        Expect.equal forwardedReleases 1 "native release succeeds exactly once"
                        (window :> IDisposable).Dispose())
        ]
