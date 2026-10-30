module internal NativeApiConformance.SheetElements

open Ranvier.Dom
open Ranvier.Css

/// <summary>Checks element-associated stylesheets, authored CSS output and real resource load/error completion.</summary>
/// <param name="document">The live page document on its owning engine thread.</param>
/// <param name="computed">Reads resolved properties from the fixture's matching element.</param>
/// <param name="checkpoint">Yields while the owning engine processes resource and event callbacks.</param>
/// <param name="errorName">Reads the engine's stable exception name for data-origin rule access.</param>
let run (document: Document) (computed: HTMLElement -> string -> string) (checkpoint: unit -> Async<unit>) (errorName: exn -> string) =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let same label expected actual =
            equal label true (obj.ReferenceEquals (expected, actual))

        let generated width height =
            Stylesheet.render
                [
                    Rule.style
                        (Selector.className "d12-probe")
                        [
                            Css.width (Css.px width) |> Declaration.important
                            Css.height (Css.px height)
                            |> Declaration.important
                        ]
                ]

        let probe = document.createElement "div" :?> HTMLElement
        probe.className <- "d12-probe"
        probe.style.cssText <- "width:19px;height:13px"
        let style = document.createElement "style" :?> HTMLStyleElement
        let link = document.createElement "link" :?> HTMLLinkElement
        let initialSheets = document.styleSheets.length
        let events = ResizeArray<string * bool * bool>()
        let mutable failureState = "unobserved"

        let received (event: Event) =
            events.Add (event.``type``, obj.ReferenceEquals (event.target, link), obj.ReferenceEquals (event.currentTarget, link))

        link.addEventListener ("load", received, false)
        link.addEventListener ("error", received, false)
        document.body.appendChild probe |> ignore

        let awaitEvents expected =
            async {
                let mutable attempts = 0

                while events.Count < expected && attempts < 300 do
                    do! checkpoint ()
                    attempts <- attempts + 1

                equal "resource event count" expected events.Count
            }

        let eventAt index expected =
            let kind, target, currentTarget = events[index]
            equal "resource completion kind" expected kind
            equal "resource completion target" true target
            equal "resource completion currentTarget" true currentTarget

        let width () =
            computed probe "width"

        let height () =
            computed probe "height"

        let awaitEffect expectedWidth expectedHeight =
            async {
                let mutable attempts = 0

                while (width () <> expectedWidth
                       || height () <> expectedHeight)
                      && attempts < 300 do
                    do! checkpoint ()
                    attempts <- attempts + 1

                equal "settled link width" expectedWidth (width ())
                equal "settled link height" expectedHeight (height ())
            }

        let inspectDataRules label readable (sheet: CSSStyleSheet) expectedCount expectedWidth =
            let result =
                try
                    Choice1Of2 sheet.cssRules
                with error ->
                    Choice2Of2 (errorName error)

            match result with
            | Choice1Of2 rules ->
                equal (label + " rule access profile") true readable
                equal (label + " rule count") expectedCount rules.length

                if expectedCount > 0u then
                    let rule = rules.item 0u :?> CSSStyleRule
                    equal (label + " generated declaration") expectedWidth (rule.style.getPropertyValue "width")
                    same (label + " rule association") sheet rule.parentStyleSheet
            | Choice2Of2 name ->
                equal (label + " rule access profile") false readable
                equal (label + " origin protection") "SecurityError" name

        try
            equal "detached empty style has no sheet" true (isNull style.sheet)
            equal "detached link has no sheet" true (isNull link.sheet)
            style.``type`` <- "text/css"
            style.media <- "screen"
            let blocking = style.blocking
            same "style blocking identity" blocking style.blocking
            blocking.add "render"
            equal "style blocking reflection" "render" (style.getAttribute "blocking")
            blocking.remove "render"
            let firstText = generated 47. 27.
            style.textContent <- firstText
            document.head.appendChild style |> ignore
            let firstSheet = style.sheet
            equal "style associated sheet exists" false (isNull firstSheet)
            same "style sheet through LinkStyle" firstSheet (style :> LinkStyle).sheet
            same "generated style sheet owner" style firstSheet.ownerNode
            equal "generated style rule count" 1u firstSheet.cssRules.length
            let firstRule = firstSheet.cssRules.item 0u :?> CSSStyleRule
            same "generated style rule association" firstSheet firstRule.parentStyleSheet
            equal "generated style selector" ".d12-probe" firstRule.selectorText
            equal "generated style declaration" "47px" (firstRule.style.getPropertyValue "width")
            equal "generated style text retained" firstText style.textContent
            equal "generated style actual width" "47px" (width ())
            equal "generated style actual height" "27px" (height ())
            style.media <- "not all"
            equal "style media attribute reflected" "not all" (style.getAttribute "media")
            equal "style media changes sheet" "not all" firstSheet.media.mediaText
            equal "style inactive media restores baseline" "19px" (width ())
            style.media <- "screen"
            equal "style active media restores effect" "47px" (width ())
            style.disabled <- true
            equal "style disabled changes sheet" true firstSheet.disabled
            equal "disabled style restores baseline" "19px" (width ())
            style.disabled <- false
            equal "style enabled actual width" "47px" (width ())
            let oldText = firstRule.cssText
            style.textContent <- generated 59. 31.
            let replacedSheet = style.sheet
            equal "style replacement changes sheet" false (obj.ReferenceEquals (firstSheet, replacedSheet))
            same "replacement style owner" style replacedSheet.ownerNode
            same "retained old rule parent" firstSheet firstRule.parentStyleSheet
            equal "retained old rule text" oldText firstRule.cssText
            equal "replacement actual width" "59px" (width ())
            equal "replacement actual height" "31px" (height ())
            style.``type`` <- "text/plain"
            equal "non-CSS type removes sheet" true (isNull style.sheet)
            style.``type`` <- "text/css"
            equal "CSS type restores sheet" false (isNull style.sheet)
            equal "CSS type restores effect" "59px" (width ())
            document.head.removeChild style |> ignore
            equal "removed style returns baseline" "19px" (width ())

            link.rel <- "stylesheet"
            same "link relList identity" link.relList link.relList
            equal "link relList reflects rel" true (link.relList.contains "stylesheet")
            link.crossOrigin <- "anonymous"
            equal "link crossOrigin reflection" "anonymous" (link.getAttribute "crossorigin")
            link.crossOrigin <- null
            link.``as`` <- "style"
            equal "link as reflection" "style" (link.getAttribute "as")
            link.media <- "screen"
            link.``type`` <- "text/css"
            link.hreflang <- "en"
            link.integrity <- "test-value"
            equal "link integrity reflection" "test-value" (link.getAttribute "integrity")
            link.integrity <- ""
            link.imageSrcset <- "first.png 1x, second.png 2x"
            link.imageSizes <- "100vw"
            link.referrerPolicy <- "no-referrer"
            link.fetchPriority <- "high"
            link.charset <- "utf-8"
            link.rev <- "made"
            link.target <- "_self"
            equal "link type reflection" "text/css" (link.getAttribute "type")
            equal "link hreflang reflection" "en" (link.getAttribute "hreflang")
            equal "link imageSrcset reflection" "first.png 1x, second.png 2x" (link.getAttribute "imagesrcset")
            equal "link imageSizes reflection" "100vw" (link.getAttribute "imagesizes")
            equal "link referrerPolicy reflection" "no-referrer" (link.getAttribute "referrerpolicy")
            equal "link fetchPriority reflection" "high" (link.getAttribute "fetchpriority")
            equal "link charset reflection" "utf-8" (link.getAttribute "charset")
            equal "link rev reflection" "made" (link.getAttribute "rev")
            equal "link target reflection" "_self" (link.getAttribute "target")
            let sizes = link.sizes
            same "link sizes identity" sizes link.sizes
            sizes.add "16x16"
            equal "link sizes reflection" "16x16" (link.getAttribute "sizes")
            let linkBlocking = link.blocking
            same "link blocking identity" linkBlocking link.blocking
            linkBlocking.add "render"
            equal "link blocking reflection" "render" (link.getAttribute "blocking")
            linkBlocking.remove "render"

            let firstUrl =
                "data:text/css,"
                + System.Uri.EscapeDataString (generated 71. 37.)

            link.href <- firstUrl
            equal "link href reflection" firstUrl (link.getAttribute "href")
            equal "detached link does not load" 0 events.Count
            document.head.appendChild link |> ignore
            do! awaitEvents 1
            eventAt 0 "load"
            let linkedSheet = link.sheet
            equal "loaded link associated sheet exists" false (isNull linkedSheet)
            same "link sheet through LinkStyle" linkedSheet (link :> LinkStyle).sheet
            same "loaded link owner" link linkedSheet.ownerNode
            same "loaded link repeated identity" linkedSheet link.sheet
            equal "loaded link sheet href" firstUrl linkedSheet.href
            equal "loaded link media" "screen" linkedSheet.media.mediaText
            equal "loaded link generated width" "71px" (width ())
            equal "loaded link generated height" "37px" (height ())
            inspectDataRules "loaded link" true linkedSheet 1u "71px"
            equal "loaded link in document list" (initialSheets + 1u) document.styleSheets.length
            same "loaded link list identity" linkedSheet (document.styleSheets.item initialSheets)

            link.href <-
                "data:text/css,"
                + System.Uri.EscapeDataString (generated 83. 41.)

            do! awaitEvents 2
            eventAt 1 "load"
            let secondSheet = link.sheet
            equal "link href replacement changes sheet" false (obj.ReferenceEquals (linkedSheet, secondSheet))
            same "replacement link owner" link secondSheet.ownerNode
            equal "link replacement actual width" "83px" (width ())
            equal "link replacement actual height" "41px" (height ())
            link.href <- "data:text/css;base64,%%%"
            do! awaitEvents 3
            eventAt 2 "error"

            failureState <-
                if isNull link.sheet then
                    "none"
                elif obj.ReferenceEquals (secondSheet, link.sheet) then
                    "previous"
                else
                    "replacement"

            equal "failed resource associated-sheet state" "replacement" failureState
            equal "failed resource computed width" "19px" (width ())
            same "failed resource sheet owner" link link.sheet.ownerNode
            inspectDataRules "failed link" false link.sheet 0u ""
            link.href <- firstUrl
            do! awaitEvents 4
            eventAt 3 "load"
            equal "recovery resource actual width" "71px" (width ())
            same "recovered resource owner" link link.sheet.ownerNode
            link.removeEventListener ("load", received, false)
            link.removeEventListener ("error", received, false)

            link.href <-
                "data:text/css,"
                + System.Uri.EscapeDataString (generated 97. 43.)

            do! awaitEffect "97px" "43px"
            do! checkpoint ()
            equal "completed resource after unsubscribe stays silent" 4 events.Count
            link.media <- "not all"
            do! awaitEffect "19px" "13px"
            equal "link media updates associated sheet" "not all" link.sheet.media.mediaText
            same "media-updated link sheet owner" link link.sheet.ownerNode
            link.media <- "screen"
            do! awaitEffect "97px" "43px"
            link.disabled <- true
            equal "link disabled flag" true link.disabled
            do! awaitEffect "19px" "13px"
            link.disabled <- false
            do! awaitEffect "97px" "43px"
            same "reenabled link sheet owner" link link.sheet.ownerNode
            equal "removed resource listeners stay silent" 4 events.Count
        finally
            link.removeEventListener ("load", received, false)
            link.removeEventListener ("error", received, false)

            for node in [| style :> Node; link :> Node; probe :> Node |] do
                if not (isNull node.parentNode) then
                    node.parentNode.removeChild node |> ignore

        equal "sheet elements cleanup restores document list" initialSheets document.styleSheets.length
        return checks, failureState
    }
