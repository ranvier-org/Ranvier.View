module internal NativeApiConformance.Native

open Ranvier.View.WebCore

let private runLocales () =
    let lifecycle = LocaleLifecycle.run ()
    let deterministic = LocalePreferences.deterministic ()
    use host = WebCoreHost.Create (16, 16)
    let count = LocalePreferences.run (fun () -> Locales.forHost host)
    let service = Locales.forHost host
    System.Console.WriteLine $"NATIVE_API H06 PASS assertions={count}"
    System.Console.WriteLine $"NATIVE_API H06_DETERMINISTIC PASS assertions={deterministic}"
    System.Console.WriteLine $"NATIVE_API H06 LIFETIME PASS assertions={lifecycle}"
    System.Console.WriteLine $"NATIVE_API H06 PROVIDER {service.ProviderName}; {service.LocaleCanonicalization}; {service.TimeZoneCanonicalization}"
    let localeAlias = service.CanonicalizeLocale "iw-IL"
    let zoneAlias = service.CanonicalizeTimeZone "US/Eastern"
    System.Console.WriteLine $"NATIVE_API H06 ALIAS iw-IL={localeAlias} US/Eastern={zoneAlias}"
    0

let private runSvg (host: WebCoreHost) =
    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let geometry = SvgGeometry.run host.Document errorName
    let values = SvgValues.run host.Document errorName false true

    if geometry <> 126 || values <> 361 then
        failwith "SVG shared assertions did not run"

    System.Console.WriteLine $"NATIVE_API S01_S02 PASS assertions={geometry}"
    System.Console.WriteLine $"NATIVE_API S04_REACHABLE PASS assertions={values}"
    geometry + values

