module internal NativeApiConformance.IntersectionObservationChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>The engine whose intersection observation the fixture expects.</summary>
type Engine =
    /// <summary>The native host: root bounds grown by the scroll margin, and performance.now in whole milliseconds
    /// from the window's time origin.</summary>
    | Native
    /// <summary>Chromium: root bounds without the scroll margin.</summary>
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

/// <summary>The viewport the fixture's rectangles assume.</summary>
let Viewport = 320, 240

let private Css =
    ".io-t{position:absolute;width:50px;height:50px}"
    + "#io-far{left:10px;top:400px}"
    + "#io-s{position:absolute;left:100px;top:20px;width:200px;height:100px;overflow:auto}"
    + "#io-sp{height:500px;position:relative}#io-u{left:10px;top:150px}#io-x{left:10px;top:150px}"
    + "#io-c{position:absolute;left:10px;top:10px;width:80px;height:100px;overflow:hidden}"
    + "#io-cp{height:300px;position:relative}#io-v{left:0;top:110px}"

let private number (value: float) =
    string (Math.Round (value, 3))

let private rect (r: IntersectionObserverRect) =
    number r.X
    + ","
    + number r.Y
    + ","
    + number r.Width
    + ","
    + number r.Height

/// <summary>Labels without generic formatting, which NativeAOT cannot print.</summary>
let private show (entry: IntersectionObserverEntry) =
    entry.Target.id
    + " "
    + number entry.IntersectionRatio
    + (if entry.IsIntersecting then " in" else " out")
    + " root "
    + (match entry.RootBounds with
       | Some bounds -> rect bounds
       | None -> "none")
    + " bounds "
    + rect entry.BoundingClientRect
    + " at "
    + rect entry.IntersectionRect

