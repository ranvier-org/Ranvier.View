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
            failwith (label + ": expected " + string (box expected) + ", got " + string (box actual))

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

        equal (label + ": current time is document time minus origin") true offset
        equal (label + ": duration") None (time own.duration)
        equal (label + ": a timeline of its own") false (obj.ReferenceEquals (own, timeline))

    let future = service.CreateDocumentTimeline 1e9

    equal "a future origin gives a negative current time" true (time future.currentTime |> Option.exists (fun value -> value < 0.0))

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
