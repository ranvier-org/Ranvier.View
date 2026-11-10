module Ranvier.View.WebCore.Tests.WindowTests

open System
open Expecto
open Ranvier.Dom
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

let private square =
    Component.define (fun _ (count: Signal<int>) -> count) (fun t ->
        t.button
            [|
                t.Attrs.attribute ("style", "position:absolute;left:0;top:0;width:100px;height:100px;border:0;background:rgb(255,0,0)")
                t.Events.on ("click", fun (count: Signal<int>) (_: Event) -> count.Value <- count.Value + 1)
            |]
            [| t.text (fun (count: Signal<int>) -> string count.Value) |])

let private pointerSquare =
    Component.define (fun _ (count: Signal<int>) -> count) (fun t ->
        t.button
            [|
                t.Attrs.attribute ("style", "position:absolute;left:0;top:0;width:100px;height:100px")
                t.Events.on ("pointerdown", fun (count: Signal<int>) (_: Event) -> count.Value <- count.Value + 1)
            |]
            [||])

// A hidden window drawn by SDL's software renderer, so the test needs no GPU, on the WebCore host thread.
let private windowCase (name: string) (body: Window -> unit) =
    testCase name (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("Ranvier test", 200, 150, hidden = true, renderer = "software")

            body window
            Expect.isEmpty (List.ofSeq window.Host.Errors) "listener, microtask and timer errors"))

let private steps (window: Window) count =
    for _ in 1..count do
        window.Step 0 |> ignore

// Steps for 50 milliseconds, longer than a refresh interval, so held PerFrame input is due and dispatched.
let private settle (window: Window) =
    let elapsed = Diagnostics.Stopwatch.StartNew ()

    while elapsed.ElapsedMilliseconds < 50L do
        window.Step 50 |> ignore

let private pixel (struct (width: int, _: int, bytes: byte array)) x y =
    let at = (y * width + x) * 4
    bytes[at], bytes[at + 1], bytes[at + 2]

// Queues a left button press or release at x, y in the window, as SDL reports one from the platform.
let private press (window: Window) (kind: SDL_EventType) x y =
    let mutable event = SDL_Event ()
    let down = kind = SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
    event.button.``type`` <- kind
    event.button.windowID <- SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)
    event.button.button <- byte SDL3.SDL_BUTTON_LEFT
    event.button.down <- SDLBool.op_Implicit down
    event.button.clicks <- 1uy
    event.button.x <- x
    event.button.y <- y

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event): bool) then
        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

let private windowId (window: Window) =
    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

let private push (event: SDL_Event) =
    let mutable event = event

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event): bool) then
        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

// Queues a mouse move to x, y in the window.
let private move (window: Window) x y =
    let mutable event = SDL_Event ()
    event.motion.``type`` <- SDL_EventType.SDL_EVENT_MOUSE_MOTION
    event.motion.windowID <- windowId window
    event.motion.x <- x
    event.motion.y <- y
    push event

// Queues a vertical wheel scroll of `ticks` at 50, 50; negative ticks scroll down.
let private scroll (window: Window) ticks =
    let mutable event = SDL_Event ()
    event.wheel.``type`` <- SDL_EventType.SDL_EVENT_MOUSE_WHEEL
    event.wheel.windowID <- windowId window
    event.wheel.y <- ticks
    event.wheel.mouse_x <- 50.0f
    event.wheel.mouse_y <- 50.0f
    push event

// Queues an A key press.
let private keyDown (window: Window) =
    let mutable event = SDL_Event ()
    event.key.``type`` <- SDL_EventType.SDL_EVENT_KEY_DOWN
    event.key.windowID <- windowId window
    event.key.scancode <- SDL_Scancode.SDL_SCANCODE_A
    event.key.down <- SDLBool.op_Implicit true
    push event

let private pixelSizeChanged (window: Window) =
    let mutable event = SDL_Event ()
    event.window.``type`` <- SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
    event.window.windowID <- windowId window
    push event

// Queues a hovering move of pen 42 to x, y.
let private penMove (window: Window) x y =
    let mutable event = SDL_Event ()
    event.pmotion.``type`` <- SDL_EventType.SDL_EVENT_PEN_MOTION
    event.pmotion.windowID <- windowId window
    event.pmotion.which <- LanguagePrimitives.EnumOfValue 42u
    event.pmotion.x <- x
    event.pmotion.y <- y
    push event

// Queues a pressure change of pen 42 at x, y.
let private penPressure (window: Window) x y pressure =
    let mutable event = SDL_Event ()
    event.paxis.``type`` <- SDL_EventType.SDL_EVENT_PEN_AXIS
    event.paxis.windowID <- windowId window
    event.paxis.which <- LanguagePrimitives.EnumOfValue 42u
    event.paxis.x <- x
    event.paxis.y <- y
    event.paxis.axis <- SDL_PenAxis.SDL_PEN_AXIS_PRESSURE
    event.paxis.value <- pressure
    push event

