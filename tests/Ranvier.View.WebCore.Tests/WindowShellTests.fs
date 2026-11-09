module Ranvier.View.WebCore.Tests.WindowShellTests

open System
open System.Runtime.InteropServices
open Expecto
open Ranvier.Dom
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

[<Struct; StructLayout(LayoutKind.Sequential)>]
type private Point =
    val mutable x: int
    val mutable y: int

[<DllImport("user32.dll", EntryPoint = "SendMessageW")>]
extern nativeint private SendMessage(nativeint hwnd, uint32 message, nativeint wParam, nativeint lParam)

[<DllImport("user32.dll")>]
extern bool private ClientToScreen(nativeint hwnd, Point& point)

[<DllImport("dwmapi.dll")>]
extern int private DwmGetWindowAttribute(nativeint hwnd, int attribute, int& value, int size)

// A page 300 by 200 with a title bar along the top: a drag region holding a span, a content tab and a maximize button.
let private page =
    "<style>html,body{margin:0}*{position:absolute}</style>"
    + "<div id='bar' data-window-region='drag' style='left:0;top:0;width:300px;height:40px'>"
    + "<span id='label' style='left:100px;top:10px;width:40px;height:20px'></span>"
    + "<button id='tab' data-window-region='content' style='left:20px;top:5px;width:30px;height:30px'></button>"
    + "<button id='max' data-window-region='maximize' style='left:200px;top:5px;width:30px;height:30px'></button>"
    + "</div>"

// A hidden window drawn by SDL's software renderer, on the WebCore host thread.
let private shellCase (name: string) (body: Window -> unit) =
    testCase name (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("Ranvier shell", 300, 200, hidden = true, renderer = "software", frame = WindowFrame.Custom 6)

            window.Host.Document.body.innerHTML <- page
            body window
            Expect.isEmpty (List.ofSeq window.Host.Errors) "listener, microtask and timer errors"))

let private windowsCase (name: string) (body: Window -> unit) =
    shellCase name (fun window ->
        if not (OperatingSystem.IsWindows ()) then
            skiptest "Windows only"

        body window)

let private nativeWindow (window: Window) =
    NativePtr.ofNativeInt<SDL_Window> window.SdlWindow

let private push (event: SDL_Event) =
    let mutable event = event

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event): bool) then
        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

let private windowEvent (window: Window) (kind: SDL_EventType) (data: int) =
    let mutable event = SDL_Event ()
    event.window.``type`` <- kind
    event.window.windowID <- SDL3.SDL_GetWindowID (nativeWindow window)
    event.window.data1 <- data
    push event

// The tick SDL raises during the platform's modal move or resize loop.
let private modalTick (window: Window) =
    windowEvent window SDL_EventType.SDL_EVENT_WINDOW_EXPOSED 1

let private rightButton (window: Window) (down: bool) x y =
    let mutable event = SDL_Event ()

    event.button.``type`` <-
        if down then
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
        else
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP

    event.button.windowID <- SDL3.SDL_GetWindowID (nativeWindow window)
    event.button.button <- byte SDL3.SDL_BUTTON_RIGHT
    event.button.down <- SDLBool.op_Implicit down
    event.button.clicks <- 1uy
    event.button.x <- x
    event.button.y <- y
    push event

let private steps (window: Window) count =
    for _ in 1..count do
        window.Step 0 |> ignore

// Counts the runs of an effect on the window's graph.
let private watch (window: Window) (read: unit -> unit) =
    let mutable runs = 0

    window.Graph.Run (fun () ->
        Api.createRoot (fun _ ->
            Api.createEffect (fun () ->
                read ()
                runs <- runs + 1)))

    fun () -> runs

let private frameCounter (window: Window) =
    let mutable frames = 0

    window.Host.RequestAnimationFrame (fun _ -> frames <- frames + 1)
    |> ignore

    fun () -> frames

let private mouseLog (window: Window) =
    let log = ResizeArray<string>()

    for kind in [ "mousemove"; "mousedown"; "mouseup"; "click" ] do
        window.Host.Document.addEventListener (
            kind,
            fun (event: Event) ->
                let mouse = unbox<MouseEvent> event
                log.Add (kind + " " + mouse.clientX.ToString () + "," + mouse.clientY.ToString ())
        )

    log

