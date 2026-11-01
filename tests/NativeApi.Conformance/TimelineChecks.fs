module internal NativeApiConformance.TimelineChecks

open Ranvier.Dom

/// Times are doubles: an offset holds to within a microsecond.
let private Close = 1e-6

/// <summary>
/// A06: compares document timelines with the browser's: the time origin of a new DocumentTimeline against
/// document.timeline, the null duration, a timeline that is not document.timeline, the inactive timeline of a document
/// without a browsing context and an animation played on it, and an animation moved to a new timeline. The result is the
/// assertion count.
/// </summary>
let runDocumentTimelines (document: Document) (create: unit -> AnimationService) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (
                label
                + ": expected "
                + string (box expected)
                + ", got "
                + string (box actual)
            )

    let time (value: DoubleOrCSSNumericValue option) =
        match value with
        | Some (DoubleOrCSSNumericValue.Double milliseconds) -> Some milliseconds
        | _ -> None

    let service = create ()
    let timeline = document.timeline
    let now = time timeline.currentTime
    equal "document.timeline has a current time" true now.IsSome
    equal "document.timeline duration" None (time timeline.duration)

    for origin in [ 100.0; 0.0; -50.0; 1e9 ] do
        let own = service.CreateDocumentTimeline origin
        let label = "DocumentTimeline originTime " + string origin

        let offset =
            match time own.currentTime, now with
            | Some value, Some reference -> abs (value - (reference - origin)) < Close
            | _ -> false

        equal
            (label
             + ": current time is document time minus origin")
            true
            offset

        equal (label + ": duration") None (time own.duration)
        equal (label + ": a timeline of its own") false (obj.ReferenceEquals (own, timeline))

    let future = service.CreateDocumentTimeline 1e9

    equal
        "a future origin gives a negative current time"
        true
        (time future.currentTime
         |> Option.exists (fun value -> value < 0.0))

    equal "the default origin is document.timeline's" now (time (service.CreateDocumentTimeline ()).currentTime)

    let detached = document.implementation.createHTMLDocument ""
    let inactive = detached.timeline
    equal "inactive timeline current time" None (time inactive.currentTime)
    equal "inactive timeline duration" None (time inactive.duration)
    equal "inactive timeline identity" true (obj.ReferenceEquals (inactive, detached.timeline))

    let keyframe (opacity: string) =
        let property = KeyframeProperty ()
        property.name <- "opacity"
        property.value <- opacity
        let value = Keyframe ()
        value.properties <- [| property |]
        value

    let effect () =
        service.CreateKeyframeEffectFor (null, [| keyframe "0"; keyframe "1" |], 10000.0)

    let parked = service.CreateAnimation (effect (), inactive)
    parked.play ()
    equal "animation on an inactive timeline: play state" AnimationPlayState.Running parked.playState
    equal "animation on an inactive timeline: current time" (Some 0.0) (time parked.currentTime)
    equal "animation on an inactive timeline: start time" None (time parked.startTime)
    parked.cancel ()

    let own = service.CreateDocumentTimeline 250.0
    let moved = service.CreateAnimation (effect (), timeline)
    moved.pause ()
    moved.currentTime <- Some (DoubleOrCSSNumericValue.Double 500.0)
    moved.timeline <- (own :> AnimationTimeline)
    equal "Animation.timeline set to a new timeline" true (obj.ReferenceEquals (moved.timeline, own))
    equal "a paused animation keeps its current time on the new timeline" (Some 500.0) (time moved.currentTime)
    moved.cancel ()
    checks

let private ScrollCss =
    "#tl-s{width:100px;height:100px;overflow:auto}#tl-c{width:300px;height:400px}#tl-n{width:80px;height:80px;overflow:auto}"
    + "#tl-nc{height:240px}#tl-vis{width:100px;height:100px;overflow:visible}#tl-a{width:40px;height:40px;background:rgb(0,0,255)}"

