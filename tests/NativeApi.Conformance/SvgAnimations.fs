module internal NativeApiConformance.SvgAnimations

open System
open Ranvier.Dom

/// <summary>Checks SMIL targets, timeline pause and seek, begin/end controls, finite offsets and running events.</summary>
/// <param name="wait">Waits the given milliseconds while the provider's animation clock runs.</param>
/// <param name="frozenRestartsOnSeek">True when a new begin on a frozen animation takes effect during a paused seek.</param>
let run (document: Document) (wait: int -> Async<unit>) (errorName: exn -> string) (frozenRestartsOnSeek: bool) =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwithf "%s: expected %A, got %A" label expected actual

        let near label (tolerance: float) (expected: float) (actual: float) =
            checks <- checks + 1

            if
                Double.IsNaN actual
                || Double.IsInfinity actual
                || abs (expected - actual) > tolerance
            then
                failwithf "%s: expected %g, got %g" label expected actual

        let failure label expected action =
            let outcome =
                try
                    action ()
                    "accepted"
                with error ->
                    errorName error

            equal label expected outcome

        let host = document.createElement "div"

        host.innerHTML <-
            "<svg id='smil-root' width='200' height='200'>"
            + "<rect id='smil-seek' width='10' height='10'><animate id='smil-width' attributeName='width' from='10' to='110' dur='10s' begin='indefinite' fill='freeze'/></rect>"
            + "<rect id='smil-shift' width='10' height='10'><animate id='smil-delay' attributeName='x' from='0' to='100' dur='10s' begin='indefinite' fill='freeze'/></rect>"
            + "<rect id='smil-run' width='10' height='10'><animate id='smil-events' attributeName='height' from='10' to='20' dur='0.1s' repeatCount='2' begin='indefinite'/></rect>"
            + "</svg>"

        document.body.appendChild host |> ignore

        try
            let root = document.getElementById "smil-root" :?> SVGSVGElement
            let target = document.getElementById "smil-seek" :?> SVGRectElement
            let animation = document.getElementById "smil-width" :?> SVGAnimationElement
            let running = document.getElementById "smil-run" :?> SVGRectElement
            let events = document.getElementById "smil-events" :?> SVGAnimationElement
            equal "target element identity" true (obj.ReferenceEquals (animation.targetElement, target))
            equal "target element stable" true (obj.ReferenceEquals (animation.targetElement, animation.targetElement))
            equal "timeline initially running" false (root.animationsPaused ())
            near "simple duration" 0.0001 10. (float (animation.getSimpleDuration ()))
            failure "start time before begin" "InvalidStateError" (fun () -> animation.getStartTime () |> ignore)
            root.pauseAnimations ()
            equal "paused timeline" true (root.animationsPaused ())
            animation.beginElement ()
            let start = float (animation.getStartTime ())
            near "begin starts at paused document time" 0.05 0. start
            root.setCurrentTime 5.f
            near "paused seek document time" 0.0001 5. (float (root.getCurrentTime ()))
            near "paused seek element time" 0.0001 5. (float (animation.getCurrentTime ()))
            let expected = 10. + 10. * (5. - start)
            near "seeked animation value" 0.001 expected (float target.width.animVal.value)
            near "seek preserves base value" 0.0001 10. (float target.width.baseVal.value)
            near "seeked rendered geometry" 0.001 expected (float (target.getBBox ()).width)
            animation.endElement ()
            near "end freezes value" 0.001 expected (float target.width.animVal.value)

            for label, action in
                [
                    "beginElementAt NaN", (fun () -> animation.beginElementAt nanf)
                    "beginElementAt infinity", (fun () -> animation.beginElementAt infinityf)
                    "endElementAt NaN", (fun () -> animation.endElementAt nanf)
                    "setCurrentTime NaN", (fun () -> root.setCurrentTime nanf)
                ] do
                failure (label + " rejects") "TypeError" action

            near "rejected offsets preserve document time" 0.0001 5. (float (root.getCurrentTime ()))
            root.setCurrentTime 6.f
            animation.beginElementAt 1.f
            root.setCurrentTime 8.f

            if frozenRestartsOnSeek then
                near "frozen animation restarts at its new begin" 0.001 20. (float target.width.animVal.value)
            else
                near "frozen animation stays frozen across the seek" 0.001 expected (float target.width.animVal.value)

            let delayed = document.getElementById "smil-delay" :?> SVGAnimationElement
            let shifted = document.getElementById "smil-shift" :?> SVGRectElement
            delayed.beginElementAt 1.f
            root.setCurrentTime 8.5f
            near "delayed begin has not started" 0.0001 0. (float shifted.x.animVal.value)
            root.setCurrentTime 9.5f
            near "delayed begin start time" 0.0001 9. (float (delayed.getStartTime ()))
            near "delayed begin animation value" 0.001 5. (float shifted.x.animVal.value)
            near "delayed begin rendered geometry" 0.001 5. (float (shifted.getBBox ()).x)
            delayed.endElementAt 1.f
            root.setCurrentTime 11.f
            near "delayed end freezes at end time" 0.001 15. (float shifted.x.animVal.value)
            root.unpauseAnimations ()
            equal "resumed timeline" false (root.animationsPaused ())
            let resumed = float (root.getCurrentTime ())
            let observed = ResizeArray<string>()

            for name in [ "beginEvent"; "repeatEvent"; "endEvent" ] do
                events.addEventListener (name, (fun (_: Event) -> observed.Add name))

            events.beginElement ()
            let mutable attempts = 0

            while not (observed.Contains "endEvent")
                  && attempts < 200 do
                do! wait 10
                attempts <- attempts + 1

            equal "running begin/repeat/end events" [ "beginEvent"; "repeatEvent"; "endEvent" ] (List.ofSeq observed)
            equal "document time advances while running" true (float (root.getCurrentTime ()) > resumed)
            near "ended animation restores base height" 0.0001 10. (float running.height.animVal.value)

            let detached =
                document.createElementNS ("http://www.w3.org/2000/svg", "animate") :?> SVGAnimationElement

            equal "detached animation has no target" true (isNull (box detached.targetElement))
            failure "detached simple duration" "NotSupportedError" (fun () -> detached.getSimpleDuration () |> ignore)
            failure "detached start time" "InvalidStateError" (fun () -> detached.getStartTime () |> ignore)
            return checks
        finally
            document.body.removeChild host |> ignore
    }
