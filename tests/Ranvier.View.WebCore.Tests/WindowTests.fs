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
            }
        ]
