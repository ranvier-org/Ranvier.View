namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Standalone nodes, ranges, documents, events and matrices of a native host.</summary>
module ValueFactories =
    type private Create = delegate of nativeint * byref<nativeint> -> int

    let private features =
        [
            "dom-constructor:Text"
            "dom-constructor:Comment"
            "dom-constructor:DocumentFragment"
            "dom-constructor:Range"
            "dom-constructor:StaticRange"
            "dom-constructor:Document"
            "dom-constructor:InputEvent"
            "dom-constructor:WheelEvent"
            "dom-constructor:PointerEvent"
            "dom-document-parse-html"
            "dom-geometry-matrix"
            "dom-geometry-matrix-readonly"
        ]

    /// <summary>A value factory service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks a factory. Nodes, targets and events of another
    /// host session raise ArgumentException; an engine exception raises ValueFactoryException.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ValueFactoryService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        for feature in features do
            if not (WebCoreHost.Has feature) then
                raise (NotSupportedException ("The native library has no " + feature))

        let session = host.Session
        let owner = unbox<NativeObject>(box host.Document)
        let disposed = ref false

        let target () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof ValueFactoryService))

            host.EnsureActive ()
            Interop.handle (box owner)

        let flag value =
            if value then 1 else 0

        let utf8 (value: string) =
            Encoding.UTF8.GetBytes value

        let own (name: string) (value: obj) =
            match value with
            | :? NativeObject as item when obj.ReferenceEquals (item.Session, session) -> item
            | _ -> invalidArg name (name + " belongs to another provider")

        // The handle a factory returns; an engine exception keeps its name, every other failure the interop exception.
        let call (label: string) (action: Create) =
            let receiver = target ()
            let mutable result = 0n
            let status = action.Invoke (receiver, &result)

            try
                Interop.check label status
            with WebCoreDomException (name, detail) ->
                raise (ValueFactoryException (name, detail))

            result

        let node (label: string) (action: Create) : NativeObject =
            let result = call label action
            session.Node result

        let event (label: string) (action: Create) : NativeObject =
            let result = call label action
            session.Event result

        let range (label: string) (isRange: bool) (action: Create) : NativeObject =
            let result = call label action

            session.Object (
                result,
                Kind.Range,
                owner,
                Func<Session, nativeint, NativeObject>(fun s h ->
                    if isRange then
                        RangeObject (s, h) :> NativeObject
                    else
                        StaticRangeObject (s, h) :> NativeObject)
            )

        let matrix (label: string) (action: Create) : NativeObject =
            let result = call label action

            session.Object (
                result,
                Kind.Matrix,
                owner,
                Func<Session, nativeint, NativeObject>(fun s h ->
                    let value = Classes.DOMMatrixReadOnly (s, h)
                    value.BindDocumentContext owner
                    value)
            )

        // The RvEventTarget of a related target, owned when converted from a node; released by the caller.
        let related (value: EventTarget) =
            match box value with
            | null -> struct (0n, false)
            | other ->
                let item = own "RelatedTarget" other

                if item.Kind = Kind.EventTarget then
                    struct (item.Handle, false)
                else
                    struct (Native.rv_node_as_event_target item.Handle, true)

        let release (struct (handle: nativeint, owned: bool)) =
            if owned then
                Native.rv_event_target_release handle

        let modifiers (value: EventModifiers) =
            struct (flag (value.HasFlag EventModifiers.Control),
                    flag (value.HasFlag EventModifiers.Shift),
                    flag (value.HasFlag EventModifiers.Alt),
                    flag (value.HasFlag EventModifiers.Meta))

        // A pinned sequence of the pointer events' handles, each from this session.
        let pointerEvents (scope: InputScope) (name: string) (events: PointerEvent array) =
            let handles =
                events
                |> Array.map (fun item -> (own name (box item)).Handle)

            let mutable sequence = NativeDom.RvDomSequencePointerEvent ()
            sequence.present <- 1
            sequence.count <- handles.Length
            sequence.items <- (if handles.Length = 0 then 0n else scope.Pin handles)
            scope.Pin [| sequence |]

        let tilt (scope: InputScope) (value: int option) =
            match value with
            | Some number -> scope.Pin [| number |]
            | None -> 0n

        { new ValueFactoryService with
            member _.CreateText data =
                let data = utf8 (ValueFactoryContract.text "data" data)
                unbox<Text>(node "Text constructor" (Create(fun t r -> NativeDom.rv_dom_text_create (t, data, data.Length, &r))))

            member _.CreateComment data =
                let data = utf8 (ValueFactoryContract.text "data" data)
                unbox<Comment>(node "Comment constructor" (Create(fun t r -> NativeDom.rv_dom_comment_create (t, data, data.Length, &r))))

            member _.CreateDocumentFragment() =
                unbox<DocumentFragment>(node "DocumentFragment constructor" (Create(fun t r -> NativeDom.rv_dom_document_fragment_create (t, &r))))

            member _.CreateRange() =
                unbox<Ranvier.Dom.Range>(range "Range constructor" true (Create(fun t r -> NativeDom.rv_dom_range_create (t, &r))))

            member _.CreateStaticRange(startContainer, startOffset, endContainer, endOffset) =
                let start =
                    own "startContainer" (box (ValueFactoryContract.node "startContainer" startContainer))

                let finish =
                    own "endContainer" (box (ValueFactoryContract.node "endContainer" endContainer))

                let mutable init = NativeDom.RvDomStaticRangeInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomStaticRangeInit>())
                init.start_container <- start.Handle
                init.start_offset <- startOffset
                init.end_container <- finish.Handle
                init.end_offset <- endOffset

                unbox<StaticRange>(range "StaticRange constructor" false (Create(fun t r -> NativeDom.rv_dom_static_range_create (t, [| init |], &r))))

            member _.CreateDocument() =
                unbox<Document>(node "Document constructor" (Create(fun t r -> NativeDom.rv_dom_document_create (t, &r))))

            member _.ParseHtmlUnsafe html =
                let html = utf8 (ValueFactoryContract.text "html" html)

                unbox<Document>(node "Document.parseHTMLUnsafe" (Create(fun t r -> NativeDom.rv_dom_document_parse_html_unsafe (t, html, html.Length, &r))))

            member _.CreateInputEvent(eventType, options, fields) =
                let fields = ValueFactoryContract.input eventType fields
                let name = utf8 eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomInputEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomInputEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.detail <- fields.Detail
                init.data <- inputs.TextInput (Option.ofObj fields.Data)
                init.is_composing <- flag fields.IsComposing
                init.input_type <- inputs.TextInput (Some fields.InputType)

                unbox<InputEvent>(
                    event "InputEvent constructor" (Create(fun t r -> NativeDom.rv_dom_input_event_create (t, name, name.Length, [| init |], &r)))
                )

            member _.CreateWheelEvent(eventType, options, mouse, fields) =
                let mouse = ValueFactoryContract.mouse eventType options mouse
                let fields = ValueFactoryContract.wheel fields
                let name = utf8 eventType
                let target = related mouse.RelatedTarget

                try
                    let struct (control, shift, alt, meta) = modifiers mouse.Modifiers
                    let struct (relatedHandle, _) = target
                    let mutable init = NativeDom.RvDomWheelEventInit ()
                    init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomWheelEventInit>())
                    init.bubbles <- flag options.Bubbles
                    init.cancelable <- flag options.Cancelable
                    init.composed <- flag options.Composed
                    init.detail <- mouse.Detail
                    init.ctrl_key <- control
                    init.shift_key <- shift
                    init.alt_key <- alt
                    init.meta_key <- meta
                    init.screen_x <- mouse.ScreenX
                    init.screen_y <- mouse.ScreenY
                    init.client_x <- mouse.ClientX
                    init.client_y <- mouse.ClientY
                    init.button <- int16 mouse.Button
                    init.buttons <- uint16 mouse.Buttons
                    init.related_target <- relatedHandle
                    init.delta_x <- fields.DeltaX
                    init.delta_y <- fields.DeltaY
                    init.delta_z <- fields.DeltaZ
                    init.delta_mode <- fields.DeltaMode
                    init.wheel_delta_x <- fields.WheelDeltaX
                    init.wheel_delta_y <- fields.WheelDeltaY

                    unbox<WheelEvent>(
                        event "WheelEvent constructor" (Create(fun t r -> NativeDom.rv_dom_wheel_event_create (t, name, name.Length, [| init |], &r)))
                    )
                finally
                    release target

            member _.CreatePointerEvent(eventType, options, mouse, fields) =
                let mouse = ValueFactoryContract.mouse eventType options mouse
                let fields = ValueFactoryContract.pointer fields
                let name = utf8 eventType
                use inputs = new InputScope ()
                let coalesced = pointerEvents inputs "CoalescedEvents" fields.CoalescedEvents
                let predicted = pointerEvents inputs "PredictedEvents" fields.PredictedEvents
                let target = related mouse.RelatedTarget

                try
                    let struct (control, shift, alt, meta) = modifiers mouse.Modifiers
                    let struct (relatedHandle, _) = target
                    let mutable init = NativeDom.RvDomPointerEventInit ()
                    init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomPointerEventInit>())
                    init.bubbles <- flag options.Bubbles
                    init.cancelable <- flag options.Cancelable
                    init.composed <- flag options.Composed
                    init.detail <- mouse.Detail
                    init.ctrl_key <- control
                    init.shift_key <- shift
                    init.alt_key <- alt
                    init.meta_key <- meta
                    init.screen_x <- mouse.ScreenX
                    init.screen_y <- mouse.ScreenY
                    init.client_x <- mouse.ClientX
                    init.client_y <- mouse.ClientY
                    init.button <- int16 mouse.Button
                    init.buttons <- uint16 mouse.Buttons
                    init.related_target <- relatedHandle
                    init.pointer_id <- fields.PointerId
                    init.width <- fields.Width
                    init.height <- fields.Height
                    init.pressure <- fields.Pressure
                    init.tangential_pressure <- fields.TangentialPressure
                    init.tilt_x <- tilt inputs fields.TiltX
                    init.tilt_y <- tilt inputs fields.TiltY
                    init.twist <- fields.Twist
                    init.pointer_type <- inputs.TextInput (Some fields.PointerType)
                    init.is_primary <- flag fields.IsPrimary
                    init.coalesced_events <- coalesced
                    init.predicted_events <- predicted

                    unbox<PointerEvent>(
                        event "PointerEvent constructor" (Create(fun t r -> NativeDom.rv_dom_pointer_event_create (t, name, name.Length, [| init |], &r)))
                    )
                finally
                    release target

            member _.MatrixFromFloat32Array values =
                let values = ValueFactoryContract.values values

                unbox<DOMMatrix>(
                    matrix "DOMMatrix.fromFloat32Array" (Create(fun t r -> NativeDom.rv_dom_dom_matrix_from_float32_array (t, values, values.Length, &r)))
                )

            member _.MatrixFromFloat64Array values =
                let values = ValueFactoryContract.values values

                unbox<DOMMatrix>(
                    matrix "DOMMatrix.fromFloat64Array" (Create(fun t r -> NativeDom.rv_dom_dom_matrix_from_float64_array (t, values, values.Length, &r)))
                )

            member _.ReadOnlyMatrixFromFloat32Array values =
                let values = ValueFactoryContract.values values

                unbox<DOMMatrixReadOnly>(
                    matrix "DOMMatrixReadOnly.fromFloat32Array" (Create(fun t r ->
                        NativeDom.rv_dom_dom_matrix_read_only_from_float32_array (t, values, values.Length, &r)))
                )

            member _.ReadOnlyMatrixFromFloat64Array values =
                let values = ValueFactoryContract.values values

                unbox<DOMMatrixReadOnly>(
                    matrix "DOMMatrixReadOnly.fromFloat64Array" (Create(fun t r ->
                        NativeDom.rv_dom_dom_matrix_read_only_from_float64_array (t, values, values.Length, &r)))
                )

            member _.Dispose() =
                disposed.Value <- true
        }
