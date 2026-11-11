module Responsiveness.Program

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Responsiveness.SemanticPublication
open Responsiveness.EditorViewport

let option name fallback (args: string array) =
    match Array.tryFindIndex ((=) name) args with
    | Some i when i + 1 < args.Length -> args[i + 1]
    | _ -> fallback

let percentile quantile (values: int64 array) =
    if values.Length = 0 then
        0.0
    else
        Array.sortInPlace values

        float values[min (values.Length - 1) (int (ceil (quantile * float values.Length)) - 1)]
        * 1000.0
        / float Stopwatch.Frequency

let sample scenario seconds operations lines unbounded visible renderer tracing interactive pipeline boundedIme output repetition =
    use window =
        Window.Create ("Ranvier responsiveness", 800, 600, hidden = not visible, ?renderer = renderer)

    if interactive then
        window.InteractiveBudget <- Some OwnerTurnBudget.Default

    window.GpuFramePipeline <- pipeline
    window.BoundedImeContext <- boundedIme

    let host = window.Host
    host.Document.body.innerHTML <- "<textarea id='input' style='width:700px;height:80px'></textarea><div id='lines'></div>"
    let input = host.Document.getElementById "input" :?> HTMLTextAreaElement
    let mutable inputDocumentOrigin = 0L

    let trimInput () =
        let text = input.value

        if text.Length > 256 then
            let mutable removed = text.Length - 128

            if
                Char.IsLowSurrogate text[removed]
                && Char.IsHighSurrogate text[removed - 1]
            then
                removed <- removed + 1

            if int input.selectionStart >= removed then
                let start = int input.selectionStart - removed
                let finish = int input.selectionEnd - removed
                let direction = input.selectionDirection
                input.value <- text.Substring removed
                input.setSelectionRange (uint32 start, uint32 finish, direction)
                inputDocumentOrigin <- inputDocumentOrigin + int64 removed

    let viewport = host.Document.getElementById "lines" :?> HTMLElement

    let document =
        if scenario = "document" then
            Some (Responsiveness.DocumentModel.DocumentBuffer (String.concat "\n" (Array.init lines (fun i -> $"line {i}: let value = {i}"))))
        else
            None

    use editor =
        new EditorViewport (host, viewport, lines, unbounded = unbounded, textKind = scenario, ?document = document)

    let phases = Frames.layoutForHost host
    let count = editor.Count
    input.focus ()
    host.SetFocused true

    for _ in 1..10 do
        window.Step (timeout = 0) |> ignore

    host.SetSchedulingTrace tracing

    if WebCoreHost.Has "host-tracing" then
        host.SetRenderingTrace tracing

    let events = ResizeArray<SchedulingEvent>()
    let turnDurations = ResizeArray<int64>()
    let mutable dropped = 0L
    let mutable nativeDropped = 0UL
    let nativeEvents = ResizeArray<obj>()
    let allocated = GC.GetTotalAllocatedBytes false
    let context = SynchronizationContext.Current

    let mutable revision =
        {
            DocumentId = Guid.NewGuid ()
            TextRevision = 0L
            ProjectRevision = 0L
            RequestId = 0L
        }

    let mutable visibleColor = ""

    use publication =
        new LatestPublication<Result> (
            host,
            (fun result ->
                if
                    isCurrent revision result
                    && visibleColor <> result.VisibleColor
                then
                    visibleColor <- result.VisibleColor
                    viewport.style.setProperty ("color", visibleColor, ""))
        )

    let mutable cancellation = new CancellationTokenSource ()

    use cancelOnExit =
        { new IDisposable with
            member _.Dispose() =
                cancellation.Cancel ()
                cancellation.Dispose ()
        }

    let mutable worker = Tasks.Task.CompletedTask
    let clock = Stopwatch.StartNew ()
    let mutable iteration = 0

    while (if operations > 0 then
               iteration < operations
           else
               clock.Elapsed.TotalSeconds < seconds) do
        let started = Stopwatch.GetTimestamp ()

        match scenario with
        | "document"
        | "typing" ->
            trimInput ()

            host.Type ("a", "KeyA", "a")
            editor.Edit input.value
        | "ime" ->
            editor.PinComposition ()
            host.SetComposition ("日本", 2, 2)
            editor.MoveTo (50 + iteration % max 1 (lines - 125))

            if iteration % 2 = 0 then
                host.CancelComposition ()
            else
                host.CommitComposition "日本"

            editor.EndComposition ()
            editor.Edit input.value

            trimInput ()
        | "scroll"
        | "long-line"
        | "bidi" -> editor.MoveTo (50 + iteration % max 1 (lines - 125))
        | "posted" ->
            for i in 1..1000 do
                let value = i
                context.Post (SendOrPostCallback (fun _ -> viewport.setAttribute ("data-result", string value)), null)
        | "publication" ->
            // Bound worker concurrency as well as owner delivery; workers see only detached data.
            if worker.IsCompleted then
                worker.GetAwaiter().GetResult()
                cancellation.Cancel ()
                cancellation.Dispose ()
                cancellation <- new CancellationTokenSource ()

                revision <-
                    { revision with
                        RequestId = revision.RequestId + 1L
                    }

                let captured = revision
                let token = cancellation.Token

                worker <-
                    Tasks.Task.Run (fun () ->
                        for i in 1..1000 do
                            if not token.IsCancellationRequested then
                                publication.Publish
                                    {
                                        Revision = captured
                                        VisibleColor = if i % 2 = 0 then "#225588" else "#884422"
                                    }
                                |> ignore)
        | "resize" ->
            host.Resize (800 + iteration % 2, 600)
            editor.Configure (700 + iteration % 2, 500, 1., false)
        | "idle" -> ()
        | other -> invalidArg "scenario" other

        editor.Measure ()

        if boundedIme then
            ImeContext.read host 4096 |> ignore

        phases.Present (fun () -> window.Step (timeout = 0) |> ignore)
        turnDurations.Add (Stopwatch.GetTimestamp () - started)
        let struct (batch, lost) = host.DrainSchedulingTrace ()
        dropped <- dropped + lost

        if events.Count + batch.Length <= 500000 then
            events.AddRange batch
        else
            dropped <- dropped + int64 batch.Length

        if tracing && WebCoreHost.Has "host-tracing" then
            let native, lostNative = host.DrainRenderingTrace ()
            nativeDropped <- nativeDropped + lostNative

            if nativeEvents.Count + native.Length <= 500000 then
                nativeEvents.AddRange (native |> Array.map box)
            else
                nativeDropped <- nativeDropped + uint64 native.Length

        iteration <- iteration + 1

        let remaining =
            8
            - int (Stopwatch.GetElapsedTime(started).TotalMilliseconds)

        if remaining > 0 then
            Thread.Sleep remaining

    cancellation.Cancel ()
    worker.GetAwaiter().GetResult()
    let allocatedBytes = GC.GetTotalAllocatedBytes false - allocated

    let nativeName =
        if OperatingSystem.IsWindows () then
            "RanvierWebCoreHost.dll"
        elif OperatingSystem.IsMacOS () then
            "libRanvierWebCoreHost.dylib"
        else
            "libRanvierWebCoreHost.so"

    let runtime =
        Path.Combine (AppContext.BaseDirectory, "runtimes", Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, "native", nativeName)

    let hash =
        if File.Exists runtime then
            Convert.ToHexString (SHA256.HashData (File.ReadAllBytes runtime))
        else
            "platform runtime: see package receipt"

    let stats =
        {|
            schemaVersion = 1
            benchmarkProtocol = "owner-turn-v1"
            machineId = Environment.MachineName
            sourceCommit = Environment.GetEnvironmentVariable "RANVIER_BENCH_COMMIT"
            pendingWork =
                if scenario = "posted" || scenario = "publication" then
                    -1
                else
                    0
            scenario = scenario
            repetition = repetition
            seconds = seconds
            logicalLines = lines
            liveLines = count
            measurementCacheEntries = editor.CacheCount
            hidden = not visible
            tracing = tracing
            interactive = interactive
            gpuFramePipeline = pipeline
            boundedImeContext = boundedIme
            gpuPipelineSupported = WebCoreHost.Has "gpu-frame-pipeline-v1"
            imeWindowSupported = WebCoreHost.Has "ime-surrounding-window-v1"
            nativeAbi = RanvierWebCoreAbi.Native.rv_abi_version ()
            inputDocumentOrigin = inputDocumentOrigin
            inputDocumentCursor =
                inputDocumentOrigin
                + int64 (
                    if input.selectionDirection = "backward" then
                        input.selectionStart
                    else
                        input.selectionEnd
                )
            requestedRenderer = defaultArg renderer "default"
            stopwatchFrequency = Stopwatch.Frequency
            runtimeHash = hash
            os = Environment.OSVersion.ToString ()
            managedVersion = typeof<WebCoreHost>.Assembly.GetName().Version.ToString()
            iterations = iteration
            allocatedBytes = allocatedBytes
            dropped = dropped
            nativeDropped = nativeDropped
            gpuFrames = window.GpuFramesPresented
            cpuFrames = window.CpuFramesPresented
            fullCpuPaints = window.FullCpuPaints
            dirtyCpuPaints = window.DirtyCpuPaints
            turnP50Ms = percentile 0.50 (turnDurations.ToArray ())
            turnP95Ms = percentile 0.95 (turnDurations.ToArray ())
            turnP99Ms = percentile 0.99 (turnDurations.ToArray ())
            maximumTurnMs = percentile 1.0 (turnDurations.ToArray ())
            qualification =
                "Synthetic local workload; not OS input-to-photon or display-refresh qualification. Native timestamps remain in their own clock domain."
        |}

    let prefix = Path.Combine (output, $"{scenario}-{repetition}")
    let options = JsonSerializerOptions (IncludeFields = true)
    File.WriteAllText (prefix + ".json", JsonSerializer.Serialize (stats, options))
    File.WriteAllText (prefix + "-turns.json", JsonSerializer.Serialize (turnDurations.ToArray (), options))
    File.WriteAllText (prefix + "-managed.json", JsonSerializer.Serialize (events, options))
    File.WriteAllText (prefix + "-native.json", JsonSerializer.Serialize (nativeEvents, options))
    printfn "%s" (JsonSerializer.Serialize stats)

    if host.Errors.Count > 0 then
        failwithf "Host errors: %A" (List.ofSeq host.Errors)

