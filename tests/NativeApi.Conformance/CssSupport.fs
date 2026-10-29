module internal NativeApiConformance.CssSupport

open Ranvier.Dom

/// <summary>Checks declaration and condition parsing through the shared CSS query contract.</summary>
/// <param name="service">The live document's engine-backed provider.</param>
let run (service: CssQueryService) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if actual <> expected then
            failwith $"{label}: expected {expected}, got {actual}"

    for property, value, expected in
        [|
            "display", "block", true
            "display", "not-a-display-value", false
            "color", "rgb(1 2 3)", true
            "not-a-real-property", "red", false
            "", "red", false
            "color", "", false
            "--probe", "42", true
            "--\u03c0", "42", true
            "width", "calc(1px + 2px)", true
            "width", "var(--probe)", true
            "display\u0000unknown", "block", false
            "display", "block\u0000unknown", false
            null, "red", false
            "--probe", null, true
            "display", null, false
            "DISPLAY", "BLOCK", true
            " color", "red", false
            "color", "red !important", false
        |] do
        equal $"declaration {property}:{value}" expected (service.Supports (property, value))

    for text, expected in
        [|
            "", false
            null, false
            "(display: block)", true
            "(display: not-a-display-value)", false
            "not (display: not-a-display-value)", true
            "(display: block) and (color: red)", true
            "(no-such-property: 1)", false
            "(display: block) or (no-such-property: 1)", true
            "selector(:is(.probe))", true
            "selector(:not-a-real-pseudo)", false
            "(display: block)\u0000junk", false
            "display: block", true
            "((display: block))", true
            "(display: block) and", false
        |] do
        equal $"condition {text}" expected (service.Supports text)

    checks
