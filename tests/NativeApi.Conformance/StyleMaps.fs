module internal NativeApiConformance.StyleMaps

open System
open Ranvier.Dom

/// <summary>Checks actual typed map mutation, computed effects, snapshots and two-sheet ownership isolation.</summary>
/// <param name="document">The live document containing the fixture's elements and stylesheets.</param>
/// <param name="create">Creates a borrowed map provider.</param>
/// <param name="values">Constructs actual typed values for the same engine session.</param>
/// <param name="computed">Reads actual live computed declarations.</param>
/// <param name="checkpoint">Flushes the engine's owning-thread style/layout work.</param>
/// <param name="errorName">Reads observable generated member and provider failures.</param>
/// <param name="disposedError">The provider-specific observable disposal error.</param>
/// <param name="readonlyMutation">Attempts mutation through a read-only map's actual public facade.</param>
/// <param name="readonlyError">The provider-specific read-only rejection.</param>
let run
    (document: Document)
    (create: unit -> StyleMapService)
    (values: StyleValueService)
    (computed: ComputedStyleService)
    (checkpoint: unit -> unit)
    (errorName: exn -> string)
    disposedError
    (readonlyMutation: StylePropertyMapReadOnly -> unit)
    readonlyError
    =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith (sprintf "%s: expected %A, got %A" label expected actual)

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

    let rejected label action =
        let mutable failed = false

        try
            action ()
        with _ ->
            failed <- true

        equal label true failed

    let text value =
        CSSStyleValueOrUSVString.USVString value

    let typed value =
        CSSStyleValueOrUSVString.CSSStyleValue value

    let serialize (value: CSSStyleValue) =
        if isNull (box value) then "<null>" else value.toString ()

    let all (map: StylePropertyMapReadOnly) property =
        map.getAll property |> Array.map serialize

    let width element =
        checkpoint ()
        computed.Get(element, None).getPropertyValue "width"

    let baseStyle = document.createElement "style" :?> HTMLStyleElement
    let style = document.createElement "style" :?> HTMLStyleElement
    let target = document.createElement "div" :?> HTMLElement
    target.id <- "c13-target"
    baseStyle.textContent <- "#c13-target { width:19px; height:29px } #c13-copy { width:19px }"
    style.textContent <- "#c13-target { width:30px; height:40px }"
    document.head.appendChild baseStyle |> ignore
    document.head.appendChild style |> ignore
    document.body.appendChild target |> ignore
    use service = create ()

    try
        let attribute = service.Attribute (target :> ElementCSSInlineStyle)
        same "inline same object" attribute (service.Attribute (target :> ElementCSSInlineStyle))
        equal "empty inline size" 0u attribute.size
        equal "missing get" None (service.Get (attribute, "width"))
        equal "missing getAll" [||] (all attribute "width")
        equal "missing has" false (attribute.has "width")

        equal
            "missing direct get arm"
            true
            (match attribute.get "width" with
             | UndefinedOrCSSStyleValue.Undefined () -> true
             | _ -> false)

        attribute.set ("width", text "20px")
        attribute.set ("height", typed (values.CreateUnit (21., "px")))
        equal "inline width serialization" [| "20px" |] (all attribute "width")
        equal "inline height serialization" [| "21px" |] (all attribute "height")
        equal "inline size" 2u attribute.size
        equal "inline has" true (attribute.has "width")
        equal "inline computed effect" "20px" (width target)
        let readOnly = service.Computed target

        equal
            "computed map width"
            "20px"
            (service.Get (readOnly, "width")
             |> Option.map serialize
             |> Option.defaultValue "missing")

        equal "computed map has width" true (readOnly.has "width")
        equal "computed map size positive" true (readOnly.size > 0u)
        let computedEntries = service.Entries readOnly

        let computedWidth =
            computedEntries
            |> Array.find (fun entry -> entry.Property = "width")

        equal "computed entry width" [| "20px" |] (computedWidth.Values |> Array.map serialize)

        equal
            "computed entry keys unique"
            computedEntries.Length
            (computedEntries
             |> Array.map _.Property
             |> Array.distinct
             |> Array.length)

        reject "computed map rejects mutation" readonlyError (fun () -> readonlyMutation readOnly)
        equal "read-only failure preserves effect" "20px" (width target)
        let returned = service.Get (attribute, "width") |> Option.get :?> CSSUnitValue
        returned.value <- 91.
        equal "returned value mutates independently" "91px" (returned.toString ())
        equal "returned value does not write map" [| "20px" |] (all attribute "width")
        equal "returned value does not change layout" "20px" (width target)
        let incoming = values.CreateUnit (24., "px")
        attribute.set ("width", typed incoming)
        incoming.value <- 92.
        equal "input typed value is independent" [| "24px" |] (all attribute "width")
        equal "input typed value layout" "24px" (width target)

        reject "invalid property get" "TypeError" (fun () ->
            service.Get (attribute, "not-a-property")
            |> ignore)

        reject "invalid property getAll" "TypeError" (fun () -> attribute.getAll "not-a-property" |> ignore)
        reject "invalid property has" "TypeError" (fun () -> attribute.has "not-a-property" |> ignore)
        reject "invalid property set" "TypeError" (fun () -> attribute.set ("not-a-property", text "red"))
        reject "invalid property delete" "TypeError" (fun () -> attribute.delete "not-a-property")
        reject "empty set" "TypeError" (fun () -> attribute.set ("width", [||]))
        reject "wrong dimension" "TypeError" (fun () -> attribute.set ("width", typed (values.CreateUnit (2., "deg"))))
        reject "multiple scalar values" "TypeError" (fun () -> attribute.set ("width", text "1px", text "2px"))
        reject "append scalar" "TypeError" (fun () -> attribute.append ("width", text "1px"))
        reject "append custom property" "TypeError" (fun () -> attribute.append ("--c13-custom", text "red"))
        attribute.append ("not-a-property", [||])
        equal "empty append preserves declarations" [| "24px" |] (all attribute "width")
        reject "associated-property mismatch" "TypeError" (fun () -> attribute.set ("padding", typed (values.Parse ("margin", "1px 2px"))))
        equal "invalid operations preserve effect" "24px" (width target)
        attribute.set ("margin", text "1px 2px")

        equal
            "shorthand serialization"
            "1px 2px"
            (service.Get (attribute, "margin")
             |> Option.map serialize
             |> Option.defaultValue "missing")

        attribute.delete "margin"
        equal "shorthand deletion" false (attribute.has "margin")
        attribute.set ("--c13-custom", text "before var(--fallback, blue) after")

        let custom =
            service.Get (attribute, "--c13-custom")
            |> Option.get
            :?> CSSUnparsedValue

        equal "custom typed shape" true (custom.length > 0u)
        equal "custom serialization" "before var(--fallback, blue) after" (custom.toString ())
        reject "custom typed number restriction" "TypeError" (fun () -> attribute.set ("--c13-custom", typed incoming))
        let nullReference = typed Unchecked.defaultof<CSSStyleValue>
        rejected "null typed reference rejected" (fun () -> attribute.set ("--c13-null-reference", nullReference))
        equal "null reference does not create property" false (attribute.has "--c13-null-reference")

        let forged =
            { new CSSStyleValue with
                member _.toString() = "forged"
            }

        rejected "forged typed reference rejected" (fun () -> attribute.set ("--c13-forged", typed forged))
        equal "forged reference does not create property" false (attribute.has "--c13-forged")
        rejected "null union container rejected" (fun () -> attribute.set ("--c13-null-union", [| Unchecked.defaultof<_> |]))
        rejected "null variadic container rejected" (fun () -> attribute.set ("--c13-null-array", null))
        attribute.set ("--c13-null-string", text null)
        equal "null string conversion" [| "null" |] (all attribute "--c13-null-string")
        attribute.set ("--c13-nul", text "a\u0000b")
        equal "custom parser NUL replacement" [| "a\ufffdb" |] (all attribute "--c13-nul")
        attribute.set ("--c13-surrogate", text ("a" + string (char 0xd800) + "b"))
        equal "custom parser USV replacement" [| "a\ufffdb" |] (all attribute "--c13-surrogate")
        target.style.setProperty ("width", "25px", "important")
        attribute.set ("width", text "26px")
        equal "map removes prior priority" "" (target.style.getPropertyPriority "width")
        equal "map priority computed effect" "26px" (width target)
        target.style.setProperty ("--c13-priority", "red", "important")
        attribute.set ("--c13-priority", text "blue")
        equal "custom map removes prior priority" "" (target.style.getPropertyPriority "--c13-priority")
        equal "custom map priority value" "blue" (target.style.getPropertyValue "--c13-priority")

        let image name =
            "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='1' height='1'%3E%3C/svg%3E#c13-"
            + name
            + "\")"

        attribute.set ("background-image", text (image "first"), text (image "second"))
        attribute.append ("background-image", text (image "third"))
        let images = all attribute "background-image"
        equal "image list cardinality" 3 images.Length

        for index, name in [ 0, "first"; 1, "second"; 2, "third" ] do
            equal "image list ordering" true (images[index].Contains("#c13-" + name))

        let imageEntry =
            service.Entries attribute
            |> Array.find (fun entry -> entry.Property = "background-image")

        equal "entries preserve list values" images (imageEntry.Values |> Array.map serialize)
        attribute.clear ()
        equal "clear size" 0u attribute.size
        equal "clear get" None (service.Get (attribute, "width"))
        equal "clear entries" 0 (service.Entries attribute).Length
        equal "clear exposes declared rule" "30px" (width target)
        attribute.set ("width", text "20px")
        attribute.set ("opacity", text "0.5")
        attribute.set ("height", text "21px")
        let snapshot = service.Entries attribute
        equal "entry key ordering" [| "width"; "opacity"; "height" |] (snapshot |> Array.map _.Property)
        attribute.set ("width", text "22px")
        attribute.set ("display", text "block")
        equal "entries snapshot retains keys" [| "width"; "opacity"; "height" |] (snapshot |> Array.map _.Property)

        let widthEntry =
            snapshot
            |> Array.find (fun entry -> entry.Property = "width")

        equal "entries snapshot value serialization" [| "20px" |] (widthEntry.Values |> Array.map serialize)
        let snapshotUnit = widthEntry.Values[0] :?> CSSUnitValue
        snapshotUnit.value <- 93.
        equal "entry value independently mutable" "93px" (snapshotUnit.toString ())
        equal "entry value mutation leaves map" [| "22px" |] (all attribute "width")
        widthEntry.Values[0] <- values.CreateKeyword "initial"
        equal "snapshot array mutation leaves map" [| "22px" |] (all attribute "width")
        attribute.clear ()
        attribute.set ("--c13-雪", text "before var(--c13-色, blue) after")
        let unicodeSnapshot = service.Entries attribute
        equal "Unicode entry key" [| "--c13-雪" |] (unicodeSnapshot |> Array.map _.Property)
        attribute.clear ()
        equal "Unicode snapshot retains value after clear" [| "before var(--c13-色, blue) after" |] (unicodeSnapshot[0].Values |> Array.map serialize)
        let sheet = style.sheet
        let rule = sheet.cssRules.item 0u :?> CSSStyleRule
        let declared = service.Declared rule
        same "declared same object" declared (service.Declared rule)
        equal "declared initial width" [| "30px" |] (all declared "width")
        declared.set ("width", typed (values.CreateUnit (44., "px")))
        equal "declared CSSOM update" "44px" (rule.style.getPropertyValue "width")
        equal "declared layout update" "44px" (width target)

        equal
            "computed map remains live"
            "44px"
            (service.Get (readOnly, "width")
             |> Option.map serialize
             |> Option.defaultValue "missing")

        sheet.deleteRule 0u
        equal "removed rule retained map" [| "44px" |] (all declared "width")
        declared.set ("width", text "55px")
        equal "detached map edits retained rule" "55px" (rule.style.getPropertyValue "width")
        equal "detached map does not restore cascade" "19px" (width target)
        equal "detached map does not restore rule" 0u sheet.cssRules.length
        declared.clear ()
        equal "detached clear" 0u declared.size
        let copyTarget = document.createElement "div" :?> HTMLElement
        copyTarget.id <- "c13-copy"
        document.body.appendChild copyTarget |> ignore

        try
            let copyCss =
                "#c13-copy { width:var(--c13-size,19px); --c13-size:30px; background-image:"
                + image "first"
                + " }"

            let cases: (string * (StylePropertyMap -> unit) * string * int) list =
                [
                    "set known", (fun map -> map.set ("width", text "44px")), "44px", 1
                    "set custom", (fun map -> map.set ("--c13-size", text "40px")), "40px", 1
                    "append", (fun map -> map.append ("background-image", text (image "second"))), "30px", 2
                    "delete known", (fun map -> map.delete "width"), "19px", 1
                    "delete custom", (fun map -> map.delete "--c13-size"), "19px", 1
                    "clear", (fun map -> map.clear ()), "19px", 0
                ]

            for label, mutate, expectedWidth, imageCount in cases do
                let first = document.createElement "style" :?> HTMLStyleElement
                let second = document.createElement "style" :?> HTMLStyleElement
                first.textContent <- copyCss
                second.textContent <- copyCss
                document.head.appendChild first |> ignore
                document.head.appendChild second |> ignore

                try
                    let firstSheet, secondSheet = first.sheet, second.sheet

                    let firstRule, secondRule =
                        firstSheet.cssRules.item 0u :?> CSSStyleRule, secondSheet.cssRules.item 0u :?> CSSStyleRule

                    let firstMap, secondMap = service.Declared firstRule, service.Declared secondRule
                    let secondText = secondRule.cssText
                    let secondWidth = all secondMap "width"
                    secondSheet.disabled <- true
                    equal (label + " COW baseline") "30px" (width copyTarget)
                    mutate firstMap
                    equal (label + " owning layout") expectedWidth (width copyTarget)
                    equal (label + " image list") imageCount (firstMap.getAll "background-image").Length

                    let cssomChanged =
                        match label with
                        | "set known" -> firstRule.style.getPropertyValue "width" = "44px"
                        | "set custom" -> firstRule.style.getPropertyValue "--c13-size" = "40px"
                        | "append" -> (firstRule.style.getPropertyValue "background-image").Contains "#c13-second"
                        | "delete known" -> firstRule.style.getPropertyValue "width" = ""
                        | "delete custom" -> firstRule.style.getPropertyValue "--c13-size" = ""
                        | "clear" -> firstRule.style.length = 0u
                        | _ -> failwith "Unknown COW mutation case"

                    equal (label + " owning CSSOM") true cssomChanged
                    equal (label + " survivor full text") secondText secondRule.cssText
                    equal (label + " survivor width") secondWidth (all secondMap "width")
                    firstSheet.disabled <- true
                    secondSheet.disabled <- false
                    equal (label + " survivor layout") "30px" (width copyTarget)
                finally
                    first.remove ()
                    second.remove ()
        finally
            copyTarget.remove ()

        attribute.set ("width", text "59px")
        service.Dispose ()
        equal "disposed provider leaves acquired map" [| "59px" |] (all attribute "width")
        attribute.set ("width", text "60px")
        equal "acquired map remains mutable" "60px" (width target)
        reject "disposed Get" disposedError (fun () -> service.Get (attribute, "width") |> ignore)
        reject "disposed Entries" disposedError (fun () -> service.Entries attribute |> ignore)
        checks
    finally
        target.remove ()
        style.remove ()
        baseStyle.remove ()