let private runTransforms (host: WebCoreHost) =
    let shared =
        TransformValues.run
            (fun () -> Ranvier.View.WebCore.TransformValues.forHost host)
            (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
            (fun () -> Geometry.forHost host)
            (function
             | WebCoreDomException (name, _) -> name
             | error -> error.GetType().Name)

    use transforms = Ranvier.View.WebCore.TransformValues.forHost host
    use values = Ranvier.View.WebCore.StyleValues.forHost host
    use maps = Ranvier.View.WebCore.StyleMaps.forHost host

    let effects =
        TransformValues.runEffects
            host.Document
            transforms
            values
            maps
            (fun () -> host.Frame () |> ignore)
            (fun value -> value :? Ranvier.Dom.CSSTransformValue)
            (fun value -> value :? Ranvier.Dom.CSSTranslate)

    if shared <> 450 || effects <> 21 then
        failwith "Typed transform shared/effect assertions did not run"

    shared + effects

/// <summary>Drives a task on the host owner thread: continuations run between host cycles.</summary>
type private OwnerLoop() =
    inherit System.Threading.SynchronizationContext()

    let queue =
        System.Collections.Concurrent.ConcurrentQueue<struct (System.Threading.SendOrPostCallback * obj)>()

    override _.Post(callback, state) =
        queue.Enqueue (struct (callback, state))

    override this.Send(callback, state) =
        callback.Invoke state

    member _.Drain() =
        let mutable item = Unchecked.defaultof<_>

        while queue.TryDequeue (&item) do
            let struct (callback, state) = item
            callback.Invoke state

let mutable private survivor: (Ranvier.Dom.EventTarget * Ranvier.Dom.Event) option =
    None

/// <summary>Runs the shared frame fixture on the manual scheduler and on the native host scheduler.</summary>
let private runFrames () =
    let complete (pending: System.Threading.Tasks.Task<int>) (pump: unit -> unit) =
        let timeout = System.Diagnostics.Stopwatch.StartNew ()

        while not pending.IsCompleted
              && timeout.ElapsedMilliseconds < 20000L do
            pump ()

        pending.GetAwaiter().GetResult()

    let manual = Ranvier.Dom.ManualFrameScheduler ()

    let manualDriver: FrameChecks.Driver =
        {
            Checkpoint = fun () -> async { manual.RunMicrotasks () }
            Elapse = fun milliseconds -> async { manual.Advance milliseconds }
            NextFrame = fun () -> async { manual.RunFrame () }
        }

    let manualCount =
        complete
            (FrameChecks.run (fun () -> manual :> Ranvier.Dom.FrameScheduler) manualDriver
             |> Async.StartImmediateAsTask)
            ignore

    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    let hostCount =
        try
            let driver: FrameChecks.Driver =
                {
                    Checkpoint = fun () -> async { host.RunMicrotasks () }
                    Elapse =
                        fun milliseconds ->
                            async {
                                let clock = System.Diagnostics.Stopwatch.StartNew ()

                                while clock.Elapsed.TotalMilliseconds < milliseconds do
                                    host.Cycle 1

                                host.Cycle ()
                            }
                    NextFrame = fun () -> async { host.Frame () |> ignore }
                }

            complete
                (FrameChecks.run (fun () -> Ranvier.View.WebCore.Frames.forHost host) driver
                 |> Async.StartImmediateAsTask)
                (fun () -> host.Cycle ())
        finally
            (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith "Frame fixture leaked native wrappers"

    if manualCount <> 18 || hostCount <> 18 then
        failwith $"Frame shared assertions did not run: manual {manualCount}, host {hostCount}"

    System.Console.WriteLine $"NATIVE_API A12 PASS assertions={hostCount} manual={manualCount}"
    hostCount

let private runIdle () =
    let complete (pending: System.Threading.Tasks.Task<int>) (pump: unit -> unit) =
        let timeout = System.Diagnostics.Stopwatch.StartNew ()

        while not pending.IsCompleted
              && timeout.ElapsedMilliseconds < 20000L do
            pump ()

        pending.GetAwaiter().GetResult()

    let manual = Ranvier.Dom.ManualFrameScheduler ()

    let manualCount =
        complete
            (IdleChecks.run
                (fun () -> manual :> Ranvier.Dom.IdleScheduler, manual :> Ranvier.Dom.FrameScheduler)
                {
                    Idle = fun () -> async { manual.RunIdle 50.0 }
                    NextFrame = fun () -> async { manual.RunFrame () }
                    Starve = fun milliseconds -> async { manual.Advance milliseconds }
                }
             |> Async.StartImmediateAsTask)
            ignore

    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    let hostCount, lifetime =
        try
            let driver: IdleChecks.Driver =
                {
                    Idle =
                        fun () ->
                            async {
                                if host.FrameRequested then
                                    host.Frame () |> ignore

                                host.Cycle ()
                            }
                    NextFrame = fun () -> async { host.Frame () |> ignore }
                    Starve =
                        fun milliseconds ->
                            async {
                                let blocker = host.RequestAnimationFrame ignore
                                let clock = System.Diagnostics.Stopwatch.StartNew ()

                                while clock.Elapsed.TotalMilliseconds < milliseconds do
                                    host.Cycle 1

                                host.Cycle ()
                                host.CancelAnimationFrame blocker
                                host.Frame () |> ignore
                            }
                }

            let count =
                complete
                    (IdleChecks.run (fun () -> Ranvier.View.WebCore.Frames.idleForHost host, Ranvier.View.WebCore.Frames.forHost host) driver
                     |> Async.StartImmediateAsTask)
                    ignore

            // Host-loop lifetime: a requested frame defers the idle period, and a period is bounded by the next timer.
            let mutable checks = 0
            let mutable ran = false
            let mutable remaining = -1.0

            let check label condition =
                checks <- checks + 1

                if not condition then
                    failwith $"Idle lifetime {label} failed"

            host.Frame () |> ignore
            let frame = host.RequestAnimationFrame ignore

            host.RequestIdleCallback (fun deadline ->
                ran <- true
                remaining <- deadline.TimeRemaining ())
            |> ignore

            host.Cycle ()
            check "frame defers idle" (not ran)
            host.CancelAnimationFrame frame
            host.Frame () |> ignore
            let timer = host.SetTimeout (ignore, 10.0)
            host.Cycle ()
            check "idle runs after the frame" ran
            check "deadline bounded by the next timer" (remaining <= 10.0)
            host.ClearTimeout timer
            let unknown = host.RequestIdleCallback ignore
            host.CancelIdleCallback unknown
            host.CancelIdleCallback unknown
            host.CancelIdleCallback 999999
            check "cancellation of unknown ids is a no-op" true
            let pendingIdle = Ranvier.View.WebCore.Frames.idleForHost host
            pendingIdle.Idle (Some 5.0, ignore) |> ignore
            pendingIdle.Idle (None, ignore) |> ignore
            count, checks
        finally
            (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith "Idle fixture leaked native wrappers"

    if
        manualCount <> 14
        || hostCount <> 14
        || lifetime <> 4
    then
        failwith $"Idle shared assertions did not run: manual {manualCount}, host {hostCount}, lifetime {lifetime}"

    System.Console.WriteLine $"NATIVE_API H01 PASS assertions={hostCount} manual={manualCount}"
    System.Console.WriteLine $"NATIVE_API H01 LIFETIME PASS assertions={lifetime}"
    hostCount

/// <summary>Runs the shared input state fixture with native window identity and host-disposal checks.</summary>
let private runInputState () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let input action =
        async { action () }

    let driver: InputStateChecks.Driver =
        {
            SetActive = fun active -> input (fun () -> host.SetFocused active)
            Move = fun x y -> input (fun () -> host.Mouse (0, x, y, -1, 0, 0) |> ignore)
            Press = fun x y -> input (fun () -> host.Mouse (1, x, y, 0, 1, 0) |> ignore)
            Release = fun x y -> input (fun () -> host.Mouse (2, x, y, 0, 1, 0) |> ignore)
            Tab =
                fun () ->
                    input (fun () ->
                        host.Key (0, "Tab", "Tab", null, 0, false)
                        |> ignore

                        host.Key (1, "Tab", "Tab", null, 0, false)
                        |> ignore)
        }

    let mutable kept = None

    let shared =
        try
            let count =
                InputStateChecks.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.InputState.forHost host)
                    Ranvier.View.WebCore.InputState.capabilities
                    Ranvier.Dom.CapabilityState.No
                    driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 20000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()

            let window = Ranvier.View.WebCore.InputState.window host
            expect (obj.ReferenceEquals (window, Ranvier.View.WebCore.InputState.window host)) "One window wrapper per host"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            Ranvier.View.WebCore.InputState.forHost host
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Input state rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (obj.ReferenceEquals (window, Ranvier.View.WebCore.InputState.window host)) "Document replacement keeps the window"
            let service = Ranvier.View.WebCore.InputState.forHost host
            let changes = System.Collections.Generic.List<Ranvier.Dom.InputChange>()
            service.Subscribe changes.Add |> ignore
            host.SetFocused false

            expect
                (changes.Count = 1
                 && changes[0] = Ranvier.Dom.InputChange.Activation false)
                "Service on the replacement document observes the window"

            kept <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    kept.Value.Dispose ()
    kept.Value.Dispose ()
    expect (session.LiveObjects = 0) "Input state fixture leaves no live native objects"
    System.Console.WriteLine $"NATIVE_API I12 COUNT {shared}"

    if shared <> 39 then
        failwith $"Input state shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API I12 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API I12 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared viewport fixture with native visibility and host-disposal checks.</summary>
let private runViewport () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let settle () =
        async {
            for _ in 1..3 do
                host.Frame ()
                host.Cycle 20 |> ignore

            host.Frame ()
        }

    let driver: ViewportChecks.Driver =
        {
            Resize =
                fun width height scale ->
                    async {
                        host.Resize (width, height)
                        host.SetDeviceScale (float32 scale)
                        do! settle ()
                    }
            Settle = settle
            SetActive =
                fun active ->
                    async {
                        host.SetFocused active
                        do! settle ()
                    }
            SetVisible =
                Some (fun visible ->
                    async {
                        host.SetVisible visible
                        do! settle ()
                    })
            PaintSize =
                fun () ->
                    async {
                        let struct (width, height) = host.PaintSize
                        return width, height
                    }
            PaintRounds = false
            OuterIsViewport = true
        }

    let mutable kept = None

    let shared =
        try
            host.SetFocused true

            let count =
                ViewportChecks.run host.Document (fun () -> Ranvier.View.WebCore.Viewport.forHost host) driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 20000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            Ranvier.View.WebCore.Viewport.forHost host
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Viewport rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            let service = Ranvier.View.WebCore.Viewport.forHost host
            let changes = System.Collections.Generic.List<Ranvier.Dom.ViewportChange>()
            service.Subscribe changes.Add |> ignore
            host.Resize (300, 200)
            host.Frame ()

            // The replacement document starts unscrolled, so the update also reports its scroll.
            expect
                (changes.Count > 0
                 && changes[0] = Ranvier.Dom.ViewportChange.Resized (service.Current ()))
                $"Service on the replacement document observes the window: %A{List.ofSeq changes}"

            kept <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    kept.Value.Dispose ()
    kept.Value.Dispose ()
    expect (session.LiveObjects = 0) "Viewport fixture leaves no live native objects"

    if shared <> 61 then
        failwith $"Viewport shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G02/G12 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G02/G12 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared fractional scrolling fixture with native thread and host-disposal checks.</summary>
[<Literal>]
let private ScrollChecksCount = 278

let private runViewportScroll () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let settle () =
        async {
            for _ in 1..3 do
                host.Frame ()
                host.Cycle 20 |> ignore

            host.Frame ()
        }

    let driver: ScrollChecks.Driver =
        {
            SetScale =
                fun scale ->
                    async {
                        host.Resize (320, 240)
                        host.SetDeviceScale (float32 scale)
                        do! settle ()
                    }
            Settle = settle
            Snapping = ScrollChecks.Truncate
        }

    let mutable kept = None

    let shared =
        try
            let count =
                ScrollChecks.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.ViewportScroll.forHost host)
                    (fun () -> Ranvier.View.WebCore.Viewport.forHost host)
                    driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            Ranvier.View.WebCore.ViewportScroll.forHost host
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Viewport scrolling rejects another thread"
            let service = Ranvier.View.WebCore.ViewportScroll.forHost host

            let crossThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Position () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect crossThread "A service call on another thread raises"
            host.Document.body.setAttribute ("style", "height:2000px")
            service.ScrollTo (Ranvier.Dom.ScrollRequest.point 0.0 10.5)
            expect ((service.Position ()).Y = 10.0) "The native engine truncates 10.5 to 10"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect ((service.Position ()).Y = 0.0) "A replacement document starts unscrolled"
            kept <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    let raises (action: unit -> unit) =
        try
            action ()
            false
        with :? System.ObjectDisposedException ->
            true

    expect (raises (fun () -> kept.Value.Position () |> ignore)) "Position after host dispose raises"
    expect (raises (fun () -> kept.Value.ScrollBy (Ranvier.Dom.ScrollRequest.point 1.0 1.0))) "ScrollBy after host dispose raises"
    expect (session.LiveObjects = 0) "Viewport scrolling fixture leaves no live native objects"

    if shared <> ScrollChecksCount then
        failwith $"Viewport scrolling shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G05 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G05 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared visual viewport fixture with native page replacement, thread and host-disposal checks.</summary>
[<Literal>]
let private VisualViewportChecksCount = 66

let private runVisualViewport () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let settle () =
        async {
            for _ in 1..4 do
                host.Frame ()
                host.Cycle 20 |> ignore

            host.Frame ()
        }

    let driver: VisualViewportChecks.Driver =
        {
            Resize =
                fun width height ->
                    async {
                        host.Resize (width, height)
                        do! settle ()
                    }
            SetDeviceScale =
                fun scale ->
                    async {
                        host.SetDeviceScale (float32 scale)
                        do! settle ()
                    }
            SetPageScale =
                fun service scale x y ->
                    async {
                        if service.SetPageScale (scale, x, y) <> Ok () then
                            failwith $"rv_host_set_page_scale {scale}"

                        do! settle ()
                    }
            Wheel =
                fun x y dx dy ->
                    async {
                        host.Wheel (x, y, float32 dx, float32 dy, 0)
                        |> ignore

                        do! settle ()
                    }
            Settle = settle
            Engine = VisualViewportChecks.WebKit
            HostInsets = true
        }

    let mutable kept = None

    let shared =
        try
            let window () =
                box host.Document :?> Ranvier.View.WebCore.NativeObject

            let scrollTo x y =
                RanvierWebCoreAbi.NativeDom.rv_dom_window_scroll_to_2 ((window ()).Handle, x, y)
                |> ignore

            let count =
                VisualViewportChecks.run host.Document scrollTo (fun () -> Ranvier.View.WebCore.VisualViewport.forHost host) driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            Ranvier.View.WebCore.VisualViewport.forHost host
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "The visual viewport rejects another thread"
            let service = Ranvier.View.WebCore.VisualViewport.forHost host

            let crossThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Current () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect crossThread "A service read on another thread raises"
            expect (service.SetPageScale (2.0, 0, 0) = Ok ()) "page scale 2"
            settle () |> Async.StartImmediate

            expect
                (match service.Current () with
                 | Ok state -> state.Width = 160.0 && state.Scale = 2.0
                 | Error _ -> false)
                "page scale 2 halves the visual viewport"

            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            // The engine keeps the frame's window for the replacement document; the earlier provider reports Detached,
            // as a browser's VisualViewport does after its frame navigates.
            expect (service.Current () = Error Ranvier.Dom.VisualViewportUnavailable.Detached) "The replaced document's visual viewport is detached"
            let replacement = Ranvier.View.WebCore.VisualViewport.forHost host

            expect
                (match replacement.Current () with
                 | Ok state -> state.Scale = 2.0
                 | Error _ -> false)
                "The page scale outlives the page replacement"

            expect (replacement.SetPageScale (1.0, 0, 0) = Ok ()) "page scale reset"
            service.Dispose ()
            kept <- Some replacement
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (kept.Value.Current () = Error Ranvier.Dom.VisualViewportUnavailable.Detached) "Current after host dispose is Detached"

    let raises (action: unit -> unit) =
        try
            action ()
            false
        with :? System.ObjectDisposedException ->
            true

    expect (raises (fun () -> kept.Value.SetPageScale (2.0, 0, 0) |> ignore)) "SetPageScale after host dispose raises"
    kept.Value.Dispose ()
    expect (session.LiveObjects = 0) "The visual viewport fixture leaves no live native objects"

    if shared <> VisualViewportChecksCount then
        failwith $"Visual viewport shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G03 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G03 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared ARIA element reflection fixture with native thread and host-disposal checks.</summary>
[<Literal>]
let private AriaChecksCount = 18

let private runAria () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let mutable kept = null

    let shared =
        try
            let count = AriaChecks.run host.Document AriaChecks.WebKit
            let button = host.Document.createElement "button"
            let label = host.Document.createElement "span"
            host.Document.body.appendChild label |> ignore
            host.Document.body.appendChild button |> ignore
            button.ariaLabelledByElements <- [| label |]

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            button.ariaLabelledByElements |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A reflection read on another thread raises"
            expect (button.ariaLabelledByElements.Length = 1) "the reflection holds the label"
            kept <- button
            count
        finally
            (host :> System.IDisposable).Dispose()

    let raises (action: unit -> unit) =
        try
            action ()
            false
        with :? System.ObjectDisposedException ->
            true

    expect (raises (fun () -> kept.ariaLabelledByElements |> ignore)) "A reflection read after host dispose raises"
    expect (session.LiveObjects = 0) "The ARIA fixture leaves no live native objects"

    if shared <> AriaChecksCount then
        failwith $"ARIA shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API I11 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API I11 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared caret hit-testing fixture with native thread and host-disposal checks.</summary>
[<Literal>]
let private CaretChecksCount = 1537

let private runCaret () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let settle () =
        async {
            for _ in 1..3 do
                host.Frame ()
                host.Cycle 20 |> ignore

            host.Frame ()
        }

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let driver: CaretChecks.Driver =
        {
            SetScale =
                fun scale ->
                    async {
                        host.Resize (320, 240)
                        host.SetDeviceScale (float32 scale)
                        do! settle ()
                    }
            Settle = settle
            Font =
                System.Convert.ToBase64String (
                    System.IO.File.ReadAllBytes (System.IO.Path.Combine (System.AppContext.BaseDirectory, "Assets", "Ahem.ttf"))
                )
            Qualified = CaretOracle.nativeQualified
        }

    let mutable kept = None

    let shared =
        try
            let count =
                CaretChecks.run host.Document (Ranvier.View.WebCore.ViewportScroll.forHost host) errorName driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 120000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()
            host.Document.body.innerHTML <- "<p id=\"p\">AAAA</p>"
            host.Frame ()
            let caret = host.Document.caretPositionFromPoint (2.0, 2.0)
            expect (not (isNull caret)) "A caret over text before dispose"

            let crossThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            caret.offset |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect crossThread "CaretPosition.offset on another thread raises"
            kept <- Some caret
            count
        finally
            (host :> System.IDisposable).Dispose()

    let raises (action: unit -> unit) =
        try
            action ()
            false
        with :? System.ObjectDisposedException ->
            true

    expect (raises (fun () -> kept.Value.offset |> ignore)) "CaretPosition.offset after host dispose raises"
    expect (raises (fun () -> kept.Value.getClientRect () |> ignore)) "CaretPosition.getClientRect after host dispose raises"
    expect (session.LiveObjects = 0) "Caret fixture leaves no live native objects"

    if shared <> CaretChecksCount then
        failwith $"Caret shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G09 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G09 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared form fixture with native thread, collection, FormData and event teardown checks.</summary>
[<Literal>]
let private FormChecksCount = 69

let private runForms () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let mutable held = None

    let shared =
        try
            let count =
                FormChecks.run host.Document (fun () -> Ranvier.View.WebCore.FormData.forHost host) errorName

            use service = Ranvier.View.WebCore.FormData.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Create () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "FormData rejects another thread"
            let document = host.Document
            let form = unbox<Ranvier.Dom.HTMLFormElement>(document.createElement "form")
            let input = document.createElement "input"
            input.setAttribute ("name", "kept")
            input.setAttribute ("value", "v")
            form.appendChild input |> ignore
            document.body.appendChild form |> ignore
            let data = service.FromForm (form, null)
            let elements = form.elements

            let submit =
                service.CreateSubmitEvent ("submit", Ranvier.Dom.EventOptions.Default, null)

            let event =
                service.CreateFormDataEvent ("formdata", Ranvier.Dom.EventOptions.Default, data)

            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            host.Cycle ()
            RanvierWebCoreAbi.Native.rv_collect_garbage ()

            expect
                (service.Entries data =
                    [|
                        {
                            Name = "kept"
                            Value = Ranvier.Dom.FormDataEntryValue.String "v"
                        }
                    |])
                "FormData survives collection"

            expect (elements.length = 1u) "Form controls collection survives collection"
            expect (service.Entries event.formData = service.Entries data) "FormDataEvent keeps its FormData"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (service.Get (data, "kept") = Some (Ranvier.Dom.FormDataEntryValue.String "v")) "FormData survives document replacement"
            held <- Some (data, elements, submit)
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Form fixture leaves no live native objects"
    let data, elements, submit = held.Value

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect (rejected (fun () -> data.append ("k", "v"))) "A held FormData rejects use after host disposal"
    expect (rejected (fun () -> elements.length |> ignore)) "A held collection rejects use after host disposal"
    expect (rejected (fun () -> submit.submitter |> ignore)) "A held SubmitEvent rejects use after host disposal"

    if shared <> FormChecksCount then
        failwith $"Form shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API D13 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API D13 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared binary value fixture with native thread, document replacement and teardown checks.</summary>
[<Literal>]
let private BinaryChecksCount = 102

let private runBinary () =
    let host = WebCoreHost.Create (320, 200)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let driver: BinaryChecks.Driver =
        {
            Settle = fun () -> async { host.Cycle () |> ignore }
            CrlfLineEndings = System.OperatingSystem.IsWindows ()
        }

    let mutable held = None

    let shared =
        try
            let count =
                BinaryChecks.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.BinaryValues.forHost host)
                    (fun () -> Ranvier.View.WebCore.FormData.forHost host)
                    driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle () |> ignore

            let count = count.GetAwaiter().GetResult()
            use service = Ranvier.View.WebCore.BinaryValues.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.CreateBlob ([||], Ranvier.Dom.BlobOptions.Default)
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Blob construction rejects another thread"

            let file =
                service.CreateFile (
                    [| Ranvier.Dom.BlobPart.Text "kept" |],
                    "kept.txt",
                    { Ranvier.Dom.FileOptions.Default with
                        LastModified = Some 7L
                    }
                )

            let slice = file.slice (1L, 3L)
            let list = service.CreateFileList [| file |]
            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            host.Cycle () |> ignore
            RanvierWebCoreAbi.Native.rv_collect_garbage ()
            expect (file.size = 4UL && slice.size = 2UL) "A File and its slice survive collection"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            let result = ref None
            use _ = service.ReadBytes (slice, fun outcome -> result.Value <- Some outcome)

            while result.Value.IsNone do
                host.Cycle () |> ignore

            expect (result.Value = Some (Ranvier.Dom.ResourceOutcome.Succeeded "ep"B)) "A slice reads after document replacement"

            expect
                (list.length = 1u
                 && obj.ReferenceEquals (list.item 0u, file))
                "A FileList survives document replacement"

            let pending = service.ReadBytes (file, ignore)
            held <- Some (file, list, pending)
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Binary fixture leaves no live native objects"
    let file, list, pending = held.Value

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect (rejected (fun () -> file.size |> ignore)) "A held File rejects use after host disposal"
    expect (rejected (fun () -> list.length |> ignore)) "A held FileList rejects use after host disposal"
    pending.Dispose ()

    if shared <> BinaryChecksCount then
        failwith $"Binary shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API H11 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API H11 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared URL fixture with native thread and teardown checks.</summary>
[<Literal>]
let private UrlChecksCount = 189

