module Ranvier.View.WebCore.Tests.CpuStagingTests

open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

[<Tests>]
let dirtyPaintRegression =
    testCase "ordinary CPU edits preserve dirty painting" (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("dirty paint", 200, 150, hidden = true, renderer = "software")

            let host = window.Host

            host.Document.body.innerHTML <-
                "<div id='box' style='position:absolute;left:10px;top:10px;width:40px;height:40px;background:red'></div>"

            window.Step (timeout = 0) |> ignore
            let before = window.FullCpuPaints
            let dirtyBefore = window.DirtyCpuPaints
            let box = host.Document.getElementById "box"
            box.setAttribute ("style", "position:absolute;left:10px;top:10px;width:40px;height:40px;background:blue")
            window.Step (timeout = 0) |> ignore
            let deadline = System.Diagnostics.Stopwatch.StartNew ()

            while window.DirtyCpuPaints = dirtyBefore
                  && window.FullCpuPaints = before
                  && deadline.ElapsedMilliseconds < 1000L do
                System.Threading.Thread.Sleep 1
                window.Step (timeout = 0) |> ignore

            Expect.equal window.FullCpuPaints before "ordinary edit must not force full paint"
            Expect.isGreaterThan window.DirtyCpuPaints dirtyBefore "edit paints dirty region"
            box.setAttribute ("style", "display:none")
            window.Step (timeout = 0) |> ignore
            let struct (width, height, partial) = window.ReadPixels ()
            let reference = Array.zeroCreate<byte>(width * height * 4)
            host.Paint reference
            Expect.sequenceEqual partial reference "removed content clears old pixels"
            window.Present ()
            Expect.isGreaterThan window.FullCpuPaints before "explicit presentation retains full invalidation"))

[<Tests>]
let tests =
    testCase "CPU staging reuses buffers and traces upload independently" (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("CPU staging", 200, 150, hidden = true, renderer = "software")

            let host = window.Host
            host.SetSchedulingTrace true
            window.ReadPixels () |> ignore
            let struct (first, _) = host.DrainSchedulingTrace ()

            Expect.equal
                (first
                 |> Array.filter (fun event -> event.Phase = SchedulingPhase.CpuBufferCreate)
                 |> Array.length)
                1
                "initial staging allocation"

            Expect.isTrue
                (first
                 |> Array.exists (fun event ->
                     event.Phase = SchedulingPhase.CpuUpload
                     && event.Count = 200 * 150 * 4))
                "upload has byte count and separate timing"

            let element = host.Document.createElement "div"
            element.setAttribute ("style", "position:absolute;left:10px;top:10px;width:40px;height:40px;background:red")
            host.Document.body.appendChild element |> ignore
            host.Frame ()
            window.ReadPixels () |> ignore
            let struct (second, _) = host.DrainSchedulingTrace ()

            for event in Array.append first second do
                if
                    event.Phase = SchedulingPhase.CpuBufferCreate
                    || event.Phase = SchedulingPhase.CpuUpload
                then
                    printfn
                        "CPU_COST phase=%O bytes=%d milliseconds=%.4f"
                        event.Phase
                        event.Count
                        (float event.Duration * 1000.0
                         / float System.Diagnostics.Stopwatch.Frequency)

            Expect.isFalse
                (second
                 |> Array.exists (fun event -> event.Phase = SchedulingPhase.CpuBufferCreate))
                "same-size damage reuses backing and texture"

            Expect.isTrue
                (second
                 |> Array.exists (fun event -> event.Phase = SchedulingPhase.CpuUpload))
                "damage uploaded"

            element.setAttribute ("style", "display:none")
            host.Frame ()
            let struct (width, height, partial) = window.ReadPixels ()
            let full = Array.zeroCreate<byte>(width * height * 4)
            host.Paint full
            Expect.sequenceEqual partial full "removed content clears reused pixels"
            Expect.isEmpty (List.ofSeq host.Errors) "host errors"))
