module Ranvier.View.WebCore.Tests.LifecycleChecks

#nowarn "9"
open System
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

let mutable private assertions = 0

let private check condition message =
    assertions <- assertions + 1

    if not condition then
        failwith message

let private collect () =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()

let private failed action =
    try
        action ()
        null
    with error ->
        error

let private inputAdmission () =
    use host = WebCoreHost.Create (80, 60)
    let sample = PointerSample.Create (0, 1, 0, 10.0, 10.0)

    let operations: (unit -> unit) array =
        [|
            (fun () -> host.Mouse (0, 10, 10, -1, 0, 0) |> ignore)
            (fun () -> host.Click (10, 10) |> ignore)
            (fun () -> host.Wheel (10, 10, 0.0f, 1.0f, 0) |> ignore)
            (fun () ->
                host.Key (0, "a", "KeyA", null, 0, false)
                |> ignore)
            (fun () -> host.Type ("a", "KeyA", "a"))
            (fun () -> host.Pointer [| sample |] |> ignore)
            (fun () -> host.Frame ())
            (fun () -> host.SetDeviceScale 1.0f)
        |]

    let failures = ResizeArray<exn>()

    let requester =
        Thread (
            ThreadStart (fun () ->
                for operation in operations do
                    failures.Add (failed operation))
        )

    requester.Start ()
    check (requester.Join 5000) "foreign input thread terminated"
    check (failures.Count = operations.Length) "all owner admissions executed"

    for error in failures do
        check (error :? InvalidOperationException) "foreign input rejected before native call"

    (host :> IDisposable).Dispose()

    for operation in operations do
        check (failed operation :? ObjectDisposedException) "disposed input rejected"

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private queuedPayload (host: WebCoreHost) kind =
    let payload = obj ()
    let weak = WeakReference payload

    let keep () =
        GC.KeepAlive payload

    match kind with
    | "timer" -> host.SetTimeout (keep, 1000000.0) |> ignore
    | "frame" ->
        host.RequestAnimationFrame (fun _ -> keep ())
        |> ignore
    | "microtask" -> host.QueueMicrotask keep
    | "idle" ->
        host.RequestIdleCallback (fun _ -> keep ())
        |> ignore
    | _ -> SynchronizationContext.Current.Post ((fun _ -> keep ()), null)

    weak

let private scheduling () =
    for kind in [| "timer"; "frame"; "microtask"; "posted" |] do
        use host = WebCoreHost.Create (80, 60)
        let trace = ResizeArray<string>()

        let first () =
            trace.Add "first"
            (host :> IDisposable).Dispose()

        let later () =
            trace.Add "later"

        match kind with
        | "timer" ->
            host.SetTimeout (first, 0.0) |> ignore
            host.SetTimeout (later, 0.0) |> ignore
        | "frame" ->
            host.RequestAnimationFrame (fun _ -> first ())
            |> ignore

            host.RequestAnimationFrame (fun _ -> later ())
            |> ignore
        | "microtask" ->
            host.QueueMicrotask first
            host.QueueMicrotask later
        | _ ->
            SynchronizationContext.Current.Post ((fun _ -> first ()), null)
            SynchronizationContext.Current.Post ((fun _ -> later ()), null)

        if kind = "frame" then host.Frame () else host.Cycle ()

        check
            (trace.ToArray () = [| "first" |])
            (kind
             + " suppresses later work after callback disposal")

    do
        use host = WebCoreHost.Create (80, 60)
        let trace = ResizeArray<int>()
        let mutable second = 0

        host.RequestAnimationFrame (fun _ ->
            trace.Add 1
            host.CancelAnimationFrame second)
        |> ignore

        second <- host.RequestAnimationFrame (fun _ -> trace.Add 2)
        host.Frame ()
        check (trace.ToArray () = [| 1 |]) "RAF cancellation suppresses undelivered callback"

    do
        let host = WebCoreHost.Create (80, 60)

        let weak =
            [|
                for kind in [| "timer"; "frame"; "microtask"; "idle"; "posted" |] -> queuedPayload host kind
            |]

        (host :> IDisposable).Dispose()
        collect ()

        for item in weak do
            check (not item.IsAlive) "disposed retained host releases queued payload"

        GC.KeepAlive host

let private windowDisposal () =
    do
        let window =
            Window.Create ("AOT frame cleanup", 80, 60, hidden = true, renderer = "software")

        let id = SDL.SDL3.SDL_GetWindowID (NativePtr.ofNativeInt window.SdlWindow)

        window.Host.RequestAnimationFrame (fun _ -> (window :> IDisposable).Dispose())
        |> ignore

        window.Present ()
        check (NativePtr.isNullPtr (SDL.SDL3.SDL_GetWindowFromID id)) "frame callback closes actual SDL window"
        (window :> IDisposable).Dispose()

    do
        let window =
            Window.Create ("AOT direct host cleanup", 80, 60, hidden = true, renderer = "software")

        let id = SDL.SDL3.SDL_GetWindowID (NativePtr.ofNativeInt window.SdlWindow)
        (window.Host :> IDisposable).Dispose()
        (window :> IDisposable).Dispose()
        (window :> IDisposable).Dispose()

        check (NativePtr.isNullPtr (SDL.SDL3.SDL_GetWindowFromID id)) "direct host closure followed by repeated Window cleanup"

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private pickerPayload host dispose =
    let payload = obj ()
    let weak = WeakReference payload
    let registration = FilePickers.install host (fun _ -> GC.KeepAlive payload)

    if dispose then
        registration.Dispose ()

    weak, registration