/// <summary>
/// A07: compares scroll timelines with the browser's after each scroll and two frames (<c>settle</c>): axis, source,
/// a null and the default source, the 100% duration, progress of the block, inline and nested scrollers at their start,
/// middle, end and quarter, an animation created by Element.animate on a timeline (current time, play state and
/// computed opacity), a content resize and the source removed. The result is the assertion count.
/// </summary>
let runScrollTimelines (document: Document) (create: unit -> TimelineService) (styles: unit -> ComputedStyleService) (settle: unit -> Async<unit>) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if actual <> expected then
                failwith (
                    label
                    + ": expected "
                    + string (box expected)
                    + ", got "
                    + string (box actual)
                )

        let percent (value: DoubleOrCSSNumericValue option) =
            match value with
            | Some (DoubleOrCSSNumericValue.CSSNumericValue numeric) ->
                let unit = unbox<CSSUnitValue> numeric
                Some (System.Math.Round (unit.value, 3), unit.unit)
            | Some (DoubleOrCSSNumericValue.Double number) -> Some (number, "number")
            | None -> None

        let element tag id =
            let value = document.createElement tag
            value.id <- id
            value

        let style = document.createElement "style"
        style.textContent <- ScrollCss
        document.head.appendChild style |> ignore
        let target = element "div" "tl-a"
        let scroller = element "div" "tl-s"
        let content = element "div" "tl-c"
        let nested = element "div" "tl-n"
        let nestedContent = element "div" "tl-nc"
        let visible = element "div" "tl-vis"
        let tall = element "div" "tl-vis-content"
        tall.setAttribute ("style", "height:300px")
        nested.appendChild nestedContent |> ignore
        content.appendChild nested |> ignore
        scroller.appendChild content |> ignore
        visible.appendChild tall |> ignore

        for node in [ target; scroller; visible ] do
            document.body.appendChild node |> ignore

        let service = create ()
        let computed = styles ()

        try
            let block = service.CreateScrollTimeline scroller
            let inline' = service.CreateScrollTimeline (scroller, ScrollAxis.Inline)
            let inner = service.CreateScrollTimeline nested
            let unscrollable = service.CreateScrollTimeline visible
            let inactive = service.CreateScrollTimelineWithoutSource ()
            let fallback = service.CreateScrollTimeline ()
            equal "block axis" ScrollAxis.Block block.axis
            equal "inline axis" ScrollAxis.Inline inline'.axis
            equal "source" true (obj.ReferenceEquals (block.source, scroller))
            equal "default source is the scrolling element" true (obj.ReferenceEquals (fallback.source, document.scrollingElement))
            equal "null source" true (isNull inactive.source)
            equal "duration" (Some (100.0, "percent")) (percent block.duration)
            equal "inactive timeline current time" None (percent inactive.currentTime)
            equal "unscrollable source current time" None (percent unscrollable.currentTime)
            let options = KeyframeAnimationOptions ()
            options.fill <- FillMode.Both
            options.timeline <- Some (block :> AnimationTimeline)

            let frame (opacity: string) =
                let property = KeyframeProperty ()
                property.name <- "opacity"
                property.value <- opacity
                let value = Keyframe ()
                value.properties <- [| property |]
                value

            let animation = target.animate ([| frame "0"; frame "1" |], options)
            equal "Animation.timeline is the scroll timeline" true (obj.ReferenceEquals (animation.timeline, block))
            do! settle ()

            for top, left, nestedTop, progress, state, opacity in
                [
                    0, 0, 0, 0.0, AnimationPlayState.Running, "0"
                    150, 100, 80, 50.0, AnimationPlayState.Running, "0.5"
                    300, 200, 160, 100.0, AnimationPlayState.Finished, "1"
                    75, 50, 40, 25.0, AnimationPlayState.Running, "0.25"
                ] do
                scroller.scrollTop <- top
                scroller.scrollLeft <- left
                nested.scrollTop <- nestedTop
                do! settle ()
                let label = "scrollTop " + string top
                let wanted = Some (progress, "percent")
                equal (label + ": block progress") wanted (percent block.currentTime)
                equal (label + ": inline progress") wanted (percent inline'.currentTime)
                equal (label + ": nested progress") wanted (percent inner.currentTime)
                equal (label + ": animation current time") wanted (percent animation.currentTime)
                equal (label + ": animation play state") state animation.playState
                equal (label + ": opacity") opacity ((computed.Get (target, None)).getPropertyValue "opacity")

            content.setAttribute ("style", "height:250px")
            do! settle ()
            equal "resized: scrollTop" 75 scroller.scrollTop
            equal "resized: block progress" (Some (50.0, "percent")) (percent block.currentTime)
            equal "resized: animation current time" (Some (50.0, "percent")) (percent animation.currentTime)
            scroller.remove ()
            do! settle ()
            equal "removed source: block current time" None (percent block.currentTime)
            equal "removed source: animation current time" None (percent animation.currentTime)
            animation.cancel ()
        finally
            target.remove ()
            visible.remove ()
            style.remove ()

        return checks
    }

let private ViewCss =
    "#tl-v{position:relative;width:100px;height:100px;overflow:auto}#tl-vc{height:500px}"
    + "#tl-t{position:absolute;top:200px;width:20px;height:50px}#tl-va{width:40px;height:40px;background:rgb(0,0,255)}"

