module internal NativeApiConformance.ScrollChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>How a backend snaps a fractional CSS scroll target to its whole-pixel offset.</summary>
type Snapping =
    /// <summary>Toward zero: the native engine (WebKit <c>IntPoint</c> conversion).</summary>
    | Truncate
    /// <summary>To the nearest pixel, halves up: Chromium.</summary>
    | RoundHalfUp

/// <summary>Host changes the fixture drives.</summary>
type Driver =
    {
        /// <summary>Sets the device scale at a 320x240 CSS viewport, then lets rendering updates run.</summary>
        SetScale: float -> Async<unit>
        /// <summary>Lets rendering updates and queued tasks run.</summary>
        Settle: unit -> Async<unit>
        Snapping: Snapping
    }

type private Operation =
    | To
    | By

type private Step =
    {
        Name: string
        Operation: Operation
        Left: float option
        Top: float option
        Behavior: ScrollBehavior
        /// <summary>True for the (x, y) form of an element's scrollTo/scrollBy.</summary>
        Pair: bool
    }

let private step name operation left top behavior pair =
    {
        Name = name
        Operation = operation
        Left = left
        Top = top
        Behavior = behavior
        Pair = pair
    }

// The oracle steps of Ranvier.WebCore tests/fixtures/viewport-scroll-oracle.json that the managed contract expresses.
let private steps =
    [
        step "scrollTo(10.25, 20.5)" To (Some 10.25) (Some 20.5) ScrollBehavior.Auto true
        step "scrollTo(10.75, 20.75)" To (Some 10.75) (Some 20.75) ScrollBehavior.Auto true
        step "scrollTo(10.4, 20.4)" To (Some 10.4) (Some 20.4) ScrollBehavior.Auto true
        step "scrollTo({left: 3.3, top: 7.7, behavior: instant})" To (Some 3.3) (Some 7.7) ScrollBehavior.Instant false
        step "scrollTo({top: 9.6}) keeps left" To None (Some 9.6) ScrollBehavior.Auto false
        step "scrollTo(5.5, 6.5)" To (Some 5.5) (Some 6.5) ScrollBehavior.Auto true
        step "scrollBy(0.4, 0.4)" By (Some 0.4) (Some 0.4) ScrollBehavior.Auto true
        step "scrollBy(0.4, 0.4) again" By (Some 0.4) (Some 0.4) ScrollBehavior.Auto true
        step "scrollBy({top: 0.6})" By None (Some 0.6) ScrollBehavior.Auto false
        step "scrollBy(-0.3, -0.3)" By (Some -0.3) (Some -0.3) ScrollBehavior.Auto true
        step "scrollTo(-5.5, -5.5) clamps to 0" To (Some -5.5) (Some -5.5) ScrollBehavior.Auto true
        step "scrollTo(1e6, 1e6) clamps to the maximum" To (Some 1e6) (Some 1e6) ScrollBehavior.Auto true
        step "scrollBy(-0.5, -0.5) from the maximum" By (Some -0.5) (Some -0.5) ScrollBehavior.Auto true
        step "scrollTo({left: 0.5, top: 50.5, behavior: smooth})" To (Some 0.5) (Some 50.5) ScrollBehavior.Smooth false
        step "scrollTo(0, 0)" To (Some 0.0) (Some 0.0) ScrollBehavior.Auto true
    ]

let private scales = [ 1.0; 1.5; 2.0 ]

