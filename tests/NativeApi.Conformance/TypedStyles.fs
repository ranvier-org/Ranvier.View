module internal NativeApiConformance.TypedStyles

open System
open Ranvier.Css
open Ranvier.Css.Dom
open Ranvier.Dom

/// <summary>Checks typed authored declarations and restoration against the live browser or native style engine.</summary>
/// <param name="document">The live document; the fixture removes its elements before returning.</param>
/// <param name="computed">The borrowed provider used to inspect actual cascade results.</param>
/// <param name="errorName">Reads the engine's DOM exception name.</param>
let run (document: Document) (computed: ComputedStyleService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let nullRejected label action =
        let mutable rejected = false

        try
            action ()
        with :? ArgumentNullException ->
            rejected <- true

        equal label true rejected

    let parent = document.createElement "div" :?> HTMLElement
    let element = document.createElement "div" :?> HTMLElement
    parent.style.setProperty ("width", "200px", "")
    parent.appendChild element |> ignore
    document.body.appendChild parent |> ignore

    try
        let style = element.style
        style.cssText <- "margin: 1px 2px !important; color: rgb(1, 2, 3); --original: held;"
        let original = style.cssText
        let snapshot = StyleDeclaration.capture style
        let widthValue = Css.px 42.
        let width = Css.width widthValue |> Declaration.important
        equal "typed property name" "width" (Declaration.name width)
        equal "typed serialized value" "42px" (Declaration.value width)
        equal "typed importance" true (Declaration.isImportant width)

        let metadata = (widthValue :> Ranvier.Css.Generated.CssGrammar.IValue).Data.Numeric
        equal "numeric magnitude and unit" (Some (42., "px")) metadata
        StyleDeclaration.apply style width
        equal "typed inline value and priority" ("42px", "important") (StyleDeclaration.read style width)
        let resolved = computed.Get (element, None)
        equal "typed declaration computed result" "42px" (resolved.getPropertyValue "width")

        StyleDeclaration.applyMany style [ Css.width (Css.px 11.); Css.width (Css.px 22.); Css.opacity (Css.number 0.5) ]
        equal "ordered repeated declaration" ("22px", "") (StyleDeclaration.read style width)
        equal "normal write removes importance" "" (style.getPropertyPriority "width")
        equal "live typed computed width" "22px" (resolved.getPropertyValue "width")
        equal "typed unitless opacity" "0.5" (resolved.getPropertyValue "opacity")
        StyleDeclaration.apply style (Css.width CssValue.CssWide.inherit')
        equal "CSS-wide keyword uses actual cascade" "200px" (resolved.getPropertyValue "width")
        StyleDeclaration.apply style (Css.width (Css.percent 25.))
        equal "typed percentage serialization" "25%" (style.getPropertyValue "width")
        equal "typed percentage computed result" "50px" (resolved.getPropertyValue "width")
        StyleDeclaration.apply style (Css.display CssValue.Display.block)
        equal "typed literal keyword" "block" (resolved.getPropertyValue "display")

        let variable = Css.variable<CssValue.Width.Value> "--typed-width"
        StyleDeclaration.apply style (CssValue.Variable.declaration variable (Css.px 64.))
        StyleDeclaration.apply style (Css.width variable)
        equal "typed custom-property value" "64px" (style.getPropertyValue "--typed-width")
        equal "typed custom-property substitution" "64px" (resolved.getPropertyValue "width")

        style.setProperty ("margin-left", "9px", "")
        style.setProperty ("--original", "changed", "important")
        StyleDeclaration.restore style snapshot
        equal "full snapshot restores shorthand order" original style.cssText
        equal "snapshot restores shorthand value" "2px" (style.getPropertyValue "margin-left")
        equal "snapshot restores shorthand priority" "important" (style.getPropertyPriority "margin-left")
        equal "snapshot restores custom property" ("held", "") (style.getPropertyValue "--original", style.getPropertyPriority "--original")
        equal "snapshot removes added property" ("", "") (StyleDeclaration.read style width)
        equal "snapshot removes added custom property" "" (style.getPropertyValue "--typed-width")

        style.cssText <- ""
        let empty = StyleDeclaration.capture style
        StyleDeclaration.apply style width
        StyleDeclaration.restore style empty
        equal "empty snapshot restores property absence" "" style.cssText
        equal "empty snapshot restores zero properties" 0u style.length

        let mutable writeError = ""

        try
            StyleDeclaration.apply resolved width
        with error ->
            writeError <- errorName error

        equal "typed sink preserves read-only error" "NoModificationAllowedError" writeError
        let mutable restoreError = ""

        try
            StyleDeclaration.restore resolved empty
        with error ->
            restoreError <- errorName error

        equal "typed restore preserves read-only error" "NoModificationAllowedError" restoreError
        nullRejected "null style rejected" (fun () -> StyleDeclaration.apply Unchecked.defaultof<CSSStyleDeclaration> width)
        nullRejected "null sequence sink rejected" (fun () -> StyleDeclaration.applyMany Unchecked.defaultof<CSSStyleDeclaration> [])

        nullRejected "null read sink rejected" (fun () ->
            StyleDeclaration.read Unchecked.defaultof<CSSStyleDeclaration> width
            |> ignore)

        nullRejected "null capture sink rejected" (fun () ->
            StyleDeclaration.capture Unchecked.defaultof<CSSStyleDeclaration>
            |> ignore)

        nullRejected "null restore sink rejected" (fun () -> StyleDeclaration.restore Unchecked.defaultof<CSSStyleDeclaration> empty)
        nullRejected "null declaration rejected" (fun () -> StyleDeclaration.apply style Unchecked.defaultof<Declaration>)

        nullRejected "null read declaration rejected" (fun () ->
            StyleDeclaration.read style Unchecked.defaultof<Declaration>
            |> ignore)

        nullRejected "null declaration sequence rejected" (fun () -> StyleDeclaration.applyMany style Unchecked.defaultof<seq<Declaration>>)
        nullRejected "null snapshot rejected" (fun () -> StyleDeclaration.restore style Unchecked.defaultof<StyleSnapshot>)

        nullRejected "null mid-sequence declaration rejected" (fun () ->
            StyleDeclaration.applyMany style [ Css.width (Css.px 19.); Unchecked.defaultof<Declaration> ])

        equal "earlier declaration survives null mid-sequence" "19px" (style.getPropertyValue "width")
        let mutable enumerationFailed = false

        try
            StyleDeclaration.applyMany
                style
                (seq {
                    yield Css.width (Css.px 17.)
                    raise (InvalidOperationException "enumeration failed")
                })
        with :? InvalidOperationException ->
            enumerationFailed <- true

        equal "enumerator failure propagates" true enumerationFailed
        equal "earlier declaration survives enumerator failure" "17px" (style.getPropertyValue "width")
        checks
    finally
        document.body.removeChild parent |> ignore
