module internal NativeApiConformance.PointerInputChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>A touch contact of a frame: identifier, action (0 move, 1 down, 2 up, 3 cancel, 4 stationary), position,
/// radii, rotation in degrees and force.</summary>
type Contact =
    {
        Id: int
        Action: int
        X: float
        Y: float
        RadiusX: float
        RadiusY: float
        Angle: float
        Force: float
    }

/// <summary>A pen sample: action, position, buttons, pressure, barrel pressure, tilt, twist and movement.</summary>
type Pen =
    {
        Action: int
        X: float
        Y: float
        Buttons: int
        Pressure: float
        Tangential: float
        TiltX: int
        TiltY: int
        Twist: int
        MovementX: float
        MovementY: float
    }

/// <summary>Input delivered as the user's.</summary>
type Driver =
    {
        /// <summary>One touch frame: every active contact, unchanged ones stationary.</summary>
        Touch: Contact array -> Async<unit>
        Pen: Pen -> Async<unit>
        /// <summary>A left click at x, y.</summary>
        Click: int -> int -> Async<unit>
        /// <summary>A mouse move by movementX, movementY to x, y.</summary>
        MoveBy: int -> int -> int -> int -> Async<unit>
        /// <summary>The embedder grants the pending pointer-lock request.</summary>
        GrantLock: unit -> Async<unit>
        /// <summary>Runs queued tasks, such as pointerlockchange.</summary>
        Settle: unit -> Async<unit>
    }

