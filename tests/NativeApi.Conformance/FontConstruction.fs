module internal NativeApiConformance.FontConstruction

open System
open System.Collections.Generic
open Ranvier.Dom

let private describe (value: obj) : string =
    match value with
    | null -> "null"
    | :? string as text -> "\"" + text + "\""
    | :? bool as flag -> if flag then "true" else "false"
    | :? int as number -> number.ToString ()
    | _ -> value.GetType().Name

// A DOM string enumeration is a string under Fable, so a status is matched by case rather than by type test.
let private status (value: FontFaceLoadStatus) =
    match value with
    | FontFaceLoadStatus.Unloaded -> "Unloaded"
    | FontFaceLoadStatus.Loading -> "Loading"
    | FontFaceLoadStatus.Loaded -> "Loaded"
    | FontFaceLoadStatus.Error -> "Error"

let private outcome (value: ResourceOutcome<'T>) =
    match value with
    | ResourceOutcome.Succeeded value -> "Succeeded " + describe (box value)
    | ResourceOutcome.Failed (ResourceFailure.LoadFailed message) -> "LoadFailed " + message
    | ResourceOutcome.Failed (ResourceFailure.DecodeFailed message) -> "DecodeFailed " + message
    | ResourceOutcome.Failed ResourceFailure.Cancelled -> "Cancelled"
    | ResourceOutcome.Failed (ResourceFailure.Invalidated _) -> "Invalidated"

/// <summary>
/// Checks FontFace construction from bytes and URL sources, descriptors and their errors, FontFace.load and loaded,
/// FontFaceSet add/has/delete/clear and iteration with measurement, cancellation and disposal.
/// </summary>
/// <param name="ahem">The Ahem TrueType font's bytes.</param>
let run (document: Document) (server: Fonts.FontServer) (create: unit -> FontSet) (ahem: byte array) (wait: int -> Async<unit>) =
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

        // Interface type tests fail under Fable, so a face outcome names its family through the typed value.
        let trackFace (start: (ResourceOutcome<FontFace> -> unit) -> ResourceOperation<FontFace>) =
            let terminals = List<string>()
            let mutable inside = true

            let operation =
                start (fun value ->
                    let text =
                        match value with
                        | ResourceOutcome.Succeeded face -> "Succeeded FontFace " + face.family
                        | failed -> outcome failed

                    terminals.Add ((if inside then "inside " else "") + text))

            inside <- false
            operation, terminals

        let first (terminals: List<string>) =
            "[" + String.Join ("; ", terminals) + "]"

        let throws (action: unit -> unit) =
            try
                action ()
                false
            with _ ->
                true

        let set = create ()
        let none = FontFaceDescriptors.Default
        let span = document.createElement "span"
        span.setAttribute ("style", "font:20px RvT02Added, monospace;white-space:pre")
        span.textContent <- "XXXX"
        document.body.appendChild span |> ignore

        let width () =
            span.getBoundingClientRect().width

        let fallback = width ()

        // Byte sources settle at construction; loaded reports the outcome on a later turn.
        let face = set.Create ("RvT02Bytes", FontFaceSource.Bytes ahem, none)
        equal "Bytes face status" "Loaded" (status face.status)

        let operation, terminals =
            trackFace (fun terminal -> set.FaceLoaded (face, terminal))

        do! until "bytes loaded" (fun () -> settled terminals)
        equal "Bytes face loaded" "[Succeeded FontFace RvT02Bytes]" (first terminals)
        operation.Dispose ()

        for name, bytes in [ "invalid", "not a font"B; "empty", [||] ] do
            let bad = set.Create ("RvT02" + name, FontFaceSource.Bytes bytes, none)
            equal (name + " bytes status") "Error" (status bad.status)

            let operation, terminals =
                trackFace (fun terminal -> set.FaceLoaded (bad, terminal))

            do! until (name + " loaded") (fun () -> settled terminals)
            equal (name + " bytes loaded") "[DecodeFailed SyntaxError]" (first terminals)
            operation.Dispose ()

        // Descriptors and their errors.
        let given =
            set.Create (
                "RvT02Desc",
                FontFaceSource.Bytes ahem,
                { none with
                    Weight = "bold"
                    Style = "italic"
                    UnicodeRange = "U+0-7F"
                    Display = "swap"
                }
            )

        // WebKit writes unicode-range hexadecimal in lowercase, Chromium in uppercase.
        equal
            "Descriptors"
            "bold italic U+0-7F swap"
            (String.Join (" ", given.weight, given.style, given.unicodeRange.ToUpperInvariant (), given.display))

        equal "Default stretch" "normal" given.stretch

        let bad =
            set.Create ("RvT02BadWeight", FontFaceSource.Bytes ahem, { none with Weight = "heavy" })

        equal "Invalid weight status" "Error" (status bad.status)

        let operation, terminals =
            trackFace (fun terminal -> set.FaceLoaded (bad, terminal))

        do! until "invalid weight" (fun () -> settled terminals)
        equal "Invalid weight loaded" "[DecodeFailed SyntaxError]" (first terminals)
        operation.Dispose ()
        given.weight <- "300"
        equal "Weight setter" "300" given.weight
        truth "Invalid weight setter throws" (throws (fun () -> given.weight <- "heavy"))
        equal "Invalid weight setter keeps the value" "300" given.weight

        // URL sources: loaded waits for a load; load starts it.
        let url =
            set.Create ("RvT02Url", FontFaceSource.Css ("url(" + server.Url "construct" + ")"), none)

        equal "URL face unloaded" "Unloaded" (status url.status)
        let waiting, waited = trackFace (fun terminal -> set.FaceLoaded (url, terminal))
        do! wait 50
        equal "loaded starts no load" "[]" (first waited)
        let loading, loaded = trackFace (fun terminal -> set.LoadFace (url, terminal))
        equal "URL face loading" "Loading" (status url.status)
        do! until "URL requested" (fun () -> server.Requested "construct")
        do! server.Respond "construct" Fonts.FontResponse.Font
        do! until "URL settles" (fun () -> async { return waited.Count > 0 && loaded.Count > 0 })
        equal "URL load" "[Succeeded FontFace RvT02Url]" (first loaded)
        equal "URL loaded" "[Succeeded FontFace RvT02Url]" (first waited)
        waiting.Dispose ()
        loading.Dispose ()

        let missing =
            set.Create ("RvT02Missing", FontFaceSource.Css ("url(" + server.Url "missingface" + ")"), none)

        let operation, terminals =
            trackFace (fun terminal -> set.LoadFace (missing, terminal))

        do! until "missing requested" (fun () -> server.Requested "missingface")
        do! server.Respond "missingface" Fonts.FontResponse.Missing
        do! until "missing settles" (fun () -> settled terminals)
        equal "Missing URL" "[LoadFailed NetworkError]" (first terminals)
        operation.Dispose ()

        let operation, terminals =
            trackFace (fun terminal -> set.LoadFace (missing, terminal))

        do! until "missing again" (fun () -> settled terminals)
        equal "A failed face keeps its outcome" "[LoadFailed NetworkError]" (first terminals)
        operation.Dispose ()

        let urlBad =
            set.Create ("RvT02UrlBad", FontFaceSource.Css ("url(" + server.Url "never" + ")"), { none with Weight = "heavy" })

        let operation, terminals =
            trackFace (fun terminal -> set.LoadFace (urlBad, terminal))

        do! until "URL bad descriptor" (fun () -> settled terminals)
        equal "URL face with an invalid descriptor" "[DecodeFailed SyntaxError]" (first terminals)
        operation.Dispose ()

        // Registration and measurement.
        let faces = set.Faces
        let initial = faces.size
        let added = set.Create ("RvT02Added", FontFaceSource.Bytes ahem, none)
        truth "Fallback before add" (abs (fallback - 80.0) > 1.0)

        equal
            "check before add"
            "Ok true"
            (match set.Check ("20px RvT02Added", " ") with
             | Ok v -> "Ok " + describe (box v)
             | Error e -> "Error " + e)

        faces.add added |> ignore
        faces.add added |> ignore
        equal "size after add" (string (initial + 1)) (string faces.size)
        truth "has after add" (faces.has added)
        truth "Values ends with the added face" (obj.ReferenceEquals (List.last (set.Values ()), added))
        truth "Added face measures with Ahem metrics" (abs (width () - 80.0) <= 1.0)
        truth "delete" (faces.delete added)
        truth "delete again is false" (not (faces.delete added))
        truth "Deleted face leaves layout" (abs (width () - fallback) <= 0.01)
        faces.add added |> ignore
        faces.clear ()
        equal "clear leaves CSS faces" (string initial) (string faces.size)
        truth "clear removes the face" (not (faces.has added))
        equal "A cleared face keeps its status" "Loaded" (status added.status)

        // Cancellation and disposal.
        let held =
            set.Create ("RvT02Held", FontFaceSource.Css ("url(" + server.Url "heldface" + ")"), none)

        let operation, terminals = trackFace (fun terminal -> set.LoadFace (held, terminal))
        do! until "held requested" (fun () -> server.Requested "heldface")
        operation.Cancel ()
        do! until "cancel settles" (fun () -> settled terminals)
        equal "Cancel" "[Cancelled]" (first terminals)
        operation.Dispose ()

        let lazyFace =
            set.Create ("RvT02Lazy", FontFaceSource.Css ("url(" + server.Url "lazyface" + ")"), none)

        let pending, pendingTerminals =
            trackFace (fun terminal -> set.FaceLoaded (lazyFace, terminal))

        (set :> IDisposable).Dispose()
        do! until "disposal settles" (fun () -> settled pendingTerminals)
        equal "Disposal cancels pending operations" "[Cancelled]" (first pendingTerminals)
        pending.Dispose ()
        do! server.Respond "heldface" Fonts.FontResponse.Font
        span.remove ()
        return checks
    }
