module internal NativeApiConformance.ViewportChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>Host changes of the window: viewport size and device scale, page visibility and activation.</summary>
type Driver =
    {
        /// <summary>Sets the viewport to the CSS size and the device scale, then lets rendering updates run.</summary>
        Resize: int -> int -> float -> Async<unit>
        /// <summary>Lets rendering updates and queued tasks run.</summary>
        Settle: unit -> Async<unit>
        SetActive: bool -> Async<unit>
        /// <summary>Page visibility, where the host can change it.</summary>
        SetVisible: (bool -> Async<unit>) option
        /// <summary>The device-pixel size of a full paint.</summary>
        PaintSize: unit -> Async<int * int>
        /// <summary>True when the paint size rounds the scaled viewport, false when it truncates.</summary>
        PaintRounds: bool
        /// <summary>True when <c>outerWidth</c>/<c>outerHeight</c> equal the viewport.</summary>
        OuterIsViewport: bool
    }

let private sizes = [ 320, 240; 333, 221 ]
let private scales = [ 1.0; 1.25; 1.5; 2.0; 0.75 ]

/// <summary>Checks metrics against paint size, change delivery and order, visibility and disposal.</summary>
let run (document: Document) (create: unit -> ViewportService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

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

        document.body.setAttribute ("style", "margin:0;width:2000px;height:2000px")
        let reader = create ()

        for width, height in sizes do
            for scale in scales do
                do! driver.Resize width height scale
                let metrics = reader.Current ()
                let label = $"{width}x{height}@{scale}"
                equal (label + " inner size") (width, height) (metrics.InnerWidth, metrics.InnerHeight)
                equal (label + " devicePixelRatio") scale metrics.DevicePixelRatio

                if driver.OuterIsViewport then
                    equal (label + " outer size") (width, height) (metrics.OuterWidth, metrics.OuterHeight)

                let scaled (css: int) =
                    let value = float css * scale

                    int (
                        if driver.PaintRounds then
                            Math.Floor (value + 0.5)
                        else
                            Math.Floor value
                    )

                let! paint = driver.PaintSize ()
                equal (label + " paint size") (scaled width, scaled height) paint

        reader.Dispose ()
        do! driver.Resize 320 240 1.0
        let service = create ()
        let log = List<ViewportChange>()
        let first = service.Subscribe log.Add

        let step label (action: unit -> Async<unit>) (expected: ViewportChange list) =
            async {
                do! action ()
                equal label expected (List.ofSeq log)
                log.Clear ()
            }

        let resized width height scale =
            let metrics = service.Current ()
            equal "metrics in the delivered size" (width, height, scale) (metrics.InnerWidth, metrics.InnerHeight, metrics.DevicePixelRatio)
            ViewportChange.Resized metrics

        do! driver.Resize 400 300 1.0
        equal "resize" [ resized 400 300 1.0 ] (List.ofSeq log)
        log.Clear ()

        do!
            step
                "scroll then scrollend"
                (fun () ->
                    document.scrollingElement.scrollTop <- 100
                    driver.Settle ())
                [ ViewportChange.Scrolled; ViewportChange.ScrollEnded ]

        do! driver.Resize 401 301 1.5
        equal "resize then scale" [ resized 401 301 1.5; ViewportChange.ScaleChanged 1.5 ] (List.ofSeq log)
        log.Clear ()
        do! step "blur" (fun () -> driver.SetActive false) [ ViewportChange.Activation false ]
        do! step "focus" (fun () -> driver.SetActive true) [ ViewportChange.Activation true ]

        match driver.SetVisible with
        | Some setVisible ->
            equal "visible" false (service.Hidden ())
            do! step "hide" (fun () -> setVisible false) [ ViewportChange.VisibilityChanged true ]
            equal "hidden" true (service.Hidden ())
            do! step "resize while hidden" (fun () -> driver.Resize 300 200 1.5) []
            do! setVisible true
            equal "show, then the deferred resize" [ ViewportChange.VisibilityChanged false; resized 300 200 1.5 ] (List.ofSeq log)
            log.Clear ()
            equal "visible again" false (service.Hidden ())
        | None -> ()

        let second = List<ViewportChange>()
        let other = service.Subscribe second.Add
        first.Dispose ()
        first.Dispose ()
        do! driver.Resize 320 240 1.5
        equal "removed subscriber" 0 log.Count
        equal "remaining subscriber" [ resized 320 240 1.5 ] (List.ofSeq second)
        second.Clear ()
        service.Dispose ()
        service.Dispose ()
        do! driver.Resize 330 250 1.0
        equal "nothing after dispose" 0 second.Count
        raises "Current after dispose" (fun () -> service.Current () |> ignore)
        raises "Subscribe after dispose" (fun () -> service.Subscribe ignore |> ignore)
        other.Dispose ()
        let extra = create ()
        raises "null observer" (fun () -> extra.Subscribe (Unchecked.defaultof<_>) |> ignore)
        extra.Dispose ()
        do! driver.Resize 320 240 1.0
        return checks
    }
