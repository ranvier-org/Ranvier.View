module internal NativeApiConformance.ValueFactoryChecks

open System
open Ranvier.Dom

// The values of the D17 producer oracle (Ranvier.WebCore tests/fixtures/value-factories-oracle.json) that both engines
// share; the qualified engine differences (an empty parseHTMLUnsafe compat mode, a complete tilt pair and the legacy
// wheel delta signs) are outside this fixture.
let private full =
    {
        Bubbles = true
        Cancelable = true
        Composed = true
    }

/// <summary>
/// Checks every value factory: node, range and document defaults and mutation, static range bounds and node type
/// errors, Input/Wheel/Pointer event defaults and full inits, dispatch outcomes, float-array matrices and argument
/// validation; the result is the assertion count.
/// </summary>
let run (document: Document) (create: unit -> ValueFactoryService) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected %A{expected}, got %A{actual}"

    let truth label (condition: bool) =
        equal label true condition

    // Providers raise ArgumentNullException and ArgumentException; the native lifetime checks assert the types,
    // which Fable erases.
    let raises label (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with _ ->
                true

        if not raised then
            failwith $"{label}: expected an exception"

    let engineError label (expected: string) (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                "none"
            with
            | ValueFactoryException (name, _) -> name
            | error -> "other: " + error.Message

        if name <> expected then
            failwith $"{label}: expected ValueFactoryException {expected}, got {name}"

    let same label (expected: obj) (actual: obj) =
        truth label (Object.ReferenceEquals (expected, actual))

    let options label (event: Event) (expected: EventOptions) (eventType: string) =
        equal (label + " type") eventType event.``type``
        equal (label + " bubbles") expected.Bubbles event.bubbles
        equal (label + " cancelable") expected.Cancelable event.cancelable
        equal (label + " composed") expected.Composed event.composed
        equal (label + " isTrusted") false event.isTrusted

    use service = create ()
    let body = document.body
    let paragraph = document.createElement "p"
    body.appendChild paragraph |> ignore
    let hello = service.CreateText "hello"
    paragraph.appendChild hello |> ignore

    // Text and Comment.
    let text = service.CreateText ""
    equal "Text nodeType" 3us text.nodeType
    equal "Text default data" "" text.data
    same "Text ownerDocument" document text.ownerDocument
    equal "Text isConnected" false text.isConnected
    truth "Text parentNode" (isNull text.parentNode)
    let accented = service.CreateText "héllo \U0001F600"
    equal "Text data" "héllo \U0001F600" accented.data
    accented.data <- "changed"
    equal "Text mutated" "changed" accented.data
    let comment = service.CreateComment "note"
    equal "Comment nodeType" 8us comment.nodeType
    equal "Comment data" "note" comment.data
    same "Comment ownerDocument" document comment.ownerDocument
    raises "A null Text data" (fun () -> service.CreateText null |> ignore)
    raises "A null Comment data" (fun () -> service.CreateComment null |> ignore)

    // DocumentFragment.
    let fragment = service.CreateDocumentFragment ()
    equal "DocumentFragment nodeType" 11us fragment.nodeType
    equal "DocumentFragment empty" 0u fragment.childNodes.length

    fragment.appendChild (service.CreateText "x")
    |> ignore

    equal "DocumentFragment appended" 1u fragment.childNodes.length
    equal "DocumentFragment isConnected" false fragment.isConnected

    // Range.
    let range = service.CreateRange ()
    same "Range startContainer" document range.startContainer
    equal "Range startOffset" 0u range.startOffset
    equal "Range collapsed" true range.collapsed
    range.setStart (hello, 1u)
    range.setEnd (hello, 3u)
    same "Range set startContainer" hello range.startContainer
    equal "Range set offsets" (1u, 3u) (range.startOffset, range.endOffset)
    equal "Range set collapsed" false range.collapsed

    // Document and parseHTMLUnsafe.
    let created = service.CreateDocument ()
    equal "Document nodeType" 9us created.nodeType
    equal "Document contentType" "application/xml" created.contentType
    equal "Document characterSet" "UTF-8" created.characterSet
    equal "Document compatMode" "CSS1Compat" created.compatMode
    truth "Document documentElement" (isNull created.documentElement)
    truth "Document is new" (not (Object.ReferenceEquals (created, document)))
    let root = created.createElement "root"
    truth "Document createElement namespace" (isNull root.namespaceURI)
    created.appendChild root |> ignore
    let moved = service.CreateText "y"
    root.appendChild moved |> ignore
    same "A node appended to the new Document is adopted" created moved.ownerDocument
    let parsed = service.ParseHtmlUnsafe "<!doctype html><p id=\"a\">one</p><p>two</p>"
    equal "parseHTMLUnsafe contentType" "text/html" parsed.contentType
    equal "parseHTMLUnsafe compatMode" "CSS1Compat" parsed.compatMode
    equal "parseHTMLUnsafe body children" 2u parsed.body.childNodes.length
    equal "parseHTMLUnsafe first child" 10us parsed.firstChild.nodeType
    raises "A null html" (fun () -> service.ParseHtmlUnsafe null |> ignore)

    // StaticRange.
    let bounds = service.CreateStaticRange (hello, 1u, hello, 9u)
    same "StaticRange startContainer" hello bounds.startContainer
    equal "StaticRange offsets past the length" (1u, 9u) (bounds.startOffset, bounds.endOffset)
    equal "StaticRange collapsed" false bounds.collapsed
    equal "StaticRange collapsed at a point" true (service.CreateStaticRange (paragraph, 0u, paragraph, 0u)).collapsed

    engineError "A StaticRange in an Attr" "InvalidNodeTypeError" (fun () ->
        service.CreateStaticRange (document.createAttribute "x", 0u, hello, 0u)
        |> ignore)

    engineError "A StaticRange in a DocumentType" "InvalidNodeTypeError" (fun () ->
        service.CreateStaticRange (hello, 0u, parsed.firstChild, 0u)
        |> ignore)

    raises "A null StaticRange container" (fun () ->
        service.CreateStaticRange (hello, 0u, null, 0u)
        |> ignore)

    // InputEvent.
    let input =
        service.CreateInputEvent ("beforeinput", EventOptions.Default, InputFields.Default)

    options "InputEvent default" input EventOptions.Default "beforeinput"
    truth "InputEvent default data" (isNull input.data)
    equal "InputEvent default fields" (false, "", 0) (input.isComposing, input.inputType, input.getTargetRanges().Length)

    let input =
        service.CreateInputEvent (
            "beforeinput",
            full,
            {
                Detail = 2
                Data = "abé"
                IsComposing = true
                InputType = "insertText"
            }
        )

    options "InputEvent full" input full "beforeinput"
    equal "InputEvent full fields" (2, "abé", true, "insertText") (input.detail, input.data, input.isComposing, input.inputType)
    equal "InputEvent empty data" "" (service.CreateInputEvent ("input", EventOptions.Default, { InputFields.Default with Data = "" })).data

    raises "A null InputType" (fun () ->
        service.CreateInputEvent (
            "input",
            EventOptions.Default,
            { InputFields.Default with
                InputType = null
            }
        )
        |> ignore)

    // WheelEvent.
    let wheel =
        service.CreateWheelEvent ("wheel", EventOptions.Default, MouseFields.Default, WheelFields.Default)

    options "WheelEvent default" wheel EventOptions.Default "wheel"
    equal "WheelEvent default deltas" (0.0, 0.0, 0.0, 0u) (wheel.deltaX, wheel.deltaY, wheel.deltaZ, wheel.deltaMode)
    equal "WheelEvent default wheel deltas" (0, 0) (wheel.wheelDeltaX, wheel.wheelDeltaY)
    equal "WheelEvent default mouse" (0.0, 0s, 0us) (wheel.clientX, wheel.button, wheel.buttons)
    let wheelOptions = { full with Composed = false }

    let wheel =
        service.CreateWheelEvent (
            "wheel",
            wheelOptions,
            { MouseFields.Default with
                ClientX = 10.0
                ClientY = 20.0
                ScreenX = 1.0
                ScreenY = 2.0
                Buttons = 1
                Modifiers = EventModifiers.Control ||| EventModifiers.Meta
                RelatedTarget = paragraph
            },
            {
                DeltaX = 3.5
                DeltaY = -120.0
                DeltaZ = 1.0
                DeltaMode = 1u
                WheelDeltaX = 0
                WheelDeltaY = 0
            }
        )

    options "WheelEvent full" wheel wheelOptions "wheel"
    equal "WheelEvent full deltas" (3.5, -120.0, 1.0, 1u) (wheel.deltaX, wheel.deltaY, wheel.deltaZ, wheel.deltaMode)
    equal "WheelEvent full mouse" (10.0, 20.0, 1.0, 2.0, 1us) (wheel.clientX, wheel.clientY, wheel.screenX, wheel.screenY, wheel.buttons)
    equal "WheelEvent full modifiers" (true, false, false, true) (wheel.ctrlKey, wheel.shiftKey, wheel.altKey, wheel.metaKey)
    same "WheelEvent relatedTarget" paragraph wheel.relatedTarget

    raises "A NaN delta" (fun () ->
        service.CreateWheelEvent (
            "wheel",
            EventOptions.Default,
            MouseFields.Default,
            { WheelFields.Default with
                DeltaX = nan
            }
        )
        |> ignore)

    // PointerEvent.
    let pointer =
        service.CreatePointerEvent ("pointerdown", EventOptions.Default, MouseFields.Default, PointerFields.Default)

    options "PointerEvent default" pointer EventOptions.Default "pointerdown"
    equal "PointerEvent default identity" (0, "", false) (pointer.pointerId, pointer.pointerType, pointer.isPrimary)
    equal "PointerEvent default contact" (1.0, 1.0, 0.0f, 0.0f) (pointer.width, pointer.height, pointer.pressure, pointer.tangentialPressure)
    equal "PointerEvent default tilt" (0, 0, 0) (pointer.tiltX, pointer.tiltY, pointer.twist)
    equal "PointerEvent default sub-events" (0, 0) (pointer.getCoalescedEvents().Length, pointer.getPredictedEvents().Length)

    let sub =
        service.CreatePointerEvent (
            "pointermove",
            EventOptions.Default,
            MouseFields.Default,
            { PointerFields.Default with
                PointerId = 7
            }
        )

    let pointer =
        service.CreatePointerEvent (
            "pointermove",
            full,
            { MouseFields.Default with
                ClientX = 12.5
                ClientY = 7.25
                Button = 1
                Buttons = 4
                RelatedTarget = paragraph
            },
            { PointerFields.Default with
                PointerId = 7
                Width = 2.5
                Height = 3.5
                Pressure = 0.5f
                TangentialPressure = -0.25f
                Twist = 90
                PointerType = "pen"
                IsPrimary = true
                CoalescedEvents = [| sub |]
                PredictedEvents = [| sub; sub |]
            }
        )

    options "PointerEvent full" pointer full "pointermove"
    equal "PointerEvent full identity" (7, "pen", true) (pointer.pointerId, pointer.pointerType, pointer.isPrimary)

    equal
        "PointerEvent full contact"
        (2.5, 3.5, 0.5f, -0.25f, 90)
        (pointer.width, pointer.height, pointer.pressure, pointer.tangentialPressure, pointer.twist)

    equal "PointerEvent fractional coordinates" (12.5, 7.25, 1s, 4us) (pointer.clientX, pointer.clientY, pointer.button, pointer.buttons)
    same "PointerEvent relatedTarget" paragraph pointer.relatedTarget
    equal "PointerEvent sub-event counts" (1, 2) (pointer.getCoalescedEvents().Length, pointer.getPredictedEvents().Length)
    same "PointerEvent coalesced identity" sub (pointer.getCoalescedEvents().[0])

    equal
        "PointerEvent single tilt"
        30
        (service.CreatePointerEvent (
            "pointerdown",
            EventOptions.Default,
            MouseFields.Default,
            { PointerFields.Default with
                TiltX = Some 30
            }
        ))
            .tiltX

    raises "A null sub-event" (fun () ->
        service.CreatePointerEvent (
            "pointermove",
            EventOptions.Default,
            MouseFields.Default,
            { PointerFields.Default with
                CoalescedEvents = [| null |]
            }
        )
        |> ignore)

    raises "A NaN width" (fun () ->
        service.CreatePointerEvent (
            "pointermove",
            EventOptions.Default,
            MouseFields.Default,
            { PointerFields.Default with
                Width = nan
            }
        )
        |> ignore)

    // Dispatch outcomes: an untrusted cancelable event dispatched to a canceling listener at target.
    let seen = Collections.Generic.List<string>()

    let listener: EventListener =
        fun event ->
            seen.Add (
                string event.eventPhase
                + ":"
                + string event.isTrusted
            )

            event.preventDefault ()

    paragraph.addEventListener ("wheel", listener)

    let cancelable =
        service.CreateWheelEvent (
            "wheel",
            { EventOptions.Default with
                Cancelable = true
                Bubbles = true
            },
            MouseFields.Default,
            WheelFields.Default
        )

    equal "A canceled dispatch" (false, true) (paragraph.dispatchEvent cancelable, cancelable.defaultPrevented)

    let plain =
        service.CreateWheelEvent ("wheel", EventOptions.Default, MouseFields.Default, WheelFields.Default)

    equal "A non-cancelable dispatch" (true, false) (paragraph.dispatchEvent plain, plain.defaultPrevented)
    paragraph.removeEventListener ("wheel", listener)
    paragraph.dispatchEvent cancelable |> ignore

    equal
        "Listener calls at target"
        [ "2:False"; "2:False" ]
        (List.ofSeq seen
         |> List.map (fun item -> item.Replace ("false", "False")))

    // Matrices.
    let six = [| 0.1; 0.0; 0.0; 1.0; 5.0; 6.0 |]

    let sixteen =
        [|
            1.0
            2.0
            0.0
            0.0
            3.0
            4.0
            0.0
            0.0
            0.0
            0.0
            1.0
            0.0
            5.0
            6.0
            0.0
            1.0
        |]

    let single = service.MatrixFromFloat32Array (Array.map float32 six)
    equal "fromFloat32Array widens" 0.10000000149011612 single.a
    equal "fromFloat32Array translation" (5.0, 6.0, true, false) (single.e, single.f, single.is2D, single.isIdentity)
    let double = service.MatrixFromFloat64Array six
    equal "fromFloat64Array" (0.1, 5.0, 6.0, true) (double.a, double.e, double.f, double.is2D)
    double.a <- 2.0
    equal "A DOMMatrix is mutable" 2.0 double.a
    let deep = service.MatrixFromFloat64Array sixteen
    equal "fromFloat64Array 3D" (1.0, 2.0, 3.0, 4.0, 5.0, 6.0, false) (deep.m11, deep.m12, deep.m21, deep.m22, deep.m41, deep.m42, deep.is2D)
    let readOnly = service.ReadOnlyMatrixFromFloat32Array (Array.map float32 six)
    equal "DOMMatrixReadOnly.fromFloat32Array" (0.10000000149011612, true) (readOnly.a, readOnly.is2D)
    equal "DOMMatrixReadOnly.fromFloat64Array 3D" 5.0 (service.ReadOnlyMatrixFromFloat64Array sixteen).m41

    engineError "Five elements" "TypeError" (fun () ->
        service.MatrixFromFloat64Array (Array.zeroCreate 5)
        |> ignore)

    engineError "No elements" "TypeError" (fun () -> service.MatrixFromFloat32Array [||] |> ignore)

    engineError "Seventeen elements" "TypeError" (fun () ->
        service.ReadOnlyMatrixFromFloat32Array (Array.zeroCreate 17)
        |> ignore)

    raises "A null array" (fun () -> service.MatrixFromFloat64Array null |> ignore)

    body.removeChild paragraph |> ignore
    checks
