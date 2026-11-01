module internal NativeApiConformance.SvgFilters

open Ranvier.Dom
open NativeApiConformance.SvgChecks

/// <summary>Checks filter and primitive reflection and the rendered effect of edited primitive parameters.</summary>
let run (document: Document) (pixel: Pixel) (errorName: exn -> string) =
    async {
        let checks = Checks errorName

        let root =
            create<SVGSVGElement> document "svg" [ "width", "400"; "height", "200"; "style", "display:block" ]

        let defs = create<Element> document "defs" []
        root.appendChild defs |> ignore

        let filter id (attributes: (string * string) list) (primitives: Element list) =
            let node = create<SVGFilterElement> document "filter" ([ "id", id ] @ attributes)

            for primitive in primitives do
                node.appendChild primitive |> ignore

            defs.appendChild node |> ignore
            node

        let matrix =
            create<SVGFEColorMatrixElement> document "feColorMatrix" [ "type", "matrix"; "values", "0 0 1 0 0 0 1 0 0 0 1 0 0 0 0 0 0 0 1 0" ]

        let transfer =
            create<SVGFEComponentTransferElement> document "feComponentTransfer" []

        let red =
            create<SVGComponentTransferFunctionElement> document "feFuncR" [ "type", "linear"; "slope", "0"; "intercept", "1" ]

        transfer.appendChild red |> ignore

        let lighting =
            create<SVGFEDiffuseLightingElement>
                document
                "feDiffuseLighting"
                [ "lighting-color", "white"; "surfaceScale", "1"; "diffuseConstant", "1" ]

        let light =
            create<SVGFEDistantLightElement> document "feDistantLight" [ "azimuth", "0"; "elevation", "90" ]

        lighting.appendChild light |> ignore

        let blur =
            create<SVGFEGaussianBlurElement> document "feGaussianBlur" [ "stdDeviation", "0" ]

        let offset = create<SVGFEOffsetElement> document "feOffset" [ "dx", "0"; "dy", "0" ]
        let matrixFilter = filter "filter-matrix" [] [ matrix ]
        filter "filter-transfer" [] [ transfer ] |> ignore
        filter "filter-lighting" [] [ lighting ] |> ignore

        filter "filter-blur" [ "x", "-50%"; "width", "200%" ] [ blur ]
        |> ignore

        filter
            "filter-offset"
            [
                "filterUnits", "userSpaceOnUse"
                "x", "0"
                "y", "100"
                "width", "400"
                "height", "100"
            ]
            [ offset ]
        |> ignore

        for x, y, width, fill, id in
            [
                "0", "0", "100", "red", "filter-matrix"
                "100", "0", "100", "black", "filter-transfer"
                "200", "0", "100", "black", "filter-lighting"
                "300", "0", "40", "lime", "filter-blur"
                "0", "100", "20", "lime", "filter-offset"
            ] do
            root.appendChild (
                create<Element>
                    document
                    "rect"
                    [
                        "x", x
                        "y", y
                        "width", width
                        "height", "100"
                        "fill", fill
                        "filter", $"url(#{id})"
                    ]
            )
            |> ignore

        document.body.appendChild root |> ignore

        let sample point =
            at pixel root point

        let color label expected point =
            async {
                let! actual = sample point
                checks.Color label expected actual
            }

        try
            checks.Equal "filter units default" 2us matrixFilter.filterUnits.baseVal
            checks.Equal "primitive units default" 1us matrixFilter.primitiveUnits.baseVal
            checks.Equal "filter x default" "-10%" matrixFilter.x.baseVal.valueAsString
            checks.Equal "filter width default" "120%" matrixFilter.width.baseVal.valueAsString
            checks.Equal "filter href empty" "" matrixFilter.href.baseVal
            checks.Equal "matrix value count" 20u matrix.values.baseVal.numberOfItems
            checks.Equal "matrix values identity" true (obj.ReferenceEquals (matrix.values.baseVal, matrix.values.baseVal))
            checks.Equal "matrix input default" "" matrix.in1.baseVal
            do! color "channel swap" SvgChecks.blue (50., 50.)
            let values = matrix.values.baseVal
            (values.getItem 0u).value <- 1.f
            (values.getItem 2u).value <- 0.f
            (values.getItem 10u).value <- 0.f
            (values.getItem 12u).value <- 1.f
            checks.Equal "matrix list edit writes attribute" "1 0 0 0 0 0 1 0 0 0 0 0 1 0 0 0 0 0 1 0" (matrix.getAttribute "values")
            do! color "identity matrix" SvgChecks.red (50., 50.)
            checks.Near "transfer slope" 0. (float red.slope.baseVal)
            checks.Near "transfer intercept" 1. (float red.intercept.baseVal)
            checks.Near "transfer amplitude default" 1. (float red.amplitude.baseVal)
            checks.Near "transfer exponent default" 1. (float red.exponent.baseVal)
            checks.Equal "transfer table empty" 0u red.tableValues.baseVal.numberOfItems
            do! color "linear transfer" SvgChecks.red (150., 50.)
            red.intercept.baseVal <- 0.f
            checks.Equal "transfer edit writes attribute" "0" (red.getAttribute "intercept")
            do! color "edited transfer" black (150., 50.)
            checks.Near "lighting surface scale" 1. (float lighting.surfaceScale.baseVal)
            checks.Near "lighting constant" 1. (float lighting.diffuseConstant.baseVal)
            checks.Near "light elevation" 90. (float light.elevation.baseVal)
            checks.Near "light azimuth" 0. (float light.azimuth.baseVal)
            do! color "overhead light" white (250., 50.)
            light.elevation.baseVal <- 0.f
            do! color "grazing light" black (250., 50.)
            light.elevation.baseVal <- 90.f
            lighting.diffuseConstant.baseVal <- 0.f
            do! color "unlit surface" black (250., 50.)
            checks.Near "blur deviation x" 0. (float blur.stdDeviationX.baseVal)
            do! color "sharp edge" white (345., 50.)
            blur.setStdDeviation (6.f, 6.f)
            checks.Near "blur deviation x edit" 6. (float blur.stdDeviationX.baseVal)
            checks.Near "blur deviation y edit" 6. (float blur.stdDeviationY.baseVal)
            let! (r, g, _) = sample (345., 50.)
            checks.Equal "blur spreads color" true (r < 235 && g > 200)
            checks.Near "offset dx" 0. (float offset.dx.baseVal)
            do! color "unshifted offset" lime (10., 150.)
            do! color "unshifted offset gap" white (60., 150.)
            offset.dx.baseVal <- 50.f
            checks.Equal "offset edit writes attribute" "50" (offset.getAttribute "dx")
            do! color "shifted offset" lime (60., 150.)
            do! color "shifted offset source" white (10., 150.)

            let standard (label: string) (result: SVGAnimatedString) (x: SVGAnimatedLength) (width: SVGAnimatedLength) =
                checks.Equal (label + " result default") "" result.baseVal
                checks.Equal (label + " x default") "0%" x.baseVal.valueAsString
                checks.Equal (label + " width default") "100%" width.baseVal.valueAsString
                result.baseVal <- "out"
                checks.Equal (label + " result reflects") "out" result.animVal

            let blend = create<SVGFEBlendElement> document "feBlend" []
            standard "blend" blend.result blend.x blend.width
            checks.Equal "blend mode default" 1us blend.mode.baseVal
            checks.Equal "blend second input" "" blend.in2.baseVal
            let composite = create<SVGFECompositeElement> document "feComposite" []
            standard "composite" composite.result composite.x composite.width
            checks.Equal "composite operator default" 1us composite.``operator``.baseVal
            checks.Near "composite k1" 0. (float composite.k1.baseVal)

            let displacement =
                create<SVGFEDisplacementMapElement> document "feDisplacementMap" []

            standard "displacement" displacement.result displacement.x displacement.width
            checks.Equal "displacement x channel" 4us displacement.xChannelSelector.baseVal
            checks.Near "displacement scale" 0. (float displacement.scale.baseVal)
            let shadow = create<SVGFEDropShadowElement> document "feDropShadow" []
            standard "drop shadow" shadow.result shadow.x shadow.width
            checks.Near "drop shadow dx" 2. (float shadow.dx.baseVal)
            checks.Near "drop shadow deviation" 2. (float shadow.stdDeviationX.baseVal)
            shadow.setStdDeviation (3.f, 4.f)
            checks.Near "drop shadow deviation x edit" 3. (float shadow.stdDeviationX.baseVal)
            checks.Near "drop shadow deviation y edit" 4. (float shadow.stdDeviationY.animVal)
            let flood = create<SVGFEFloodElement> document "feFlood" []
            standard "flood" flood.result flood.x flood.width
            let image = create<SVGFEImageElement> document "feImage" []
            standard "image" image.result image.x image.width
            checks.Equal "image aspect default" 6us image.preserveAspectRatio.baseVal.align
            let merge = create<SVGFEMergeElement> document "feMerge" []
            standard "merge" merge.result merge.x merge.width

            let node =
                create<SVGFEMergeNodeElement> document "feMergeNode" [ "in", "SourceAlpha" ]

            checks.Equal "merge node input" "SourceAlpha" node.in1.baseVal

            let morphology =
                create<SVGFEMorphologyElement> document "feMorphology" [ "radius", "2 3" ]

            standard "morphology" morphology.result morphology.x morphology.width
            checks.Equal "morphology operator default" 1us morphology.``operator``.baseVal
            checks.Near "morphology radius y" 3. (float morphology.radiusY.baseVal)
            let specular = create<SVGFESpecularLightingElement> document "feSpecularLighting" []
            standard "specular" specular.result specular.x specular.width
            checks.Near "specular exponent" 1. (float specular.specularExponent.baseVal)
            let tile = create<SVGFETileElement> document "feTile" []
            standard "tile" tile.result tile.x tile.width

            let turbulence =
                create<SVGFETurbulenceElement> document "feTurbulence" [ "baseFrequency", "0.1 0.2" ]

            standard "turbulence" turbulence.result turbulence.x turbulence.width
            checks.Near "turbulence frequency y" 0.2 (float turbulence.baseFrequencyY.baseVal)
            checks.Equal "turbulence octaves" 1 turbulence.numOctaves.baseVal
            checks.Equal "turbulence stitch default" 2us turbulence.stitchTiles.baseVal
            standard "matrix" matrix.result matrix.x matrix.width
            standard "transfer" transfer.result transfer.x transfer.width
            standard "lighting" lighting.result lighting.x lighting.width
            standard "blur" blur.result blur.x blur.width

            let point =
                create<SVGFEPointLightElement> document "fePointLight" [ "x", "1"; "y", "2"; "z", "3" ]

            checks.Near "point light z" 3. (float point.z.baseVal)
            let spot = create<SVGFESpotLightElement> document "feSpotLight" [ "pointsAtX", "4" ]
            checks.Near "spot light target" 4. (float spot.pointsAtX.baseVal)
            checks.Near "spot light exponent default" 1. (float spot.specularExponent.baseVal)
            return checks.Count
        finally
            document.body.removeChild root |> ignore
    }