// Win32 messages, sent to the window's real procedure.
let private WM_MOUSEMOVE = 0x0200u
let private WM_LBUTTONUP = 0x0202u
let private WM_NCHITTEST = 0x0084u
let private WM_NCMOUSEMOVE = 0x00A0u
let private WM_NCLBUTTONDOWN = 0x00A1u
let private WM_NCLBUTTONUP = 0x00A2u
let private WM_CAPTURECHANGED = 0x0215u
let private HTCLIENT = 1n
let private HTCAPTION = 2n
let private HTMAXBUTTON = 9n
let private HTTOP = 12n

let private packed x y =
    nativeint ((y &&& 0xFFFF) <<< 16 ||| (x &&& 0xFFFF))

let private screenPacked (window: Window) x y =
    let mutable point = Point ()
    point.x <- x
    point.y <- y
    ClientToScreen (window.NativeHandle, &point) |> ignore
    packed point.x point.y

let private hitTestMessage (window: Window) x y =
    SendMessage (window.NativeHandle, WM_NCHITTEST, 0n, screenPacked window x y)

let private nonClient (window: Window) message x y =
    SendMessage (window.NativeHandle, message, HTMAXBUTTON, screenPacked window x y)
    |> ignore

let private client (window: Window) message x y =
    SendMessage (window.NativeHandle, message, 0n, packed x y) |> ignore

