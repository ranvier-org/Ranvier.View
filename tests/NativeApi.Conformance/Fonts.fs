module internal NativeApiConformance.Fonts

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>How the fixture's server answers a held font request.</summary>
[<RequireQualifiedAccess>]
type FontResponse =
    | Font
    | Broken
    | Missing

/// <summary>A font server that holds each request until the fixture answers it.</summary>
type FontServer =
    {
        /// <summary>The URL of a held font named <c>name</c>.</summary>
        Url: string -> string
        /// <summary>True once the document requested the named font.</summary>
        Requested: string -> Async<bool>
        /// <summary>Answers the named font's held request.</summary>
        Respond: string -> FontResponse -> Async<unit>
    }

let private describe (value: obj) : string =
    match value with
    | null -> "null"
    | :? string as text -> "\"" + text + "\""
    | :? bool as flag -> if flag then "true" else "false"
    | :? int as number -> number.ToString ()
    | :? FontSetStatus as status ->
        match status with
        | FontSetStatus.Loading -> "Loading"
        | FontSetStatus.Loaded -> "Loaded"
    | :? FontSetEvent as event ->
        match event with
        | FontSetEvent.Loading -> "Loading"
        | FontSetEvent.LoadingDone families ->
            "LoadingDone ["
            + String.Join (";", families)
            + "]"
        | FontSetEvent.LoadingError families ->
            "LoadingError ["
            + String.Join (";", families)
            + "]"
    | _ -> value.GetType().Name

let private eventList (events: FontSetEvent list) =
    "["
    + String.Join ("; ", events |> List.map (fun e -> describe (box e)))
    + "]"

