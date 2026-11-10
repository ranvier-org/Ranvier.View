module Ranvier.View.WebCore.Tests.SwapChainTests

open System
open System.Threading
open Expecto
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

let private red =
    Component.define (fun _ (count: Signal<int>) -> count) (fun t ->
        t.div
            [|
                t.Attrs.attribute ("style", "position:absolute;left:0;top:0;width:100px;height:100px;background:rgb(255,0,0)")
            |]
            [| t.text (fun (count: Signal<int>) -> string count.Value) |])

let private chainOf (window: Window) =
    match window.SwapChain with
    | Some chain -> chain
    | None when Environment.GetEnvironmentVariable "RANVIER_REQUIRE_SWAP_CHAIN" = "1" ->
        failtest "RANVIER_REQUIRE_SWAP_CHAIN is set, and this window has no D3D11 swap chain"
    | None -> skiptest "the machine has no D3D11 hardware device or runtime presentation"

// A hidden window on its default path, with a red square mounted and shown.
let private chainCase (name: string) (body: Window -> D3D11SwapChain -> Signal<int> -> unit) =
    testCase name (fun () ->
        if not (OperatingSystem.IsWindows ()) then
            skiptest "swap chains are Windows only"

        onHostThread (fun () ->
            use window = Window.Create ("Ranvier swap chain", 200, 150, hidden = true)
            let chain = chainOf window
            use graph = new Graph ()
            let count = graph.Run (fun () -> Api.createSignal 0)
            use _mounted = mount window.Host graph red count
            window.Present ()
            body window chain count
            Expect.isEmpty (List.ofSeq window.Host.Errors) "listener, microtask and timer errors"))

let private redAt (struct (width: int, _: int, bytes: byte array)) x y =
    let at = (y * width + x) * 4
    bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]

let private blocking (milliseconds: int) (released: ManualResetEventSlim) =
    fun (present: unit -> int) ->
        Thread.Sleep milliseconds
        let result = present ()
        released.Set ()
        result

// Steps until the window has no present running.
let private stepUntilIdle (window: Window) (chain: D3D11SwapChain) =
    let elapsed = Diagnostics.Stopwatch.StartNew ()

    while chain.Presenting
          && elapsed.ElapsedMilliseconds < 5000L do
        window.Step 1 |> ignore

    Expect.isFalse chain.Presenting "the present finishes"

