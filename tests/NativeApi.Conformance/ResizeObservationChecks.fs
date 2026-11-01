module internal NativeApiConformance.ResizeObservationChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>The engine whose resize observation the fixture expects.</summary>
type Engine =
    /// <summary>The native host: no device-pixel content box (Ranvier.WebCore#146), observers in first-observe order,
    /// and a zoomed element's contentRect origin in zoomed pixels.</summary>
    | Native
    /// <summary>Chromium: the specification's construction order and unzoomed origin.</summary>
    | Chromium

/// <summary>The host operations the fixture drives.</summary>
type Driver =
    {
        /// <summary>Runs one rendering update and the work it queues, so its notifications have been delivered.</summary>
        Update: unit -> Async<unit>
        /// <summary>The number of callback exceptions the host has reported.</summary>
        Errors: unit -> int
        Engine: Engine
    }

let private Css =
    "body{margin:0}.ro-b{box-sizing:content-box;width:100px;height:50px;padding:10px 4px 6px 2px;border:5px solid black;margin:3px}"
    + ".ro-v{writing-mode:vertical-rl}.ro-z{zoom:2}.ro-f{width:100.5px;height:33.25px}"
    + ".ro-bb{box-sizing:border-box;width:100px;height:50px;padding:10px;border:5px solid black}"

/// <summary>Labels without generic formatting, which NativeAOT cannot print.</summary>
let private show (entry: ResizeObserverEntry) =
    let size (sizes: ResizeObserverSize array) =
        String.Join (
            ";",
            sizes
            |> Array.map (fun s -> string s.InlineSize + "x" + string s.BlockSize)
        )

    let r = entry.ContentRect

    entry.Target.id
    + " rect "
    + string r.X
    + ","
    + string r.Y
    + ","
    + string r.Width
    + ","
    + string r.Height
    + " content "
    + size entry.ContentBoxSize
    + " border "
    + size entry.BorderBoxSize