let private runUrls () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let mutable held = None

    let shared =
        try
            let count =
                UrlChecks.run (fun () -> Ranvier.View.WebCore.Urls.forHost host) errorName UrlChecks.WebKit

            use service = Ranvier.View.WebCore.Urls.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.CanParse ("http://a.test/", None)
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "URL rejects another thread"
            let url = service.Create ("https://held.test/a?b=c", None)
            let parameters = url.searchParams

            let pattern =
                service.CreatePattern (Ranvier.Dom.USVStringOrURLPatternInit.USVString "https://held.test/:p", None, false)

            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            host.Cycle ()
            RanvierWebCoreAbi.Native.rv_collect_garbage ()

            expect
                (url.href = "https://held.test/a?b=c"
                 && parameters.get "b" = "c")
                "URL and searchParams survive collection"

            expect (pattern.test "https://held.test/x") "URLPattern survives collection"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (url.href = "https://held.test/a?b=c") "URL survives document replacement"

            let replaced =
                service.Exec (pattern, Some (Ranvier.Dom.USVStringOrURLPatternInit.USVString "https://held.test/y"), None)

            expect
                (replaced
                 |> Option.map (fun m -> m.Pathname.Groups)
                    =
                    Some [| "p", Some "y" |])
                "URLPattern matches after document replacement"

            held <- Some (url, parameters, pattern)
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "URL fixture leaves no live native objects"
    let url, parameters, pattern = held.Value

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect (rejected (fun () -> url.href |> ignore)) "A held URL rejects use after host disposal"
    expect (rejected (fun () -> parameters.size |> ignore)) "A held URLSearchParams rejects use after host disposal"
    expect (rejected (fun () -> pattern.test "https://held.test/z" |> ignore)) "A held URLPattern rejects use after host disposal"

    if shared <> UrlChecksCount then
        failwith $"URL shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API H13 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API H13 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared event detail fixture with native type, thread and teardown checks.</summary>
[<Literal>]
let private EventDetailChecksCount = 91

let private runEventDetails () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let raisesType (accept: exn -> bool) (action: unit -> unit) =
        try
            action ()
            false
        with error ->
            accept error

    let driver: EventDetailChecks.Driver =
        {
            Settle =
                fun () ->
                    async {
                        host.Cycle 16 |> ignore
                        host.Frame ()
                    }
        }

    let mutable held = None

    let shared =
        try
            let count =
                EventDetailChecks.run host.Document (fun () -> Ranvier.View.WebCore.EventDetails.forHost host) driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle () |> ignore
                host.Frame ()

            let count = count.GetAwaiter().GetResult()
            let service = Ranvier.View.WebCore.EventDetails.forHost host
            let options = Ranvier.Dom.EventOptions.Default

            let error =
                service.CreateErrorEvent ("error", options, Ranvier.Dom.ErrorFields.Default)

            expect (box error :? Ranvier.Dom.ErrorEvent) "An ErrorEvent wraps as ErrorEvent"

            let animation =
                service.CreateAnimationEvent ("animationend", options, Ranvier.Dom.AnimationFields.Default)

            expect (box animation :? Ranvier.Dom.AnimationEvent) "An AnimationEvent wraps as AnimationEvent"

            expect
                (raisesType (fun error -> error :? System.ArgumentNullException) (fun () ->
                    service.CreateErrorEvent (null, options, Ranvier.Dom.ErrorFields.Default)
                    |> ignore))
                "A null event type raises ArgumentNullException"

            expect
                (raisesType (fun error -> error :? System.ArgumentException) (fun () ->
                    service.CreateProgressEvent (
                        "progress",
                        options,
                        { Ranvier.Dom.ProgressFields.Default with
                            Loaded = nan
                        }
                    )
                    |> ignore))
                "A NaN loaded raises ArgumentException"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        raisesType (fun error -> error :? System.InvalidOperationException) (fun () ->
                            service.CreateProgressEvent ("progress", options, Ranvier.Dom.ProgressFields.Default)
                            |> ignore))
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A constructor rejects another thread"
            held <- Some (service, error)
            count
        finally
            (host :> System.IDisposable).Dispose()

    let service, error = held.Value

    expect
        (raisesType (fun error -> error :? System.ObjectDisposedException) (fun () -> error.message |> ignore))
        "A constructed ErrorEvent raises ObjectDisposedException after host disposal"

    expect
        (raisesType
            (fun error ->
                error :? System.ObjectDisposedException
                || error :? System.InvalidOperationException)
            (fun () ->
                service.CreateErrorEvent ("error", Ranvier.Dom.EventOptions.Default, Ranvier.Dom.ErrorFields.Default)
                |> ignore))
        "A constructor after host disposal raises"

    expect (session.LiveObjects = 0) "Event detail fixture leaves no live native objects"
    service.Dispose ()

    expect
        (raisesType (fun error -> error :? System.ObjectDisposedException) (fun () ->
            service.CreatePopStateEvent ("popstate", Ranvier.Dom.EventOptions.Default, false)
            |> ignore))
        "A constructor after disposal raises ObjectDisposedException"

    if shared <> EventDetailChecksCount then
        failwith $"Event detail shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API I08 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API I08 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared value factory fixture with native type, session, thread and teardown checks.</summary>
[<Literal>]
let private ValueFactoryChecksCount = 112

let private runValueFactories () =
    let host = WebCoreHost.Create (320, 240)
    let other = WebCoreHost.Create (64, 64)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let raisesType (accept: exn -> bool) (action: unit -> unit) =
        try
            action ()
            false
        with error ->
            accept error

    let mutable held = None

    let shared =
        try
            let count =
                ValueFactoryChecks.run host.Document (fun () -> Ranvier.View.WebCore.ValueFactories.forHost host)

            let service = Ranvier.View.WebCore.ValueFactories.forHost host
            let foreign = Ranvier.View.WebCore.ValueFactories.forHost other
            let options = Ranvier.Dom.EventOptions.Default
            let text = service.CreateText "kept"

            let wheel =
                service.CreateWheelEvent ("wheel", options, Ranvier.Dom.MouseFields.Default, Ranvier.Dom.WheelFields.Default)

            expect (box text :? Ranvier.Dom.Text) "A constructed Text wraps as Text"
            expect (box wheel :? Ranvier.Dom.WheelEvent) "A constructed WheelEvent wraps as WheelEvent"
            expect (box (service.CreateRange ()) :? Ranvier.Dom.Range) "A constructed Range wraps as Range"
            expect (box (service.CreateStaticRange (text, 0u, text, 1u)) :? Ranvier.Dom.StaticRange) "A constructed StaticRange wraps as StaticRange"

            expect
                (box (service.MatrixFromFloat64Array [| 1.0; 0.0; 0.0; 1.0; 0.0; 0.0 |]) :? Ranvier.Dom.DOMMatrix)
                "fromFloat64Array wraps as DOMMatrix"

            expect
                (raisesType (fun error -> error :? System.ArgumentNullException) (fun () -> service.CreateText null |> ignore))
                "A null Text data raises ArgumentNullException"

            expect
                (raisesType (fun error -> error :? System.ArgumentException) (fun () ->
                    service.CreateWheelEvent (
                        "wheel",
                        options,
                        Ranvier.Dom.MouseFields.Default,
                        { Ranvier.Dom.WheelFields.Default with
                            DeltaY = infinity
                        }
                    )
                    |> ignore))
                "An infinite delta raises ArgumentException"

            let foreignText = foreign.CreateText "z"
            expect (System.Object.ReferenceEquals (foreignText.ownerDocument, other.Document)) "Another host's service makes nodes of its document"

            expect
                (raisesType (fun error -> error :? System.ArgumentException) (fun () ->
                    service.CreateStaticRange (foreignText, 0u, text, 0u)
                    |> ignore))
                "A StaticRange container of another host raises ArgumentException"

            expect
                (raisesType (fun error -> error :? System.ArgumentException) (fun () ->
                    service.CreatePointerEvent (
                        "pointermove",
                        options,
                        { Ranvier.Dom.MouseFields.Default with
                            RelatedTarget = foreignText
                        },
                        Ranvier.Dom.PointerFields.Default
                    )
                    |> ignore))
                "A related target of another host raises ArgumentException"

            let foreignPointer =
                foreign.CreatePointerEvent ("pointermove", options, Ranvier.Dom.MouseFields.Default, Ranvier.Dom.PointerFields.Default)

            expect
                (raisesType (fun error -> error :? System.ArgumentException) (fun () ->
                    service.CreatePointerEvent (
                        "pointermove",
                        options,
                        Ranvier.Dom.MouseFields.Default,
                        { Ranvier.Dom.PointerFields.Default with
                            CoalescedEvents = [| foreignPointer |]
                        }
                    )
                    |> ignore))
                "A coalesced event of another host raises ArgumentException"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () -> raisesType (fun error -> error :? System.InvalidOperationException) (fun () -> service.CreateText "w" |> ignore))
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A factory rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (text.data = "kept") "A constructed node survives document replacement"
            expect (wheel.deltaX = 0.0) "A constructed event survives document replacement"
            foreign.Dispose ()
            held <- Some (service, text)
            count
        finally
            (other :> System.IDisposable).Dispose()
            (host :> System.IDisposable).Dispose()

    let service, text = held.Value

    expect
        (raisesType (fun error -> error :? System.ObjectDisposedException) (fun () -> text.data |> ignore))
        "A constructed Text raises ObjectDisposedException after host disposal"

    expect
        (raisesType
            (fun error ->
                error :? System.ObjectDisposedException
                || error :? System.InvalidOperationException)
            (fun () -> service.CreateComment "c" |> ignore))
        "A factory after host disposal raises"

    expect (session.LiveObjects = 0) "Value factory fixture leaves no live native objects"
    service.Dispose ()

    expect
        (raisesType (fun error -> error :? System.ObjectDisposedException) (fun () -> service.CreateRange () |> ignore))
        "A factory after disposal raises ObjectDisposedException"

    if shared <> ValueFactoryChecksCount then
        failwith $"Value factory shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API D17 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API D17 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared form submission fixture with native exception, thread and teardown checks.</summary>
[<Literal>]
let private SubmissionChecksCount = 26

let private runSubmissions () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let raisesType (accept: exn -> bool) (action: unit -> unit) =
        try
            action ()
            false
        with error ->
            accept error

    let driver: SubmissionChecks.Driver =
        {
            Settle =
                fun () ->
                    async {
                        for _ in 1..3 do
                            host.Cycle 1 |> ignore
                    }
        }

    let mutable held = None

    let shared =
        try
            let count =
                SubmissionChecks.run host.Document (fun () -> Ranvier.View.WebCore.FormSubmissions.forHost host) driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle () |> ignore

            let count = count.GetAwaiter().GetResult()
            let service = Ranvier.View.WebCore.FormSubmissions.forHost host

            expect
                (raisesType (fun error -> error :? System.ArgumentNullException) (fun () ->
                    service.Subscribe (Unchecked.defaultof<_>)
                    |> ignore))
                "A null handler raises ArgumentNullException"

            expect
                (raisesType (fun error -> error :? System.InvalidOperationException) (fun () ->
                    Ranvier.View.WebCore.FormSubmissions.forHost host
                    |> ignore))
                "A second service raises InvalidOperationException"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () -> raisesType (fun error -> error :? System.InvalidOperationException) (fun () -> service.Subscribe ignore |> ignore))
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Subscribe rejects another thread"
            let mutable late = 0

            service.Subscribe (fun _ -> late <- late + 1)
            |> ignore

            let document = host.Document
            let form = unbox<Ranvier.Dom.HTMLFormElement>(document.createElement "form")
            form.setAttribute ("action", "https://app.test/late")
            document.body.appendChild form |> ignore
            form.requestSubmit ()
            expect (late = 0) "A submission waits for a later task"
            held <- Some (service, (fun () -> late))
            count
        finally
            (host :> System.IDisposable).Dispose()

    let service, late = held.Value

    RanvierWebCoreAbi.Native.rv_run_loop_cycle 50
    |> ignore

    expect (late () = 0) "A submission pending at host disposal reaches no handler"
    expect (session.LiveObjects = 0) "Form submission fixture leaves no live native objects"
    service.Dispose ()

    expect
        (raisesType (fun error -> error :? System.ObjectDisposedException) (fun () -> service.Subscribe ignore |> ignore))
        "Subscribe after disposal raises ObjectDisposedException"

    if shared <> SubmissionChecksCount then
        failwith $"Form submission shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API D14 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API D14 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared text segmentation fixture with native thread and teardown checks.</summary>
[<Literal>]
let private SegmentChecksCount = 219

let private runSegments () =
    let host = WebCoreHost.Create (320, 240)
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let shared =
        try
            let count =
                TextSegmentationChecks.run (fun () -> Ranvier.View.WebCore.TextSegmentation.forHost host) errorName TextSegmentationChecks.WebKit

            use segmenter = Ranvier.View.WebCore.TextSegmentation.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            segmenter.Segment ("a b", Ranvier.Dom.SegmentGranularity.Word, None)
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Text segmentation rejects another thread"
            count
        finally
            (host :> System.IDisposable).Dispose()

    // Segmentation needs no live host: it keeps working after the host is destroyed.
    let after = WebCoreHost.Create (320, 240)
    let segmenter = Ranvier.View.WebCore.TextSegmentation.forHost after
    (after :> System.IDisposable).Dispose()

    let segments =
        match segmenter.Segment ("e\u0301x", Ranvier.Dom.SegmentGranularity.Grapheme, None) with
        | Ranvier.Dom.SegmentationResult.Segmented result -> result.Segments |> Array.map (fun s -> s.Segment)
        | _ -> [||]

    expect (segments = [| "e\u0301"; "x" |]) "A segmenter keeps working after its host is destroyed"

    if shared <> SegmentChecksCount then
        failwith $"Text segmentation shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API T06 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API T06 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared screen fixture with native default, work area, thread and teardown checks.</summary>