let private pickers () =
    check (WebCoreHost.Has "file-picker") "actual native picker capability"

    do
        let host = WebCoreHost.Create (320, 240)
        let registrations = [| for _ in 1..4 -> pickerPayload host true |]
        collect ()

        for weak, _ in registrations do
            check (not weak.IsAlive) "disposed registration releases provider while retained"

        let weak, active = pickerPayload host false
        (host :> IDisposable).Dispose()
        collect ()
        check (not weak.IsAlive) "host closure releases active provider"
        GC.KeepAlive active
        GC.KeepAlive registrations
        GC.KeepAlive host

    do
        use host = WebCoreHost.Create (320, 240)
        let mutable registration: IDisposable = null
        let mutable replacement: IDisposable = null
        let mutable opened = false
        let mutable invalidated = 0

        registration <-
            FilePickers.install host (function
                | Show _ -> opened <- true
                | Invalidate _ ->
                    invalidated <- invalidated + 1
                    registration.Dispose ()
                    replacement <- FilePickers.install host ignore)

        let input = host.Document.createElement "input"
        input.setAttribute ("type", "file")
        host.Document.body.appendChild input |> ignore
        host.Cycle ()
        let rect = input.getBoundingClientRect ()

        host.Click (int (rect.x + rect.width / 2.0), int (rect.y + rect.height / 2.0))
        |> ignore

        host.Cycle ()
        check opened "real native file request delivered"
        registration.Dispose ()
        check (invalidated = 1 && not (isNull replacement)) "invalidation reentrant disposal and replacement"
        replacement.Dispose ()

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private resizePayload (service: ResizeObservationService) =
    let payload = obj ()
    let weak = WeakReference payload
    let observer = service.Create (fun _ -> GC.KeepAlive payload)
    observer.Dispose ()
    weak, observer

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private intersectionPayload (service: IntersectionObservationService) =
    let payload = obj ()
    let weak = WeakReference payload

    let observer =
        service.Create ((fun _ -> GC.KeepAlive payload), IntersectionObserverInit.Default)

    observer.Dispose ()
    weak, observer

let private observations () =
    use host = WebCoreHost.Create (320, 240)

    check
        (WebCoreHost.Has "resize-entries"
         && WebCoreHost.Has "intersection-entries")
        "actual observation capabilities"

    let resize = ResizeObservation.forHost host
    let intersection = IntersectionObservation.forHost host
    let resized = [| for _ in 1..4 -> resizePayload resize |]
    let intersected = [| for _ in 1..4 -> intersectionPayload intersection |]
    collect ()

    for weak, _ in resized do
        check (not weak.IsAlive) "disposed resize observer releases payload while retained"

    for weak, _ in intersected do
        check (not weak.IsAlive) "disposed intersection observer releases payload while retained"

    let target = host.Document.createElement "div" :?> HTMLElement
    target.style.cssText <- "width:40px;height:40px"
    host.Document.body.appendChild target |> ignore
    let mutable resizeFirst: ResizeObserver option = None
    let mutable resizeNext: ResizeObserver option = None
    let mutable intersectionFirst: IntersectionObserver option = None
    let mutable intersectionNext: IntersectionObserver option = None
    let mutable resizeCalls = 0
    let mutable intersectionCalls = 0
    let mutable resizeReplacement = 0
    let mutable intersectionReplacement = 0

    resizeFirst <-
        Some (
            resize.Create (fun _ ->
                resizeCalls <- resizeCalls + 1
                resizeFirst.Value.Dispose ()
                collect ()
                let next = resize.Create (fun _ -> resizeReplacement <- resizeReplacement + 1)
                resizeNext <- Some next
                next.Observe target)
        )

    intersectionFirst <-
        Some (
            intersection.Create (
                (fun _ ->
                    intersectionCalls <- intersectionCalls + 1
                    intersectionFirst.Value.Dispose ()
                    collect ()

                    let next =
                        intersection.Create ((fun _ -> intersectionReplacement <- intersectionReplacement + 1), IntersectionObserverInit.Default)

                    intersectionNext <- Some next
                    next.Observe target),
                IntersectionObserverInit.Default
            )
        )

    resizeFirst.Value.Observe target
    intersectionFirst.Value.Observe target

    for _ in 1..8 do
        host.Frame ()
        host.Cycle ()

    check (resizeCalls = 1 && intersectionCalls = 1) "each original observer self-disposed exactly once"

    check
        (resizeReplacement > 0
         && intersectionReplacement > 0)
        "replacement observers deliver after GC in original callback"

    resizeNext
    |> Option.iter (fun value -> value.Dispose ())

    intersectionNext
    |> Option.iter (fun value -> value.Dispose ())

    resize.Dispose ()
    intersection.Dispose ()
    GC.KeepAlive resized
    GC.KeepAlive intersected
    GC.KeepAlive host

let run () =
    assertions <- 0
    inputAdmission ()
    scheduling ()
    windowDisposal ()
    pickers ()
    observations ()

    Console.WriteLine (
        "COMMON_LIFECYCLE_ASSERTIONS="
        + assertions.ToString ()
    )

    assertions
