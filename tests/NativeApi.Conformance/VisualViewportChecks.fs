module internal NativeApiConformance.VisualViewportChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>The engine whose measured differences a step expects.</summary>
type Engine =
    | WebKit
    | Chromium

/// <summary>Host changes of the window. Each lets rendering updates and queued tasks run before it returns.</summary>
type Driver =
    {
        Resize: int -> int -> Async<unit>
        SetDeviceScale: float -> Async<unit>
        /// <summary>
        /// Sets the page scale with the scaled document's scroll position as the origin: through the service where the
        /// host owns the input, otherwise through the browser's emulation.
        /// </summary>
        SetPageScale: VisualViewportService -> float -> int -> int -> Async<unit>
        /// <summary>A wheel at the viewport point, by CSS pixel deltas.</summary>
        Wheel: int -> int -> float -> float -> Async<unit>
        Settle: unit -> Async<unit>
        Engine: Engine
        /// <summary>True when the host sets the obscured insets.</summary>
        HostInsets: bool
    }

type private Step =
    {
        Name: string
        Action: VisualViewportService -> Async<unit>
        /// <summary>offsetLeft, offsetTop, pageLeft, pageTop, width, height, scale.</summary>
        Expected: float array
        /// <summary>The kinds of the delivered changes: "resize" or "scroll".</summary>
        Changes: string list
    }

