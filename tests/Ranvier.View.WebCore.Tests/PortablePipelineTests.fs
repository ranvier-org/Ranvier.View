module Ranvier.View.WebCore.Tests.PortablePipelineTests

open System
open Expecto
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

[<Tests>]
let tests =
    testCase "portable GPU readiness preserves input across resize and two windows"
    <| fun () ->
        if OperatingSystem.IsWindows () then
            skiptest "EGL/Metal integration"

        onHostThread (fun () ->
            let renderer = if OperatingSystem.IsMacOS () then "metal" else "opengles2"

            use first =
                Window.Create ("pipeline one", 200, 150, hidden = true, renderer = renderer)

            use second =
                Window.Create ("pipeline two", 200, 150, hidden = true, renderer = renderer)

            if not (WebCoreHost.Has "gpu-frame-pipeline-v1") then
                if Environment.GetEnvironmentVariable "RANVIER_REQUIRE_PIPELINE" = "1" then
                    failtest "qualified runtime must expose the GPU pipeline"

                skiptest "requires platform GPU pipeline"

            for window in [ first; second ] do
                window.GpuFramePipeline <- true
                Expect.equal (RanvierWebCoreAbi.Native.rv_host_set_accelerated_compositing (window.Host.Handle, 1)) 0 "compositing"
                window.Host.Document.body.innerHTML <- "<style>body{background:red;transform:translateZ(0)}</style><input id='input'>"

            let input = first.Host.Document.getElementById "input" :?> HTMLInputElement
            input.focus ()
            first.Host.SetFocused true
            let timer = Diagnostics.Stopwatch.StartNew ()

            while (first.GpuFramesPresented < 2L
                   || second.GpuFramesPresented < 2L)
                  && timer.ElapsedMilliseconds < 5000L do
                first.Host.Type ("a", "KeyA", "a")
                first.Host.Resize (220, 160)
                first.Host.SetDeviceScale 1.5f
                first.Step 2 |> ignore
                second.Present ()
                second.Step 2 |> ignore

            Expect.isGreaterThan first.GpuFramesPresented 1L "first window completed frames"
            Expect.isGreaterThan second.GpuFramesPresented 1L "second window completed frames"
            Expect.isGreaterThan input.value.Length 0 "input progressed while readiness was polled"
            Expect.equal (first.CpuFramesPresented, second.CpuFramesPresented) (0L, 0L) "no congestion fallback"
            Expect.isTrue first.GpuFramePipeline "pipeline remains enabled"
            first.GpuFramePipeline <- false
            first.Present () // Explicit fallback settles a retained Metal frame on owner.
            Expect.isEmpty (List.ofSeq first.Host.Errors) "no input errors")