/// <summary>
/// A08: compares view timelines with the browser's after each scroll and two frames (<c>settle</c>): subject, source and
/// axis; start and end offsets and cover-range progress with the insets auto, 10px and "auto 20px"; an inset sequence; an
/// invalid inset; an animation with range strings (current time, range read-back) and the range setters. The result is
/// the assertion count.
/// </summary>
let runViewTimelines (document: Document) (create: unit -> TimelineService) (settle: unit -> Async<unit>) (errorName: exn -> string) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if actual <> expected then
                failwith (
                    label
                    + ": expected "
                    + string (box expected)
                    + ", got "
                    + string (box actual)
                )

        let numeric (value: CSSNumericValue) =
            let unit = unbox<CSSUnitValue> value
            System.Math.Round (unit.value, 3), unit.unit

        let percent (value: DoubleOrCSSNumericValue option) =
            match value with
            | Some (DoubleOrCSSNumericValue.CSSNumericValue value) -> Some (numeric value)
            | Some (DoubleOrCSSNumericValue.Double number) -> Some (number, "number")
            | None -> None

        let range (value: TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString) =
            match value with
            | TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.TimelineRangeOffset offset ->
                let amount =
                    match offset.offset with
                    | Some value ->
                        let number, unit = numeric value
                        " " + string number + " " + unit
                    | None -> ""

                defaultArg offset.rangeName "" + amount
            | TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString text -> text
            | TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.CSSNumericValue value ->
                let number, unit = numeric value
                string number + " " + unit
            | TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.CSSOMKeywordValue value -> value.value

        let element id =
            let value = document.createElement "div"
            value.id <- id
            value

        let style = document.createElement "style"
        style.textContent <- ViewCss
        document.head.appendChild style |> ignore
        let target = element "tl-va"
        let scroller = element "tl-v"
        let subject = element "tl-t"
        scroller.appendChild (element "tl-vc") |> ignore
        scroller.appendChild subject |> ignore

        for node in [ target; scroller ] do
            document.body.appendChild node |> ignore

        let service = create ()

        try
            let auto = service.CreateViewTimeline subject
            let inset = service.CreateViewTimeline (subject, ScrollAxis.Block, "10px")
            let mixed = service.CreateViewTimeline (subject, inset = "auto 20px")

            let sequence =
                service.CreateViewTimelineWithInsets (
                    subject,
                    [|
                        CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString "auto"
                        CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString "auto"
                    |]
                )

            equal "subject" true (obj.ReferenceEquals (auto.subject, subject))
            equal "source is the nearest scroller" true (obj.ReferenceEquals (auto.source, scroller))
            equal "axis" ScrollAxis.Block auto.axis

            let invalid =
                try
                    service.CreateViewTimeline (subject, inset = "1s")
                    |> ignore

                    "none"
                with error ->
                    errorName error

            equal "invalid inset" "TypeError" invalid
            let options = KeyframeAnimationOptions ()
            options.fill <- FillMode.Both
            options.timeline <- Some (auto :> AnimationTimeline)
            options.rangeStart <- TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString "entry 0%"
            options.rangeEnd <- TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString "exit 100%"

            let frame (opacity: string) =
                let property = KeyframeProperty ()
                property.name <- "opacity"
                property.value <- opacity
                let value = Keyframe ()
                value.properties <- [| property |]
                value

            let animation = target.animate ([| frame "0"; frame "1" |], options)
            do! settle ()
            equal "rangeStart read-back" "entry 0 percent" (range animation.rangeStart)
            equal "rangeEnd read-back" "exit 100 percent" (range animation.rangeEnd)

            for top, cover, ten, twenty in
                [
                    0, -66.667, -84.615, -92.308
                    100, 0.0, -7.692, -15.385
                    150, 33.333, 30.769, 23.077
                    250, 100.0, 107.692, 100.0
                    400, 200.0, 223.077, 215.385
                ] do
                scroller.scrollTop <- top
                do! settle ()
                let label = "scrollTop " + string top
                equal (label + ": cover progress") (Some (cover, "percent")) (percent auto.currentTime)
                equal (label + ": 10px inset progress") (Some (ten, "percent")) (percent inset.currentTime)
                equal (label + ": auto 20px inset progress") (Some (twenty, "percent")) (percent mixed.currentTime)
                equal (label + ": inset sequence progress") (Some (cover, "percent")) (percent sequence.currentTime)
                equal (label + ": start offset") (100.0, "px") (numeric auto.startOffset)
                equal (label + ": end offset") (250.0, "px") (numeric auto.endOffset)
                equal (label + ": 10px start offset") (110.0, "px") (numeric inset.startOffset)
                equal (label + ": 10px end offset") (240.0, "px") (numeric inset.endOffset)
                // The animation holds its end once the cover range is past.
                equal (label + ": animation current time") (Some (min cover 100.0, "percent")) (percent animation.currentTime)

            scroller.scrollTop <- 175
            animation.rangeStart <- TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString "cover 20%"
            animation.rangeEnd <- TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString "cover 80%"
            do! settle ()
            equal "set rangeStart read-back" "cover 20 percent" (range animation.rangeStart)
            equal "set rangeEnd read-back" "cover 80 percent" (range animation.rangeEnd)
            equal "set range current time" (Some (30.0, "percent")) (percent animation.currentTime)
            animation.cancel ()
        finally
            target.remove ()
            scroller.remove ()
            style.remove ()

        return checks
    }
