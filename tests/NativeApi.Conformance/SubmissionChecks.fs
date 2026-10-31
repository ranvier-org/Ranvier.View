module internal NativeApiConformance.SubmissionChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>Task delivery the fixture drives.</summary>
type Driver =
    {
        /// <summary>Lets queued tasks run, so pending submissions reach the handlers.</summary>
        Settle: unit -> Async<unit>
    }

// A subset of the D14 producer fixture markup (Ranvier.WebCore tests/fixtures/form-submission-cases.json), placed in a
// container. Every action is on https://app.test/, which no run loads.
let private markup =
    "<form id=\"g\" action=\"https://app.test/search#frag\" method=\"get\"><input name=\"q\" value=\"a b&amp;c=d\"><input type=\"checkbox\" name=\"cb\" value=\"on\" checked><input name=\"dis\" value=\"x\" disabled><button id=\"go\" name=\"go\" value=\"one\">Go</button><button id=\"plain\" name=\"o\" value=\"v\" formaction=\"https://app.test/other?k=1\" formmethod=\"post\" formenctype=\"text/plain\">P</button><button id=\"blank\" formtarget=\"_blank\">B</button></form><form id=\"p\" action=\"https://app.test/post\" method=\"post\"><input name=\"name\" value=\"&#233; &#20013;+\"><textarea name=\"ta\">line1\nline2</textarea><select name=\"s\"><option value=\"1\">One</option><option value=\"2\" selected>Two</option></select><input id=\"pgo\" type=\"submit\" name=\"pgo\" value=\"Send\"></form><form id=\"m\" action=\"https://app.test/multi\" method=\"post\" enctype=\"multipart/form-data\"><input name=\"mt\" value=\"text\"><textarea name=\"q&quot;x\">two\nlines</textarea></form><form id=\"v\" action=\"https://app.test/valid\" method=\"post\"><input name=\"req\" required><button id=\"vgo\">V</button><button id=\"vskip\" formnovalidate>S</button></form><form id=\"c\" action=\"https://app.test/cancel\"><input name=\"c\" value=\"1\"></form><form id=\"one\" action=\"https://app.test/one\" target=\"_self\"><input name=\"only\" value=\"v\"></form><dialog id=\"d\" open><form id=\"df\" method=\"dialog\"><button id=\"dok\" value=\"ok\">ok</button></form></dialog><p id=\"marker\">kept</p>"

let private eventTypes = [ "submit"; "formdata"; "invalid" ]

