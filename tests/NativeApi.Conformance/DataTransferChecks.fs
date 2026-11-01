module internal NativeApiConformance.DataTransferChecks

open Ranvier.Dom

/// <summary>How a provider constructs DataTransfers and Files, and delivers a drop and a paste to a page.</summary>
type Driver =
    {
        /// <summary>A new read/write DataTransfer.</summary>
        Create: unit -> DataTransfer
        /// <summary>A File named <c>name</c> of type <c>kind</c> holding the UTF-8 bytes of <c>text</c>.</summary>
        File: string -> string -> string -> File
        /// <summary>Drops the strings and Files on the element; its drop listeners have run when it returns.</summary>
        Drop: Element -> struct (string * string) array -> File array -> unit
        /// <summary>Pastes the strings and Files into the focused element; its paste listeners have run when it returns.</summary>
        Paste: Element -> struct (string * string) array -> File array -> unit
    }

/// <summary>What a listener read from an event's DataTransfer.</summary>
type private Snapshot =
    {
        Types: string array
        Text: string
        Html: string
        Files: (string * string * float) array
        Items: (string * string) array
    }

let private snapshot (transfer: DataTransfer) =
    let files = transfer.files

    {
        Types = transfer.types
        Text = transfer.getData "text/plain"
        Html = transfer.getData "text/html"
        Files =
            Array.init (int files.length) (fun index ->
                let file = files.item (uint32 index)
                file.name, file.``type``, float file.size)
        Items =
            let items = transfer.items

            Array.init items.length (fun index ->
                let item = items.item (uint32 index)
                item.kind, item.``type``)
    }

/// <summary>
/// Checks DataTransfer construction and its data, effect, item list and file members, and the DataTransfer a drop
/// and a paste carry; the result is the assertion count. The expected values are the Chromium 156 oracle of
/// Ranvier.WebCore tests/fixtures/data-transfer-oracle.json, outside its recorded engine differences.
/// </summary>
let run (document: Document) (driver: Driver) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith (
                label
                + ": expected "
                + string (box expected)
                + ", got "
                + string (box actual)
            )

    let sequence label (expected: 'T array) (actual: 'T array) =
        equal (label + " length") expected.Length actual.Length

        for index in 0 .. expected.Length - 1 do
            equal (label + " " + string index) expected[index] actual[index]

    let fails label (action: unit -> unit) =
        checks <- checks + 1

        let failed =
            try
                action ()
                false
            with _ ->
                true

        if not failed then
            failwith (label + ": expected an exception")

    // Construction: a read/write DataTransfer of strings and a File.
    let transfer = driver.Create ()
    equal "initial dropEffect" "none" transfer.dropEffect
    sequence "initial types" [||] transfer.types
    transfer.dropEffect <- "bogus"
    equal "an unknown dropEffect is ignored" "none" transfer.dropEffect
    transfer.setData ("Text", "a")
    transfer.setData ("text/html", "<b>h</b>")
    sequence "types after setData" [| "text/plain"; "text/html" |] transfer.types
    equal "Text reads as text/plain" "a" (transfer.getData "text/plain")
    equal "an absent type reads empty" "" (transfer.getData "URL")
    let z = driver.File "z.txt" "text/plain" "zz"
    let items = transfer.items
    let item = items.add z
    equal "items.add(file) is a file item" "file" item.kind
    equal "the file item has the File's type" "text/plain" item.``type``
    equal "getAsFile returns the added File" true (obj.ReferenceEquals (item.getAsFile (), z))
    equal "the item list counts the strings and the file" 3 items.length
    equal "files lists the added File" 1.0 (float transfer.files.length)
    equal "the File's name" "z.txt" (transfer.files.item(0u).name)
    equal "a string item's kind" "string" (items.item(0u).kind)
    fails "a second string of one type throws" (fun () -> items.add ("dup", "text/plain") |> ignore)
    items.remove 0u

    sequence
        "remove drops the first string"
        [| "text/html" |]
        (transfer.types
         |> Array.filter (fun kind -> kind <> "Files"))

    transfer.clearData "text/html"
    equal "clearData(type) removes it" "" (transfer.getData "text/html")
    items.clear ()
    equal "items.clear drops the File" 0.0 (float transfer.files.length)
    equal "items.clear leaves no item" 0 items.length

    // A drop and a paste of strings and Files.
    let target = document.createElement "div"
    target.setAttribute ("style", "position:absolute;left:0;top:0;width:200px;height:80px")
    target.textContent <- "drop"
    let editable = document.createElement "div"
    editable.setAttribute ("contenteditable", "")
    editable.setAttribute ("style", "position:absolute;left:0;top:90px;width:200px;height:40px")
    editable.textContent <- "ab"
    document.body.appendChild target |> ignore
    document.body.appendChild editable |> ignore
    let dropped = ResizeArray<Snapshot>()
    let pasted = ResizeArray<Snapshot>()

    let onDrop: EventListener =
        fun event ->
            event.preventDefault ()
            dropped.Add (snapshot (unbox<DragEvent> event).dataTransfer)

    let onOver: EventListener = fun event -> event.preventDefault ()

    let onPaste: EventListener =
        fun event ->
            event.preventDefault ()
            pasted.Add (snapshot (unbox<ClipboardEvent> event).clipboardData)

    target.addEventListener ("drop", onDrop)
    target.addEventListener ("dragover", onOver)
    editable.addEventListener ("paste", onPaste)

    let strings = [| struct ("text/plain", "XY"); struct ("text/html", "<i>X</i>Y") |]
    let note = driver.File "note.txt" "text/plain" "hello file"
    let table = driver.File "table.csv" "text/csv" "a,b"
    driver.Drop target strings [| note; table |]
    equal "one drop" 1 dropped.Count
    let drop = dropped[0]
    sequence "drop types" [| "text/plain"; "text/html"; "Files" |] drop.Types
    equal "drop text" "XY" drop.Text
    equal "drop HTML" "<i>X</i>Y" drop.Html

    sequence "drop files" [| ("note.txt", "text/plain", 10.0); ("table.csv", "text/csv", 3.0) |] drop.Files

    sequence
        "drop items"
        [|
            ("string", "text/plain")
            ("string", "text/html")
            ("file", "text/plain")
            ("file", "text/csv")
        |]
        drop.Items

    let image = driver.File "image.png" "image/png" "png"
    driver.Paste editable [| struct ("text/plain", "PQ"); struct ("text/html", "<u>P</u>Q") |] [| image |]
    equal "one paste" 1 pasted.Count
    let paste = pasted[0]
    sequence "paste types" [| "text/plain"; "text/html"; "Files" |] paste.Types
    equal "paste text" "PQ" paste.Text
    equal "paste HTML" "<u>P</u>Q" paste.Html
    sequence "paste files" [| ("image.png", "image/png", 3.0) |] paste.Files

    sequence "paste items" [| ("string", "text/plain"); ("string", "text/html"); ("file", "image/png") |] paste.Items

    target.remove ()
    editable.remove ()
    target.removeEventListener ("drop", onDrop)
    target.removeEventListener ("dragover", onOver)
    editable.removeEventListener ("paste", onPaste)
    checks