// Queues a finger event at normalized x, y.
let private finger (window: Window) (kind: SDL_EventType) x y =
    let mutable event = SDL_Event ()
    event.tfinger.``type`` <- kind
    event.tfinger.windowID <- windowId window
    event.tfinger.touchID <- LanguagePrimitives.EnumOfValue 7UL
    event.tfinger.fingerID <- LanguagePrimitives.EnumOfValue 9UL
    event.tfinger.x <- x
    event.tfinger.y <- y
    event.tfinger.pressure <- 0.5f
    push event

// The page's events of `kinds`: a mouse or pointer event's client position, a wheel event's deltas, or zeros.
let private eventLog (window: Window) (kinds: string list) =
    let log = ResizeArray<string * int * int>()

    for kind in kinds do
        window.Host.Document.addEventListener (
            kind,
            fun (event: Event) ->
                match event with
                | :? WheelEvent as wheel -> log.Add (kind, int wheel.deltaX, int wheel.deltaY)
                | :? MouseEvent as mouse -> log.Add (kind, int mouse.clientX, int mouse.clientY)
                | _ -> log.Add (kind, 0, 0)
        )

    log

let private mouseLog (window: Window) =
    eventLog window [ "mousemove"; "mousedown"; "mouseup" ]

// A hidden window with `rates`, focused so key events reach the page.
let private ratesCase (name: string) (rates: InputRates) (body: Window -> unit) =
    testCase name (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("Ranvier test", 200, 150, hidden = true, renderer = "software", inputRates = rates)

            window.Host.SetFocused true
            steps window 2
            body window
            Expect.isEmpty (List.ofSeq window.Host.Errors) "listener, microtask and timer errors"))

let private closeRequest (window: Window) =
    let mutable event = SDL_Event ()
    event.window.``type`` <- SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED
    event.window.windowID <- SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event): bool) then
        failwith "SDL_PushEvent rejected the close request"

