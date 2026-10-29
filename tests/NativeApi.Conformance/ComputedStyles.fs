module internal NativeApiConformance.ComputedStyles

open System
open Ranvier.Dom

/// <summary>Checks live computed declarations, cascade and pseudo-element results through both engine providers.</summary>
/// <param name="document">The live engine document; fixture elements and rules are removed before returning.</param>
/// <param name="create">Creates a borrowed provider for this document.</param>
/// <param name="errorName">Reads the engine's DOM exception name.</param>
let run (document: Document) (create: unit -> ComputedStyleService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let argumentError label action =
        let mutable rejected = false

        try
            action ()
        with :? ArgumentException ->
            rejected <- true

        equal label true rejected

    let parent = document.createElement "div" :?> HTMLElement
    let element = document.createElement "div" :?> HTMLElement
    let plain = document.createElement "div" :?> HTMLElement
    let sheet = document.createElement "style" :?> HTMLStyleElement
    parent.style.setProperty ("color", "rgb(1,2,3)", "")
    parent.style.setProperty ("--inherited", "parent-value", "")
    element.id <- "computed-style-probe"
    element.textContent <- "probe"

    sheet.textContent <-
        "#computed-style-probe { width:120px; opacity:.5; --probe:held; }"
        + "#computed-style-probe::before { content:'before'; width:11px; opacity:.25; }"
        + "#computed-style-probe::after { content:'after'; width:22px; opacity:.75; }"

    parent.appendChild element |> ignore
    parent.appendChild plain |> ignore
    document.head.appendChild sheet |> ignore
    document.body.appendChild parent |> ignore

    try
        let service = create ()
        let baseline = service.Get (element, None)

        for property, expected in
            [|
                "width", "120px"
                "opacity", "0.5"
                "color", "rgb(1, 2, 3)"
                "--probe", "held"
                "--inherited", "parent-value"
                "--missing", ""
            |] do
            equal $"computed cascade {property}" expected (baseline.getPropertyValue property)

        equal "computed declaration exposes properties" true (baseline.length > 0u)

        for pseudo in [ Some ""; Some null; Some "no-colon" ] do
            equal "element fallback" "120px" ((service.Get (element, pseudo)).getPropertyValue "width")

        for pseudo, width, opacity, content in
            [|
                "::before", "11px", "0.25", "\"before\""
                ":before", "11px", "0.25", "\"before\""
                "::after", "22px", "0.75", "\"after\""
            |] do
            let declaration = service.Get (element, Some pseudo)
            equal $"{pseudo} width" width (declaration.getPropertyValue "width")
            equal $"{pseudo} opacity" opacity (declaration.getPropertyValue "opacity")
            equal $"{pseudo} content" content (declaration.getPropertyValue "content")

        for pseudo in
            [
                "::not-real"
                "::before("
                "::part(foo)"
                "::slotted(*)"
                ":hover"
                "::before\u0000junk"
            ] do
            let declaration = service.Get (element, Some pseudo)
            equal $"{pseudo} empty declaration" 0u declaration.length
            equal $"{pseudo} empty value" "" (declaration.getPropertyValue "width")

        let absent = service.Get (plain, Some "::before")

        for property, expected in [| "content", "none"; "opacity", "1"; "width", "auto" |] do
            equal $"absent pseudo {property}" expected (absent.getPropertyValue property)

        let marker = service.Get (element, Some "::marker")
        equal "marker opacity" "1" (marker.getPropertyValue "opacity")

        element.style.setProperty ("color", "rgb(9,8,7)", "")
        element.style.setProperty ("opacity", "0.6", "")
        equal "live inline color" "rgb(9, 8, 7)" (baseline.getPropertyValue "color")
        equal "inline overrides stylesheet" "0.6" (baseline.getPropertyValue "opacity")
        parent.style.setProperty ("--inherited", "updated-parent", "")
        equal "live inherited custom property" "updated-parent" (baseline.getPropertyValue "--inherited")

        let mutable writeError = ""

        try
            baseline.setProperty ("opacity", "1", "")
        with error ->
            writeError <- errorName error

        equal "computed declaration rejects writes" "NoModificationAllowedError" writeError
        equal "failed write preserves value" "0.6" (baseline.getPropertyValue "opacity")

        argumentError "null element rejected" (fun () ->
            service.Get (Unchecked.defaultof<Element>, None)
            |> ignore)

        let otherDocument = document.implementation.createHTMLDocument ("foreign")
        let foreign = otherDocument.createElement "div"
        argumentError "foreign document element rejected" (fun () -> service.Get (foreign, None) |> ignore)
        checks
    finally
        document.body.removeChild parent |> ignore
        document.head.removeChild sheet |> ignore
