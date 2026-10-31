module internal NativeApiConformance.CustomEvents

open System
open Ranvier.Dom

/// <summary>Checks typed and custom event construction, synchronous dispatch and standalone targets on one provider.</summary>
let run (document: Document) (create: unit -> EventService) (cancellation: unit -> CancellationService) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label (expected: obj) (actual: obj) =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: expected the same object"

    let raises label (expected: exn -> bool) (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with error ->
                if not (expected error) then
                    failwith $"{label}: unexpected {error.Message}"

                true

        if not raised then
            failwith $"{label}: expected an exception"

    let argument (error: exn) =
        error :? ArgumentException

    let dataClone (error: exn) =
        match error with
        | TimingException ("DataCloneError", _) -> true
        | _ -> false

    use service = create ()

    let options =
        { EventOptions.Default with
            Bubbles = true
            Cancelable = true
            Composed = true
        }

    // Base events.
    let plain = service.Create ("plain", EventConstruction.Event options)
    equal "type" "plain" plain.``type``
    equal "bubbles" true plain.bubbles
    equal "cancelable" true plain.cancelable
    equal "composed" true plain.composed
    equal "untrusted" false plain.isTrusted
    equal "phase before dispatch" 0us plain.eventPhase
    equal "no target before dispatch" true (isNull plain.target)
    equal "plain has no detail" None (service.Detail plain)

    // Custom details are copied portable trees.
    let fields =
        [|
            "items",
            TimingDetail.Array
                [|
                    TimingDetail.Number 1.0
                    TimingDetail.Text "two"
                    TimingDetail.Null
                    TimingDetail.Boolean true
                |]
        |]

    let custom =
        service.Create (
            "custom",
            EventConstruction.Custom (
                { EventOptions.Default with
                    Cancelable = true
                },
                TimingDetail.Object fields
            )
        )

    fields[0] <- ("items", TimingDetail.Null)

    let expectedDetail =
        TimingDetail.Object
            [|
                "items",
                TimingDetail.Array
                    [|
                        TimingDetail.Number 1.0
                        TimingDetail.Text "two"
                        TimingDetail.Null
                        TimingDetail.Boolean true
                    |]
            |]

    equal "custom detail copied" (Some expectedDetail) (service.Detail custom)
    equal "custom detail stable" (service.Detail custom) (service.Detail custom)

    equal
        "null detail"
        (Some TimingDetail.Null)
        (service.Detail (service.Create ("custom", EventConstruction.Custom (EventOptions.Default, TimingDetail.Null))))

    raises "non-finite detail" argument (fun () ->
        service.Create ("x", EventConstruction.Custom (EventOptions.Default, TimingDetail.Number Double.NaN))
        |> ignore)

    let cyclic = [| TimingDetail.Null |]
    cyclic[0] <- TimingDetail.Array cyclic

    raises "cyclic detail" dataClone (fun () ->
        service.Create ("x", EventConstruction.Custom (EventOptions.Default, TimingDetail.Array cyclic))
        |> ignore)

    raises "null type" argument (fun () ->
        service.Create (null, EventConstruction.Event options)
        |> ignore)

    // Typed init dictionaries.
    let target = service.CreateTarget ()
    let host = document.createElement "div"
    let child = document.createElement "span"
    host.appendChild child |> ignore
    document.body.appendChild host |> ignore

    let mouse =
        service.Create (
            "mousedown",
            EventConstruction.Mouse (
                options,
                { MouseFields.Default with
                    Detail = 2
                    ScreenX = 40.0
                    ScreenY = 50.0
                    ClientX = 12.0
                    ClientY = -3.0
                    Button = 2
                    Buttons = 5
                    Modifiers = EventModifiers.Shift ||| EventModifiers.Meta
                    RelatedTarget = target
                }
            )
        )
        :?> MouseEvent

    equal "mouse coordinates" (40.0, 50.0, 12.0, -3.0) (mouse.screenX, mouse.screenY, mouse.clientX, mouse.clientY)
    equal "mouse buttons" (2s, 5us) (mouse.button, mouse.buttons)
    equal "mouse modifiers" (true, false, false, true) (mouse.shiftKey, mouse.ctrlKey, mouse.altKey, mouse.metaKey)
    equal "mouse detail" 2 (mouse :> UIEvent).detail
    same "mouse relatedTarget" target mouse.relatedTarget

    raises "non-finite coordinate" argument (fun () ->
        service.Create (
            "x",
            EventConstruction.Mouse (
                options,
                { MouseFields.Default with
                    ClientX = Double.PositiveInfinity
                }
            )
        )
        |> ignore)

    raises "button range" argument (fun () ->
        service.Create (
            "x",
            EventConstruction.Mouse (
                options,
                { MouseFields.Default with
                    Button = 40000
                }
            )
        )
        |> ignore)

    raises "buttons range" argument (fun () ->
        service.Create (
            "x",
            EventConstruction.Mouse (
                options,
                { MouseFields.Default with
                    Buttons = -1
                }
            )
        )
        |> ignore)

    let keyboard =
        service.Create (
            "keydown",
            EventConstruction.Keyboard (
                EventOptions.Default,
                { KeyboardFields.Default with
                    Key = "é"
                    Code = "KeyE"
                    Location = 1
                    Repeat = true
                    IsComposing = true
                    Modifiers = EventModifiers.Control
                }
            )
        )
        :?> KeyboardEvent

    equal "keyboard key and code" ("é", "KeyE") (keyboard.key, keyboard.code)
    equal "keyboard flags" (1u, true, true, true) (keyboard.location, keyboard.repeat, keyboard.isComposing, keyboard.ctrlKey)

    raises "negative location" argument (fun () ->
        service.Create (
            "x",
            EventConstruction.Keyboard (
                EventOptions.Default,
                { KeyboardFields.Default with
                    Location = -1
                }
            )
        )
        |> ignore)

    equal "UI detail" 7 (service.Create ("ui", EventConstruction.UI (EventOptions.Default, 7)) :?> UIEvent).detail

    let focus =
        service.Create ("focusin", EventConstruction.Focus (options, 0, child)) :?> FocusEvent

    same "focus relatedTarget" child focus.relatedTarget
    equal "no relatedTarget" true (isNull (service.Create ("m", EventConstruction.Mouse (options, MouseFields.Default)) :?> MouseEvent).relatedTarget)

    // Standalone target dispatch.
    let order = ResizeArray<string>()

    let flag value =
        if value then "yes" else "no"

    let listener name (action: Event -> unit) : Event -> unit =
        fun event ->
            order.Add
                $"{name}:{event.eventPhase}:{flag (obj.ReferenceEquals (event.currentTarget, target))}:{flag (obj.ReferenceEquals (event.target, target))}"

            action event

    // Capture is registered first: at the target, WebKit runs capture listeners first and the measured Chromium runs
    // registration order, so this order is the same on both.
    target.addEventListener ("custom", listener "capture" ignore, true)
    target.addEventListener ("custom", listener "first" (fun event -> event.preventDefault ()))
    target.addEventListener ("custom", listener "stop" (fun event -> event.stopImmediatePropagation ()))
    target.addEventListener ("custom", listener "after" ignore)
    equal "cancelled dispatch returns false" false (target.dispatchEvent custom)

    equal
        "capture first at target, registration order, stopImmediatePropagation"
        [ "capture:2:yes:yes"; "first:2:yes:yes"; "stop:2:yes:yes" ]
        (List.ofSeq order)

    equal "defaultPrevented" true custom.defaultPrevented
    same "target kept after dispatch" target custom.target
    equal "currentTarget cleared after dispatch" true (isNull custom.currentTarget)
    equal "phase after dispatch" 0us custom.eventPhase
    order.Clear ()

    let plainTarget =
        service.Create ("custom", EventConstruction.Event EventOptions.Default)

    equal "uncancelable dispatch returns true" true (target.dispatchEvent plainTarget)
    equal "uncancelable preventDefault ignored" false plainTarget.defaultPrevented

    // Tree dispatch: capture, target and bubble with stopPropagation.
    let path = ResizeArray<string>()

    let record (name: string) : Event -> unit =
        fun event -> path.Add $"{name}:{event.eventPhase}:{flag (obj.ReferenceEquals (event.target, child))}"

    host.addEventListener ("tree", record "host-capture", true)
    host.addEventListener ("tree", record "host-bubble")
    child.addEventListener ("tree", record "child")

    let tree =
        service.Create (
            "tree",
            EventConstruction.Event
                { EventOptions.Default with
                    Bubbles = true
                }
        )

    equal "tree dispatch result" true (child.dispatchEvent tree)
    equal "capture, target, bubble" [ "host-capture:1:yes"; "child:2:yes"; "host-bubble:3:yes" ] (List.ofSeq path)
    path.Clear ()
    child.addEventListener ("stopped", fun event -> event.stopPropagation ())
    host.addEventListener ("stopped", record "host-bubble")

    equal
        "stopped dispatch"
        true
        (child.dispatchEvent (
            service.Create (
                "stopped",
                EventConstruction.Event
                    { EventOptions.Default with
                        Bubbles = true
                    }
            )
        ))

    equal "stopPropagation keeps the event at the target" 0 path.Count
    path.Clear ()

    let nonBubbling =
        service.Create ("tree", EventConstruction.Event EventOptions.Default)

    child.dispatchEvent nonBubbling |> ignore
    equal "non-bubbling skips the bubble phase" [ "host-capture:1:yes"; "child:2:yes" ] (List.ofSeq path)

    // Standalone targets with I01 signals.
    use signals = cancellation ()
    let controller = signals.CreateController ()
    let mutable signalled = 0
    signals.Listen (target, "signalled", (fun _ -> signalled <- signalled + 1), null, controller.Signal)

    target.dispatchEvent (service.Create ("signalled", EventConstruction.Event EventOptions.Default))
    |> ignore

    controller.Abort ()

    target.dispatchEvent (service.Create ("signalled", EventConstruction.Event EventOptions.Default))
    |> ignore

    equal "abort removes a standalone listener" 1 signalled
    let mutable once = 0
    target.addEventListener ("once", (fun _ -> once <- once + 1), AddEventListenerOptions (once = true))

    target.dispatchEvent (service.Create ("once", EventConstruction.Event EventOptions.Default))
    |> ignore

    target.dispatchEvent (service.Create ("once", EventConstruction.Event EventOptions.Default))
    |> ignore

    equal "once on a standalone target" 1 once
    let mutable removed = 0
    let removable: Event -> unit = fun _ -> removed <- removed + 1
    target.addEventListener ("removed", removable)
    target.removeEventListener ("removed", removable)

    target.dispatchEvent (service.Create ("removed", EventConstruction.Event EventOptions.Default))
    |> ignore

    equal "removeEventListener on a standalone target" 0 removed
    let other = service.CreateTarget ()
    equal "targets are distinct" false (obj.ReferenceEquals (other, target))
    host.remove ()
    checks