/// <summary>
/// Checks fractional scrolling of the viewport (through the scroll service) and of a scrolling box (through
/// <c>Element.scroll*</c>) at device scales 1, 1.5 and 2: each step lands on the backend's snapped and clamped
/// position, a move dispatches scroll then scrollend, and no move dispatches nothing.
/// </summary>
let run (document: Document) (create: unit -> ViewportScrollService) (viewport: unit -> ViewportService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        document.documentElement.setAttribute ("style", "margin:0")
        document.body.setAttribute ("style", "margin:0;width:2000.25px;height:2000.5px")
        let box = document.createElement "div"
        box.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:100px;overflow:auto")
        let content = document.createElement "div"
        content.setAttribute ("style", "width:1000.25px;height:1000.5px")
        box.appendChild content |> ignore
        document.body.appendChild box |> ignore
        let scroller = create ()
        let changes = viewport ()
        let events = List<string>()

        let subscription =
            changes.Subscribe (fun change ->
                match change with
                | ViewportChange.Scrolled -> events.Add "viewport:scroll"
                | ViewportChange.ScrollEnded -> events.Add "viewport:scrollend"
                | _ -> ())

        let boxScroll = fun (_: Event) -> events.Add "box:scroll"
        let boxScrollEnd = fun (_: Event) -> events.Add "box:scrollend"
        box.addEventListener ("scroll", boxScroll)
        box.addEventListener ("scrollend", boxScrollEnd)

        let snap (value: float) =
            match driver.Snapping with
            | Truncate -> Math.Truncate value
            | RoundHalfUp -> Math.Floor (value + 0.5)

        let expected (current: float * float) (maximum: int * int) (step: Step) =
            let target current (value: float option) =
                match step.Operation, value with
                | To, Some value -> value
                | By, Some value -> current + value
                | _, None -> current

            let clamp (value: float) (limit: int) =
                Math.Min (Math.Max (snap value, 0.0), float limit)

            clamp (target (fst current) step.Left) (fst maximum), clamp (target (snd current) step.Top) (snd maximum)

        for scale in scales do
            do! driver.SetScale scale

            for kind in [ "viewport"; "box" ] do
                scroller.ScrollTo (ScrollRequest.point 0.0 0.0)
                box.scrollTo (0.0, 0.0)
                do! driver.Settle ()
                events.Clear ()
                let label = $"{kind}@{scale}"

                let element =
                    if kind = "viewport" then
                        document.scrollingElement
                    else
                        box

                let maximum =
                    element.scrollWidth - element.clientWidth, element.scrollHeight - element.clientHeight

                equal (label + " maximum") (if kind = "viewport" then (1680, 1761) else (900, 901)) maximum

                let read () =
                    if kind = "viewport" then
                        let position = scroller.Position ()
                        equal (label + " scrollingElement agrees") (position.X, position.Y) (float document.scrollingElement.scrollLeft, float document.scrollingElement.scrollTop)
                        position.X, position.Y
                    else
                        float box.scrollLeft, float box.scrollTop

                for step in steps do
                    let before = read ()
                    let target = expected before maximum step

                    if kind = "viewport" then
                        let request =
                            {
                                Left = step.Left
                                Top = step.Top
                                Behavior = step.Behavior
                            }

                        match step.Operation with
                        | To -> scroller.ScrollTo request
                        | By -> scroller.ScrollBy request
                    elif step.Pair then
                        match step.Operation with
                        | To -> box.scrollTo (step.Left.Value, step.Top.Value)
                        | By -> box.scrollBy (step.Left.Value, step.Top.Value)
                    else
                        let options = ScrollToOptions ()
                        options.left <- step.Left
                        options.top <- step.Top
                        options.behavior <- step.Behavior

                        match step.Operation with
                        | To -> box.scrollTo options
                        | By -> box.scrollBy options

                    let source = if kind = "viewport" then "viewport" else "box"
                    let mutable settles = 0

                    let finished () =
                        step.Behavior <> ScrollBehavior.Smooth || events.Contains (source + ":scrollend")

                    do! driver.Settle ()

                    while not (finished ()) && settles < 100 do
                        settles <- settles + 1
                        do! driver.Settle ()

                    let after = read ()
                    equal $"{label} {step.Name} position" target after

                    if step.Behavior = ScrollBehavior.Smooth then
                        let scrolls = events |> Seq.filter (fun item -> item = source + ":scroll") |> Seq.length

                        equal
                            $"{label} {step.Name} scroll events then one scrollend"
                            true
                            (scrolls >= 1
                             && events.Count = scrolls + 1
                             && events[events.Count - 1] = source + ":scrollend")
                    else
                        let moved = after <> before

                        equal
                            $"{label} {step.Name} events"
                            (if moved then [ source + ":scroll"; source + ":scrollend" ] else [])
                            (List.ofSeq events)

                    events.Clear ()

        box.removeEventListener ("scroll", boxScroll)
        box.removeEventListener ("scrollend", boxScrollEnd)
        subscription.Dispose ()
        changes.Dispose ()
        box.scrollTo (5.0, 5.0)
        scroller.ScrollTo (ScrollRequest.point 0.0 3.0)
        do! driver.Settle ()
        equal "no delivery after removal" 0 events.Count
        equal "point request" (0.0, 3.0) ((scroller.Position ()).X, (scroller.Position ()).Y)
        scroller.ScrollTo (ScrollRequest.point 0.0 0.0)
        do! driver.SetScale 1.0
        box.remove ()
        document.body.removeAttribute "style"
        document.documentElement.removeAttribute "style"
        return checks
    }
