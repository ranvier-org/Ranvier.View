module internal NativeApiConformance.BinaryChecks

open System
open Ranvier.Dom
open Ranvier.View.Dom

/// <summary>Task delivery and platform facts the fixture needs.</summary>
type Driver =
    {
        /// <summary>Lets queued work run, so pending reads deliver.</summary>
        Settle: unit -> Async<unit>
        /// <summary>Whether endings "native" write CRLF.</summary>
        CrlfLineEndings: bool
    }

let private hex (bytes: byte array) =
    bytes
    |> Array.map (fun b -> (int b).ToString("x2"))
    |> String.concat ""

let private parse (text: string) =
    Array.init (text.Length / 2) (fun i -> Convert.ToByte (text.Substring (i * 2, 2), 16))

/// <summary>
/// Checks Blob, File and FileList construction, slicing, reads, owner-bound read cancellation, text and data URLs, a
/// FileList through a file input and the File entries of FormData on one provider; the result is the assertion count.
/// The expected values are the Chromium 156 oracle of Ranvier.WebCore tests/fixtures/binary-values-oracle.json.
/// </summary>
let run (document: Document) (create: unit -> BinaryValueService) (forms: unit -> FormDataService) (driver: Driver) =
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

        let read (blob: Blob) =
            async {
                let result = ref None
                use _ = service.ReadBytes (blob, fun outcome -> result.Value <- Some outcome)

                while result.Value.IsNone do
                    do! driver.Settle ()

                return
                    match result.Value.Value with
                    | ResourceOutcome.Succeeded bytes -> bytes
                    | ResourceOutcome.Failed failure -> failwith $"read failed: %A{failure}"
            }

        let describe (blob: Blob) =
            async {
                let! bytes = read blob
                equal "size equals the bytes read" (float blob.size) (float bytes.Length)
                return float blob.size, blob.``type``, hex bytes
            }

        let file (blob: Blob) =
            let file = unbox<File> blob
            file.name, float file.lastModified

        let blobOf parts type' =
            service.CreateBlob (
                parts,
                { BlobOptions.Default with
                    Type = type'
                }
            )

        // Construction.
        let inner = blobOf [| BlobPart.Text "ab" |] ""

        let parts =
            blobOf
                [|
                    BlobPart.Text "héllo"
                    BlobPart.Bytes [| 0uy; 1uy; 255uy |]
                    BlobPart.Blob inner
                    BlobPart.Text (string (char (int "55296")) + "x")
                |]
                "Text/Plain"

        let! value = describe parts
        equal "parts" (15.0, "text/plain", "68c3a96c6c6f0001ff6162efbfbd78") value
        let! value = describe (blobOf [||] "")
        equal "empty" (0.0, "", "") value
        let! value = describe (blobOf [||] "a/b")
        equal "empty parts with a type" (0.0, "a/b", "") value
        let! value = describe (blobOf [| BlobPart.Bytes [| 2uy; 3uy; 4uy |] |] "")
        equal "bytes" (3.0, "", "020304") value

        for type', expected in
            [
                "Text/Plain", "text/plain"
                "text/é", ""
                "a\u007fb", ""
                "  ", "  "
                "text/plain;charset=UTF-8", "text/plain;charset=utf-8"
                "", ""
            ] do
            equal $"type {type'}" expected (blobOf [||] type').``type``

        let lines = [| BlobPart.Text "a\nb\r\nc\rd" |]
        let! transparent = read (service.CreateBlob (lines, BlobOptions.Default))
        equal "endings transparent" "610a620d0a630d64" (hex transparent)

        let! native =
            read (
                service.CreateBlob (
                    lines,
                    { BlobOptions.Default with
                        Endings = BlobLineEndings.Native
                    }
                )
            )

        equal
            "endings native"
            (if driver.CrlfLineEndings then
                 "610d0a620d0a630d0a64"
             else
                 "610a620a630a64")
            (hex native)

        // Slicing.
        let digits = blobOf [| BlobPart.Text "0123456789" |] "text/plain"

        for label, slice, expected in
            [
                "slice()", digits.slice (), (10.0, "", "30313233343536373839")
                "slice(1, 3)", digits.slice (1L, 3L), (2.0, "", "3132")
                "slice(-2)", digits.slice (-2L), (2.0, "", "3839")
                "slice(0, -1)", digits.slice (0L, -1L), (9.0, "", "303132333435363738")
                "slice(3, 1)", digits.slice (3L, 1L), (0.0, "", "")
                "slice(type)", digits.slice (contentType = "X/Y"), (10.0, "x/y", "30313233343536373839")
                "slice(100, 200)", digits.slice (100L, 200L), (0.0, "", "")
                "slice(-100)", digits.slice (-100L), (10.0, "", "30313233343536373839")
                "slice(2, 8, invalid type)", digits.slice (2L, 8L, "text/é"), (6.0, "", "323334353637")
                "slice of a slice", (digits.slice (2L, 8L)).slice(1L, -1L), (4.0, "", "33343536")
            ] do
            let! value = describe slice
            equal label expected value

        // Files.
        let abc =
            service.CreateFile (
                [| BlobPart.Text "abc" |],
                "a/b.txt",
                { FileOptions.Default with
                    Type = "TEXT/X"
                    LastModified = Some 1234L
                }
            )

        let! value = describe abc
        equal "File bytes" (3.0, "text/x", "616263") value
        equal "File name and lastModified" ("a/b.txt", 1234.0) (file abc)

        let negative =
            service.CreateFile (
                [||],
                "n",
                { FileOptions.Default with
                    LastModified = Some -5L
                }
            )

        equal "negative lastModified" ("n", -5.0) (file negative)
        let before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ()
        let now = service.CreateFile ([||], "now", FileOptions.Default)
        let after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ()

        equal
            "default lastModified is now"
            true
            (float now.lastModified >= float before - 1000.0
             && float now.lastModified <= float after + 1000.0)

        let outer =
            service.CreateFile (
                [|
                    BlobPart.Blob (
                        service.CreateFile (
                            [| BlobPart.Text "xy" |],
                            "inner",
                            { FileOptions.Default with
                                LastModified = Some 1L
                            }
                        )
                    )
                |],
                "outer",
                { FileOptions.Default with
                    LastModified = Some 2L
                }
            )

        let! value = describe outer
        equal "File of a File" (2.0, "", "7879") value
        equal "File of a File metadata" ("outer", 2.0) (file outer)
        equal "a File slice is a Blob" "" ((abc.slice (0L, 2L)).``type``)

        let big = Array.init (1 <<< 20) (fun i -> byte ((i * 31 + 7) &&& 255))
        let! back = read (blobOf [| BlobPart.Bytes big |] "")
        let mutable checksum = 0.0

        for b in back do
            checksum <- (checksum * 33.0 + float b) % 4294967296.0

        equal "one MiB checksum" (1048576, 3271032832.0) (back.Length, checksum)

        // Decodings.
        for label, bytes, expected in
            [
                "text with a byte order mark", "efbbbf41", "A"
                "text with an invalid byte", "ff41", "�A"
                "text with a truncated sequence", "e282", "�"
                "text with a surrogate sequence", "eda080", "���"
                "ascii text", "6869", "hi"
            ] do
            equal label expected (BinaryValueContract.text (parse bytes))

        equal "typed data URL" "data:text/plain;base64,aGk=" (BinaryValueContract.dataUrl "text/plain" (parse "6869"))
        equal "untyped data URL" "data:application/octet-stream;base64,aGk=" (BinaryValueContract.dataUrl "" (parse "6869"))
        equal "empty data URL" "data:application/octet-stream;base64," (BinaryValueContract.dataUrl "" [||])

        equal
            "data URL with parameters"
            "data:text/plain;charset=utf-8;base64,aGk="
            (BinaryValueContract.dataUrl "text/plain;charset=utf-8" (parse "6869"))

        // Owner-bound reads.
        let hi = blobOf [| BlobPart.Text "hi" |] "text/plain"
        let received = Collections.Generic.List<string>()

        let record label =
            fun (outcome: ResourceOutcome<string>) ->
                match outcome with
                | ResourceOutcome.Succeeded value -> received.Add $"{label} {value}"
                | ResourceOutcome.Failed failure -> received.Add $"{label} %A{failure}"

        BlobReads.text service hi (record "text")
        |> ignore

        BlobReads.dataUrl service hi (record "dataUrl")
        |> ignore

        let cancelled = BlobReads.text service hi (record "disposed")
        cancelled.Dispose ()

        Ranvier.Api.createRoot (fun root ->
            BlobReads.text service hi (record "owner")
            |> ignore

            root.Dispose ())

        while received.Count < 2 do
            do! driver.Settle ()

        do! driver.Settle ()
        do! driver.Settle ()

        equal "owner-bound reads deliver; disposed reads are skipped" [ "text hi"; "dataUrl data:text/plain;base64,aGk=" ] (List.ofSeq received)

        let outcome = ref None
        let operation = service.ReadBytes (hi, fun value -> outcome.Value <- Some value)
        operation.Cancel ()
        equal "a cancelled read delivers Cancelled" (Some (ResourceOutcome<byte array>.Failed ResourceFailure.Cancelled)) outcome.Value
        operation.Dispose ()

        // FileList through a file input, and FormData File entries.
        let form = document.createElement "form"
        let input = unbox<HTMLInputElement>(document.createElement "input")
        input.``type`` <- "file"
        input.name <- "upload"
        form.appendChild input |> ignore
        document.body.appendChild form |> ignore
        use formService = forms ()

        let emptyEntry =
            match formService.Get (formService.FromForm (unbox<HTMLFormElement> form, null), "upload") with
            | Some (FormDataEntryValue.File value) -> value
            | other -> failwith $"empty file input: %A{other}"

        let! value = describe emptyEntry
        equal "empty file input entry" (0.0, "application/octet-stream", "") value
        equal "empty file input entry name" "" emptyEntry.name
        equal "empty file input FileList" 0u input.files.length

        let one =
            service.CreateFile (
                [| BlobPart.Text "one" |],
                "one.txt",
                { FileOptions.Default with
                    Type = "text/plain"
                    LastModified = Some 10L
                }
            )

        let two =
            service.CreateFile (
                [| BlobPart.Bytes [| 9uy; 8uy |] |],
                "two.bin",
                { FileOptions.Default with
                    LastModified = Some 20L
                }
            )

        input.files <- service.CreateFileList [| one; two |]
        let files = input.files
        equal "FileList length" 2u files.length
        equal "FileList item 0 keeps the File" true (obj.ReferenceEquals (files.item 0u, one))
        equal "FileList item 1 keeps the File" true (obj.ReferenceEquals (files.item 1u, two))
        equal "FileList item 2" true (isNull (box (files.item 2u)))
        let! value = describe (files.item 1u)
        equal "FileList item bytes" (2.0, "", "0908") value

        let entries =
            formService.Entries (formService.FromForm (unbox<HTMLFormElement> form, null))

        for index, expected in [ 0, (one, (3.0, "text/plain", "6f6e65")); 1, (two, (2.0, "", "0908")) ] do
            match entries[index].Value with
            | FormDataEntryValue.File value ->
                let source, bytes = expected
                equal $"form entry {index} keeps the File" true (obj.ReferenceEquals (value, source))
                let! read = describe value
                equal $"form entry {index} reads through the binary service" bytes read
            | other -> failwith $"form entry {index}: %A{other}"

        let data = formService.Create ()
        let plain = blobOf [| BlobPart.Text "p" |] "text/p"
        data.append ("a", plain)
        data.append ("b", plain, "named.txt")
        data.append ("c", one)
        data.append ("d", one, "renamed")
        data.set ("e", blobOf [||] "")

        for key, name, bytes, same in
            [
                "a", "blob", (1.0, "text/p", "70"), false
                "b", "named.txt", (1.0, "text/p", "70"), false
                "c", "one.txt", (3.0, "text/plain", "6f6e65"), true
                "d", "renamed", (3.0, "text/plain", "6f6e65"), false
                "e", "blob", (0.0, "", ""), false
            ] do
            match formService.Get (data, key) with
            | Some (FormDataEntryValue.File value) ->
                equal $"FormData {key} name" name value.name
                equal $"FormData {key} identity" same (obj.ReferenceEquals (value, one))
                let! read = describe value
                equal $"FormData {key} bytes" bytes read
            | other -> failwith $"FormData {key}: %A{other}"

        match formService.Get (data, "d") with
        | Some (FormDataEntryValue.File value) -> equal "a renamed File keeps lastModified" 10.0 (float value.lastModified)
        | other -> failwith $"FormData d: %A{other}"

        document.body.removeChild form |> ignore

        // Argument errors and disposal.
        fails "null parts" (fun () ->
            service.CreateBlob (null, BlobOptions.Default)
            |> ignore)

        fails "null Blob part" (fun () ->
            service.CreateBlob ([| BlobPart.Blob null |], BlobOptions.Default)
            |> ignore)

        fails "null name" (fun () ->
            service.CreateFile ([||], null, FileOptions.Default)
            |> ignore)

        fails "null File in a list" (fun () -> service.CreateFileList [| null |] |> ignore)
        fails "null read" (fun () -> service.ReadBytes (null, ignore) |> ignore)
        let disposed = create ()
        disposed.Dispose ()

        fails "disposed service" (fun () ->
            disposed.CreateBlob ([||], BlobOptions.Default)
            |> ignore)

        let! value = describe parts
        equal "a value outlives its service" (15.0, "text/plain", "68c3a96c6c6f0001ff6162efbfbd78") value
        return checks
    }