/// <summary>
/// Checks box options, entry values under writing modes, zoom and fractional sizes, the initial observation of attached,
/// hidden, inline and detached targets, delivery order across observers and depths, re-observation, unobserve, disconnect,
/// disposal and abort signals, callback isolation, entry copies held past their delivery and argument validation; the
/// result is the assertion count.
/// </summary>
let run (document: Document) (create: unit -> ResizeObservationService) (cancellation: unit -> CancellationService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal (label: string) (expected: string list) (actual: string list) =
            checks <- checks + 1

            if actual <> expected then
                failwith (
                    label
                    + ": expected ["
                    + String.Join (" | ", expected)
                    + "], got ["
                    + String.Join (" | ", actual)
                    + "]"
                )

        let truth (label: string) condition =
            checks <- checks + 1

            if not condition then
                failwith label

        // Services raise ArgumentNullException, NotSupportedException and ObjectDisposedException; Fable erases the types.
        let raises (label: string) (action: unit -> unit) =
            checks <- checks + 1

            let raised =
                try
                    action ()
                    false
                with _ ->
                    true

            if not raised then
                failwith (label + ": expected an exception")

        let element (tag: string) (id: string) (className: string) =
            let value = document.createElement tag :?> HTMLElement
            value.id <- id

            if className <> "" then
                value.className <- className

            value

        let style = element "style" "ro-style" ""
        style.textContent <- Css
        let a = element "div" "ro-a" "ro-b"
        let v = element "div" "ro-v" "ro-b ro-v"
        let z = element "div" "ro-z" "ro-b ro-z"
        let f = element "div" "ro-f" "ro-b ro-f"
        let bb = element "div" "ro-bb" "ro-bb"
        let inl = element "span" "ro-inl" ""
        inl.textContent <- "inline"
        let hid = element "div" "ro-hid" ""
        hid.style.cssText <- "display:none"
        let outer = element "div" "ro-outer" ""
        outer.style.cssText <- "width:200px"
        let mid = element "div" "ro-mid" ""
        mid.style.cssText <- "width:150px"
        let deep = element "div" "ro-deep" ""
        deep.style.cssText <- "width:100px;height:10px"
        mid.appendChild deep |> ignore
        outer.appendChild mid |> ignore
        let tmp = element "div" "ro-tmp" ""
        tmp.style.cssText <- "width:40px;height:20px"

        for value in [ style; a; v; z; f; bb; inl; hid; outer ] do
            document.body.appendChild value |> ignore

        let service = create ()
        let log = List<string>()
        let entries = List<string>()

        let observer (name: string) =
            service.Create (fun delivered ->
                log.Add (
                    name
                    + ":"
                    + String.Join (",", delivered |> Array.map (fun e -> e.Target.id))
                )

                for entry in delivered do
                    entries.Add (show entry))

        let update () =
            async {
                do! driver.Update ()
                log.Add "|"
            }

        let take () =
            let delivered = List.ofSeq log, List.ofSeq entries
            log.Clear ()
            entries.Clear ()
            delivered

        try
            truth "content-box supported" (service.Supports ResizeObserverBox.ContentBox)
            truth "border-box supported" (service.Supports ResizeObserverBox.BorderBox)

            truth
                "device-pixel-content-box support follows the engine"
                (service.Supports ResizeObserverBox.DevicePixelContentBox = (driver.Engine = Chromium))

            // Each box option: one initial notification with every target, in observation order.
            let zoomOrigin = if driver.Engine = Native then "4,20" else "2,10"

            for observed, name in
                [
                    ResizeObserverBox.ContentBox, "content"
                    ResizeObserverBox.BorderBox, "border"
                ] do
                let o = observer name

                for target in [ a; v; z; f; bb; inl; hid ] do
                    o.Observe (target, observed)

                do! update ()
                do! update ()
                let delivered, values = take ()
                equal (name + "-box deliveries") [ name + ":ro-a,ro-v,ro-z,ro-f,ro-bb,ro-inl,ro-hid"; "|"; "|" ] delivered

                equal
                    (name + "-box entries")
                    [
                        "ro-a rect 2,10,100,50 content 100x50 border 116x76"
                        "ro-v rect 2,10,100,50 content 50x100 border 76x116"
                        "ro-z rect "
                        + zoomOrigin
                        + ",100,50 content 100x50 border 116x76"
                        "ro-f rect 2,10,100.5,33.25 content 100.5x33.25 border 116.5x59.25"
                        "ro-bb rect 10,10,70,20 content 70x20 border 100x50"
                        "ro-inl rect 0,0,0,0 content 0x0 border 0x0"
                        "ro-hid rect 0,0,0,0 content 0x0 border 0x0"
                    ]
                    values

                o.Dispose ()

            // The device-pixel content box: the browser reports it; the native engine rejects the option.
            let devicePixel = List<int>()

            let d =
                service.Create (fun delivered ->
                    for entry in delivered do
                        devicePixel.Add entry.DevicePixelContentBoxSize.Length)

            if driver.Engine = Chromium then
                d.Observe (a, ResizeObserverBox.DevicePixelContentBox)
                do! update ()
                truth "a device-pixel content box size per entry" (List.ofSeq devicePixel = [ 1 ])
            else
                raises "device-pixel-content-box is unsupported (Ranvier.WebCore#146)" (fun () ->
                    d.Observe (a, ResizeObserverBox.DevicePixelContentBox))

                d.Observe a
                do! update ()
                truth "no device-pixel content box size natively" (List.ofSeq devicePixel = [ 0 ])

            d.Dispose ()
            take () |> ignore

            // A padding change reaches the border-box observer alone; a width change reaches both.
            let c = observer "c"
            let b = observer "b"
            c.Observe (a, ResizeObserverBox.ContentBox)
            b.Observe (a, ResizeObserverBox.BorderBox)
            do! update ()
            a.style.setProperty ("padding", "20px")
            do! update ()
            a.style.setProperty ("width", "80px")
            do! update ()
            let delivered, values = take ()
            equal "box changes" [ "c:ro-a"; "b:ro-a"; "|"; "b:ro-a"; "|"; "c:ro-a"; "b:ro-a"; "|" ] delivered

            equal
                "box change sizes"
                [
                    "ro-a rect 2,10,100,50 content 100x50 border 116x76"
                    "ro-a rect 2,10,100,50 content 100x50 border 116x76"
                    "ro-a rect 20,20,100,50 content 100x50 border 150x100"
                    "ro-a rect 20,20,80,50 content 80x50 border 130x100"
                    "ro-a rect 20,20,80,50 content 80x50 border 130x100"
                ]
                values

            c.Dispose ()
            b.Dispose ()
            a.style.removeProperty "padding" |> ignore
            a.style.removeProperty "width" |> ignore

            // Logical sizes follow the writing mode.
            let w = observer "w"
            w.Observe (v, ResizeObserverBox.BorderBox)
            do! update ()
            v.style.setProperty ("height", "70px")
            do! update ()
            v.style.setProperty ("writing-mode", "horizontal-tb")
            do! update ()
            let delivered, values = take ()
            equal "writing mode deliveries" [ "w:ro-v"; "|"; "w:ro-v"; "|"; "w:ro-v"; "|" ] delivered

            equal
                "writing mode sizes"
                [
                    "ro-v rect 2,10,100,50 content 50x100 border 76x116"
                    "ro-v rect 2,10,100,70 content 70x100 border 96x116"
                    "ro-v rect 2,10,100,70 content 100x70 border 116x96"
                ]
                values

            w.Dispose ()
            v.style.removeProperty "height" |> ignore
            v.style.removeProperty "writing-mode" |> ignore

            // Re-observing with the same box changes nothing; another box reports again.
            let r = observer "r"
            r.Observe a
            do! update ()
            r.Observe (a, ResizeObserverBox.ContentBox)
            do! update ()
            r.Observe (a, ResizeObserverBox.BorderBox)
            do! update ()
            equal "re-observation" [ "r:ro-a"; "|"; "|"; "r:ro-a"; "|" ] (fst (take ()))
            r.Dispose ()

            // Observers created in the order of their first observation deliver in that order.
            let o2 = observer "o2"
            let o1 = observer "o1"
            o2.Observe f
            o1.Observe bb
            o1.Observe a
            do! update ()

            for target in [ a; bb; f ] do
                target.style.setProperty ("width", "120px")

            do! update ()
            equal "observer order" [ "o2:ro-f"; "o1:ro-bb,ro-a"; "|"; "o2:ro-f"; "o1:ro-bb,ro-a"; "|" ] (fst (take ()))
            o1.Dispose ()
            o2.Dispose ()

            for target in [ a; bb; f ] do
                target.style.removeProperty "width" |> ignore

            // Observers observing out of construction order: the browser keeps construction order.
            let p1 = observer "p1"
            let p2 = observer "p2"
            p2.Observe f
            p1.Observe a
            do! update ()

            let expected =
                if driver.Engine = Native then
                    [ "p2:ro-f"; "p1:ro-a"; "|" ]
                else
                    [ "p1:ro-a"; "p2:ro-f"; "|" ]

            equal "first-observe order (qualified)" expected (fst (take ()))
            p1.Dispose ()
            p2.Dispose ()

            // A deeper size change made in a callback is delivered in the same update, a shallower one in the next.
            let mutable step = 0

            let depth =
                service.Create (fun delivered ->
                    log.Add (
                        "d:"
                        + String.Join (
                            ",",
                            delivered
                            |> Array.map (fun e -> e.Target.id + "=" + string e.ContentRect.Width)
                        )
                    )

                    if
                        delivered
                        |> Array.exists (fun e -> obj.ReferenceEquals (e.Target, mid))
                    then
                        if step = 1 then
                            step <- 2
                            deep.style.setProperty ("width", "90px")
                        elif step = 3 then
                            step <- 4
                            outer.style.setProperty ("width", "190px"))

            for target in [ outer; mid; deep ] do
                depth.Observe target

            do! update ()
            step <- 1
            mid.style.setProperty ("width", "140px")
            do! update ()
            step <- 3
            mid.style.setProperty ("width", "130px")
            do! update ()
            do! update ()

            equal
                "depth order"
                [
                    "d:ro-outer=200,ro-mid=150,ro-deep=100"
                    "|"
                    "d:ro-mid=140"
                    "d:ro-deep=90"
                    "|"
                    "d:ro-mid=130"
                    "|"
                    "d:ro-outer=190"
                    "|"
                ]
                (fst (take ()))

            depth.Dispose ()

            // Unobserve and disconnect drop pending notifications; a disconnected observer observes again.
            let s = observer "s"
            s.Observe a
            s.Observe bb
            do! update ()
            a.style.setProperty ("width", "111px")
            s.Unobserve a
            do! update ()
            bb.style.setProperty ("width", "111px")
            s.Disconnect ()
            do! update ()
            s.Observe bb
            do! update ()
            s.Unobserve inl
            s.Disconnect ()
            s.Disconnect ()
            do! update ()
            equal "unobserve and disconnect" [ "s:ro-a,ro-bb"; "|"; "|"; "|"; "s:ro-bb"; "|"; "|" ] (fst (take ()))
            s.Dispose ()
            s.Dispose ()
            raises "Observe after Dispose" (fun () -> s.Observe a)
            raises "Disconnect after Dispose" (fun () -> s.Disconnect ())
            a.style.removeProperty "width" |> ignore
            bb.style.removeProperty "width" |> ignore

            // A detached target reports 0 by 0, then its size once attached, and 0 by 0 when removed or hidden.
            let l = observer "l"
            l.Observe (tmp, ResizeObserverBox.BorderBox)
            do! update ()
            document.body.appendChild tmp |> ignore
            do! update ()
            tmp.remove ()
            do! update ()
            document.body.appendChild tmp |> ignore
            do! update ()
            tmp.style.setProperty ("display", "none")
            do! update ()
            let delivered, values = take ()

            equal
                "lifecycle deliveries"
                [
                    "l:ro-tmp"
                    "|"
                    "l:ro-tmp"
                    "|"
                    "l:ro-tmp"
                    "|"
                    "l:ro-tmp"
                    "|"
                    "l:ro-tmp"
                    "|"
                ]
                delivered

            equal
                "lifecycle sizes"
                [
                    "ro-tmp rect 0,0,0,0 content 0x0 border 0x0"
                    "ro-tmp rect 0,0,40,20 content 40x20 border 40x20"
                    "ro-tmp rect 0,0,0,0 content 0x0 border 0x0"
                    "ro-tmp rect 0,0,40,20 content 40x20 border 40x20"
                    "ro-tmp rect 0,0,0,0 content 0x0 border 0x0"
                ]
                values

            l.Dispose ()
            tmp.remove ()

            // A throwing callback is reported and leaves the next observer delivering.
            let errors = driver.Errors ()
            let thrower = service.Create (fun _ -> raise (InvalidOperationException "boom"))
            let after = observer "after"
            thrower.Observe a
            after.Observe a
            do! update ()
            equal "a throwing callback leaves others delivering" [ "after:ro-a"; "|" ] (fst (take ()))
            truth "the throwing callback is reported once" (driver.Errors () = errors + 1)
            thrower.Dispose ()
            after.Dispose ()

            // Entries are copies: one held past its delivery keeps its values and target.
            let mutable held: ResizeObserverEntry array = [||]

            let keep =
                service.Create (fun delivered ->
                    if held.Length = 0 then
                        held <- delivered)

            keep.Observe bb
            do! update ()
            bb.style.setProperty ("width", "160px")
            do! update ()
            keep.Dispose ()
            do! update ()
            equal "a held entry keeps its values" [ "ro-bb rect 10,10,70,20 content 70x20 border 100x50" ] (held |> Array.map show |> List.ofArray)
            truth "a held entry keeps its target" (obj.ReferenceEquals (held[0].Target, bb))
            bb.style.removeProperty "width" |> ignore
            take () |> ignore

            // An abort signal disposes its observer; an aborted one returns a disposed observer.
            use signals = cancellation ()
            let controller = signals.CreateController ()
            let aborting = service.Create ((fun _ -> log.Add "aborting"), controller.Signal)
            aborting.Observe a
            do! update ()
            controller.Abort ()
            a.style.setProperty ("width", "99px")
            do! update ()
            equal "abort stops delivery" [ "aborting"; "|"; "|" ] (fst (take ()))
            raises "Observe after abort" (fun () -> aborting.Observe a)

            let late =
                service.Create ((fun _ -> log.Add "late"), signals.Abort AbortReason.Abort)

            raises "Observe with an aborted signal" (fun () -> late.Observe a)
            a.style.removeProperty "width" |> ignore

            // Argument validation.
            let checkedObserver = observer "x"
            raises "null target" (fun () -> checkedObserver.Observe (Unchecked.defaultof<Element>))
            raises "null unobserve target" (fun () -> checkedObserver.Unobserve (Unchecked.defaultof<Element>))

            raises "null callback" (fun () ->
                service.Create (Unchecked.defaultof<ResizeObserverEntry array -> unit>)
                |> ignore)

            // Disposing the service disposes its observers.
            checkedObserver.Observe a
            service.Dispose ()
            do! update ()
            equal "service disposal stops delivery" [ "|" ] (fst (take ()))
            raises "Observe after service disposal" (fun () -> checkedObserver.Observe a)
            raises "Create after service disposal" (fun () -> service.Create ignore |> ignore)
        finally
            service.Dispose ()

            for value in [ style; a; v; z; f; bb; inl; hid; outer ] do
                value.remove ()

        return checks
    }
