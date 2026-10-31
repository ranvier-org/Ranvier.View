module internal NativeApiConformance.SvgClipping

open Ranvier.Dom
open NativeApiConformance.SvgChecks

/// <summary>Checks clipPath, mask and marker reflection and their rendered clipping and marker output.</summary>
let run (document: Document) (pixel: Pixel) (errorName: exn -> string) =
    async {
        let checks = Checks errorName

        let root =
            create<SVGSVGElement> document "svg" [ "width", "320"; "height", "100"; "style", "display:block" ]

        let defs = create<Element> document "defs" []
        let clip = create<SVGClipPathElement> document "clipPath" [ "id", "clip-left" ]

        let clipShape =
            create<SVGRectElement> document "rect" [ "width", "50"; "height", "100" ]

        clip.appendChild clipShape |> ignore

        let mask =
            create<SVGMaskElement> document "mask" [ "id", "mask-left"; "maskContentUnits", "userSpaceOnUse" ]

        let maskShape =
            create<SVGRectElement> document "rect" [ "x", "100"; "width", "50"; "height", "100"; "fill", "white" ]

        mask.appendChild maskShape |> ignore

        let marker =
            create<SVGMarkerElement>
                document
                "marker"
                [
                    "id", "marker-end"
                    "markerUnits", "userSpaceOnUse"
                    "markerWidth", "10"
                    "markerHeight", "10"
                    "refX", "0"
                    "refY", "5"
                    "orient", "auto"
                    "overflow", "visible"
                ]

        marker.appendChild (create<Element> document "rect" [ "width", "10"; "height", "10"; "fill", "lime" ])
        |> ignore

        for node in [ clip :> Element; mask; marker ] do
            defs.appendChild node |> ignore

        root.appendChild defs |> ignore

        root.appendChild (
            create<Element>
                document
                "rect"
                [
                    "width", "100"
                    "height", "100"
                    "fill", "red"
                    "clip-path", "url(#clip-left)"
                ]
        )
        |> ignore

        root.appendChild (
            create<Element>
                document
                "rect"
                [
                    "x", "100"
                    "width", "100"
                    "height", "100"
                    "fill", "blue"
                    "mask", "url(#mask-left)"
                ]
        )
        |> ignore

        root.appendChild (
            create<Element>
                document
                "path"
                [
                    "d", "M220 50 H280"
                    "stroke", "black"
                    "stroke-width", "1"
                    "marker-end", "url(#marker-end)"
                ]
        )
        |> ignore

        document.body.appendChild root |> ignore

        let color label expected point =
            async {
                let! actual = at pixel root point
                checks.Color label expected actual
            }

        try
            checks.Equal "clip units default" 1us clip.clipPathUnits.baseVal
            checks.Equal "clip transform empty" 0u clip.transform.baseVal.numberOfItems
            do! color "clipped inside" SvgChecks.red (25., 50.)
            do! color "clipped outside" white (75., 50.)
            clipShape.width.baseVal.value <- 80.f
            do! color "edited clip geometry" SvgChecks.red (75., 50.)
            do! color "edited clip outside" white (90., 50.)
            clip.setAttribute ("transform", "translate(20,0)")
            checks.Equal "clip transform count" 1u clip.transform.baseVal.numberOfItems
            do! color "transformed clip" SvgChecks.red (90., 50.)
            do! color "transformed clip uncovered" white (10., 50.)
            clip.removeAttribute "transform"
            checks.Equal "removed clip transform" 0u clip.transform.baseVal.numberOfItems
            clip.clipPathUnits.baseVal <- 2us
            checks.Equal "clip units writes attribute" "objectBoundingBox" (clip.getAttribute "clipPathUnits")
            do! color "bounding-box clip covers" SvgChecks.red (95., 50.)
            clipShape.width.baseVal.value <- 0.5f
            do! color "bounding-box clip half" white (75., 50.)
            do! color "bounding-box clip kept half" SvgChecks.red (25., 50.)
            checks.Equal "mask units default" 2us mask.maskUnits.baseVal
            checks.Equal "mask content units" 1us mask.maskContentUnits.baseVal
            checks.Equal "mask x default" "-10%" mask.x.baseVal.valueAsString
            checks.Equal "mask width default" "120%" mask.width.baseVal.valueAsString
            checks.Equal "mask tests empty" 0u mask.requiredExtensions.numberOfItems
            do! color "masked inside" SvgChecks.blue (125., 50.)
            do! color "masked outside" white (175., 50.)
            mask.maskContentUnits.baseVal <- 2us
            maskShape.x.baseVal.value <- 0.5f
            maskShape.width.baseVal.value <- 0.5f
            maskShape.height.baseVal.value <- 1.f
            do! color "bounding-box mask uncovered" white (125., 50.)
            do! color "bounding-box mask covered" SvgChecks.blue (175., 50.)
            mask.width.baseVal.valueAsString <- "60%"
            do! color "mask region clips content" white (175., 50.)
            checks.Equal "marker units" 1us marker.markerUnits.baseVal
            checks.Near "marker width" 10. (float marker.markerWidth.baseVal.value)
            checks.Near "marker ref y" 5. (float marker.refY.baseVal.value)
            checks.Equal "marker orient auto" 1us marker.orientType.baseVal
            do! color "auto-oriented marker" lime (285., 50.)

            marker.setOrientToAngle (
                let angle = root.createSVGAngle ()
                angle.value <- 90.f
                angle
            )

            checks.Equal "marker orient angle" 2us marker.orientType.baseVal
            checks.Near "marker angle value" 90. (float marker.orientAngle.baseVal.value)
            checks.Equal "marker orient writes attribute" "90" (marker.getAttribute "orient")
            do! color "rotated marker leaves path end" white (287., 50.)
            do! color "rotated marker below end" lime (280., 57.)
            marker.setOrientToAuto ()
            checks.Equal "marker orient auto again" 1us marker.orientType.baseVal
            marker.refX.baseVal.value <- 5.f
            do! color "marker reference shift" lime (277., 50.)
            do! color "marker reference shift end" white (287., 50.)
            marker.markerUnits.baseVal <- 2us
            checks.Equal "marker units writes attribute" "strokeWidth" (marker.getAttribute "markerUnits")
            return checks.Count
        finally
            document.body.removeChild root |> ignore
    }
