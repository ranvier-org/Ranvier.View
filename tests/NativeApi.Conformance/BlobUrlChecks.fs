module internal NativeApiConformance.BlobUrlChecks

open System
open Ranvier.Dom

/// <summary>Task delivery and the engine facts the fixture needs.</summary>
type Driver =
    {
        /// <summary>Lets queued work run, so pending reads and FileReader events deliver.</summary>
        Settle: unit -> Async<unit>
        /// <summary>
        /// Whether readAsText without an encoding decodes with the blob type's charset, as WebKit and the File API do;
        /// Chromium decodes UTF-8 (Ranvier.WebCore tests/fixtures/blob-urls-qualified.json).
        /// </summary>
        CharsetFromType: bool
        /// <summary>The serialized origin of the document: "null" for an opaque one.</summary>
        Origin: string
    }

let private hex (bytes: byte array) =
    bytes
    |> Array.map (fun b -> (int b).ToString("x2"))
    |> String.concat ""

let private parse (text: string) =
    Array.init (text.Length / 2) (fun i -> Convert.ToByte (text.Substring (i * 2, 2), 16))

/// <summary>Whether <c>url</c> is <c>blob:&lt;origin&gt;/&lt;UUID v4&gt;</c>; the origin is the document's.</summary>
let private isUuidUrl (origin: string) (url: string) =
    let prefix = "blob:" + origin + "/"

    if not (url.StartsWith prefix) then
        false
    else
        let id = url.Substring prefix.Length
        let groups = id.Split '-'

        id.Length = 36
        && groups.Length = 5
        && (groups |> Array.map String.length) = [| 8; 4; 4; 4; 12 |]
        && groups[2].[0] = '4'
        && "89ab".Contains groups[3].[0]
        && id
           |> Seq.forall (fun c -> c = '-' || "0123456789abcdef".Contains c)