[<Literal>]
let private ScreenChecksCount = 30

let private runScreen () =
    let host = WebCoreHost.Create (320, 200)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let mutable held = None

    let shared =
        try
            let service = Ranvier.View.WebCore.Screen.forHost host
            let first = service.Read ()
            expect ((first.Width, first.Height, first.AvailWidth, first.ColorDepth) = (320, 200, 320, 24)) "The default screen is the viewport"

            expect
                (first.Orientation = Ranvier.Dom.ScreenOrientationType.LandscapePrimary
                 && first.Angle = 0)
                "The default orientation"

            let resized = ref None
            use _ = service.Subscribe (fun info -> resized.Value <- Some info)
            host.Resize (200, 320)

            expect
                (resized.Value
                 |> Option.map (fun info -> info.Width, info.Height, info.Angle)
                    =
                    Some (200, 320, 90))
                "Resize changes the default screen"

            host.Resize (320, 200)
            service.Dispose ()

            let driver: ScreenChecks.Driver =
                {
                    Set = fun (width, height, orientation) -> async { host.SetScreen (width, height, 0, 0, width, height, 24, orientation) }
                    Settle = fun () -> async { host.Cycle () |> ignore }
                    LockResolves = false
                }

            let count =
                ScreenChecks.run (fun () -> Ranvier.View.WebCore.Screen.forHost host) driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle () |> ignore

            let count = count.GetAwaiter().GetResult()
            let service = Ranvier.View.WebCore.Screen.forHost host
            host.SetScreen (1920, 1080, 0, 40, 1920, 1000, 30, Ranvier.Dom.ScreenOrientationType.LandscapePrimary)
            let area = service.Read ()
            expect ((area.AvailTop, area.AvailHeight, area.ColorDepth, area.PixelDepth) = (40, 1000, 30, 30)) "A set work area and depth"
            host.Resize (640, 480)
            expect (service.Read () = area) "A set screen does not follow Resize"

            let rejected =
                try
                    host.SetScreen (0, 1080, 0, 0, 0, 1080, 24, Ranvier.Dom.ScreenOrientationType.LandscapePrimary)
                    false
                with :? System.NotSupportedException ->
                    true

            expect rejected "SetScreen rejects a zero width"
            expect (service.Read () = area) "A rejected screen changes nothing"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Read () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Read rejects another thread"
            held <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Screen fixture leaves no live native objects"

    let rejected =
        try
            held.Value.Read () |> ignore
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect rejected "A held screen service rejects Read after host disposal"
    held.Value.Dispose ()

    if shared <> ScreenChecksCount then
        failwith $"Screen shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G04 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G04 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared U+0000 round-trip fixture (WebCore #142) on a native host.</summary>
[<Literal>]
let private StringChecksCount = 18

let private runStrings () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let shared =
        try
            StringChecks.run
                host.Document
                (fun () -> Ranvier.View.WebCore.EventDetails.forHost host)
                (fun () -> Ranvier.View.WebCore.BinaryValues.forHost host)
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "U+0000 round-trip fixture leaves no live native objects"

    if shared <> StringChecksCount then
        failwith $"U+0000 round-trip shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API WC142 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API WC142 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared custom highlight fixture with native thread, document replacement and teardown checks.</summary>
[<Literal>]
let private HighlightChecksCount = 35

let private runHighlights () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    let mutable held = None

    let shared =
        try
            let count =
                HighlightChecks.run (fun () -> Ranvier.View.WebCore.Highlights.forHost host) host.Document HighlightChecks.WebKit

            let service = Ranvier.View.WebCore.Highlights.forHost host
            let registry = service.Registry

            let highlight =
                service.Create [| host.Document.createRange () :> Ranvier.Dom.AbstractRange |]

            service.Set (registry, "kept", highlight)

            let wrongThread =
                System.Threading.Tasks.Task.Run(fun () -> rejected (fun () -> service.Size highlight |> ignore)).GetAwaiter().GetResult()

            expect wrongThread "Highlight.size rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (rejected (fun () -> service.Count registry |> ignore)) "A highlight service rejects calls after document replacement"
            use fresh = Ranvier.View.WebCore.Highlights.forHost host
            expect (fresh.Count fresh.Registry = 0) "The new document has its own empty registry"
            expect (not (obj.ReferenceEquals (fresh.Registry, registry))) "The new document has another registry"
            expect (fresh.Size highlight = 0) "Document replacement empties a highlight registered in the replaced document"
            highlight.priority <- 3
            expect (highlight.priority = 3) "A held highlight stays usable after document replacement"
            held <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Highlight fixture leaves no live native objects"
    let service = held.Value
    expect (rejected (fun () -> service.Registry |> ignore)) "A held highlight service rejects calls after host disposal"
    service.Dispose ()

    if shared <> HighlightChecksCount then
        failwith $"Highlight shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API T03 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API T03 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared diagnostics fixture with native thread, snapshot release and teardown checks.</summary>
[<Literal>]
let private DiagnosticsChecksCount = 73

let private runDiagnostics () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let snapshots = RanvierWebCoreAbi.Native.rv_diagnostic_live_count ()
    let mutable held = None

    let shared =
        try
            let count =
                DiagnosticsChecks.run (fun () -> Ranvier.View.WebCore.Diagnostics.forHost host) host.Document DiagnosticsChecks.Native

            expect (RanvierWebCoreAbi.Native.rv_diagnostic_live_count () = snapshots) "Every native diagnostics snapshot is released"
            let service = Ranvier.View.WebCore.Diagnostics.forHost host
            service.Enable ()
            let body = host.Document.body

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.BoxModel body |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A diagnostics read rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (service.BoxModel body = Error Ranvier.Dom.DiagnosticsUnavailable.NotRendered) "An element of a replaced document is not rendered"

            expect
                (service.DocumentRenderTree Ranvier.Dom.RenderTreeOptions.None
                 |> Result.isOk)
                "The replaced document has a render tree"

            held <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Diagnostics fixture leaves no live native objects"
    let service = held.Value

    let rejected =
        try
            service.DocumentRenderTree Ranvier.Dom.RenderTreeOptions.None
            |> ignore

            false
        with :? System.ObjectDisposedException ->
            true

    expect rejected "A held diagnostics service rejects reads after host disposal"
    service.Dispose ()
    expect (not service.Enabled) "Disposing after host disposal disables the service"
    expect (RanvierWebCoreAbi.Native.rv_diagnostic_live_count () = snapshots) "No snapshot outlives the fixture"

    if shared <> DiagnosticsChecksCount then
        failwith $"Diagnostics shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API H08 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API H08 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared blob URL fixture with native thread, file-backed, document replacement and teardown checks.</summary>
[<Literal>]
let private BlobUrlChecksCount = 42

let private runBlobUrls () =
    let host = WebCoreHost.Create (320, 200)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    let wait (result: 'T option ref) =
        let timeout = System.Diagnostics.Stopwatch.StartNew ()

        while result.Value.IsNone && timeout.ElapsedMilliseconds < 60000L do
            host.Cycle () |> ignore

        result.Value.Value

    let driver: BlobUrlChecks.Driver =
        {
            Settle = fun () -> async { host.Cycle () |> ignore }
            CharsetFromType = true
            Origin = "null"
        }

    let mutable held = None

    let shared =
        try
            let count =
                BlobUrlChecks.run
                    (fun () -> Ranvier.View.WebCore.BlobUrls.forHost host)
                    (fun () -> Ranvier.View.WebCore.BinaryValues.forHost host)
                    driver
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted && timeout.ElapsedMilliseconds < 120000L do
                host.Cycle () |> ignore

            let count = count.GetAwaiter().GetResult()
            let service = Ranvier.View.WebCore.BlobUrls.forHost host
            use values = Ranvier.View.WebCore.BinaryValues.forHost host
            let blob = values.CreateBlob ([| Ranvier.Dom.BlobPart.Text "kept" |], Ranvier.Dom.BlobOptions.Default)

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () -> rejected (fun () -> service.CreateObjectUrl blob |> ignore))
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "URL.createObjectURL rejects another thread"

            // A file-backed File: read, then the file deleted between create and read.
            let directory = System.IO.Directory.CreateTempSubdirectory "rv-blob-"
            let path = System.IO.Path.Combine (directory.FullName, "data.txt")
            System.IO.File.WriteAllText (path, "file-bytes")
            let file = Ranvier.View.WebCore.BlobUrls.createFile host path
            expect (file.name = "data.txt" && file.size = 10UL && file.``type`` = "text/plain") "A file-backed File takes its name, size and type from the path"
            let text = ref None
            use _ = service.ReadText (file, fun outcome -> text.Value <- Some outcome)
            expect (wait text = Ranvier.Dom.ResourceOutcome.Succeeded "file-bytes") "A file-backed File reads its bytes"
            host.Cycle 100 |> ignore
            System.IO.File.Delete path
            directory.Delete ()
            let missing = ref None
            use _ = service.ReadText (file, fun outcome -> missing.Value <- Some outcome)

            expect
                (wait missing = Ranvier.Dom.ResourceOutcome.Failed (Ranvier.Dom.ResourceFailure.LoadFailed "NotFoundError"))
                "A deleted file fails its read with NotFoundError"

            expect
                (try
                    Ranvier.View.WebCore.BlobUrls.createFile host "relative.txt" |> ignore
                    false
                 with :? System.ArgumentException ->
                     true)
                "A relative path is rejected"

            // Document replacement revokes the document's URLs and invalidates a read in flight.
            let url = service.CreateObjectUrl blob
            let big = values.CreateBlob ([| Ranvier.Dom.BlobPart.Bytes (Array.zeroCreate (1 <<< 20)) |], Ranvier.Dom.BlobOptions.Default)
            let replaced = ref None
            use _ = service.ReadBytes (big, fun outcome -> replaced.Value <- Some outcome)
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"

            expect
                (match wait replaced with
                 | Ranvier.Dom.ResourceOutcome.Failed (Ranvier.Dom.ResourceFailure.Invalidated _) -> true
                 | _ -> false)
                "Document replacement invalidates a read in flight"

            expect (rejected (fun () -> service.CreateObjectUrl blob |> ignore)) "A blob URL service rejects calls after document replacement"
            use fresh = Ranvier.View.WebCore.BlobUrls.forHost host
            let afterReplace = ref None
            use _ = fresh.ReadUrl (url, fun outcome -> afterReplace.Value <- Some outcome)

            expect
                (match wait afterReplace with
                 | Ranvier.Dom.ResourceOutcome.Failed (Ranvier.Dom.ResourceFailure.LoadFailed _) -> true
                 | _ -> false)
                "A blob URL stops loading after its document is replaced"

            let reader = fresh.CreateFileReader ()
            reader.readAsArrayBuffer big
            let pending = fresh.ReadBytes (big, ignore)
            held <- Some (fresh, reader, pending)
            service.Dispose ()
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Blob URL fixture leaves no live native objects"
    let service, _, pending = held.Value
    expect (pending.Snapshot <> Ranvier.Dom.ResourceState.Pending) "Host disposal ends a read in flight"
    expect (rejected (fun () -> service.CreateFileReader () |> ignore)) "A held blob URL service rejects calls after host disposal"
    pending.Dispose ()
    service.Dispose ()

    if shared <> BlobUrlChecksCount then
        failwith $"Blob URL shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API B144 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API B144 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared coordinate fixture with page scale, thread and teardown checks.</summary>
[<Literal>]
let private CoordinateChecksCount = 304

let private runCoordinates () =
    let host = WebCoreHost.Create (320, 240)
    let other = WebCoreHost.Create (100, 80)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let point x y : Ranvier.Dom.CoordinatePoint = { X = x; Y = y }
    let client = Ranvier.Dom.CoordinateSpace.Client
    let hostSpace = Ranvier.Dom.CoordinateSpace.Host
    let page = Ranvier.Dom.CoordinateSpace.Page
    let mutable held = None

    let shared =
        try
            let foreign () =
                let value = other.Document.createElement "div"
                other.Document.body.appendChild value |> ignore
                value

            host.SetDeviceScale 2.0f

            let count =
                CoordinateChecks.run
                    (fun () -> Ranvier.View.WebCore.Coordinates.forHost host)
                    host.Document
                    foreign
                    (fun () -> 2.0)
                    CoordinateChecks.Native

            let service = Ranvier.View.WebCore.Coordinates.forHost host

            // While page-scaled, HOST is CLIENT times the page scale in both directions.
            RanvierWebCoreAbi.Native.rv_host_set_page_scale (host.Handle, 2.0, 40, 60)
            |> fun status -> if status <> 0 then failwith $"rv_host_set_page_scale {status}"

            expect (service.Convert (point 10.0 20.0, client, hostSpace) = Ok (point 20.0 40.0)) "Client to host doubles at page scale 2"
            expect (service.Convert (point 10.0 20.0, hostSpace, client) = Ok (point 5.0 10.0)) "Host to client halves at page scale 2"
            expect (service.Convert (point 10.0 20.0, client, page) = Ok (point 30.0 50.0)) "Page is client plus the scaled origin"

            expect
                (service.Convert (point 5.0 10.0, hostSpace, Ranvier.Dom.CoordinateSpace.Device) = Ok (point 10.0 20.0))
                "Device is host times the device scale"

            RanvierWebCoreAbi.Native.rv_host_set_page_scale (host.Handle, 1.0, 0, 0)
            |> fun status -> if status <> 0 then failwith $"rv_host_set_page_scale {status}"

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Convert (point 0.0 0.0, client, hostSpace) |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A conversion rejects another thread"
            let body = host.Document.body
            let stranger = other.Document.body
            (other :> System.IDisposable).Dispose ()

            expect
                (service.Convert (point 0.0 0.0, Ranvier.Dom.CoordinateSpace.Element stranger, client) = Error Ranvier.Dom.CoordinateUnavailable.Detached)
                "An element of a disposed host is detached"

            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"

            expect
                (service.Convert (point 0.0 0.0, Ranvier.Dom.CoordinateSpace.Element body, client) = Error Ranvier.Dom.CoordinateUnavailable.Detached)
                "An element of a replaced document is detached"

            expect
                (service.Convert (point 0.0 0.0, client, Ranvier.Dom.CoordinateSpace.Element body) = Error Ranvier.Dom.CoordinateUnavailable.Detached)
                "A conversion into an element of a replaced document is detached"

            expect (service.Convert (point 1.0 2.0, client, page) = Ok (point 1.0 2.0)) "The replacing document converts"
            held <- Some service
            count
        finally
            (other :> System.IDisposable).Dispose ()
            (host :> System.IDisposable).Dispose ()

    expect (session.LiveObjects = 0) "Coordinate fixture leaves no live native objects"
    let service = held.Value

    let rejected =
        try
            service.Convert (point 0.0 0.0, page, client) |> ignore
            false
        with :? System.ObjectDisposedException ->
            true

    expect rejected "A held coordinate service rejects conversions after host disposal"
    service.Dispose ()

    if shared <> CoordinateChecksCount then
        failwith $"Coordinate shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API G14 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API G14 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared cascade fixture with native thread, snapshot release and teardown checks.</summary>
