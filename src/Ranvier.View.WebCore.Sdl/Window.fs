namespace Ranvier.View.WebCore.Sdl

open System
open System.Collections.Generic
open System.Runtime.ExceptionServices
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Finishes every page phase before marking the page closed and propagating its first failure.</summary>
module internal PageCleanup =
    /// <summary>Attempts every phase and marks the page closed even when a phase fails.</summary>
    let run (phases: (unit -> unit) array) (markClosed: unit -> unit) =
        let cleanup = Cleanup ()

        try
            for phase in phases do
                cleanup.Attempt phase
        finally
            markClosed ()

        cleanup.Finish ()

module internal Sdl =
    let ok (value: SDLBool) : bool =
        SDLBool.op_Implicit value

    let viewportExtent pixels scale =
        max 1 (int (MathF.Ceiling (float32 pixels / scale)))

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

    /// <summary>Converts an SDL editing offset to a UTF-16 offset for WebCore.</summary>
    let compositionOffset windows (text: string) index =
        // SDL's Windows IME forwards WCHAR offsets; X11 reports character offsets.
        if windows then
            min (max 0 index) text.Length
        else
            utf16Offset text index

/// <summary>How often a window dispatches a kind of high-rate input to its page.</summary>
[<RequireQualifiedAccess>]
type InputRate =
    /// <summary>
    /// At most one dispatch per display refresh: a browser's rate. The window holds the latest input, with wheel deltas
    /// summed, and dispatches it before the next frame's animation callbacks, or one refresh interval after the
    /// previous dispatch when no frame is due.
    /// </summary>
    | PerFrame
    /// <summary>One dispatch per run of the input in a drain of SDL's event queue.</summary>
    | PerDrain
    /// <summary>Every event SDL reports.</summary>
    | Raw

    override this.ToString() =
        match this with
        | PerFrame -> "PerFrame"
        | PerDrain -> "PerDrain"
        | Raw -> "Raw"

/// <summary>The window's <c>InputRate</c> for each kind of high-rate input.</summary>
/// <remarks>
/// Held input is dispatched before any button, key, focus or other input event, so the page receives events in
/// the order SDL reports them. A resize under <c>PerFrame</c> or <c>PerDrain</c> applies the latest size once per drain.
/// </remarks>
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
type InputRates =
    {
        /// <summary>Mouse motion, as <c>mousemove</c>: about 1,000 events a second from a gaming mouse.</summary>
        PointerMotion: InputRate
        /// <summary>Mouse wheel and touchpad scrolling, as <c>wheel</c>, with held deltas summed.</summary>
        Wheel: InputRate
        /// <summary>Pen motion and axis changes, as one <c>pointermove</c> per pen.</summary>
        PenMotion: InputRate
        /// <summary>Touch motion, as one <c>pointermove</c> frame carrying every moved contact.</summary>
        TouchMotion: InputRate
        /// <summary>Window pixel size and display scale changes, as a page resize.</summary>
        Resize: InputRate
    }

    /// <summary><c>PerFrame</c> for every kind.</summary>
    static member Default =
        {
            PointerMotion = InputRate.PerFrame
            Wheel = InputRate.PerFrame
            PenMotion = InputRate.PerFrame
            TouchMotion = InputRate.PerFrame
            Resize = InputRate.PerFrame
        }

    /// <summary><c>Raw</c> for every kind.</summary>
    static member Raw =
        {
            PointerMotion = InputRate.Raw
            Wheel = InputRate.Raw
            PenMotion = InputRate.Raw
            TouchMotion = InputRate.Raw
            Resize = InputRate.Raw
        }

    // The record as sprintf "%+A" lays it out. String.Concat, because an interpolated string with non-string holes
    // compiles to F#'s printf, which native AOT rejects.
    member internal this.Display =
        String.Concat (
            [|
                "{ PointerMotion = "
                this.PointerMotion.ToString ()
                "\n  Wheel = "
                this.Wheel.ToString ()
                "\n  PenMotion = "
                this.PenMotion.ToString ()
                "\n  TouchMotion = "
                this.TouchMotion.ToString ()
                "\n  Resize = "
                this.Resize.ToString ()
                " }"
            |]
        )

    override this.ToString() =
        if Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            this.Display

/// <summary>How an SDL event relates to held input.</summary>
[<RequireQualifiedAccess; Struct>]
type internal HeldKind =
    /// <summary>An event the page never receives, or whose order relative to input is irrelevant.</summary>
    | Ignored
    /// <summary>An event that dispatches all held input before itself.</summary>
    | Barrier
    | Motion
    | Wheel
    | Pen
    | Touch
    | Resize

    override this.ToString() =
        match this with
        | Ignored -> "Ignored"
        | Barrier -> "Barrier"
        | Motion -> "Motion"
        | Wheel -> "Wheel"
        | Pen -> "Pen"
        | Touch -> "Touch"
        | Resize -> "Resize"