/// <summary>
/// Checks GET, urlencoded, text/plain and multipart encodings, submitter overrides, targets, cancellation,
/// interactive validation, coalescing of two submissions of a form in one task, dialog forms, subscriptions and
/// service lifetime on one provider; the result is the assertion count.
/// </summary>
let run (document: Document) (create: unit -> FormSubmissionService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        // Providers raise ArgumentNullException, InvalidOperationException and ObjectDisposedException; the native
        // lifetime checks assert the types, which Fable erases.
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

        let host = document.createElement "div"
        host.setAttribute ("id", "submission-host")
        document.body.appendChild host |> ignore
        host.innerHTML <- markup

        let node (id: string) =
            document.getElementById id

        let form (id: string) =
            unbox<HTMLFormElement>(node id)

        let button (id: string) =
            unbox<HTMLElement>(node id)

        let marker = node "marker"
        let url = document.baseURI
        let events = List<string>()

        let listener: EventListener =
            fun event ->
                let target = unbox<Element> event.target
                events.Add (event.``type`` + ":" + target.id)

        for kind in eventTypes do
            document.addEventListener (kind, listener, true)

        let service = create ()
        let received = List<string>()

        let boundary (contentType: string option) =
            match contentType with
            | Some value when value.StartsWith "multipart/form-data; boundary=" -> Some (value.Substring 30)
            | _ -> None

        let describe (submission: FormSubmission) =
            let body = Text.Encoding.UTF8.GetString submission.Body

            let body, contentType =
                match boundary submission.ContentType with
                | Some value -> body.Replace (value, "BOUNDARY"), "multipart/form-data; boundary=BOUNDARY"
                | None -> body, defaultArg submission.ContentType "-"

            let submitter =
                match submission.Submitter with
                | Some element -> element.id
                | None -> "-"

            $"{submission.Method} {submission.Url} target={submission.Target} type={contentType} body={body} form={submission.Form.id} submitter={submitter}"

        let subscription =
            service.Subscribe (fun submission -> received.Add (describe submission))

        let step (action: unit -> unit) =
            async {
                events.Clear ()
                received.Clear ()
                action ()
                let during = List.ofSeq received
                do! driver.Settle ()
                return List.ofSeq events, during, List.ofSeq received
            }

        let! events1, during, submissions =
            step (fun () -> (form "g").requestSubmit(button "go"))

        equal "GET events" [ "submit:g"; "formdata:g" ] events1
        equal "submissions wait for a later task" [] during

        equal
            "GET with submitter"
            [
                "GET https://app.test/search?q=a+b%26c%3Dd&cb=on&go=one#frag target= type=- body= form=g submitter=go"
            ]
            submissions

        let! _, _, submissions = step (fun () -> (form "g").requestSubmit(button "plain"))

        equal
            "text/plain submitter override"
            [
                "POST https://app.test/other?k=1 target= type=text/plain body=q=a b&c=d\r\ncb=on\r\no=v\r\n form=g submitter=plain"
            ]
            submissions

        let! _, _, submissions = step (fun () -> (form "g").requestSubmit(button "blank"))

        equal
            "formtarget _blank"
            [
                "GET https://app.test/search?q=a+b%26c%3Dd&cb=on#frag target=_blank type=- body= form=g submitter=blank"
            ]
            submissions

        let! _, _, submissions = step (fun () -> (button "pgo").click())

        equal
            "UTF-8 urlencoded POST by a click"
            [
                "POST https://app.test/post target= type=application/x-www-form-urlencoded body=name=%C3%A9+%E4%B8%AD%2B&ta=line1%0D%0Aline2&s=2&pgo=Send form=p submitter=pgo"
            ]
            submissions

        let! events1, _, submissions = step (fun () -> (form "m").submit())
        equal "submit() fires formdata and no submit event" [ "formdata:m" ] events1

        equal
            "multipart POST by submit()"
            [
                "POST https://app.test/multi target= type=multipart/form-data; boundary=BOUNDARY body=--BOUNDARY\r\nContent-Disposition: form-data; name=\"mt\"\r\n\r\ntext\r\n--BOUNDARY\r\nContent-Disposition: form-data; name=\"q%22x\"\r\n\r\ntwo\r\nlines\r\n--BOUNDARY--\r\n form=m submitter=-"
            ]
            submissions

        let cancel: EventListener = fun event -> event.preventDefault ()

        (form "c").addEventListener("submit", cancel)
        let! events1, _, submissions = step (fun () -> (form "c").requestSubmit())
        equal "cancelled submit events" [ "submit:c" ] events1
        equal "a cancelled submit event submits nothing" [] submissions
        (form "c").removeEventListener("submit", cancel)

        let! events1, _, submissions =
            step (fun () -> (form "v").requestSubmit(button "vgo"))

        equal
            "invalid form fires invalid"
            [ "invalid:" ]
            (events1
             |> List.map (fun item -> item.Substring (0, item.IndexOf ':' + 1)))

        equal "an invalid form submits nothing" [] submissions
        let! _, _, submissions = step (fun () -> (form "v").requestSubmit(button "vskip"))

        equal
            "formnovalidate submits"
            [
                "POST https://app.test/valid target= type=application/x-www-form-urlencoded body=req= form=v submitter=vskip"
            ]
            submissions

        let! events1, _, submissions =
            step (fun () ->
                (form "one").requestSubmit()
                (node "one").querySelector("input").setAttribute("value", "w")
                (form "one").requestSubmit())

        equal "two submissions in one task fire both events" [ "submit:one"; "formdata:one"; "submit:one"; "formdata:one" ] events1

        equal
            "the later submission of a form replaces the pending one"
            [
                "GET https://app.test/one?only=w target=_self type=- body= form=one submitter=-"
            ]
            submissions

        let dialog = unbox<HTMLDialogElement>(node "d")
        let! _, _, submissions = step (fun () -> (button "dok").click())
        equal "a dialog form submits nothing" [] submissions
        equal "a dialog form closes its dialog" (false, "ok") (dialog.``open``, dialog.returnValue)

        let second = List<string>()
        let extra = service.Subscribe (fun submission -> second.Add submission.Form.id)
        subscription.Dispose ()
        let! _, _, submissions = step (fun () -> (form "c").requestSubmit())
        equal "a disposed subscription receives nothing" [] submissions
        equal "a later subscription receives the submission" [ "c" ] (List.ofSeq second)
        extra.Dispose ()
        let! events1, _, _ = step (fun () -> (form "c").requestSubmit())
        equal "without handlers the events still fire" [ "submit:c"; "formdata:c" ] events1
        equal "without handlers the submission is dropped" [ "c" ] (List.ofSeq second)

        raises "a null handler" (fun () ->
            service.Subscribe (Unchecked.defaultof<_>)
            |> ignore)

        raises "a second service of the document" (fun () -> create () |> ignore)

        equal "the document stays" (url, true, "kept") (document.baseURI, marker.isConnected, marker.textContent)

        service.Dispose ()
        service.Dispose ()
        raises "Subscribe after disposal" (fun () -> service.Subscribe ignore |> ignore)
        let next = create ()
        let later = List<string>()
        use _ = next.Subscribe (fun submission -> later.Add submission.Form.id)
        let! _, _, _ = step (fun () -> (form "c").requestSubmit())
        equal "a service after disposal reports" [ "c" ] (List.ofSeq later)
        next.Dispose ()

        for kind in eventTypes do
            document.removeEventListener (kind, listener, true)

        document.body.removeChild host |> ignore
        return checks
    }