[<Literal>]
let private CascadeChecksCount = 609

let private runCascade () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let snapshots = RanvierWebCoreAbi.Native.rv_diagnostic_live_count ()
    let mutable held = None

    let shared =
        try
            let count =
                CascadeChecks.run
                    (fun () -> Ranvier.View.WebCore.Diagnostics.forHost host)
                    (Ranvier.View.WebCore.ComputedStyles.forHost host)
                    host.Document
                    CascadeChecks.Native

            expect (RanvierWebCoreAbi.Native.rv_diagnostic_live_count () = snapshots) "Every native cascade snapshot is released"
            let service = Ranvier.View.WebCore.Diagnostics.forHost host
            service.Enable ()
            let body = host.Document.body

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.Cascade (body, "order") |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "A cascade read rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (service.Cascade (body, "order") = Error Ranvier.Dom.DiagnosticsUnavailable.NotRendered) "An element of a replaced document is not read"
            held <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Cascade fixture leaves no live native objects"
    let service = held.Value

    let rejected =
        try
            service.Cascade (Unchecked.defaultof<Ranvier.Dom.Element>, "order")
            |> ignore

            false
        with
        | :? System.ObjectDisposedException
        | :? System.ArgumentNullException -> true

    expect rejected "A held diagnostics service rejects cascade reads after host disposal"
    service.Dispose ()
    expect (not service.Enabled) "Disposing after host disposal disables the service"
    expect (RanvierWebCoreAbi.Native.rv_diagnostic_live_count () = snapshots) "No cascade snapshot outlives the fixture"

    if shared <> CascadeChecksCount then
        failwith $"Cascade shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API C25 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API C25 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared crypto fixture with native thread and teardown checks.</summary>
[<Literal>]
let private CryptoChecksCount = 155

let private runCrypto () =
    let host = WebCoreHost.Create (320, 200)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let mutable held = None

    let shared =
        try
            let count =
                CryptoChecks.run (fun () -> Ranvier.View.WebCore.Crypto.forHost host)
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle () |> ignore

            let count = count.GetAwaiter().GetResult()
            let service = Ranvier.View.WebCore.Crypto.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.GetRandomValues (Ranvier.Dom.RandomValuesKind.Uint8, Array.zeroCreate 8, 0, 8)
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "getRandomValues rejects another thread"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            let bytes = Array.zeroCreate<byte> 16
            service.GetRandomValues (Ranvier.Dom.RandomValuesKind.Uint8, bytes, 0, 16)
            expect (Array.exists (fun b -> b <> 0uy) bytes) "getRandomValues fills after document replacement"
            held <- Some service
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Crypto fixture leaves no live native objects"

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    let service = held.Value

    expect
        (rejected (fun () -> service.GetRandomValues (Ranvier.Dom.RandomValuesKind.Uint8, Array.zeroCreate 8, 0, 8)))
        "A held crypto service rejects getRandomValues after host disposal"

    expect (rejected (fun () -> service.RandomUUID () |> ignore)) "A held crypto service rejects randomUUID after host disposal"
    service.Dispose ()

    if shared <> CryptoChecksCount then
        failwith $"Crypto shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API B22 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API B22 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared dialog, popover and command invoker fixture with native thread and teardown checks.</summary>
[<Literal>]
let private DialogChecksCount = 143

let private runDialogs () =
    let host = WebCoreHost.Create (800, 600)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let settle () =
        async {
            for _ in 1..3 do
                host.Frame ()
                host.Cycle 16 |> ignore

            host.Frame ()
        }

    let driver: DialogChecks.Driver =
        {
            Settle = settle
            Press =
                fun key ->
                    async {
                        host.Key (0, key, key, null, 0, false) |> ignore
                        host.Key (1, key, key, null, 0, false) |> ignore
                    }
            Click =
                fun x y ->
                    async {
                        host.Frame ()
                        host.Click (x, y) |> ignore
                    }
            Backend = DialogChecks.Native
        }

    let mutable held = None

    let shared =
        try
            host.SetFocused true

            let count =
                DialogChecks.run host.Document (fun () -> Ranvier.View.WebCore.ToggleEvents.forHost host) driver errorName
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not count.IsCompleted
                  && timeout.ElapsedMilliseconds < 60000L do
                host.Cycle ()

            let count = count.GetAwaiter().GetResult()
            use service = Ranvier.View.WebCore.ToggleEvents.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.CreateToggleEvent ("toggle", Ranvier.Dom.EventOptions.Default, Ranvier.Dom.ToggleFields.Default)
                            |> ignore

                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "ToggleEvent construction rejects another thread"
            let document = host.Document
            let dialog = unbox<Ranvier.Dom.HTMLDialogElement>(document.createElement "dialog")
            let popover = unbox<Ranvier.Dom.HTMLElement>(document.createElement "div")
            popover.popover <- "auto"
            document.body.appendChild dialog |> ignore
            document.body.appendChild popover |> ignore
            dialog.showModal ()
            popover.showPopover ()

            expect
                (dialog.matches ":modal"
                 && popover.matches ":popover-open")
                "An open modal dialog and popover"

            let toggle =
                service.CreateToggleEvent (
                    "toggle",
                    Ranvier.Dom.EventOptions.Default,
                    {
                        OldState = "closed"
                        NewState = "open"
                        Source = null
                    }
                )

            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            host.Cycle ()
            RanvierWebCoreAbi.Native.rv_collect_garbage ()
            expect (toggle.newState = "open") "A ToggleEvent survives collection"
            host.LoadSvg "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"
            expect (popover.matches ":popover-open") "A popover of a replaced document stays open in it"
            expect (toggle.oldState = "closed") "A ToggleEvent survives document replacement"
            held <- Some (dialog, toggle)
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Dialog fixture leaves no live native objects"
    let dialog, toggle = held.Value

    let rejected (action: unit -> unit) =
        try
            action ()
            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect (rejected (fun () -> dialog.close ())) "A held open modal dialog rejects use after host disposal"
    expect (rejected (fun () -> toggle.newState |> ignore)) "A held ToggleEvent rejects use after host disposal"

    if shared <> DialogChecksCount then
        failwith $"Dialog shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API D16 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API D16 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared custom event fixture with native standalone-target lifetime checks.</summary>
let private runCustomEvents () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let shared =
        try
            let count =
                CustomEvents.run host.Document (fun () -> Ranvier.View.WebCore.EventConstruction.forHost host) (fun () ->
                    Ranvier.View.WebCore.Cancellation.forHost host)

            use service = Ranvier.View.WebCore.EventConstruction.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.CreateTarget () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Event construction rejects another thread"
            let mutable calls = 0
            let target = service.CreateTarget ()
            target.addEventListener ("kept", fun _ -> calls <- calls + 1)

            let custom =
                service.Create ("kept", Ranvier.Dom.EventConstruction.Custom (Ranvier.Dom.EventOptions.Default, Ranvier.Dom.TimingDetail.Text "kept"))

            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            host.Cycle ()
            RanvierWebCoreAbi.Native.rv_collect_garbage ()
            expect (service.Detail custom = Some (Ranvier.Dom.TimingDetail.Text "kept")) "Detail survives managed and engine collection"
            expect (target.dispatchEvent custom) "Dispatch on a retained standalone target"
            expect (calls = 1) "Listener ran"
            survivor <- Some (target, custom)
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Custom event fixture leaves no live native objects"
    let disposedTarget, disposedEvent = survivor.Value

    let rejected =
        try
            disposedTarget.dispatchEvent disposedEvent
            |> ignore

            false
        with
        | :? System.ObjectDisposedException
        | :? System.InvalidOperationException -> true

    expect rejected "Host disposal invalidates standalone targets"

    System.Console.WriteLine $"NATIVE_API I02 COUNT {shared}"

    if shared <> 45 then
        failwith $"Custom event shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API I02 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API I02 LIFETIME PASS assertions={lifetime}"
    shared

/// <summary>Runs the shared cancellation fixture and native thread, error, timer and teardown checks.</summary>
let private runCancellation () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let mutable lifetime = 0
    let mutable survivor: Ranvier.Dom.AbortSignal option = None

    let expect condition message =
        lifetime <- lifetime + 1

        if not condition then
            failwith message

    let shared =
        try
            let turn () =
                host.Cycle ()
                System.Threading.Tasks.Task.FromResult ()

            let pending =
                Cancellation.run host.Document (fun () -> Ranvier.View.WebCore.Cancellation.forHost host) turn

            if not pending.IsCompleted then
                failwith "Cancellation fixture did not complete on the owner thread"

            let count = pending.GetAwaiter().GetResult()
            use service = Ranvier.View.WebCore.Cancellation.forHost host

            let wrongThread =
                System.Threading.Tasks.Task
                    .Run(fun () ->
                        try
                            service.CreateController () |> ignore
                            false
                        with :? System.InvalidOperationException ->
                            true)
                    .GetAwaiter()
                    .GetResult()

            expect wrongThread "Cancellation rejects another thread"
            let failing = service.CreateController ()
            let errors = host.Errors.Count
            let mutable after = 0
            use _failing = failing.Signal.OnAbort (fun _ -> failwith "abort callback failure")
            use _after = failing.Signal.OnAbort (fun _ -> after <- after + 1)
            failing.Abort ()
            expect (host.Errors.Count = errors + 1) "Abort callback failure is reported to the host"
            expect (after = 1) "Later abort callbacks run after a failure"
            use source = new System.Threading.CancellationTokenSource ()
            let remote = service.FromCancellationToken source.Token
            System.Threading.Tasks.Task.Run(fun () -> source.Cancel ()).GetAwaiter().GetResult()
            expect (not remote.Aborted) "Off-thread token cancellation waits for the owner"
            host.Cycle ()
            expect remote.Aborted "Owner Cycle applies off-thread token cancellation"
            expect host.NextTimerDelay.IsNone "No host timer before the timeout"
            let distant = service.Timeout 1.0e9
            expect host.NextTimerDelay.IsSome "Timeout uses a host timer"
            service.Dispose ()
            expect host.NextTimerDelay.IsNone "Service disposal clears its host timer"

            let disposedRead =
                try
                    distant.Aborted |> ignore
                    false
                with :? System.ObjectDisposedException ->
                    true

            expect disposedRead "Signals of a disposed service reject reads"
            survivor <- Some ((Ranvier.View.WebCore.Cancellation.forHost host).Timeout 1.0e9)
            expect host.NextTimerDelay.IsSome "Survivor timeout pending before host disposal"
            count
        finally
            (host :> System.IDisposable).Dispose()

    expect (session.LiveObjects = 0) "Cancellation fixture leaves no live native objects"

    let hostDisposedRead =
        try
            survivor.Value.Aborted |> ignore
            false
        with :? System.ObjectDisposedException ->
            true

    expect hostDisposedRead "Host disposal invalidates its cancellation domains"

    if shared <> 56 then
        failwith $"Cancellation shared assertions did not run: {shared}"

    System.Console.WriteLine $"NATIVE_API I01 PASS assertions={shared}"
    System.Console.WriteLine $"NATIVE_API I01 LIFETIME PASS assertions={lifetime}"
    shared