/// <summary>
/// Replays the Ranvier.WebCore oracle steps of tests/fixtures/visual-viewport-oracle.json, then checks argument
/// validation, the obscured insets, subscription removal and disposal.
/// </summary>
/// <remarks>
/// WebKit keeps the layout viewport at the document's origin while page-scaled, so its offsets equal the page
/// position, and its window scroll moves the visual viewport. Sizes compare within one LayoutUnit (1/64 px).
/// </remarks>
let run (document: Document) (scrollTo: float -> float -> unit) (create: unit -> VisualViewportService) (driver: Driver) =
    async {
        let mutable checks = 0

        let check label condition =
            checks <- checks + 1

            if not condition then
                failwith label

        let equal label (expected: 'T) (actual: 'T) =
            check $"{label}: expected {expected}, got {actual}" (expected = actual)

        let raisesWith (expected: exn -> bool) (name: string) label (action: unit -> unit) =
            let raised =
                try
                    action ()
                    false
                with error ->
                    expected error

            check (label + ": expected " + name) raised

        // Fable cannot type-test .NET exception classes, so the shared checks accept any exception.
        let outOfRange label action =
            raisesWith (fun _ -> true) "an argument error" label action

        let disposed label action =
            raisesWith (fun _ -> true) "a disposal error" label action

        let values (state: VisualViewportState) =
            [|
                state.OffsetLeft
                state.OffsetTop
                state.PageLeft
                state.PageTop
                state.Width
                state.Height
                state.Scale
            |]

        let v (items: int list) =
            items |> List.map float |> Array.ofList

        let finite (value: float) =
            not (Double.IsNaN value)
            && not (Double.IsInfinity value)

        let near (expected: float array) (actual: float array) =
            Array.forall2 (fun (e: float) (a: float) -> Math.Abs (e - a) < 1.0 / 64.0) expected actual

        let current (service: VisualViewportService) =
            match service.Current () with
            | Ok state -> state
            | Error reason -> failwith $"VisualViewport read failed: {reason}"

        document.body.setAttribute ("style", "margin:0;width:2000px;height:3000px")
        do! driver.Resize 320 240
        do! driver.SetDeviceScale 1.0
        let service = create ()
        let log = List<string * VisualViewportState>()

        let subscription =
            service.Subscribe (fun change ->
                match change with
                | VisualViewportChange.Resized state -> log.Add (("resize", state))
                | VisualViewportChange.Scrolled state -> log.Add (("scroll", state)))

        let webKit = driver.Engine = WebKit

        let scrolled =
            fun x y (_: VisualViewportService) ->
                async {
                    scrollTo x y
                    do! driver.Settle ()
                }

        let steps =
            [
                {
                    Name = "initial"
                    Action = (fun _ -> driver.Settle ())
                    Expected = v [ 0; 0; 0; 0; 320; 240; 1 ]
                    Changes = []
                }
                {
                    Name = "scrollTo(10, 20)"
                    Action = scrolled 10.0 20.0
                    Expected = v [ 0; 0; 10; 20; 320; 240; 1 ]
                    Changes = []
                }
                {
                    Name = "scrollTo(0, 0)"
                    Action = scrolled 0.0 0.0
                    Expected = v [ 0; 0; 0; 0; 320; 240; 1 ]
                    Changes = []
                }
                {
                    Name = "resize 300x200"
                    Action = (fun _ -> driver.Resize 300 200)
                    Expected = v [ 0; 0; 0; 0; 300; 200; 1 ]
                    Changes = [ "resize" ]
                }
                {
                    Name = "device scale 2"
                    Action = (fun _ -> driver.SetDeviceScale 2.0)
                    Expected = v [ 0; 0; 0; 0; 300; 200; 1 ]
                    Changes = []
                }
                {
                    Name = "device scale 1"
                    Action = (fun _ -> driver.SetDeviceScale 1.0)
                    Expected = v [ 0; 0; 0; 0; 300; 200; 1 ]
                    Changes = []
                }
                {
                    Name = "page scale 2 at 0,0"
                    Action = (fun s -> driver.SetPageScale s 2.0 0 0)
                    Expected = v [ 0; 0; 0; 0; 150; 100; 2 ]
                    Changes = [ "resize" ]
                }
                {
                    Name = "page scale 1 at 0,0"
                    Action = (fun s -> driver.SetPageScale s 1.0 0 0)
                    Expected = v [ 0; 0; 0; 0; 300; 200; 1 ]
                    Changes = [ "resize" ]
                }
                {
                    Name = "page scale 2 at 0,0 again"
                    Action = (fun s -> driver.SetPageScale s 2.0 0 0)
                    Expected = v [ 0; 0; 0; 0; 150; 100; 2 ]
                    Changes = [ "resize" ]
                }
                {
                    Name = "scrollTo(100, 50) at page scale 2"
                    Action = scrolled 100.0 50.0
                    Expected =
                        (if webKit then
                             v [ 100; 50; 100; 50; 150; 100; 2 ]
                         else
                             v [ 0; 0; 100; 50; 150; 100; 2 ])
                    Changes = (if webKit then [ "scroll" ] else [])
                }
                {
                    Name = "wheel 30, 40 at 150, 100 at page scale 2"
                    Action = (fun _ -> driver.Wheel 150 100 30.0 40.0)
                    Expected =
                        (if webKit then
                             v [ 115; 70; 115; 70; 150; 100; 2 ]
                         else
                             v [ 15; 20; 115; 70; 150; 100; 2 ])
                    Changes = [ "scroll" ]
                }
                {
                    Name = "page scale 1 at 100,50"
                    Action = (fun s -> driver.SetPageScale s 1.0 100 50)
                    Expected = v [ 0; 0; 100; 50; 300; 200; 1 ]
                    Changes = [ "resize"; "scroll" ]
                }
                {
                    Name = "scrollTo(0, 0) at page scale 1"
                    Action = scrolled 0.0 0.0
                    Expected = v [ 0; 0; 0; 0; 300; 200; 1 ]
                    Changes = []
                }
                {
                    Name = "resize 320x240"
                    Action = (fun _ -> driver.Resize 320 240)
                    Expected = v [ 0; 0; 0; 0; 320; 240; 1 ]
                    Changes = [ "resize" ]
                }
            ]

        for step in steps do
            do! driver.Settle ()
            log.Clear ()
            do! step.Action service
            let state = values (current service)
            check $"{step.Name}: expected {step.Expected}, got {state}" (near step.Expected state)
            equal (step.Name + " changes") step.Changes (log |> Seq.map fst |> Seq.distinct |> List.ofSeq)

            // Each change carries the members read when it fired.
            check
                $"{step.Name} delivers live members"
                (log
                 |> Seq.forall (fun (_, delivered) ->
                     Array.forall finite (values delivered)
                     && delivered.Scale > 0.0))

        // Arguments: Core rejects them before the provider runs, in every engine.
        for scale in [ 0.0; -1.0; nan; infinity; -infinity ] do
            outOfRange $"page scale {scale}" (fun () -> service.SetPageScale (scale, 0, 0) |> ignore)

        for insets in
            [
                {
                    Top = -1.0
                    Right = 0.0
                    Bottom = 0.0
                    Left = 0.0
                }
                {
                    Top = 0.0
                    Right = nan
                    Bottom = 0.0
                    Left = 0.0
                }
                {
                    Top = 0.0
                    Right = 0.0
                    Bottom = infinity
                    Left = 0.0
                }
            ] do
            outOfRange $"insets {insets}" (fun () -> service.SetObscuredInsets insets |> ignore)

        // The obscured insets are a host input: the host shrinks the visual viewport by the covered edges, and a page
        // cannot set them.
        let covered =
            {
                Top = 20.0
                Right = 0.0
                Bottom = 40.0
                Left = 0.0
            }

        do! driver.Settle ()
        log.Clear ()

        match service.SetObscuredInsets covered with
        | Ok () ->
            check "the host owns the insets" driver.HostInsets
            do! driver.Settle ()
            check "insets 20,0,40,0" (near (v [ 0; 0; 0; 0; 320; 180; 1 ]) (values (current service)))
            equal "insets fire one resize" [ "resize" ] (log |> Seq.map fst |> Seq.distinct |> List.ofSeq)
            log.Clear ()
            equal "insets cleared" (Ok ()) (service.SetObscuredInsets { covered with Top = 0.0; Bottom = 0.0 })
            do! driver.Settle ()
            check "insets 0" (near (v [ 0; 0; 0; 0; 320; 240; 1 ]) (values (current service)))
            equal "clearing fires one resize" [ "resize" ] (log |> Seq.map fst |> Seq.distinct |> List.ofSeq)
        | Error (VisualViewportUnavailable.Unsupported _) ->
            check "the browser reports the insets unsupported" (not driver.HostInsets)
            check "an unsupported input changes nothing" (near (v [ 0; 0; 0; 0; 320; 240; 1 ]) (values (current service)))
            check "an unsupported input fires nothing" (log.Count = 0)
            checks <- checks + 3
        | Error other -> failwith $"insets: {other}"

        match service.SetPageScale (1.5, 0, 0) with
        | Ok () ->
            check "the host owns the page scale" driver.HostInsets
            do! driver.Settle ()
            // 320 / 1.5 in LayoutUnits is 213.328125.
            check "page scale 1.5" (near [| 0.0; 0.0; 0.0; 0.0; 320.0 / 1.5; 160.0; 1.5 |] (values (current service)))
            equal "page scale reset" (Ok ()) (service.SetPageScale (1.0, 0, 0))
            do! driver.Settle ()
        | Error (VisualViewportUnavailable.Unsupported _) ->
            check "the browser reports the page scale unsupported" (not driver.HostInsets)
            checks <- checks + 2
        | Error other -> failwith $"page scale: {other}"

        // Removal and disposal.
        subscription.Dispose ()
        subscription.Dispose ()
        log.Clear ()
        do! driver.Resize 300 200
        equal "no delivery after the subscription is disposed" 0 log.Count

        let second =
            service.Subscribe (fun _ -> log.Add (("second", Unchecked.defaultof<_>)))

        do! driver.Resize 320 240
        equal "a later subscriber receives resize" 1 log.Count
        second.Dispose ()
        service.Dispose ()
        service.Dispose ()
        disposed "Current after dispose" (fun () -> service.Current () |> ignore)
        disposed "SetPageScale after dispose" (fun () -> service.SetPageScale (2.0, 0, 0) |> ignore)
        disposed "Subscribe after dispose" (fun () -> service.Subscribe ignore |> ignore)
        let other = create ()
        let otherLog = List<VisualViewportChange>()
        let third = other.Subscribe otherLog.Add
        log.Clear ()
        do! driver.Resize 300 200
        equal "a disposed service's subscribers stay silent" 0 log.Count
        equal "a new service receives resize" 1 otherLog.Count
        third.Dispose ()
        other.Dispose ()
        do! driver.Resize 320 240
        return checks
    }