/// <summary>
/// An SDL3 window showing a <c>WebCoreHost</c>'s page: it paints the page into a texture when the page asks for a frame
/// and forwards the window's mouse, keyboard, input method and focus events to it. All calls go on the thread that
/// created it, which is the host's thread.
/// </summary>
/// <remarks>All windows use the thread admitted by the first <c>Create</c>, including after every window has closed.</remarks>
[<Sealed>]
type Window
    private
    (
        window: nativeptr<SDL_Window>,
        renderer: nativeptr<SDL_Renderer>,
        host: WebCoreHost,
        egl: EglRenderer option,
        swapChain: D3D11SwapChain option,
        initialRates: InputRates,
        frame: WindowFrame
    ) as this =
    static let owner = WebCoreHost.Owner
    static let windows = WindowRegistry<Window>()
    // A page call is on the owner thread's stack: a modal tick waits for the next turn.
    static let mutable onPage = false
    // CSS pixels a wheel tick scrolls.
    static let wheelStep = 40.0f
    // RV_CURSOR_NONE.
    static let noCursor = 20

    let id = SDL3.SDL_GetWindowID window
    let cursors = Dictionary<SDL_SystemCursor, nativeint>()
    let touches = TouchFrames ()
    let pens = PenSamples ()
    let touchInput = WebCoreHost.Has "touch-input"
    let penInput = WebCoreHost.Has "pointer-input"
    let surrounding = SurroundingText.Bridge (NativePtr.toNativeInt window, host)
    let presenter = new Presenter (renderer, host, egl, swapChain)
    let mutable accessibilityAttached = false
    let mutable linuxAccessibility: LinuxAccessibility option = None
    let mutable fullPaint = true
    let mutable exposed = true
    // A frame ran during a long present and is not on screen yet.
    let mutable framePending = false
    let mutable tracedPresentation: int64 voption = ValueNone

    let completePresentationTrace () =
        match tracedPresentation with
        | ValueSome frameId ->
            tracedPresentation <- ValueNone
            let observed = host.SchedulingTrace.Start ()
            host.SchedulingTrace.Finish (SchedulingPhase.PresentCompleted, observed, 0, frameId = frameId)
        | ValueNone -> ()

    let mutable lastFrameAt = 0L
    let mutable toCss = 1.0f
    let mutable closed = false
    let mutable composing = false
    let mutable pendingCompositionClear = false
    let mutable suppressImeText = false
    let mutable cursor = -1
    let mutable disposed = false
    let mutable pageDisposed = false
    let mutable interactiveBudget: OwnerTurnBudget option = None
    let mutable eventDrainPending = false

    let cycleHost () =
        match interactiveBudget with
        | Some budget -> host.CycleInteractive budget |> ignore
        | None -> host.Cycle ()

    let wakeGate = obj ()
    let mutable wakeActive = true
    let wakeEvent = SDL3.SDL_RegisterEvents 1
    let mutable cleanupCompleted: string -> unit = ignore
    let mutable maxTouchPoints = 0
    let mutable rates = initialRates
    // Held input, under PerFrame and PerDrain. At most one kind is held at a time.
    let mutable pendingMotion: SDL_MouseMotionEvent voption = ValueNone
    // Mouse id, CSS position, summed deltas and modifiers.
    let mutable pendingWheel: struct (SDL_MouseID * int * int * float32 * float32 * int) voption =
        ValueNone

    let pendingPens = Dictionary<uint32, PointerSample>()
    let mutable pendingTouch: PointerSample array = null
    let mutable pendingResize = false
    // Stopwatch timestamps of each kind's latest dispatch.
    let mutable lastMotion = 0L
    let mutable lastWheel = 0L
    let mutable lastPen = 0L
    let mutable lastTouch = 0L
    let mutable refreshTicks = Diagnostics.Stopwatch.Frequency / 60L
    let mutable resizesApplied = 0
    // Whether the page received input since the window last read its surrounding text.
    let mutable delivered = true

    let alive () =
        not closed
        && not disposed
        && not pageDisposed
        && not host.Session.IsDisposed

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

    let mutable placementReader =
        fun () -> WindowHit.placement (SDL3.SDL_GetWindowFlags window)

    let placementSignal = graph.Run (fun () -> createSignal (placementReader ()))

    let focusedSignal =
        graph.Run (fun () ->
            createSignal (
                SDL3.SDL_GetWindowFlags window
                &&& SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS
                <> LanguagePrimitives.EnumOfValue 0UL
            ))

    let struct (stateOwner, placementMemo, focusedMemo) =
        graph.Run (fun () ->
            createRoot (fun owner ->
                struct (owner :> IDisposable, createMemo (fun _ -> placementSignal.Value), createMemo (fun _ -> focusedSignal.Value))))

    let mutable systemMenu =
        fun x y ->
            SDL3.SDL_ShowWindowSystemMenu (window, x, y)
            |> ignore

    let mutable modalFrame = cycleHost
    // Native callbacks hold these delegates' function pointers for the window's lifetime.
    let mutable hitTestCallback: HitTestCallback = null
    let mutable eventWatch: EventWatchCallback = null
    let mutable eventWatchPointer = 0n
    let mutable snapButton: SnapButtonSubclass option = None
    let mutable regionReads = 0L
    let mutable modalResizePending = false
    // The first failure inside a native callback, rethrown by the next Step.
    let mutable nativeFailure: ExceptionDispatchInfo = null
    // Page code is running inside a native callback, where SDL cannot destroy this window.
    let mutable callbackDepth = 0
    let mutable disposePending = false

    let guarded fallback (call: unit -> 'T) : 'T =
        try
            call ()
        with error ->
            if isNull nativeFailure then
                nativeFailure <- ExceptionDispatchInfo.Capture error

            fallback

    let rethrowNativeFailure () =
        if not (isNull nativeFailure) then
            let failure = nativeFailure
            nativeFailure <- null
            failure.Throw ()

    let readRefresh () =
        let mode = SDL3.SDL_GetCurrentDisplayMode (SDL3.SDL_GetDisplayForWindow window)

        let rate =
            if NativePtr.isNullPtr mode then
                0.0f
            else
                (NativePtr.read mode).refresh_rate

        refreshTicks <-
            if rate > 0.0f then
                int64 (float Diagnostics.Stopwatch.Frequency / float rate)
            else
                Diagnostics.Stopwatch.Frequency / 60L

    let resize () =
        modalResizePending <- false
        resizesApplied <- resizesApplied + 1
        readRefresh ()
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
        presenter.Resized (width, height)
        host.SetDeviceScale scale
        host.Resize (Sdl.viewportExtent width scale, Sdl.viewportExtent height scale)

        linuxAccessibility
        |> Option.iter (fun bridge -> bridge.Update ())

        presenter.Invalidate ()
        fullPaint <- true

    let css (value: float32) =
        int (MathF.Floor (value * toCss))

    let modifiers () =
        Keys.modifiers (SDL3.SDL_GetModState ())

    let sendPointers (samples: PointerSample array) =
        if samples.Length > 0 && alive () then
            delivered <- true
            host.Pointer samples |> ignore

    let penButtons (state: SDL_PenInputFlags) =
        (if state.HasFlag SDL_PenInputFlags.SDL_PEN_INPUT_DOWN then
             1
         else
             0)
        ||| (if state.HasFlag SDL_PenInputFlags.SDL_PEN_INPUT_BUTTON_1 then
                 2
             else
                 0)
        ||| (if state.HasFlag SDL_PenInputFlags.SDL_PEN_INPUT_BUTTON_2 then
                 4
             else
                 0)

    let penSample id action x y button state axis =
        pens.Update (uint32 id, action, float (x * toCss), float (y * toCss), button, penButtons state, modifiers (), axis)

    let pen id action x y button state axis =
        sendPointers [| penSample id action x y button state axis |]

    let syntheticMouse (id: SDL_MouseID) =
        (touchInput && id = SDL3.SDL_TOUCH_MOUSEID)
        || (penInput && id = SDL3.SDL_PEN_MOUSEID)

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

    let now () =
        Diagnostics.Stopwatch.GetTimestamp ()

    let sendMotion (motion: SDL_MouseMotionEvent) =
        lastMotion <- now ()
        delivered <- true

        host.Mouse (0, css motion.x, css motion.y, held motion.state, 0, modifiers ())
        |> ignore

        updateCursor ()

    let flushMotion () =
        match pendingMotion with
        | ValueSome motion ->
            pendingMotion <- ValueNone

            if alive () then
                sendMotion motion
        | ValueNone -> ()

    let sendWheel x y deltaX deltaY keys =
        lastWheel <- now ()
        delivered <- true
        host.Wheel (x, y, deltaX, deltaY, keys) |> ignore

    let flushWheel () =
        match pendingWheel with
        | ValueSome (struct (_, x, y, deltaX, deltaY, keys)) ->
            pendingWheel <- ValueNone

            if alive () then
                sendWheel x y deltaX deltaY keys
        | ValueNone -> ()

    let flushPens () =
        if pendingPens.Count > 0 then
            let samples = Seq.toArray pendingPens.Values
            pendingPens.Clear ()
            lastPen <- now ()

            for sample in samples do
                sendPointers [| sample |]

    // Movement accumulates across the samples a held pen sample replaces.
    let holdPen (id: uint32) (sample: PointerSample) =
        let mutable merged = sample
        let mutable previous = Unchecked.defaultof<PointerSample>

        if pendingPens.TryGetValue (id, &previous) then
            merged.MovementX <- merged.MovementX + previous.MovementX
            merged.MovementY <- merged.MovementY + previous.MovementY

        pendingPens[id] <- merged

    let flushTouch () =
        if not (isNull pendingTouch) then
            let frame = pendingTouch
            pendingTouch <- null
            lastTouch <- now ()
            sendPointers frame

    // A held touch frame keeps every contact that moved in the frames it replaces, with their movement summed.
    let holdTouch (frame: PointerSample array) =
        if not (isNull pendingTouch) then
            for index in 0 .. frame.Length - 1 do
                let mutable sample = frame[index]

                match
                    pendingTouch
                    |> Array.tryFind (fun previous -> previous.Id = sample.Id && previous.Action = 0)
                with
                | Some previous ->
                    sample.Action <- 0
                    sample.MovementX <- sample.MovementX + previous.MovementX
                    sample.MovementY <- sample.MovementY + previous.MovementY
                    frame[index] <- sample
                | None -> ()

        pendingTouch <- frame

    let flushResize () =
        if pendingResize then
            pendingResize <- false

            if alive () then
                resize ()

    // Dispatches all held input except the kind in `keep`.
    let flushHeld (keep: HeldKind) =
        if keep <> HeldKind.Resize then
            flushResize ()

        if keep <> HeldKind.Motion then
            flushMotion ()

        if keep <> HeldKind.Wheel then
            flushWheel ()

        if keep <> HeldKind.Pen then
            flushPens ()

        if keep <> HeldKind.Touch then
            flushTouch ()

    let classify (event: SDL_Event) =
        let own target =
            target = id

        match enum<SDL_EventType>(int event.``type``) with
        | SDL_EventType.SDL_EVENT_MOUSE_MOTION when own event.motion.windowID ->
            if syntheticMouse event.motion.which then
                HeldKind.Ignored
            else
                HeldKind.Motion
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when own event.button.windowID ->
            if syntheticMouse event.button.which then
                HeldKind.Ignored
            else
                HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_MOUSE_WHEEL when own event.wheel.windowID -> HeldKind.Wheel
        | SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED when own event.window.windowID -> HeldKind.Resize
        | SDL_EventType.SDL_EVENT_PEN_MOTION when penInput && own event.pmotion.windowID -> HeldKind.Pen
        | SDL_EventType.SDL_EVENT_PEN_AXIS when penInput && own event.paxis.windowID -> HeldKind.Pen
        | SDL_EventType.SDL_EVENT_PEN_DOWN
        | SDL_EventType.SDL_EVENT_PEN_UP when penInput && own event.ptouch.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_PEN_BUTTON_DOWN
        | SDL_EventType.SDL_EVENT_PEN_BUTTON_UP when penInput && own event.pbutton.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_PEN_PROXIMITY_OUT when penInput && own event.pproximity.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_FINGER_MOTION when touchInput && own event.tfinger.windowID -> HeldKind.Touch
        | SDL_EventType.SDL_EVENT_FINGER_DOWN
        | SDL_EventType.SDL_EVENT_FINGER_UP
        | SDL_EventType.SDL_EVENT_FINGER_CANCELED when touchInput && own event.tfinger.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_KEY_DOWN
        | SDL_EventType.SDL_EVENT_KEY_UP when own event.key.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_TEXT_INPUT when own event.text.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_TEXT_EDITING when own event.edit.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST
        | SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED when own event.window.windowID -> HeldKind.Barrier
        | SDL_EventType.SDL_EVENT_QUIT -> HeldKind.Barrier
        | _ when
            surrounding.EventType <> 0u
            && event.``type`` = surrounding.EventType
            && own event.user.windowID
            ->
            HeldKind.Barrier
        | _ -> HeldKind.Ignored

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

    // The page's region at a window position, from the element under it at the time of the call.
    let regionAt (x: float32) (y: float32) =
        if alive () then
            regionReads <- regionReads + 1L
            let element = host.Document.elementFromPoint (float (x * toCss), float (y * toCss))

            if isNull (box element) then
                WindowRegion.Content
            else
                let marked = element.closest WindowRegionAttribute.Selector

                if isNull (box marked) then
                    WindowRegion.Content
                else
                    WindowRegionAttribute.parse (marked.getAttribute WindowRegionAttribute.Name)
        else
            WindowRegion.Content

    let resizeResult (border: int) (x: int) (y: int) =
        let mutable width = 0
        let mutable height = 0

        SDL3.SDL_GetWindowSize (window, &&width, &&height)
        |> ignore

        WindowHit.resize (placementReader ()) border width height x y

    // The resize border, then the page's regions.
    let hitTest (border: int) (x: int) (y: int) =
        match resizeResult border x y with
        | ValueSome edge -> edge
        | ValueNone ->
            let region =
                match snapButton with
                | Some button when button.Answering.IsSome -> button.Answering.Value
                | _ -> regionAt (float32 x) (float32 y)

            if region = WindowRegion.Drag then
                SDL_HitTestResult.SDL_HITTEST_DRAGGABLE
            else
                SDL_HitTestResult.SDL_HITTEST_NORMAL

    let syncWindowState (event: SDL_Event) =
        let write (signal: Signal<'T>) (value: 'T) =
            graph.Run (fun () ->
                signal.Value <- value
                flush ())

        match enum<SDL_EventType>(int event.``type``) with
        | SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED
        | SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED
        | SDL_EventType.SDL_EVENT_WINDOW_RESTORED
        | SDL_EventType.SDL_EVENT_WINDOW_ENTER_FULLSCREEN
        | SDL_EventType.SDL_EVENT_WINDOW_LEAVE_FULLSCREEN when event.window.windowID = id -> write placementSignal (placementReader ())
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED when event.window.windowID = id -> write focusedSignal true
        | SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST when event.window.windowID = id -> write focusedSignal false
        | _ -> ()

    let dispatch (event: SDL_Event) =
        syncWindowState event

        match enum<SDL_EventType>(int event.``type``) with
        | SDL_EventType.SDL_EVENT_DISPLAY_ORIENTATION
        | SDL_EventType.SDL_EVENT_DISPLAY_ADDED
        | SDL_EventType.SDL_EVENT_DISPLAY_REMOVED
        | SDL_EventType.SDL_EVENT_DISPLAY_MOVED
        | SDL_EventType.SDL_EVENT_DISPLAY_CURRENT_MODE_CHANGED
        | SDL_EventType.SDL_EVENT_DISPLAY_USABLE_BOUNDS_CHANGED when not (isNull (box displaySignal)) -> displaySignal.Value <- displayReader ()
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_CHANGED when event.window.windowID = id ->
            readRefresh ()

            if not (isNull (box displaySignal)) then
                displaySignal.Value <- displayReader ()

                linuxAccessibility
                |> Option.iter (fun bridge -> bridge.Update ())
        | SDL_EventType.SDL_EVENT_WINDOW_MOVED when event.window.windowID = id ->
            linuxAccessibility
            |> Option.iter (fun bridge -> bridge.Update ())
        | SDL_EventType.SDL_EVENT_QUIT -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED when event.window.windowID = id -> closed <- true
        | SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
        | SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED when event.window.windowID = id ->
            if rates.Resize = InputRate.Raw then
                resize ()
            else
                pendingResize <- true
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
            suppressImeText <- false

            if composing then
                composing <- false
                pendingCompositionClear <- false
                host.CancelComposition ()

            if touchInput && alive () then
                sendPointers (touches.Cancel (modifiers ()))

            if penInput && alive () then
                for sample in pens.Cancel (modifiers ()) do
                    sendPointers [| sample |]

            if alive () then
                host.SetFocused false
        | SDL_EventType.SDL_EVENT_FINGER_DOWN
        | SDL_EventType.SDL_EVENT_FINGER_UP
        | SDL_EventType.SDL_EVENT_FINGER_MOTION
        | SDL_EventType.SDL_EVENT_FINGER_CANCELED when touchInput && event.tfinger.windowID = id ->
            let finger = event.tfinger
            let mutable width = 0
            let mutable height = 0

            SDL3.SDL_GetWindowSize (window, &&width, &&height)
            |> ignore

            let action =
                match enum<SDL_EventType>(int event.``type``) with
                | SDL_EventType.SDL_EVENT_FINGER_DOWN -> 1
                | SDL_EventType.SDL_EVENT_FINGER_UP -> 2
                | SDL_EventType.SDL_EVENT_FINGER_CANCELED -> 3
                | _ -> 0

            let frame =
                touches.Update (
                    uint64 finger.touchID,
                    uint64 finger.fingerID,
                    action,
                    float (finger.x * float32 width * toCss),
                    float (finger.y * float32 height * toCss),
                    finger.pressure,
                    modifiers ()
                )

            if touches.Count > maxTouchPoints then
                maxTouchPoints <- touches.Count
                host.SetMaxTouchPoints maxTouchPoints

            if action <> 0 || rates.TouchMotion = InputRate.Raw then
                sendPointers frame
            elif frame.Length > 0 then
                holdTouch frame
        | SDL_EventType.SDL_EVENT_PEN_MOTION when penInput && event.pmotion.windowID = id ->
            let motion = event.pmotion
            let sample = penSample motion.which 0 motion.x motion.y -1 motion.pen_state None

            if rates.PenMotion = InputRate.Raw then
                sendPointers [| sample |]
            else
                holdPen (uint32 motion.which) sample
        | SDL_EventType.SDL_EVENT_PEN_DOWN
        | SDL_EventType.SDL_EVENT_PEN_UP when penInput && event.ptouch.windowID = id ->
            let touch = event.ptouch
            pen touch.which (if Sdl.ok touch.down then 1 else 2) touch.x touch.y 0 touch.pen_state None
        | SDL_EventType.SDL_EVENT_PEN_BUTTON_DOWN
        | SDL_EventType.SDL_EVENT_PEN_BUTTON_UP when penInput && event.pbutton.windowID = id ->
            let button = event.pbutton

            let mapped =
                match button.button with
                | 1uy -> 2
                | 2uy -> 1
                | _ -> -1

            pen
                button.which
                (if mapped < 0 then 0
                 elif Sdl.ok button.down then 1
                 else 2)
                button.x
                button.y
                mapped
                button.pen_state
                None
        | SDL_EventType.SDL_EVENT_PEN_AXIS when penInput && event.paxis.windowID = id ->
            let axis = event.paxis

            let sample =
                penSample axis.which 0 axis.x axis.y -1 axis.pen_state (Some (axis.axis, axis.value))

            if rates.PenMotion = InputRate.Raw then
                sendPointers [| sample |]
            else
                holdPen (uint32 axis.which) sample
        | SDL_EventType.SDL_EVENT_PEN_PROXIMITY_OUT when penInput && event.pproximity.windowID = id ->
            match pens.Leave (uint32 event.pproximity.which, modifiers ()) with
            | ValueSome sample -> sendPointers [| sample |]
            | ValueNone -> ()
        | SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE when event.window.windowID = id ->
            host.Mouse (0, -1, -1, -1, 0, modifiers ())
            |> ignore

            updateCursor ()
        | SDL_EventType.SDL_EVENT_MOUSE_MOTION when
            event.motion.windowID = id
            && not (syntheticMouse event.motion.which)
            ->
            let motion = event.motion

            if rates.PointerMotion = InputRate.Raw then
                sendMotion motion
            else
                match pendingMotion with
                | ValueSome previous when previous.which <> motion.which -> flushMotion ()
                | _ -> ()

                pendingMotion <- ValueSome motion
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
        | SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when
            event.button.windowID = id
            && not (syntheticMouse event.button.which)
            ->
            let press = event.button

            match int press.button with
            | 1
            | 2
            | 3 as button ->
                let action = if Sdl.ok press.down then 1 else 2

                host.Mouse (action, css press.x, css press.y, button - 1, int press.clicks, modifiers ())
                |> ignore

                updateCursor ()

                // Windows opens this menu itself; other platforms report the right click to the window.
                if
                    action = 2
                    && button = 3
                    && frame <> WindowFrame.Native
                    && alive ()
                    && regionAt press.x press.y = WindowRegion.Drag
                then
                    systemMenu (int press.x) (int press.y)
            | _ -> ()
        | SDL_EventType.SDL_EVENT_MOUSE_WHEEL when event.wheel.windowID = id ->
            let wheel = event.wheel

            let direction =
                if wheel.direction = SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED then
                    -1.0f
                else
                    1.0f

            let x = css wheel.mouse_x
            let y = css wheel.mouse_y
            let deltaX = wheel.x * wheelStep * direction
            let deltaY = -wheel.y * wheelStep * direction
            let keys = modifiers ()

            if rates.Wheel = InputRate.Raw then
                sendWheel x y deltaX deltaY keys
            else
                match pendingWheel with
                | ValueSome (struct (mouse, _, _, heldX, heldY, heldKeys)) when mouse = wheel.which && heldKeys = keys ->
                    pendingWheel <- ValueSome (struct (mouse, x, y, heldX + deltaX, heldY + deltaY, keys))
                | _ ->
                    flushWheel ()
                    pendingWheel <- ValueSome (struct (wheel.which, x, y, deltaX, deltaY, keys))
        | _ when
            surrounding.EventType <> 0u
            && event.``type`` = surrounding.EventType
            && event.user.windowID = id
            ->
            let deletion = event.user
            let token = uint64 (unativeint deletion.data2)
            let count = uint32 (unativeint deletion.data1)

            match surrounding.Delete (token, deletion.code, count) with
            | 1 -> ()
            | 2 ->
                composing <- false
                pendingCompositionClear <- false
                suppressImeText <- true
            | _ -> suppressImeText <- true
        | SDL_EventType.SDL_EVENT_KEY_DOWN when event.key.windowID = id && suppressImeText -> suppressImeText <- false
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
        | SDL_EventType.SDL_EVENT_TEXT_INPUT when event.text.windowID = id && not suppressImeText ->
            let typed = Sdl.text event.text.text

            if composing then
                composing <- false
                pendingCompositionClear <- false
                host.CommitComposition typed
            else
                // RV_KEY_CHAR, between the key's RV_KEY_DOWN and RV_KEY_UP, as SDL sends text between them.
                host.Key (2, typed, "", typed, modifiers (), false)
                |> ignore
        | SDL_EventType.SDL_EVENT_TEXT_EDITING when event.edit.windowID = id && not suppressImeText ->
            let edit = event.edit
            let composition = Sdl.text edit.text

            if String.IsNullOrEmpty composition then
                if composing then
                    pendingCompositionClear <- true
            else
                if pendingCompositionClear then
                    pendingCompositionClear <- false
                    composing <- false
                    host.CancelComposition ()

                composing <- true

                let first =
                    if edit.start < 0 then
                        composition.Length
                    else
                        Sdl.compositionOffset (OperatingSystem.IsWindows ()) composition edit.start

                let last =
                    if edit.start < 0 || edit.length < 0 then
                        first
                    else
                        Sdl.compositionOffset (OperatingSystem.IsWindows ()) composition (edit.start + edit.length)

                if alive () then
                    host.SetComposition (composition, first, last)

            if alive () then
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
        linuxAccessibility <- LinuxAccessibility.TryCreate (host, window)
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

        match frame with
        | WindowFrame.Native -> ()
        | WindowFrame.Custom border ->
            hitTestCallback <-
                HitTestCallback (fun _ point _ ->
                    guarded SDL_HitTestResult.SDL_HITTEST_NORMAL (fun () ->
                        let at = NativePtr.read (NativePtr.ofNativeInt<SDL_Point> point)
                        hitTest border at.x at.y))

            if not (Sdl.ok (SDL3.SDL_SetWindowHitTest (window, Marshal.GetFunctionPointerForDelegate<HitTestCallback> hitTestCallback, 0n))) then
                raise (Sdl.failure "SDL_SetWindowHitTest")

            if OperatingSystem.IsWindows () then
                let hwnd =
                    use key = fixed Sdl.utf8 "SDL.window.win32.hwnd"
                    SDL3.SDL_GetPointerProperty (SDL3.SDL_GetWindowProperties window, key, 0n)

                let inCallback (call: unit -> unit) =
                    guarded () (fun () ->
                        if alive () then
                            callbackDepth <- callbackDepth + 1

                            try
                                call ()
                            finally
                                callbackDepth <- callbackDepth - 1)

                let subclass =
                    SnapButtonSubclass (
                        hwnd,
                        (fun x y ->
                            guarded WindowRegion.Content (fun () ->
                                if (resizeResult border x y).IsSome then
                                    WindowRegion.Content
                                else
                                    regionAt (float32 x) (float32 y))),
                        (fun action x y button clicks ->
                            inCallback (fun () ->
                                host.Mouse (action, css (float32 x), css (float32 y), button, clicks, modifiers ())
                                |> ignore)),
                        (fun () ->
                            inCallback (fun () ->
                                host.Mouse (0, -1, -1, -1, 0, modifiers ())
                                |> ignore))
                    )

                Win32Native.applyCompositorFrame hwnd
                subclass.Install ()
                snapButton <- Some subclass

        eventWatch <-
            EventWatchCallback (fun _ pointer ->
                guarded 1uy (fun () ->
                    let event = NativePtr.read (NativePtr.ofNativeInt<SDL_Event> pointer)

                    match enum<SDL_EventType>(int event.``type``) with
                    | SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED when
                        event.window.windowID = id
                        && host.Session.IsOwnerThread
                        ->
                        modalResizePending <- true
                    // SDL marks the tick raised inside the platform's modal move or resize loop with data1 = 1.
                    | SDL_EventType.SDL_EVENT_WINDOW_EXPOSED when
                        event.window.windowID = id
                        && event.window.data1 = 1
                        && host.Session.IsOwnerThread
                        ->
                        this.ModalTurn ()
                    | _ -> ()

                    1uy))

        eventWatchPointer <- Marshal.GetFunctionPointerForDelegate<EventWatchCallback> eventWatch

        SDL3.SDL_AddEventWatch (eventWatchPointer, 0n)
        |> ignore

        windows.Add (uint32 id, this)

        host.SetOwnerWake (
            Some (fun () ->
                lock wakeGate (fun () ->
                    if wakeActive then
                        if wakeEvent = UInt32.MaxValue then
                            invalidOp "SDL owner wake registration failed"

                        let mutable wake = SDL_Event ()
                        // The registered event type is a stable token; no engine pointer crosses threads.
                        wake.``type`` <- wakeEvent

                        if not (Sdl.ok (SDL3.SDL_PushEvent &&wake)) then
                            invalidOp "SDL owner wake signal failed"))
        )

    // A turn of the page inside the platform's modal move or resize loop, which blocks Step for the whole gesture.
    member private this.ModalTurn() =
        if not onPage && alive () then
            onPage <- true
            callbackDepth <- callbackDepth + 1

            try
                if modalResizePending then
                    resize ()

                modalFrame ()

                if
                    alive ()
                    && (host.FrameRequested
                        || fullPaint
                        || exposed
                        || framePending)
                    && not presenter.Presenting
                then
                    this.Present ()
            finally
                callbackDepth <- callbackDepth - 1
                onPage <- false

    member private _.Dispatch(event: SDL_Event) =
        if alive () then
            match classify event with
            | HeldKind.Ignored -> ()
            | HeldKind.Barrier ->
                delivered <- true
                flushHeld HeldKind.Barrier
            | kind -> flushHeld kind

            dispatch event

    // Ends a drain of SDL's queue: dispatches a held resize, PerDrain input, and PerFrame input once a refresh interval
    // has passed since its kind's previous dispatch.
    member private this.EndDrain() =
        this.CompleteDispose ()

        let due rate last =
            match rate with
            | InputRate.PerFrame -> now () - last >= refreshTicks
            | InputRate.PerDrain
            | InputRate.Raw -> true

        flushResize ()

        if
            pendingMotion.IsSome
            && due rates.PointerMotion lastMotion
        then
            flushMotion ()

        if pendingWheel.IsSome && due rates.Wheel lastWheel then
            flushWheel ()

        if
            pendingPens.Count > 0
            && due rates.PenMotion lastPen
        then
            flushPens ()

        if
            not (isNull pendingTouch)
            && due rates.TouchMotion lastTouch
        then
            flushTouch ()

    // Milliseconds to wait in Step for held PerFrame input to become due. One millisecond longer than the time left,
    // because SDL_WaitEventTimeout on Windows returns at once for a timeout of 1 and up to a millisecond early for
    // longer ones.
    member private _.HeldDue: int voption =
        let remaining isHeld last =
            if isHeld then
                let ticks = last + refreshTicks - now ()

                ValueSome (
                    if ticks <= 0L then
                        0
                    else
                        1
                        + int (
                            Math.Ceiling (
                                float ticks * 1000.0
                                / float Diagnostics.Stopwatch.Frequency
                            )
                        )
                )
            else
                ValueNone

        if not (alive ()) then
            ValueNone
        else
            [
                remaining pendingMotion.IsSome lastMotion
                remaining pendingWheel.IsSome lastWheel
                remaining (pendingPens.Count > 0) lastPen
                remaining (not (isNull pendingTouch)) lastTouch
            ]
            |> List.fold
                (fun earliest due ->
                    match earliest, due with
                    | ValueSome a, ValueSome b -> ValueSome (min a b)
                    | ValueNone, due -> due
                    | earliest, ValueNone -> earliest)
                ValueNone

    member private _.FlushComposition() =
        if pendingCompositionClear && alive () then
            pendingCompositionClear <- false
            composing <- false
            host.CancelComposition ()

    member private _.RefreshSurrounding() =
        if alive () then
            delivered <- false
            surrounding.Refresh ()

    // Reads the surrounding text again only when the page received input since the last read.
    member private this.RefreshAfterInput() =
        if delivered then
            this.RefreshSurrounding ()

    static member private CreateCore
        (
            title: string,
            width: int,
            height: int,
            hidden: bool option,
            renderer: string option,
            rates: InputRates,
            frame: WindowFrame,
            minimumSize: struct (int * int) option
        ) : Window =
        EglRenderer.RetryFailedConstruction ()
        EglRenderer.Prepare ()
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
            ||| (match frame with
                 | WindowFrame.Custom _ -> SDL_WindowFlags.SDL_WINDOW_BORDERLESS
                 | WindowFrame.Native -> LanguagePrimitives.EnumOfValue 0UL)

        let window =
            use name = fixed Sdl.utf8 title
            SDL3.SDL_CreateWindow (name, width, height, flags)

        if NativePtr.isNullPtr window then
            let error = Sdl.failure "SDL_CreateWindow"
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            raise error

        match minimumSize with
        | Some (struct (minimumWidth, minimumHeight)) ->
            SDL3.SDL_SetWindowMinimumSize (window, minimumWidth, minimumHeight)
            |> ignore
        | None -> ()

        // Windows frames present through the window's own swap chain, which excludes an SDL renderer on the same window.
        let swapChain =
            if
                (renderer.IsNone || renderer = Some "direct3d11")
                && OperatingSystem.IsWindows ()
                && WebCoreHost.Has "presentation-d3d11"
            then
                let mutable pixelWidth = 0
                let mutable pixelHeight = 0

                SDL3.SDL_GetWindowSizeInPixels (window, &&pixelWidth, &&pixelHeight)
                |> ignore

                use key = fixed Sdl.utf8 "SDL.window.win32.hwnd"

                let hwnd =
                    SDL3.SDL_GetPointerProperty (SDL3.SDL_GetWindowProperties window, key, 0n)

                D3D11SwapChain.TryCreate (hwnd, pixelWidth, pixelHeight)
            else
                None

        let drawing =
            match renderer with
            | _ when swapChain.IsSome -> NativePtr.nullPtr<SDL_Renderer>
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
            | None when
                OperatingSystem.IsMacOS ()
                && WebCoreHost.Has "iosurface-frames"
                ->
                use name = fixed Sdl.utf8 "metal"
                SDL3.SDL_CreateRenderer (window, name)
            | None when
                OperatingSystem.IsLinux ()
                && WebCoreHost.Has "presentation-egl"
                ->
                use name = fixed Sdl.utf8 "opengles2"
                SDL3.SDL_CreateRenderer (window, name)
            | None -> SDL3.SDL_CreateRenderer (window, NativePtr.nullPtr<byte>)

        if swapChain.IsNone && NativePtr.isNullPtr drawing then
            let error = Sdl.failure "SDL_CreateRenderer"
            SDL3.SDL_DestroyWindow window
            SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
            raise error

        if swapChain.IsNone then
            SDL3.SDL_SetRenderVSync (drawing, 1) |> ignore

        let cleanupEgl =
            if swapChain.IsSome then
                None
            else
                EglRenderer.Capture drawing

        let mutable page = None
        let mutable egl = None

        try
            if swapChain.IsNone then
                egl <- EglRenderer.TryCreate drawing

            let host = WebCoreHost.Create (width, height)
            page <- Some host
            new Window (window, drawing, host, egl, swapChain, rates, frame)
        with _ ->
            page
            |> Option.iter (fun host -> (host :> IDisposable).Dispose())

            let cleanup () =
                if
                    cleanupEgl
                    |> Option.forall (fun consumer -> consumer.Activate ())
                then
                    swapChain
                    |> Option.iter (fun chain -> (chain :> IDisposable).Dispose())

                    if not (NativePtr.isNullPtr drawing) then
                        SDL3.SDL_DestroyRenderer drawing

                    SDL3.SDL_DestroyWindow window
                    SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO
                    true
                else
                    false

            if not (cleanup ()) then
                EglRenderer.RetainFailedConstruction cleanup

            reraise ()

    /// <summary>
    /// Opens a window of <c>width</c> by <c>height</c> in the platform's window units, CSS pixels at the display's
    /// scale, with an empty page. <c>hidden</c> keeps it off screen; <c>renderer</c> names an SDL render driver, such
    /// as "software", and "direct3d11" or no name uses a D3D11 swap chain with no SDL renderer where the runtime
    /// supports it. <c>inputRates</c> sets <c>InputRates</c>, <c>InputRates.Default</c> when omitted. <c>frame</c> is
    /// <c>WindowFrame.Native</c> when omitted. <c>minimumSize</c> is a width and height in window units.
    /// </summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><c>frame</c> is <c>WindowFrame.Custom</c> with a negative border.</exception>
    static member Create
        (
            title: string,
            width: int,
            height: int,
            ?hidden: bool,
            ?renderer: string,
            ?inputRates: InputRates,
            ?frame: WindowFrame,
            ?minimumSize: struct (int * int)
        ) : Window =
        match frame with
        | Some (WindowFrame.Custom border) when border < 0 ->
            raise (ArgumentOutOfRangeException (nameof frame, border, "A custom frame's resize border is at least zero."))
        | _ -> ()

        owner.Enter ()

        try
            Window.CreateCore (
                title,
                width,
                height,
                hidden,
                renderer,
                defaultArg inputRates InputRates.Default,
                defaultArg frame WindowFrame.Native,
                minimumSize
            )
        finally
            owner.Exit ()

    /// <summary>
    /// The graph holding <c>Placement</c> and <c>Focused</c>. A view reading them mounts on this graph: a computation
    /// on another graph reads their first value and never re-runs.
    /// </summary>
    member _.Graph = graph

    /// <summary>How the window occupies its display, on <c>Graph</c>.</summary>
    member _.Placement: Memo<WindowPlacement> = placementMemo

    /// <summary>Whether the window has input focus, on <c>Graph</c>.</summary>
    member _.Focused: Memo<bool> = focusedMemo

    /// <summary>The window's frame.</summary>
    member _.Frame = frame

    /// <summary>The window's title.</summary>
    member _.Title
        with get () = SDL3.SDL_GetWindowTitle window
        and set (value: string) =
            use name = fixed Sdl.utf8 value
            SDL3.SDL_SetWindowTitle (window, name) |> ignore

    /// <summary>Shows a window created with <c>hidden</c>.</summary>
    member _.Show() =
        SDL3.SDL_ShowWindow window |> ignore

    /// <summary>Minimizes the window.</summary>
    member _.Minimize() =
        SDL3.SDL_MinimizeWindow window |> ignore

    /// <summary>Maximizes a restored window and restores a maximized one.</summary>
    member _.ToggleMaximize() =
        if placementReader () = WindowPlacement.Maximized then
            SDL3.SDL_RestoreWindow window |> ignore
        else
            SDL3.SDL_MaximizeWindow window |> ignore

    /// <summary>Opens the platform's window menu at a CSS position in the page.</summary>
    member _.ShowSystemMenu(x: int, y: int) =
        systemMenu (int (float32 x / toCss)) (int (float32 y / toCss))

    /// <summary>Sets the window's icon from top-down RGBA bytes.</summary>
    /// <exception cref="T:System.ArgumentException"><c>rgba</c> does not hold four bytes per pixel.</exception>
    member _.SetIcon(width: int, height: int, rgba: byte array) =
        if rgba.Length <> width * height * 4 then
            invalidArg (nameof rgba) "An icon has four bytes per pixel."

        use pixels = fixed rgba

        // ABGR8888 is R, G, B, A in byte order on the little-endian platforms SDL windows run on here.
        let surface =
            SDL3.SDL_CreateSurfaceFrom (width, height, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, NativePtr.toNativeInt pixels, width * 4)

        if NativePtr.isNullPtr surface then
            raise (Sdl.failure "SDL_CreateSurfaceFrom")

        SDL3.SDL_SetWindowIcon (window, surface) |> ignore
        SDL3.SDL_DestroySurface surface

    /// <summary>Runs the window's loop until it closes.</summary>
    member this.Run() =
        while this.Step () do
            ()

    /// <summary>The page's region at a window position.</summary>
    member internal _.RegionAt(x: float32, y: float32) =
        regionAt x y

    /// <summary>The SDL hit test result at a window position; a native frame's is always normal.</summary>
    member internal _.HitTest(x: int, y: int) =
        match frame with
        | WindowFrame.Custom border -> hitTest border x y
        | WindowFrame.Native -> SDL_HitTestResult.SDL_HITTEST_NORMAL

    /// <summary>The number of region queries against the page.</summary>
    member internal _.RegionReads = regionReads

    /// <summary>Replaces how the window reads its placement; tests stand in for showing a window.</summary>
    member internal _.SetPlacementReader(reader: unit -> WindowPlacement) =
        placementReader <- reader

    /// <summary>Replaces how the window opens its menu; tests stand in for the platform's modal menu.</summary>
    member internal _.SetSystemMenu(menu: int -> int -> unit) =
        systemMenu <- menu

    /// <summary>Replaces the page work of a modal turn, for failure checks.</summary>
    member internal _.SetModalFrame(turn: unit -> unit) =
        modalFrame <- turn

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

    /// <summary>How often the window dispatches each kind of high-rate input to its page.</summary>
    /// <remarks>Setting it dispatches any held input.</remarks>
    member _.InputRates
        with get () = rates
        and set (value: InputRates) =
            flushHeld HeldKind.Barrier
            rates <- value

    /// <summary>The number of page resizes the window has applied.</summary>
    member internal _.ResizesApplied = resizesApplied

    /// <summary>Whether the window has been asked to close.</summary>
    member _.Closed = closed

    /// <summary>Whether the focused window's SDL input backend has valid surrounding text.</summary>
    member _.ImeSurroundingAvailable = surrounding.Available

    /// <summary>Opts into bounded control context when the runtime supports it; disabled by default.</summary>
    /// <remarks>Composition and unsupported editable structures report unavailable surrounding text.</remarks>
    member _.BoundedImeContext
        with get () = surrounding.Bounded
        and set value =
            host.EnsureActive ()
            surrounding.Bounded <- value
            surrounding.Refresh ()

    /// <summary>Asks the window to close: the next <c>Step</c> returns false.</summary>
    member _.Close() =
        closed <- true

    /// <summary>
    /// Runs a frame and shows it: the page's animation frame callbacks and rendering update, then the painted area in
    /// the texture, presented on the window's next vertical blank.
    /// </summary>
    /// <remarks>
    /// A frame callback that closes the window or disposes its host stops presentation. On a window with a D3D11 swap
    /// chain the present runs on its own thread, so a present that blocks leaves this call and <c>Step</c> running.
    /// </remarks>
    member this.Present() =
        presenter.Invalidate ()
        this.PresentPending ()

    member private this.PresentPending() =
        let outer = onPage
        onPage <- true

        try
            this.PresentFrame ()
        finally
            onPage <- outer

    member private _.PresentFrame() =
        if host.FrameRequested || fullPaint || framePending then
            presenter.Invalidate ()

        flushHeld HeldKind.Barrier
        host.Frame ()
        lastFrameAt <- now ()

        if alive () then
            let hidden =
                SDL3.SDL_GetWindowFlags window
                &&& (SDL_WindowFlags.SDL_WINDOW_HIDDEN
                     ||| SDL_WindowFlags.SDL_WINDOW_MINIMIZED)
                <> LanguagePrimitives.EnumOfValue 0UL

            presenter.Pace (
                not hidden,
                int (
                    refreshTicks * 1000L
                    / Diagnostics.Stopwatch.Frequency
                )
            )

            if not presenter.Presenting then
                completePresentationTrace ()

            let submittedFrame = host.SchedulingTrace.FrameId
            let presentStarted = host.SchedulingTrace.Start ()
            let shown = presenter.Present exposed

            if
                not presenter.GpuFramePipelineActive
                || not presenter.Presenting
            then
                completePresentationTrace ()

            let submittedFrame =
                match presenter.PresentedFrameId with
                | ValueSome identity when shown -> identity
                | _ -> submittedFrame

            host.SchedulingTrace.Finish (SchedulingPhase.Present, presentStarted, 0, frameId = submittedFrame)

            if shown then
                tracedPresentation <- ValueSome submittedFrame

                if not presenter.Presenting then
                    completePresentationTrace ()

            framePending <- false
            fullPaint <- false
            exposed <- not shown
            placeInputMethod ()

    /// <summary>
    /// One turn of the window's loop: waits up to <c>timeout</c> milliseconds (50 by default), less when a timer is due
    /// sooner or a frame is wanted, for window events and dispatches them to the page, cycles the host, then presents a
    /// frame if the page asked for one. Returns false once the window has been asked to close.
    /// </summary>
    /// <remarks>
    /// Drains SDL's global event queue for all live Ranvier windows.
    /// An application that owns the SDL event pump runs its own loop instead.
    /// </remarks>
    /// <exception cref="T:System.Exception">
    /// A failure inside a native callback since the previous call: a modal move or resize turn,
    /// a hit test or the maximize button's input.
    /// </exception>
    member this.Step(?timeout: int) : bool =
        owner.Ensure ()

        if not host.Session.IsOwnerThread then
            invalidOp "An SDL window is stepped on the thread that created it"

        rethrowNativeFailure ()
        let outer = onPage
        onPage <- true

        try
            let running = this.Turn (defaultArg timeout 50, outer)
            this.CompleteDispose ()
            rethrowNativeFailure ()
            running
        finally
            onPage <- outer

    // Finishes a Dispose requested inside a native callback.
    member private this.CompleteDispose() =
        if disposePending && callbackDepth = 0 then
            disposePending <- false
            (this :> IDisposable).Dispose()

    // `outer` is onPage on entry to Step, restored while SDL pumps.
    /// <summary>Opt-in cooperative posted-work and SDL event budgets. None retains exhaustive draining.</summary>
    member _.InteractiveBudget
        with get () = interactiveBudget
        and set (value: OwnerTurnBudget option) =
            host.EnsureActive ()

            value
            |> Option.iter (fun budget -> budget.Validate ())

            interactiveBudget <- value

    member private _.CycleHost() =
        cycleHost ()

    /// <summary>Opts into bounded nonblocking D3D11 transport when the native runtime supports it.</summary>
    /// <remarks>False by default. Congestion retains the previous image and retries at the display interval.
    /// Unsupported platforms retain their existing presentation path.</remarks>
    member _.GpuFramePipeline
        with get () = presenter.GpuFramePipeline
        and set value =
            host.EnsureActive ()
            presenter.GpuFramePipeline <- value

    member private this.Turn(longest: int, outer: bool) : bool =
        if not closed then
            host.EnsureActive ()

            if presenter.Settle () then
                exposed <- true

            if not presenter.Presenting then
                completePresentationTrace ()

            for target in windows.Snapshot () do
                target.RefreshSurrounding ()

            // A frame runs without waiting for the screen once a present has run this long past the last frame.
            let stall = refreshTicks * 3L / 2L

            let wait =
                if
                    eventDrainPending
                    || (windows.Snapshot ()
                        |> Seq.exists (fun target -> target.Host.HasPendingWork))
                then
                    0
                elif
                    (host.FrameRequested
                     || fullPaint
                     || exposed
                     || framePending
                     || presenter.PendingGpuFrame)
                    && not presenter.Presenting
                then
                    let retry = presenter.GpuRetryDelay

                    let due =
                        match host.NextTimerDelay with
                        | ValueSome delay -> min retry (max 0 (int (Math.Ceiling delay)))
                        | ValueNone -> retry

                    min longest due
                else
                    let timer =
                        match host.NextTimerDelay with
                        | ValueSome delay -> min longest (int (Math.Ceiling delay))
                        | ValueNone -> longest

                    let held =
                        windows.Snapshot ()
                        |> Seq.fold
                            (fun wait (target: Window) ->
                                match target.HeldDue with
                                | ValueSome due -> min wait due
                                | ValueNone -> wait)
                            timer

                    if presenter.Presenting && host.FrameRequested then
                        let ticks = lastFrameAt + stall - now ()

                        if ticks <= 0L then
                            0
                        else
                            min
                                held
                                (1
                                 + int (
                                     Math.Ceiling (
                                         float ticks * 1000.0
                                         / float Diagnostics.Stopwatch.Frequency
                                     )
                                 ))
                    else
                        held

            let mutable event = SDL_Event ()

            // A platform move or resize loop runs inside these two SDL calls.
            onPage <- outer
            let received = Sdl.ok (SDL3.SDL_WaitEventTimeout (&&event, min wait 50))
            onPage <- true

            let drainStarted = now ()
            let mutable eventCount = 0

            let withinEventBudget () =
                interactiveBudget.IsNone
                || (eventCount < 256
                    && float (now () - drainStarted) * 1000.0
                       / float Diagnostics.Stopwatch.Frequency
                        <
                        2.0)

            eventDrainPending <- false

            if received then
                eventCount <- 1

                for target in windows.Snapshot () do
                    target.Dispatch event
                    target.RefreshAfterInput ()

                while alive ()
                      && withinEventBudget ()
                      && (onPage <- outer
                          let polled = Sdl.ok (SDL3.SDL_PollEvent &&event)
                          onPage <- true
                          polled) do
                    eventCount <- eventCount + 1

                    for target in windows.Snapshot () do
                        target.Dispatch event
                        target.RefreshAfterInput ()

                eventDrainPending <- not (withinEventBudget ())

            if not eventDrainPending then
                for target in windows.Snapshot () do
                    target.FlushComposition ()

            host.SchedulingTrace.Record (
                SchedulingPhase.EventDrain,
                drainStarted,
                now () - drainStarted,
                (if eventDrainPending then 1 else 0),
                processed = eventCount
            )

            for target in windows.Snapshot () do
                target.EndDrain ()

            for target in windows.Snapshot () do
                if
                    not target.Host.Session.IsDisposed
                    && (obj.ReferenceEquals (target, this)
                        || target.Host.HasPendingWork)
                then
                    target.CycleHost ()

            if
                alive ()
                && (host.FrameRequested
                    || fullPaint
                    || exposed
                    || framePending
                    || presenter.PendingGpuFrame)
            then
                if not presenter.Presenting then
                    if presenter.GpuRetryDelay = 0 then
                        this.PresentPending ()
                elif
                    host.FrameRequested
                    && now () - lastFrameAt >= stall
                then
                    flushHeld HeldKind.Barrier
                    host.Frame ()
                    lastFrameAt <- now ()
                    framePending <- true

        alive ()

    /// <summary>The rendered page's physical dimensions and top-down RGBA bytes.</summary>
    member internal _.ReadPixels() : struct (int * int * byte array) =
        presenter.ReadPixels ()

    /// <summary>The number of shared GPU frames presented to the window.</summary>
    member internal _.GpuFramesPresented = presenter.GpuFramesPresented

    /// <summary>The number of presentation attempts that took the CPU path.</summary>
    member internal _.CpuFramesPresented = presenter.CpuFramesPresented

    /// <summary>The D3D11 swap chain presenting this window's frames on its own thread, when it has one.</summary>
    member internal _.SwapChain = presenter.SwapChain

    /// <summary>Whether Windows UI Automation or the optional Linux AT-SPI provider is attached.</summary>
    /// <remarks>The Linux provider requires its native module and an accessible desktop bus. Unavailable providers return false.</remarks>
    member _.AccessibilityAttached =
        not host.Session.IsDisposed
        && (accessibilityAttached
            || (linuxAccessibility
                |> Option.exists (fun bridge -> bridge.Attached)))

    /// <summary>Whether the optional native AT-SPI token still belongs to this window.</summary>
    member internal _.AtspiLeaseActive =
        linuxAccessibility
        |> Option.exists (fun bridge -> bridge.Attached)

    /// <summary>The display the host's screen follows, as the window last read it.</summary>
    member internal _.Display = displaySignal.Value

    /// <summary>Replaces how the window reads its display on a display event; tests stand in for SDL's displays.</summary>
    member internal _.SetDisplayReader(reader: unit -> DisplayScreen voption) =
        displayReader <- reader

    /// <summary>Observes completed page cleanup phases on this window's owner thread.</summary>
    member internal _.SetCleanupCompleted(callback: string -> unit) =
        cleanupCompleted <- callback

    /// <summary>Wraps this window's native EGL frame release; false means its renderer uses another transport.</summary>
    member internal _.SetEglFrameRelease(release: nativeint -> int) =
        match egl with
        | Some consumer ->
            consumer.SetFrameRelease release
            true
        | None -> false

    /// <summary>The actual frame retained until its EGL consumer can release it.</summary>
    member internal _.PendingEglFrame = presenter.PendingEglFrame

    /// <summary>The IOSurface frame retained until its Metal consumer finishes and native release succeeds.</summary>
    member internal _.PendingIOSurfaceFrame = presenter.PendingIOSurfaceFrame

    /// <summary>Wraps this window's actual Metal queue completion for failure and retry controls.</summary>
    member internal _.SetIOSurfaceCompletion(wrapper: (unit -> unit) -> unit) =
        presenter.SetIOSurfaceCompletion wrapper

    /// <summary>Wraps this window's native IOSurface release for failure and retry controls.</summary>
    member internal _.SetIOSurfaceFrameRelease(release: nativeint -> int) =
        presenter.SetIOSurfaceFrameRelease release

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                if not host.Session.IsOwnerThread then
                    invalidOp "An SDL window is disposed on the thread that created it"

            if not disposed && callbackDepth > 0 then
                closed <- true
                disposePending <- true
            elif not disposed then
                lock wakeGate (fun () -> wakeActive <- false)
                host.SetOwnerWake None

                if not (isNull hitTestCallback) then
                    SDL3.SDL_SetWindowHitTest (window, 0n, 0n)
                    |> ignore

                snapButton
                |> Option.iter (fun button -> button.Uninstall ())

                snapButton <- None

                if eventWatchPointer <> 0n then
                    SDL3.SDL_RemoveEventWatch (eventWatchPointer, 0n)
                    eventWatchPointer <- 0n

                presenter.ReleasePending ()

                if not pageDisposed then
                    PageCleanup.run
                        [|
                            (fun () ->
                                linuxAccessibility
                                |> Option.iter (fun bridge -> (bridge :> IDisposable).Dispose())

                                if accessibilityAttached then
                                    try
                                        WindowAccessibility.detach host
                                    finally
                                        accessibilityAttached <- false

                                cleanupCompleted "accessibility")
                            (fun () ->
                                if not (isNull displayFeed) then
                                    displayFeed.Dispose ()

                                stateOwner.Dispose ()
                                cleanupCompleted "display")
                            (fun () ->
                                (graph :> IDisposable).Dispose()
                                cleanupCompleted "graph")
                            (fun () ->
                                (presenter :> IDisposable).Dispose()
                                cleanupCompleted "presenter")
                            (fun () ->
                                surrounding.Clear ()
                                (host :> IDisposable).Dispose()
                                cleanupCompleted "host")
                        |]
                        (fun () ->
                            pageDisposed <- true
                            windows.Remove (uint32 id))

                egl
                |> Option.iter (fun consumer ->
                    if not (consumer.Activate ()) then
                        raise (Sdl.failure "SDL_GL_MakeCurrent"))

                for created in cursors.Values do
                    SDL3.SDL_DestroyCursor (NativePtr.ofNativeInt created)

                SDL3.SDL_StopTextInput window |> ignore

                if not (NativePtr.isNullPtr renderer) then
                    SDL3.SDL_DestroyRenderer renderer

                SDL3.SDL_DestroyWindow window
                SDL3.SDL_QuitSubSystem SDL_InitFlags.SDL_INIT_VIDEO

                disposed <- true

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