let private runCapabilities () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let previous = System.Threading.SynchronizationContext.Current
    let loop = OwnerLoop ()
    System.Threading.SynchronizationContext.SetSynchronizationContext loop

    let count =
        try
            let pending =
                Capabilities.run (fun () -> Ranvier.View.WebCore.Capabilities.forHost host)

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not pending.IsCompleted
                  && timeout.ElapsedMilliseconds < 10000L do
                loop.Drain ()
                host.Cycle ()
                System.Threading.Thread.Sleep 1

            if not pending.IsCompleted then
                failwith "Capability fixture did not complete on the owner loop"

            pending.GetAwaiter().GetResult()
        finally
            System.Threading.SynchronizationContext.SetSynchronizationContext previous
            (host :> System.IDisposable).Dispose()

    if count <> 42 then
        failwith "Capability shared assertions did not run"

    if session.LiveObjects <> 0 then
        failwith $"Capability fixture leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine $"NATIVE_API H07 PASS assertions={count}"
    System.Console.WriteLine "NATIVE_API H07 TEARDOWN PASS"
    count

let private crc32 (data: byte array) =
    let mutable crc = 0xFFFFFFFFu

    for value in data do
        crc <- crc ^^^ uint32 value

        for _ in 1..8 do
            crc <-
                if crc &&& 1u <> 0u then
                    (crc >>> 1) ^^^ 0xEDB88320u
                else
                    crc >>> 1

    crc ^^^ 0xFFFFFFFFu

let private resourceFixture () : Resources.Fixture =
    let chunk (kind: string) (data: byte array) =
        let name = System.Text.Encoding.ASCII.GetBytes kind

        let length =
            System.BitConverter.GetBytes (uint32 data.Length)
            |> Array.rev

        let crc = crc32 (Array.append name data)
        Array.concat [ length; name; data; System.BitConverter.GetBytes crc |> Array.rev ]

    let png corrupt =
        let rows =
            Array.concat
                [
                    for _ in 1..2 -> Array.append [| 0uy |] (Array.concat [ for _ in 1..3 -> [| 255uy; 0uy; 0uy; 255uy |] ])
                ]

        let body =
            if corrupt then
                Array.append [| 0x78uy; 0x9cuy |] [| 1uy .. 39uy |]
            else
                use output = new System.IO.MemoryStream ()

                do
                    use zlib =
                        new System.IO.Compression.ZLibStream (output, System.IO.Compression.CompressionLevel.Optimal)

                    zlib.Write (rows, 0, rows.Length)

                output.ToArray ()

        let header =
            Array.concat
                [
                    System.BitConverter.GetBytes 3u |> Array.rev
                    System.BitConverter.GetBytes 2u |> Array.rev
                    [| 8uy; 6uy; 0uy; 0uy; 0uy |]
                ]

        Array.concat
            [
                [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x0Duy; 0x0Auy; 0x1Auy; 0x0Auy |]
                chunk "IHDR" header
                chunk "IDAT" body
                chunk "IEND" [||]
            ]

    let source =
        match System.Environment.GetEnvironmentVariable "RANVIER_WEBKIT_SOURCE" with
        | null
        | "" -> "C:/wk/src"
        | value -> value

    {
        Png = png false
        CorruptPng = png true
        Font = System.IO.File.ReadAllBytes (System.IO.Path.Combine (source, "Tools/TestWebKitAPI/Resources/cocoa/Ahem.ttf"))
    }

