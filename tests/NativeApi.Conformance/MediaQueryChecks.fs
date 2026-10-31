module internal NativeApiConformance.MediaQueryChecks

open System
open System.Collections.Generic
open Ranvier
open Ranvier.Dom

/// <summary>A step of Ranvier.WebCore tests/fixtures/media-queries.json: viewport, scale, preferences and matches.</summary>
type Step =
    {
        Name: string
        Width: int
        Height: int
        Scale: float
        Preferences: MediaPreferences
        Matches: string list
        /// <summary>False for a step Chromium cannot emulate: it always reports a pointing device.</summary>
        Chromium: bool
    }

/// <summary>How an engine applies a step and lets its rendering update run.</summary>
type Driver =
    {
        /// <summary>Sets the step's viewport, scale and preferences: natively through the host, in Chromium through CDP.</summary>
        Apply: Step -> Async<unit>
        Settle: unit -> Async<unit>
        /// <summary>The native host: runs the native-only steps and the supply checks.</summary>
        Native: bool
    }

let queries =
    [
        "(min-width: 300px)"
        "(max-width: 250px)"
        "(orientation: portrait)"
        "(resolution: 2dppx)"
        "(pointer: fine)"
        "(pointer: coarse)"
        "(pointer: none)"
        "(hover: hover)"
        "(any-pointer: fine)"
        "(any-pointer: coarse)"
        "(any-hover: hover)"
        "(prefers-color-scheme: dark)"
        "(prefers-reduced-motion: reduce)"
        "(prefers-contrast: more)"
        "(prefers-contrast: no-preference)"
        "(forced-colors: none)"
        "(color)"
        "screen and (min-width: 300px) and (prefers-reduced-motion: no-preference)"
    ]

let private desktop =
    { MediaPreferences.Default with
        Pointer = Some PointerAccuracy.Fine
        Hover = Some true
        AnyPointer = Some PointerAccuracy.Fine
        AnyHover = Some true
    }

let private touch =
    { MediaPreferences.Default with
        Pointer = Some PointerAccuracy.Coarse
        Hover = Some false
        AnyPointer = Some PointerAccuracy.Coarse
        AnyHover = Some false
    }

let private styled scheme motion contrast (preferences: MediaPreferences) =
    { preferences with
        ColorScheme = Some scheme
        ReducedMotion = Some motion
        Contrast = Some contrast
    }

let private light =
    styled ColorSchemePreference.Light MotionPreference.NoPreference ContrastPreference.NoPreference

let private dark =
    styled ColorSchemePreference.Dark MotionPreference.Reduce ContrastPreference.More

/// <summary>The steps of the WebCore fixture; its Chromium 156 oracle agrees on every emulated step.</summary>
let steps =
    [
        {
            Name = "engine defaults"
            Width = 320
            Height = 240
            Scale = 1.0
            Preferences = MediaPreferences.Default
            Matches =
                [
                    "(min-width: 300px)"
                    "(pointer: none)"
                    "(prefers-contrast: no-preference)"
                    "(forced-colors: none)"
                    "(color)"
                    queries[17]
                ]
            Chromium = false
        }
        {
            Name = "desktop pointer"
            Width = 320
            Height = 240
            Scale = 1.0
            Preferences = light desktop
            Matches =
                [
                    "(min-width: 300px)"
                    "(pointer: fine)"
                    "(hover: hover)"
                    "(any-pointer: fine)"
                    "(any-hover: hover)"
                    "(prefers-contrast: no-preference)"
                    "(forced-colors: none)"
                    "(color)"
                    queries[17]
                ]
            Chromium = true
        }
        {
            Name = "dark, reduced motion and more contrast"
            Width = 320
            Height = 240
            Scale = 1.0
            Preferences = dark desktop
            Matches =
                [
                    "(min-width: 300px)"
                    "(pointer: fine)"
                    "(hover: hover)"
                    "(any-pointer: fine)"
                    "(any-hover: hover)"
                    "(prefers-color-scheme: dark)"
                    "(prefers-reduced-motion: reduce)"
                    "(prefers-contrast: more)"
                    "(forced-colors: none)"
                    "(color)"
                ]
            Chromium = true
        }
        {
            Name = "narrow high-density viewport"
            Width = 200
            Height = 400
            Scale = 2.0
            Preferences = dark desktop
            Matches =
                [
                    "(max-width: 250px)"
                    "(orientation: portrait)"
                    "(resolution: 2dppx)"
                    "(pointer: fine)"
                    "(hover: hover)"
                    "(any-pointer: fine)"
                    "(any-hover: hover)"
                    "(prefers-color-scheme: dark)"
                    "(prefers-reduced-motion: reduce)"
                    "(prefers-contrast: more)"
                    "(forced-colors: none)"
                    "(color)"
                ]
            Chromium = true
        }
        {
            Name = "touch input"
            Width = 200
            Height = 400
            Scale = 2.0
            Preferences = dark touch
            Matches =
                [
                    "(max-width: 250px)"
                    "(orientation: portrait)"
                    "(resolution: 2dppx)"
                    "(pointer: coarse)"
                    "(any-pointer: coarse)"
                    "(prefers-color-scheme: dark)"
                    "(prefers-reduced-motion: reduce)"
                    "(prefers-contrast: more)"
                    "(forced-colors: none)"
                    "(color)"
                ]
            Chromium = true
        }
        {
            Name = "light and no preference again"
            Width = 320
            Height = 240
            Scale = 1.0
            Preferences = light touch
            Matches =
                [
                    "(min-width: 300px)"
                    "(pointer: coarse)"
                    "(any-pointer: coarse)"
                    "(prefers-contrast: no-preference)"
                    "(forced-colors: none)"
                    "(color)"
                    queries[17]
                ]
            Chromium = true
        }
    ]

