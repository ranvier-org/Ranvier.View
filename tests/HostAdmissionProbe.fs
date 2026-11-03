module internal HostAdmissionProbe

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

let private operation name (host: WebCoreHost) =
    let pixels = Array.zeroCreate<byte>(160 * 120 * 4)

    match name with
    | "frame" -> fun () -> host.Frame ()
    | "resize" -> fun () -> host.Resize (160, 120)
    | "screen" -> fun () -> host.SetScreen (160, 120, 0, 0, 160, 120, 24, enum<ScreenOrientationType> 0)
    | "deviceScale" -> fun () -> host.SetDeviceScale (1.0f)
    | "paintSize" -> fun () -> host.PaintSize |> ignore
    | "paint" -> fun () -> host.Paint (pixels)
    | "dirtyRect" -> fun () -> host.DirtyRect |> ignore
    | "paintDirty" -> fun () -> host.PaintDirty (pixels) |> ignore
    | "touchPoints" -> fun () -> host.SetMaxTouchPoints (2)
    | "focused" -> fun () -> host.SetFocused (true)
    | "visible" -> fun () -> host.SetVisible (false)
    | "cursor" -> fun () -> host.Cursor |> ignore
    | "compositionRect" -> fun () -> host.CompositionRect |> ignore
    | "clipboard" -> fun () -> host.SetClipboard ((fun _ _ _ -> ()), (fun () -> Seq.empty))
    | "cycle" -> fun () -> host.Cycle ()
    | "microtask" -> fun () -> host.QueueMicrotask (ignore)
    | "runMicrotasks" -> fun () -> host.RunMicrotasks ()
    | "timer" -> fun () -> host.SetTimeout (ignore, 0.0) |> ignore
    | "clearTimer" -> fun () -> host.ClearTimeout (0)
    | "animationFrame" -> fun () -> host.RequestAnimationFrame (ignore) |> ignore
    | "cancelAnimationFrame" -> fun () -> host.CancelAnimationFrame (0)
    | "idleCallback" -> fun () -> host.RequestIdleCallback (ignore) |> ignore
    | "cancelIdleCallback" -> fun () -> host.CancelIdleCallback (0)
    | "trace" -> fun () -> host.SetRenderingTrace (false)
    | "drainTrace" -> fun () -> host.DrainRenderingTrace () |> ignore
    | "errorRecording" -> fun () -> host.SetErrorRecording (false)
    | "takeError" -> fun () -> host.TakeHostError () |> ignore
    | "capability" -> fun () -> WebCoreHost.Has ("pointer-input") |> ignore
    | value -> invalidArg (nameof name) value

let private capture call =
    try
        call ()
        "returned"
    with error ->
        error.GetType().FullName

[<EntryPoint>]
let main args =
    let name, phase = args[0], args[1]
    use host = WebCoreHost.Create (160, 120)
    let call = operation name host
    Console.WriteLine ("ADMISSION_BEGIN " + name + " " + phase)

    let outcome =
        match phase with
        | "healthy" -> capture call
        | "disposed" ->
            (host :> IDisposable).Dispose()
            capture call
        | "wrongThread" ->
            let mutable result = "not-run"
            let worker = Thread (ThreadStart (fun () -> result <- capture call))
            worker.Start ()

            if not (worker.Join (TimeSpan.FromSeconds (5.0))) then
                failwith "Wrong-thread call did not finish"

            result
        | "frameCallbackDispose" ->
            host.RequestAnimationFrame (fun _ -> (host :> IDisposable).Dispose())
            |> ignore

            capture host.Frame
        | value -> invalidArg "phase" value

    Console.WriteLine (
        "ADMISSION_RESULT "
        + name
        + " "
        + phase
        + " "
        + outcome
    )

    0