let private runResources () =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session
    let previous = System.Threading.SynchronizationContext.Current
    let loop = OwnerLoop ()
    System.Threading.SynchronizationContext.SetSynchronizationContext loop

    let count =
        try
            let image (url: string) =
                let element = host.Document.createElement "img"
                element.setAttribute ("src", url)
                element

            let pending =
                Resources.run
                    {
                        CorruptImageDecodes = false
                        TransportReleaseRequests = 1
                        ReleaseEngineCaches =
                            fun () ->
                                // Lets each image loader release its element, then prunes images without clients.
                                for _ in 1..3 do
                                    host.Cycle 50
                                    RanvierWebCoreAbi.Native.rv_collect_garbage ()

                                RanvierWebCoreAbi.Native.rv_release_memory ()
                                host.Cycle 5
                    }
                    (resourceFixture ())
                    (fun provider -> Ranvier.View.WebCore.Resources.forHost host provider)
                    image
                |> Async.StartImmediateAsTask

            let timeout = System.Diagnostics.Stopwatch.StartNew ()

            while not pending.IsCompleted
                  && timeout.ElapsedMilliseconds < 20000L do
                loop.Drain ()
                host.Cycle ()
                System.Threading.Thread.Sleep 1

            if not pending.IsCompleted then
                failwith "Resource fixture did not complete on the owner loop"

            pending.GetAwaiter().GetResult()
        finally
            System.Threading.SynchronizationContext.SetSynchronizationContext previous
            (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"Resource fixture leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine $"NATIVE_API H05 PASS assertions={count}"
    System.Console.WriteLine "NATIVE_API H05 TEARDOWN PASS"
    count

/// <summary>Runs the shared tree traversal and MathML fixtures with native traversal lifetime checks.</summary>
let private runDomModel () =
    let lifetime = TraversalLifecycle.run ()
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    let errorName =
        function
        | WebCoreDomException (name, _) -> name
        | error -> error.GetType().Name

    let interfaceOf (element: Ranvier.Dom.Element) =
        let name = element.GetType().Name
        name.Substring (0, name.Length - "Object".Length)

    let traversal, reentry, mathml =
        try
            let traversal, reentry = TreeTraversal.run host.Document errorName true
            let mathml = MathMLElements.run host.Document interfaceOf "MathMLMathElement"
            traversal, reentry, mathml
        finally
            (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"DOM model fixture leaked {session.LiveObjects} managed native objects"

    if traversal <> 62 || lifetime <> 15 || mathml <> 116 then
        failwith $"DOM model assertions did not run: {traversal}/{lifetime}/{mathml}"

    // Qualification: the engine leaves the re-entered iterator's referenceNode null.
    if
        reentry
        <> {
               TreeTraversal.Iterator = "InvalidStateError"
               TreeTraversal.Walker = "InvalidStateError"
               TreeTraversal.Reference = "null"
           }
    then
        failwith $"Unexpected traversal re-entry outcome: {reentry}"

    System.Console.WriteLine $"NATIVE_API D01 PASS assertions={traversal}"
    System.Console.WriteLine $"NATIVE_API D01 REENTRY iterator={reentry.Iterator} walker={reentry.Walker} reference={reentry.Reference}"
    System.Console.WriteLine $"NATIVE_API D01 LIFETIME PASS assertions={lifetime}"
    System.Console.WriteLine $"NATIVE_API S13 PASS assertions={mathml}"
    System.Console.WriteLine "NATIVE_API D01 S13 TEARDOWN PASS"
    traversal + mathml

let private runAll () =
    runLocales () |> ignore
    let svgLifecycle = SvgLifecycle.run ()

    if svgLifecycle <> 37 then
        failwith "SVG lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API SVG LIFETIME PASS assertions={svgLifecycle}"
    UserTimingNative.run ()
    runCapabilities () |> ignore
    runCancellation () |> ignore
    runResources () |> ignore
    runCustomEvents () |> ignore
    runInputState () |> ignore
    runViewport () |> ignore
    runViewportScroll () |> ignore
    runVisualViewport () |> ignore
    runAria () |> ignore
    runCaret () |> ignore
    runForms () |> ignore
    runDialogs () |> ignore
    runBinary () |> ignore
    runUrls () |> ignore
    runSubmissions () |> ignore
    runSegments () |> ignore
    runCrypto () |> ignore
    runScreen () |> ignore
    runEventDetails () |> ignore
    runValueFactories () |> ignore
    runHighlights () |> ignore
    runStrings () |> ignore
    runDiagnostics () |> ignore
    runCascade () |> ignore
    runCoordinates () |> ignore
    runBlobUrls () |> ignore
    runDomModel () |> ignore
    runFrames () |> ignore
    runIdle () |> ignore
    let transformLifecycle = TransformLifecycle.run ()

    if transformLifecycle <> 47 then
        failwith "Typed transform lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C16 LIFETIME PASS assertions={transformLifecycle}"
    let matrixLifecycle = MatrixLifecycle.run ()

    if matrixLifecycle <> 23 then
        failwith "Matrix lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API G08 LIFETIME PASS assertions={matrixLifecycle}"
    let geometryLifecycle = GeometryLifecycle.run ()

    if geometryLifecycle <> 26 then
        failwith "Geometry lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API G07 LIFETIME PASS assertions={geometryLifecycle}"
    let styleMapLifecycle = StyleMapLifecycle.run ()

    if styleMapLifecycle <> 33 then
        failwith "Typed-map lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C13 LIFETIME PASS assertions={styleMapLifecycle}"
    let styleValueLifecycle = StyleValueLifecycle.run ()

    if styleValueLifecycle <> 19 then
        failwith "Typed-style lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C14 LIFETIME PASS assertions={styleValueLifecycle}"
    let registrationLifecycle = CustomPropertyLifecycle.run ()

    if registrationLifecycle <> 10 then
        failwith "Custom-property lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C12 LIFETIME PASS assertions={registrationLifecycle}"
    let mutable nullQueryHostRejected = false

    try
        CssQueries.forHost Unchecked.defaultof<WebCoreHost>
        |> ignore
    with :? System.ArgumentNullException ->
        nullQueryHostRejected <- true

    if not nullQueryHostRejected then
        failwith "CSS query provider accepted a null host"

    let mutable nullComputedHostRejected = false

    try
        Ranvier.View.WebCore.ComputedStyles.forHost Unchecked.defaultof<WebCoreHost>
        |> ignore
    with :? System.ArgumentNullException ->
        nullComputedHostRejected <- true

    if not nullComputedHostRejected then
        failwith "Computed-style provider accepted a null host"

    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    try
        runSvg host |> ignore
        let transforms = runTransforms host
        System.Console.WriteLine $"NATIVE_API C16 PASS assertions={transforms}"

        let matrices =
            MatrixValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMMatrix)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ArgumentNullException"

        if matrices <> 710 then
            failwith "Matrix shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API G08 PASS assertions={matrices}"

        let geometry =
            GeometryValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMRect, value :? Ranvier.Dom.DOMPoint)
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if geometry <> 177 then
            failwith "Geometry shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API G07 PASS assertions={geometry}"
        let viewTables = ViewTables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10_VIEW PASS assertions={viewTables}"
        let viewHyperlinks = ViewHyperlinks.run host.Document

        if viewHyperlinks <> 50 then
            failwith "View hyperlink authoring assertions did not run"

        System.Console.WriteLine $"NATIVE_API D11_VIEW_HYPERLINKS PASS assertions={viewHyperlinks}"
        let cssEscaping = CssEscaping.run host.Document
        System.Console.WriteLine $"NATIVE_API C05 PASS assertions={cssEscaping}"
        let cssQueries = CssQueries.forHost host
        let cssSupport = CssSupport.run cssQueries
        System.Console.WriteLine $"NATIVE_API C03 PASS assertions={cssSupport}"

        let computedStyles =
            ComputedStyles.run host.Document (fun () -> Ranvier.View.WebCore.ComputedStyles.forHost host) (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C01_C02 PASS assertions={computedStyles}"
        let computedService = Ranvier.View.WebCore.ComputedStyles.forHost host

        let customProperties =
            CustomProperties.run
                host.Document
                (fun () -> Ranvier.View.WebCore.CustomProperties.forHost host)
                computedService
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if customProperties <> 50 then
            failwith "Custom-property shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C12 PASS assertions={customProperties}"

        let styleValues =
            StyleValues.run
                (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ObjectDisposedException"
                (fun value -> value :? Ranvier.Dom.CSSImageValue, value :? Ranvier.Dom.CSSNumericValue)

        if styleValues <> 99 then
            failwith "Typed-style shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C14 PASS assertions={styleValues}"
        use styleValueService = Ranvier.View.WebCore.StyleValues.forHost host

        let styleMaps =
            StyleMaps.run
                host.Document
                (fun () -> Ranvier.View.WebCore.StyleMaps.forHost host)
                styleValueService
                computedService
                (fun () -> host.Frame ())
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ObjectDisposedException"
                (fun map -> (map :?> Ranvier.Dom.StylePropertyMap).set("width", Ranvier.Dom.CSSStyleValueOrUSVString.USVString "999px"))
                "InvalidCastException"

        if styleMaps <> 127 then
            failwith "Typed-map shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C13 PASS assertions={styleMaps}"

        let ruleMutations =
            RuleMutations.run host.Document computedService (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C08 PASS assertions={ruleMutations}"

        let typedStyles =
            TypedStyles.run host.Document computedService (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C04 PASS assertions={typedStyles}"
        let fragmentRectangles = FragmentRectangles.run host.Document
        System.Console.WriteLine $"NATIVE_API G06 PASS assertions={fragmentRectangles}"
        let fontRules, fontWidthAlias = FontTransitionRules.fonts host.Document
        System.Console.WriteLine $"NATIVE_API C18 PASS assertions={fontRules}"
        System.Console.WriteLine $"NATIVE_API C18 CAPABILITY fontWidthAlias={fontWidthAlias}"
        let transitionRules = FontTransitionRules.transitions host.Document
        System.Console.WriteLine $"NATIVE_API C22 PASS assertions={transitionRules}"

        let positionTry, positionTryAvailable =
            PositionTry.run host.Document (fun () -> host.Frame ())

        if positionTry <> 44 || not positionTryAvailable then
            failwith $"Position fallback proof incomplete: assertions={positionTry}, available={positionTryAvailable}"

        System.Console.WriteLine $"NATIVE_API C21 PASS assertions={positionTry}"
        System.Console.WriteLine $"NATIVE_API C21 CAPABILITY positionTry={positionTryAvailable}"

        let counterStyles, counterSpeakAs =
            CounterStyleRules.run host.Document (fun () -> host.Frame ())

        if counterStyles <> 64 then
            failwith $"Counter style proof incomplete: assertions={counterStyles}"

        System.Console.WriteLine $"NATIVE_API C19 PASS assertions={counterStyles}"
        System.Console.WriteLine $"NATIVE_API C19 CAPABILITY speakAs={counterSpeakAs}"

        let keyframeRules, keyframeLength, invalidKeyTextError =
            KeyframeRules.run host.Document computedService (fun () -> host.Frame ()) (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        if
            keyframeRules <> 82
            || not keyframeLength
            || invalidKeyTextError <> ""
        then
            failwith $"Keyframe rule proof incomplete: assertions={keyframeRules}, length={keyframeLength}, error={invalidKeyTextError}"

        System.Console.WriteLine $"NATIVE_API C11 PASS assertions={keyframeRules}"
        System.Console.WriteLine $"NATIVE_API C11 CAPABILITY length={keyframeLength} invalidKeyTextError={invalidKeyTextError}"
        let lifetimeRectangles = host.Document.body.getClientRects ()
        let lifetimeRectangle = lifetimeRectangles.item 0u

        if isNull lifetimeRectangle then
            failwith "Rendered body has no lifetime-test rectangle"

        let rectangleReads =
            [|
                (fun () -> lifetimeRectangles.length |> ignore)
                (fun () -> lifetimeRectangles.item 0u |> ignore)
                (fun () -> lifetimeRectangle.left |> ignore)
            |]

        for read in rectangleReads do
            let mutable rejected = false

            let worker =
                System.Threading.Thread (
                    System.Threading.ThreadStart (fun () ->
                        try
                            read ()
                        with :? System.InvalidOperationException ->
                            rejected <- true)
                )

            worker.Start ()
            worker.Join ()

            if not rejected then
                failwith "Fragment geometry read accepted the wrong thread"

        let lifetimeComputedElement = host.Document.body
        let lifetimeComputedStyle = computedService.Get (lifetimeComputedElement, None)
        let lifetimeTypedStyle = lifetimeComputedElement.style
        let lifetimeTypedDeclaration = Ranvier.Css.Css.width (Ranvier.Css.Css.px 1.)
        let mutable foreignHostRejected = false

        do
            use otherHost = WebCoreHost.Create (16, 16)

            try
                computedService.Get (otherHost.Document.body, None)
                |> ignore
            with :? System.ArgumentException ->
                foreignHostRejected <- true

        if not foreignHostRejected then
            failwith "Computed-style provider accepted another host's element"

        let checks = Tables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10 PASS assertions={checks}"
        let hyperlinks = Hyperlinks.run host.Document
        System.Console.WriteLine $"NATIVE_API HYPERLINKS PASS assertions={hyperlinks}"
        let reflection = Reflection.run host.Document
        System.Console.WriteLine $"NATIVE_API D11 PASS assertions={reflection}"
        let namespaces = Namespaces.run host.Document (fun () -> host.Frame ())
        System.Console.WriteLine $"NATIVE_API D09 PASS assertions={namespaces}"
        let viewportOperations = ElementOperations.viewport host.Document

        if viewportOperations <> 30 then
            failwith "Selected SVG viewport assertions did not run"

        System.Console.WriteLine $"NATIVE_API D09_VIEWPORT PASS assertions={viewportOperations}"
        let svgStylesheets = Stylesheets.runSvg host.Document
        System.Console.WriteLine $"NATIVE_API SVG_CSSOM PASS assertions={svgStylesheets}"
        let stylesheets = Stylesheets.run host.Document
        System.Console.WriteLine $"NATIVE_API CSSOM PASS assertions={stylesheets}"

        let ownerContext = System.Threading.SynchronizationContext.Current

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) ->
                host.QueueMicrotask (fun () -> ownerContext.Post (System.Threading.SendOrPostCallback (fun _ -> success ()), null)))

        let computed (element: Ranvier.Dom.HTMLElement) property =
            let buffer = Array.zeroCreate<byte> 4096
            let length = [| 0 |]
            let handle = (box element :?> NativeObject).Handle

            let status =
                RanvierWebCoreAbi.Native.rv_element_computed_style (handle, property, buffer, buffer.Length, length)

            if status <> 0 then
                failwith $"Computed style returned {status}"

            System.Text.Encoding.UTF8.GetString (buffer, 0, length[0])

        let mutable construction: Result<int, exn> option = None

        Async.StartImmediate (
            async {
                try
                    let originalViewport = host.PaintSize

                    let! conditionalRules =
                        ConditionalRules.run
                            host.Document
                            computedService
                            originalViewport
                            (fun width height ->
                                async {
                                    host.Resize (width, height)
                                    host.Frame ()
                                })
                            (function
                             | WebCoreDomException (name, _) -> name
                             | error -> error.GetType().Name)

                    if host.PaintSize <> originalViewport then
                        failwith "Conditional rules did not restore the original viewport"

                    System.Console.WriteLine $"NATIVE_API C09 PASS assertions={conditionalRules}"

                    let! cascadeRules, scopeRules, nestedDeclarations, importContentsReadable =
                        CascadeRules.run host.Document computedService (fun () -> Async.Sleep 1) (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)

                    System.Console.WriteLine $"NATIVE_API C10 PASS assertions={cascadeRules}"

                    System.Console.WriteLine
                        $"NATIVE_API C10 CAPABILITY scopeRules={scopeRules} nestedDeclarations={nestedDeclarations} importContentsReadable={importContentsReadable}"

                    let! semanticOperations, attachmentIdentifier =
                        ElementOperations.semantic
                            host.Document
                            (fun () -> Async.Sleep 20)
                            (fun () -> host.Frame ())
                            (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)
                            (fun value -> if isNull value then "null" else "string")
                            (if System.OperatingSystem.IsWindows () then
                                 [ "string:" ]
                             else
                                 [ "string:"; "error:NotSupportedException" ])

                    if semanticOperations <> 26 then
                        failwith "Selected HTML operation assertions did not run"

                    System.Console.WriteLine $"NATIVE_API D11_OPERATIONS PASS assertions={semanticOperations}"
                    System.Console.WriteLine $"NATIVE_API D11 CAPABILITY attachmentIdentifier={attachmentIdentifier}"

                    let! sheetElements, failedLinkSheet =
                        SheetElements.run host.Document computed (fun () -> Async.Sleep 10) (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)

                    System.Console.WriteLine $"NATIVE_API D12 PASS assertions={sheetElements}"

                    if sheetElements <> 109 then
                        failwith "Stylesheet/link element assertions did not run"

                    System.Console.WriteLine $"NATIVE_API D12 CAPABILITY failedLinkSheet={failedLinkSheet}"

                    let! checks =
                        SheetConstruction.run
                            host.Document
                            (fun () -> StyleSheets.forHost host)
                            checkpoint
                            computed
                            (fun error -> error.GetType().Name)
                            "ObjectDisposedException"

                    construction <- Some (Ok checks)
                with error ->
                    construction <- Some (Error error)
            }
        )

        let deadline = System.Diagnostics.Stopwatch.StartNew ()

        while construction.IsNone
              && deadline.Elapsed.TotalSeconds < 20.0 do
            host.Cycle 1

        match construction with
        | Some (Ok checks) ->
            if checks <> 78 then
                failwith "Constructed stylesheet preservation assertions did not run"

            System.Console.WriteLine $"NATIVE_API C07 PASS assertions={checks}"
        | Some (Error error) -> raise error
        | None -> failwith "Stylesheet construction fixture did not complete within 20 seconds"

        let svg =
            host.Document.getElementById ("native-api-svg") :?> Ranvier.Dom.HTMLOrSVGElement

        svg.focus ()

        if not (host.Key (0, "Tab", "Tab", "", 0, false)) then
            failwith "Native Tab traversal was not handled"

        let math = host.Document.getElementById ("native-api-math")

        if not (host.Document.activeElement.isSameNode (math)) then
            failwith "Native Tab did not move from SVG to MathML"

        System.Console.WriteLine "NATIVE_API D09 TAB PASS"

        let lifetimeService = StyleSheets.forHost host
        let lifetimeSheet = lifetimeService.Create Ranvier.Dom.StyleSheetOptions.Default
        let mutable wrongThreadRejected = false

        let worker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    Async.StartWithContinuations (
                        lifetimeService.ReplaceAsync (lifetimeSheet, ".wrong-thread {}"),
                        ignore,
                        (fun error -> wrongThreadRejected <- error :? System.InvalidOperationException),
                        ignore
                    ))
            )

        worker.Start ()
        worker.Join ()

        if not wrongThreadRejected then
            failwith "Stylesheet replacement was not rejected off the owning thread"

        let mutable queryWrongThreadRejected = false

        let queryWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        cssQueries.Supports "(display: block)" |> ignore
                    with :? System.InvalidOperationException ->
                        queryWrongThreadRejected <- true)
            )

        queryWorker.Start ()
        queryWorker.Join ()

        if not queryWrongThreadRejected then
            failwith "CSS support query was not rejected off the owning thread"

        let mutable computedWrongThreadRejected = false

        let computedWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        computedService.Get (lifetimeComputedElement, None)
                        |> ignore
                    with :? System.InvalidOperationException ->
                        computedWrongThreadRejected <- true)
            )

        computedWorker.Start ()
        computedWorker.Join ()

        if not computedWrongThreadRejected then
            failwith "Computed-style query was not rejected off the owning thread"

        let mutable typedWrongThreadRejected = false

        let typedWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        Ranvier.Css.Dom.StyleDeclaration.apply lifetimeTypedStyle lifetimeTypedDeclaration
                    with :? System.InvalidOperationException ->
                        typedWrongThreadRejected <- true)
            )

        typedWorker.Start ()
        typedWorker.Join ()

        if not typedWrongThreadRejected then
            failwith "Typed CSS write was not rejected off the owning thread"

        let mutable terminal = ""

        Async.StartWithContinuations (
            lifetimeService.ReplaceAsync (lifetimeSheet, ".before-host-dispose {}"),
            (fun _ -> terminal <- "success"),
            (fun _ -> terminal <- "error"),
            (fun _ -> terminal <- "cancel")
        )

        if terminal <> "" then
            failwith "Stylesheet replacement completed without a checkpoint"

        (host :> System.IDisposable).Dispose()

        for value in [ box lifetimeRectangles; box lifetimeRectangle ] do
            if (value :?> NativeObject).Handle <> 0n then
                failwith "Disposed host retained a fragment geometry handle"

        for read in rectangleReads do
            let mutable rejected = false

            try
                read ()
            with :? System.ObjectDisposedException ->
                rejected <- true

            if not rejected then
                failwith "Fragment geometry read accepted a disposed host"

        System.Console.WriteLine "NATIVE_API G06 LIFETIME PASS assertions=8"

        let mutable disposedQueryRejected = false

        try
            cssQueries.Supports ("display", "block") |> ignore
        with :? System.ObjectDisposedException ->
            disposedQueryRejected <- true

        if not disposedQueryRejected then
            failwith "CSS support query accepted a disposed host"

        let mutable disposedFactoryRejected = false

        try
            CssQueries.forHost host |> ignore
        with :? System.ObjectDisposedException ->
            disposedFactoryRejected <- true

        if not disposedFactoryRejected then
            failwith "CSS query provider accepted a disposed host"

        System.Console.WriteLine "NATIVE_API C03 LIFETIME PASS assertions=4"

        let mutable disposedComputedRejected = false

        try
            computedService.Get (lifetimeComputedElement, None)
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedComputedRejected <- true

        if not disposedComputedRejected then
            failwith "Computed-style query accepted a disposed host"

        let mutable disposedComputedFactoryRejected = false

        try
            Ranvier.View.WebCore.ComputedStyles.forHost host
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedComputedFactoryRejected <- true

        if not disposedComputedFactoryRejected then
            failwith "Computed-style provider accepted a disposed host"

        if
            (box lifetimeComputedStyle :?> NativeObject).Handle
            <> 0n
        then
            failwith "Disposed host retained a computed declaration handle"

        let mutable disposedDeclarationRejected = false

        try
            lifetimeComputedStyle.getPropertyValue "width"
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedDeclarationRejected <- true

        if not disposedDeclarationRejected then
            failwith "Computed declaration accepted a read after host disposal"

        System.Console.WriteLine "NATIVE_API C01_C02 LIFETIME PASS assertions=7"

        let mutable disposedTypedRejected = false

        try
            Ranvier.Css.Dom.StyleDeclaration.apply lifetimeTypedStyle lifetimeTypedDeclaration
        with :? System.ObjectDisposedException ->
            disposedTypedRejected <- true

        if not disposedTypedRejected then
            failwith "Typed CSS write accepted a disposed host"

        System.Console.WriteLine "NATIVE_API C04 LIFETIME PASS assertions=2"

        if terminal <> "cancel" then
            failwith $"Host disposal did not terminate pending replacement: {terminal}"

        if (box lifetimeSheet :?> NativeObject).Handle <> 0n then
            failwith "Disposed host retained a constructed stylesheet handle"

        System.Console.WriteLine "NATIVE_API C07 LIFETIME PASS assertions=4"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"Shared API fixtures leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine "NATIVE_API TEARDOWN PASS groups=D09,D10,D11,CSSOM"
    0

