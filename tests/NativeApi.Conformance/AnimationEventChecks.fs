module internal NativeApiConformance.AnimationEventChecks

open System
open Ranvier.Dom

/// <summary>Task delivery and the engine facts the fixture needs.</summary>
type Driver =
    {
        /// <summary>Lets queued work and one rendering update run, so animation and transition events dispatch.</summary>
        Settle: unit -> Async<unit>
        /// <summary>The interface name of an event object, such as <c>AnimationEvent</c>.</summary>
        InterfaceName: Event -> string
    }

let private Css =
    "@keyframes life-spin{from{opacity:0}to{opacity:1}}"
    + ".ev{width:20px;height:20px;background:blue}"
    + "#ev-css.on{animation:life-spin 80ms 2}#ev-pse::before{content:'x'}#ev-pse.on::before{animation:life-spin 80ms}"
    + "#ev-cc.on{animation:life-spin 2000ms}#ev-cc.off{animation:none}"
    + "#ev-tr{transition:opacity 80ms;opacity:1}#ev-tr.on{opacity:0}"
    + "#ev-trp::after{content:'y';transition:opacity 80ms;opacity:1}#ev-trp.on::after{opacity:0}"
    + "#ev-generic.on{animation:life-spin 80ms}"

let private Types =
    [
        "animationstart"
        "animationiteration"
        "animationend"
        "animationcancel"
        "transitionrun"
        "transitionstart"
        "transitionend"
        "transitioncancel"
    ]

