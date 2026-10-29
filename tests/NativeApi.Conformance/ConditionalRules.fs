module internal NativeApiConformance.ConditionalRules

open System
open Ranvier.Dom

/// <summary>Checks media/supports/container metadata and computed effects while restoring the original viewport.</summary>
/// <param name="document">The live document; fixture nodes are removed before returning.</param>
/// <param name="computed">The borrowed provider used to inspect live cascade results.</param>
/// <param name="originalViewport">The original host width and height in CSS pixels.</param>
/// <param name="resize">Resizes the controlled viewport on its owning thread.</param>
/// <param name="errorName">Reads the engine DOM exception name.</param>
let run
    (document: Document)
    (computed: ComputedStyleService)
    (originalViewport: struct (int * int))
    (resize: int -> int -> Async<unit>)
    (errorName: exn -> string)
    =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let same label expected actual =
            equal label true (Object.ReferenceEquals (expected, actual))

        let style = document.createElement "style" :?> HTMLStyleElement
        let probe = document.createElement "div" :?> HTMLElement
        let container = document.createElement "div" :?> HTMLElement
        let contained = document.createElement "div" :?> HTMLElement
        probe.id <- "native-c09-probe"
        contained.id <- "native-c09-contained"
        container.style.cssText <- "container-type:inline-size;container-name:fixture;width:200px;"

        style.textContent <-
            """
#native-c09-probe { width: 13px; height: 11px; }
@media (min-width: 350px) {
  #native-c09-probe { width: 61px; }
  @supports (display: grid) { #native-c09-probe { height: 31px; } }
}
#native-c09-contained { height: 17px; }
@container fixture (width > 100px) { #native-c09-contained { height: 43px; } }
"""

        container.appendChild contained |> ignore
        document.body.appendChild style |> ignore
        document.body.appendChild probe |> ignore
        document.body.appendChild container |> ignore

        let! outcome =
            Async.Catch (
                async {
                    try
                        let sheet = style.sheet
                        let media = sheet.cssRules.item 1u :?> CSSMediaRule
                        let support = media.cssRules.item 1u :?> CSSSupportsRule
                        let query = sheet.cssRules.item 3u :?> CSSContainerRule
                        same "media identity" media (sheet.cssRules.item 1u)
                        same "media list identity" media.media media.media
                        equal "media condition" "(min-width: 350px)" media.conditionText
                        equal "media list initial length" 1u media.media.length
                        equal "media list first item" "(min-width: 350px)" (media.media.item 0u)
                        equal "media list out of range is null" true (isNull (media.media.item 1u))
                        equal "nested support condition" "(display: grid)" support.conditionText
                        same "nested support parent" media support.parentRule
                        same "nested support sheet" sheet support.parentStyleSheet
                        equal "nested support children" 1u support.cssRules.length
                        same "nested style parent" support (support.cssRules.item 0u).parentRule
                        equal "container name" "fixture" query.containerName
                        equal "container query" "(width > 100px)" query.containerQuery
                        equal "container full condition" ("fixture " + query.containerQuery) query.conditionText
                        equal "container children" 1u query.cssRules.length
                        same "container style parent" query (query.cssRules.item 0u).parentRule
                        let liveProbe = computed.Get (probe, None)
                        let liveContained = computed.Get (contained, None)
                        do! resize 400 240
                        equal "wide viewport media width" "61px" (liveProbe.getPropertyValue "width")
                        equal "wide viewport nested supports height" "31px" (liveProbe.getPropertyValue "height")
                        do! resize 280 240
                        equal "narrow viewport media width" "13px" (liveProbe.getPropertyValue "width")
                        equal "narrow viewport nested support height" "11px" (liveProbe.getPropertyValue "height")
                        media.media.appendMedium "all"
                        equal "append medium live length" 2u media.media.length
                        equal "append medium item order" "all" (media.media.item 1u)
                        equal "appended all activates media width" "61px" (liveProbe.getPropertyValue "width")
                        equal "appended all activates nested support" "31px" (liveProbe.getPropertyValue "height")
                        media.media.appendMedium "all"
                        equal "duplicate medium ignored" 2u media.media.length
                        media.media.deleteMedium "all"
                        equal "medium deletion live length" 1u media.media.length
                        equal "medium deletion deactivates width" "13px" (liveProbe.getPropertyValue "width")
                        let beforeMissing = media.media.mediaText
                        let mutable missingError = ""

                        try
                            media.media.deleteMedium "print"
                        with error ->
                            missingError <- errorName error

                        equal "missing medium error" "NotFoundError" missingError
                        equal "failed medium deletion preserves list" beforeMissing media.media.mediaText
                        media.media.mediaText <- null
                        equal "null media text uses empty-string conversion" "" media.media.mediaText
                        equal "empty media list length" 0u media.media.length
                        equal "empty media item is null" true (isNull (media.media.item 0u))
                        equal "empty media list matches viewport" "61px" (liveProbe.getPropertyValue "width")
                        media.media.mediaText <- "not all"
                        equal "false media disables width" "13px" (liveProbe.getPropertyValue "width")
                        media.media.mediaText <- "all"
                        media.media.appendMedium "screen, print"
                        equal "multi-query append ignored" "all" media.media.mediaText
                        media.media.appendMedium ""
                        equal "empty append ignored" 1u media.media.length
                        let mutable indexError = ""
                        let beforeInvalidInsertion = support.cssText
                        let retainedChild = support.cssRules.item 0u
                        let beforeInvalidHeight = liveProbe.getPropertyValue "height"

                        try
                            support.insertRule ("#unused { height: 1px; }", 99u)
                            |> ignore
                        with error ->
                            indexError <- errorName error

                        equal "nested invalid insertion error" "IndexSizeError" indexError
                        equal "nested invalid insertion is atomic" 1u support.cssRules.length
                        equal "nested invalid insertion preserves rules" beforeInvalidInsertion support.cssText
                        same "nested invalid insertion preserves child identity" retainedChild (support.cssRules.item 0u)
                        equal "nested invalid insertion preserves style" beforeInvalidHeight (liveProbe.getPropertyValue "height")
                        equal "wide container query height" "43px" (liveContained.getPropertyValue "height")
                        container.style.setProperty ("width", "80px", "")
                        equal "narrow container query height" "17px" (liveContained.getPropertyValue "height")
                        container.style.setProperty ("width", "200px", "")
                        equal "container reactivation" "43px" (liveContained.getPropertyValue "height")
                        return checks
                    finally
                        for node in [ style :> Node; probe :> Node; container :> Node ] do
                            if not (isNull node.parentNode) then
                                node.parentNode.removeChild node |> ignore
                }
            )

        let struct (width, height) = originalViewport
        let! restored = Async.Catch (resize width height)

        match outcome, restored with
        | Choice2Of2 error, _ -> return raise error
        | Choice1Of2 _, Choice2Of2 error -> return raise error
        | Choice1Of2 result, Choice1Of2 () -> return result
    }
