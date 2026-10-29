module internal NativeApiConformance.CascadeRules

open System
open Ranvier.Dom

/// <summary>Checks cascade effects and live editing of layers, scopes, nesting, imports and namespaces.</summary>
/// <param name="document">The live document; temporary nodes and stylesheets are removed before returning.</param>
/// <param name="computed">The borrowed provider used for live computed declarations.</param>
/// <param name="nextTurn">Completes after the owning engine has processed a resource-loading turn.</param>
/// <param name="errorName">Reads the engine DOM exception name.</param>
/// <returns>The assertion count and scope, nesting and imported-sheet inspection capability outcomes.</returns>
let run (document: Document) (computed: ComputedStyleService) (nextTurn: unit -> Async<unit>) (errorName: exn -> string) =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"C10 {label}: expected {expected}, got {actual}"

        let same label expected actual =
            equal label true (Object.ReferenceEquals (expected, actual))

        let nodes = ResizeArray<Node>()
        let originalStyleSheets = document.styleSheets.length
        let originalChildren = document.body.childNodes.length

        let attach (node: Node) =
            nodes.Add node
            document.body.appendChild node |> ignore

        let element tag id =
            let node = document.createElement tag :?> HTMLElement
            node.id <- id
            node

        let stylesheet text =
            let node = document.createElement "style" :?> HTMLStyleElement
            node.textContent <- text
            attach node
            node

        let read (node: Element) =
            computed.Get (node, None)

        let mutable scopeAvailable = false
        let mutable nestingAvailable = false
        let mutable importContentsReadable = false

        let optionalRule (sheet: CSSStyleSheet) text =
            try
                let index = sheet.insertRule (text, sheet.cssRules.length)
                Some (sheet.cssRules.item index)
            with error when errorName error = "SyntaxError" ->
                None

        try
            let layerProbe = element "div" "c10-layer"
            attach layerProbe

            let layerSource order =
                "@layer "
                + order
                + "; "
                + "@layer c10second { #c10-layer { width: 47px; height: 47px !important; } } "
                + "@layer c10first { #c10-layer { width: 31px; height: 31px !important; } }"

            let layerStyle = stylesheet (layerSource "c10first, c10second")
            let layerSheet = layerStyle.sheet
            let statement = layerSheet.cssRules.item 0u :?> CSSLayerStatementRule
            let second = layerSheet.cssRules.item 1u :?> CSSLayerBlockRule
            let first = layerSheet.cssRules.item 2u :?> CSSLayerBlockRule
            equal "ordered layer names" [ "c10first"; "c10second" ] (Array.toList statement.nameList)
            same "layer statement identity" statement (layerSheet.cssRules.item 0u)
            equal "second layer name" "c10second" second.name
            equal "first layer name" "c10first" first.name
            same "layer block parent sheet" layerSheet second.parentStyleSheet
            same "layer child parent" second (second.cssRules.item 0u).parentRule
            let liveLayer = read layerProbe
            equal "normal layer priority overrides source order" "47px" (liveLayer.getPropertyValue "width")
            equal "important reverses layer priority" "31px" (liveLayer.getPropertyValue "height")
            let oldChild = second.cssRules.item 0u :?> CSSStyleRule
            oldChild.style.setProperty ("width", "59px", "")
            equal "layer declaration edit affects cascade" "59px" (liveLayer.getPropertyValue "width")
            second.deleteRule 0u
            equal "layer child deletion affects cascade" "31px" (liveLayer.getPropertyValue "width")
            equal "detached layer child retains declaration" "59px" (oldChild.style.getPropertyValue "width")
            equal "detached layer child clears parent" true (isNull oldChild.parentRule)
            equal "layer child insert index" 0u (second.insertRule ("#c10-layer { width: 67px; }", 0u))
            equal "layer child insertion affects cascade" "67px" (liveLayer.getPropertyValue "width")
            layerStyle.textContent <- layerSource "c10second, c10first"
            equal "replaced layer order affects normal cascade" "31px" (liveLayer.getPropertyValue "width")
            equal "replaced layer order affects important cascade" "47px" (liveLayer.getPropertyValue "height")
            equal "retained layer statement preserves names" [ "c10first"; "c10second" ] (Array.toList statement.nameList)
            equal "retained layer block preserves edited child" "67px" ((second.cssRules.item 0u :?> CSSStyleRule).style.getPropertyValue "width")

            let scopedRoot = element "section" "c10-scope-root"
            scopedRoot.className <- "c10-scope"
            let scoped = element "div" "c10-scoped"
            scoped.className <- "c10-target"
            let limit = element "section" "c10-scope-limit"
            limit.className <- "c10-limit"
            let excluded = element "div" "c10-excluded"
            excluded.className <- "c10-target"
            let outside = element "div" "c10-outside"
            outside.className <- "c10-target"
            scopedRoot.appendChild scoped |> ignore
            scopedRoot.appendChild limit |> ignore
            limit.appendChild excluded |> ignore
            attach scopedRoot
            attach outside
            let scopeStyle = stylesheet ".c10-target { opacity: 0.2; }"

            match optionalRule scopeStyle.sheet "@scope (.c10-scope) to (.c10-limit) { .c10-target { opacity: 0.7; } }" with
            | None -> equal "scope syntax capability unavailable" 1u scopeStyle.sheet.cssRules.length
            | Some rule ->
                scopeAvailable <- true
                let scope = rule :?> CSSScopeRule
                equal "scope start selector" ".c10-scope" scope.start
                equal "scope end selector" ".c10-limit" scope.``end``
                same "scope parent sheet" scopeStyle.sheet scope.parentStyleSheet
                same "scope child parent" scope (scope.cssRules.item 0u).parentRule
                let liveScoped, liveExcluded, liveOutside = read scoped, read excluded, read outside
                equal "scope applies within bounds" "0.7" (liveScoped.getPropertyValue "opacity")
                equal "scope excludes lower boundary subtree" "0.2" (liveExcluded.getPropertyValue "opacity")
                equal "scope excludes outside element" "0.2" (liveOutside.getPropertyValue "opacity")
                let scopedChild = scope.cssRules.item 0u :?> CSSStyleRule
                scopedChild.style.setProperty ("opacity", "0.8", "")
                equal "scope declaration edit affects cascade" "0.8" (liveScoped.getPropertyValue "opacity")
                scope.deleteRule 0u
                equal "scope child deletion affects cascade" "0.2" (liveScoped.getPropertyValue "opacity")
                equal "scope child insert index" 0u (scope.insertRule (".c10-target { opacity: 0.6; }", 0u))
                equal "scope child insertion affects cascade" "0.6" (liveScoped.getPropertyValue "opacity")
                scopeStyle.sheet.deleteRule 1u
                equal "scope rule deletion affects cascade" "0.2" (liveScoped.getPropertyValue "opacity")
                equal "detached scope retains selector" ".c10-scope" scope.start
                equal "detached scope retains child" 1u scope.cssRules.length

                let implicitScope =
                    scopeStyle.sheet.insertRule ("@scope { .c10-target { opacity: 0.9; } }", 1u)

                let implicitRule = scopeStyle.sheet.cssRules.item implicitScope :?> CSSScopeRule
                equal "implicit scope start is null" true (isNull implicitRule.start)
                equal "implicit scope end is null" true (isNull implicitRule.``end``)
                equal "implicit scope affects descendants" "0.9" (liveOutside.getPropertyValue "opacity")

            let nestingProbe = element "div" "c10-nesting"
            let nestedChild = element "span" "c10-nested-child"
            nestedChild.className <- "c10-nested-child"
            nestingProbe.appendChild nestedChild |> ignore
            attach nestingProbe
            let liveOuter = read nestingProbe
            let originalOuterWidth = liveOuter.getPropertyValue "width"
            let nestingStyle = stylesheet ""

            match
                optionalRule nestingStyle.sheet "#c10-nesting { opacity: 0.2; & .c10-nested-child { opacity: 0.4; } opacity: 0.6; width: 37px; }"
            with
            | None -> equal "nesting syntax capability unavailable" 0u nestingStyle.sheet.cssRules.length
            | Some rule ->
                let outer = rule :?> CSSStyleRule
                equal "nested rule and declarations count" 2u outer.cssRules.length
                let nested = outer.cssRules.item 1u :?> CSSNestedDeclarations
                nestingAvailable <- true
                same "nested declarations parent" outer nested.parentRule
                same "nested declarations parent sheet" nestingStyle.sheet nested.parentStyleSheet
                same "nested declaration style identity" nested.style nested.style
                same "nested declaration style parent" nested nested.style.parentRule
                equal "post-nesting declaration parse" "0.6" (nested.style.getPropertyValue "opacity")
                equal "post-nesting width parse" "37px" (nested.style.getPropertyValue "width")
                let liveChild = read nestedChild
                equal "post-nesting declarations win cascade" "0.6" (liveOuter.getPropertyValue "opacity")
                equal "post-nesting width affects cascade" "37px" (liveOuter.getPropertyValue "width")
                equal "nested selector affects child" "0.4" (liveChild.getPropertyValue "opacity")
                nested.style.setProperty ("opacity", "0.8", "")
                equal "nested declaration edit affects cascade" "0.8" (liveOuter.getPropertyValue "opacity")
                nested.style.removeProperty "width" |> ignore
                equal "nested declaration removal is reflected" "" (nested.style.getPropertyValue "width")
                equal "nested declaration removal restores computed width" originalOuterWidth (liveOuter.getPropertyValue "width")
                outer.deleteRule 1u
                equal "nested declaration deletion restores earlier value" "0.2" (liveOuter.getPropertyValue "opacity")
                equal "detached nested declaration retains edit" "0.8" (nested.style.getPropertyValue "opacity")
                equal "detached nested declaration clears parent" true (isNull nested.parentRule)

            let svgNamespace = "http://www.w3.org/2000/svg"
            let htmlNamespace = "http://www.w3.org/1999/xhtml"
            let svg = document.createElementNS (svgNamespace, "svg")
            let svgRect = document.createElementNS (svgNamespace, "rect")
            let htmlRect = document.createElement "rect"
            svg.appendChild svgRect |> ignore
            attach svg
            attach htmlRect

            let namespaceSource uri =
                "@namespace fx \""
                + uri
                + "\"; fx|rect { opacity: 0.6; }"

            let namespaceStyle = stylesheet (namespaceSource svgNamespace)
            let nsRule = namespaceStyle.sheet.cssRules.item 0u :?> CSSNamespaceRule
            equal "namespace prefix" "fx" nsRule.prefix
            equal "namespace URI" svgNamespace nsRule.namespaceURI
            same "namespace parent sheet" namespaceStyle.sheet nsRule.parentStyleSheet
            let liveSvg, liveHtml = read svgRect, read htmlRect
            equal "namespace selector matches SVG" "0.6" (liveSvg.getPropertyValue "opacity")
            equal "namespace selector excludes HTML" "1" (liveHtml.getPropertyValue "opacity")
            namespaceStyle.textContent <- namespaceSource htmlNamespace
            equal "namespace replacement excludes old namespace" "1" (liveSvg.getPropertyValue "opacity")
            equal "namespace replacement matches new namespace" "0.6" (liveHtml.getPropertyValue "opacity")
            equal "detached namespace retains URI" svgNamespace nsRule.namespaceURI
            equal "replacement namespace URI" htmlNamespace (namespaceStyle.sheet.cssRules.item 0u :?> CSSNamespaceRule).namespaceURI

            namespaceStyle.textContent <-
                "@namespace \""
                + svgNamespace
                + "\"; rect { opacity: 0.5; }"

            equal "default namespace empty prefix" "" (namespaceStyle.sheet.cssRules.item 0u :?> CSSNamespaceRule).prefix
            equal "default namespace matches SVG" "0.5" (liveSvg.getPropertyValue "opacity")
            equal "default namespace excludes HTML" "1" (liveHtml.getPropertyValue "opacity")

            let importProbe = element "div" "c10-import"
            attach importProbe
            let importUrl = "data:text/css,%23c10-import%7Bwidth%3A71px%3Bheight%3A73px%3B%7D"

            let importStyle =
                stylesheet (
                    "@layer c10base, c10import; @import url(\""
                    + importUrl
                    + "\") layer(c10import) supports(display: grid) screen; "
                    + "@layer c10base { #c10-import { width: 13px; height: 17px; } }"
                )

            let imported = importStyle.sheet.cssRules.item 1u :?> CSSImportRule
            equal "import href" importUrl imported.href
            equal "import layer" "c10import" imported.layerName
            equal "import supports text" "display: grid" imported.supportsText
            equal "import media" "screen" imported.media.mediaText
            same "import media identity" imported.media imported.media
            same "import parent sheet" importStyle.sheet imported.parentStyleSheet
            let liveImport = read importProbe
            let mutable turns = 0

            while (isNull imported.styleSheet
                   || liveImport.getPropertyValue "width" <> "71px")
                  && turns < 1000 do
                do! nextTurn ()
                turns <- turns + 1

            equal "data stylesheet load completed" true (not (isNull imported.styleSheet))
            let importedSheet = imported.styleSheet
            same "imported sheet identity" importedSheet imported.styleSheet
            same "imported sheet owner rule" imported importedSheet.ownerRule
            same "imported sheet parent" importStyle.sheet importedSheet.parentStyleSheet
            equal "import affects cascade width" "71px" (liveImport.getPropertyValue "width")
            equal "import affects cascade height" "73px" (liveImport.getPropertyValue "height")

            try
                let importedRules = importedSheet.cssRules
                importContentsReadable <- true
                equal "imported child count" 1u importedRules.length
                let importedChild = importedRules.item 0u :?> CSSStyleRule
                equal "imported selector" "#c10-import" importedChild.selectorText
                importedChild.style.setProperty ("width", "83px", "")
                equal "imported declaration edit affects cascade" "83px" (liveImport.getPropertyValue "width")
            with error when errorName error = "SecurityError" ->
                equal "cross-origin imported rules reject inspection" "SecurityError" (errorName error)

            let activeWidth = if importContentsReadable then "83px" else "71px"
            imported.media.mediaText <- "not all"
            equal "import media edit disables cascade" "13px" (liveImport.getPropertyValue "width")
            imported.media.mediaText <- "all"
            equal "import media edit reactivates cascade" activeWidth (liveImport.getPropertyValue "width")
            importStyle.sheet.deleteRule 1u
            equal "import deletion restores base layer" "13px" (liveImport.getPropertyValue "width")
            equal "detached import retains layer" "c10import" imported.layerName
            same "detached import retains sheet" importedSheet imported.styleSheet
            let replacementUrl = importUrl.Replace ("71px", "89px")

            importStyle.textContent <-
                "@import url(\""
                + replacementUrl
                + "\"); "
                + "@layer c10base { #c10-import { width: 13px; height: 17px; } }"

            let replacementImport = importStyle.sheet.cssRules.item 0u :?> CSSImportRule
            turns <- 0

            while (isNull replacementImport.styleSheet
                   || liveImport.getPropertyValue "width" <> "89px")
                  && turns < 1000 do
                do! nextTurn ()
                turns <- turns + 1

            equal "replacement import changes cascade" "89px" (liveImport.getPropertyValue "width")
            equal "replacement import removes layer association" true (isNull replacementImport.layerName)
            equal "replacement import has independent identity" false (Object.ReferenceEquals (imported, replacementImport))
            equal "detached import retains original href" importUrl imported.href
            let plain = stylesheet "@import url(\"data:text/css,\");"
            let plainImport = plain.sheet.cssRules.item 0u :?> CSSImportRule
            equal "absent import layer is null" true (isNull plainImport.layerName)
            equal "absent import supports is null" true (isNull plainImport.supportsText)
            equal "omitted import media empty" "" plainImport.media.mediaText

            plain.textContent <- "@import url(\"data:text/css,\") layer;"
            equal "anonymous import layer is empty string" "" (plain.sheet.cssRules.item 0u :?> CSSImportRule).layerName
        finally
            for node in nodes do
                if not (isNull node.parentNode) then
                    node.parentNode.removeChild node |> ignore

        equal "fixture stylesheet cleanup" originalStyleSheets document.styleSheets.length
        equal "fixture node cleanup" originalChildren document.body.childNodes.length
        return checks, scopeAvailable, nestingAvailable, importContentsReadable
    }
