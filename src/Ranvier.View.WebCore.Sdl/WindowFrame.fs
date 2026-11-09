namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open SDL
open Ranvier.View

/// <summary>Who draws the window's frame.</summary>
[<RequireQualifiedAccess; System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
type WindowFrame =
    /// <summary>The platform's title bar and borders.</summary>
    | Native
    /// <summary>
    /// The page draws the frame. Elements marked with <c>t.Attrs.windowRegion</c> form the title bar, and the outer
    /// <c>resizeBorder</c> window units resize the window.
    /// </summary>
    | Custom of resizeBorder: int

    // A generated display uses sprintf "%A", which native AOT rejects. The DebuggerDisplay attribute replaces its other use.
    override this.ToString() =
        match this with
        | Native -> "Native"
        | Custom border -> "Custom " + border.ToString ()

/// <summary>How the window occupies its display.</summary>
[<RequireQualifiedAccess>]
type WindowPlacement =
    | Normal
    | Minimized
    | Maximized
    | Fullscreen

    override this.ToString() =
        match this with
        | Normal -> "Normal"
        | Minimized -> "Minimized"
        | Maximized -> "Maximized"
        | Fullscreen -> "Fullscreen"

/// <summary>The role of a page element in a custom frame. The nearest marked ancestor decides an element's region.</summary>
[<RequireQualifiedAccess; Struct>]
type WindowRegion =
    /// <summary>Page content: the page receives the input.</summary>
    | Content
    /// <summary>Moves the window, snaps it, and maximizes it on a double click.</summary>
    | Drag
    /// <summary>The window's maximize button: the page receives the input, and Windows 11 shows Snap Layouts on hover.</summary>
    | Maximize

    override this.ToString() =
        match this with
        | Content -> "content"
        | Drag -> "drag"
        | Maximize -> "maximize"

module internal WindowRegionAttribute =
    [<Literal>]
    let Name = "data-window-region"

    [<Literal>]
    let Selector = "[data-window-region]"

    /// <summary>The region for an attribute value. Unknown values are <c>Content</c>.</summary>
    let parse (value: string) =
        match value with
        | "drag" -> WindowRegion.Drag
        | "maximize" -> WindowRegion.Maximize
        | _ -> WindowRegion.Content

/// <summary>Template parts for custom window frames.</summary>
[<AutoOpen>]
module WindowFrameTemplate =
    type AttributeBuilder<'inst> with
        /// <summary>Marks the element and its descendants as <c>region</c> in a custom frame.</summary>
        member this.windowRegion(region: WindowRegion) : Part<'native, 'inst> =
            this.attribute (WindowRegionAttribute.Name, region.ToString ())

module internal WindowHit =
    /// <summary>The placement for SDL window flags.</summary>
    let placement (flags: SDL_WindowFlags) =
        let has (flag: SDL_WindowFlags) =
            flags &&& flag
            <> LanguagePrimitives.EnumOfValue 0UL

        if has SDL_WindowFlags.SDL_WINDOW_MINIMIZED then
            WindowPlacement.Minimized
        elif has SDL_WindowFlags.SDL_WINDOW_FULLSCREEN then
            WindowPlacement.Fullscreen
        elif has SDL_WindowFlags.SDL_WINDOW_MAXIMIZED then
            WindowPlacement.Maximized
        else
            WindowPlacement.Normal

    /// <summary>
    /// The resize result at a window position, in a window of <c>width</c> by <c>height</c>. Only a <c>Normal</c>
    /// window has resize edges.
    /// </summary>
    let resize (placement: WindowPlacement) (border: int) (width: int) (height: int) (x: int) (y: int) : SDL_HitTestResult voption =
        if placement <> WindowPlacement.Normal then
            ValueNone
        else
            let left = x < border
            let right = x >= width - border
            let top = y < border
            let bottom = y >= height - border

            if top && left then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPLEFT
            elif top && right then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPRIGHT
            elif bottom && right then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMRIGHT
            elif bottom && left then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMLEFT
            elif top then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_TOP
            elif right then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_RIGHT
            elif bottom then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOM
            elif left then
                ValueSome SDL_HitTestResult.SDL_HITTEST_RESIZE_LEFT
            else
                ValueNone

[<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
type internal HitTestCallback = delegate of nativeint * nativeint * nativeint -> SDL_HitTestResult

[<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
type internal EventWatchCallback = delegate of nativeint * nativeint -> byte

[<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
type internal WindowProcedure = delegate of nativeint * uint32 * nativeint * nativeint -> nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal Win32Point =
    val mutable x: int
    val mutable y: int

[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal Win32TrackMouse =
    val mutable cbSize: uint32
    val mutable dwFlags: uint32
    val mutable hwndTrack: nativeint
    val mutable dwHoverTime: uint32

module internal Win32Native =
    [<DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")>]
    extern nativeint SetWindowLongPtr(nativeint hwnd, int index, nativeint value)

    [<DllImport("user32.dll", EntryPoint = "CallWindowProcW")>]
    extern nativeint CallWindowProc(nativeint previous, nativeint hwnd, uint32 message, nativeint wParam, nativeint lParam)

    [<DllImport("user32.dll")>]
    extern bool ScreenToClient(nativeint hwnd, Win32Point& point)

    [<DllImport("user32.dll")>]
    extern bool TrackMouseEvent(Win32TrackMouse& track)

    [<DllImport("user32.dll")>]
    extern nativeint SetCapture(nativeint hwnd)

    [<DllImport("user32.dll")>]
    extern bool ReleaseCapture()

    [<DllImport("dwmapi.dll")>]
    extern int DwmSetWindowAttribute(nativeint hwnd, int attribute, int& value, int size)

    /// <summary>Gives a borderless window Windows 11's rounded corners and the compositor's shadow. Earlier Windows versions keep square corners.</summary>
    /// <remarks><c>DwmExtendFrameIntoClientArea</c> gives the same frame and suppresses the Snap Layouts flyout.</remarks>
    let applyCompositorFrame (hwnd: nativeint) =
        // DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND.
        let mutable round = 2

        DwmSetWindowAttribute (hwnd, 33, &round, 4)
        |> ignore

/// <summary>
/// Reports a Windows window's <c>Maximize</c> region as its maximize button, the condition for Snap Layouts, and returns
/// that button's mouse input to the page. A press takes mouse capture, so the page receives the release wherever it lands.
/// </summary>
/// <remarks>
/// SDL's hit test has no maximize-button result, so this subclass answers <c>WM_NCHITTEST</c> ahead of SDL.
/// <c>regionAt</c> takes client coordinates. <c>mouse</c> takes an RV_MOUSE_* action, client coordinates, an RV_BUTTON_*
/// button or -1, and a click count.
/// </remarks>
[<Sealed>]
type internal SnapButtonSubclass
    (hwnd: nativeint, regionAt: int -> int -> WindowRegion, mouse: int -> int -> int -> int -> int -> unit, leave: unit -> unit) as this =
    [<Literal>]
    let GWLP_WNDPROC = -4

    [<Literal>]
    let WM_NCHITTEST = 0x0084u

    [<Literal>]
    let WM_NCMOUSEMOVE = 0x00A0u

    [<Literal>]
    let WM_NCLBUTTONDOWN = 0x00A1u

    [<Literal>]
    let WM_NCLBUTTONUP = 0x00A2u

    [<Literal>]
    let WM_NCLBUTTONDBLCLK = 0x00A3u

    [<Literal>]
    let WM_MOUSEMOVE = 0x0200u

    [<Literal>]
    let WM_LBUTTONUP = 0x0202u

    [<Literal>]
    let WM_CAPTURECHANGED = 0x0215u

    [<Literal>]
    let WM_NCMOUSELEAVE = 0x02A2u

    // RV_MOUSE_*.
    [<Literal>]
    let MouseMove = 0

    [<Literal>]
    let MouseDown = 1

    [<Literal>]
    let MouseUp = 2

    let HTMAXBUTTON = 9n

    let procedure =
        WindowProcedure (fun hwnd message wParam lParam -> this.Procedure (hwnd, message, wParam, lParam))

    let mutable previous = 0n
    let mutable overButton = false
    let mutable pressed = false
    let mutable answering: WindowRegion voption = ValueNone

    let packedPoint (lParam: nativeint) =
        let packed = int64 lParam
        struct (int (int16 (packed &&& 0xFFFFL)), int (int16 ((packed >>> 16) &&& 0xFFFFL)))

    let clientFromScreen (lParam: nativeint) =
        let struct (x, y) = packedPoint lParam
        let mutable point = Win32Point ()
        point.x <- x
        point.y <- y

        Win32Native.ScreenToClient (hwnd, &point)
        |> ignore

        struct (point.x, point.y)

    let trackLeave () =
        let mutable request = Win32TrackMouse ()
        request.cbSize <- uint32 (Marshal.SizeOf<Win32TrackMouse>())
        // TME_LEAVE ||| TME_NONCLIENT.
        request.dwFlags <- 0x00000012u
        request.hwndTrack <- hwnd
        Win32Native.TrackMouseEvent (&request) |> ignore

    /// <summary>The region evaluated for the <c>WM_NCHITTEST</c> being forwarded, for SDL's hit test beneath it.</summary>
    member _.Answering = answering

    member private _.Procedure(hwnd: nativeint, message: uint32, wParam: nativeint, lParam: nativeint) : nativeint =
        let forward () =
            Win32Native.CallWindowProc (previous, hwnd, message, wParam, lParam)

        match message with
        | WM_NCHITTEST ->
            let struct (x, y) = clientFromScreen lParam
            let region = regionAt x y

            if region = WindowRegion.Maximize then
                HTMAXBUTTON
            else
                answering <- ValueSome region

                try
                    forward ()
                finally
                    answering <- ValueNone
        | WM_NCMOUSEMOVE when wParam = HTMAXBUTTON ->
            let struct (x, y) = clientFromScreen lParam
            mouse MouseMove x y -1 0

            if not overButton then
                overButton <- true
                trackLeave ()

            forward ()
        | WM_NCLBUTTONDOWN
        | WM_NCLBUTTONDBLCLK when wParam = HTMAXBUTTON ->
            let struct (x, y) = clientFromScreen lParam
            Win32Native.SetCapture hwnd |> ignore
            pressed <- true
            mouse MouseDown x y 0 1
            0n
        | WM_NCLBUTTONUP when wParam = HTMAXBUTTON ->
            let struct (x, y) = clientFromScreen lParam
            pressed <- false
            Win32Native.ReleaseCapture () |> ignore
            mouse MouseUp x y 0 1
            0n
        | WM_MOUSEMOVE when pressed ->
            let struct (x, y) = packedPoint lParam
            mouse MouseMove x y 0 0
            0n
        | WM_LBUTTONUP when pressed ->
            let struct (x, y) = packedPoint lParam
            pressed <- false
            Win32Native.ReleaseCapture () |> ignore
            mouse MouseUp x y 0 1
            0n
        | WM_CAPTURECHANGED when pressed ->
            pressed <- false
            mouse MouseUp -1 -1 0 1
            forward ()
        | WM_NCMOUSELEAVE ->
            if overButton then
                overButton <- false
                leave ()

            forward ()
        | _ -> forward ()

    member _.Install() =
        previous <- Win32Native.SetWindowLongPtr (hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate<WindowProcedure> procedure)

    member _.Uninstall() =
        if previous <> 0n then
            Win32Native.SetWindowLongPtr (hwnd, GWLP_WNDPROC, previous)
            |> ignore

            previous <- 0n
