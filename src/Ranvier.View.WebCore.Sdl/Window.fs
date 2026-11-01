namespace Ranvier.View.WebCore.Sdl

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.Dom
open Ranvier.View.WebCore

module internal Sdl =
    let ok (value: SDLBool) : bool =
        SDLBool.op_Implicit value

    /// <summary>NUL-terminated UTF-8, for SDL's byte pointer overloads.</summary>
    let utf8 (text: string) =
        Encoding.UTF8.GetBytes (text + "\000")

    let text (pointer: nativeptr<byte>) =
        Marshal.PtrToStringUTF8 (NativePtr.toNativeInt pointer)

    let failure (name: string) =
        InvalidOperationException $"{name} failed: {SDL3.SDL_GetError ()}"

    /// <summary>The SDL system cursor for an RV_CURSOR_* value.</summary>
    let cursor (value: int) =
        match value with
        | 1 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_POINTER
        | 2 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_TEXT
        | 3 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_VERTICAL_TEXT
        | 4 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR
        | 5 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_MOVE
        | 6 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_ALL_SCROLL
        | 7 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT
        | 8 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_PROGRESS
        | 9 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_HELP
        | 10 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_CONTEXT_MENU
        | 11 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_CELL
        | 12 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_ALIAS
        | 13 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_COPY
        | 14 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO_DROP
        | 15 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NOT_ALLOWED
        | 16 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_GRAB
        | 17 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_GRABBING
        | 18 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_ZOOM_IN
        | 19 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_ZOOM_OUT
        | 21 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_COL_RESIZE
        | 22 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_ROW_RESIZE
        | 23 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_N_RESIZE
        | 24 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_E_RESIZE
        | 25 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_S_RESIZE
        | 26 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_W_RESIZE
        | 27 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NE_RESIZE
        | 28 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NW_RESIZE
        | 29 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_SE_RESIZE
        | 30 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_SW_RESIZE
        | 31 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_EW_RESIZE
        | 32 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NS_RESIZE
        | 33 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NESW_RESIZE
        | 34 -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_NWSE_RESIZE
        | _ -> SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT

    /// <summary>The UTF-16 offset of the code point at <c>index</c> in <c>text</c>, as SDL counts an input method's cursor.</summary>
    let utf16Offset (text: string) (index: int) =
        let mutable offset = 0
        let mutable points = 0

        while points < index && offset < text.Length do
            offset <-
                offset
                + (if Char.IsHighSurrogate text[offset] then 2 else 1)

            points <- points + 1

        min offset text.Length