[<EntryPoint>]
let main args =
    let scenario = option "--scenario" "all" args

    let seconds =
        Double.Parse (option "--seconds" "60" args, Globalization.CultureInfo.InvariantCulture)

    let repetitions = int (option "--repetitions" "5" args)
    let operations = int (option "--iterations" "0" args)

    if operations < 0 then
        invalidArg "--iterations" "must be non-negative"

    let lines = int (option "--lines" "100000" args)

    if
        seconds <= 0.0
        || not (Double.IsFinite seconds)
        || repetitions < 1
        || lines < 1
    then
        invalidArg "args" "positive finite duration, repetitions and lines required"

    let output = option "--output" "artifacts/responsiveness" args
    Directory.CreateDirectory output |> ignore

    let renderer =
        match option "--renderer" "default" args with
        | "default" -> None
        | value -> Some value

    let scenarios =
        if scenario = "all" then
            [
                "idle"
                "typing"
                "scroll"
                "ime"
                "posted"
                "publication"
                "resize"
                "long-line"
                "bidi"
            ]
        else
            [ scenario ]

    for name in scenarios do
        for repetition in 1..repetitions do
            sample
                name
                seconds
                operations
                lines
                (Array.contains "--unbounded-dom" args)
                (Array.contains "--visible" args)
                renderer
                (not (Array.contains "--no-trace" args))
                (Array.contains "--interactive" args)
                (Array.contains "--gpu-pipeline" args)
                (Array.contains "--bounded-ime" args)
                output
                repetition

    0
