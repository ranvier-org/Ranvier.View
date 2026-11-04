module internal Ranvier.View.WebCore.Tests.IOSurfaceChecks

open System
open System.Threading
open System.Runtime.InteropServices
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

let mutable private assertions = 0

let assertionCount () =
    assertions

let private check condition message =
    assertions <- assertions + 1

    if not condition then
        invalidOp message

let private throws expected action message =
    let caught =
        try
            action ()
            false
        with error when expected error ->
            true

    check caught message

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

let unavailableInput (host: WebCoreHost) =
    let target = host.Document.createElement "div"
    throws (fun error -> error :? NotSupportedException) (fun () -> host.BindIOSurface (target, 0n, false)) "explicit unsupported capability"

let inputAdmission () =
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

    check
        (failure
         |> Option.exists (fun error -> error :? InvalidOperationException))
        "owner check precedes transport access"

    (host :> IDisposable).Dispose()
    throws (fun error -> error :? ObjectDisposedException) (fun () -> host.BindIOSurface (target, 0n, false)) "disposed check precedes target access"

let fractionalFrames () =
    use window =
        Window.Create ("Ranvier IOSurface transport", 320, 240, renderer = "metal")

    window.Host.Document.body.innerHTML <-
        "<style>html,body{margin:0;background:white}#probe{width:40px;height:40px;background:red;will-change:transform}</style><div id=probe></div>"

    check (WebCoreHost.Has "iosurface-input") "matching macOS input endpoint"
    let input = window.Host.Document.createElement "div"
    input.setAttribute ("style", "position:absolute;left:50px;top:0;width:20px;height:20px;will-change:transform")

    window.Host.Document.body.appendChild input
    |> ignore

    window.Host.BindIOSurface (input, 0n, false)
    throws (fun error -> error :? NotSupportedException) (fun () -> window.Host.NativeTextureChanged input) "unbound surface rejects publication"

    for scale in [ 1.0f; 1.5f; 2.0f ] do
        window.Host.SetDeviceScale scale
        window.Host.Resize (81, 61)

        check
            (window.Host.PaintSize = struct (int (MathF.Floor (81.0f * scale)), int (MathF.Floor (61.0f * scale))))
            "physical backing uses floor at odd CSS dimensions"

        window.Present ()
        let struct (width, height, pixels) = window.ReadPixels ()
        let x = int (5.0f * scale)
        let y = int (5.0f * scale)
        check (width > x) "physical output width"
        check (height > y) "physical output height"
        let index = (y * width + x) * 4

        check
            ((pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]) = (255uy, 0uy, 0uy, 255uy))
            "BGRA import produces red RGBA readback"

        check (window.PendingIOSurfaceFrame = 0n) "GPU completion releases the native frame"

    check (window.GpuFramesPresented > 0L) "shared GPU output was presented"
    check (window.CpuFramesPresented = 0L) "no CPU presentation fallback"