/// <summary>
/// Checks matches, change delivery, an owned subscription, owner-scoped queries, disposal and, natively, preference
/// supply on one provider; the result is the assertion count. Change order across lists is never asserted: Chromium
/// and WebKit order them differently.
/// </summary>
let run (create: unit -> MediaQueryService) (driver: Driver) =
    async {
        use graph = new Graph ()
        use _ = graph.Activate ()
        let mutable checks = 0

        let check label condition =
            checks <- checks + 1

            if not condition then
                failwith ("G01/G15 " + label)

        let raises label (action: unit -> unit) =
            let raised =
                try
                    action ()
                    false
                with _ ->
                    true

            check label raised

        let service = create ()
        check "supply follows the engine" (service.CanSupply = driver.Native)
        let lists = queries |> List.map service.Match
        check "media text round-trips" (lists |> List.map (fun list -> list.Media) = queries)
        let changes = List<string * bool>()

        let subscriptions =
            lists
            |> List.map (fun list -> list.Subscribe (fun matches -> changes.Add ((list.Media, matches))))

        let mutable previous: Step option = None

        for step in
            steps
            |> List.filter (fun step -> driver.Native || step.Chromium) do
            do! driver.Apply step
            do! driver.Settle ()

            let matching =
                lists
                |> List.filter (fun list -> list.Matches)
                |> List.map (fun list -> list.Media)

            check
                (step.Name + ": matches")
                (matching =
                    (queries
                     |> List.filter (fun query -> List.contains query step.Matches)))

            let expected =
                match previous with
                | None -> []
                | Some before ->
                    queries
                    |> List.filter (fun query ->
                        List.contains query before.Matches
                        <> List.contains query step.Matches)

            let fired = changes |> Seq.map fst |> List.ofSeq
            check (step.Name + ": one change per changed list") (List.sort fired = List.sort expected)

            check
                (step.Name + ": change carries the new matches")
                (changes
                 |> Seq.forall (fun (media, matches) -> matches = List.contains media step.Matches))

            changes.Clear ()

            if driver.Native then
                check (step.Name + ": supplied preferences read back") (service.Supplied () = step.Preferences)

            previous <- Some step

        let last = previous.Value

        // One owned subscription: removed before a change, it receives nothing; the kept one receives the change.
        let motion = service.Match "(prefers-reduced-motion: reduce)"
        let kept = List<bool>()
        let removed = List<bool>()
        let keep = motion.Subscribe kept.Add
        let drop = motion.Subscribe removed.Add
        drop.Dispose ()
        drop.Dispose ()

        let reduce =
            { last with
                Name = "reduced motion"
                Matches =
                    "(prefers-reduced-motion: reduce)"
                    :: List.filter (fun query -> query <> queries[17]) last.Matches
                Preferences =
                    { last.Preferences with
                        ReducedMotion = Some MotionPreference.Reduce
                    }
            }

        do! driver.Apply reduce
        do! driver.Settle ()
        check "the kept subscription sees the change" (List.ofSeq kept = [ true ])
        check "a removed subscription receives no change" (removed.Count = 0)
        keep.Dispose ()

        // A query owned by a reactive scope: its signal follows the change until the owner is disposed.
        let owner, read =
            createRoot (fun owner -> owner, Ranvier.View.Dom.MediaQueries.matches service "(prefers-reduced-motion: reduce)")

        check "an owned query reads the current matches" (read ())
        do! driver.Apply last
        do! driver.Settle ()
        check "an owned query follows a change" (not (read ()))
        owner.Dispose ()
        do! driver.Apply reduce
        do! driver.Settle ()
        check "a disposed owner's query no longer changes" (not (read ()))
        check "no callback after removal" (List.ofSeq kept = [ true ] && removed.Count = 0)
        motion.Dispose ()
        raises "a disposed query rejects reads" (fun () -> motion.Matches |> ignore)
        raises "a disposed query rejects subscriptions" (fun () -> motion.Subscribe ignore |> ignore)

        // Supply: validation and readback natively; the platform's in a browser.
        if driver.Native then
            raises "CoarseAndFine is not a primary pointer" (fun () ->
                service.Supply
                    { last.Preferences with
                        Pointer = Some PointerAccuracy.CoarseAndFine
                    })

            raises "a text scale outside 0.25 to 8" (fun () ->
                service.Supply
                    { last.Preferences with
                        TextScale = Some 9.0
                    })

            check "rejected preferences apply nothing" (service.Supplied () = reduce.Preferences)

            check
                "unsupplied preferences are explicit"
                (List.contains "forced-colors" service.Unavailable
                 && List.contains "prefers-contrast: less" service.Unavailable)

            service.Supply
                { reduce.Preferences with
                    AnyPointer = Some PointerAccuracy.CoarseAndFine
                    TextScale = Some 1.5
                }

            do! driver.Settle ()

            check
                "coarse and fine devices"
                (lists[8].Matches
                 && lists[9].Matches
                 && service.Supplied().TextScale = Some 1.5)
        else
            raises "a browser supplies no preferences" (fun () -> service.Supply last.Preferences)
            raises "a browser reads no supplied preferences" (fun () -> service.Supplied () |> ignore)
            check "a browser has no unavailable preference" (List.isEmpty service.Unavailable)

        do! driver.Apply last
        do! driver.Settle ()
        changes.Clear ()

        for subscription in subscriptions do
            subscription.Dispose ()

        service.Dispose ()

        let closed (list: MediaQuery) =
            try
                list.Matches |> ignore
                false
            with _ ->
                true

        check "disposing the service disposes its queries" (lists |> List.forall closed)
        raises "a disposed service rejects Match" (fun () -> service.Match "(color)" |> ignore)
        return checks
    }
