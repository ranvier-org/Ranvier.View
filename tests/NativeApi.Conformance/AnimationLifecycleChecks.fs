module internal NativeApiConformance.AnimationLifecycleChecks

open System
open Ranvier.Dom

/// <summary>Task delivery and the engine facts the fixture needs.</summary>
type Driver =
    {
        /// <summary>Lets queued work and one rendering update run, so promises settle and playback events dispatch.</summary>
        Settle: unit -> Async<unit>
    }

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

let private fade () =
    [| frame [ "opacity", "0" ]; frame [ "opacity", "1" ] |]

let private time (value: DoubleOrCSSNumericValue option) =
    match value with
    | Some (DoubleOrCSSNumericValue.Double milliseconds) -> Some milliseconds
    | _ -> None

let private describe (outcome: ResourceOutcome<WebAnimation>) =
    match outcome with
    | ResourceOutcome.Succeeded _ -> "resolved"
    | ResourceOutcome.Failed (ResourceFailure.Aborted name) -> "aborted " + name
    | ResourceOutcome.Failed ResourceFailure.Cancelled -> "cancelled"
    | ResourceOutcome.Failed (ResourceFailure.Invalidated _) -> "invalidated"
    | ResourceOutcome.Failed (ResourceFailure.LoadFailed message) -> "load failed " + message
    | ResourceOutcome.Failed (ResourceFailure.DecodeFailed message) -> "decode failed " + message