let nativeInput () =
    let helper = Environment.GetEnvironmentVariable "RANVIER_MAC_INPUT_HELPER"

    if String.IsNullOrWhiteSpace helper then
        invalidOp "set RANVIER_MAC_INPUT_HELPER to the tests/mac/build_metal_input.py output"

    let library = NativeLibrary.Load helper

    try
        let create =
            Marshal.GetDelegateForFunctionPointer<MetalInput.Create>(NativeLibrary.GetExport (library, "ranvier_test_metal_input_create"))

        let surface =
            Marshal.GetDelegateForFunctionPointer<MetalInput.Surface>(NativeLibrary.GetExport (library, "ranvier_test_metal_input_surface"))

        let publish =
            Marshal.GetDelegateForFunctionPointer<MetalInput.Publish>(NativeLibrary.GetExport (library, "ranvier_test_metal_input_publish"))

        let destroy =
            Marshal.GetDelegateForFunctionPointer<MetalInput.Destroy>(NativeLibrary.GetExport (library, "ranvier_test_metal_input_destroy"))

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
        check (input <> 0n) "actual native Metal producer"

        try
            check (publish.Invoke (input, 0) = 0) "GPU producer completes its red render pass"
            window.Host.BindIOSurface (target, surface.Invoke input, false)
            window.Present ()
            check (MetalInput.pixel (window.ReadPixels ()) 30 20 = (255uy, 0uy, 0uy, 255uy)) "public wrapper imports native Metal pixels"
            check (publish.Invoke (input, 1) = 0) "GPU producer completes its blue render pass"
            window.Host.NativeTextureChanged target
            window.Present ()
            check (MetalInput.pixel (window.ReadPixels ()) 30 20 = (0uy, 0uy, 255uy, 255uy)) "publication updates retained native input"
        finally
            destroy.Invoke input

        window.Present ()
        check (MetalInput.pixel (window.ReadPixels ()) 30 20 = (0uy, 0uy, 255uy, 255uy)) "host retains storage after producer ownership ends"
        let invalid = create.Invoke (17, 13, 1)
        check (invalid <> 0n) "unsupported-format fixture storage"

        try
            throws
                (fun error -> error :? NotSupportedException)
                (fun () -> window.Host.BindIOSurface (target, surface.Invoke invalid, false))
                "failed replacement is explicit"
        finally
            destroy.Invoke invalid

        window.Present ()
        check (MetalInput.pixel (window.ReadPixels ()) 30 20 = (0uy, 0uy, 255uy, 255uy)) "failed replacement preserves prior binding"
        window.Host.BindIOSurface (target, 0n, false)
        window.Present ()
        check (MetalInput.pixel (window.ReadPixels ()) 30 20 = (255uy, 255uy, 255uy, 255uy)) "explicit unbind releases composited input"
        throws (fun error -> error :? NotSupportedException) (fun () -> window.Host.NativeTextureChanged target) "unbound publication is rejected"
        check (window.CpuFramesPresented = 0L) "native input stays on the shared GPU presentation path"
        GC.KeepAlive create
        GC.KeepAlive surface
        GC.KeepAlive publish
        GC.KeepAlive destroy
    finally
        NativeLibrary.Free library

let cleanupRetry () =
    for failCompletion in [ true; false ] do
        use window = Window.Create ("Ranvier IOSurface retry", 160, 120, renderer = "metal")
        window.Host.Document.body.innerHTML <- "<style>body{margin:0;background:red}</style>retry"
        let mutable first = true
        let mutable completions = 0
        let mutable releaseAttempts = 0
        let mutable forwardedReleases = 0

        check
            (window.SetIOSurfaceCompletion (fun finish ->
                completions <- completions + 1

                if failCompletion && first then
                    first <- false
                    invalidOp "test-only Metal completion failure"

                finish ()))
            "real Metal consumer"

        check
            (window.SetIOSurfaceFrameRelease (fun frame ->
                releaseAttempts <- releaseAttempts + 1

                if not failCompletion && first then
                    first <- false
                    -5
                else
                    forwardedReleases <- forwardedReleases + 1
                    RanvierWebCoreAbi.Native.rv_gpu_frame_release frame))
            "real native release"

        throws (fun error -> error :? InvalidOperationException) (fun () -> window.Present ()) "completion/release failure reaches the caller"
        let retained = window.PendingIOSurfaceFrame
        check (retained <> 0n) "actual acquired frame remains owned"
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

        check
            (wrongThread
             |> Option.exists (fun error -> error :? InvalidOperationException))
            "foreign disposal is rejected"

        check (window.PendingIOSurfaceFrame = retained) "foreign disposal preserves the lease"
        (window :> IDisposable).Dispose()
        check (window.PendingIOSurfaceFrame = 0n) "owner cleanup retries the frame before host destruction"
        check (completions = 2) "one failed delivery plus one cleanup completion"
        check (releaseAttempts = (if failCompletion then 1 else 2)) "completion failure never forwards release early"
        check (forwardedReleases = 1) "native release succeeds exactly once"
        (window :> IDisposable).Dispose()