/// <summary>
/// An SDL3 window showing a <c>WebCoreHost</c>'s page: it paints the page into a texture when the page asks for a frame
/// and forwards the window's mouse, keyboard, input method and focus events to it. All calls go on the thread that
/// created it, which is the host's thread.
/// </summary>
[<Sealed>]
type Window private (window: nativeptr<SDL_Window>, renderer: nativeptr<SDL_Renderer>, host: WebCoreHost) =
    // CSS pixels a wheel tick scrolls.
    static let wheelStep = 40.0f
    // RV_CURSOR_NONE.
    static let noCursor = 20

    let id = SDL3.SDL_GetWindowID window
    let cursors = Dictionary<SDL_SystemCursor, nativeint>()
    let presenter = new Presenter (renderer, host)
    let mutable accessibilityAttached = false
    let mutable fullPaint = true
    let mutable exposed = true
    let mutable toCss = 1.0f
    let mutable closed = false
    let mutable composing = false
    let mutable cursor = -1
    let mutable disposed = false

    // The window's display, which the host's screen follows through a core effect on the window's own graph.
    let readDisplay () =
        let display = SDL3.SDL_GetDisplayForWindow window

        if display = LanguagePrimitives.EnumOfValue 0u then
            ValueNone
        else
            let mutable bounds = SDL_Rect ()
            let mutable usable = SDL_Rect ()

            if
                not (Sdl.ok (SDL3.SDL_GetDisplayBounds (display, &&bounds)))
                || not (Sdl.ok (SDL3.SDL_GetDisplayUsableBounds (display, &&usable)))
                || bounds.w <= 0
                || bounds.h <= 0
            then
                ValueNone
            else
                let mode = SDL3.SDL_GetCurrentDisplayMode display

                let bits =
                    if NativePtr.isNullPtr mode then
                        24
                    else
                        match SDL3.SDL_BITSPERPIXEL (NativePtr.read mode).format with
                        | 32
                        | 0 -> 24
                        | bits -> bits

                let orientation =
                    match SDL3.SDL_GetCurrentDisplayOrientation display with
                    | SDL_DisplayOrientation.SDL_ORIENTATION_LANDSCAPE -> ScreenOrientationType.LandscapePrimary
                    | SDL_DisplayOrientation.SDL_ORIENTATION_LANDSCAPE_FLIPPED -> ScreenOrientationType.LandscapeSecondary
                    | SDL_DisplayOrientation.SDL_ORIENTATION_PORTRAIT -> ScreenOrientationType.PortraitPrimary
                    | SDL_DisplayOrientation.SDL_ORIENTATION_PORTRAIT_FLIPPED -> ScreenOrientationType.PortraitSecondary
                    | _ when bounds.h > bounds.w -> ScreenOrientationType.PortraitPrimary
                    | _ -> ScreenOrientationType.LandscapePrimary

                ValueSome
                    {
                        Width = bounds.w
                        Height = bounds.h
                        UsableLeft = usable.x - bounds.x
                        UsableTop = usable.y - bounds.y
                        UsableWidth = max 0 usable.w
                        UsableHeight = max 0 usable.h
                        ColorDepth = bits
                        Orientation = orientation
                    }

    let mutable displayReader = readDisplay
    let graph = new Graph ()
    let mutable displayFeed: IDisposable = null
    let mutable displaySignal: Signal<DisplayScreen voption> = Unchecked.defaultof<_>

    let resize () =
        let mutable width = 0
        let mutable height = 0

        SDL3.SDL_GetWindowSizeInPixels (window, &&width, &&height)
        |> ignore

        let scale =
            match SDL3.SDL_GetWindowDisplayScale window with
            | scale when scale > 0.0f -> scale
            | _ -> 1.0f

        let density =
            match SDL3.SDL_GetWindowPixelDensity window with
            | density when density > 0.0f -> density
            | _ -> 1.0f

        toCss <- density / scale
        host.SetDeviceScale scale
        host.Resize (max 1 (int (float32 width / scale)), max 1 (int (float32 height / scale)))
        presenter.Invalidate ()
        fullPaint <- true

    let css (value: float32) =
        int (MathF.Floor (value * toCss))

    let modifiers () =
        Keys.modifiers (SDL3.SDL_GetModState ())

    let updateCursor () =
        let next = host.Cursor

        if next <> cursor then
            cursor <- next

            if next = noCursor then
                SDL3.SDL_HideCursor () |> ignore
            else
                let system = Sdl.cursor next
                let mutable created = 0n

                if not (cursors.TryGetValue (system, &created)) then
                    created <- NativePtr.toNativeInt (SDL3.SDL_CreateSystemCursor system)
                    cursors[system] <- created

                SDL3.SDL_SetCursor (NativePtr.ofNativeInt created)
                |> ignore

                SDL3.SDL_ShowCursor () |> ignore

    // RV_BUTTON_* of a held button for a move, from SDL's button state.
    let held (state: SDL_MouseButtonFlags) =
        if state.HasFlag SDL_MouseButtonFlags.SDL_BUTTON_LMASK then
            0
        elif state.HasFlag SDL_MouseButtonFlags.SDL_BUTTON_MMASK then
            1
        elif state.HasFlag SDL_MouseButtonFlags.SDL_BUTTON_RMASK then
            2
        else
            -1

    // Places the input method's candidate window at the composition or the caret.
    let placeInputMethod () =
        match host.CompositionRect with
        | ValueSome (struct (x, y, width, height)) ->
            let mutable area = SDL_Rect ()
            area.x <- int (float32 x / toCss)
            area.y <- int (float32 y / toCss)
            area.w <- int (float32 width / toCss)
            area.h <- int (float32 height / toCss)

            SDL3.SDL_SetTextInputArea (window, &&area, 0)
            |> ignore
        | ValueNone -> ()

    let dispatch (event: SDL_Event) =
        match enum<SDL_EventType>(int event.``type``) with
        | SDL_EventType.SDL_EVENT_DISPLAY_ORIENTATION
        | SDL_EventType.SDL_EVENT_DISPLAY_ADDED
        | SDL_EventType.SDL_EVENT_DISPLAY_REMOVED
        | SDL_EventType.SDL_EVENT_DISPLAY_MOVED
        | SDL_EventType.SDL_EVENT_DISPLAY_CURRENT_MODE_CHANGED
        | SDL_EventType.SDL_EVENT_DISPLAY_USABLE_BOUNDS_CHANGED when not (isNull (box displaySignal)) -> displaySignal.Value <- displayReader ()
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_CHANGED when
            event.window.windowID = id
            && not (isNull (box displaySignal))
            ->
            displaySignal.Value <- displayReader ()
        | SDL_EventType.SDL_EVENT_QUIT -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED when event.window.windowID = id -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED when event.window.windowID = id -> resize ()
        | SDL_EventType.SDL_EVENT_WINDOW_EXPOSED when event.window.windowID = id ->
            exposed <- true
            host.SetVisible true
        | SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED
        | SDL_EventType.SDL_EVENT_WINDOW_HIDDEN
        | SDL_EventType.SDL_EVENT_WINDOW_OCCLUDED when event.window.windowID = id -> host.SetVisible false
        | SDL_EventType.SDL_EVENT_WINDOW_RESTORED
        | SDL_EventType.SDL_EVENT_WINDOW_SHOWN when event.window.windowID = id -> host.SetVisible true
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED when event.window.windowID = id -> host.SetFocused true
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST when event.window.windowID = id ->
            if composing then
                composing <- false
                host.CancelComposition ()

            host.SetFocused false
        | SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE when event.window.windowID = id ->
            host.Mouse (0, -1, -1, -1, 0, modifiers ())
            |> ignore

            updateCursor ()
        | SDL_EventType.SDL_EVENT_MOUSE_MOTION when event.motion.windowID = id ->
            let motion = event.motion

            host.Mouse (0, css motion.x, css motion.y, held motion.state, 0, modifiers ())
            |> ignore

            updateCursor ()
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when event.button.windowID = id ->
            let press = event.button

            match int press.button with
            | 1
            | 2
            | 3 as button ->
                let action = if Sdl.ok press.down then 1 else 2

                host.Mouse (action, css press.x, css press.y, button - 1, int press.clicks, modifiers ())
                |> ignore

                updateCursor ()
            | _ -> ()
        | SDL_EventType.SDL_EVENT_MOUSE_WHEEL when event.wheel.windowID = id ->
            let wheel = event.wheel

            let direction =
                if wheel.direction = SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED then
                    -1.0f
                else
                    1.0f

            host.Wheel (css wheel.mouse_x, css wheel.mouse_y, wheel.x * wheelStep * direction, -wheel.y * wheelStep * direction, modifiers ())
            |> ignore
        | SDL_EventType.SDL_EVENT_KEY_DOWN
        | SDL_EventType.SDL_EVENT_KEY_UP when event.key.windowID = id && not composing ->
            let press = event.key
            let action = if Sdl.ok press.down then 0 else 1

            host.Key (
                action,
                Keys.key press.scancode press.``mod``,
                Keys.code press.scancode,
                null,
                Keys.modifiers press.``mod``,
                Sdl.ok press.repeat
            )
            |> ignore
        | SDL_EventType.SDL_EVENT_TEXT_INPUT when event.text.windowID = id ->
            let typed = Sdl.text event.text.text

            if composing then
                composing <- false
                host.CommitComposition typed
            else
                // RV_KEY_CHAR, between the key's RV_KEY_DOWN and RV_KEY_UP, as SDL sends text between them.
                host.Key (2, typed, "", typed, modifiers (), false)
                |> ignore
        | SDL_EventType.SDL_EVENT_TEXT_EDITING when event.edit.windowID = id ->
            let edit = event.edit
            let composition = Sdl.text edit.text

            if String.IsNullOrEmpty composition then
                if composing then
                    composing <- false
                    host.SetComposition ("", 0, 0)
            else
                composing <- true

                let first =
                    if edit.start < 0 then
                        composition.Length
                    else
                        Sdl.utf16Offset composition edit.start

                let last =
                    if edit.start < 0 || edit.length < 0 then
                        first
                    else
                        Sdl.utf16Offset composition (edit.start + edit.length)

                host.SetComposition (composition, first, last)

            placeInputMethod ()
        | _ -> ()

    do
        resize ()

        if WebCoreHost.Has "screen" then
            use _ = graph.Activate ()

            let owner =
                createRoot (fun owner ->
                    displaySignal <- createSignal (readDisplay ())
                    ScreenFeed.follow host displaySignal
                    owner)

            displayFeed <- owner :> IDisposable

        accessibilityAttached <- WindowAccessibility.attach host window
        SDL3.SDL_StartTextInput window |> ignore

        if WebCoreHost.Has "clipboard" then
            host.SetClipboard (
                (fun kind data _ ->
                    if kind = "text/plain" then
                        use text = fixed Sdl.utf8 data
                        SDL3.SDL_SetClipboardText text |> ignore),
                (fun () ->
                    if Sdl.ok (SDL3.SDL_HasClipboardText ()) then
                        Seq.singleton (struct ("text/plain", SDL3.SDL_GetClipboardText ()))
                    else
                        Seq.empty)
            )

        if
            SDL3.SDL_GetWindowFlags window
            &&& SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS
            <> LanguagePrimitives.EnumOfValue 0UL
        then
            host.SetFocused true

    /// <summary>
    /// Opens a window of <c>width</c> by <c>height</c> in the platform's window units, CSS pixels at the display's
    /// scale, with an empty page. <c>hidden</c> keeps it off screen; <c>renderer</c> names an SDL render driver, such
    /// as "software".
    /// </summary>
    static member Create(title: string, width: int, height: int, ?hidden: bool, ?renderer: string) : Window =
        // The page draws the input method's composition itself; the platform draws the candidates.
        (use name = fixed Sdl.utf8 "SDL_IME_IMPLEMENTED_UI"
         use value = fixed Sdl.utf8 "composition"
         SDL3.SDL_SetHint (name, value) |> ignore)

        if not (Sdl.ok (SDL3.SDL_InitSubSystem SDL_InitFlags.SDL_INIT_VIDEO)) then
            raise (Sdl.failure "SDL_InitSubSystem")

        let flags =
            SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            ||| SDL_WindowFlags.SDL_WINDOW_RESIZABLE
            ||| (if defaultArg hidden false then
                     SDL_WindowFlags.SDL_WINDOW_HIDDEN
                 else
                     LanguagePrimitives.EnumOfValue 0UL)

        let window =
            use name = fixed Sdl.utf8 title
            SDL3.SDL_CreateWindow (name, width, height, flags)

        if NativePtr.isNullPtr window then
            let error = Sdl.failure "SDL_CreateWindow"
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            raise error

        let drawing =
            match renderer with
            | Some driver ->
                use name = fixed Sdl.utf8 driver
                SDL3.SDL_CreateRenderer (window, name)
            | None when
                OperatingSystem.IsWindows ()
                && WebCoreHost.Has "presentation-d3d11"
                ->
                let accelerated =
                    use name = fixed Sdl.utf8 "direct3d11"
                    SDL3.SDL_CreateRenderer (window, name)

                if NativePtr.isNullPtr accelerated then
                    SDL3.SDL_CreateRenderer (window, NativePtr.nullPtr<byte>)
                else
                    accelerated
            | None -> SDL3.SDL_CreateRenderer (window, NativePtr.nullPtr<byte>)

        if NativePtr.isNullPtr drawing then
            let error = Sdl.failure "SDL_CreateRenderer"
            SDL3.SDL_DestroyWindow window
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            raise error

        SDL3.SDL_SetRenderVSync (drawing, 1) |> ignore
        let mutable page = None

        try
            let host = WebCoreHost.Create (width, height)
            page <- Some host
            new Window (window, drawing, host)
        with _ ->
            page
            |> Option.iter (fun host -> (host :> IDisposable).Dispose())

            SDL3.SDL_DestroyRenderer drawing
            SDL3.SDL_DestroyWindow window
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            reraise ()

    /// <summary>The window's page.</summary>
    member _.Host = host

    /// <summary>The <c>SDL_Window</c> pointer, for SDL calls the window does not wrap.</summary>
    member _.SdlWindow = NativePtr.toNativeInt window

    /// <summary>
    /// The platform's window: an <c>HWND</c> on Windows, an <c>NSWindow</c> on macOS, a Wayland <c>wl_surface</c> on
    /// Linux, or zero (an X11 window is a number SDL keeps as such), for an accessibility bridge.
    /// </summary>
    member _.NativeHandle: nativeint =
        let properties = SDL3.SDL_GetWindowProperties window

        let read (key: string) =
            use name = fixed Sdl.utf8 key
            SDL3.SDL_GetPointerProperty (properties, name, 0n)

        if OperatingSystem.IsWindows () then
            read "SDL.window.win32.hwnd"
        elif OperatingSystem.IsMacOS () then
            read "SDL.window.cocoa.window"
        else
            read "SDL.window.wayland.surface"

    /// <summary>Whether the window has been asked to close.</summary>
    member _.Closed = closed

    /// <summary>Asks the window to close: the next <c>Step</c> returns false.</summary>
    member _.Close() =
        closed <- true

    /// <summary>
    /// Runs a frame and shows it: the page's animation frame callbacks and rendering update, then the painted area in
    /// the texture, presented on the window's next vertical blank.
    /// </summary>
    member _.Present() =
        host.Frame ()
        presenter.Present exposed
        fullPaint <- false
        exposed <- false
        placeInputMethod ()

    /// <summary>
    /// One turn of the window's loop: waits up to <c>timeout</c> milliseconds (50 by default), less when a timer is due
    /// sooner or a frame is wanted, for window events and dispatches them to the page, cycles the host, then presents a
    /// frame if the page asked for one. Returns false once the window has been asked to close.
    /// </summary>
    member this.Step(?timeout: int) : bool =
        if not closed then
            let longest = defaultArg timeout 50

            let wait =
                if host.FrameRequested || fullPaint || exposed then
                    0
                else
                    match host.NextTimerDelay with
                    | ValueSome delay -> min longest (int (Math.Ceiling delay))
                    | ValueNone -> longest

            let mutable event = SDL_Event ()

            if Sdl.ok (SDL3.SDL_WaitEventTimeout (&&event, wait)) then
                dispatch event

                while Sdl.ok (SDL3.SDL_PollEvent &&event) do
                    dispatch event

            host.Cycle ()

            if
                not closed
                && (host.FrameRequested || fullPaint || exposed)
            then
                this.Present ()

        not closed

    /// <summary>The rendered page's physical dimensions and top-down RGBA bytes.</summary>
    member internal _.ReadPixels() : struct (int * int * byte array) =
        presenter.ReadPixels ()

    /// <summary>The number of shared GPU frames presented to the window.</summary>
    member internal _.GpuFramesPresented = presenter.GpuFramesPresented

    /// <summary>Whether Windows UI Automation is attached to the window.</summary>
    member internal _.AccessibilityAttached = accessibilityAttached

    /// <summary>The display the host's screen follows, as the window last read it.</summary>
    member internal _.Display = displaySignal.Value

    /// <summary>Replaces how the window reads its display on a display event; tests stand in for SDL's displays.</summary>
    member internal _.SetDisplayReader(reader: unit -> DisplayScreen voption) =
        displayReader <- reader

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true

                if accessibilityAttached then
                    WindowAccessibility.detach host
                    accessibilityAttached <- false

                if not (isNull displayFeed) then
                    displayFeed.Dispose ()

                (graph :> IDisposable).Dispose()
                (presenter :> IDisposable).Dispose()
                (host :> IDisposable).Dispose()

                for created in cursors.Values do
                    SDL3.SDL_DestroyCursor (NativePtr.ofNativeInt created)

                SDL3.SDL_StopTextInput window |> ignore
                SDL3.SDL_DestroyRenderer renderer
                SDL3.SDL_DestroyWindow window
                SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO

/// <summary>Opens SDL3 windows showing WebCore pages.</summary>
module Window =
    /// <summary>Opens a window of <c>width</c> by <c>height</c> with an empty page; the app calls <c>Step</c> in its loop.</summary>
    let create (title: string, width: int, height: int) =
        Window.Create (title, width, height)

    /// <summary>
    /// Opens a window, calls <c>setup</c> with its page, such as to mount a component, and runs the window's loop until it
    /// closes; disposes <c>setup</c>'s result if it is disposable.
    /// </summary>
    let run (title: string, width: int, height: int) (setup: WebCoreHost -> 'T) : unit =
        use window = Window.Create (title, width, height)
        let state = setup window.Host

        try
            while window.Step () do
                ()
        finally
            match box state with
            | :? IDisposable as disposable -> disposable.Dispose ()
            | _ -> ()