/// <summary>
/// Captures the engine's CSS animation and transition events at the document as typed <c>AnimationEvent</c> and
/// <c>TransitionEvent</c> objects, with animationName or propertyName, elapsedTime and the <c>::before</c> and
/// <c>::after</c> pseudo-elements; the finish and cancel <c>AnimationPlaybackEvent</c>s of Element.animate with their
/// times; and generic subscription: capture and bubbling listeners both see an event, and neither after removal. The
/// values are the Chromium 156 oracle of Ranvier.WebCore tests/fixtures/animation-events-oracle.json. The result is the
/// assertion count.
/// </summary>
let run (document: Document) (driver: Driver) =
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

        let style = document.createElement "style"
        style.textContent <- Css
        document.head.appendChild style |> ignore

        let element id =
            let value = document.createElement "div" :?> HTMLElement
            value.id <- id
            value.className <- "ev"
            document.body.appendChild value |> ignore
            value

        let until label (condition: unit -> bool) =
            async {
                let mutable rounds = 0

                while not (condition ()) do
                    rounds <- rounds + 1

                    if rounds > 2000 then
                        failwith ("hang guard: " + label)

                    do! driver.Settle ()
            }

        let settle count =
            async {
                for _ in 1..count do
                    do! driver.Settle ()
            }

        let rounded (value: float) =
            Math.Round (value * 1e6) / 1e6

        let targetId (event: Event) =
            match event.target with
            | null -> ""
            | target -> (unbox<Element> target).id

        // One CSS case: the element's events in dispatch order; a cancel case sets its class after the trigger event.
        let case (id: string) (cancel: (string * string) option) (last: string) =
            async {
                let target = element id
                let events = Collections.Generic.List<string * string * string * string * string>()
                let mutable cancelPending = false

                let record (event: Event) =
                    if targetId event = id then
                        let kind = event.``type``
                        let interfaceName = driver.InterfaceName event

                        let name, elapsed, pseudo =
                            if interfaceName = "AnimationEvent" then
                                let value = unbox<AnimationEvent> event
                                value.animationName, value.elapsedTime, value.pseudoElement
                            else
                                let value = unbox<TransitionEvent> event
                                value.propertyName, value.elapsedTime, value.pseudoElement

                        let elapsedText =
                            if kind.EndsWith "cancel" then
                                "number"
                            else
                                string (rounded elapsed)

                        events.Add ((kind, interfaceName, name, elapsedText, pseudo))

                        match cancel with
                        | Some (trigger, _) when kind = trigger -> cancelPending <- true
                        | _ -> ()

                let listeners =
                    Types
                    |> List.map (fun kind -> kind, (fun (event: Event) -> record event))

                for kind, listener in listeners do
                    document.addEventListener (kind, listener)

                do! settle 2
                target.className <- "ev on"

                let mutable cancelled = false

                do!
                    until id (fun () ->
                        if cancelPending && not cancelled then
                            cancelled <- true

                            match cancel with
                            | Some (_, value) -> target.className <- "ev " + value
                            | None -> ()

                        events
                        |> Seq.exists (fun (kind, _, _, _, _) -> kind = last))

                do! settle 4

                for kind, listener in listeners do
                    document.removeEventListener (kind, listener)

                return List.ofSeq events
            }

        let! css = case "ev-css" None "animationend"

        equal
            "animation iterations"
            [
                "animationstart", "AnimationEvent", "life-spin", "0", ""
                "animationiteration", "AnimationEvent", "life-spin", "0.08", ""
                "animationend", "AnimationEvent", "life-spin", "0.16", ""
            ]
            css

        let! pseudo = case "ev-pse" None "animationend"

        equal
            "animation ::before"
            [
                "animationstart", "AnimationEvent", "life-spin", "0", "::before"
                "animationend", "AnimationEvent", "life-spin", "0.08", "::before"
            ]
            pseudo

        let! cancelled = case "ev-cc" (Some ("animationstart", "off")) "animationcancel"

        equal
            "animation cancel"
            [
                "animationstart", "AnimationEvent", "life-spin", "0", ""
                "animationcancel", "AnimationEvent", "life-spin", "number", ""
            ]
            cancelled

        let! transition = case "ev-tr" None "transitionend"

        equal
            "transition"
            [
                "transitionrun", "TransitionEvent", "opacity", "0", ""
                "transitionstart", "TransitionEvent", "opacity", "0", ""
                "transitionend", "TransitionEvent", "opacity", "0.08", ""
            ]
            transition

        let! after = case "ev-trp" None "transitionend"

        equal
            "transition ::after"
            [
                "transitionrun", "TransitionEvent", "opacity", "0", "::after"
                "transitionstart", "TransitionEvent", "opacity", "0", "::after"
                "transitionend", "TransitionEvent", "opacity", "0.08", "::after"
            ]
            after

        // Playback events of Element.animate.
        let playback (duration: float) (cancel: bool) (last: string) =
            async {
                let target = element ("ev-pb-" + string duration)

                let keyframe opacity =
                    let value = Keyframe ()
                    let property = KeyframeProperty ()
                    property.name <- "opacity"
                    property.value <- opacity
                    value.properties <- [| property |]
                    value

                let animation = target.animate ([| keyframe "0"; keyframe "1" |], duration)
                let events = Collections.Generic.List<string * string * string * string>()

                for kind in [ "finish"; "cancel"; "remove" ] do
                    animation.addEventListener (
                        kind,
                        fun event ->
                            let value = unbox<AnimationPlaybackEvent> event

                            let current =
                                match value.currentTime with
                                | Some (DoubleOrCSSNumericValue.Double milliseconds) -> string (rounded milliseconds)
                                | _ -> "null"

                            let timeline =
                                match value.timelineTime with
                                | Some (DoubleOrCSSNumericValue.Double _) -> "number"
                                | _ -> "null"

                            events.Add ((event.``type``, driver.InterfaceName event, current, timeline))
                    )

                do! until "playback ready" (fun () -> not animation.pending)

                if cancel then
                    animation.cancel ()

                do!
                    until "playback" (fun () ->
                        events
                        |> Seq.exists (fun (kind, _, _, _) -> kind = last))

                do! settle 3
                animation.cancel ()
                return List.ofSeq events
            }

        let! finished = playback 80.0 false "finish"
        equal "playback finish" [ "finish", "AnimationPlaybackEvent", "80", "number" ] finished
        let! cancelledPlayback = playback 2000.0 true "cancel"
        equal "playback cancel" [ "cancel", "AnimationPlaybackEvent", "null", "number" ] cancelledPlayback

        // Generic subscription stays intact.
        let generic = element "ev-generic"
        let seen = Collections.Generic.List<string>()

        let capture =
            fun (event: Event) ->
                seen.Add (
                    "capture:"
                    + event.``type``
                    + ":"
                    + string event.eventPhase
                )

        let bubble =
            fun (event: Event) ->
                if targetId event = "ev-generic" then
                    seen.Add (
                        "bubble:"
                        + event.``type``
                        + ":"
                        + string event.eventPhase
                    )

        document.addEventListener ("animationstart", capture, true)
        document.addEventListener ("animationstart", bubble)
        generic.className <- "ev on"
        do! until "generic" (fun () -> seen.Count >= 2)
        equal "capture and bubble" [ "capture:animationstart:1"; "bubble:animationstart:3" ] (List.ofSeq seen)
        document.removeEventListener ("animationstart", capture, true)
        document.removeEventListener ("animationstart", bubble)
        generic.className <- "ev"
        do! settle 2
        generic.className <- "ev on"
        do! settle 8
        equal "removed listeners" 2 seen.Count
        return checks
    }
