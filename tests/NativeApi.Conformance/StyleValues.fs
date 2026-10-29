module internal NativeApiConformance.StyleValues

open System
open Ranvier.Dom

/// <summary>Checks engine-backed Typed OM parsing, construction, identity and mutable value state.</summary>
/// <param name="create">Creates a borrowed value provider for a live document.</param>
/// <param name="errorName">Reads observable provider validation and disposal errors.</param>
/// <param name="disposedError">The provider-specific observable disposal error.</param>
/// <param name="subtypes">Reads actual engine image and numeric subtype membership.</param>
let run (create: unit -> StyleValueService) (errorName: exn -> string) disposedError (subtypes: CSSStyleValue -> bool * bool) =
    let highSurrogate = string (char 0xd800)
    let lowSurrogate = string (char 0xdc00)
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    let reject label expected action =
        let mutable outcome = "accepted"

        try
            action ()
        with
        | StyleValueException (name, _) -> outcome <- name
        | error -> outcome <- errorName error

        equal label expected outcome

    let stringSegment (value: CSSUnparsedValue) index =
        match value.item index with
        | Some (USVStringOrCSSOMVariableReferenceValue.USVString text) -> text
        | _ -> failwith "Expected a string segment"

    let referenceSegment (value: CSSUnparsedValue) index =
        match value.item index with
        | Some (USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue reference) -> reference
        | _ -> failwith "Expected a variable-reference segment"

    use service = create ()
    let unitValue = service.Parse ("width", "12px") :?> CSSUnitValue
    equal "unit parsed number" 12.0 unitValue.value
    equal "unit parsed name" "px" unitValue.unit
    equal "unit parsed serialization" "12px" (unitValue.toString ())
    unitValue.value <- 8.5
    equal "unit numeric mutation" "8.5px" (unitValue.toString ())
    let keyword = service.Parse ("display", "block") :?> CSSKeywordValue
    equal "keyword parsed value" "block" keyword.value
    keyword.value <- "inline"
    equal "keyword mutation serialization" "inline" (keyword.toString ())
    let image = service.Parse ("background-image", "url(example.png)")
    equal "actual image subtype" true (subtypes image |> fst)
    equal "image serialization" true ((image.toString ()).Contains("example.png"))
    let opaque = service.Parse ("margin", "1px 2px")
    equal "opaque shorthand serialization" "1px 2px" (opaque.toString ())
    let numeric = service.Parse ("width", "calc(1px + 2%)")
    equal "opaque numeric subtype" true (subtypes numeric |> snd)
    equal "opaque numeric serialization" "calc(1px + 2%)" (numeric.toString ())
    let values = service.ParseAll ("animation-name", "first, second")
    equal "parseAll length" 2 values.Length
    equal "parseAll first" "first" (values[0].toString())
    equal "parseAll second" "second" (values[1].toString())
    equal "parse first item" "first" (service.Parse("animation-name", "first, second").toString())
    let retainedFirst = values[0]
    values[0] <- values[1]
    equal "array mutation leaves parsed value" "first" (retainedFirst.toString ())

    for property, text in
        [
            "width", "invalid-width"
            "not-a-property", "red"
            "width", ""
            null, "1px"
            "width", null
        ] do
        reject "parse rejects invalid input" "TypeError" (fun () -> service.Parse (property, text) |> ignore)
        reject "parseAll rejects invalid input" "TypeError" (fun () -> service.ParseAll (property, text) |> ignore)

    equal "parser recovery" "7px" (service.Parse("width", "7px").toString())

    let parsed =
        service.Parse ("--c14", "before var(--nested, red) after") :?> CSSUnparsedValue

    equal "parsed custom structure" 3u parsed.length
    let parsedReference = referenceSegment parsed 1u
    equal "parsed variable name" "--nested" parsedReference.variable
    equal "parsed fallback tokens" "red" ((parsedReference.fallback.toString ()).Trim())
    same "parsed variable stable identity" parsedReference (referenceSegment parsed 1u)
    same "parsed fallback stable identity" parsedReference.fallback parsedReference.fallback

    let fallback =
        service.CreateUnparsed [| USVStringOrCSSOMVariableReferenceValue.USVString "red" |]

    let reference = service.CreateVariableReference ("--x", Some fallback)
    same "constructed fallback identity" fallback reference.fallback

    let tokens =
        service.CreateUnparsed
            [|
                USVStringOrCSSOMVariableReferenceValue.USVString "before"
                USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue reference
                USVStringOrCSSOMVariableReferenceValue.USVString "a\u0000b"
            |]

    equal "constructed length" 3u tokens.length
    same "constructed reference identity" reference (referenceSegment tokens 1u)
    equal "embedded NUL survives result" "a\u0000b" (stringSegment tokens 2u)
    equal "missing segment" None (tokens.item 3u)
    tokens.setItem (0u, "changed\u0000tail")
    equal "embedded NUL survives setter" "changed\u0000tail" (stringSegment tokens 0u)
    tokens.setItem (2u, reference)
    same "reference overload identity" reference (referenceSegment tokens 2u)
    reject "out of range segment" "RangeError" (fun () -> tokens.setItem (8u, "invalid"))
    equal "invalid indexed mutation length" 3u tokens.length
    reject "maximum index string mutation" "RangeError" (fun () -> tokens.setItem (UInt32.MaxValue, "invalid"))
    reject "maximum index reference mutation" "RangeError" (fun () -> tokens.setItem (UInt32.MaxValue, reference))
    equal "maximum index preserves length" 3u tokens.length
    same "maximum index preserves reference" reference (referenceSegment tokens 1u)
    tokens.setItem (3u, "appended")
    equal "string append length" 4u tokens.length
    equal "string append value" "appended" (stringSegment tokens 3u)
    tokens.setItem (4u, reference)
    equal "reference append length" 5u tokens.length
    same "reference append identity" reference (referenceSegment tokens 4u)
    reference.variable <- "--renamed"
    equal "reference setter" "--renamed" (referenceSegment tokens 1u).variable
    reject "invalid reference mutation" "TypeError" (fun () -> reference.variable <- "invalid")
    equal "invalid reference preserves name" "--renamed" reference.variable

    for optional in [ None; Some Unchecked.defaultof<CSSUnparsedValue> ] do
        let empty = service.CreateVariableReference ("--no-fallback", optional)
        equal "nullable fallback" true (isNull (box empty.fallback))

    reject "invalid reference construction" "TypeError" (fun () ->
        service.CreateVariableReference ("invalid", None)
        |> ignore)

    let madeKeyword = service.CreateKeyword "custom"
    equal "keyword factory" "custom" madeKeyword.value
    reject "empty keyword" "TypeError" (fun () -> service.CreateKeyword "" |> ignore)
    let madeUnit = service.CreateUnit (2.5, "px")
    equal "unit factory number" 2.5 madeUnit.value
    equal "unit factory serialization" "2.5px" (madeUnit.toString ())
    reject "unknown unit" "TypeError" (fun () -> service.CreateUnit (1., "not-a-unit") |> ignore)

    for invalid in [ Double.NaN; Double.PositiveInfinity; Double.NegativeInfinity ] do
        reject "nonfinite constructor" "TypeError" (fun () -> service.CreateUnit (invalid, "px") |> ignore)
        reject "nonfinite setter" "TypeError" (fun () -> madeUnit.value <- invalid)
        equal "nonfinite preserves number" 2.5 madeUnit.value

    for input, expected in
        [
            null, "null"
            "", ""
            "a\u0000b", "a\u0000b"
            highSurrogate, "\ufffd"
            lowSurrogate, "\ufffd"
            "\ud83d\ude00", "\ud83d\ude00"
        ] do
        let value =
            service.CreateUnparsed [| USVStringOrCSSOMVariableReferenceValue.USVString input |]

        equal "factory USV conversion" expected (stringSegment value 0u)
        value.setItem (0u, input)
        equal "setter USV conversion" expected (stringSegment value 0u)

    let nullKeyword = service.CreateKeyword null
    equal "keyword factory null text" "null" nullKeyword.value
    nullKeyword.value <- highSurrogate
    equal "keyword setter USV" "\ufffd" nullKeyword.value
    madeKeyword.value <- null
    equal "keyword setter null text" "null" madeKeyword.value

    let surrogateReference =
        service.CreateVariableReference ("--" + highSurrogate, None)

    equal "reference factory USV" "--\ufffd" surrogateReference.variable
    surrogateReference.variable <- "--" + lowSurrogate
    equal "reference setter USV" "--\ufffd" surrogateReference.variable
    equal "parse custom NUL replacement" "a\ufffdb" (service.Parse("--c14", "a\u0000b").toString())
    equal "parse custom USV replacement" "a\ufffdb" (service.Parse("--c14", "a" + highSurrogate + "b").toString())
    let parsedAllTokens = service.ParseAll ("--c14", "a" + lowSurrogate + "b")
    equal "parseAll custom USV replacement" "a\ufffdb" (parsedAllTokens[0].toString())

    let rejected action =
        try
            action ()
            false
        with _ ->
            true

    equal "null segment array rejected" true (rejected (fun () -> service.CreateUnparsed null |> ignore))

    equal
        "null union container rejected"
        true
        (rejected (fun () ->
            service.CreateUnparsed [| Unchecked.defaultof<_> |]
            |> ignore))

    equal
        "null reference arm rejected"
        true
        (rejected (fun () ->
            service.CreateUnparsed
                [|
                    USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue Unchecked.defaultof<_>
                |]
            |> ignore))

    equal "null setter reference rejected" true (rejected (fun () -> tokens.setItem (1u, Unchecked.defaultof<CSSVariableReferenceValue>)))
    same "invalid null setter preserves identity" reference (referenceSegment tokens 1u)
    service.Dispose ()
    equal "disposed provider leaves existing unit" "2.5px" (madeUnit.toString ())
    madeUnit.value <- 3.
    equal "existing unit still mutable" "3px" (madeUnit.toString ())
    same "disposed provider preserves fallback identity" fallback reference.fallback
    reject "disposed parser" disposedError (fun () -> service.Parse ("width", "1px") |> ignore)
    reject "disposed factory" disposedError (fun () -> service.CreateKeyword "block" |> ignore)
    checks
