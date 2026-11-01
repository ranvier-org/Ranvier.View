module internal NativeApiConformance.AriaChecks

open Ranvier.Dom

/// <summary>The engine whose measured differences a check expects.</summary>
type Engine =
    | WebKit
    | Chromium

/// <summary>
/// Checks the reflected ARIA element relationships of the shared Element contract against the Ranvier.WebCore oracle
/// tests/fixtures/aria-relations-oracle.json: element arrays, the content attributes they set, attribute replacement,
/// removed and shadow-root targets, clearing, and repeated reads.
/// </summary>
/// <remarks>
/// WebKit keeps a duplicate in a reflected array, and gates ariaActionsElements behind a setting that every WebKit
/// configuration leaves off, so that member raises.
/// </remarks>
let run (document: Document) (engine: Engine) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected %A{expected}, got %A{actual}"

    let element (tag: string) (id: string) (text: string) =
        let created = document.createElement tag
        created.setAttribute ("id", id)
        created.textContent <- text
        document.body.appendChild created |> ignore
        created

    let ids (value: Element array) =
        if isNull value then
            None
        else
            Some (value |> Array.map (fun e -> e.id) |> List.ofArray)

    let l1 = element "span" "aria-l1" "First"
    let l2 = element "span" "aria-l2" "Second"
    let d1 = element "p" "aria-d1" "Describe"
    let d2 = element "p" "aria-d2" "More"
    let x1 = element "p" "aria-x1" "Detail"
    let button = element "button" "aria-b" "Plain"
    let list = element "div" "aria-list" ""
    let option = element "div" "aria-o1" "One"
    list.appendChild option |> ignore
    let host = element "div" "aria-host" ""
    let init = ShadowRootInit ()
    init.mode <- ShadowRootMode.Open
    let root = host.attachShadow init
    let inner = document.createElement "span"
    inner.setAttribute ("id", "aria-inner")
    root.appendChild inner |> ignore

    button.ariaLabelledByElements <- [| l2; l1 |]
    equal "labelledby elements" (Some [ "aria-l2"; "aria-l1" ]) (ids button.ariaLabelledByElements)
    equal "setting elements empties the attribute" "" (button.getAttribute "aria-labelledby")
    equal "repeated reads" (ids button.ariaLabelledByElements) (ids button.ariaLabelledByElements)
    button.ariaLabelledByElements <- [| l2; l1; l2 |]

    equal
        "a duplicate element"
        (Some (
            match engine with
            | WebKit -> [ "aria-l2"; "aria-l1"; "aria-l2" ]
            | Chromium -> [ "aria-l2"; "aria-l1" ]
        ))
        (ids button.ariaLabelledByElements)

    button.setAttribute ("aria-labelledby", "aria-l1")
    equal "the attribute replaces the elements" (Some [ "aria-l1" ]) (ids button.ariaLabelledByElements)
    button.ariaDescribedByElements <- [| d2; d1 |]
    equal "describedby elements" (Some [ "aria-d2"; "aria-d1" ]) (ids button.ariaDescribedByElements)
    d2.remove ()
    equal "a removed target leaves the reflection" (Some [ "aria-d1" ]) (ids button.ariaDescribedByElements)
    button.ariaLabelledByElements <- [| inner |]
    equal "a shadow-root target is not reflected" (Some []) (ids button.ariaLabelledByElements)
    button.ariaDescribedByElements <- null
    equal "null clears the elements" None (ids button.ariaDescribedByElements)
    equal "null removes the attribute" null (button.getAttribute "aria-describedby")
    button.ariaDetailsElements <- [| x1 |]
    equal "details elements" (Some [ "aria-x1" ]) (ids button.ariaDetailsElements)
    button.ariaFlowToElements <- [| x1 |]
    equal "flowto elements" (Some [ "aria-x1" ]) (ids button.ariaFlowToElements)
    button.ariaControlsElements <- [| list |]
    equal "controls elements" (Some [ "aria-list" ]) (ids button.ariaControlsElements)
    button.ariaErrorMessageElements <- [| d1 |]
    equal "errormessage elements" (Some [ "aria-d1" ]) (ids button.ariaErrorMessageElements)
    button.ariaControlsElements <- [||]
    equal "an empty array" (Some []) (ids button.ariaControlsElements)
    equal "an empty array empties the attribute" "" (button.getAttribute "aria-controls")
    list.ariaActiveDescendantElement <- option
    equal "activedescendant element" "aria-o1" list.ariaActiveDescendantElement.id

    let actions =
        try
            button.ariaActionsElements <- [| l1 |]
            ids button.ariaActionsElements
        with _ ->
            Some [ "raised" ]

    equal
        "actions elements"
        (Some (
            match engine with
            | WebKit -> [ "raised" ]
            | Chromium -> [ "aria-l1" ]
        ))
        actions

    for created in [ l1; l2; d1; x1; button; list; host ] do
        created.remove ()

    checks
