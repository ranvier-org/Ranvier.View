module internal NativeApiConformance.Stylesheets

open Ranvier.Dom

/// <summary>Checks typed stylesheet editing, concrete rule types and engine object identity.</summary>
/// <param name="document">The live engine document; inserted style nodes are removed before returning.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label expected actual =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: engine object identity changed"

    let style = document.createElement ("style") :?> HTMLStyleElement
    let initialSheets = document.styleSheets.length
    style.title <- "Fixture"
    style.textContent <- ".probe { color: red; width: 40px !important; }"
    let probe = document.createElement "div" :?> HTMLElement
    probe.className <- "probe"
    probe.style.setProperty ("width", "17px")
    document.body.appendChild (probe) |> ignore
    document.body.appendChild (style) |> ignore

    try
        let sheet = style.sheet
        equal "live sheet discovery" (initialSheets + 1u) document.styleSheets.length
        same "same style sheet" sheet style.sheet
        same "same sheet list" document.styleSheets document.styleSheets
        same "sheet discovered through list" sheet (document.styleSheets.item initialSheets)
        same "stylesheet owner node" style sheet.ownerNode
        equal "inline sheet href" null sheet.href
        equal "stylesheet title" "Fixture" sheet.title
        equal "enabled sheet affects layout" 40 probe.clientWidth
        sheet.disabled <- true
        equal "sheet disabled flag" true sheet.disabled
        equal "style disabled reflection" true style.disabled
        equal "disabled sheet stops applying" 17 probe.clientWidth
        style.disabled <- false
        equal "style reenables sheet" false sheet.disabled
        equal "reenabled sheet affects layout" 40 probe.clientWidth
        same "same sheet media list" sheet.media sheet.media
        sheet.media.mediaText <- "not all"
        equal "sheet media stops applying" 17 probe.clientWidth
        sheet.media.mediaText <- ""
        equal "sheet media restores layout" 40 probe.clientWidth
        let rules = sheet.cssRules
        same "same rule list" rules sheet.cssRules
        equal "initial rules" 1u rules.length
        let first = rules.item 0u :?> CSSStyleRule
        same "same rule" first (rules.item 0u)
        same "parent sheet" sheet first.parentStyleSheet
        same "same declaration" first.style first.style
        same "declaration parent rule" first first.style.parentRule
        equal "selector" ".probe" first.selectorText
        first.selectorText <- ".changed"
        equal "selector mutation" ".changed" first.selectorText
        first.style.setProperty ("color", "blue", "important")
        equal "declaration mutation" "blue" (first.style.getPropertyValue "color")
        equal "declaration priority" "important" (first.style.getPropertyPriority "color")

        let insert css =
            let index = sheet.insertRule (css, rules.length)
            equal "insert index" (rules.length - 1u) index
            rules.item index

        let media = insert "@media screen { .inside { opacity: 0.5; } }" :?> CSSMediaRule
        equal "media condition" "screen" media.conditionText
        same "same media list" media.media media.media
        media.media.appendMedium "print"
        equal "media mutation" "screen, print" media.media.mediaText
        let inside = media.cssRules.item 0u
        same "nested parent rule" media inside.parentRule
        same "nested parent sheet" sheet inside.parentStyleSheet

        let supports =
            insert "@supports (display: grid) { .supported { color: green; } }" :?> CSSSupportsRule

        equal "supports condition" "(display: grid)" supports.conditionText

        let container =
            insert "@container card (width > 10px) { .contained { color: green; } }" :?> CSSContainerRule

        equal "container name" "card" container.containerName

        let layer =
            insert "@layer theme { .layered { color: green; } }" :?> CSSLayerBlockRule

        equal "layer name" "theme" layer.name

        let scope =
            insert "@scope (.root) to (.boundary) { .scoped { color: green; } }" :?> CSSScopeRule

        equal "scope start" ".root" scope.start
        equal "scope end" ".boundary" scope.``end``

        let starting =
            insert "@starting-style { .probe { opacity: 0; } }" :?> CSSStartingStyleRule

        equal "starting style children" 1u starting.cssRules.length

        let font =
            insert "@font-face { font-family: Fixture; src: local('Fixture'); }" :?> CSSFontFaceRule

        same "same font descriptors" font.style font.style
        equal "font family" "Fixture" font.style.fontFamily
        font.style.``font-family`` <- "ChangedFixture"
        equal "descriptor alias" "ChangedFixture" font.style.fontFamily

        let position =
            insert "@position-try --fallback { top: 10px; margin-left: 4px; }" :?> CSSPositionTryRule

        equal "fallback name" "--fallback" position.name
        same "same fallback descriptors" position.style position.style
        position.style.``margin-left`` <- "8px"
        equal "position descriptor alias" "8px" position.style.marginLeft

        let keyframes =
            insert "@keyframes pulse { from { opacity: 0; } to { opacity: 1; } }" :?> CSSKeyframesRule

        equal "keyframe name" "pulse" keyframes.name
        keyframes.appendRule "50% { opacity: 0.5; }"
        let middle = keyframes.findRule "50%"
        same "same keyframe" middle (keyframes.findRule "50%")
        equal "keyframe offset" "50%" middle.keyText
        keyframes.deleteRule "50%"
        equal "keyframe deletion" 2u keyframes.length
        sheet.deleteRule (rules.length - 1u)
        equal "live rule deletion" 9u rules.length
    finally
        document.body.removeChild (probe) |> ignore
        document.body.removeChild (style) |> ignore

    equal "live sheet removal" initialSheets document.styleSheets.length
    checks