let private runOnHostThread (work: Async<int>) =
    (Async.StartImmediateAsTask work).GetAwaiter().GetResult()

let private surfaceErrorName =
    function
    | WebCoreDomException (name, _) -> name
    | error -> error.GetType().Name

let private nativeWait (host: WebCoreHost) (milliseconds: int) =
    async {
        let clock = System.Diagnostics.Stopwatch.StartNew ()

        while clock.ElapsedMilliseconds < int64 milliseconds do
            host.Cycle 5
            System.Threading.Thread.Sleep 1
    }

let private nativePaint (host: WebCoreHost) (x: float, y: float) =
    async {
        let struct (width, height) = host.PaintSize
        let rgba = Array.zeroCreate<byte>(width * height * 4)
        host.Paint rgba
        let offset = ((int y * width) + int x) * 4

        return
            rgba[offset] < 64uy
            && rgba[offset + 1] < 64uy
            && rgba[offset + 2] < 64uy
            && rgba[offset + 3] > 200uy
    }

let private nativePixel (host: WebCoreHost) (x: float, y: float) =
    async {
        let struct (width, height) = host.PaintSize
        let rgba = Array.zeroCreate<byte>(width * height * 4)
        host.Paint rgba
        let offset = ((int y * width) + int x) * 4
        return int rgba[offset], int rgba[offset + 1], int rgba[offset + 2]
    }

let private runPaint () =
    let host = WebCoreHost.Create (640, 480)
    let session = host.Session

    try
        let pixel = nativePixel host

        let paint =
            SvgPaintServers.run host.Document pixel surfaceErrorName
            |> runOnHostThread

        let filters =
            SvgFilters.run host.Document pixel surfaceErrorName
            |> runOnHostThread

        let clipping =
            SvgClipping.run host.Document pixel surfaceErrorName
            |> runOnHostThread

        let resources =
            SvgResources.run host.Document pixel (nativeWait host) surfaceErrorName
            |> runOnHostThread

        if
            paint <> 49
            || filters <> 118
            || clipping <> 38
            || resources <> 45
        then
            failwith "SVG paint assertions did not run"

        System.Console.WriteLine $"NATIVE_API S08 PASS assertions={paint}"
        System.Console.WriteLine $"NATIVE_API S09 PASS assertions={filters}"
        System.Console.WriteLine $"NATIVE_API S10 PASS assertions={clipping}"
        System.Console.WriteLine $"NATIVE_API S11 PASS assertions={resources}"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith "SVG paint fixture leaked native wrappers"

    System.Console.WriteLine "NATIVE_API SVG_PAINT TEARDOWN PASS"
    0

let private viewSelectionArguments (host: WebCoreHost) =
    let outcome action =
        try
            action () |> ignore
            "accepted"
        with error ->
            surfaceErrorName error

    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    equal "HTML document selection rejects" "NotSupportedException" (outcome (fun () -> host.SelectSvgView "half"))
    host.LoadSvg SvgViewSelection.markup
    equal "null fragment rejects" "ArgumentNullException" (outcome (fun () -> host.SelectSvgView null))
    equal "URL hash is literal fragment text" false (host.SelectSvgView "#half")
    let mutable worker = "not run"

    let thread =
        System.Threading.Thread (fun () -> worker <- outcome (fun () -> host.SelectSvgView "half"))

    thread.Start ()
    thread.Join ()
    equal "selection requires host thread" "InvalidOperationException" worker

    equal
        "repeat selection"
        true
        (host.SelectSvgView "half"
         && host.SelectSvgView "half")

    equal "repeat clear" false (host.SelectSvgView "" || host.SelectSvgView "")
    host.LoadSvg SvgViewSelection.markup

    equal
        "replacement document starts unselected"
        false
        (match SvgViews.inspect (host.Document.documentElement :?> Ranvier.Dom.SVGSVGElement) with
         | {
               UseCurrentView = Ranvier.Dom.SvgViewAvailability.Available active
           } -> active
         | _ -> true)

    checks

let private runSurfaces () =
    let documentLifetime = SvgDocumentLifecycle.run ()
    let host = WebCoreHost.Create (640, 480)
    let session = host.Session

    try
        let shapes =
            SvgShapes.run host.Document surfaceErrorName "ArgumentNullException" true

        let fontData =
            System.Convert.ToBase64String (
                System.IO.File.ReadAllBytes (System.IO.Path.Combine (System.AppContext.BaseDirectory, "Assets", "Ahem.ttf"))
            )

        let text =
            SvgText.run host.Document fontData (nativeWait host) (nativePaint host) surfaceErrorName true
            |> runOnHostThread

        let animations =
            SvgAnimations.run host.Document (nativeWait host) surfaceErrorName true
            |> runOnHostThread

        let arguments = viewSelectionArguments host
        host.LoadSvg SvgViewSelection.markup

        let views =
            SvgViewSelection.run
                host.Document
                (fun fragment -> async { return Some (host.SelectSvgView fragment) })
                (fun () -> async { host.Frame () |> ignore })
                SvgViews.inspect
                true
                false
            |> runOnHostThread

        if
            shapes <> 194
            || text <> 210
            || views <> 126
            || arguments <> 7
            || animations <> 31
            || documentLifetime <> 49
        then
            failwith "SVG surfaces assertions did not run"

        System.Console.WriteLine $"NATIVE_API S03 PASS assertions={shapes}"
        System.Console.WriteLine $"NATIVE_API S05 PASS assertions={text}"
        System.Console.WriteLine $"NATIVE_API S06 PASS assertions={views} arguments={arguments}"
        System.Console.WriteLine $"NATIVE_API S07 PASS assertions={animations}"
        System.Console.WriteLine $"NATIVE_API SVG_DOCUMENT LIFETIME PASS assertions={documentLifetime}"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith "SVG surfaces fixture leaked native wrappers"

    System.Console.WriteLine "NATIVE_API SVG_SURFACES TEARDOWN PASS"
    0

[<EntryPoint>]
let main args =
    if Array.contains "--locales-only" args then
        runLocales ()
    elif Array.contains "--svg-surfaces-only" args then
        runSurfaces ()
    elif Array.contains "--svg-paint-only" args then
        runPaint ()
    elif Array.contains "--svg-only" args then
        let lifecycle = SvgLifecycle.run ()

        if lifecycle <> 37 then
            failwith "SVG lifetime assertions did not run"

        let host = WebCoreHost.Create (640, 480)
        let session = host.Session

        try
            runSvg host |> ignore
            System.Console.WriteLine $"NATIVE_API SVG LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "SVG fixture leaked native wrappers"

        System.Console.WriteLine "NATIVE_API SVG TEARDOWN PASS"
        0
    elif Array.contains "--transforms-only" args then
        let lifecycle = TransformLifecycle.run ()
        let host = WebCoreHost.Create (64, 64)
        let session = host.Session

        try
            let count = runTransforms host

            if count <> 471 || lifecycle <> 47 then
                failwith "Typed transform focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C16 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C16 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Typed transform fixture leaked native wrappers"

        System.Console.WriteLine "NATIVE_API C16 TEARDOWN PASS"
        0
    elif Array.contains "--matrix-only" args then
        let lifecycle = MatrixLifecycle.run ()
        use host = WebCoreHost.Create (64, 64)

        let count =
            MatrixValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMMatrix)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ArgumentNullException"

        if count <> 710 || lifecycle <> 23 then
            failwith "Matrix shared/lifetime assertions did not run"

        System.Console.WriteLine $"NATIVE_API G08 PASS assertions={count}"
        System.Console.WriteLine $"NATIVE_API G08 LIFETIME PASS assertions={lifecycle}"
        (host :> System.IDisposable).Dispose()

        if host.Session.LiveObjects <> 0 then
            failwith "Matrix wrappers leaked"

        System.Console.WriteLine "NATIVE_API G08 TEARDOWN PASS"
        0
    elif Array.contains "--geometry-only" args then
        let lifecycle = GeometryLifecycle.run ()
        use host = WebCoreHost.Create (64, 64)

        let count =
            GeometryValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMRect, value :? Ranvier.Dom.DOMPoint)
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if count <> 177 || lifecycle <> 26 then
            failwith "Geometry shared/lifetime assertions did not run"

        System.Console.WriteLine $"NATIVE_API G07 PASS assertions={count}"
        System.Console.WriteLine $"NATIVE_API G07 LIFETIME PASS assertions={lifecycle}"
        (host :> System.IDisposable).Dispose()

        if host.Session.LiveObjects <> 0 then
            failwith "Geometry wrappers leaked"

        System.Console.WriteLine "NATIVE_API G07 TEARDOWN PASS"
        0
    elif Array.contains "--style-maps-only" args then
        let lifecycle = StyleMapLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            use values = Ranvier.View.WebCore.StyleValues.forHost host
            let computed = Ranvier.View.WebCore.ComputedStyles.forHost host

            let count =
                StyleMaps.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.StyleMaps.forHost host)
                    values
                    computed
                    (fun () -> host.Frame ())
                    (function
                    | WebCoreDomException (name, _) -> name
                    | error -> error.GetType().Name)
                    "ObjectDisposedException"
                    (fun map -> (map :?> Ranvier.Dom.StylePropertyMap).set("width", Ranvier.Dom.CSSStyleValueOrUSVString.USVString "999px"))
                    "InvalidCastException"

            if count <> 127 || lifecycle <> 33 then
                failwith "Typed-map focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C13 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C13 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Style-map fixture leaked native wrappers"

        0
    elif Array.contains "--style-values-only" args then
        let lifecycle = StyleValueLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            let count =
                StyleValues.run
                    (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
                    (function
                    | WebCoreDomException (name, _) -> name
                    | error -> error.GetType().Name)
                    "ObjectDisposedException"
                    (fun value -> value :? Ranvier.Dom.CSSImageValue, value :? Ranvier.Dom.CSSNumericValue)

            if count <> 99 || lifecycle <> 19 then
                failwith "Typed-style focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C14 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C14 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Typed-style fixture leaked native wrappers"

        System.Console.WriteLine "NATIVE_API C14 TEARDOWN PASS"

        0
    elif Array.contains "--custom-events-only" args then
        runCustomEvents () |> ignore
        0
    elif Array.contains "--dom-model-only" args then
        runDomModel () |> ignore
        0
    elif Array.contains "--input-state-only" args then
        runInputState () |> ignore
        0
    elif Array.contains "--viewport-only" args then
        runViewport () |> ignore
        0
    elif Array.contains "--scroll-only" args then
        runViewportScroll () |> ignore
        0
    elif Array.contains "--aria-only" args then
        runAria () |> ignore
        0
    elif Array.contains "--visual-viewport-only" args then
        runVisualViewport () |> ignore
        0
    elif Array.contains "--caret-only" args then
        runCaret () |> ignore
        0
    elif Array.contains "--forms-only" args then
        runForms () |> ignore
        0
    elif Array.contains "--segments-only" args then
        runSegments () |> ignore
        0
    elif Array.contains "--urls-only" args then
        runUrls () |> ignore
        0
    elif Array.contains "--blob-urls-only" args then
        runBlobUrls () |> ignore
        0
    elif Array.contains "--highlights-only" args then
        runHighlights () |> ignore
        0
    elif Array.contains "--strings-only" args then
        runStrings () |> ignore
        0
    elif Array.contains "--diagnostics-only" args then
        runDiagnostics () |> ignore
        0
    elif Array.contains "--cascade-only" args then
        runCascade () |> ignore
        0
    elif Array.contains "--coordinates-only" args then
        runCoordinates () |> ignore
        0
    elif Array.contains "--crypto-only" args then
        runCrypto () |> ignore
        0
    elif Array.contains "--screen-only" args then
        runScreen () |> ignore
        0
    elif Array.contains "--submissions-only" args then
        runSubmissions () |> ignore
        0
    elif Array.contains "--event-details-only" args then
        runEventDetails () |> ignore
        0
    elif Array.contains "--value-factories-only" args then
        runValueFactories () |> ignore
        0
    elif Array.contains "--dialogs-only" args then
        runDialogs () |> ignore
        0
    elif Array.contains "--binary-only" args then
        runBinary () |> ignore
        0
    elif Array.contains "--frames-only" args then
        runFrames () |> ignore
        0
    elif Array.contains "--idle-only" args then
        runIdle () |> ignore
        0
    elif Array.contains "--cancellation-only" args then
        runCancellation () |> ignore
        0
    elif Array.contains "--capabilities-only" args then
        runCapabilities () |> ignore
        0
    elif Array.contains "--resources-only" args then
        runResources () |> ignore
        0
    elif Array.contains "--custom-properties-only" args then
        let lifecycle = CustomPropertyLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            let count =
                CustomProperties.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.CustomProperties.forHost host)
                    (Ranvier.View.WebCore.ComputedStyles.forHost host)
                    (fun error -> error.GetType().Name)
                    "ObjectDisposedException"

            if count <> 50 || lifecycle <> 10 then
                failwith "Custom-property focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C12 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C12 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith $"Custom-property fixture leaked {session.LiveObjects} managed native objects"

        System.Console.WriteLine "NATIVE_API C12 TEARDOWN PASS"
        0
    else
        runSurfaces () |> ignore
        runPaint () |> ignore
        runAll ()