[<Tests>]
let tests =
    testSequenced
    <| testList
        "Window shell"
        [
            test "region attribute values parse, with unknown values as content" {
                Expect.equal (WindowRegionAttribute.parse "drag") WindowRegion.Drag "drag"
                Expect.equal (WindowRegionAttribute.parse "maximize") WindowRegion.Maximize "maximize"
                Expect.equal (WindowRegionAttribute.parse "content") WindowRegion.Content "content"
                Expect.equal (WindowRegionAttribute.parse "dragg") WindowRegion.Content "a typo"
                Expect.equal (WindowRegionAttribute.parse null) WindowRegion.Content "no value"
            }

            test "a region's attribute value parses back to the region" {
                for region in [ WindowRegion.Content; WindowRegion.Drag; WindowRegion.Maximize ] do
                    Expect.equal (WindowRegionAttribute.parse (region.ToString ())) region (region.ToString ())
            }

            test "placement comes from SDL's window flags, with minimized first" {
                let placement = WindowHit.placement
                Expect.equal (placement (LanguagePrimitives.EnumOfValue 0UL)) WindowPlacement.Normal "no flags"
                Expect.equal (placement SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) WindowPlacement.Maximized "maximized"
                Expect.equal (placement SDL_WindowFlags.SDL_WINDOW_MINIMIZED) WindowPlacement.Minimized "minimized"
                Expect.equal (placement SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) WindowPlacement.Fullscreen "fullscreen"

                Expect.equal
                    (placement (SDL_WindowFlags.SDL_WINDOW_MINIMIZED ||| SDL_WindowFlags.SDL_WINDOW_MAXIMIZED))
                    WindowPlacement.Minimized
                    "a minimized maximized window"

                Expect.equal
                    (placement (SDL_WindowFlags.SDL_WINDOW_FULLSCREEN ||| SDL_WindowFlags.SDL_WINDOW_MAXIMIZED))
                    WindowPlacement.Fullscreen
                    "a fullscreen maximized window"
            }

            test "resize edges cover the border, and corners take both edges" {
                let at = WindowHit.resize WindowPlacement.Normal 6 300 200

                for x, y, expected in
                    [
                        0, 0, SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPLEFT
                        299, 0, SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPRIGHT
                        0, 199, SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMLEFT
                        299, 199, SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMRIGHT
                        150, 5, SDL_HitTestResult.SDL_HITTEST_RESIZE_TOP
                        150, 194, SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOM
                        5, 100, SDL_HitTestResult.SDL_HITTEST_RESIZE_LEFT
                        294, 100, SDL_HitTestResult.SDL_HITTEST_RESIZE_RIGHT
                    ] do
                    Expect.equal (at x y) (ValueSome expected) $"{x},{y}"

                Expect.equal (at 6 6) ValueNone "just inside the top left"
                Expect.equal (at 293 193) ValueNone "just inside the bottom right"
            }

            test "only a normal window has resize edges" {
                for placement in [ WindowPlacement.Maximized; WindowPlacement.Minimized; WindowPlacement.Fullscreen ] do
                    Expect.equal (WindowHit.resize placement 6 300 200 0 0) ValueNone (placement.ToString ())
            }

            shellCase "an element's region comes from its nearest marked ancestor"
            <| fun window ->
                Expect.equal (window.RegionAt (70.0f, 20.0f)) WindowRegion.Drag "the bar"
                Expect.equal (window.RegionAt (110.0f, 20.0f)) WindowRegion.Drag "an unmarked child"
                Expect.equal (window.RegionAt (30.0f, 20.0f)) WindowRegion.Content "content inside the bar"
                Expect.equal (window.RegionAt (210.0f, 20.0f)) WindowRegion.Maximize "the maximize button"
                Expect.equal (window.RegionAt (100.0f, 100.0f)) WindowRegion.Content "an unmarked page"

            shellCase "a region reflects the page at the time of the query"
            <| fun window ->
                let document = window.Host.Document
                Expect.equal (window.RegionAt (70.0f, 20.0f)) WindowRegion.Drag "before"

                let overlay = document.createElement "div"
                overlay.setAttribute ("style", "position:fixed;left:0;top:0;width:300px;height:200px")
                document.body.appendChild overlay |> ignore
                Expect.equal (window.RegionAt (70.0f, 20.0f)) WindowRegion.Content "an overlay covers the bar"

                overlay.remove ()
                (document.getElementById "bar").setAttribute ("style", "left:0;top:50px;width:300px;height:40px")
                Expect.equal (window.RegionAt (70.0f, 70.0f)) WindowRegion.Drag "the bar moved"
                Expect.equal (window.RegionAt (70.0f, 20.0f)) WindowRegion.Content "where the bar was"

            shellCase "the hit test puts the resize border ahead of the page"
            <| fun window ->
                Expect.equal (window.HitTest (70, 2)) SDL_HitTestResult.SDL_HITTEST_RESIZE_TOP "the border over the bar"
                Expect.equal (window.HitTest (70, 20)) SDL_HitTestResult.SDL_HITTEST_DRAGGABLE "the bar"
                Expect.equal (window.HitTest (30, 20)) SDL_HitTestResult.SDL_HITTEST_NORMAL "content in the bar"
                Expect.equal (window.HitTest (100, 100)) SDL_HitTestResult.SDL_HITTEST_NORMAL "the page"

            shellCase "a maximized window's edge belongs to the page"
            <| fun window ->
                window.SetPlacementReader (fun () -> WindowPlacement.Maximized)
                Expect.equal (window.HitTest (70, 2)) SDL_HitTestResult.SDL_HITTEST_DRAGGABLE "the bar reaches the edge"

            testCase "a custom frame is borderless and a native frame is not" (fun () ->
                onHostThread (fun () ->
                    let borderless (window: Window) =
                        SDL3.SDL_GetWindowFlags (nativeWindow window)
                        &&& SDL_WindowFlags.SDL_WINDOW_BORDERLESS
                        <> LanguagePrimitives.EnumOfValue 0UL

                    use custom =
                        Window.Create ("Custom", 100, 80, hidden = true, renderer = "software", frame = WindowFrame.Custom 4)

                    use native = Window.Create ("Native", 100, 80, hidden = true, renderer = "software")
                    Expect.isTrue (borderless custom) "custom"
                    Expect.equal custom.Frame (WindowFrame.Custom 4) "custom's frame"
                    Expect.isFalse (borderless native) "native"
                    Expect.equal native.Frame WindowFrame.Native "the default frame"))

            test "a negative resize border is rejected" {
                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Window.Create ("Bad", 100, 80, hidden = true, frame = WindowFrame.Custom -1) |> ignore)
                    "a negative border"
            }

            testCase "the minimum size reaches the window" (fun () ->
                onHostThread (fun () ->
                    use window =
                        Window.Create ("Minimum", 300, 200, hidden = true, renderer = "software", minimumSize = struct (120, 90))

                    let mutable width = 0
                    let mutable height = 0

                    SDL3.SDL_GetWindowMinimumSize (nativeWindow window, &&width, &&height)
                    |> ignore

                    Expect.equal (width, height) (120, 90) "the minimum"))

            shellCase "the title reads back, including non-ASCII text"
            <| fun window ->
                Expect.equal window.Title "Ranvier shell" "the created title"
                window.Title <- "Ränvier ✕"
                Expect.equal window.Title "Ränvier ✕" "the set title"

            shellCase "an icon must have four bytes per pixel"
            <| fun window ->
                window.SetIcon (2, 2, Array.zeroCreate 16)

                Expect.throwsT<ArgumentException> (fun () -> window.SetIcon (2, 2, Array.zeroCreate 15)) "too few bytes"

            shellCase "focused follows the window's focus events and wakes effects on its graph"
            <| fun window ->
                let runs = watch window (fun () -> window.Focused.Value |> ignore)
                let before = runs ()
                windowEvent window SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED 0
                steps window 1
                Expect.isTrue window.Focused.Value "gained"
                Expect.isGreaterThan (runs ()) before "the effect ran"
                windowEvent window SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST 0
                steps window 1
                Expect.isFalse window.Focused.Value "lost"

            shellCase "placement follows the window's state events and wakes effects on its graph"
            <| fun window ->
                let mutable current = WindowPlacement.Normal
                window.SetPlacementReader (fun () -> current)
                let seen = ResizeArray<WindowPlacement>()
                watch window (fun () -> seen.Add window.Placement.Value) |> ignore

                for placement, kind in
                    [
                        WindowPlacement.Maximized, SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED
                        WindowPlacement.Normal, SDL_EventType.SDL_EVENT_WINDOW_RESTORED
                        WindowPlacement.Minimized, SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED
                        WindowPlacement.Fullscreen, SDL_EventType.SDL_EVENT_WINDOW_ENTER_FULLSCREEN
                        WindowPlacement.Normal, SDL_EventType.SDL_EVENT_WINDOW_LEAVE_FULLSCREEN
                    ] do
                    current <- placement
                    windowEvent window kind 0
                    steps window 1
                    Expect.equal window.Placement.Value placement (placement.ToString ())

                Expect.sequenceEqual
                    seen
                    [
                        WindowPlacement.Normal
                        WindowPlacement.Maximized
                        WindowPlacement.Normal
                        WindowPlacement.Minimized
                        WindowPlacement.Fullscreen
                        WindowPlacement.Normal
                    ]
                    "one effect run per change"

            shellCase "the placement and focus accessors have no setter"
            <| fun window ->
                Expect.isNull (window.Placement.GetType().GetProperty("Value").SetMethod) "read-only placement"
                Expect.isNull (window.Focused.GetType().GetProperty("Value").SetMethod) "read-only focus"

            shellCase "a right click on a drag region opens the window menu at the pointer"
            <| fun window ->
                let menus = ResizeArray<struct (int * int)>()
                window.SetSystemMenu (fun x y -> menus.Add (struct (x, y)))
                rightButton window true 70.0f 20.0f
                rightButton window false 70.0f 20.0f
                steps window 1
                Expect.sequenceEqual menus [ struct (70, 20) ] "the title bar"

                rightButton window true 100.0f 100.0f
                rightButton window false 100.0f 100.0f
                steps window 1
                Expect.equal menus.Count 1 "page content keeps the page's right click"

            testCase "a native frame leaves right clicks to the page" (fun () ->
                onHostThread (fun () ->
                    use window = Window.Create ("Native", 300, 200, hidden = true, renderer = "software")
                    window.Host.Document.body.innerHTML <- page
                    let menus = ResizeArray<struct (int * int)>()
                    window.SetSystemMenu (fun x y -> menus.Add (struct (x, y)))
                    rightButton window true 70.0f 20.0f
                    rightButton window false 70.0f 20.0f
                    steps window 1
                    Expect.isEmpty menus "no menu"))

            shellCase "ShowSystemMenu opens the menu at the position"
            <| fun window ->
                let menus = ResizeArray<struct (int * int)>()
                window.SetSystemMenu (fun x y -> menus.Add (struct (x, y)))
                window.ShowSystemMenu (40, 30)
                Expect.sequenceEqual menus [ struct (40, 30) ] "the position"

            shellCase "a modal tick runs the page's frame without a step"
            <| fun window ->
                steps window 2
                let frames = frameCounter window
                modalTick window
                Expect.equal (frames ()) 1 "the frame ran inside the tick"

            shellCase "an ordinary expose leaves the frame to the next step"
            <| fun window ->
                steps window 2
                let frames = frameCounter window
                windowEvent window SDL_EventType.SDL_EVENT_WINDOW_EXPOSED 0
                Expect.equal (frames ()) 0 "no frame before a step"
                steps window 1
                Expect.equal (frames ()) 1 "the step ran it"

            shellCase "a modal tick for another window runs no frame here"
            <| fun window ->
                steps window 2
                let frames = frameCounter window
                use other = Window.Create ("Other", 100, 80, hidden = true, renderer = "software")
                steps other 2
                modalTick other
                Expect.equal (frames ()) 0 "the other window's tick"

            shellCase "a modal tick applies a pending size"
            <| fun window ->
                steps window 2
                let before = window.ResizesApplied
                windowEvent window SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED 0
                Expect.equal window.ResizesApplied before "no resize until the tick"
                modalTick window
                Expect.equal window.ResizesApplied (before + 1) "the tick resized the page"
                modalTick window
                Expect.equal window.ResizesApplied (before + 1) "one resize per change"

            shellCase "a modal tick with a page frame on the stack waits for the next turn"
            <| fun window ->
                steps window 2
                let mutable depth = 0
                let mutable deepest = 0

                let rec tick (_: float) =
                    depth <- depth + 1
                    deepest <- max deepest depth
                    window.Host.RequestAnimationFrame tick |> ignore
                    modalTick window
                    depth <- depth - 1

                window.Host.RequestAnimationFrame tick |> ignore
                steps window 2
                Expect.equal deepest 1 "frames never nest"

            shellCase "a closed window ignores modal ticks"
            <| fun window ->
                steps window 2
                let frames = frameCounter window
                window.Close ()
                modalTick window
                Expect.equal (frames ()) 0 "no frame after close"

            shellCase "a frame callback that closes the window during a modal tick ends the next step"
            <| fun window ->
                steps window 2

                window.Host.RequestAnimationFrame (fun _ -> window.Close ())
                |> ignore

                modalTick window
                Expect.isTrue window.Closed "the callback ran"
                Expect.isFalse (window.Step 0) "the step reports the close"

            shellCase "a frame callback can dispose the window during a modal tick"
            <| fun window ->
                steps window 2

                window.Host.RequestAnimationFrame (fun _ -> (window :> IDisposable).Dispose())
                |> ignore

                modalTick window
                Expect.isTrue window.Closed "the window stops at once"
                Expect.isFalse window.Host.Session.IsDisposed "teardown waits for SDL's callback to return"
                Expect.isFalse (window.Step 0) "the step reports the close"
                Expect.isTrue window.Host.Session.IsDisposed "the step finished teardown"

            testCase "windowRegion writes the attribute the hit test reads" (fun () ->
                onHostThread (fun () ->
                    let bar =
                        Component.define (fun _ (_: unit) -> ()) (fun t ->
                            t.div
                                [|
                                    t.Attrs.windowRegion WindowRegion.Drag
                                    t.Attrs.attribute ("style", "position:absolute;left:0;top:0;width:300px;height:40px")
                                |]
                                [||])

                    use window =
                        Window.Create ("Region", 300, 200, hidden = true, renderer = "software", frame = WindowFrame.Custom 6)

                    let container = window.Host.Document.createElement "div"
                    window.Host.Document.body.appendChild container |> ignore

                    let mounted =
                        Mounting.mount window.Graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
                            Program.run (Component.render bar ()) context.Backend)

                    Expect.equal (window.RegionAt (100.0f, 20.0f)) WindowRegion.Drag "the marked element"
                    window.Graph.Run (fun () -> mounted.Dispose ())))

            shellCase "a failure in a modal tick surfaces from the next step"
            <| fun window ->
                steps window 2
                window.SetModalFrame (fun () -> failwith "modal failure")
                modalTick window
                Expect.throwsT<Exception> (fun () -> window.Step 0 |> ignore) "the step rethrows"
                window.SetModalFrame ignore
                Expect.isTrue (window.Step 0) "later steps run"

            windowsCase "the window procedure reports each region to Windows"
            <| fun window ->
                Expect.equal (hitTestMessage window 210 20) HTMAXBUTTON "the maximize button"
                Expect.equal (hitTestMessage window 70 20) HTCAPTION "the bar"
                Expect.equal (hitTestMessage window 30 20) HTCLIENT "content in the bar"
                Expect.equal (hitTestMessage window 100 100) HTCLIENT "the page"
                Expect.equal (hitTestMessage window 210 2) HTTOP "the border over the button's column"

            windowsCase "a custom frame asks the compositor for rounded corners"
            <| fun window ->
                let mutable preference = 0
                // DWMWA_WINDOW_CORNER_PREFERENCE exists from Windows 11.
                if DwmGetWindowAttribute (window.NativeHandle, 33, &preference, 4) = 0 then
                    Expect.equal preference 2 "DWMWCP_ROUND"

            windowsCase "one hit test message reads the page once"
            <| fun window ->
                let before = window.RegionReads
                hitTestMessage window 70 20 |> ignore
                Expect.equal (window.RegionReads - before) 1L "the bar"

            windowsCase "the maximize button receives hover, press and click as a page button"
            <| fun window ->
                let log = mouseLog window
                nonClient window WM_NCMOUSEMOVE 210 20
                nonClient window WM_NCLBUTTONDOWN 210 20
                nonClient window WM_NCLBUTTONUP 210 20

                Expect.sequenceEqual
                    log
                    [ "mousemove 210,20"; "mousedown 210,20"; "mouseup 210,20"; "click 210,20" ]
                    "the page's events"

            windowsCase "releasing a press on the maximize button elsewhere clicks nothing"
            <| fun window ->
                let log = mouseLog window
                nonClient window WM_NCLBUTTONDOWN 210 20
                client window WM_MOUSEMOVE 100 100
                client window WM_LBUTTONUP 100 100

                Expect.sequenceEqual
                    (log |> Seq.filter (fun line -> not (line.StartsWith "click")))
                    [ "mousedown 210,20"; "mousemove 100,100"; "mouseup 100,100" ]
                    "the press follows the pointer"

                Expect.isFalse (log |> Seq.exists (fun line -> line = "click 210,20")) "no click on the button"

            windowsCase "a maximize click that disposes the window finishes teardown in the next step"
            <| fun window ->
                (window.Host.Document.getElementById "max")
                    .addEventListener ("click", fun (_: Event) -> (window :> IDisposable).Dispose())

                nonClient window WM_NCLBUTTONDOWN 210 20
                nonClient window WM_NCLBUTTONUP 210 20
                Expect.isTrue window.Closed "the window stops at once"
                Expect.isFalse window.Host.Session.IsDisposed "teardown waits for the window procedure to return"
                Expect.isFalse (window.Step 0) "the step reports the close"
                Expect.isTrue window.Host.Session.IsDisposed "the step finished teardown"

            windowsCase "losing capture ends a press on the maximize button"
            <| fun window ->
                let log = mouseLog window
                nonClient window WM_NCLBUTTONDOWN 210 20
                client window WM_CAPTURECHANGED 0 0
                Expect.equal (log |> Seq.filter (fun line -> line.StartsWith "mouseup") |> Seq.length) 1 "one release"
                client window WM_LBUTTONUP 210 20
                Expect.equal (log |> Seq.filter (fun line -> line.StartsWith "mouseup") |> Seq.length) 1 "the press is over"
        ]