/// <summary>
/// Checks object URLs, engine reads of blobs and blob URLs, owner-bound cancellation and FileReader event order and
/// state on one provider; the result is the assertion count. The expected values are the
/// Chromium 156 oracle of Ranvier.WebCore tests/fixtures/blob-urls-oracle.json. Progress events are collapsed and
/// never counted.
/// </summary>
let run (create: unit -> BlobUrlService) (values: unit -> BinaryValueService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let fails label (action: unit -> unit) =
            checks <- checks + 1

            let failed =
                try
                    action ()
                    false
                with _ ->
                    true

            if not failed then
                failwith $"{label}: expected an exception"

        use graph = new Ranvier.Graph ()
        use _ = graph.Activate ()
        use service = create ()
        use binary = values ()

        let blobOf parts type' =
            binary.CreateBlob (
                parts,
                { BlobOptions.Default with
                    Type = type'
                }
            )

        let await' (start: (ResourceOutcome<'T> -> unit) -> ResourceOperation<'T>) =
            async {
                let result = ref None
                use _ = start (fun outcome -> result.Value <- Some outcome)

                while result.Value.IsNone do
                    do! driver.Settle ()

                return result.Value.Value
            }

        let succeeded label outcome =
            match outcome with
            | ResourceOutcome.Succeeded value -> value
            | ResourceOutcome.Failed failure -> failwith $"{label}: failed with %A{failure}"

        let loadFailed label outcome =
            checks <- checks + 1

            match outcome with
            | ResourceOutcome.Failed (ResourceFailure.LoadFailed _) -> ()
            | other -> failwith $"{label}: expected LoadFailed, got %A{other}"

        // Object URLs and URL reads.
        let abc = blobOf [| BlobPart.Text "abc" |] "text/plain"
        let first = service.CreateObjectUrl abc
        let again = service.CreateObjectUrl abc
        equal "object URL format" true (isUuidUrl driver.Origin first)
        equal "object URLs are distinct" true (first <> again)

        let readUrl url =
            await' (fun terminal -> service.ReadUrl (url, terminal))

        let! typed = readUrl first
        let typed = succeeded "typed URL read" typed
        equal "typed URL read" ("text/plain", "616263") (typed.ContentType, hex typed.Bytes)
        let! empty = readUrl (service.CreateObjectUrl (blobOf [||] ""))
        let empty = succeeded "empty URL read" empty
        equal "empty URL read" ("", "") (empty.ContentType, hex empty.Bytes)
        let digits = blobOf [| BlobPart.Text "0123456789" |] "a/b"
        let! sliced = readUrl (service.CreateObjectUrl (digits.slice (2L, 5L)))
        let sliced = succeeded "slice URL read" sliced
        equal "slice URL read" ("", "323334") (sliced.ContentType, hex sliced.Bytes)
        let! fragment = readUrl (again + "#frag")
        equal "fragment URL read" "616263" (hex (succeeded "fragment URL read" fragment).Bytes)
        service.RevokeObjectUrl first
        let! revoked = readUrl first
        loadFailed "revoked URL read" revoked
        let! other = readUrl again
        equal "another URL of the blob still reads" "616263" (hex (succeeded "other URL" other).Bytes)
        service.RevokeObjectUrl first
        service.RevokeObjectUrl "not a url"
        service.RevokeObjectUrl "https://x/"

        let! unknown =
            readUrl (
                "blob:"
                + driver.Origin
                + "/00000000-0000-4000-8000-000000000000"
            )

        loadFailed "unknown URL read" unknown
        fails "a non-blob URL is rejected" (fun () -> service.ReadUrl ("https://x/", ignore) |> ignore)

        // Blob.text(), arrayBuffer() and bytes().
        let readText blob =
            await' (fun terminal -> service.ReadText (blob, terminal))

        let readBytes blob =
            await' (fun terminal -> service.ReadBytes (blob, terminal))

        let! text = readText (blobOf [| BlobPart.Bytes (parse "efbbbf41ff") |] "")
        equal "text drops the BOM and replaces invalid UTF-8" "A�" (succeeded "text" text)
        let! latin = readText (blobOf [| BlobPart.Text "é" |] "text/plain;charset=latin1")
        equal "text ignores the type's charset" "é" (succeeded "text latin1 type" latin)

        let mixed =
            blobOf [| BlobPart.Text "hé"; BlobPart.Bytes (parse "ff4100") |] "text/plain;charset=latin1"

        let! bytes = readBytes mixed
        equal "bytes" "68c3a9ff4100" (hex (succeeded "bytes" bytes))
        let! emptyText = readText (blobOf [||] "")
        equal "empty text" "" (succeeded "empty text" emptyText)
        let big = Array.create (1 <<< 20) 0xa5uy
        let! bigBytes = readBytes (blobOf [| BlobPart.Bytes big |] "")
        let bigBytes = succeeded "big bytes" bigBytes
        equal "big bytes" (big.Length, true) (bigBytes.Length, bigBytes = big)

        // Owner-bound cancellation: disposing a pending read delivers Cancelled.
        let cancelled = ref None

        let pending =
            service.ReadBytes (blobOf [| BlobPart.Bytes big |] "", fun outcome -> cancelled.Value <- Some outcome)

        pending.Dispose ()

        while cancelled.Value.IsNone do
            do! driver.Settle ()

        equal "a disposed read is cancelled" (Some (ResourceOutcome<byte array>.Failed ResourceFailure.Cancelled)) cancelled.Value

        // FileReader.
        let describe (value: FileReaderResult) =
            match value with
            | FileReaderResult.Empty -> "null"
            | FileReaderResult.Text text -> "string:" + text
            | FileReaderResult.Bytes bytes -> "bytes:" + hex bytes

        let kind (value: FileReaderResult) =
            match value with
            | FileReaderResult.Empty -> "null"
            | FileReaderResult.Text _ -> "string"
            | FileReaderResult.Bytes _ -> "bytes"

        let x () =
            blobOf [| BlobPart.Text "x" |] ""

        /// Starts a read; returns the collapsed event order with "return" after the read call and "acted" after a hook,
        /// the readyState, result kind and error at each first event, and the final state.
        let readWith (start: FileReader -> unit) (hook: string) (act: FileReader -> unit) (ends: int) =
            async {
                let reader = service.CreateFileReader ()
                let events = Collections.Generic.List<string>()

                let states =
                    Collections.Generic.Dictionary<string, uint16 * string * string option>()

                let mutable loadends = 0
                let mutable acted = false

                for eventType in [ "loadstart"; "progress"; "load"; "abort"; "error"; "loadend" ] do
                    reader.addEventListener (
                        eventType,
                        fun _ ->
                            if
                                events.Count = 0
                                || events[events.Count - 1] <> eventType
                            then
                                events.Add eventType

                            if not (states.ContainsKey eventType) then
                                states[eventType] <- (reader.readyState, kind (service.Result reader), service.Error reader)

                            if eventType = "loadend" then
                                loadends <- loadends + 1

                            if hook = eventType && not acted then
                                acted <- true
                                act reader
                                events.Add "acted"
                    )

                start reader
                events.Add "return"

                if hook = "sync" then
                    act reader
                    events.Add "acted"

                while loadends < ends do
                    do! driver.Settle ()

                do! driver.Settle ()
                return List.ofSeq events, states, (reader.readyState, describe (service.Result reader), service.Error reader)
            }

        let read method (blob: Blob) =
            readWith (fun reader -> method reader blob) "" ignore 1

        let ok = [ "return"; "loadstart"; "progress"; "load"; "loadend" ]

        let! events, states, final =
            read (fun reader blob -> reader.readAsArrayBuffer blob) mixed

        equal "readAsArrayBuffer events" ok events
        equal "readAsArrayBuffer loadstart" (1us, "null", None) states["loadstart"]
        equal "readAsArrayBuffer load" (2us, "bytes", None) states["load"]
        equal "readAsArrayBuffer final" (2us, "bytes:68c3a9ff4100", None) final
        let! events, _, final = read (fun reader blob -> reader.readAsText blob) mixed
        equal "readAsText events" ok events

        equal
            "readAsText final"
            (2us,
             (if driver.CharsetFromType then
                  "string:hÃ©ÿA\u0000"
              else
                  "string:hé�A\u0000"),
             None)
            final

        let! _, _, final =
            read (fun reader blob -> reader.readAsText (blob, "iso-8859-1")) mixed

        equal "readAsText latin1 final" (2us, "string:hÃ©ÿA\u0000", None) final

        let! _, _, final =
            read (fun reader blob -> reader.readAsText (blob, "utf-16le")) (blobOf [| BlobPart.Bytes (parse "41004200") |] "")

        equal "readAsText utf-16le final" (2us, "string:AB", None) final
        let! _, _, final = read (fun reader blob -> reader.readAsBinaryString blob) mixed
        equal "readAsBinaryString final" (2us, "string:hÃ©ÿA\u0000", None) final
        let! _, _, final = read (fun reader blob -> reader.readAsDataURL blob) mixed
        equal "readAsDataURL final" (2us, "string:data:text/plain;charset=latin1;base64,aMOp/0EA", None) final

        let! events, _, final =
            read (fun reader blob -> reader.readAsDataURL blob) (blobOf [||] "")

        equal "readAsDataURL empty events" [ "return"; "loadstart"; "load"; "loadend" ] events
        equal "readAsDataURL empty final" (2us, "string:data:application/octet-stream;base64,", None) final

        let! events, _, final =
            read (fun reader blob -> reader.readAsArrayBuffer blob) (blobOf [| BlobPart.Bytes big |] "")

        equal "readAsArrayBuffer 1 MiB events" ok events
        equal "readAsArrayBuffer 1 MiB final state" (2us, None) (let (s, _, e) = final in s, e)

        let! events, states, final =
            readWith (fun reader -> reader.readAsText (x ())) "sync" (fun reader -> reader.abort ()) 1

        equal "abort after the read call" [ "return"; "abort"; "loadend"; "acted" ] events
        equal "abort state" (2us, "null", Some "AbortError") states["abort"]
        equal "abort final" (2us, "null", Some "AbortError") final

        let! events, _, final =
            readWith (fun reader -> reader.readAsText (x ())) "loadstart" (fun reader -> reader.abort ()) 1

        equal "abort in loadstart" [ "return"; "loadstart"; "abort"; "loadend"; "acted" ] events
        equal "abort in loadstart final" (2us, "null", Some "AbortError") final

        let! events, _, final =
            readWith (fun reader -> reader.readAsText (x ())) "load" (fun reader -> reader.abort ()) 1

        equal "abort in load is ignored" [ "return"; "loadstart"; "progress"; "load"; "acted"; "loadend" ] events
        equal "abort in load final" (2us, "string:x", None) final
        let mutable second = ""

        let again (reader: FileReader) =
            second <-
                try
                    reader.readAsText (blobOf [| BlobPart.Text "y" |] "")
                    "ok"
                with _ ->
                    "threw"

        let! events, _, final =
            readWith (fun reader -> reader.readAsText (x ())) "loadend" again 2

        equal
            "readAsText in loadend"
            (ok
             @ [ "acted"; "loadstart"; "progress"; "load"; "loadend" ],
             "ok")
            (events, second)

        equal "readAsText in loadend final" (2us, "string:y", None) final

        let! events, _, final =
            readWith (fun reader -> reader.readAsText (x ())) "sync" again 1

        equal "readAsText while loading throws" ([ "return"; "acted"; "loadstart"; "progress"; "load"; "loadend" ], "threw") (events, second)
        equal "readAsText while loading final" (2us, "string:x", None) final

        let idle = service.CreateFileReader ()
        let fired = Collections.Generic.List<string>()

        for eventType in [ "abort"; "loadend" ] do
            idle.addEventListener (eventType, fun _ -> fired.Add eventType)

        idle.abort ()
        equal "abort while empty" (0us, 0, "null", None) (idle.readyState, fired.Count, describe (service.Result idle), service.Error idle)
        return checks
    }
