module internal NativeApiConformance.AnimationEffectChecks

open Ranvier.Dom

let private Css =
    ".wx{position:absolute;width:40px;height:40px;background:rgb(0,0,255)}#wx-a{left:0;top:0}#wx-b{left:50px;top:0}"

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

/// <summary>
/// Reads, retimes, rekeys and retargets one keyframe effect: getTiming and getComputedTiming before and after
/// updateTiming, getKeyframes before and after setKeyframes, target, pseudoElement and composite settings, invalid
/// timings and keyframes, and a removed and a null target. Values are the browser's. The result is the assertion count.
/// </summary>
let run (document: Document) (styles: unit -> ComputedStyleService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (label + ": expected " + string (box expected) + ", got " + string (box actual))

    let near label (expected: float) (actual: float) =
        checks <- checks + 1

        if abs (expected - actual) > 1e-3 then
            failwith (label + ": expected " + string expected + ", got " + string actual)

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

    let millis (value: DoubleOrCSSNumericValue option) =
        match value with
        | Some (DoubleOrCSSNumericValue.Double milliseconds) -> Some milliseconds
        | _ -> None

    let durationOf (timing: EffectTiming) =
        match timing.duration with
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble milliseconds -> string milliseconds
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.DOMString text -> text
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.CSSNumericValue _ -> "typed"

    let element id =
        let value = document.createElement "div" :?> HTMLElement
        value.id <- id
        value.className <- "wx"
        document.body.appendChild value |> ignore
        value

    let style = document.createElement "style"
    style.textContent <- Css
    document.head.appendChild style |> ignore
    let a = element "wx-a"
    let b = element "wx-b"
    let computed = styles ()

    let value (target: Element) property =
        (computed.Get (target, None)).getPropertyValue property

    try
        let frames = [| frame [ "opacity", "0" ]; frame [ "opacity", "1" ]; frame [ "opacity", "0.5" ] |]
        frames[1].offset <- Some 0.5
        frames[1].easing <- Some "ease-in"
        let options = KeyframeAnimationOptions ()
        options.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 1000.0
        options.delay <- 100.0
        options.endDelay <- 50.0
        options.iterations <- 2.0
        options.direction <- PlaybackDirection.Alternate
        options.fill <- FillMode.Both
        options.iterationStart <- 0.25
        let animation = a.animate (frames, options)
        animation.pause ()
        let effect = animation.effect :?> KeyframeEffect

        let timing = effect.getTiming ()
        equal "delay" 100.0 timing.delay
        equal "endDelay" 50.0 timing.endDelay
        equal "fill" FillMode.Both timing.fill
        equal "iterationStart" 0.25 timing.iterationStart
        equal "iterations" 2.0 timing.iterations
        equal "duration" "1000" (durationOf timing)
        equal "direction" PlaybackDirection.Alternate timing.direction
        equal "easing" "linear" timing.easing

        animation.currentTime <- Some (DoubleOrCSSNumericValue.Double 600.0)
        let at600 = effect.getComputedTiming ()
        equal "endTime" (Some 2150.0) (millis at600.endTime)
        equal "activeDuration" (Some 2000.0) (millis at600.activeDuration)
        equal "localTime" (Some 600.0) (millis at600.localTime)
        near "progress at 600" 0.75 (defaultArg at600.progress nan)
        equal "currentIteration at 600" (Some 0.0) at600.currentIteration

        let keyframes = effect.getKeyframes ()
        equal "keyframe count" 3 keyframes.Length
        equal "keyframe 0 offset" true (Option.isNone keyframes[0].offset)
        equal "keyframe 1 offset" (Some 0.5) keyframes[1].offset
        equal "keyframe 2 computedOffset" 1.0 keyframes[2].computedOffset
        equal "keyframe 1 easing" "ease-in" keyframes[1].easing
        equal "keyframe composite" "auto" keyframes[0].composite

        equal
            "keyframe 2 properties"
            [ "opacity", "0.5" ]
            (keyframes[2].properties |> Array.map (fun p -> p.name, p.value) |> Array.toList)

        let update = OptionalEffectTiming ()
        update.duration <- Some (UnrestrictedDoubleOrDOMString.UnrestrictedDouble 2000.0)
        update.delay <- Some 0.0
        update.iterations <- Some 1.0
        update.direction <- Some PlaybackDirection.Normal
        update.iterationStart <- Some 0.0
        effect.updateTiming update
        let updated = effect.getTiming ()
        equal "updated duration" "2000" (durationOf updated)
        equal "updated delay" 0.0 updated.delay
        equal "updated direction" PlaybackDirection.Normal updated.direction
        equal "kept endDelay" 50.0 updated.endDelay
        animation.currentTime <- Some (DoubleOrCSSNumericValue.Double 500.0)
        near "progress after update" 0.25 (defaultArg (effect.getComputedTiming ()).progress nan)
        equal "opacity after update" "0.5" (value a "opacity")

        let auto = OptionalEffectTiming ()
        auto.duration <- Some (UnrestrictedDoubleOrDOMString.DOMString "auto")
        effect.updateTiming auto
        equal "auto duration" "auto" (durationOf (effect.getTiming ()))
        let second = OptionalEffectTiming ()
        second.duration <- Some (UnrestrictedDoubleOrDOMString.UnrestrictedDouble 2000.0)
        second.easing <- Some "ease-out"
        second.fill <- Some FillMode.Forwards
        second.endDelay <- Some 0.0
        effect.updateTiming second
        equal "eased fill" FillMode.Forwards (effect.getTiming ()).fill
        equal "eased easing" "ease-out" (effect.getTiming ()).easing

        let slide = [| frame [ "transform", "translateX(0px)" ]; frame [ "transform", "translateX(100px)" ] |]
        slide[1].composite <- Some "add"
        effect.setKeyframes slide
        let reset = effect.getKeyframes ()
        equal "set keyframe composite" "add" reset[1].composite
        equal "set keyframe property" "transform" reset[1].properties[0].name
        equal "opacity after set" "1" (value a "opacity")

        effect.target <- b
        equal "retarget" "wx-b" ((effect.target :?> HTMLElement).id)
        equal "old target transform" "none" (value a "transform")
        equal "new target animated" true ((value b "transform").StartsWith "matrix(1, 0, 0, 1, 37.8")
        effect.pseudoElement <- "::before"
        equal "pseudoElement" "::before" effect.pseudoElement
        effect.pseudoElement <- null
        effect.composite <- CompositeOperation.Add
        effect.iterationComposite <- IterationCompositeOperation.Accumulate
        equal "composite" CompositeOperation.Add effect.composite
        equal "iterationComposite" IterationCompositeOperation.Accumulate effect.iterationComposite
        effect.composite <- CompositeOperation.Replace
        effect.iterationComposite <- IterationCompositeOperation.Replace

        let invalid (configure: OptionalEffectTiming -> unit) =
            let timing = OptionalEffectTiming ()
            configure timing
            fun () -> effect.updateTiming timing

        throws "negative duration" "TypeError" (invalid (fun t -> t.duration <- Some (UnrestrictedDoubleOrDOMString.UnrestrictedDouble -1.0)))
        throws "bad duration string" "TypeError" (invalid (fun t -> t.duration <- Some (UnrestrictedDoubleOrDOMString.DOMString "bogus")))
        throws "bad easing" "TypeError" (invalid (fun t -> t.easing <- Some "bogus"))
        throws "negative iterations" "TypeError" (invalid (fun t -> t.iterations <- Some -1.0))
        throws "NaN iterations" "TypeError" (invalid (fun t -> t.iterations <- Some nan))
        let unsorted = [| frame [ "opacity", "1" ]; frame [ "opacity", "0" ] |]
        unsorted[0].offset <- Some 1.0
        unsorted[1].offset <- Some 0.0
        throws "unsorted keyframes" "TypeError" (fun () -> effect.setKeyframes unsorted)
        throws "bogus pseudoElement" "SyntaxError" (fun () -> effect.pseudoElement <- "bogus")
        effect.pseudoElement <- null
        equal "timing kept after invalid" "ease-out" (effect.getTiming ()).easing
        equal "keyframes kept after invalid" 2 (effect.getKeyframes ()).Length

        b.remove ()
        equal "removed target kept" "wx-b" ((effect.target :?> HTMLElement).id)
        near "removed target progress" 0.378138 (defaultArg (effect.getComputedTiming ()).progress nan)
        effect.target <- null
        equal "null target" true (isNull effect.target)
        equal "null target keyframes" 2 (effect.getKeyframes ()).Length
        animation.cancel ()
        let cancelled = effect.getComputedTiming ()
        equal "cancelled localTime" true (Option.isNone (millis cancelled.localTime))
        equal "cancelled progress" true (Option.isNone cancelled.progress)
        equal "cancelled currentIteration" true (Option.isNone cancelled.currentIteration)
    finally
        for node in [ a :> Node; style ] do
            node.parentNode.removeChild node |> ignore

    checks