/// <summary>Checks touch frames, pen fidelity, pen capture, pointer lock and the pointer-input capabilities.</summary>
let run (document: Document) (provider: PointerInputProvider) (capabilities: CapabilityProvider) (touchState: CapabilityState) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith (
                    label
                    + ": expected "
                    + string (box expected)
                    + ", got "
                    + string (box actual)
                )

        let near label (expected: float) (actual: float) =
            checks <- checks + 1

            if Math.Abs (expected - actual) > 1e-5 then
                failwith (
                    label
                    + ": expected "
                    + string expected
                    + ", got "
                    + string actual
                )

        let place (id: string) (top: int) (height: int) =
            let element = document.createElement "div"
            element.setAttribute ("id", id)

            element.setAttribute (
                "style",
                "position:absolute;left:20px;top:"
                + string top
                + "px;width:200px;height:"
                + string height
                + "px;touch-action:none"
            )

            document.body.appendChild element |> ignore
            element

        document.body.setAttribute ("style", "margin:0")
        let t = place "pointer-t" 20 100
        let u = place "pointer-u" 150 60

        // Every target of the fixture's events is an element; Fable has no type test for DOM interfaces.
        let idOf (target: EventTarget) =
            if isNull (box target) then
                "none"
            else
                (unbox<Element> target).getAttribute "id"

        let events = List<string * Event>()

        let listen (name: string) =
            let listener = fun (event: Event) -> events.Add ((name, event))
            document.addEventListener (name, listener, false)
            name, listener

        let listeners =
            [
                "touchstart"
                "touchmove"
                "touchend"
                "touchcancel"
                "pointerdown"
                "pointermove"
                "pointerup"
                "pointercancel"
                "gotpointercapture"
                "lostpointercapture"
            ]
            |> List.map listen

        let take () =
            let items = events.ToArray ()
            events.Clear ()
            items

        let touchesOf (items: (string * Event) array) (name: string) =
            items
            |> Array.filter (fun (kind, _) -> kind = name)
            |> Array.map (fun (_, event) -> event :?> TouchEvent)

        let pointersOf (items: (string * Event) array) (name: string) =
            items
            |> Array.filter (fun (kind, _) -> kind = name)
            |> Array.map (fun (_, event) -> event :?> PointerEvent)

        let ids (list: TouchList) =
            PointerInput.contacts provider list
            |> Array.map (fun contact -> contact.Identifier)

        let contact id action x y rx ry angle force =
            {
                Id = id
                Action = action
                X = x
                Y = y
                RadiusX = rx
                RadiusY = ry
                Angle = angle
                Force = force
            }

        // Touch: two contacts on #t, then contact 1 moves, lifts, and the platform cancels contact 0.
        do! driver.Touch [| contact 0 1 50.0 50.0 4.0 6.0 30.0 0.4 |]
        let frame = take ()

        match touchesOf frame "touchstart" with
        | [| start |] ->
            equal "touchstart trusted" true start.isTrusted
            equal "touchstart target" "pointer-t" (idOf start.target)
            equal "start 0 touches" [| 0 |] (ids start.touches)
            equal "start 0 changed" [| 0 |] (ids start.changedTouches)
            equal "start 0 target touches" [| 0 |] (ids start.targetTouches)
            let first = (PointerInput.contacts provider start.changedTouches)[0]
            equal "start 0 position" (50.0, 50.0, 50.0, 50.0) (first.ClientX, first.ClientY, first.PageX, first.PageY)
            equal "start 0 radii" (4.0, 6.0) (first.Geometry.RadiusX, first.Geometry.RadiusY)
            near "start 0 rotation" 30.0 first.Geometry.RotationAngle
            near "start 0 force" 0.4 first.Geometry.Force
            equal "start 0 contact target" "pointer-t" (idOf first.Target)
        | other -> failwith ("touchstart count " + string other.Length)

        let firstPointer =
            match pointersOf frame "pointerdown" with
            | [| down |] ->
                equal "touch pointerdown type" "touch" down.pointerType
                equal "touch pointerdown primary" true down.isPrimary
                equal "touch pointerdown size" (8.0, 12.0) (down.width, down.height)
                near "touch pointerdown pressure" 0.4 (float down.pressure)
                equal "touch pointerdown target" "pointer-t" (idOf down.target)
                down.pointerId
            | other -> failwith ("pointerdown count " + string other.Length)

        do!
            driver.Touch
                [|
                    contact 0 4 50.0 50.0 4.0 6.0 30.0 0.4
                    contact 1 1 100.0 60.0 2.0 2.0 0.0 0.8
                |]

        let frame = take ()

        match touchesOf frame "touchstart" with
        | [| start |] ->
            equal "start 1 touches" [| 0; 1 |] (ids start.touches)
            equal "start 1 changed" [| 1 |] (ids start.changedTouches)
        | other -> failwith ("second touchstart count " + string other.Length)

        let secondPointer =
            match pointersOf frame "pointerdown" with
            | [| down |] ->
                equal "second contact not primary" false down.isPrimary
                equal "second contact distinct pointer" true (down.pointerId <> firstPointer)
                down.pointerId
            | other -> failwith ("second pointerdown count " + string other.Length)

        do!
            driver.Touch
                [|
                    contact 0 4 50.0 50.0 4.0 6.0 30.0 0.4
                    contact 1 0 120.0 70.0 2.0 2.0 0.0 0.9
                |]

        let frame = take ()

        match touchesOf frame "touchmove" with
        | [| move |] ->
            equal "move 1 changed" [| 1 |] (ids move.changedTouches)
            equal "move 1 touches" [| 0; 1 |] (ids move.touches)
            let moved = (PointerInput.contacts provider move.changedTouches)[0]
            equal "move 1 position" (120.0, 70.0) (moved.ClientX, moved.ClientY)
            near "move 1 force" 0.9 moved.Geometry.Force
        | other -> failwith ("touchmove count " + string other.Length)

        let moves =
            pointersOf frame "pointermove"
            |> Array.filter (fun event -> event.pointerId = secondPointer)

        equal
            "contact 1 pointermove"
            [| (120.0, 70.0) |]
            (moves
             |> Array.map (fun event -> event.clientX, event.clientY))

        do!
            driver.Touch
                [|
                    contact 0 4 50.0 50.0 4.0 6.0 30.0 0.4
                    contact 1 2 120.0 70.0 2.0 2.0 0.0 0.9
                |]

        let frame = take ()

        match touchesOf frame "touchend" with
        | [| ending |] ->
            equal "end 1 changed" [| 1 |] (ids ending.changedTouches)
            equal "end 1 touches" [| 0 |] (ids ending.touches)
        | other -> failwith ("touchend count " + string other.Length)

        equal
            "contact 1 pointerup"
            [| secondPointer |]
            (pointersOf frame "pointerup"
             |> Array.map (fun event -> event.pointerId))

        do! driver.Touch [| contact 0 3 50.0 50.0 4.0 6.0 30.0 0.4 |]
        let frame = take ()

        match touchesOf frame "touchcancel" with
        | [| cancel |] ->
            equal "cancel 0 changed" [| 0 |] (ids cancel.changedTouches)
            equal "cancel 0 touches" 0u cancel.touches.length
        | other -> failwith ("touchcancel count " + string other.Length)

        equal
            "contact 0 pointercancel"
            [| firstPointer |]
            (pointersOf frame "pointercancel"
             |> Array.map (fun event -> event.pointerId))

        // Pen: hover, a tilted press with pressure, a drag, release.
        let pen action x y buttons pressure =
            {
                Action = action
                X = x
                Y = y
                Buttons = buttons
                Pressure = pressure
                Tangential = 0.0
                TiltX = 0
                TiltY = 0
                Twist = 0
                MovementX = 0.0
                MovementY = 0.0
            }

        do! driver.Pen (pen 0 60.0 40.0 0 0.0)

        let penPointer =
            match pointersOf (take ()) "pointermove" with
            | [| hover |] ->
                equal "pen hover type" "pen" hover.pointerType
                equal "pen hover pressure" 0.0f hover.pressure
                hover.pointerId
            | other -> failwith ("pen hover count " + string other.Length)

        do!
            driver.Pen
                { pen 1 60.0 40.0 1 0.6 with
                    Tangential = 0.2
                    TiltX = 30
                    TiltY = -20
                    Twist = 45
                }

        match pointersOf (take ()) "pointerdown" with
        | [| down |] ->
            equal "pen press pointer" penPointer down.pointerId
            near "pen press pressure" 0.6 (float down.pressure)
            near "pen press tangential" 0.2 (float down.tangentialPressure)
            equal "pen press tilt and twist" (30, -20, 45) (down.tiltX, down.tiltY, down.twist)
            near "pen press altitude" 0.9719114296335162 down.altitudeAngle
            near "pen press azimuth" 5.720701576706406 down.azimuthAngle
            equal "pen press buttons" 1us down.buttons
            equal "pen press target" "pointer-t" (idOf down.target)
        | other -> failwith ("pen press count " + string other.Length)

        // Pen capture: #t keeps the pen while it moves over #u.
        t.setPointerCapture penPointer

        do!
            driver.Pen
                { pen 0 70.0 170.0 1 0.7 with
                    TiltX = 10
                    Twist = 90
                    MovementX = 10.0
                    MovementY = 130.0
                }

        let frame = take ()

        match pointersOf frame "gotpointercapture" with
        | [| got |] -> equal "pen capture gained at #t" ("pointer-t", penPointer) (idOf got.target, got.pointerId)
        | other ->
            failwith (
                "pen gotpointercapture count "
                + string other.Length
            )

        match pointersOf frame "pointermove" with
        | [| drag |] ->
            equal "captured pen move target" "pointer-t" (idOf drag.target)
            equal "pen drag tilt and twist" (10, 0, 90) (drag.tiltX, drag.tiltY, drag.twist)
            equal "pen drag movement" (10.0, 130.0) (drag.movementX, drag.movementY)
        | other -> failwith ("pen drag count " + string other.Length)

        do! driver.Pen (pen 2 70.0 170.0 0 0.0)
        let frame = take ()

        equal
            "pen release pressure"
            [| 0.0f |]
            (pointersOf frame "pointerup"
             |> Array.map (fun event -> event.pressure))

        equal
            "pen capture lost"
            [| penPointer |]
            (pointersOf frame "lostpointercapture"
             |> Array.map (fun event -> event.pointerId))

        // Pointer lock: a click on #t requests the lock, the embedder grants it, then relative moves reach #t.
        let service = PointerInput.lock provider document
        let changes = List<PointerLockChange>()
        let subscription = service.Subscribe changes.Add
        let requests = List<Result<unit, PointerLockRefusal>>()
        let onDown = fun (_: Event) -> requests.Add (service.Request (t, false))
        t.addEventListener ("mousedown", onDown, false)
        do! driver.Click 60 60
        t.removeEventListener ("mousedown", onDown, false)
        equal "gesture request accepted" [| Ok () |] (requests.ToArray ())
        do! driver.GrantLock ()
        do! driver.Settle ()

        match service.Current () with
        | PointerLockState.Locked element -> equal "locked on #t" "pointer-t" (element.getAttribute "id")
        | _ -> failwith "pointer not locked"

        // Chromium may repeat pointerlockchange for one grant; every change names the lock target.
        let locks =
            changes.ToArray ()
            |> Array.map (fun change ->
                match change with
                | PointerLockChange.Locked element -> element.getAttribute "id"
                | PointerLockChange.Unlocked -> "unlocked"
                | PointerLockChange.Error -> "error")
            |> Array.distinct

        equal "lock changes" [| "pointer-t" |] locks
        changes.Clear ()
        equal "pointerLockElement" "pointer-t" (idOf document.pointerLockElement)
        take () |> ignore
        do! driver.MoveBy 100 180 30 20

        match pointersOf (take ()) "pointermove" with
        | [| move |] ->
            equal "locked move target" "pointer-t" (idOf move.target)
            equal "locked move holds the position" (60.0, 60.0) (move.clientX, move.clientY)
            equal "locked move movement" (30.0, 20.0) (move.movementX, move.movementY)
        | other -> failwith ("locked moves " + string other.Length)

        equal "lock is not pointer capture" false (t.hasPointerCapture 1)
        service.Exit ()
        do! driver.Settle ()
        equal "unlock change" [| PointerLockChange.Unlocked |] (changes.ToArray ())
        equal "unlocked" PointerLockState.Unlocked (service.Current ())
        subscription.Dispose ()
        service.Dispose ()

        // Capabilities.
        equal "known keys" 3 capabilities.KnownKeys.Length

        let observe name =
            async {
                let! value =
                    capabilities.Observe (Capabilities.key CapabilityCategory.PlatformFacility name) Threading.CancellationToken.None
                    |> Async.AwaitTask

                return Option.get value
            }

        let! touch = observe "input.touch-events"
        equal "touch events available" touchState touch.Available.State
        let! penGeometry = observe "input.pen-geometry"
        equal "pen geometry available" CapabilityState.Yes penGeometry.Available.State
        let! pointerLock = observe "input.pointer-lock"
        equal "pointer lock implemented" CapabilityState.Yes pointerLock.Implemented.State
        equal "pointer lock permission per request" CapabilityState.Unknown pointerLock.Permitted.State

        for name, listener in listeners do
            document.removeEventListener (name, listener, false)

        t.remove ()
        u.remove ()
        return checks
    }
