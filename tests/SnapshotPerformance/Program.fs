module SnapshotPerformance

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

[<EntryPoint>]
let main args =
    let width, height = int args[0], int args[1]
    let fresh = args[2] = "fresh"
    let output = args[3]
    use host = WebCoreHost.Create (width, height)
    host.Document.body.innerHTML <- "<div style='position:fixed;inset:0;background:linear-gradient(45deg,red,blue)'>Snapshot transfer</div>"
    host.Frame ()
    use service = BackgroundSnapshots.forHost host
    let samples = ResizeArray<obj>()

    for index in 0..31 do
        if fresh then
            host.Frame ()

        let mutable doneCapture = false
        let mutable resultBytes = 0
        let allocated = GC.GetTotalAllocatedBytes true
        let started = Stopwatch.GetTimestamp ()

        use operation =
            service.Capture (
                SnapshotSubject.Viewport,
                { SnapshotContract.defaults with
                    Encoding = ImageEncoding.Png
                },
                fun outcome ->
                    match outcome with
                    | ResourceOutcome.Succeeded image ->
                        if image.Data.Length < 8 || image.Data[0] <> 137uy then
                            failwith "invalid PNG"

                        resultBytes <- image.Data.Length
                        doneCapture <- true
                    | other -> failwithf "capture failed: %A" other
            )

        let ownerMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds

        while not doneCapture
              && Stopwatch.GetElapsedTime(started).TotalSeconds < 30. do
            host.Cycle ()
            Thread.Sleep 1

        if not doneCapture then
            failwith "capture timed out"

        let completionMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds
        let bytes = GC.GetTotalAllocatedBytes true - allocated

        if index >= 2 then
            samples.Add (
                box
                    {|
                        ownerMs = ownerMs
                        completionMs = completionMs
                        allocatedBytes = bytes
                        resultBytes = resultBytes
                    |}
            )

    let result =
        {|
            width = width
            height = height
            fresh = fresh
            nativePixels = WebCoreHost.Has "snapshot-pixels-v1"
            managedAssembly = typeof<WebCoreHost>.Assembly.GetName().Version.ToString()
            samples = samples.ToArray ()
        |}

    File.WriteAllText (output, JsonSerializer.Serialize result)
    0