[<Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
let private registerEphemeral (registry: WindowRegistry<obj>) =
    let value = obj ()
    registry.Add (7u, value)
    WeakReference<obj> value

[<Tests>]
let tests =
    testList
        "Window"
        [
            windowCase "fractional display scale covers the full pixel extent"
            <| fun window ->
                for pixelsWide, pixelsHigh, scale in [ 498, 352, 1.25f; 497, 351, 1.5f; 501, 353, 2.0f; 500, 360, 1.0f ] do
                    window.Host.SetDeviceScale scale
                    window.Host.Resize (Sdl.viewportExtent pixelsWide scale, Sdl.viewportExtent pixelsHigh scale)
                    let struct (paintWidth, paintHeight) = window.Host.PaintSize

                    Expect.isTrue
                        (paintWidth >= pixelsWide
                         && paintHeight >= pixelsHigh)
                        "the painted surface covers every output pixel"

            testCase "a bare host owns the first window admission" (fun () ->
                onHostThread (fun () ->
                    use host = WebCoreHost.Create (80, 60)
                    let mutable failure: exn = null

                    let other =
                        Threading.Thread (
                            Threading.ThreadStart (fun () ->
                                try
                                    use unexpected =
                                        Window.Create ("wrong owner", 80, 60, hidden = true, renderer = "software")

                                    ()
                                with error ->
                                    failure <- error)
                        )

                    other.Start ()
                    Expect.isTrue (other.Join 5000) "the other thread finishes"
                    Expect.isTrue (failure :? InvalidOperationException) "the other thread is rejected"
                    Expect.stringContains failure.Message "same native thread" "rejection happens at admission"
                    use window = Window.Create ("owner", 80, 60, hidden = true, renderer = "software")
                    Expect.isTrue (window.Step 0) "the host owner can create and pump a window"))

            windowCase "a mounted component reaches the window's texture"
            <| fun window ->
                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount window.Host graph square count
                steps window 3

                let image = window.ReadPixels ()
                let struct (width, height, _) = image
                Expect.isGreaterThan width 0 "the window has a texture"
                Expect.isGreaterThan height 0 "the window has a texture"
                Expect.equal (pixel image 90 90) (255uy, 0uy, 0uy) "the button's background is on screen"
                Expect.equal (pixel image 150 120) (255uy, 255uy, 255uy) "the page's background is on screen"

            windowCase "an SDL display event sets the host screen through the window's display feed"
            <| fun window ->
                let display event =
                    let mutable pushed = SDL_Event ()
                    pushed.``type`` <- uint32 event
                    pushed.display.displayID <- SDL3.SDL_GetPrimaryDisplay ()

                    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&pushed): bool) then
                        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

                    steps window 1

                use service = Screen.forHost window.Host
                let changes = ResizeArray<ScreenInfo>()
                use _ = service.Subscribe changes.Add

                let portrait =
                    {
                        Width = 1080
                        Height = 1920
                        UsableLeft = 0
                        UsableTop = 40
                        UsableWidth = 1080
                        UsableHeight = 1880
                        ColorDepth = 30
                        Orientation = ScreenOrientationType.PortraitPrimary
                    }

                window.SetDisplayReader (fun () -> ValueSome portrait)
                display SDL_EventType.SDL_EVENT_DISPLAY_ORIENTATION
                let screen = service.Read ()

                Expect.equal
                    (screen.Width, screen.Height, screen.AvailTop, screen.AvailHeight)
                    (1080, 1920, 40, 1880)
                    "the display's bounds and usable bounds"

                Expect.equal
                    (screen.ColorDepth, screen.Orientation, screen.Angle)
                    (30, ScreenOrientationType.PortraitPrimary, 90)
                    "the display's depth and orientation"

                Expect.equal changes.Count 1 "one change delivered"
                display SDL_EventType.SDL_EVENT_DISPLAY_USABLE_BOUNDS_CHANGED
                Expect.equal changes.Count 1 "the same display sets nothing"

                window.SetDisplayReader (fun () ->
                    ValueSome
                        { portrait with
                            UsableTop = 0
                            UsableHeight = 1920
                        })

                display SDL_EventType.SDL_EVENT_DISPLAY_USABLE_BOUNDS_CHANGED
                Expect.equal (service.Read ()).AvailHeight 1920 "a usable-bounds change reaches the screen"
                window.SetDisplayReader (fun () -> ValueNone)
                display SDL_EventType.SDL_EVENT_DISPLAY_REMOVED
                Expect.equal (service.Read ()).Width 1080 "a window on no display keeps its screen"

            windowCase "SDL mouse events reach the page as a click"
            <| fun window ->
                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount window.Host graph square count
                steps window 2

                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 50.0f 50.0f
                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP 50.0f 50.0f
                steps window 2
                Expect.equal (graph.Run (fun () -> count.Value)) 1 "the click handler ran"

            windowCase "queued mouse moves reach the page as one mousemove at the last position by default"
            <| fun window ->
                steps window 2
                let log = mouseLog window
                Expect.equal window.InputRates InputRates.Default "per frame is the default"

                for x in [ 10.0f; 20.0f; 30.0f; 40.0f ] do
                    move window x (x + 5.0f)

                steps window 1
                Expect.equal (List.ofSeq log) [ "mousemove", 40, 45 ] "the latest move only"

            windowCase "raw pointer motion reaches the page as every mousemove"
            <| fun window ->
                steps window 2
                let log = mouseLog window

                window.InputRates <-
                    { window.InputRates with
                        PointerMotion = InputRate.Raw
                    }

                for x in [ 10.0f; 20.0f; 30.0f ] do
                    move window x x

                steps window 1

                Expect.equal (List.ofSeq log) [ "mousemove", 10, 10; "mousemove", 20, 20; "mousemove", 30, 30 ] "every move"

            ratesCase
                "per drain pointer motion reaches the page as one mousemove per Step"
                { InputRates.Default with
                    PointerMotion = InputRate.PerDrain
                }
            <| fun window ->
                let log = mouseLog window
                move window 10.0f 10.0f
                move window 20.0f 20.0f
                steps window 1
                move window 30.0f 30.0f
                move window 40.0f 40.0f
                steps window 1
                Expect.equal (List.ofSeq log) [ "mousemove", 20, 20; "mousemove", 40, 40 ] "the latest move of each drain"

            testCase "input rates display as sprintf %+A does, without reflection"
            <| fun () ->
                let mixed =
                    { InputRates.Raw with
                        Wheel = InputRate.PerDrain
                        Resize = InputRate.PerFrame
                    }

                for rates in [ InputRates.Default; InputRates.Raw; mixed ] do
                    Expect.equal rates.Display (sprintf "%+A" rates) "the record"

                for rate in [ InputRate.PerFrame; InputRate.PerDrain; InputRate.Raw ] do
                    Expect.equal (rate.ToString ()) (sprintf "%+A" rate) "the rate"

            ratesCase "held pointer motion waits in Step until it is due" InputRates.Default
            <| fun window ->
                let log = mouseLog window
                move window 10.0f 10.0f
                steps window 1
                move window 20.0f 20.0f
                let elapsed = Diagnostics.Stopwatch.StartNew ()
                let mutable turns = 0

                while log.Count < 2
                      && elapsed.ElapsedMilliseconds < 200L do
                    window.Step 50 |> ignore
                    turns <- turns + 1

                Expect.equal (List.ofSeq log) [ "mousemove", 10, 10; "mousemove", 20, 20 ] "both moves"
                Expect.isLessThan turns 20 "Steps until the held move is due"

            ratesCase "queued wheel events reach the page as one summed wheel event around a key press" InputRates.Default
            <| fun window ->
                let log = eventLog window [ "wheel"; "keydown" ]
                scroll window -1.0f
                scroll window -1.0f
                keyDown window
                scroll window -1.0f
                steps window 1
                settle window

                Expect.equal (List.ofSeq log) [ "wheel", 0, 80; "keydown", 0, 0; "wheel", 0, 40 ] "deltas summed per run, in order"

            ratesCase "raw wheel events reach the page as every wheel event" InputRates.Raw
            <| fun window ->
                let log = eventLog window [ "wheel"; "keydown" ]
                scroll window -1.0f
                scroll window -1.0f
                keyDown window
                scroll window -1.0f
                steps window 1
                settle window

                Expect.equal (List.ofSeq log) [ "wheel", 0, 40; "wheel", 0, 40; "keydown", 0, 0; "wheel", 0, 40 ] "every wheel event"

            ratesCase "queued size changes apply once per run around a button press" InputRates.Default
            <| fun window ->
                let before = window.ResizesApplied
                let log = mouseLog window

                for _ in 1..3 do
                    pixelSizeChanged window

                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 20.0f 20.0f
                pixelSizeChanged window
                pixelSizeChanged window
                steps window 1
                Expect.equal (window.ResizesApplied - before) 2 "the latest size before the press and at the drain's end"
                Expect.equal (List.ofSeq log) [ "mousedown", 20, 20 ] "the press reaches the page"

            ratesCase "raw size changes apply every change" InputRates.Raw
            <| fun window ->
                let before = window.ResizesApplied

                for _ in 1..3 do
                    pixelSizeChanged window

                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 20.0f 20.0f
                pixelSizeChanged window
                pixelSizeChanged window
                steps window 1
                Expect.equal (window.ResizesApplied - before) 5 "every change"

            ratesCase "queued pen moves and axis changes reach the page as one pointermove per run" InputRates.Default
            <| fun window ->
                if not (WebCoreHost.Has "pointer-input") then
                    skiptest "the runtime has no pointer-input"

                let log = eventLog window [ "pointermove"; "keydown" ]
                penMove window 10.0f 10.0f
                penMove window 20.0f 20.0f
                penMove window 30.0f 30.0f
                keyDown window
                penPressure window 30.0f 30.0f 0.5f
                penMove window 40.0f 40.0f
                steps window 1
                settle window

                Expect.equal
                    (List.ofSeq log)
                    [ "pointermove", 30, 30; "keydown", 0, 0; "pointermove", 40, 40 ]
                    "the latest sample of each run, in order"

            ratesCase "raw pen moves and axis changes reach the page as every pointermove" InputRates.Raw
            <| fun window ->
                if not (WebCoreHost.Has "pointer-input") then
                    skiptest "the runtime has no pointer-input"

                let log = eventLog window [ "pointermove"; "keydown" ]
                penMove window 10.0f 10.0f
                penMove window 20.0f 20.0f
                keyDown window
                penPressure window 20.0f 20.0f 0.5f
                penMove window 40.0f 40.0f
                steps window 1

                Expect.equal
                    (List.ofSeq log)
                    [
                        "pointermove", 10, 10
                        "pointermove", 20, 20
                        "keydown", 0, 0
                        "pointermove", 20, 20
                        "pointermove", 40, 40
                    ]
                    "every sample"

            ratesCase "queued touch moves reach the page as one pointermove per run between down and up" InputRates.Default
            <| fun window ->
                if not (WebCoreHost.Has "touch-input") then
                    skiptest "the runtime has no touch-input"

                let log = eventLog window [ "pointerdown"; "pointermove"; "pointerup"; "keydown" ]
                finger window SDL_EventType.SDL_EVENT_FINGER_DOWN 0.1f 0.1f
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.2f 0.2f
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.3f 0.3f
                keyDown window
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.4f 0.4f
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.5f 0.5f
                finger window SDL_EventType.SDL_EVENT_FINGER_UP 0.5f 0.5f
                steps window 1

                Expect.equal
                    (List.ofSeq log)
                    [
                        "pointerdown", 20, 15
                        "pointermove", 60, 45
                        "keydown", 0, 0
                        "pointermove", 100, 75
                        "pointerup", 100, 75
                    ]
                    "the latest frame of each run, in order"

            ratesCase "raw touch moves reach the page as every pointermove" InputRates.Raw
            <| fun window ->
                if not (WebCoreHost.Has "touch-input") then
                    skiptest "the runtime has no touch-input"

                let log = eventLog window [ "pointerdown"; "pointermove"; "pointerup"; "keydown" ]
                finger window SDL_EventType.SDL_EVENT_FINGER_DOWN 0.1f 0.1f
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.2f 0.2f
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.3f 0.3f
                keyDown window
                finger window SDL_EventType.SDL_EVENT_FINGER_MOTION 0.5f 0.5f
                finger window SDL_EventType.SDL_EVENT_FINGER_UP 0.5f 0.5f
                steps window 1

                Expect.equal
                    (List.ofSeq log)
                    [
                        "pointerdown", 20, 15
                        "pointermove", 40, 30
                        "pointermove", 60, 45
                        "keydown", 0, 0
                        "pointermove", 100, 75
                        "pointerup", 100, 75
                    ]
                    "every frame"

            windowCase "a button press between runs of mouse moves keeps its order and position"
            <| fun window ->
                steps window 2
                let log = mouseLog window
                move window 10.0f 10.0f
                move window 20.0f 25.0f
                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 20.0f 25.0f
                move window 30.0f 30.0f
                move window 50.0f 55.0f
                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP 50.0f 55.0f
                steps window 1

                Expect.equal
                    (List.ofSeq log)
                    [
                        "mousemove", 20, 25
                        "mousedown", 20, 25
                        "mousemove", 50, 55
                        "mouseup", 50, 55
                    ]
                    "each run's latest move before the button that ends it"

            windowCase "SDL pen and touch events reach the page"
            <| fun window ->
                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount window.Host graph pointerSquare count
                steps window 2

                let target =
                    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

                if WebCoreHost.Has "pointer-input" then
                    let mutable event = SDL_Event ()
                    event.ptouch.``type`` <- SDL_EventType.SDL_EVENT_PEN_DOWN
                    event.ptouch.windowID <- target
                    event.ptouch.which <- LanguagePrimitives.EnumOfValue 42u
                    event.ptouch.x <- 40.0f
                    event.ptouch.y <- 30.0f
                    event.ptouch.down <- SDLBool.op_Implicit true
                    event.ptouch.pen_state <- SDL_PenInputFlags.SDL_PEN_INPUT_DOWN
                    Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "pen event queued"
                    steps window 1
                    Expect.equal (graph.Run (fun () -> count.Value)) 1 "pen down reaches the page"
                    event.ptouch.``type`` <- SDL_EventType.SDL_EVENT_PEN_UP
                    event.ptouch.down <- SDLBool.op_Implicit false
                    event.ptouch.pen_state <- LanguagePrimitives.EnumOfValue 0u
                    Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "pen release queued"
                    steps window 1

                if WebCoreHost.Has "touch-input" then
                    let before = graph.Run (fun () -> count.Value)
                    let mutable event = SDL_Event ()
                    event.tfinger.``type`` <- SDL_EventType.SDL_EVENT_FINGER_DOWN
                    event.tfinger.windowID <- target
                    event.tfinger.touchID <- LanguagePrimitives.EnumOfValue 7UL
                    event.tfinger.fingerID <- LanguagePrimitives.EnumOfValue 9UL
                    event.tfinger.x <- 0.2f
                    event.tfinger.y <- 0.2f
                    event.tfinger.pressure <- 0.7f
                    Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "touch event queued"
                    steps window 1
                    Expect.equal (graph.Run (fun () -> count.Value)) (before + 1) "touch down reaches the page"

            windowCase "an XIM clear followed by committed text ends the composition with its result"
            <| fun window ->
                let input = window.Host.Document.createElement "input" :?> HTMLInputElement
                input.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:40px")
                input.value <- "abc"

                window.Host.Document.body.appendChild input
                |> ignore

                window.Host.SetFocused true
                window.Host.Click (20, 20) |> ignore
                input.setSelectionRange (1u, 2u)
                let ended = ResizeArray<string>()
                input.addEventListener ("compositionend", fun (event: Event) -> ended.Add ((event :?> CompositionEvent).data))

                let target =
                    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

                use editing = fixed (Sdl.utf8 "かな")
                use empty = fixed (Sdl.utf8 "")
                use committed = fixed (Sdl.utf8 "仮名")
                let mutable event = SDL_Event ()
                event.edit.``type`` <- SDL_EventType.SDL_EVENT_TEXT_EDITING
                event.edit.windowID <- target
                event.edit.text <- editing
                event.edit.start <- 2
                event.edit.length <- 0
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "preedit queued"
                event.edit.text <- empty
                event.edit.start <- 0
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "preedit clear queued"
                event.text.``type`` <- SDL_EventType.SDL_EVENT_TEXT_INPUT
                event.text.windowID <- target
                event.text.text <- committed
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "commit queued"
                steps window 1
                Expect.equal input.value "a仮名c" "the result replaces the selected text"
                Expect.sequenceEqual ended [| "仮名" |] "compositionend contains the committed result"

            windowCase "an unaccompanied IME clear cancels at the end of the event turn"
            <| fun window ->
                let input = window.Host.Document.createElement "input" :?> HTMLInputElement
                input.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:40px")
                input.value <- "abc"

                window.Host.Document.body.appendChild input
                |> ignore

                window.Host.SetFocused true
                window.Host.Click (20, 20) |> ignore
                input.setSelectionRange (1u, 2u)
                let ended = ResizeArray<string>()
                input.addEventListener ("compositionend", fun (event: Event) -> ended.Add ((event :?> CompositionEvent).data))

                let target =
                    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

                use editing = fixed (Sdl.utf8 "かな")
                use empty = fixed (Sdl.utf8 "")
                let mutable event = SDL_Event ()
                event.edit.``type`` <- SDL_EventType.SDL_EVENT_TEXT_EDITING
                event.edit.windowID <- target
                event.edit.text <- editing
                event.edit.start <- 2
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "preedit queued"
                event.edit.text <- empty
                event.edit.start <- 0
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "cancel queued"
                steps window 1
                Expect.equal input.value "abc" "cancellation restores the original selected text"
                Expect.equal (input.selectionStart, input.selectionEnd) (Some 1u, Some 2u) "cancellation restores the original selection"
                Expect.sequenceEqual ended [| "" |] "cancellation ends the composition"

            windowCase "focus loss cancels an active composition"
            <| fun window ->
                let input = window.Host.Document.createElement "input" :?> HTMLInputElement
                input.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:40px")
                input.value <- "abc"

                window.Host.Document.body.appendChild input
                |> ignore

                window.Host.SetFocused true
                window.Host.Click (20, 20) |> ignore
                input.setSelectionRange (1u, 2u)
                let ended = ResizeArray<string>()
                input.addEventListener ("compositionend", fun (event: Event) -> ended.Add ((event :?> CompositionEvent).data))

                let target =
                    SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)

                use editing = fixed (Sdl.utf8 "かな")
                let mutable event = SDL_Event ()
                event.edit.``type`` <- SDL_EventType.SDL_EVENT_TEXT_EDITING
                event.edit.windowID <- target
                event.edit.text <- editing
                event.edit.start <- 2
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "preedit queued"
                event.window.``type`` <- SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST
                event.window.windowID <- target
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "focus loss queued"
                steps window 1
                Expect.equal input.value "abc" "focus loss restores the original selected text"
                Expect.equal (input.selectionStart, input.selectionEnd) (Some 1u, Some 2u) "focus loss restores the original selection"
                Expect.sequenceEqual ended [| "" |] "focus loss ends the composition"

            windowCase "posted work wakes the shared loop and runs on the owner"
            <| fun window ->
                use other = Window.Create ("Posted work", 100, 80, hidden = true, renderer = "software")
                settle window
                let owner = Environment.CurrentManagedThreadId
                let mutable deliveredOn = 0
                let producer = Threading.Tasks.Task.Run (fun () ->
                    Threading.Thread.Sleep 10
                    other.Host.PostToOwner (fun () -> deliveredOn <- Environment.CurrentManagedThreadId))
                let deadline = Diagnostics.Stopwatch.StartNew ()
                while deliveredOn = 0 && deadline.ElapsedMilliseconds < 2000L do
                    window.Step 1000 |> ignore
                producer.GetAwaiter().GetResult()
                Expect.equal deliveredOn owner "other host is serviced by the shared loop"
                Expect.equal other.Host.OwnerWakeFailures 0L "SDL accepted the notification"

            windowCase "post racing window disposal cannot signal a destroyed window"
            <| fun window ->
                let other = Window.Create ("Disposed work", 100, 80, hidden = true, renderer = "software")
                let context = Threading.SynchronizationContext.Current
                let producer = Threading.Tasks.Task.Run (fun () ->
                    for _ in 1..10000 do context.Post (Threading.SendOrPostCallback ignore, null))
                (other :> IDisposable).Dispose ()
                producer.GetAwaiter().GetResult()
                steps window 2
                Expect.equal other.Host.OwnerWakeFailures 0L "retired wake is harmless"

            windowCase "another window receives queued close requests"
            <| fun window ->
                use other =
                    Window.Create ("Second window", 100, 80, hidden = true, renderer = "software")

                steps window 2
                steps other 2
                closeRequest other
                steps window 1
                Expect.isTrue other.Closed "the target window receives its close request"
                Expect.isFalse window.Closed "the pumping window stays open"

            windowCase "another window receives queued pointer input"
            <| fun window ->
                use other =
                    Window.Create ("Second window", 100, 80, hidden = true, renderer = "software")

                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount other.Host graph square count
                steps other 2
                steps window 2
                press other SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 30.0f 30.0f
                press other SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP 30.0f 30.0f
                steps window 1
                Expect.equal (graph.Run (fun () -> count.Value)) 1 "the target window receives the click"

            windowCase "global display events update every live window"
            <| fun window ->
                use other =
                    Window.Create ("Second window", 100, 80, hidden = true, renderer = "software")

                steps window 2
                steps other 2

                let display width =
                    {
                        Width = width
                        Height = 600
                        UsableLeft = 0
                        UsableTop = 0
                        UsableWidth = width
                        UsableHeight = 600
                        ColorDepth = 24
                        Orientation = ScreenOrientationType.LandscapePrimary
                    }

                window.SetDisplayReader (fun () -> ValueSome (display 800))
                other.SetDisplayReader (fun () -> ValueSome (display 1200))
                let mutable event = SDL_Event ()
                event.display.``type`` <- SDL_EventType.SDL_EVENT_DISPLAY_CURRENT_MODE_CHANGED
                event.display.displayID <- SDL3.SDL_GetPrimaryDisplay ()
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "the display event is queued"
                steps window 1
                use first = Screen.forHost window.Host
                use second = Screen.forHost other.Host
                Expect.equal (first.Read ()).Width 800 "the first host follows its display"
                Expect.equal (second.Read ()).Width 1200 "the second host follows its display"

            windowCase "an off-thread step cannot consume another window's event"
            <| fun window ->
                use other =
                    Window.Create ("Second window", 100, 80, hidden = true, renderer = "software")

                steps window 2
                steps other 2
                closeRequest other
                let mutable failure: exn = null

                let requester =
                    Threading.Thread (
                        Threading.ThreadStart (fun () ->
                            try
                                window.Step 0 |> ignore
                            with error ->
                                failure <- error)
                    )

                requester.Start ()
                Expect.isTrue (requester.Join 5000) "the rejected request terminates"
                Expect.isTrue (failure :? InvalidOperationException) "the owner-thread guard rejects the request"
                steps other 1
                Expect.isTrue other.Closed "the rejected step leaves the global queue intact"

            windowCase "a dispatch snapshot skips a window closed by an earlier callback"
            <| fun window ->
                use other =
                    Window.Create ("Closing window", 100, 80, hidden = true, renderer = "software")

                steps window 2
                steps other 2
                let mutable otherReads = 0
                let display = window.Display

                other.SetDisplayReader (fun () ->
                    otherReads <- otherReads + 1
                    display)

                window.SetDisplayReader (fun () ->
                    (other :> IDisposable).Dispose()
                    display)

                let mutable event = SDL_Event ()
                event.display.``type`` <- SDL_EventType.SDL_EVENT_DISPLAY_CURRENT_MODE_CHANGED
                event.display.displayID <- SDL3.SDL_GetPrimaryDisplay ()
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "the display event is queued"
                steps window 1
                Expect.isTrue other.Host.Session.IsDisposed "the earlier callback closed the other page"
                Expect.equal otherReads 0 "the closed target is rechecked before dispatch"

                use replacement =
                    Window.Create ("Replacement window", 100, 80, hidden = true, renderer = "software")

                closeRequest replacement
                steps window 1
                Expect.isTrue replacement.Closed "a new window receives its events after the old registration closes"

            test "window registrations keep no dead window alive" {
                let registry = WindowRegistry<obj>()
                let weak = registerEphemeral registry
                GC.Collect ()
                GC.WaitForPendingFinalizers ()
                GC.Collect ()
                let mutable value = Unchecked.defaultof<obj>
                Expect.isFalse (weak.TryGetTarget &value) "the registry holds a weak reference"
                Expect.isEmpty (registry.Snapshot ()) "snapshot prunes the dead entry"
            }

            test "failed window admission permits another thread until native initialization" {
                let owner = NativeOwner ()
                owner.Enter ()
                owner.Enter ()
                owner.Exit ()
                let mutable rejected = false

                let attempt =
                    Threading.Thread (
                        Threading.ThreadStart (fun () ->
                            try
                                owner.Enter ()
                            with :? InvalidOperationException ->
                                rejected <- true)
                    )

                attempt.Start ()
                Expect.isTrue (attempt.Join 5000) "the concurrent attempt finishes"
                Expect.isTrue rejected "a nested failed attempt retains the outer reservation"
                owner.Exit ()
                let mutable accepted = false

                let retry =
                    Threading.Thread (
                        Threading.ThreadStart (fun () ->
                            owner.Enter ()
                            accepted <- true
                            owner.Exit ())
                    )

                retry.Start ()
                Expect.isTrue (retry.Join 5000) "the retry finishes"
                Expect.isTrue accepted "a failed creation releases its tentative owner"
                owner.Enter ()
                owner.Commit ()
                owner.Exit ()
                let mutable committedRejected = false

                let other =
                    Threading.Thread (
                        Threading.ThreadStart (fun () ->
                            try
                                owner.Enter ()
                            with :? InvalidOperationException ->
                                committedRejected <- true)
                    )

                other.Start ()
                Expect.isTrue (other.Join 5000) "the committed admission check finishes"
                Expect.isTrue committedRejected "native initialization retains its owner"
            }

            windowCase "a second thread cannot start another native window"
            <| fun window ->
                steps window 2
                let mutable failure: exn = null

                let requester =
                    Threading.Thread (
                        Threading.ThreadStart (fun () ->
                            try
                                use unexpected =
                                    Window.Create ("Wrong owner", 80, 60, hidden = true, renderer = "software")

                                ()
                            with error ->
                                failure <- error)
                    )

                requester.Start ()
                Expect.isTrue (requester.Join 5000) "the rejected creation terminates"
                Expect.isTrue (failure :? InvalidOperationException) "a second native owner is rejected"
                Expect.stringContains failure.Message "same native thread" "creation reports the owner-thread contract"
                Expect.isFalse window.Host.Session.IsDisposed "the original host stays live"
                closeRequest window
                Expect.isFalse (window.Step 0) "the owner still receives queued events"

            windowCase "disposing the pumping window in a display callback ends its step"
            <| fun window ->
                steps window 2

                window.SetDisplayReader (fun () ->
                    (window :> IDisposable).Dispose()
                    ValueNone)

                let mutable event = SDL_Event ()
                event.display.``type`` <- SDL_EventType.SDL_EVENT_DISPLAY_CURRENT_MODE_CHANGED
                event.display.displayID <- SDL3.SDL_GetPrimaryDisplay ()
                Expect.isTrue (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event)) "the display event is queued"
                Expect.isFalse (window.Step 0) "a disposed window stops without cycling its destroyed host"
                Expect.isTrue window.Host.Session.IsDisposed "the callback finished page teardown"

            windowCase "a timer that disposes the host ends the window step"
            <| fun window ->
                window.Host.SetTimeout ((fun () -> (window.Host :> IDisposable).Dispose()), 0.0)
                |> ignore

                Expect.isFalse (window.Step 0) "a disposed host stops the step"

            windowCase "a frame callback that disposes the window ends presentation"
            <| fun window ->
                window.Host.RequestAnimationFrame (fun _ -> (window :> IDisposable).Dispose())
                |> ignore

                Expect.isFalse (window.Step 0) "a disposed window stops presentation"

            windowCase "a frame callback that disposes only the host ends presentation"
            <| fun window ->
                window.Host.RequestAnimationFrame (fun _ -> (window.Host :> IDisposable).Dispose())
                |> ignore

                Expect.isFalse (window.Step 0) "a disposed host stops presentation"

            test "keys map to DOM codes and names" {
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_A) "KeyA" "a letter"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_0) "Digit0" "zero"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_7) "Digit7" "a digit"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_F13) "F13" "a high function key"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_KP_5) "Numpad5" "a keypad digit"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_RALT) "AltRight" "a modifier"
                Expect.equal (Keys.key SDL_Scancode.SDL_SCANCODE_LEFT SDL_Keymod.SDL_KMOD_NONE) "ArrowLeft" "a named key"
                Expect.equal (Keys.key SDL_Scancode.SDL_SCANCODE_F5 SDL_Keymod.SDL_KMOD_NONE) "F5" "a function key's name"

                Expect.equal
                    (Keys.modifiers (
                        SDL_Keymod.SDL_KMOD_LSHIFT
                        ||| SDL_Keymod.SDL_KMOD_RCTRL
                    ))
                    3
                    "shift and control bits"
            }

            test "input method offsets count code points as UTF-16" {
                Expect.equal (Sdl.utf16Offset "a😀b" 2) 3 "a surrogate pair is two units"
                Expect.equal (Sdl.utf16Offset "abc" 9) 3 "an offset past the end clamps"
                Expect.equal (Sdl.compositionOffset true "a😀b" 3) 3 "Windows SDL already reports UTF-16 units"
                Expect.equal (Sdl.compositionOffset false "a😀b" 2) 3 "X11 SDL reports Unicode characters"
                Expect.equal (Sdl.compositionOffset true "a😀b" 9) 4 "a Windows offset clamps at the end"
            }

            test "touch frames retain stationary contacts and cancel on focus loss" {
                let touches = TouchFrames ()
                let first = touches.Update (1UL, 2UL, 1, 40.0, 30.0, 0.7f, 0)
                let second = touches.Update (1UL, 3UL, 1, 20.0, 10.0, 1.2f, 0)
                Expect.equal first.Length 1 "down emits its contact"
                Expect.equal second.Length 2 "the frame contains both contacts"
                Expect.equal second[0].Action 4 "the earlier contact is stationary"
                Expect.equal second[1].Pressure 1.0f "pressure is normalized"
                let released = touches.Update (1UL, 2UL, 2, 42.0, 30.0, 0.0f, 0)
                Expect.equal released[1].Id first[0].Id "the same contact keeps its identifier"
                Expect.equal released[1].MovementX 2.0 "movement is in CSS pixels"
                let cancelled = touches.Cancel 0
                Expect.equal cancelled.Length 1 "the remaining contact is cancelled"
                Expect.equal cancelled[0].Action 3 "focus loss sends cancellation"
                Expect.isEmpty (touches.Cancel 0) "cancel clears the frame"
            }

            test "IME surrounding offsets preserve scalar boundaries" {
                let text = "前😀中後"
                Expect.equal (SurroundingText.scalarOffset text 3) (Some 2u) "a surrogate pair counts once"
                Expect.equal (SurroundingText.scalarOffset text 2) None "a split surrogate is unavailable"
                Expect.equal (SurroundingText.scalarOffset ("a" + string (char 0xd800)) 1) None "invalid UTF-16 is unavailable"
                Expect.equal (SurroundingText.scalarOffset "a\000b" 1) None "NUL cannot be sent through IBus"
                Expect.equal (SurroundingText.deleteRange "A😀BC" 2u -1 1u) (Some (1, 3)) "a scalar deletion spans the whole surrogate pair"
                Expect.equal (SurroundingText.deleteRange "abc" 0u -1 1u) None "a deletion before the context is rejected"
            }

            test "pen samples retain axes and cancel an active contact" {
                let pens = PenSamples ()

                let pressure =
                    pens.Update (42u, 0, 10.0, 20.0, -1, 0, 0, Some (SDL_PenAxis.SDL_PEN_AXIS_PRESSURE, 0.8f))

                let down = pens.Update (42u, 1, 12.0, 20.0, 0, 1, 0, None)
                Expect.equal down.Id pressure.Id "the pen keeps its identifier"
                Expect.equal down.Pressure 0.8f "pressure survives the next event"
                Expect.equal down.MovementX 2.0 "movement is in CSS pixels"
                let cancelled = pens.Cancel 0
                Expect.equal cancelled.Length 1 "the active pen is cancelled"
                Expect.equal cancelled[0].Action 3 "the contact ends"
            }
        ]
