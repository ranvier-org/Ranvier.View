module internal NativeApiConformance.CustomEffectChecks

open System.Collections.Generic
open Ranvier.Dom

/// <summary>Advances one host by an animation frame and delivers the ticks it computed.</summary>
type Driver =
    {
        NextFrame: unit -> Async<unit>
        /// <summary>The number of tick exceptions the provider has routed to the host's error reporting.</summary>
        RoutedErrors: unit -> int
    }

/// <summary>
/// Drives custom effects through the shared service: seeked progress of paused effects, DocumentTimeline.animate with
/// its id and playing animation, animation order within a frame, a tick that writes the DOM, a tick that disposes its own
/// effect, a tick exception that is routed and leaves later ticks running, disposal of one effect and of the service,
/// and the TypeError for invalid timing. Assertions count no ticks: the number of frames a provider delivers varies.
/// The result is the assertion count.
/// </summary>
let run
    (document: Document)
    (create: unit -> CustomEffectService)
    (animations: unit -> AnimationService)
    (styles: unit -> ComputedStyleService)
    (errorName: exn -> string)
    (driver: Driver)
    =
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

        let check label condition =
            equal label true condition

        let near (a: float) (b: float) =
            abs (a - b) < 1e-5

        let frames count =
            async {
                for _ in 1..count do
                    do! driver.NextFrame ()
            }

        let service = create ()
        let animation = animations ()
        let computed = styles ()
        let log = List<string * float>()

        let record label =
            fun (progress: float) -> log.Add ((label, progress))

        let since (label: string) (start: int) =
            [
                for index in start .. log.Count - 1 do
                    let name, value = log[index]

                    if name = label then
                        yield value
            ]

        let timing (duration: float) (configure: EffectTiming -> unit) =
            let value = EffectTiming ()
            value.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble duration
            configure value
            value

        let seekTo (playing: WebAnimation) (time: float) =
            playing.currentTime <- Some (DoubleOrCSSNumericValue.Double time)

        // 1. Seeked progress of paused effects: the tick after a frame receives the effect's progress there.
        let seeked =
            [
                "linear", timing 1000.0 ignore, [ 250.0, Some 0.25; 1000.0, None ]
                "reverse", timing 1000.0 (fun t -> t.direction <- PlaybackDirection.Reverse), [ 250.0, Some 0.75 ]
                "delay", timing 1000.0 (fun t -> t.delay <- 200.0), [ 100.0, None; 700.0, Some 0.5 ]
                "fill", timing 800.0 (fun t -> t.fill <- FillMode.Forwards), [ 1200.0, Some 1.0 ]
                "alternate",
                timing 500.0 (fun t ->
                    t.iterations <- 2.0
                    t.direction <- PlaybackDirection.Alternate),
                [ 750.0, Some 0.5 ]
            ]

        for name, value, samples in seeked do
            let effect = service.CreateCustomEffect (record name, value)
            let playing = animation.CreateAnimation effect.Effect
            playing.pause ()
            do! frames 2

            for time, expected in samples do
                seekTo playing time
                let start = log.Count
                do! frames 2
                let got = since name start

                match expected with
                | Some progress ->
                    check
                        (name
                         + " at "
                         + string time
                         + " delivers its progress")
                        (not got.IsEmpty && near (List.last got) progress)
                | None ->
                    check
                        (name
                         + " at "
                         + string time
                         + " delivers nothing for null progress")
                        got.IsEmpty

            playing.cancel ()
            effect.Dispose ()

        let duration = service.CreateCustomEffectFor (record "duration", 400.0)
        let durationAnimation = animation.CreateAnimation duration.Effect
        durationAnimation.pause ()
        seekTo durationAnimation 100.0
        let start = log.Count
        do! frames 2

        check
            "CreateCustomEffectFor sets the duration"
            (not (since "duration" start).IsEmpty
             && near (List.last (since "duration" start)) 0.25)

        durationAnimation.cancel ()
        duration.Dispose ()

        // 2. DocumentTimeline.animate plays at once, with the id; a running effect's progress never decreases.
        let options = CustomAnimationOptions ()
        options.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 300.0
        options.id <- "fx"
        let start = log.Count
        let animated = service.Animate (record "animate", options)
        equal "animate: id" "fx" animated.Animation.id
        check "animate: playing" (animated.Animation.playState = AnimationPlayState.Running)
        do! frames 4
        let progress = since "animate" start
        check "animate: ticks while running" (not progress.IsEmpty)

        check
            "animate: progress within [0, 1]"
            (progress
             |> List.forall (fun v -> v >= 0.0 && v <= 1.0))

        check
            "animate: progress never decreases"
            (progress
             |> List.pairwise
             |> List.forall (fun (a, b) -> a <= b + 1e-9))

        animated.Dispose ()
        let afterDispose = log.Count
        do! frames 3
        check "animate: no tick after Dispose" (since "animate" afterDispose).IsEmpty
        animated.Animation.cancel ()

        // 3. Within a frame, ticks follow animation order.
        let ordered =
            [ "A"; "B"; "C" ]
            |> List.map (fun label ->
                let effect = service.CreateCustomEffect (record label, timing 1000.0 ignore)
                let playing = animation.CreateAnimation effect.Effect
                playing.pause ()
                effect, playing)

        do! frames 2

        for _, playing in ordered do
            seekTo playing 100.0

        let start = log.Count
        do! frames 1

        let labels =
            [ for index in start .. log.Count - 1 -> fst log[index] ]
            |> List.filter (fun label -> label = "A" || label = "B" || label = "C")

        check
            "ticks follow animation order"
            (not labels.IsEmpty
             && labels
                |> List.chunkBySize 3
                |> List.forall (fun chunk -> chunk = [ "A"; "B"; "C" ]))

        for effect, playing in ordered do
            playing.cancel ()
            effect.Dispose ()

        // 4. A tick writes the DOM; the write is visible after the next frame.
        let target = document.createElement "div" :?> HTMLElement
        document.body.appendChild target |> ignore

        let writer =
            service.CreateCustomEffect ((fun progress -> target.style.setProperty ("opacity", string progress)), timing 1000.0 ignore)

        let writing = animation.CreateAnimation writer.Effect
        writing.pause ()
        seekTo writing 400.0
        do! frames 3
        equal "a tick writes the DOM" "0.4" ((computed.Get (target, None)).getPropertyValue "opacity")
        writing.cancel ()
        writer.Dispose ()

        // 5. A tick disposes its own effect: it never ticks again.
        let mutable self: CustomEffectRegistration = Unchecked.defaultof<_>
        let mutable selfTicks = 0

        self <-
            service.CreateCustomEffect (
                (fun _ ->
                    selfTicks <- selfTicks + 1
                    self.Dispose ()),
                timing 1000.0 ignore
            )

        let selfAnimation = animation.CreateAnimation self.Effect
        selfAnimation.pause ()
        seekTo selfAnimation 100.0
        do! frames 2
        seekTo selfAnimation 200.0
        do! frames 2
        equal "a tick that disposes its effect ticks once" 1 selfTicks
        selfAnimation.cancel ()

        // 6. A tick exception is routed; later ticks of other effects still run.
        let routed = driver.RoutedErrors ()

        let thrower =
            service.CreateCustomEffect ((fun _ -> failwith "tick failure"), timing 1000.0 ignore)

        let survivor = service.CreateCustomEffect (record "survivor", timing 1000.0 ignore)
        let throwing = animation.CreateAnimation thrower.Effect
        let surviving = animation.CreateAnimation survivor.Effect
        throwing.pause ()
        surviving.pause ()
        seekTo throwing 100.0
        seekTo surviving 100.0
        let start = log.Count
        do! frames 2
        check "a tick exception is routed to error reporting" (driver.RoutedErrors () > routed)
        check "a later effect still ticks after a tick exception" (not (since "survivor" start).IsEmpty)
        thrower.Dispose ()
        throwing.cancel ()
        surviving.cancel ()
        survivor.Dispose ()

        // 7. Invalid timing raises TypeError.
        let invalid label (configure: EffectTiming -> unit) =
            let name =
                try
                    service.CreateCustomEffect (ignore, timing 1000.0 configure)
                    |> ignore

                    "none"
                with error ->
                    errorName error

            equal label "TypeError" name

        invalid "negative duration" (fun t -> t.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble -1.0)
        invalid "invalid easing" (fun t -> t.easing <- "nope")
        invalid "negative iterations" (fun t -> t.iterations <- -1.0)

        // 8. Disposing the service stops every effect it created.
        let last = service.CreateCustomEffect (record "last", timing 1000.0 ignore)
        let lastAnimation = animation.CreateAnimation last.Effect
        lastAnimation.pause ()
        service.Dispose ()
        seekTo lastAnimation 300.0
        let start = log.Count
        do! frames 3
        check "no tick after the service is disposed" (since "last" start).IsEmpty
        lastAnimation.cancel ()
        target.remove ()
        return checks
    }
