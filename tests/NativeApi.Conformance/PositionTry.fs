module internal NativeApiConformance.PositionTry

open System
open Ranvier.Dom

/// <summary>Checks parsed position fallback descriptors and live anchored placement in a constrained containing block.</summary>
/// <param name="document">The live document on its owning engine thread.</param>
/// <param name="flush">Completes pending style and layout before rectangle reads.</param>
/// <returns>The assertion count and whether the engine parsed the position-try rule.</returns>
let run (document: Document) (flush: unit -> unit) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"C21 {label}: expected {expected}, got {actual}"

    let same label expected actual =
        equal label true (Object.ReferenceEquals (expected, actual))

    let near label expected actual =
        checks <- checks + 1

        if not (abs (expected - actual) <= 0.1) then
            failwith $"C21 {label}: expected {expected}, got {actual}"

    let originalSheets = document.styleSheets.length
    let originalChildren = document.body.childNodes.length
    let style = document.createElement "style" :?> HTMLStyleElement
    let container = document.createElement "div" :?> HTMLElement
    let anchor = document.createElement "div" :?> HTMLElement
    let target = document.createElement "div" :?> HTMLElement
    container.className <- "c21-container"
    anchor.className <- "c21-anchor"
    target.className <- "c21-target"

    style.textContent <-
        """
@position-try --c21-choice { position-area: top; align-self: unsafe end; margin-bottom: 3px; }
.c21-container { position: relative; width: 240px; height: 160px; padding: 0; border: 0; }
.c21-anchor { position: absolute; left: 90px; top: 135px; width: 20px; height: 10px; anchor-name: --c21-anchor; }
.c21-target { position: absolute; position-anchor: --c21-anchor; position-area: bottom;
  position-try-fallbacks: --c21-choice; align-self: unsafe start; justify-self: anchor-center;
  width: 60px; height: 30px; padding: 0; border: 0; margin: 0; }
"""

    container.appendChild anchor |> ignore
    container.appendChild target |> ignore
    document.head.appendChild style |> ignore
    document.body.appendChild container |> ignore
    let mutable available = false

    let rectangles () =
        flush ()
        container.getBoundingClientRect (), anchor.getBoundingClientRect (), target.getBoundingClientRect ()

    try
        let sheet = style.sheet

        if sheet.cssRules.length > 0u then
            let candidate = sheet.cssRules.item 0u

            if candidate.cssText.StartsWith "@position-try " then
                let rule = candidate :?> CSSPositionTryRule
                available <- true
                let declaration = rule.style
                equal "parsed fallback name" "--c21-choice" rule.name
                same "rule parent sheet" sheet rule.parentStyleSheet
                same "retained descriptor identity" declaration rule.style
                same "descriptor parent rule" rule declaration.parentRule
                equal "parsed fallback area" "top" declaration.positionArea
                equal "parsed fallback margin" "3px" declaration.marginBottom
                equal "hyphenated margin alias" "3px" declaration.``margin-bottom``
                let _, anchorRect, targetRect = rectangles ()
                near "top fallback width" 60. targetRect.width
                near "top fallback height" 30. targetRect.height
                near "top fallback margin effect" 3. (anchorRect.top - targetRect.bottom)

                target.style.setProperty ("position-try-fallbacks", "none")
                let containerRect, anchorRect, targetRect = rectangles ()
                near "default placement begins below anchor" anchorRect.bottom targetRect.top
                equal "default placement overflows containing block" true (targetRect.bottom > containerRect.bottom)

                target.style.removeProperty "position-try-fallbacks"
                |> ignore

                let _, anchorRect, targetRect = rectangles ()
                near "restored fallback returns above anchor" 3. (anchorRect.top - targetRect.bottom)

                declaration.positionArea <- "left"
                declaration.alignSelf <- "anchor-center"
                declaration.justifySelf <- "unsafe end"
                declaration.marginBottom <- "0px"
                declaration.marginRight <- "5px"
                same "descriptor edit preserves rule identity" rule (sheet.cssRules.item 0u)
                same "descriptor edit preserves declaration identity" declaration rule.style
                equal "typed area mutation" "left" declaration.positionArea
                equal "typed block alignment mutation" "anchor-center" declaration.alignSelf
                equal "typed inline alignment mutation" "unsafe end" declaration.justifySelf
                let _, anchorRect, targetRect = rectangles ()
                near "left fallback margin effect" 5. (anchorRect.left - targetRect.right)
                declaration.width <- "70px"
                let _, anchorRect, targetRect = rectangles ()
                near "typed fallback width effect" 70. targetRect.width
                near "typed width preserves chosen side" 5. (anchorRect.left - targetRect.right)
                equal "typed width reflected in descriptor" "70px" declaration.width

                let beforeInvalid = rule.cssText
                declaration.positionArea <- "not-an-area"
                declaration.width <- "not-a-width"
                declaration.setProperty ("color", "red")
                equal "invalid or excluded descriptors preserve complete rule" beforeInvalid rule.cssText
                let _, anchorRect, targetRect = rectangles ()
                near "invalid descriptor preserves width" 70. targetRect.width
                near "invalid descriptor preserves placement" 5. (anchorRect.left - targetRect.right)

                declaration.cssText <- "position-area: left; align-self: anchor-center; justify-self: unsafe end; width: 76px; margin-right: 7px;"
                same "cssText edit preserves descriptor identity" declaration rule.style
                equal "replacement descriptor width" "76px" declaration.width
                let _, anchorRect, targetRect = rectangles ()
                near "cssText width effect" 76. targetRect.width
                near "cssText margin effect" 7. (anchorRect.left - targetRect.right)

                anchor.style.setProperty ("top", "60px")
                target.style.setProperty ("position-try-fallbacks", "none")
                let _, anchorRect, targetRect = rectangles ()
                near "fitting default placement with fallbacks disabled" anchorRect.bottom targetRect.top
                near "fitting default uses ordinary width" 60. targetRect.width

                target.style.removeProperty "position-try-fallbacks"
                |> ignore

                let _, anchorRect, targetRect = rectangles ()
                near "reset fallback preference keeps fitting default" anchorRect.bottom targetRect.top
                near "reset fallback preference keeps ordinary width" 60. targetRect.width
                anchor.style.removeProperty "top" |> ignore
                let _, anchorRect, targetRect = rectangles ()
                near "overflow selects fallback again" 7. (anchorRect.left - targetRect.right)
                near "overflow restores fallback width" 76. targetRect.width

                let retainedText = declaration.cssText
                sheet.deleteRule 0u
                equal "removed fallback clears parent sheet" true (isNull rule.parentStyleSheet)
                same "removed rule retains descriptors" declaration rule.style
                equal "removed descriptors retain complete text" retainedText declaration.cssText
                let containerRect, anchorRect, targetRect = rectangles ()
                near "removed fallback restores below placement" anchorRect.bottom targetRect.top
                equal "removed fallback overflows again" true (targetRect.bottom > containerRect.bottom)
                declaration.width <- "89px"
                equal "detached descriptor stays editable" "89px" declaration.width
                let _, _, targetRect = rectangles ()
                near "detached rule edit leaves rendered width unchanged" 60. targetRect.width
    finally
        for node in [| style :> Node; container :> Node |] do
            if not (isNull node.parentNode) then
                node.parentNode.removeChild node |> ignore

    equal "stylesheet cleanup" originalSheets document.styleSheets.length
    equal "node cleanup" originalChildren document.body.childNodes.length
    checks, available
