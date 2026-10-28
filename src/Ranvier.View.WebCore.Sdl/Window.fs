namespace Ranvier.View.WebCore.Sdl

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore

module internal Sdl =
    let ok (value: SDLBool) : bool =
        SDLBool.op_Implicit value

    /// NUL-terminated UTF-8, for SDL's byte pointer overloads.
    let utf8 (text: string) =
        Encoding.UTF8.GetBytes (text + "\000")

    let text (pointer: nativeptr<byte>) =
        Marshal.PtrToStringUTF8 (NativePtr.toNativeInt pointer)

    let failure (name: string) =
        InvalidOperationException $"{name} failed: {SDL3.SDL_GetError ()}"

    /// The SDL system cursor for an RV_CURSOR_* value.
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

    /// The UTF-16 offset of the code point at <c>index</c> in <c>text</c>, as SDL counts an input method's cursor.
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
    let mutable texture = NativePtr.nullPtr<SDL_Texture>
    let mutable pixels: byte array = [||]
    let mutable textureWidth = 0
    let mutable textureHeight = 0
    let mutable fullPaint = true
    let mutable exposed = true
    let mutable toCss = 1.0f
    let mutable closed = false
    let mutable composing = false
    let mutable cursor = -1
    let mutable disposed = false

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

    let render () =
        SDL3.SDL_RenderClear renderer |> ignore

        if not (NativePtr.isNullPtr texture) then
            let mutable target = SDL_FRect ()
            target.w <- float32 textureWidth
            target.h <- float32 textureHeight

            SDL3.SDL_RenderTexture (renderer, texture, NativePtr.nullPtr, &&target)
            |> ignore

    let dispatch (event: SDL_Event) =
        match enum<SDL_EventType>(int event.``type``) with
        | SDL_EventType.SDL_EVENT_QUIT -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED when event.window.windowID = id -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED when event.window.windowID = id -> resize ()
        | SDL_EventType.SDL_EVENT_WINDOW_EXPOSED when event.window.windowID = id -> exposed <- true
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
            | None -> SDL3.SDL_CreateRenderer (window, NativePtr.nullPtr<byte>)

        if NativePtr.isNullPtr drawing then
            let error = Sdl.failure "SDL_CreateRenderer"
            SDL3.SDL_DestroyWindow window
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            raise error

        SDL3.SDL_SetRenderVSync (drawing, 1) |> ignore
        new Window (window, drawing, WebCoreHost.Create (width, height))

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
        let struct (width, height) = host.PaintSize

        if width > 0 && height > 0 then
            if
                width <> textureWidth
                || height <> textureHeight
                || NativePtr.isNullPtr texture
            then
                if not (NativePtr.isNullPtr texture) then
                    SDL3.SDL_DestroyTexture texture

                texture <-
                    SDL3.SDL_CreateTexture (
                        renderer,
                        SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888,
                        SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                        width,
                        height
                    )

                if NativePtr.isNullPtr texture then
                    raise (Sdl.failure "SDL_CreateTexture")

                SDL3.SDL_SetTextureBlendMode (texture, SDL_BlendMode.SDL_BLENDMODE_NONE)
                |> ignore

                textureWidth <- width
                textureHeight <- height
                pixels <- Array.zeroCreate (width * height * 4)
                fullPaint <- true

            let painted =
                if fullPaint then
                    fullPaint <- false
                    host.Paint pixels
                    true
                else
                    (host.PaintDirty pixels).IsSome

            if painted then
                use start = fixed pixels

                SDL3.SDL_UpdateTexture (texture, NativePtr.nullPtr, NativePtr.toNativeInt start, width * 4)
                |> ignore

            if painted || exposed then
                exposed <- false
                render ()
                SDL3.SDL_RenderPresent renderer |> ignore

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

    /// <summary>The texture as the window last showed it: its width and height in pixels and RGBA bytes, row by row.</summary>
    member internal _.ReadPixels() : struct (int * int * byte array) =
        render ()
        let surface = SDL3.SDL_RenderReadPixels (renderer, NativePtr.nullPtr)

        if NativePtr.isNullPtr surface then
            raise (Sdl.failure "SDL_RenderReadPixels")

        let converted =
            SDL3.SDL_ConvertSurface (surface, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888)

        SDL3.SDL_DestroySurface surface

        if NativePtr.isNullPtr converted then
            raise (Sdl.failure "SDL_ConvertSurface")

        let image = NativePtr.read converted
        let bytes = Array.zeroCreate<byte>(image.w * image.h * 4)

        for row in 0 .. image.h - 1 do
            Marshal.Copy (image.pixels + nativeint (row * image.pitch), bytes, row * image.w * 4, image.w * 4)

        SDL3.SDL_DestroySurface converted
        struct (image.w, image.h, bytes)

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                (host :> IDisposable).Dispose()

                if not (NativePtr.isNullPtr texture) then
                    SDL3.SDL_DestroyTexture texture

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