/// <summary>
/// Checks implicit, document, element and detached roots, pixel, percentage and negative root margins, scroll margins,
/// threshold crossings, entry rectangles, ratios and times against performance.now and the H02 timing clock,
/// takeRecords inside and outside delivery, unobserve, disconnect, target and root removal, disposal and abort signals,
/// callback isolation, entry copies held past their delivery and option errors; the result is the assertion count.
/// </summary>
let run
    (document: Document)
    (create: unit -> IntersectionObservationService)
    (timing: unit -> TimingService)
    (cancellation: unit -> CancellationService)
    (driver: Driver)
    =
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

        // Services raise ArgumentNullException and ObjectDisposedException; Fable erases the types.
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

        let optionError (label: string) (expected: string) (action: unit -> unit) =
            checks <- checks + 1

            let name =
                try
                    action ()
                    "none"
                with
                | IntersectionObserverException (name, _) -> name
                | error -> "other " + error.Message

            if name <> expected then
                failwith (label + ": expected " + expected + ", got " + name)

        let element (tag: string) (id: string) (className: string) =
            let value = document.createElement tag :?> HTMLElement
            value.id <- id

            if className <> "" then
                value.className <- className

            value

        let style = element "style" "io-style" ""
        style.textContent <- Css
        let far = element "div" "io-far" "io-t"
        let s = element "div" "io-s" ""
        let sp = element "div" "io-sp" ""
        let u = element "div" "io-u" "io-t"
        let x = element "div" "io-x" "io-t"
        let c = element "div" "io-c" ""
        let cp = element "div" "io-cp" ""
        let v = element "div" "io-v" "io-t"
        let loose = element "div" "io-loose" ""
        sp.appendChild u |> ignore
        s.appendChild sp |> ignore
        cp.appendChild v |> ignore
        c.appendChild cp |> ignore

        for value in [ style; far; s; x; c ] do
            document.body.appendChild value |> ignore

        let service = create ()
        let clock = timing ()
        let logs = Dictionary<string, List<string>>()
        let times = List<float>()
        let mutable window = (0.0, 0.0)

        let log (name: string) =
            if not (logs.ContainsKey name) then
                logs[name] <- List<string>()

            logs[name]

        let record (name: string) (delivered: IntersectionObserverEntry array) =
            let entries = log name

            for entry in delivered do
                entries.Add (show entry)
                times.Add entry.Time

        let observer (name: string) (init: IntersectionObserverInit) =
            service.Create ((fun delivered -> record name delivered), init)

        let update () =
            async {
                let before = service.Now ()
                do! driver.Update ()
                window <- (before, service.Now ())
            }

        // The entries of an observer since the last take.
        let take (name: string) =
            let entries = List.ofSeq (log name)
            (log name).Clear()
            entries

        let move (target: HTMLElement) (top: string) =
            target.style.setProperty ("top", top)

        let init threshold =
            { IntersectionObserverInit.Default with
                Threshold = threshold
            }

        let rootBounds grown plain =
            if driver.Engine = Native then grown else plain

        try
            // The implicit root: the 320x240 viewport.
            let implicitRoot = observer "implicit" (init [| 0.0; 0.5; 1.0 |])

            let documentRoot =
                observer
                    "document"
                    { (init [| 0.0; 0.5; 1.0 |]) with
                        Root = IntersectionObserverRoot.Document
                        RootMargin = "10%"
                    }

            let negative =
                observer
                    "negative"
                    { (init [| 0.0; 0.4; 1.0 |]) with
                        RootMargin = "-20px 0px"
                    }

            for o in [ implicitRoot; documentRoot; negative ] do
                o.Observe far

            do! update ()
            let first = window

            for top in [ "200px"; "235px"; "400px" ] do
                move far top
                do! update ()

            equal
                "implicit root crossings"
                [
                    "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0"
                    "io-far 0.8 in root 0,0,320,240 bounds 10,200,50,50 at 10,200,50,40"
                    "io-far 0.1 in root 0,0,320,240 bounds 10,235,50,50 at 10,235,50,5"
                    "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0"
                ]
                (take "implicit")

            equal
                "document root with a percentage margin"
                [
                    "io-far 0 out root -32,-24,384,288 bounds 10,400,50,50 at 0,0,0,0"
                    "io-far 1 in root -32,-24,384,288 bounds 10,200,50,50 at 10,200,50,50"
                    "io-far 0.58 in root -32,-24,384,288 bounds 10,235,50,50 at 10,235,50,29"
                    "io-far 0 out root -32,-24,384,288 bounds 10,400,50,50 at 0,0,0,0"
                ]
                (take "document")

            equal
                "a negative margin"
                [
                    "io-far 0 out root 0,20,320,200 bounds 10,400,50,50 at 0,0,0,0"
                    "io-far 0.4 in root 0,20,320,200 bounds 10,200,50,50 at 10,200,50,20"
                    "io-far 0 out root 0,20,320,200 bounds 10,235,50,50 at 0,0,0,0"
                ]
                (take "negative")

            // Entry times lie in their update and never decrease; the first update's entries share one time.
            truth
                "entry times never decrease"
                (Seq.pairwise times
                 |> Seq.forall (fun (a, b) -> b >= a))

            let firstTimes = times |> Seq.take 3 |> Seq.distinct |> Seq.toList
            truth "one time per rendering update" (firstTimes.Length = 1)

            truth
                "the time lies within its update"
                (fst first - 1.0 <= firstTimes.Head
                 && firstTimes.Head <= snd first + 1.0)

            let last = times[times.Count - 1]

            truth
                "the last time lies within its update"
                (fst window - 1.0 <= last
                 && last <= snd window + 1.0)

            times.Clear ()

            // The H02 timing clock and the observation clock share the host's time origin.
            let before = clock.Now ()
            let observed = service.Now ()
            let after = clock.Now ()
            let tolerance = if driver.Engine = Native then 2.0 else 0.0
            truth "now is nondecreasing" (service.Now () >= observed)

            truth
                ("the observation clock agrees with TimingService.Now: "
                 + number before
                 + " <= "
                 + number observed
                 + " <= "
                 + number after)
                (before - tolerance - 1.0 <= observed
                 && observed <= after + tolerance + 1.0)

            for o in [ implicitRoot; documentRoot; negative ] do
                o.Dispose ()

            // An element root with a pixel margin; a target outside its containing block chain reports zero rectangles.
            let elementRoot =
                observer
                    "element"
                    { (init [| 0.0; 0.5; 1.0 |]) with
                        Root = IntersectionObserverRoot.Element s
                        RootMargin = "10px"
                    }

            elementRoot.Observe u
            elementRoot.Observe x
            do! update ()

            for top in [ 100; 165; 0 ] do
                s.scrollTop <- top
                do! update ()

            equal
                "element root crossings"
                [
                    "io-u 0 out root 90,10,220,120 bounds 110,170,50,50 at 0,0,0,0"
                    "io-x 0 out root 0,0,0,0 bounds 0,0,0,0 at 0,0,0,0"
                    "io-u 1 in root 90,10,220,120 bounds 110,70,50,50 at 110,70,50,50"
                    "io-u 0.9 in root 90,10,220,120 bounds 110,5,50,50 at 110,10,50,45"
                    "io-u 0 out root 90,10,220,120 bounds 110,170,50,50 at 0,0,0,0"
                ]
                (take "element")

            elementRoot.Dispose ()

            // Scroll margins grow the scrollport of #io-c, which clips #io-v 10px below its edge.
            let margin =
                observer
                    "margin"
                    { (init [| 0.0; 0.2 |]) with
                        ScrollMargin = "20px"
                    }

            let plain = observer "plain" (init [| 0.0; 0.2 |])
            margin.Observe v
            plain.Observe v
            do! update ()

            equal
                "a scroll margin"
                [
                    "io-v 0.2 in root "
                    + rootBounds "-20,-20,360,280" "0,0,320,240"
                    + " bounds 10,120,50,50 at 10,120,50,10"
                ]
                (take "margin")

            equal "no scroll margin" [ "io-v 0 out root 0,0,320,240 bounds 10,120,50,50 at 0,0,0,0" ] (take "plain")
            margin.Dispose ()
            plain.Dispose ()

            // takeRecords is empty outside delivery; inside the first callback it takes the other observer's entries.
            let paired = List<string>()
            let mutable pair: (IntersectionObserver * IntersectionObserver) option = None
            let mutable armed = false

            let pairObserver (name: string) =
                service.Create (
                    (fun delivered ->
                        if armed then
                            armed <- false
                            let p, q = pair.Value
                            let other = if name = "p" then q else p

                            for entry in Array.append delivered (other.TakeRecords ()) do
                                paired.Add (show entry)

                            paired.Add "taken"
                        else
                            record name delivered),
                    init [| 0.0 |]
                )

            let p = pairObserver "p"
            let q = pairObserver "q"
            pair <- Some (p, q)
            p.Observe far
            q.Observe far
            do! update ()
            take "p" |> ignore
            take "q" |> ignore
            move far "200px"
            truth "takeRecords is empty before the update" (p.TakeRecords().Length = 0)
            armed <- true
            do! update ()

            equal
                "the first callback takes the other observer's records"
                [
                    "io-far 0.8 in root 0,0,320,240 bounds 10,200,50,50 at 10,200,50,40"
                    "io-far 0.8 in root 0,0,320,240 bounds 10,200,50,50 at 10,200,50,40"
                    "taken"
                ]
                (List.ofSeq paired)

            equal "the taken observer does not deliver" [] (take "p" @ take "q")
            truth "takeRecords is empty after the update" (q.TakeRecords().Length = 0)
            p.Dispose ()
            q.Dispose ()
            move far "400px"

            // Unobserve, re-observe, disconnect and takeRecords after disconnect.
            let stop = observer "stop" (init [| 0.0 |])
            stop.Observe far
            stop.Observe v
            do! update ()
            stop.Unobserve far
            move far "200px"
            do! update ()
            stop.Observe far
            do! update ()
            stop.Disconnect ()
            move far "400px"
            do! update ()
            truth "takeRecords after disconnect is empty" (stop.TakeRecords().Length = 0)
            stop.Observe far
            do! update ()

            equal
                "unobserve and disconnect"
                [
                    "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0"
                    "io-v 0 out root 0,0,320,240 bounds 10,120,50,50 at 0,0,0,0"
                    "io-far 0.8 in root 0,0,320,240 bounds 10,200,50,50 at 10,200,50,40"
                    "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0"
                ]
                (take "stop")

            stop.Dispose ()

            // A removed target and a removed root report zero rectangles; a detached root intersects nothing.
            let removal =
                observer
                    "removal"
                    { (init [| 0.0 |]) with
                        Root = IntersectionObserverRoot.Element s
                    }

            let detached =
                observer
                    "detached"
                    { (init [| 0.0 |]) with
                        Root = IntersectionObserverRoot.Element loose
                    }

            s.scrollTop <- 100
            removal.Observe u
            detached.Observe far
            do! update ()
            sp.removeChild u |> ignore
            do! update ()
            sp.appendChild u |> ignore
            s.scrollTop <- 100
            do! update ()
            document.body.removeChild s |> ignore
            do! update ()

            equal
                "target and root removal"
                [
                    "io-u 1 in root 100,20,200,100 bounds 110,70,50,50 at 110,70,50,50"
                    "io-u 0 out root 0,0,0,0 bounds 0,0,0,0 at 0,0,0,0"
                    "io-u 1 in root 100,20,200,100 bounds 110,70,50,50 at 110,70,50,50"
                    "io-u 0 out root 0,0,0,0 bounds 0,0,0,0 at 0,0,0,0"
                ]
                (take "removal")

            equal "a detached root" [ "io-far 0 out root 0,0,0,0 bounds 0,0,0,0 at 0,0,0,0" ] (take "detached")
            document.body.appendChild s |> ignore
            removal.Dispose ()
            detached.Dispose ()

            // An exception in one callback is reported once and leaves the other observers delivering.
            let errors = driver.Errors ()

            let thrower =
                service.Create ((fun _ -> raise (InvalidOperationException "boom")), init [| 0.0 |])

            let witness = observer "witness" (init [| 0.0 |])
            thrower.Observe far
            witness.Observe far
            do! update ()
            truth "the throwing callback is reported once" (driver.Errors () = errors + 1)
            equal "other observers still deliver" [ "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0" ] (take "witness")
            thrower.Dispose ()
            witness.Dispose ()

            // An entry is a copy that outlives its delivery.
            let mutable held = None

            let holder =
                service.Create ((fun delivered -> held <- Some delivered[0]), init [| 0.0 |])

            holder.Observe far
            do! update ()
            holder.Dispose ()
            move far "200px"
            do! update ()
            let entry = held.Value

            truth
                "a held entry keeps its values"
                (entry.Target.id = "io-far"
                 && entry.BoundingClientRect.Y = 400.0
                 && not entry.IsIntersecting)

            move far "400px"

            // Disposal and abort signals.
            let disposed = observer "disposed" (init [| 0.0 |])
            disposed.Dispose ()
            disposed.Dispose ()
            raises "Observe after Dispose" (fun () -> disposed.Observe far)
            raises "Disconnect after Dispose" (fun () -> disposed.Disconnect ())
            truth "takeRecords after Dispose is empty" (disposed.TakeRecords().Length = 0)
            let signals = cancellation ()
            let controller = signals.CreateController ()

            let aborting =
                service.Create ((fun delivered -> record "aborting" delivered), init [| 0.0 |], controller.Signal)

            aborting.Observe far
            do! update ()
            controller.Abort ()
            move far "200px"
            do! update ()
            equal "abort stops delivery" [ "io-far 0 out root 0,0,320,240 bounds 10,400,50,50 at 0,0,0,0" ] (take "aborting")
            raises "Observe after abort" (fun () -> aborting.Observe far)

            let late =
                service.Create ((fun delivered -> record "late" delivered), init [| 0.0 |], signals.Abort AbortReason.Abort)

            raises "Observe with an aborted signal" (fun () -> late.Observe far)
            move far "400px"

            // Option errors carry the engine's names.
            optionError "an invalid root margin" "SyntaxError" (fun () ->
                observer
                    "bad"
                    { (init [| 0.0 |]) with
                        RootMargin = "bogus"
                    }
                |> ignore)

            optionError "an em root margin" "SyntaxError" (fun () ->
                observer
                    "bad"
                    { (init [| 0.0 |]) with
                        RootMargin = "1em"
                    }
                |> ignore)

            optionError "an invalid scroll margin" "SyntaxError" (fun () ->
                observer
                    "bad"
                    { (init [| 0.0 |]) with
                        ScrollMargin = "bogus"
                    }
                |> ignore)

            optionError "a threshold above 1" "RangeError" (fun () -> observer "bad" (init [| 2.0 |]) |> ignore)
            optionError "a negative threshold" "RangeError" (fun () -> observer "bad" (init [| -0.1 |]) |> ignore)
            let unsorted = observer "unsorted" (init [| 1.0; 0.0 |])
            unsorted.Dispose ()
            truth "unsorted thresholds are accepted" true
            let checkedObserver = observer "checked" (init [| 0.0 |])
            raises "null target" (fun () -> checkedObserver.Observe (Unchecked.defaultof<Element>))
            raises "null unobserve target" (fun () -> checkedObserver.Unobserve (Unchecked.defaultof<Element>))

            raises "null callback" (fun () ->
                service.Create (Unchecked.defaultof<_>, init [| 0.0 |])
                |> ignore)

            service.Dispose ()
            raises "Observe after service disposal" (fun () -> checkedObserver.Observe far)
            raises "Create after service disposal" (fun () -> service.Create (ignore, init [| 0.0 |]) |> ignore)
            signals.Dispose ()
            clock.Dispose ()
        finally
            for value in [ style; far; s; x; c ] do
                if not (isNull value.parentNode) then
                    value.parentNode.removeChild value |> ignore

        return checks
    }