[<Tests>]
let tests =
    testSequenced
    <| testList
        "the D3D11 swap chain window"
        [
            chainCase "the frame pipeline replaces a pending frame after direct host resize" (fun window chain _ ->
                if not (WebCoreHost.Has "gpu-frame-pipeline-v1") then
                    skiptest "requires native GPU pipeline"

                stepUntilIdle window chain
                window.GpuFramePipeline <- true

                let flags =
                    Reflection.BindingFlags.Instance
                    ||| Reflection.BindingFlags.NonPublic

                let presenter = typeof<Window>.GetField("presenter", flags).GetValue window

                let set name value =
                    presenter.GetType().GetField(name, flags).SetValue(presenter, value)

                Expect.equal (OptionalNative.rv_host_configure_gpu_pipeline (window.Host.Handle, chain.Device)) 0 "configure"
                Expect.equal (OptionalNative.rv_host_submit_gpu_frame (window.Host.Handle, 100UL)) 0 "accepted native frame"
                // Reproduce the accepted-but-not-yet-acquired interval independently of GPU speed.
                set "pipelineId" (box 100UL)
                set "pipelinePending" (box true)
                set "pipelineNeedsSubmit" (box false)
                let size = presenter.GetType().GetField("pipelineSize", flags)

                if not (isNull size) then
                    size.SetValue (presenter, box window.Host.PaintSize)

                window.Host.Resize (310, 230)
                window.Host.SetDeviceScale 1.5f
                let before = window.GpuFramesPresented
                let timer = Diagnostics.Stopwatch.StartNew ()

                while window.GpuFramesPresented = before
                      && timer.ElapsedMilliseconds < 5000L do
                    window.Step 2 |> ignore

                Expect.isGreaterThan window.GpuFramesPresented before "discarded completion is replaced"
                stepUntilIdle window chain
                Expect.equal window.CpuFramesPresented 0L "resize stays on GPU")
            chainCase "the frame pipeline explicitly presents an unchanged page" (fun window chain _ ->
                if not (WebCoreHost.Has "gpu-frame-pipeline-v1") then
                    skiptest "requires native GPU pipeline"

                stepUntilIdle window chain
                window.GpuFramePipeline <- true

                let waitForFrame before =
                    let timer = Diagnostics.Stopwatch.StartNew ()

                    while window.GpuFramesPresented = before
                          && timer.ElapsedMilliseconds < 5000L do
                        window.Step 2 |> ignore

                    Expect.isGreaterThan window.GpuFramesPresented before "a GPU frame is submitted"
                    stepUntilIdle window chain

                waitForFrame window.GpuFramesPresented
                let before = window.GpuFramesPresented
                window.Present ()
                waitForFrame before
                Expect.equal window.CpuFramesPresented 0L "explicit present remains on GPU")
            chainCase "the frame pipeline recovers after resize and device loss" (fun window chain _ ->
                if not (WebCoreHost.Has "gpu-frame-pipeline-v1") then
                    skiptest "requires native GPU pipeline"

                stepUntilIdle window chain
                window.GpuFramePipeline <- true

                let waitForFrame before =
                    let timer = Diagnostics.Stopwatch.StartNew ()

                    while window.GpuFramesPresented = before
                          && timer.ElapsedMilliseconds < 5000L do
                        window.Step 2 |> ignore

                    Expect.isGreaterThan window.GpuFramesPresented before "pipeline resumes"
                    stepUntilIdle window chain

                waitForFrame window.GpuFramesPresented

                SDL3.SDL_SetWindowSize (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow, 300, 220)
                |> ignore

                waitForFrame window.GpuFramesPresented
                let struct (width, height, _) = window.ReadPixels ()
                Expect.equal (width, height) (300, 220) "new dimensions reach presentation"
                chain.SetPresentWrapper (fun _ -> 0x887A0005)
                let beforeLoss = window.GpuFramesPresented
                window.Present ()
                waitForFrame beforeLoss
                chain.SetPresentWrapper Unchecked.defaultof<_>
                waitForFrame window.GpuFramesPresented
                Expect.equal (redAt (window.ReadPixels ()) 90 90) (255uy, 0uy, 0uy, 255uy) "recovered page"
                Expect.equal window.CpuFramesPresented 0L "recovery remains on GPU")
            chainCase "the frame pipeline defers a held present without CPU fallback" (fun window chain _ ->
                if not (WebCoreHost.Has "gpu-frame-pipeline-v1") then
                    skiptest "requires native GPU pipeline"

                stepUntilIdle window chain
                window.GpuFramePipeline <- true
                let cpu = window.CpuFramesPresented
                let gpu = window.GpuFramesPresented
                use released = new ManualResetEventSlim (false)
                chain.SetPresentWrapper (blocking 200 released)
                let deadline = Diagnostics.Stopwatch.StartNew ()

                while not chain.Presenting
                      && deadline.ElapsedMilliseconds < 5000L do
                    window.Step 2 |> ignore

                Expect.isTrue chain.Presenting "a completed pipeline frame reaches the consumer"
                let call = Diagnostics.Stopwatch.StartNew ()
                window.Present ()
                Expect.isLessThan call.ElapsedMilliseconds 100L "owner does not wait for held present"
                Expect.equal window.CpuFramesPresented cpu "congestion never selects CPU"
                stepUntilIdle window chain
                chain.SetPresentWrapper (fun present -> present ())
                Expect.isGreaterThan window.GpuFramesPresented gpu "pipeline presents a GPU frame"
                Expect.equal (redAt (window.ReadPixels ()) 10 10) (255uy, 0uy, 0uy, 255uy) "retained pixels")
            testCase "the software renderer keeps SDL's renderer"
            <| fun () ->
                onHostThread (fun () ->
                    use window =
                        Window.Create ("Ranvier software", 80, 60, hidden = true, renderer = "software")

                    Expect.isNone window.SwapChain "the software renderer draws through SDL")

            testCase "the direct3d11 renderer name uses the swap chain"
            <| fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "swap chains are Windows only"

                onHostThread (fun () ->
                    use window =
                        Window.Create ("Ranvier named", 80, 60, hidden = true, renderer = "direct3d11")

                    chainOf window |> ignore)

            chainCase "a composited frame reads back opaque from the shared texture"
            <| fun window chain _ ->
                let image = window.ReadPixels ()
                let struct (width, height, _) = image
                Expect.isGreaterThan width 0 "the frame has a width"
                Expect.isGreaterThan height 0 "the frame has a height"
                Expect.equal (redAt image 90 90) (255uy, 0uy, 0uy, 255uy) "the square is on screen"
                Expect.equal (redAt image 150 120) (255uy, 255uy, 255uy, 255uy) "the page's background is on screen"
                Expect.isGreaterThan window.GpuFramesPresented 0L "the frame came from the GPU"
                Expect.equal window.CpuFramesPresented 0L "no frame took the CPU path"
                Expect.isFalse chain.Presenting "readback waits for the present"

            chainCase "a blocked present leaves the window's loop running and its frames being produced"
            <| fun window chain _ ->
                use released = new ManualResetEventSlim ()
                stepUntilIdle window chain
                chain.SetPresentWrapper (blocking 300 released)
                let before = window.GpuFramesPresented
                window.Present ()
                Expect.isTrue chain.Presenting "the present runs after Present returns"
                let mutable worstStep = 0.0
                let mutable framesDuringBlock = 0
                let scheduler = Frames.forHost window.Host

                let rec onFrame (_: float) =
                    if not released.IsSet then
                        framesDuringBlock <- framesDuringBlock + 1
                        scheduler.Frame onFrame |> ignore

                scheduler.Frame onFrame |> ignore

                while not released.IsSet do
                    let started = Diagnostics.Stopwatch.StartNew ()
                    window.Step 1 |> ignore
                    worstStep <- max worstStep started.Elapsed.TotalMilliseconds

                chain.SetPresentWrapper Unchecked.defaultof<_>
                stepUntilIdle window chain
                window.Step 0 |> ignore
                stepUntilIdle window chain
                Expect.isLessThan worstStep 150.0 "no step waits out the 300 ms present"
                Expect.isGreaterThan framesDuringBlock 0 "page frames run during the present"
                Expect.isGreaterThan window.GpuFramesPresented before "the frame produced during the present is shown afterwards"
                let image = window.ReadPixels ()
                Expect.equal (redAt image 90 90) (255uy, 0uy, 0uy, 255uy) "the page is intact afterwards"

            chainCase "presentation completion retains the submitted frame identity"
            <| fun window chain _ ->
                use released = new ManualResetEventSlim ()
                stepUntilIdle window chain
                window.Host.SetSchedulingTrace true
                chain.SetPresentWrapper (blocking 200 released)
                window.Present ()
                let struct (submitted, _) = window.Host.DrainSchedulingTrace ()

                let submission =
                    submitted
                    |> Array.find (fun item -> item.Phase = SchedulingPhase.Present)

                Expect.isTrue chain.Presenting "submission is outstanding"
                // The stall path can update the host while this submission is outstanding.
                window.Host.Frame ()
                Expect.isGreaterThan window.Host.SchedulingTrace.FrameId submission.FrameId "host advances without submitting"
                Expect.isTrue (released.Wait (TimeSpan.FromSeconds 5.0)) "worker completes"
                chain.SetPresentWrapper Unchecked.defaultof<_>
                window.Step 0 |> ignore
                let struct (completed, lost) = window.Host.DrainSchedulingTrace ()
                Expect.equal lost 0L "trace retained"

                let completion =
                    completed
                    |> Array.find (fun item -> item.Phase = SchedulingPhase.PresentCompleted)

                Expect.equal completion.FrameId submission.FrameId "completion belongs to the submitted frame"

            testCase "closing a window during a blocked present waits for it"
            <| fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "swap chains are Windows only"

                onHostThread (fun () ->
                    use released = new ManualResetEventSlim ()

                    (use window = Window.Create ("Ranvier swap chain", 200, 150, hidden = true)
                     let chain = chainOf window
                     chain.SetPresentWrapper (blocking 150 released)
                     window.Present ()
                     Expect.isTrue chain.Presenting "the present is running at disposal")

                    Expect.isTrue released.IsSet "disposal finishes after the present")

            chainCase "a resize during a blocked present reaches the next frame"
            <| fun window chain _ ->
                use released = new ManualResetEventSlim ()
                stepUntilIdle window chain
                chain.SetPresentWrapper (blocking 100 released)
                window.Present ()
                let struct (widthBefore, _, _) = window.ReadPixels ()
                chain.SetPresentWrapper (blocking 100 released)
                window.Present ()

                SDL3.SDL_SetWindowSize (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow, 300, 220)
                |> ignore

                stepUntilIdle window chain
                chain.SetPresentWrapper Unchecked.defaultof<_>
                window.Step 0 |> ignore
                window.Present ()
                stepUntilIdle window chain
                let image = window.ReadPixels ()
                let struct (widthAfter, heightAfter, _) = image
                Expect.isGreaterThan widthAfter widthBefore "the frame has the new width"
                Expect.isGreaterThan heightAfter 0 "the frame has a height"
                Expect.equal (redAt image 90 90) (255uy, 0uy, 0uy, 255uy) "the page is drawn at the new size"
                Expect.equal window.CpuFramesPresented 0L "the resize stays on the GPU path"

            chainCase "a present that loses the device rebuilds it and draws the page again"
            <| fun window chain _ ->
                stepUntilIdle window chain
                // DXGI_ERROR_DEVICE_REMOVED.
                chain.SetPresentWrapper (fun _ -> 0x887A0005)
                window.Present ()
                stepUntilIdle window chain
                chain.SetPresentWrapper Unchecked.defaultof<_>
                let before = window.GpuFramesPresented
                let elapsed = Diagnostics.Stopwatch.StartNew ()

                while window.GpuFramesPresented = before
                      && elapsed.ElapsedMilliseconds < 5000L do
                    window.Step 1 |> ignore

                stepUntilIdle window chain
                Expect.isGreaterThan window.GpuFramesPresented before "a frame follows the lost device without a page change"
                let image = window.ReadPixels ()
                Expect.equal (redAt image 90 90) (255uy, 0uy, 0uy, 255uy) "the rebuilt device shows the page"
                Expect.equal window.CpuFramesPresented 0L "the rebuilt device stays on the GPU path"

            chainCase "a failing present thread surfaces on the window's thread and the next frame still shows"
            <| fun window chain _ ->
                stepUntilIdle window chain
                chain.SetPresentWrapper (fun _ -> failwith "present failed")
                window.Present ()
                chain.WaitIdle ()
                chain.SetPresentWrapper Unchecked.defaultof<_>

                Expect.throwsT<InvalidOperationException> (fun () -> window.Step 0 |> ignore) "the failure reaches Step"

                window.Present ()
                stepUntilIdle window chain
                let image = window.ReadPixels ()
                Expect.equal (redAt image 90 90) (255uy, 0uy, 0uy, 255uy) "the page is shown after the failure"

            testCase "two windows present on their own threads"
            <| fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "swap chains are Windows only"

                onHostThread (fun () ->
                    use first = Window.Create ("Ranvier first", 200, 150, hidden = true)
                    use second = Window.Create ("Ranvier second", 200, 150, hidden = true)
                    let firstChain = chainOf first
                    let secondChain = chainOf second
                    first.Host.Document.body.setAttribute ("style", "background:rgb(255,0,0)")
                    second.Host.Document.body.setAttribute ("style", "background:rgb(255,0,0)")
                    use released = new ManualResetEventSlim ()
                    firstChain.SetPresentWrapper (blocking 100 released)
                    first.Present ()
                    second.Present ()
                    Expect.isTrue firstChain.Presenting "the first window's present is blocked"
                    stepUntilIdle second secondChain
                    Expect.isTrue firstChain.Presenting "the second window finishes during the first window's block"
                    Expect.equal (redAt (second.ReadPixels ()) 90 90) (255uy, 0uy, 0uy, 255uy) "the second window shows its page"
                    stepUntilIdle first firstChain
                    firstChain.SetPresentWrapper Unchecked.defaultof<_>
                    Expect.equal (redAt (first.ReadPixels ()) 90 90) (255uy, 0uy, 0uy, 255uy) "the first window shows its page")
        ]
