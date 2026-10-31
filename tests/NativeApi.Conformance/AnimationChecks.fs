module internal NativeApiConformance.AnimationChecks

open Ranvier.Dom

let private Css =
    ".wa{position:absolute;width:40px;height:40px;background:rgb(0,0,255)}"
    + "#wa-a{left:0;top:0}#wa-b{left:50px;top:0}#wa-c{left:100px;top:0;transform:translateX(10px)}#wa-e{left:50px;top:50px;opacity:0.5}"

let private frame (properties: (string * string) list) =
    let value = Keyframe ()

    value.properties <-
        properties
        |> List.map (fun (name, text) ->
            let property = KeyframeProperty ()
            property.name <- name
            property.value <- text
            property)
        |> List.toArray

    value

let private fade () = [| frame [ "opacity", "0" ]; frame [ "opacity", "1" ] |]

let private slide (distance: int) =
    [| frame [ "transform", "translateX(0px)" ]; frame [ "transform", "translateX(" + string distance + "px)" ] |]

/// <summary>
/// Creates keyframe animations through Element.animate, the KeyframeEffect and Animation constructors and a new
/// DocumentTimeline, pauses and seeks each to its start, midpoint and end, and compares play state, current time and
/// computed opacity or transform with the browser's values, including delay, direction and composite options; also
/// keyframe offsets, easing and composite, the copy constructor, an animation without a timeline and the engine's
/// TypeError and SyntaxError cases. The result is the assertion count.
/// </summary>
let run (document: Document) (create: unit -> AnimationService) (styles: unit -> ComputedStyleService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (label + ": expected " + string (box expected) + ", got " + string (box actual))

    let throws label expected (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                "none"
            with error ->
                errorName error

        if name <> expected then
            failwith (label + ": expected " + expected + ", got " + name)

    let element id =
        let value = document.createElement "div" :?> HTMLElement
        value.id <- id
        value.className <- "wa"
        document.body.appendChild value |> ignore
        value

    let style = document.createElement "style"
    style.textContent <- Css
    document.head.appendChild style |> ignore
    let a = element "wa-a"
    let b = element "wa-b"
    let c = element "wa-c"
    let e = element "wa-e"
    let service = create ()
    let computed = styles ()

    let value (target: Element) property =
        (computed.Get (target, None)).getPropertyValue property

    let time (animation: WebAnimation) =
        match animation.currentTime with
        | Some (DoubleOrCSSNumericValue.Double milliseconds) -> Some milliseconds
        | _ -> None

    let seek (animation: WebAnimation) (milliseconds: float) =
        animation.currentTime <- Some (DoubleOrCSSNumericValue.Double milliseconds)

    // Seeks a paused animation to each time and compares the property's computed value.
    let points label (animation: WebAnimation) (target: Element) property (expected: (float * string) list) =
        animation.pause ()

        for milliseconds, wanted in expected do
            seek animation milliseconds
            equal (label + " currentTime " + string milliseconds) (Some milliseconds) (time animation)
            equal (label + " paused at " + string milliseconds) AnimationPlayState.Paused animation.playState
            equal (label + " " + property + " at " + string milliseconds) wanted (value target property)

        animation.cancel ()
        equal (label + " cancelled") AnimationPlayState.Idle animation.playState

    let options (configure: KeyframeAnimationOptions -> unit) =
        let value = KeyframeAnimationOptions ()
        value.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 1000.0
        configure value
        value

    let animate (target: Element) frames configure = target.animate (frames, options configure)

    try
        points "opacity fill both" (animate a (fade ()) (fun o -> o.fill <- FillMode.Both)) a "opacity" [ 0.0, "0"; 500.0, "0.5"; 1000.0, "1" ]
        points "transform" (animate b (slide 100) (fun o -> o.fill <- FillMode.Both)) b "transform"
            [ 0.0, "matrix(1, 0, 0, 1, 0, 0)"; 500.0, "matrix(1, 0, 0, 1, 50, 0)"; 1000.0, "matrix(1, 0, 0, 1, 100, 0)" ]
        points "delay" (animate a (fade ()) (fun o -> o.delay <- 500.0)) a "opacity" [ 250.0, "1"; 1000.0, "0.5"; 1500.0, "1" ]
        points "reverse" (animate a (fade ()) (fun o -> o.direction <- PlaybackDirection.Reverse; o.fill <- FillMode.Both)) a "opacity"
            [ 0.0, "1"; 250.0, "0.75"; 1000.0, "0" ]
        points "alternate" (animate a (fade ()) (fun o -> o.iterations <- 2.0; o.direction <- PlaybackDirection.Alternate; o.fill <- FillMode.Both))
            a "opacity" [ 250.0, "0.25"; 1250.0, "0.75" ]
        points "composite add" (animate c (slide 100) (fun o -> o.composite <- CompositeOperation.Add; o.fill <- FillMode.Both)) c "transform"
            [ 500.0, "matrix(1, 0, 0, 1, 60, 0)" ]
        let keyframeAdd = [| frame [ "opacity", "0" ]; frame [ "opacity", "0.25" ] |]

        for item in keyframeAdd do
            item.composite <- Some "add"

        points "keyframe composite add" (animate e keyframeAdd (fun o -> o.fill <- FillMode.Both)) e "opacity" [ 500.0, "0.625" ]
        let offsets = [| frame [ "opacity", "0" ]; frame [ "opacity", "0.8" ]; frame [ "opacity", "1" ] |]
        offsets[1].offset <- Some 0.8
        points "offsets" (animate a offsets (fun o -> o.fill <- FillMode.Both)) a "opacity" [ 400.0, "0.4"; 900.0, "0.9" ]
        let stepped = fade ()
        stepped[0].easing <- Some "step-end"
        points "keyframe easing" (animate a stepped (fun o -> o.fill <- FillMode.Both)) a "opacity" [ 500.0, "0"; 1000.0, "1" ]
        points "number options" (a.animate (fade (), 2000.0)) a "opacity" [ 500.0, "0.25" ]
        let identified = animate a (fade ()) (fun o -> o.id <- "wa-id"; o.fill <- FillMode.Both; o.timeline <- Some (document.timeline :> AnimationTimeline))
        equal "animate id" "wa-id" identified.id
        equal "animate on document.timeline" true (obj.ReferenceEquals (identified.timeline, document.timeline))
        points "timeline option" identified a "opacity" [ 500.0, "0.5" ]

        // Constructors.
        let effectOptions = KeyframeEffectOptions ()
        effectOptions.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 1000.0
        effectOptions.fill <- FillMode.Forwards
        let effect = service.CreateKeyframeEffect (b, slide 40, effectOptions)
        equal "effect target" "wa-b" ((effect.target :?> HTMLElement).id)
        equal "effect composite" CompositeOperation.Replace effect.composite
        let constructed = service.CreateAnimation (effect, document.timeline)
        equal "constructed idle" AnimationPlayState.Idle constructed.playState
        points "KeyframeEffect and Animation" constructed b "transform" [ 500.0, "matrix(1, 0, 0, 1, 20, 0)"; 1000.0, "matrix(1, 0, 0, 1, 40, 0)" ]
        let source = service.CreateKeyframeEffectFor (b, fade (), 2000.0)
        let copy = service.CopyKeyframeEffect source
        equal "copy target" "wa-b" ((copy.target :?> HTMLElement).id)
        points "copy" (service.CreateAnimation copy) b "opacity" [ 500.0, "0.25" ]
        let empty = service.CreateAnimation ()
        equal "Animation without effect" AnimationPlayState.Idle empty.playState
        equal "Animation without effect has no effect" true (isNull empty.effect)
        equal "Animation without effect has no current time" None (time empty)
        let detached = service.CreateAnimationWithoutTimeline (service.CreateKeyframeEffectFor (a, fade (), 1000.0))
        equal "Animation without timeline" true (isNull detached.timeline)
        let timeline = service.CreateDocumentTimeline ()
        equal "new DocumentTimeline has a current time" true (timeline.currentTime.IsSome)
        let own = service.CreateAnimation (service.CreateKeyframeEffectFor (a, fade (), 1000.0), timeline)
        points "new DocumentTimeline" own a "opacity" [ 250.0, "0.25" ]

        // Engine errors.
        let unsorted = fade ()
        unsorted[0].offset <- Some 0.8
        unsorted[1].offset <- Some 0.2
        throws "unsorted offsets" "TypeError" (fun () -> a.animate (unsorted, 1000.0) |> ignore)
        let badEasing = fade ()
        badEasing[0].easing <- Some "bogus"
        throws "invalid keyframe easing" "TypeError" (fun () -> a.animate (badEasing, 1000.0) |> ignore)
        let badComposite = fade ()
        badComposite[0].composite <- Some "bogus"
        throws "invalid keyframe composite" "TypeError" (fun () -> a.animate (badComposite, 1000.0) |> ignore)
        throws "negative duration" "TypeError" (fun () -> a.animate (fade (), -1.0) |> ignore)
        let pseudo = KeyframeEffectOptions ()
        pseudo.pseudoElement <- "bogus"
        throws "invalid pseudo-element" "SyntaxError" (fun () -> service.CreateKeyframeEffect (a, [||], pseudo) |> ignore)
        equal "empty keyframes" AnimationPlayState.Running ((a.animate ([||], 1000.0)).playState)

        for animation in document.getAnimations () do
            animation.cancel ()

        service.Dispose ()
        // Services raise ObjectDisposedException; Fable erases the type.
        checks <- checks + 1

        let rejected =
            try
                service.CreateAnimation () |> ignore
                false
            with _ ->
                true

        if not rejected then
            failwith "disposed service: expected an exception"
    finally
        for node in [ a :> Node; b; c; e; style ] do
            node.parentNode.removeChild node |> ignore

    checks
