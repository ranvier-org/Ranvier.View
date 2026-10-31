module internal NativeApiConformance.SvgResources

open Ranvier.Dom
open NativeApiConformance.SvgChecks

let private png =
    "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAD0lEQVR4nGNg+M+AQMRxAJ6jD/Flt2QIAAAAAElFTkSuQmCC"

/// <summary>Checks use, image, foreignObject, link, style, switch and metadata elements, URI references and resource errors.</summary>
/// <param name="wait">Waits the given milliseconds while the provider loads resources.</param>
let run (document: Document) (pixel: Pixel) (wait: int -> Async<unit>) (errorName: exn -> string) =
    async {
        let checks = Checks errorName

        let root =
            create<SVGSVGElement> document "svg" [ "width", "400"; "height", "100"; "style", "display:block" ]

        let defs = create<Element> document "defs" []

        let symbol =
            create<SVGSymbolElement> document "symbol" [ "id", "resource-symbol"; "viewBox", "0 0 10 10" ]

        symbol.appendChild (create<Element> document "rect" [ "width", "10"; "height", "10"; "fill", "lime" ])
        |> ignore

        defs.appendChild symbol |> ignore
        root.appendChild defs |> ignore

        let reuse =
            create<SVGUseElement>
                document
                "use"
                [
                    "href", "#resource-symbol"
                    "x", "10"
                    "y", "10"
                    "width", "20"
                    "height", "20"
                ]

        let image =
            create<SVGImageElement> document "image" [ "x", "100"; "width", "40"; "height", "40"; "preserveAspectRatio", "none" ]

        let foreign =
            create<SVGForeignObjectElement> document "foreignObject" [ "x", "200"; "width", "40"; "height", "40" ]

        let content = document.createElement "div"
        content.setAttribute ("style", "width:40px;height:40px;background:blue")
        foreign.appendChild content |> ignore
        let choice = create<SVGSwitchElement> document "switch" []

        let skipped =
            create<SVGRectElement>
                document
                "rect"
                [
                    "x", "300"
                    "width", "40"
                    "height", "40"
                    "fill", "red"
                    "requiredExtensions", "urn:ranvier:unsupported"
                ]

        let chosen =
            create<SVGRectElement> document "rect" [ "x", "300"; "width", "40"; "height", "40"; "fill", "lime" ]

        choice.appendChild skipped |> ignore
        choice.appendChild chosen |> ignore

        let link =
            create<SVGAElement> document "a" [ "href", "#target"; "target", "_blank"; "rel", "noopener" ]

        let style = create<SVGStyleElement> document "style" [ "media", "all" ]

        style.textContent <- ".resource-styled{fill:lime}"

        let styled =
            create<Element>
                document
                "rect"
                [
                    "class", "resource-styled"
                    "x", "300"
                    "y", "60"
                    "width", "40"
                    "height", "40"
                    "fill", "red"
                ]

        let title = create<SVGTitleElement> document "title" []
        title.textContent <- "Resources"
        let desc = create<SVGDescElement> document "desc" []
        desc.textContent <- "Shared resource fixture"
        let metadata = create<SVGMetadataElement> document "metadata" []

        for node in
            [
                title :> Element
                desc
                metadata
                reuse
                image
                foreign
                choice
                link
                style
                styled
            ] do
            root.appendChild node |> ignore

        document.body.appendChild root |> ignore

        let color label expected point =
            async {
                let! actual = at pixel root point
                checks.Color label expected actual
            }

        let loaded = ResizeArray<string>()
        image.addEventListener ("load", (fun (_: Event) -> loaded.Add "load"))
        image.addEventListener ("error", (fun (_: Event) -> loaded.Add "error"))

        let settle expected =
            async {
                let mutable attempts = 0

                while loaded.Count < expected && attempts < 300 do
                    do! wait 10
                    attempts <- attempts + 1
            }

        try
            checks.Equal "use href" "#resource-symbol" reuse.href.baseVal
            checks.Near "use x" 10. (float reuse.x.baseVal.value)
            checks.Near "symbol view box" 10. (float symbol.viewBox.baseVal.width)
            checks.Equal "symbol aspect default" 6us symbol.preserveAspectRatio.baseVal.align
            do! color "used symbol" lime (20., 20.)
            reuse.x.baseVal.value <- 50.f
            checks.Equal "use edit writes attribute" "50" (reuse.getAttribute "x")
            do! color "moved use" lime (60., 20.)
            do! color "moved use origin" white (20., 20.)
            reuse.href.baseVal <- "#missing"
            checks.Equal "missing reference reflects" "#missing" reuse.href.animVal
            do! color "missing reference renders nothing" white (60., 20.)
            reuse.href.baseVal <- "#resource-symbol"
            do! color "restored reference" lime (60., 20.)
            checks.Equal "image crossOrigin default" true (isNull image.crossOrigin)
            image.crossOrigin <- "anonymous"
            checks.Equal "image crossOrigin reflects" "anonymous" (image.getAttribute "crossorigin")
            image.removeAttribute "crossorigin"
            checks.Equal "image aspect none" 1us image.preserveAspectRatio.baseVal.align
            checks.Near "image width" 40. (float image.width.baseVal.value)
            image.href.baseVal <- png
            do! settle 1
            checks.Equal "image load event" [ "load" ] (List.ofSeq loaded)
            do! color "loaded image paint" lime (120., 20.)
            image.href.baseVal <- "data:image/png;base64,AAAA"
            do! settle 2
            checks.Equal "image error event" [ "load"; "error" ] (List.ofSeq loaded)
            let! (fr, fg, fb) = at pixel root (120., 20.)
            checks.Equal "failed image drops loaded paint" false (fr < 40 && fg > 215 && fb < 40)
            checks.Near "foreign object width" 40. (float foreign.width.baseVal.value)
            do! color "foreign content" SvgChecks.blue (220., 20.)
            foreign.x.baseVal.value <- 240.f
            do! color "moved foreign content" SvgChecks.blue (260., 20.)
            do! color "moved foreign origin" white (220., 20.)
            checks.Equal "switch skipped extension count" 1u skipped.requiredExtensions.numberOfItems
            checks.Equal "switch skipped extension" "urn:ranvier:unsupported" (skipped.requiredExtensions.getItem 0u)
            do! color "switch renders supported child" lime (320., 20.)
            skipped.removeAttribute "requiredExtensions"
            checks.Equal "removed condition list length" 0u skipped.requiredExtensions.numberOfItems
            do! color "inserted switch keeps its selection" lime (320., 20.)
            let fresh = create<SVGSwitchElement> document "switch" []

            for fill in [ "red"; "lime" ] do
                fresh.appendChild (create<Element> document "rect" [ "x", "360"; "width", "40"; "height", "40"; "fill", fill ])
                |> ignore

            root.appendChild fresh |> ignore
            do! color "switch renders first unconditioned child" SvgChecks.red (380., 20.)
            checks.Equal "link href" "#target" link.href.baseVal
            checks.Equal "link target" "_blank" link.target.baseVal
            checks.Equal "link rel" "noopener" link.rel
            link.relList.add [| "noreferrer" |]
            checks.Equal "link relList writes rel" "noopener noreferrer" (link.getAttribute "rel")
            link.hreflang <- "en"
            checks.Equal "link hreflang reflects" "en" (link.getAttribute "hreflang")
            checks.Equal "style media" "all" style.media
            let titled = create<SVGStyleElement> document "style" [ "title", "resources" ]
            checks.Equal "detached style title" "resources" titled.title
            titled.title <- "renamed"
            checks.Equal "style title writes attribute" "renamed" (titled.getAttribute "title")
            checks.Equal "style enabled" false style.disabled
            do! color "style applies" lime (320., 80.)
            style.disabled <- true
            do! color "disabled style" SvgChecks.red (320., 80.)
            style.disabled <- false
            checks.Equal "title text" "Resources" title.textContent
            checks.Equal "desc text" "Shared resource fixture" desc.textContent
            checks.Equal "metadata element" "metadata" metadata.localName
            let script = create<SVGScriptElement> document "script" [ "type", "text/plain" ]
            checks.Equal "script href empty" "" script.href.baseVal
            script.href.baseVal <- "data:text/plain,inert"
            checks.Equal "script href reflects" "data:text/plain,inert" (script.getAttribute "href")
            return checks.Count
        finally
            document.body.removeChild root |> ignore
    }
