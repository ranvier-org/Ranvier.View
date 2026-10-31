module internal NativeApiConformance.FormChecks

open System
open Ranvier.Dom

// The D13 producer fixture markup (Ranvier.WebCore tests/fixtures/form-collections-cases.json). The forms submit
// into the sink frame, so a browser submission leaves the page in place.
let private markup =
    "<form id=\"f\" name=\"main\" action=\"about:blank\" target=\"forms-sink\"><input id=\"a1\" name=\"a\" value=\"1\"><input id=\"a2\" name=\"a\" value=\"2\"><input id=\"idonly\" value=\"i\"><input id=\"x\" name=\"y\" value=\"xy\"><input id=\"y\" name=\"z\" value=\"yz\"><fieldset id=\"fs\"><input id=\"inner\" name=\"inner\" value=\"in\"><output id=\"out\" name=\"out\">o</output></fieldset><input id=\"dis\" name=\"dis\" value=\"d\" disabled><input id=\"cb\" type=\"checkbox\" name=\"cb\" value=\"on\" checked><input id=\"cb2\" type=\"checkbox\" name=\"cb2\" value=\"off\"><input id=\"file\" type=\"file\" name=\"upload\"><input id=\"img\" type=\"image\" name=\"img\"><select id=\"s\" name=\"s\"><option id=\"o1\" value=\"v1\">One</option><optgroup id=\"g\" label=\"G\"><option id=\"o2\" name=\"two\">Two</option></optgroup><option id=\"o3\" value=\"v3\" selected>Three</option></select><textarea id=\"ta\" name=\"ta\">text</textarea><button id=\"b1\" name=\"go\" value=\"one\">Go</button><button id=\"b2\" type=\"button\" name=\"nb\">No</button><button id=\"b3\" type=\"reset\" name=\"rb\">Reset</button><input id=\"sub\" type=\"submit\" name=\"s2\" value=\"Send\"></form><input id=\"outside\" name=\"a\" value=\"3\" form=\"f\"><form id=\"f2\" action=\"about:blank\" target=\"forms-sink\"><button id=\"other\" name=\"other\">Other</button></form><p id=\"p\"></p><iframe name=\"forms-sink\"></iframe>"

let private initial =
    [ "a", "1"; "a", "2"; "y", "xy"; "z", "yz"; "inner", "in"; "cb", "on"; "upload", "<file>"; "s", "v3"; "ta", "text" ]

/// <summary>The entry list of the fixture form, with the submitter's entry when it is given.</summary>
let private formEntries (submitter: (string * string) option) =
    initial @ Option.toList submitter @ [ ("a", "3") ]

let private text (value: FormDataEntryValue) =
    match value with
    | FormDataEntryValue.String value -> value
    | FormDataEntryValue.File _ -> "<file>"

