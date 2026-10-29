module internal NativeApiConformance.RuleMutations

open System
open Ranvier.Dom

/// <summary>Checks nested rule edits, computed effects and state preservation after rejected mutations.</summary>
/// <param name="document">The live document; fixture nodes are removed before returning.</param>
/// <param name="computed">The borrowed provider used to read live cascade results.</param>
/// <param name="errorName">Reads the engine DOM exception name.</param>
let run (document: Document) (computed: ComputedStyleService) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label expected actual =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: engine object identity changed"

    let style = document.createElement "style" :?> HTMLStyleElement

    style.textContent <-
        "#native-api-rule-probe { width: 11px; height: 12px; } "
        + "@media all { #native-api-rule-probe { width: 21px; } } "
        + "#native-api-rule-parent { width: 13px; }"

    let probe = document.createElement "div" :?> HTMLElement
    probe.id <- "native-api-rule-probe"
    let parent = document.createElement "div" :?> HTMLElement
    parent.id <- "native-api-rule-parent"
    let child = document.createElement "div" :?> HTMLElement
    child.className <- "rule-child"
    parent.appendChild child |> ignore
    document.body.appendChild style |> ignore
    document.body.appendChild probe |> ignore
    document.body.appendChild parent |> ignore

    try
        let sheet = style.sheet
        let rules = sheet.cssRules
        let group = rules.item 1u :?> CSSGroupingRule
        let parentRule = rules.item 2u :?> CSSStyleRule
        let liveProbe = computed.Get (probe, None)
        let liveChild = computed.Get (child, None)

        let dimensions () =
            [|
                liveProbe.getPropertyValue "width"
                liveProbe.getPropertyValue "height"
                liveChild.getPropertyValue "width"
                liveChild.getPropertyValue "height"
            |]

        let rejected label expectedError (list: CSSRuleList) serialize action =
            let beforeText = serialize ()
            let beforeCount = list.length
            let beforeItems = Array.init (int beforeCount) (fun i -> list.item (uint32 i))
            let beforeDimensions = dimensions ()
            let mutable actualError = ""

            try
                action ()
            with error ->
                actualError <- errorName error

            equal (label + " error") expectedError actualError
            equal (label + " text") beforeText (serialize ())
            equal (label + " count") beforeCount list.length

            for i in 0 .. beforeItems.Length - 1 do
                same (label + " child identity") beforeItems[i] (list.item (uint32 i))

            equal (label + " computed result") beforeDimensions (dimensions ())

        let sheetText () =
            [| for i in 0 .. int rules.length - 1 -> (rules.item (uint32 i)).cssText |]
            |> String.concat "\n"

        equal "stylesheet initial rules" 3u rules.length
        equal "stylesheet rule item out of range" true (isNull (rules.item rules.length))
        equal "stylesheet maximum index is null" true (isNull (rules.item UInt32.MaxValue))
        same "stylesheet rule list identity" rules sheet.cssRules
        equal "top rule parent" true (isNull parentRule.parentRule)
        same "top rule stylesheet" sheet parentRule.parentStyleSheet
        equal "style legacy type" 1us parentRule.``type``
        equal "media legacy type" 4us group.``type``
        equal "initial nested cascade width" "21px" (liveProbe.getPropertyValue "width")
        equal "initial nested cascade height" "12px" (liveProbe.getPropertyValue "height")

        let childBaseline =
            liveChild.getPropertyValue "width", liveChild.getPropertyValue "height"

        rejected "sheet invalid syntax" "SyntaxError" rules sheetText (fun () -> sheet.insertRule "not a rule" |> ignore)

        rejected "sheet invalid insert index" "IndexSizeError" rules sheetText (fun () ->
            sheet.insertRule ("#unused { width: 1px; }", UInt32.MaxValue)
            |> ignore)

        rejected "sheet invalid delete index" "IndexSizeError" rules sheetText (fun () -> sheet.deleteRule rules.length)

        let groupRules = group.cssRules
        let original = groupRules.item 0u :?> CSSStyleRule
        same "group child list identity" groupRules group.cssRules
        same "group child identity" original (groupRules.item 0u)
        same "group child parent" group original.parentRule
        same "group child stylesheet" sheet original.parentStyleSheet
        equal "group nullable indexing" true (isNull (groupRules.item groupRules.length))
        rejected "group invalid syntax" "SyntaxError" groupRules (fun () -> group.cssText) (fun () -> group.insertRule "not a rule" |> ignore)

        rejected "group invalid insert index" "IndexSizeError" groupRules (fun () -> group.cssText) (fun () ->
            group.insertRule ("#unused { width: 1px; }", UInt32.MaxValue)
            |> ignore)

        rejected "group invalid delete index" "IndexSizeError" groupRules (fun () -> group.cssText) (fun () -> group.deleteRule groupRules.length)

        rejected "group import hierarchy" "HierarchyRequestError" groupRules (fun () -> group.cssText) (fun () ->
            group.insertRule "@import url('data:text/css,');"
            |> ignore)

        equal "group default insertion index" 0u (group.insertRule "#native-api-rule-probe { width: 31px; height: 32px; }")
        equal "group live list after insertion" 2u groupRules.length
        same "group original moved to next index" original (groupRules.item 1u)
        let inserted = groupRules.item 0u :?> CSSStyleRule
        equal "group inserted selector" "#native-api-rule-probe" inserted.selectorText
        equal "group inserted declaration" "31px" (inserted.style.getPropertyValue "width")
        equal "group later sibling wins width" "21px" (liveProbe.getPropertyValue "width")
        equal "group inserted height applies" "32px" (liveProbe.getPropertyValue "height")
        inserted.selectorText <- "#native-api-rule-absent"
        equal "edited group selector affects cascade" "12px" (liveProbe.getPropertyValue "height")
        inserted.selectorText <- "#native-api-rule-probe"
        same "same inserted style declaration" inserted.style inserted.style
        same "inserted declaration parent" inserted inserted.style.parentRule
        inserted.style.cssText <- "width: 41px; height: 42px !important;"
        equal "edited nested declaration" "41px" (inserted.style.getPropertyValue "width")
        equal "edited nested priority" "important" (inserted.style.getPropertyPriority "height")
        equal "edited nested height applies" "42px" (liveProbe.getPropertyValue "height")
        group.deleteRule 1u
        equal "group live list after deletion" 1u groupRules.length
        equal "deleted group child parent cleared" true (isNull original.parentRule)
        equal "deleted group child stylesheet cleared" true (isNull original.parentStyleSheet)
        equal "retained detached declaration readable" "21px" (original.style.getPropertyValue "width")
        equal "deleting later sibling changes width" "41px" (liveProbe.getPropertyValue "width")
        group.deleteRule 0u
        equal "group emptied live list" 0u groupRules.length
        equal "emptied group item is null" true (isNull (groupRules.item 0u))
        equal "group deletion restores width" "11px" (liveProbe.getPropertyValue "width")
        equal "group deletion restores height" "12px" (liveProbe.getPropertyValue "height")

        let nested = parentRule.cssRules
        same "style own child list identity" nested parentRule.cssRules
        equal "style own empty child list" 0u nested.length
        equal "style own empty item is null" true (isNull (nested.item 0u))
        equal "style own first insert index" 0u (parentRule.insertRule "& > .rule-child { height: 22px; }")
        let originalNested = nested.item 0u :?> CSSStyleRule
        same "style own nested parent" parentRule originalNested.parentRule
        same "style own nested stylesheet" sheet originalNested.parentStyleSheet
        equal "style own nested effect" "22px" (liveChild.getPropertyValue "height")

        rejected "style own invalid syntax" "SyntaxError" nested (fun () -> parentRule.cssText) (fun () ->
            parentRule.insertRule "not a rule" |> ignore)

        rejected "style own invalid insert index" "IndexSizeError" nested (fun () -> parentRule.cssText) (fun () ->
            parentRule.insertRule ("& > .unused { width: 1px; }", UInt32.MaxValue)
            |> ignore)

        rejected "style own invalid delete index" "IndexSizeError" nested (fun () -> parentRule.cssText) (fun () ->
            parentRule.deleteRule nested.length)

        rejected "style own import hierarchy" "HierarchyRequestError" nested (fun () -> parentRule.cssText) (fun () ->
            parentRule.insertRule "@import url('data:text/css,');"
            |> ignore)

        equal "style own default insertion index" 0u (parentRule.insertRule "& > .rule-child { width: 33px; height: 34px; }")
        equal "style own live child count" 2u nested.length
        same "style own original child moved" originalNested (nested.item 1u)
        let insertedNested = nested.item 0u :?> CSSStyleRule
        equal "style own inserted width applies" "33px" (liveChild.getPropertyValue "width")
        equal "style own later child wins height" "22px" (liveChild.getPropertyValue "height")
        insertedNested.style.setProperty ("height", "35px", "important")
        equal "style own priority mutation effect" "35px" (liveChild.getPropertyValue "height")
        equal "style own remove returns value" "35px" (insertedNested.style.removeProperty "height")
        equal "style own removal reveals sibling" "22px" (liveChild.getPropertyValue "height")
        originalNested.style.cssText <- "height: 36px; width: 37px;"
        equal "style own declaration replacement height" "36px" (liveChild.getPropertyValue "height")
        equal "style own declaration replacement width" "37px" (liveChild.getPropertyValue "width")
        let beforeSelector = originalNested.cssText
        originalNested.selectorText <- ":::"
        equal "invalid selector preserves rule text" beforeSelector originalNested.cssText
        same "invalid selector preserves identity" originalNested (nested.item 1u)
        equal "invalid selector preserves effect" "36px" (liveChild.getPropertyValue "height")
        originalNested.selectorText <- "& > .absent"
        equal "valid nested selector edit changes width" "33px" (liveChild.getPropertyValue "width")
        originalNested.selectorText <- "& > .rule-child"
        let beforeCssText = originalNested.cssText
        originalNested.cssText <- "#unused { height: 999px; }"
        equal "CSSRule cssText setter preserves text" beforeCssText originalNested.cssText
        same "CSSRule cssText setter preserves identity" originalNested (nested.item 1u)
        equal "CSSRule cssText setter preserves effect" "36px" (liveChild.getPropertyValue "height")
        parentRule.deleteRule 1u
        equal "style own deletion updates list" 1u nested.length
        equal "deleted style own child parent cleared" true (isNull originalNested.parentRule)
        equal "deleted style own child sheet cleared" true (isNull originalNested.parentStyleSheet)
        equal "retained style own declaration readable" "37px" (originalNested.style.getPropertyValue "width")
        equal "style own deletion reveals inserted width" "33px" (liveChild.getPropertyValue "width")
        equal "style own deletion reveals inserted height" (snd childBaseline) (liveChild.getPropertyValue "height")
        parentRule.deleteRule 0u
        equal "style own final list empty" 0u nested.length
        equal "style own final item null" true (isNull (nested.item 0u))
        equal "style own deletion restores dimensions" childBaseline (liveChild.getPropertyValue "width", liveChild.getPropertyValue "height")
        checks
    finally
        for node in [ style :> Node; probe :> Node; parent :> Node ] do
            if not (isNull node.parentNode) then
                node.parentNode.removeChild node |> ignore