let private outcome (value: ResourceOutcome<'T>) =
    match value with
    | ResourceOutcome.Succeeded value -> "Succeeded " + describe (box value)
    | ResourceOutcome.Failed (ResourceFailure.LoadFailed message) -> "LoadFailed " + message
    | ResourceOutcome.Failed (ResourceFailure.DecodeFailed message) -> "DecodeFailed " + message
    | ResourceOutcome.Failed ResourceFailure.Cancelled -> "Cancelled"
    | ResourceOutcome.Failed (ResourceFailure.Invalidated _) -> "Invalidated"

/// <summary>
/// Checks font set status, check, load, ready and events over a held CSS @font-face, a broken and a missing face,
/// cancellation through an abort signal, and disposal with operations pending.
/// </summary>
/// <param name="create">Creates a font set over the document.</param>
/// <param name="signals">Creates abort controllers.</param>
let run (document: Document) (server: FontServer) (create: unit -> FontSet) (signals: CancellationService) (wait: int -> Async<unit>) =
    async {
        let mutable checks = 0

        let equal label (expected: string) (actual: string) =
            checks <- checks + 1

            if expected <> actual then
                failwith (
                    label
                    + ": expected "
                    + expected
                    + ", got "
                    + actual
                )

        let truth label condition =
            equal label "true" (describe (box condition))

        // Hang guard: polling counts no assertion, so totals do not depend on timing.
        let until label (condition: unit -> Async<bool>) =
            async {
                let mutable ready = false
                let mutable turns = 0

                while not ready do
                    let! value = condition ()
                    ready <- value

                    if not ready then
                        turns <- turns + 1

                        if turns > 2000 then
                            failwith ("Timed out: " + label)

                        do! wait 5
            }

        let settled (terminals: List<string>) =
            async { return terminals.Count > 0 }

        let track (start: (ResourceOutcome<'T> -> unit) -> ResourceOperation<'T>) =
            let terminals = List<string>()
            let mutable inside = true

            let operation =
                start (fun value -> terminals.Add ((if inside then "inside " else "") + outcome value))

            inside <- false
            operation, terminals

        let set = create ()
        let events = List<FontSetEvent>()
        let subscription = set.Subscribe events.Add
        let controller = signals.CreateController ()
        let aborted = List<FontSetEvent>()
        let _ = ResourceSignals.disposeOn controller.Signal (set.Subscribe aborted.Add)

        let style = document.createElement "style"

        style.textContent <-
            "@font-face{font-family:RvLate;src:url("
            + server.Url "late"
            + ")}"
            + "@font-face{font-family:RvBroken;src:url("
            + server.Url "broken"
            + ")}"
            + "@font-face{font-family:RvMissing;src:url("
            + server.Url "missing"
            + ")}"
            + "@font-face{font-family:RvHeld;src:url("
            + server.Url "held"
            + ")}"
            + "#rv-font-probe{font:20px RvLate, monospace;white-space:pre;position:absolute}"

        document.head.appendChild style |> ignore
        let span = document.createElement "span"
        span.id <- "rv-font-probe"
        span.textContent <- "XXXX"
        document.body.appendChild span |> ignore
        span.getBoundingClientRect () |> ignore
        do! until "late font requested" (fun () -> server.Requested "late")
        let fallback = span.getBoundingClientRect().width
        do! until "loading event" (fun () -> async { return events.Count > 0 && aborted.Count > 0 })
        controller.Abort ()

        equal "Status while held" "Loading" (describe (box set.Status))

        equal
            "check while held"
            "Ok false"
            (match set.Check ("20px RvLate", " ") with
             | Ok v -> "Ok " + describe (box v)
             | Error e -> e)

        equal
            "check without a match"
            "Ok true"
            (match set.Check ("20px NoSuchFamily", " ") with
             | Ok v -> "Ok " + describe (box v)
             | Error e -> e)

        equal
            "check syntax"
            "SyntaxError"
            (match set.Check ("not a font", " ") with
             | Ok v -> "Ok " + describe (box v)
             | Error e -> e)

        let _, readyHeld = track set.Ready
        let _, late = track (fun terminal -> set.Load ("20px RvLate", " ", terminal))
        let _, syntax = track (fun terminal -> set.Load ("not a font", " ", terminal))
        let _, none = track (fun terminal -> set.Load ("20px NoSuchFamily", " ", terminal))
        do! until "syntax and no-match loads" (fun () -> async { return syntax.Count > 0 && none.Count > 0 })
        equal "load syntax" "LoadFailed SyntaxError" syntax[0]
        equal "load without a match" "Succeeded 0" none[0]
        do! wait 50
        equal "ready waits" "0" (describe (box readyHeld.Count))
        equal "load waits" "0" (describe (box late.Count))

        do! server.Respond "late" FontResponse.Font
        do! until "late font settles" (fun () -> async { return readyHeld.Count > 0 && late.Count > 0 })
        equal "late load" "Succeeded 1" late[0]
        equal "ready" "Succeeded null" readyHeld[0]
        let measured = span.getBoundingClientRect().width

        truth
            "measured after ready"
            (abs (measured - 80.0) <= 1.0
             && abs (fallback - 80.0) > 1.0)

        do! until "loadingdone" (fun () -> async { return events.Count >= 2 })
        do! wait 20
        equal "events" "[Loading; LoadingDone [RvLate]]" (eventList (List.ofSeq events))
        equal "aborted subscription ends at the abort" "[Loading]" (eventList (List.ofSeq aborted))
        equal "status loaded" "Loaded" (describe (box set.Status))

        equal
            "check loaded"
            "Ok true"
            (match set.Check ("20px RvLate", " ") with
             | Ok v -> "Ok " + describe (box v)
             | Error e -> e)

        let _, readyNow = track set.Ready
        equal "settled ready is deferred" "0" (describe (box readyNow.Count))
        do! until "settled ready" (fun () -> settled readyNow)
        equal "settled ready" "Succeeded null" readyNow[0]

        let count = events.Count
        let _, broken = track (fun terminal -> set.Load ("20px RvBroken", " ", terminal))
        let _, missing = track (fun terminal -> set.Load ("20px RvMissing", " ", terminal))
        do! until "broken requested" (fun () -> server.Requested "broken")
        do! until "missing requested" (fun () -> server.Requested "missing")
        do! server.Respond "broken" FontResponse.Broken
        do! server.Respond "missing" FontResponse.Missing
        do! until "failed loads" (fun () -> async { return broken.Count > 0 && missing.Count > 0 })
        equal "broken load" "LoadFailed NetworkError" broken[0]
        equal "missing load" "LoadFailed NetworkError" missing[0]
        do! until "loadingerror" (fun () -> async { return events.Count >= count + 3 })
        do! wait 20

        let failedEvents =
            List.ofSeq events
            |> List.skip count
            |> List.map (fun event ->
                match event with
                | FontSetEvent.LoadingError families -> FontSetEvent.LoadingError (List.sort families)
                | other -> other)

        equal "failure events" "[Loading; LoadingDone []; LoadingError [RvBroken;RvMissing]]" (eventList failedEvents)

        // Cancellation through an abort signal, then disposal with a pending ready: one terminal each, none after.
        let abort = signals.CreateController ()
        let heldLoad, held = track (fun terminal -> set.Load ("20px RvHeld", " ", terminal))
        let registration = ResourceSignals.cancelOn abort.Signal heldLoad
        do! until "held requested" (fun () -> server.Requested "held")
        abort.Abort ()

        equal
            "abort cancels"
            "Cancelled"
            (if held.Count = 1 then
                 held[0]
             else
                 describe (box held.Count))

        registration.Dispose ()
        let _, pendingReady = track set.Ready
        let other = create ()
        let _, otherReady = track other.Ready
        other.Dispose ()

        equal
            "dispose cancels"
            "Cancelled"
            (if otherReady.Count = 1 then
                 otherReady[0]
             else
                 describe (box otherReady.Count))

        do! server.Respond "held" FontResponse.Font
        do! until "pending ready" (fun () -> settled pendingReady)
        do! wait 50

        equal
            "no stale terminal"
            "1 1"
            (describe (box held.Count)
             + " "
             + describe (box otherReady.Count))

        let disposedStatus =
            try
                describe (box other.Status)
            with error ->
                if error.Message.Contains "FontSet" then
                    "disposed"
                else
                    error.Message

        equal "disposed set" "disposed" disposedStatus
        subscription.Dispose ()
        let before = events.Count
        set.Dispose ()
        span.remove ()
        style.remove ()
        do! wait 20
        equal "no event after dispose" (describe (box before)) (describe (box events.Count))
        return checks
    }
