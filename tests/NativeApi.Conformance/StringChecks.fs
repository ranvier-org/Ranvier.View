module internal NativeApiConformance.StringChecks

open Ranvier.Dom

/// <summary>
/// Checks that strings holding U+0000 reach the DOM whole: plain string arguments, strings in sequences and unions,
/// dictionary string fields, and strings in container results; the result is the assertion count. The expected values
/// are Chromium 156's (Ranvier.WebCore tests/fixtures/string-nul-oracle.json).
/// </summary>
let run (document: Document) (createEvents: unit -> EventDetailService) (createBinary: unit -> BinaryValueService) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"U+0000 round trip {label}: expected %A{expected}, got %A{actual}"

    let element () =
        let value = document.createElement "div"
        document.body.appendChild value |> ignore
        value

    // Plain string arguments.
    let attributes = element ()
    attributes.setAttribute ("data-v", "a\u0000b")
    equal "setAttribute value" "a\u0000b" (attributes.getAttribute "data-v")
    attributes.setAttribute ("data-w", "é\u0000")
    equal "setAttribute trailing U+0000" "é\u0000" (attributes.getAttribute "data-w")
    attributes.id <- "i\u0000d"
    equal "id" "i\u0000d" (attributes.getAttribute "id")
    attributes.setAttributeNS (null, "data-n", "n\u0000s")
    equal "setAttributeNS with a null namespace" "n\u0000s" (attributes.getAttributeNS (null, "data-n"))
    let content = element ()
    content.textContent <- "x\u0000y\u0000z"
    equal "textContent" "x\u0000y\u0000z" content.textContent
    content.textContent <- null
    equal "textContent null" "" content.textContent
    equal "createTextNode" "\u0000lead" (document.createTextNode "\u0000lead").data
    equal "createComment" "c\u0000omment" (document.createComment "c\u0000omment").data
    let text = document.createTextNode "a"
    text.appendData "\u0000tail"
    equal "appendData" "a\u0000tail" text.data

    // Strings in sequences and unions.
    let tokens = element ()
    tokens.classList.add "a\u0000b"
    equal "classList.add" "a\u0000b" (tokens.getAttribute "class")
    tokens.setAttribute ("class", "a\u0000b a")
    tokens.classList.remove "a\u0000b"
    equal "classList.remove" "a" (tokens.getAttribute "class")
    let hidden = unbox<HTMLElement> (element ())
    hidden.hidden <- Some (BooleanOrUnrestrictedDoubleOrDOMString.DOMString "until-found\u0000x")
    equal "hidden with a suffix after U+0000" "" (hidden.getAttribute "hidden")
    hidden.hidden <- Some (BooleanOrUnrestrictedDoubleOrDOMString.DOMString "until-found")
    equal "hidden until-found" "until-found" (hidden.getAttribute "hidden")

    // Strings in container results.
    let names = element ()
    names.setAttribute ("data-é", "1")
    names.setAttribute ("b", "2")
    equal "getAttributeNames" [| "data-é"; "b" |] (names.getAttributeNames ())
    equal "hidden getter" (Some (BooleanOrUnrestrictedDoubleOrDOMString.DOMString "until-found")) hidden.hidden

    // Dictionary string fields.
    use events = createEvents ()
    let error = events.CreateErrorEvent ("error", EventOptions.Default, { ErrorFields.Default with Message = "m\u0000sg" })
    equal "ErrorEventInit.message" "m\u0000sg" error.message
    use binary = createBinary ()
    let blob = binary.CreateBlob ([||], { BlobOptions.Default with Type = "text/plain\u0000x" })
    equal "BlobPropertyBag.type with U+0000" "" blob.``type``
    let plain = binary.CreateBlob ([||], { BlobOptions.Default with Type = "Text/Plain" })
    equal "BlobPropertyBag.type" "text/plain" plain.``type``
    checks