/// <summary>
/// Checks form control and option collections, FormData construction and entry access, engine-fired SubmitEvent
/// and FormDataEvent, constructed events and service disposal on one provider; the result is the assertion count.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>TypeError</c>.</param>
let run (document: Document) (create: unit -> FormDataService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected %A{expected}, got %A{actual}"

    let fails label (expected: string) (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                None
            with error ->
                Some (errorName error)

        if name <> Some expected then
            failwith $"{label}: expected {expected}, got %A{name}"

    let host = document.createElement "div"
    host.setAttribute ("id", "forms-host")
    document.body.appendChild host |> ignore
    host.innerHTML <- markup

    let node (id: string) = document.getElementById id
    let idOf (element: Element) = if isNull element then null else element.id
    let label (element: Element) = if isNull element then "-" else element.id
    let flag value = if value then "T" else "F"

    let ids (collection: HTMLCollection) =
        [ for index in 0 .. int collection.length - 1 -> (collection.item (uint32 index)).id ]

    let form = unbox<HTMLFormElement> (node "f")
    let form2 = unbox<HTMLFormElement> (node "f2")
    let select = unbox<HTMLSelectElement> (node "s")
    let element (id: string) = unbox<HTMLElement> (node id)

    let named (name: string) =
        match form.elements.namedItem name with
        | None -> "none"
        | Some (RadioNodeListOrElement.Element value) -> "element " + value.id
        | Some (RadioNodeListOrElement.RadioNodeList list) ->
            "list "
            + String.Join (",", [ for index in 0 .. int list.length - 1 -> (unbox<Element> (list.item (uint32 index))).id ])

    let service = create ()

    let entries (formData: DOMFormData) =
        service.Entries formData
        |> Array.map (fun entry -> entry.Name, text entry.Value)
        |> List.ofArray

    try
        // Form controls collection: tree order, form-attribute association, indexed and named lookup.
        equal
            "elements"
            [ "a1"; "a2"; "idonly"; "x"; "y"; "fs"; "inner"; "out"; "dis"; "cb"; "cb2"; "file"; "s"; "ta"; "b1"; "b2"; "b3"; "sub"; "outside" ]
            (ids form.elements)

        equal "elements.length" 19u form.elements.length
        equal "elements[0]" "a1" (idOf (form.elements.item 0u))
        equal "elements[99]" null (idOf (form.elements.item 99u))
        equal "f2 elements" [ "other" ] (ids form2.elements)
        equal "named a" "list a1,a2,outside" (named "a")
        equal "named idonly" "element idonly" (named "idonly")
        equal "named x" "element x" (named "x")
        equal "named y" "list x,y" (named "y")
        equal "named z" "element y" (named "z")
        equal "named inner" "element inner" (named "inner")
        equal "named img" "none" (named "img")
        equal "named missing" "none" (named "missing")
        equal "named empty" "none" (named "")

        // Options collection: indexed, named and selected lookup, then edits.
        let listed = select.options
        equal "options" [ "o1"; "o2"; "o3" ] (ids listed)
        equal "options.length" 3u listed.length
        equal "selectedIndex" 2 listed.selectedIndex
        equal "options[1]" "o2" (idOf (listed.item 1u))
        equal "options[9]" null (idOf (listed.item 9u))
        equal "options named two" "o2" (idOf (listed.namedItem "two"))
        equal "options named o1" "o1" (idOf (listed.namedItem "o1"))
        equal "options named v1" null (idOf (listed.namedItem "v1"))

        let edit = document.createElement "select"
        edit.innerHTML <- "<option id=\"e1\" value=\"v1\">One</option><optgroup><option id=\"e2\">Two</option></optgroup><option id=\"e3\" selected>Three</option>"
        (node "p").appendChild edit |> ignore
        let options = (unbox<HTMLSelectElement> edit).options

        let option (id: string) =
            let value = unbox<HTMLOptionElement> (document.createElement "option")
            value.id <- id
            value.textContent <- id
            value

        fails "options.add before a grouped option" "NotFoundError" (fun () -> options.add (option "n0", element "e2"))
        options.add (option "n1", element "e3")
        options.add (option "n2", 0)
        options.add (option "n3")
        fails "options.add before a foreign node" "NotFoundError" (fun () -> options.add (option "n5", element "p"))
        equal "options after add" [ "n2"; "e1"; "e2"; "n1"; "e3"; "n3" ] (ids options)
        options.remove 0
        options.length <- 2u
        equal "options after truncate" [ "e1"; "e2" ] (ids options)
        options.length <- 4u
        equal "options after grow" 4u options.length
        options.setItem (3u, option "n6")
        equal "options after setItem" "n6" (idOf (options.item 3u))
        options.selectedIndex <- 1
        equal "selectedIndex set" 1 options.selectedIndex
        options.selectedIndex <- -1
        equal "selectedIndex cleared" -1 options.selectedIndex

        // Engine-fired events from requestSubmit and submit.
        let events = Collections.Generic.List<string>()
        let mutable prevent = false

        let onSubmit: EventListener =
            fun event ->
                let submit = unbox<SubmitEvent> event

                events.Add $"submit {flag event.isTrusted}{flag event.bubbles}{flag event.cancelable} {label submit.submitter}"

                if prevent then
                    event.preventDefault ()

        let onFormData: EventListener =
            fun event ->
                let data = (unbox<FormDataEvent> event).formData
                let list = entries data |> List.map (fun (name, value) -> $"{name}={value}")
                events.Add $"""formdata {flag event.isTrusted}{flag event.bubbles}{flag event.cancelable} {String.Join (";", list)}"""

        let line (entries: (string * string) list) =
            String.Join (";", entries |> List.map (fun (name, value) -> $"{name}={value}"))

        form.addEventListener ("submit", onSubmit)
        form.addEventListener ("formdata", onFormData)

        let drain () =
            let result = List.ofSeq events
            events.Clear ()
            result

        form.requestSubmit ()

        equal
            "requestSubmit()"
            [ "submit TTT -"; $"formdata TTF {line (formEntries None)}" ]
            (drain ())

        form.requestSubmit (element "b1")

        equal
            "requestSubmit(b1)"
            [ "submit TTT b1"; $"""formdata TTF {line (formEntries (Some ("go", "one")))}""" ]
            (drain ())

        form.requestSubmit (element "sub")

        equal
            "requestSubmit(sub)"
            [ "submit TTT sub"; $"""formdata TTF {line (formEntries (Some ("s2", "Send")))}""" ]
            (drain ())

        prevent <- true
        form.requestSubmit (element "b1")
        equal "prevented submit fires no formdata" [ "submit TTT b1" ] (drain ())
        prevent <- false
        fails "requestSubmit(button)" "TypeError" (fun () -> form.requestSubmit (element "b2"))
        fails "requestSubmit(reset)" "TypeError" (fun () -> form.requestSubmit (element "b3"))
        fails "requestSubmit(text input)" "TypeError" (fun () -> form.requestSubmit (element "a1"))
        fails "requestSubmit(other form)" "NotFoundError" (fun () -> form.requestSubmit (element "other"))
        equal "rejected submitters fire nothing" [] (drain ())
        form.submit ()
        equal "submit() fires formdata only" [ $"formdata TTF {line (formEntries None)}" ] (drain ())

        // FormData construction and entry access.
        let fromForm = service.FromForm (form, null)
        equal "FromForm fires formdata" [ $"formdata TTF {line (formEntries None)}" ] (drain ())
        equal "FromForm entries" (formEntries None) (entries fromForm)
        let withSubmitter = service.FromForm (form, element "b1")
        drain () |> ignore
        equal "FromForm(b1) entries" (formEntries (Some ("go", "one"))) (entries withSubmitter)
        fails "FromForm(button)" "TypeError" (fun () -> service.FromForm (form, element "b2") |> ignore)
        fails "FromForm(other form)" "NotFoundError" (fun () -> service.FromForm (form, element "other") |> ignore)
        equal "rejected FromForm fires nothing" [] (drain ())

        let file =
            match service.Get (fromForm, "upload") with
            | Some (FormDataEntryValue.File file) -> file
            | other -> failwith $"upload: expected a File entry, got %A{other}"

        let unsupported =
            try
                FormDataContract.readFile file |> ignore
                false
            with :? NotSupportedException ->
                true

        equal "File contents are unsupported" true unsupported

        fromForm.append ("upload", "text")
        equal "getAll mixed" [ "<file>"; "text" ] (service.GetAll (fromForm, "upload") |> Array.map text |> List.ofArray)

        let data = service.Create ()
        equal "empty entries" [] (entries data)
        data.append ("k", "1")
        data.append ("k", "2")
        data.append ("café", "ü\u0000x")
        equal "get" (Some (FormDataEntryValue.String "1")) (service.Get (data, "k"))
        equal "getAll" [| FormDataEntryValue.String "1"; FormDataEntryValue.String "2" |] (service.GetAll (data, "k"))
        equal "has" true (data.has "k")
        equal "get non-ASCII" (Some (FormDataEntryValue.String "ü\u0000x")) (service.Get (data, "café"))
        data.set ("k", "3")
        equal "set" [ "k", "3"; "café", "ü\u0000x" ] (entries data)
        data.delete "k"
        equal "delete has" false (data.has "k")
        equal "delete get" None (service.Get (data, "k"))
        equal "delete getAll" [||] (service.GetAll (data, "k"))

        // Listener edits reach the submitted entry list.
        form.removeEventListener ("formdata", onFormData)

        let onEdit: EventListener =
            fun event ->
                let data = (unbox<FormDataEvent> event).formData
                data.append ("extra", "e")
                data.delete "a"
                data.set ("ta", "T")

        form.addEventListener ("formdata", onEdit)
        let edited = service.FromForm (form, element "sub")

        equal
            "listener edits"
            [ "y", "xy"; "z", "yz"; "inner", "in"; "cb", "on"; "upload", "<file>"; "s", "v3"; "ta", "T"; "s2", "Send"; "extra", "e" ]
            (entries edited)

        form.removeEventListener ("formdata", onEdit)
        form.removeEventListener ("submit", onSubmit)

        // Constructed events are untrusted and carry their init members.
        let submit =
            service.CreateSubmitEvent ("submit", { EventOptions.Default with Cancelable = true }, element "b1")

        equal "SubmitEvent" ("submit", false, false, true, "b1") (submit.``type``, submit.isTrusted, submit.bubbles, submit.cancelable, idOf submit.submitter)
        let custom = service.CreateSubmitEvent ("custom", { EventOptions.Default with Bubbles = true }, null)
        equal "SubmitEvent without submitter" ("custom", true, null) (custom.``type``, custom.bubbles, idOf custom.submitter)
        let held = service.Create ()
        held.append ("k", "v")
        let constructed = service.CreateFormDataEvent ("formdata", { EventOptions.Default with Bubbles = true }, held)
        equal "FormDataEvent" ("formdata", false, true) (constructed.``type``, constructed.isTrusted, constructed.bubbles)
        equal "FormDataEvent.formData" [ ("k", "v") ] (entries constructed.formData)
        held.append ("k2", "w")
        equal "FormDataEvent shares its FormData" [ "k", "v"; "k2", "w" ] (entries constructed.formData)
        let mutable seen = []
        let onConstructed: EventListener = fun event -> seen <- entries (unbox<FormDataEvent> event).formData
        form.addEventListener ("formdata", onConstructed)
        equal "dispatch constructed FormDataEvent" true (form.dispatchEvent constructed)
        equal "dispatched FormDataEvent entries" [ "k", "v"; "k2", "w" ] seen
        form.removeEventListener ("formdata", onConstructed)

        checks <- checks + 1

        try
            service.CreateFormDataEvent ("formdata", EventOptions.Default, null) |> ignore
            failwith "CreateFormDataEvent accepted a null FormData"
        with :? ArgumentNullException ->
            ()
    finally
        host.remove ()

    // Disposal is idempotent and stops the service.
    service.Dispose ()
    service.Dispose ()
    checks <- checks + 1

    let stopped =
        try
            service.Create () |> ignore
            false
        with _ ->
            true

    if not stopped then
        failwith "A disposed FormData service created a FormData"

    checks
