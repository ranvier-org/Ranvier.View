module internal NativeApiConformance.EventDetailChecks

open System
open Ranvier.Dom

/// <summary>Task and frame delivery the fixture drives.</summary>
type Driver =
    {
        /// <summary>Runs queued tasks and one rendering update, so animation and transition events dispatch.</summary>
        Settle: unit -> Async<unit>
    }

// The constructed values of the I08 producer fixture (Ranvier.WebCore tests/fixtures/event-details-cases.json).
let private full =
    {
        Bubbles = true
        Cancelable = true
        Composed = true
    }

/// <summary>
/// Checks every event detail constructor with its defaults and a full init, a constructed record dispatched to a
/// listener, argument validation, and engine-dispatched animation and transition payloads; the result is the
/// assertion count.
/// </summary>
let run (document: Document) (create: unit -> EventDetailService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        // Providers raise ArgumentNullException and ArgumentException; the native lifetime checks assert
        // the types, which Fable erases.
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

        let options label (event: Event) (expected: EventOptions) (eventType: string) =
            equal (label + " type") eventType event.``type``
            equal (label + " bubbles") expected.Bubbles event.bubbles
            equal (label + " cancelable") expected.Cancelable event.cancelable
            equal (label + " composed") expected.Composed event.composed
            equal (label + " isTrusted") false event.isTrusted

        use service = create ()

        let error =
            service.CreateErrorEvent ("error", EventOptions.Default, ErrorFields.Default)

        options "ErrorEvent defaults" error EventOptions.Default "error"
        equal "ErrorEvent defaults" ("", "", 0u, 0u) (error.message, error.filename, error.lineno, error.colno)

        let error =
            service.CreateErrorEvent (
                "error",
                full,
                {
                    Message = "boom"
                    Filename = "https://app.test/a.js"
                    Lineno = 3u
                    Colno = 4u
                }
            )

        options "ErrorEvent full" error full "error"
        equal "ErrorEvent full" ("boom", "https://app.test/a.js", 3u, 4u) (error.message, error.filename, error.lineno, error.colno)

        let progress =
            service.CreateProgressEvent ("progress", EventOptions.Default, ProgressFields.Default)

        equal "ProgressEvent defaults" (false, 0.0, 0.0) (progress.lengthComputable, progress.loaded, progress.total)

        let progress =
            service.CreateProgressEvent (
                "progress",
                full,
                {
                    LengthComputable = true
                    Loaded = 5.0
                    Total = 10.0
                }
            )

        options "ProgressEvent full" progress full "progress"
        equal "ProgressEvent full" (true, 5.0, 10.0) (progress.lengthComputable, progress.loaded, progress.total)

        let animation =
            service.CreateAnimationEvent ("animationend", EventOptions.Default, AnimationFields.Default)

        equal "AnimationEvent defaults" ("", 0.0, "") (animation.animationName, animation.elapsedTime, animation.pseudoElement)

        let animation =
            service.CreateAnimationEvent (
                "animationend",
                full,
                {
                    Name = "spin"
                    ElapsedTime = 1.5
                    PseudoElement = "::before"
                }
            )

        options "AnimationEvent full" animation full "animationend"
        equal "AnimationEvent full" ("spin", 1.5, "::before") (animation.animationName, animation.elapsedTime, animation.pseudoElement)

        let transition =
            service.CreateTransitionEvent ("transitionend", EventOptions.Default, AnimationFields.Default)

        equal "TransitionEvent defaults" ("", 0.0, "") (transition.propertyName, transition.elapsedTime, transition.pseudoElement)

        let transition =
            service.CreateTransitionEvent (
                "transitionend",
                full,
                {
                    Name = "opacity"
                    ElapsedTime = 0.25
                    PseudoElement = "::after"
                }
            )

        options "TransitionEvent full" transition full "transitionend"

        equal "TransitionEvent full" ("opacity", 0.25, "::after") (transition.propertyName, transition.elapsedTime, transition.pseudoElement)

        let visibility =
            service.CreateContentVisibilityEvent ("contentvisibilityautostatechange", EventOptions.Default, false)

        equal "ContentVisibilityAutoStateChangeEvent defaults" false visibility.skipped

        let visibility =
            service.CreateContentVisibilityEvent ("contentvisibilityautostatechange", full, true)

        options "ContentVisibilityAutoStateChangeEvent full" visibility full "contentvisibilityautostatechange"
        equal "ContentVisibilityAutoStateChangeEvent full" true visibility.skipped

        let number (value: DoubleOrCSSNumericValue option) =
            match value with
            | Some (DoubleOrCSSNumericValue.Double number) -> Some number
            | Some _ -> Some nan
            | None -> None

        let playback =
            service.CreateAnimationPlaybackEvent ("finish", EventOptions.Default, AnimationPlaybackFields.Default)

        equal "AnimationPlaybackEvent defaults" (None, None) (number playback.currentTime, number playback.timelineTime)

        let playback =
            service.CreateAnimationPlaybackEvent (
                "finish",
                full,
                {
                    CurrentTime = Some 12.0
                    TimelineTime = Some 34.0
                }
            )

        options "AnimationPlaybackEvent full" playback full "finish"
        equal "AnimationPlaybackEvent full" (Some 12.0, Some 34.0) (number playback.currentTime, number playback.timelineTime)

        equal "PageTransitionEvent defaults" false (service.CreatePageTransitionEvent ("pageshow", EventOptions.Default, false)).persisted
        let page = service.CreatePageTransitionEvent ("pageshow", full, true)
        options "PageTransitionEvent full" page full "pageshow"
        equal "PageTransitionEvent full" true page.persisted

        let hash =
            service.CreateHashChangeEvent ("hashchange", EventOptions.Default, HashChangeFields.Default)

        equal "HashChangeEvent defaults" ("", "") (hash.oldURL, hash.newURL)

        let hash =
            service.CreateHashChangeEvent (
                "hashchange",
                full,
                {
                    OldURL = "https://app.test/#one"
                    NewURL = "https://app.test/#two"
                }
            )

        options "HashChangeEvent full" hash full "hashchange"
        equal "HashChangeEvent full" ("https://app.test/#one", "https://app.test/#two") (hash.oldURL, hash.newURL)

        let media =
            service.CreateMediaQueryListEvent ("change", EventOptions.Default, MediaQueryFields.Default)

        equal "MediaQueryListEvent defaults" ("", false) (media.media, media.matches)

        let media =
            service.CreateMediaQueryListEvent (
                "change",
                full,
                {
                    Media = "(min-width: 300px)"
                    Matches = true
                }
            )

        options "MediaQueryListEvent full" media full "change"
        equal "MediaQueryListEvent full" ("(min-width: 300px)", true) (media.media, media.matches)

        equal "PopStateEvent defaults" false (service.CreatePopStateEvent ("popstate", EventOptions.Default, false)).hasUAVisualTransition

        let pop = service.CreatePopStateEvent ("popstate", full, true)
        options "PopStateEvent full" pop full "popstate"
        equal "PopStateEvent full" true pop.hasUAVisualTransition

        let composition =
            service.CreateCompositionEvent ("compositionupdate", EventOptions.Default, CompositionFields.Default)

        equal "CompositionEvent defaults" ("", 0) (composition.data, composition.detail)

        let composition =
            service.CreateCompositionEvent ("compositionupdate", full, { Data = "ka"; Detail = 2 })

        options "CompositionEvent full" composition full "compositionupdate"
        equal "CompositionEvent full" ("ka", 2) (composition.data, composition.detail)

        // A constructed record reaches a listener with its fields.
        let host = document.createElement "div"
        host.setAttribute ("id", "event-detail-host")
        document.body.appendChild host |> ignore
        let mutable received = []

        let listener: EventListener =
            fun event -> received <- (unbox<ErrorEvent> event).message :: received

        host.addEventListener ("error", listener)
        host.dispatchEvent error |> ignore
        host.removeEventListener ("error", listener)
        equal "dispatched ErrorEvent reaches the listener" [ "boom" ] received

        raises "a null event type" (fun () ->
            service.CreateErrorEvent (null, full, ErrorFields.Default)
            |> ignore)

        raises "a null message" (fun () ->
            service.CreateErrorEvent (
                "error",
                full,
                { ErrorFields.Default with
                    Message = null
                }
            )
            |> ignore)

        raises "a NaN loaded" (fun () ->
            service.CreateProgressEvent (
                "progress",
                full,
                { ProgressFields.Default with
                    Loaded = nan
                }
            )
            |> ignore)

        raises "an infinite current time" (fun () ->
            service.CreateAnimationPlaybackEvent (
                "finish",
                full,
                {
                    CurrentTime = Some infinity
                    TimelineTime = None
                }
            )
            |> ignore)

        // Engine payloads: a 0 s animation on a pseudo-element and a 50 ms transition, each started by a style change.
        let style = document.createElement "style"

        style.textContent <-
            "@keyframes spin{from{opacity:0}to{opacity:1}}#event-detail-a.run::before{content:'x';animation:spin 0s linear 1}"
            + "#event-detail-t{width:10px;height:10px;opacity:1;transition:opacity 50ms linear}#event-detail-t.off{opacity:0}"

        document.head.appendChild style |> ignore
        let animated = document.createElement "div"
        animated.setAttribute ("id", "event-detail-a")
        host.appendChild animated |> ignore
        let transitioned = document.createElement "div"
        transitioned.setAttribute ("id", "event-detail-t")
        host.appendChild transitioned |> ignore
        let mutable engine = []

        let record: EventListener =
            fun event ->
                let entry =
                    match event.``type`` with
                    | "animationstart"
                    | "animationend" ->
                        let value = unbox<AnimationEvent> event
                        event.``type``, value.animationName, value.elapsedTime, value.pseudoElement
                    | _ ->
                        let value = unbox<TransitionEvent> event
                        event.``type``, value.propertyName, value.elapsedTime, value.pseudoElement

                engine <- engine @ [ entry ]

        let kinds =
            [
                "animationstart"
                "animationend"
                "transitionrun"
                "transitionstart"
                "transitionend"
            ]

        for kind in kinds do
            host.addEventListener (kind, record)

        // A hang guard: the number of settles depends on timing and is not asserted.
        let until (kind: string) =
            async {
                let mutable rounds = 0

                while not (
                    engine
                    |> List.exists (fun (name, _, _, _) -> name = kind)
                ) do
                    if rounds > 500 then
                        failwith $"{kind} was not dispatched"

                    rounds <- rounds + 1
                    do! driver.Settle ()
            }

        do! driver.Settle ()
        animated.setAttribute ("class", "run")
        do! until "animationend"

        equal
            "0 s animation events"
            [
                "animationstart", "spin", 0.0, "::before"
                "animationend", "spin", 0.0, "::before"
            ]
            (List.distinct engine)

        engine <- []
        do! driver.Settle ()
        transitioned.setAttribute ("class", "off")
        do! until "transitionend"

        let names =
            engine
            |> List.map (fun (name, _, _, _) -> name)
            |> List.distinct

        equal "transition event order" [ "transitionrun"; "transitionstart"; "transitionend" ] names

        let last (kind: string) =
            engine
            |> List.find (fun (name, _, _, _) -> name = kind)

        let _, property, start, pseudo = last "transitionrun"
        equal "transitionrun payload" ("opacity", 0.0, "") (property, start, pseudo)
        let _, property, finish, _ = last "transitionend"
        equal "transitionend payload" ("opacity", true) (property, abs (finish - 0.05) < 1e-9)

        for kind in kinds do
            host.removeEventListener (kind, record)

        host.remove ()
        style.remove ()
        return checks
    }
