module internal NativeApiConformance.KeyframeRules

open System
open System.Globalization
open Ranvier.Dom

/// <summary>Checks keyframe editing through a paused CSS animation held at one quarter of its duration.</summary>
/// <param name="document">The live document; fixture nodes and stylesheets are removed before returning.</param>
/// <param name="computed">The borrowed provider for actual animation output.</param>
/// <param name="checkpoint">Flushes style and animation work on the owning engine thread.</param>
/// <param name="errorName">Reads the engine DOM exception name.</param>
/// <returns>The assertion count, length-property capability and invalid-keyText exception profile.</returns>
let run (document: Document) (computed: ComputedStyleService) (checkpoint: unit -> unit) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"C11 {label}: expected {expected}, got {actual}"

    let same label expected actual =
        equal label true (Object.ReferenceEquals (expected, actual))

    let originalChildren = document.body.childNodes.length
    let originalSheets = document.styleSheets.length
    let style = document.createElement "style" :?> HTMLStyleElement
    let probe = document.createElement "div" :?> HTMLElement
    probe.id <- "c11-probe"

    style.textContent <-
        """
@keyframes c11-progress {
  from { width: 20px; opacity: 0.2; }
  25% { width: 60px; opacity: 0.6; }
  to { width: 100px; opacity: 1; }
}
#c11-probe { width: 12px; height: 10px; animation: c11-progress 100s linear -25s 1 normal both paused; }
"""

    document.body.appendChild style |> ignore
    document.body.appendChild probe |> ignore
    let mutable lengthAvailable = false
    let mutable invalidKeyTextError = ""

    try
        let sheet = style.sheet
        let frames = sheet.cssRules.item 0u :?> CSSKeyframesRule
        let list = frames.cssRules
        lengthAvailable <- not (isNull (box frames.length))

        let length expected =
            equal "live keyframe rule-list length" expected list.length

            if lengthAvailable then
                equal "live keyframes length" expected frames.length

        equal "keyframes name" "c11-progress" frames.name
        same "keyframes parent sheet" sheet frames.parentStyleSheet
        same "keyframes list identity" list frames.cssRules
        length 3u
        let first = frames.findRule "from"
        let middle = frames.findRule "25%"
        let last = frames.findRule "to"
        same "from alias normalization" first (frames.findRule "0%")
        same "to alias normalization" last (frames.findRule "100%")
        same "indexed first child identity" first (frames.item 0u)
        same "indexed middle child identity" middle (frames.item 1u)
        same "rule-list middle child identity" middle (list.item 1u)
        equal "indexed out-of-range is nullish" true (isNull (frames.item 3u))
        equal "missing keyframe is null" true (isNull (frames.findRule "75%"))
        equal "invalid find key is null" true (isNull (frames.findRule "invalid"))
        equal "first serialized offset" "0%" first.keyText
        equal "last serialized offset" "100%" last.keyText
        same "keyframe parent" frames middle.parentRule
        same "keyframe parent sheet" sheet middle.parentStyleSheet
        same "keyframe style identity" middle.style middle.style
        same "keyframe style parent" middle middle.style.parentRule
        let live = computed.Get (probe, None)
        let mutable operation = "keyframe edits"

        let numeric property expected =
            checkpoint ()
            let text = live.getPropertyValue property

            let number =
                if text.EndsWith "px" then
                    text.Substring (0, text.Length - 2)
                else
                    text

            let actual = Double.Parse (number, CultureInfo.InvariantCulture)
            checks <- checks + 1

            if not (abs (expected - actual) <= 0.02) then
                failwith $"C11 {operation}, computed {property}: expected {expected}, got {text}"

        numeric "width" 60.
        numeric "opacity" 0.6
        equal "animation is paused" "paused" (live.getPropertyValue "animation-play-state")
        equal "controlled negative delay" "-25s" (live.getPropertyValue "animation-delay")
        middle.keyText <- "50%"
        equal "edited offset" "50%" middle.keyText
        equal "old offset lookup misses" true (isNull (frames.findRule "25%"))
        same "new offset lookup preserves identity" middle (frames.findRule "50%")
        numeric "width" 40.
        numeric "opacity" 0.4
        middle.style.setProperty ("width", "100px", "")
        numeric "width" 60.
        middle.style.cssText <- "width: 140px; opacity: 0.8;"
        equal "declaration text mutation" "140px" (middle.style.getPropertyValue "width")
        numeric "width" 80.
        numeric "opacity" 0.5
        let beforeInvalidOffset = frames.cssText

        try
            middle.keyText <- "not-an-offset"
        with error when errorName error = "SyntaxError" ->
            invalidKeyTextError <- errorName error

        equal "invalid offset preserves key text" "50%" middle.keyText
        equal "invalid offset preserves complete keyframes" beforeInvalidOffset frames.cssText
        numeric "width" 80.
        let beforeInvalidAppend = frames.cssText
        frames.appendRule "not-an-offset { width: 999px; }"
        equal "invalid append preserves complete keyframes" beforeInvalidAppend frames.cssText
        frames.deleteRule "not-an-offset"
        frames.deleteRule "75%"
        equal "invalid and missing deletion preserve complete keyframes" beforeInvalidAppend frames.cssText
        frames.appendRule "50% { width: 180px; opacity: 0.4; }"
        length 4u
        let duplicate = frames.findRule "50%"
        equal "duplicate lookup chooses appended rule" false (Object.ReferenceEquals (middle, duplicate))
        same "append preserves source order" duplicate (frames.item 3u)
        equal "duplicate declaration" "180px" (duplicate.style.getPropertyValue "width")
        numeric "width" 100.
        frames.deleteRule "50%"
        length 3u
        same "deletion exposes preceding duplicate" middle (frames.findRule "50%")
        equal "deleted duplicate clears parent" true (isNull duplicate.parentRule)
        equal "deleted duplicate retains declaration" "180px" (duplicate.style.getPropertyValue "width")
        numeric "width" 80.
        frames.deleteRule "50%"
        length 2u
        equal "last matching offset removed" true (isNull (frames.findRule "50%"))
        equal "deleted edited frame retains offset" "50%" middle.keyText
        equal "deleted edited frame retains style" "140px" (middle.style.getPropertyValue "width")
        numeric "width" 40.
        frames.deleteRule "from"
        length 1u
        equal "from deletion removes zero offset" true (isNull (frames.findRule "0%"))
        numeric "width" 34.
        frames.appendRule "from { width: 20px; opacity: 0.2; }"
        length 2u
        let newFirst = frames.findRule "0%"
        same "appended from remains last in source list" newFirst (frames.item 1u)
        numeric "width" 40.
        let retainedFirst = newFirst
        frames.appendRule "0%, 100% { opacity: 0.9; }"
        length 3u
        let multiple = frames.findRule "0%, 100%"
        equal "multi-offset lookup succeeds" true (not (isNull multiple))
        equal "multi-offset serialization" "0%,100%" (multiple.keyText.Replace (" ", ""))
        numeric "opacity" 0.9
        frames.deleteRule "0%, 100%"
        length 2u
        numeric "opacity" 0.4
        operation <- "rename keyframes rule"
        frames.name <- "c11-renamed"
        equal "keyframes name mutation" "c11-renamed" frames.name
        numeric "width" 12.
        probe.style.setProperty ("animation-name", "c11-renamed", "")
        numeric "width" 40.
        let keyframeBeforeNoop = retainedFirst.cssText
        retainedFirst.cssText <- "50% { width: 999px; }"
        equal "inherited cssText setter is no-op" keyframeBeforeNoop retainedFirst.cssText
        numeric "width" 40.
        operation <- "delete keyframes rule"
        sheet.deleteRule 0u
        numeric "width" 12.
        equal "detached keyframes retains name" "c11-renamed" frames.name
        same "detached keyframes retains child identity" retainedFirst (frames.findRule "from")
        equal "detached keyframes retains declaration" "20px" (retainedFirst.style.getPropertyValue "width")
    finally
        for node in [ style :> Node; probe :> Node ] do
            if not (isNull node.parentNode) then
                node.parentNode.removeChild node |> ignore

    equal "fixture stylesheet cleanup" originalSheets document.styleSheets.length
    equal "fixture node cleanup" originalChildren document.body.childNodes.length
    checks, lengthAvailable, invalidKeyTextError