/// <summary>Checks SVG stylesheet discovery, editing, identity and layout effects.</summary>
/// <param name="document">The live engine document; fixture nodes are removed before returning.</param>
let runSvg (document: Ranvier.Dom.Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label expected actual =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: engine object identity changed"

    let initialSheets = document.styleSheets.length

    let svg =
        document.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> Ranvier.Dom.SVGElement

    let style =
        document.createElementNS ("http://www.w3.org/2000/svg", "style") :?> Ranvier.Dom.SVGStyleElement

    let probe = document.createElement "div" :?> Ranvier.Dom.HTMLElement
    probe.className <- "svg-sheet-probe"
    probe.style.setProperty ("width", "17px")
    style.``type`` <- "text/css"
    style.media <- "screen"
    style.title <- "SvgFixture"
    style.textContent <- ".svg-sheet-probe { width: 43px !important; }"
    equal "SVG style namespace" "http://www.w3.org/2000/svg" style.namespaceURI
    equal "SVG style type" "text/css" style.``type``
    equal "SVG type reflection" "text/css" (style.getAttribute "type")
    equal "SVG style media" "screen" style.media
    equal "SVG media reflection" "screen" (style.getAttribute "media")
    equal "SVG style title" "SvgFixture" style.title
    equal "SVG title reflection" "SvgFixture" (style.getAttribute "title")
    style.title <- ""
    svg.appendChild style |> ignore
    document.body.appendChild probe |> ignore
    document.body.appendChild svg |> ignore

    try
        let sheet = style.sheet
        equal "SVG sheet discovered" (initialSheets + 1u) document.styleSheets.length
        same "SVG sheet identity" sheet style.sheet
        same "SVG list sheet identity" sheet (document.styleSheets.item initialSheets)
        same "SVG sheet owner identity" style sheet.ownerNode
        equal "SVG sheet title" null sheet.title
        equal "SVG sheet media" "screen" sheet.media.mediaText
        equal "SVG sheet layout" 43 probe.clientWidth
        style.disabled <- true
        equal "SVG style disabled" true style.disabled
        equal "SVG sheet disabled" true sheet.disabled
        equal "SVG disabled layout" 17 probe.clientWidth
        sheet.disabled <- false
        equal "SVG sheet enables style" false style.disabled
        equal "SVG enabled layout" 43 probe.clientWidth
        style.media <- "not all"
        equal "SVG media updates sheet" "not all" sheet.media.mediaText
        equal "SVG inactive media layout" 17 probe.clientWidth
        style.media <- "screen"
        equal "SVG active media layout" 43 probe.clientWidth
        let rules = sheet.cssRules
        equal "SVG initial rule count" 1u rules.length
        let rule = rules.item 0u :?> Ranvier.Dom.CSSStyleRule
        same "SVG rule sheet identity" sheet rule.parentStyleSheet
        same "SVG rule declaration identity" rule.style rule.style
        rule.style.setProperty ("width", "61px", "important")
        equal "SVG rule editing layout" 61 probe.clientWidth
        let index = sheet.insertRule (".svg-sheet-probe { height: 23px; }", rules.length)
        equal "SVG inserted rule index" 1u index
        equal "SVG live rule list" 2u rules.length
        equal "SVG inserted rule layout" 23 probe.clientHeight
        sheet.deleteRule index
        equal "SVG live rule deletion" 1u rules.length
    finally
        document.body.removeChild svg |> ignore
        document.body.removeChild probe |> ignore

    equal "SVG live sheet removal" initialSheets document.styleSheets.length
    checks
