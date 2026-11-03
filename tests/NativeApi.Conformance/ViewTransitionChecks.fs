module internal NativeApiConformance.ViewTransitionChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>Exercises update, promise, interruption, type-set and callback lifetime behavior against a real engine.</summary>
let run (document: Document) (create: unit -> ViewTransitionService) (nextFrame: unit -> Async<unit>) =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if actual <> expected then
#if FABLE_COMPILER
                failwithf "%s: expected %A, got %A" label expected actual
#else
                if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
                    failwithf "%s: expected %A, got %A" label expected actual
                else
                    failwith label
#endif

        let frames count =
            async {
                for _ in 1..count do
                    do! nextFrame ()
            }

        let wait (events: List<ViewTransitionSettlement>) =
            async {
                let mutable remaining = 240

                while events.Count < 3 && remaining > 0 do
                    do! nextFrame ()
                    remaining <- remaining - 1

                equal "all transition promises settled" 3 events.Count
            }

        let outcome kind (events: List<ViewTransitionSettlement>) =
            events
            |> Seq.find (fun event -> event.Promise = kind)
            |> _.ErrorName

        use service = create ()
        let target = document.createElement "div"
        document.body.appendChild target |> ignore
        target.setAttribute ("data-phase", "old")
        let mutable updates = 0

        use transition =
            service.Start (
                (fun () ->
                    updates <- updates + 1
                    target.setAttribute ("data-phase", "new")),
                [| "slide"; "slide" |]
            )

        equal "update deferred from start" 0 updates
        equal "initial types deduplicate" 1u transition.Types.Count
        equal "initial type present" true (transition.Types.Has "slide")
        let snapshot = transition.Types.Values ()
        transition.Types.Add "fade"
        transition.Types.Add "slide"
        equal "insertion order and duplicate stability" [| "slide"; "fade" |] (transition.Types.Values ())
        equal "snapshot survives mutation" [| "slide" |] snapshot
        equal "delete finds present type" true (transition.Types.Delete "fade")
        equal "delete reports absence" false (transition.Types.Delete "fade")
        transition.Types.Clear ()
        equal "clear empties types" 0u transition.Types.Count
        let events = List<ViewTransitionSettlement>()
        use observer = transition.Observe events.Add
        equal "settlement observer deferred" 0 events.Count
        do! wait events
        equal "update called once" 1 updates
        equal "update writes the new UI" "new" (target.getAttribute "data-phase")

        equal
            "success promise order"
            [
                ViewTransitionPromise.UpdateCallbackDone
                ViewTransitionPromise.Ready
                ViewTransitionPromise.Finished
            ]
            (events |> Seq.map _.Promise |> Seq.toList)

        equal
            "successful promises fulfill"
            true
            (events
             |> Seq.forall (fun event -> event.ErrorName.IsNone))

        equal "finish clears active transition" true (isNull document.activeViewTransition)
        equal "finish clears active pseudo-state" false (document.documentElement.matches ":active-view-transition")

        let directOptions = StartViewTransitionOptions ()
        let mutable directUpdates = 0
        directOptions.update <- Some (fun () -> directUpdates <- directUpdates + 1)
        let direct = document.startViewTransition directOptions
        do! frames 40
        equal "generated document callback runs once" 1 directUpdates
        equal "generated document transition completes" true (isNull document.activeViewTransition)
        direct.skipTransition ()

        let mutable skippedUpdate = 0
        use skipped = service.Start (fun () -> skippedUpdate <- skippedUpdate + 1)
        let skippedEvents = List<ViewTransitionSettlement>()
        use skippedObserver = skipped.Observe skippedEvents.Add
        skipped.Transition.skipTransition ()
        do! wait skippedEvents
        equal "skip still runs update once" 1 skippedUpdate
        equal "skip rejects ready" (Some "AbortError") (outcome ViewTransitionPromise.Ready skippedEvents)
        equal "skip update succeeds" None (outcome ViewTransitionPromise.UpdateCallbackDone skippedEvents)
        equal "skip finished succeeds" None (outcome ViewTransitionPromise.Finished skippedEvents)

        use rejected = service.Start (fun () -> failwith "expected update rejection")
        let rejectedEvents = List<ViewTransitionSettlement>()
        use rejectedObserver = rejected.Observe rejectedEvents.Add
        do! wait rejectedEvents

        equal
            "update rejection reaches every promise"
            true
            (rejectedEvents
             |> Seq.forall (fun event -> event.ErrorName = Some "TypeError"))

        use interrupted = service.Start ()
        let firstEvents = List<ViewTransitionSettlement>()
        use firstObserver = interrupted.Observe firstEvents.Add
        use following = service.Start ()
        let nextEvents = List<ViewTransitionSettlement>()
        use nextObserver = following.Observe nextEvents.Add
        do! wait firstEvents
        do! wait nextEvents
        equal "interruption rejects first ready" (Some "AbortError") (outcome ViewTransitionPromise.Ready firstEvents)

        equal
            "replacement transition succeeds"
            true
            (nextEvents
             |> Seq.forall (fun event -> event.ErrorName.IsNone))

        use quiet = service.Start ()
        let quietEvents = List<ViewTransitionSettlement>()
        let quietObserver = quiet.Observe quietEvents.Add
        quietObserver.Dispose ()
        do! frames 40
        equal "observer disposal stops queued delivery" 0 quietEvents.Count

        use self = service.Start ()
        let mutable selfObserver: IDisposable = Unchecked.defaultof<_>
        let mutable delivered = 0

        selfObserver <-
            self.Observe (fun _ ->
                delivered <- delivered + 1
                selfObserver.Dispose ())

        do! frames 40
        equal "observer may dispose itself" 1 delivered

        let mutable revokedUpdates = 0
        let revoked = service.Start (fun () -> revokedUpdates <- revokedUpdates + 1)
        let revokedEvents = List<ViewTransitionSettlement>()
        let revokedObserver = revoked.Observe revokedEvents.Add
        revoked.Dispose ()
        do! frames 40
        equal "registration disposal revokes update" 0 revokedUpdates
        equal "registration disposal revokes observations" 0 revokedEvents.Count
        revokedObserver.Dispose ()

        let mutable lastUpdates = 0
        let last = service.Start (fun () -> lastUpdates <- lastUpdates + 1)
        let lastEvents = List<ViewTransitionSettlement>()
        let lastObserver = last.Observe lastEvents.Add
        service.Dispose ()
        do! frames 40
        equal "service disposal revokes updates" 0 lastUpdates
        equal "service disposal revokes observations" 0 lastEvents.Count
        lastObserver.Dispose ()
        last.Dispose ()
        target.remove ()
        return checks
    }
