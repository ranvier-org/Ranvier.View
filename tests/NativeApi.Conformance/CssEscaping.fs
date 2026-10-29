module internal NativeApiConformance.CssEscaping

open Ranvier.Dom

let private codeUnit (value: int) =
    string (char value)

/// <summary>Checks CSS identifier serialization and selector matching through shared contracts.</summary>
/// <param name="document">The live engine document; fixture nodes are removed before returning.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let high = codeUnit 0xd800
    let low = codeUnit 0xdfff

    let cases =
        [|
            null, "null"
            "", ""
            "-", "\\-"
            "--", "--"
            "-a", "-a"
            "_", "_"
            "0", "\\30 "
            "9x", "\\39 x"
            "-0", "-\\30 "
            "-9x", "-\\39 x"
            "a0", "a0"
            "a b", "a\\ b"
            "a#b", "a\\#b"
            "a.b", "a\\.b"
            "a:b", "a\\:b"
            "a[b]", "a\\[b\\]"
            "a\\b", "a\\\\b"
            "\u0000", "\ufffd"
            "a\u0000b", "a\ufffdb"
            "\u0001", "\\1 "
            "a\u0009f", "a\\9 f"
            "\u000a", "\\a "
            "\u000d", "\\d "
            "\u001f", "\\1f "
            "\u007f", "\\7f "
            "a\u0080b", "a\u0080b"
            "\u00a0", "\u00a0"
            "\u03bb", "\u03bb"
            "\U0001f600", "\U0001f600"
            "\u200e", "\u200e"
            "\uffff", "\uffff"
            high, high
            low, low
            "-" + high, "-" + high
            "\U0001f6001", "\U0001f6001"
        |]

    for index in 0 .. cases.Length - 1 do
        let input, expected = cases[index]
        equal $"CSS escape vector {index}" expected (CSS.escape input)

    let probe = document.createElement "div" :?> HTMLElement
    document.body.appendChild probe |> ignore

    try
        for identifier in
            [|
                "0hello"
                "-1hello"
                "a b"
                "a#b"
                "a.b"
                "a:b"
                "a[b]"
                "a\\b"
                "\u03bb\U0001f600"
                "with\u0001control"
            |] do
            probe.id <- identifier
            let found = document.querySelector ("#" + CSS.escape identifier)
            equal "CSS escaped selector round trip" true (obj.ReferenceEquals (probe, found))
    finally
        document.body.removeChild probe |> ignore

    checks