/// <summary>
/// Walks an animation through play, pause, updatePlaybackRate, reverse, finish, cancel and persist and compares
/// playState, pending, replaceState, startTime presence and playbackRate with the browser's values; observes the ready
/// and finished promises through <c>AnimationService.Ready</c> and <c>Finished</c>, with their settlement order and the
/// finish, cancel and remove playback events of each animation, including a cancel inside an event listener and inside
/// a ready callback, disposal before completion and service disposal. The values are the Chromium 156 oracle of
/// Ranvier.WebCore tests/fixtures/animation-lifecycle-oracle.json. The result is the assertion count.
/// </summary>
let run (document: Document) (create: unit -> AnimationService) (driver: Driver) =
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

        let element id =
            let value = document.createElement "div" :?> HTMLElement
            value.id <- id
            document.body.appendChild value |> ignore
            value

        let a = element "life-a"
        let b = element "life-b"
        let service = create ()
        let log = Collections.Generic.List<string>()

        let observe label (animation: WebAnimation) =
            let ready =
                service.Ready (animation, fun outcome -> log.Add (label + ":ready " + describe outcome))

            let finished =
                service.Finished (animation, fun outcome -> log.Add (label + ":finished " + describe outcome))

            for kind in [ "finish"; "cancel"; "remove" ] do
                animation.addEventListener (
                    kind,
                    fun event ->
                        let playback = unbox<AnimationPlaybackEvent> event

                        let current =
                            if (time playback.currentTime).IsSome then
                                "number"
                            else
                                "null"

                        log.Add (label + ":" + event.``type`` + " " + current)
                )

            ready, finished

        let until label (condition: unit -> bool) =
            async {
                let mutable rounds = 0

                while not (condition ()) do
                    rounds <- rounds + 1

                    if rounds > 2000 then
                        failwith (
                            "hang guard: "
                            + label
                            + " "
                            + String.Join (", ", log)
                        )

                    do! driver.Settle ()
            }

        let settle () =
            async {
                for _ in 1..4 do
                    do! driver.Settle ()
            }

        let entries (label: string) =
            log
            |> Seq.filter (fun entry -> entry.StartsWith (label + ":"))
            |> List.ofSeq

        // The state machine.
        let animation = a.animate (fade (), 1000.0)
        equal "animate playState" AnimationPlayState.Running animation.playState
        equal "animate pending" true animation.pending
        equal "animate startTime" None (time animation.startTime)
        animation.pause ()
        equal "pause while pending" (AnimationPlayState.Paused, true) (animation.playState, animation.pending)
        animation.play ()
        animation.updatePlaybackRate 2.0

        equal
            "updatePlaybackRate while pending"
            (AnimationPlayState.Running, true, 1.0)
            (animation.playState, animation.pending, animation.playbackRate)

        let readyOnce = ref None
        use _ = service.Ready (animation, fun outcome -> readyOnce.Value <- Some outcome)
        do! until "ready" (fun () -> readyOnce.Value.IsSome)

        equal
            "ready resolves with the animation"
            true
            (match readyOnce.Value with
             | Some (ResourceOutcome.Succeeded value) -> Object.ReferenceEquals (value, animation)
             | _ -> false)

        equal "after ready" (false, 2.0, true) (animation.pending, animation.playbackRate, (time animation.startTime).IsSome)
        animation.currentTime <- Some (DoubleOrCSSNumericValue.Double 500.0)
        animation.pause ()
        equal "pause running" (AnimationPlayState.Paused, true) (animation.playState, animation.pending)
        let pausedReady = ref None
        use _ = service.Ready (animation, fun outcome -> pausedReady.Value <- Some outcome)
        do! until "paused ready" (fun () -> pausedReady.Value.IsSome)
        equal "paused ready" (AnimationPlayState.Paused, false, None) (animation.playState, animation.pending, time animation.startTime)
        animation.reverse ()
        equal "reverse paused" (AnimationPlayState.Running, true) (animation.playState, animation.pending)
        animation.finish ()
        equal "finish" (AnimationPlayState.Finished, false, -2.0) (animation.playState, animation.pending, animation.playbackRate)
        equal "finish at the start" (Some 0.0) (time animation.currentTime)
        animation.cancel ()
        equal "cancel" (AnimationPlayState.Idle, None, None) (animation.playState, time animation.currentTime, time animation.startTime)
        animation.updatePlaybackRate 1.0
        animation.persist ()
        equal "persist" (AnimationPlayState.Idle, AnimationReplaceState.Persisted) (animation.playState, animation.replaceState)

        // finish() resolves ready then finished, and dispatches finish.
        let finishing = b.animate (fade (), 1000.0)
        let r1, f1 = observe "b" finishing
        finishing.finish ()
        do! until "finish()" (fun () -> (entries "b").Length >= 3)
        do! settle ()

        equal
            "finish(): promises"
            [ "b:ready resolved"; "b:finished resolved" ]
            (entries "b"
             |> List.filter (fun e -> e.Contains "ready" || e.Contains "finished "))

        equal
            "finish(): events"
            [ "b:finish number" ]
            (entries "b"
             |> List.filter (fun e -> e.StartsWith "b:finish "))

        finishing.cancel ()
        r1.Dispose ()
        f1.Dispose ()

        // cancel() while pending aborts both promises and dispatches cancel.
        log.Clear ()
        let pendingCancel = a.animate (fade (), 1000.0)
        let r2, f2 = observe "c" pendingCancel
        pendingCancel.cancel ()
        do! until "cancel pending" (fun () -> (entries "c").Length >= 3)
        do! settle ()

        equal
            "cancel pending: promises"
            [ "c:ready aborted AbortError"; "c:finished aborted AbortError" ]
            (entries "c"
             |> List.filter (fun e -> e.Contains "ready" || e.Contains "finished "))

        equal
            "cancel pending: events"
            [ "c:cancel null" ]
            (entries "c"
             |> List.filter (fun e -> e.StartsWith "c:cancel"))

        r2.Dispose ()
        f2.Dispose ()

        // A cancel inside a finish listener aborts the other animation's finished promise.
        log.Clear ()
        let e = a.animate (fade (), 1000.0)
        let f = b.animate (fade (), 1000.0)
        let r3, f3 = observe "e" e
        let r4, f4 = observe "f" f
        e.addEventListener ("finish", fun _ -> f.cancel ())

        do!
            until "listener: ready" (fun () ->
                log.Contains "e:ready resolved"
                && log.Contains "f:ready resolved")

        e.finish ()

        do!
            until "cancel in listener" (fun () ->
                log.Contains "f:finished aborted AbortError"
                && log.Contains "f:cancel null")

        do! settle ()

        equal
            "cancel in listener: e"
            [ "e:ready resolved"; "e:finished resolved"; "e:finish number" ]
            (entries "e"
             |> List.sortBy (fun x -> x.Contains "finish number"))

        equal
            "cancel in listener: f"
            [ "f:ready resolved"; "f:finished aborted AbortError"; "f:cancel null" ]
            (entries "f"
             |> List.sortBy (fun x -> x.Contains "cancel null"))

        equal "cancel in listener: order" true (log.IndexOf "e:finished resolved" < log.IndexOf "f:finished aborted AbortError")
        e.cancel ()

        for operation in [ r3; f3; r4; f4 ] do
            operation.Dispose ()

        // A cancel inside a ready callback aborts the finished promise.
        log.Clear ()
        let g = a.animate (fade (), 1000.0)
        use _ = service.Ready (g, fun _ -> g.cancel ())
        let finishedG = ref None

        use _ =
            service.Finished (g, fun outcome -> finishedG.Value <- Some (describe outcome))

        do! until "cancel in ready" (fun () -> finishedG.Value.IsSome)
        equal "cancel in ready" (Some "aborted AbortError") finishedG.Value
        equal "cancel in ready: state" AnimationPlayState.Idle g.playState

        // Disposal before completion delivers Cancelled exactly once.
        let held = a.animate (fade (), 1000.0)
        let disposed = Collections.Generic.List<string>()

        let operation =
            service.Finished (held, fun outcome -> disposed.Add (describe outcome))

        operation.Dispose ()
        equal "dispose before completion" [ "cancelled" ] (List.ofSeq disposed)
        held.finish ()
        do! settle ()
        equal "no completion after disposal" [ "cancelled" ] (List.ofSeq disposed)

        // A replaced filling animation dispatches remove.
        log.Clear ()
        let options = KeyframeAnimationOptions ()
        options.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 30.0
        options.fill <- FillMode.Forwards
        let x = b.animate (fade (), options)
        let y2 = b.animate (fade (), options)
        let r5, f5 = observe "x" x
        do! until "replace" (fun () -> log.Contains "x:remove number")
        equal "replaced" (AnimationPlayState.Finished, AnimationReplaceState.Removed) (x.playState, x.replaceState)
        equal "replacer" AnimationReplaceState.Active y2.replaceState
        x.persist ()
        equal "persist removed" AnimationReplaceState.Persisted x.replaceState
        r5.Dispose ()
        f5.Dispose ()
        x.cancel ()
        y2.cancel ()

        // Service disposal cancels pending operations.
        let last = a.animate (fade (), 100000.0)
        let pending = ref None

        let _ =
            service.Finished (last, fun outcome -> pending.Value <- Some (describe outcome))

        service.Dispose ()
        equal "service disposal" (Some "cancelled") pending.Value
        last.cancel ()
        return checks
    }
