module internal NativeApiConformance.SvgPaintServers

open Ranvier.Dom
open NativeApiConformance.SvgChecks

/// <summary>Checks gradient, stop and pattern reflection, inheritance and their rendered paint.</summary>
let run (document: Document) (pixel: Pixel) (errorName: exn -> string) =
    async {
        let checks = Checks errorName

        let root =
            create<SVGSVGElement> document "svg" [ "width", "300"; "height", "100"; "style", "display:block" ]

        let defs = create<Element> document "defs" []

        let linear =
            create<SVGLinearGradientElement> document "linearGradient" [ "id", "paint-linear"; "x1", "0"; "x2", "1" ]

        let stops =
            [ 0., "red"; 0.5, "red"; 0.5, "blue"; 1., "blue" ]
            |> List.map (fun (offset, color) ->
                let stop =
                    create<SVGStopElement> document "stop" [ "offset", string offset; "stop-color", color ]

                linear.appendChild stop |> ignore
                stop)

        let inherited =
            create<SVGLinearGradientElement> document "linearGradient" [ "id", "paint-inherited"; "href", "#paint-linear" ]

        let radial =
            create<SVGRadialGradientElement> document "radialGradient" [ "id", "paint-radial"; "cx", "0.5"; "cy", "0.5"; "r", "0.5" ]

        for offset, color in [ 0., "red"; 0.5, "red"; 0.5, "blue"; 1., "blue" ] do
            radial.appendChild (create<SVGStopElement> document "stop" [ "offset", string offset; "stop-color", color ])
            |> ignore

        let pattern =
            create<SVGPatternElement>
                document
                "pattern"
                [
                    "id", "paint-pattern"
                    "patternUnits", "userSpaceOnUse"
                    "width", "20"
                    "height", "20"
                ]

        pattern.appendChild (create<Element> document "rect" [ "width", "10"; "height", "20"; "fill", "lime" ])
        |> ignore

        for node in [ linear :> Element; inherited; radial; pattern ] do
            defs.appendChild node |> ignore

        root.appendChild defs |> ignore

        for x, y, width, height, fill in
            [
                "0", "0", "100", "50", "url(#paint-linear)"
                "100", "0", "100", "50", "url(#paint-inherited)"
                "200", "0", "100", "100", "url(#paint-radial)"
                "0", "50", "100", "50", "url(#paint-pattern)"
            ] do
            root.appendChild (create<Element> document "rect" [ "x", x; "y", y; "width", width; "height", height; "fill", fill ])
            |> ignore

        document.body.appendChild root |> ignore

        let color label expected point =
            async {
                let! actual = at pixel root point
                checks.Color label expected actual
            }

        try
            checks.Equal "gradient units default" 2us linear.gradientUnits.baseVal
            checks.Equal "spread method default" 1us linear.spreadMethod.baseVal
            checks.Near "x1" 0. (float linear.x1.baseVal.value)
            checks.Near "x2" 1. (float linear.x2.baseVal.value)
            checks.Equal "gradient transform empty" 0u linear.gradientTransform.baseVal.numberOfItems
            checks.Equal "inherited href" "#paint-linear" inherited.href.baseVal
            checks.Near "stop offset" 0.5 (float stops[1].offset.baseVal)
            checks.Near "stop animated offset" 0.5 (float stops[1].offset.animVal)
            checks.Equal "stop offset identity" true (obj.ReferenceEquals (stops[1].offset, stops[1].offset))
            do! color "linear start" red (25., 25.)
            do! color "linear end" blue (75., 25.)
            do! color "inherited start" red (125., 25.)
            do! color "inherited end" blue (175., 25.)
            stops[1].offset.baseVal <- 0.25f
            stops[2].offset.baseVal <- 0.25f
            checks.Equal "stop offset writes attribute" "0.25" (stops[1].getAttribute "offset")
            do! color "edited stop offset" blue (37., 25.)
            do! color "inherited stops follow edit" blue (137., 25.)
            stops[0].setAttribute("stop-color", "lime")
            stops[1].setAttribute("stop-color", "lime")
            do! color "edited stop color" lime (10., 25.)
            linear.x1.baseVal.value <- 1.f
            linear.x2.baseVal.value <- 0.f
            do! color "reversed vector start" blue (10., 25.)
            do! color "reversed vector end" lime (90., 25.)
            linear.x1.baseVal.value <- 0.f
            linear.x2.baseVal.value <- 1.f
            linear.setAttribute ("gradientTransform", "translate(0.5,0)")
            checks.Equal "gradient transform count" 1u linear.gradientTransform.baseVal.numberOfItems
            checks.Equal "gradient transform kind" 2us (linear.gradientTransform.baseVal.getItem 0u).``type``
            do! color "padded before transformed start" lime (10., 25.)
            do! color "transformed stop" blue (90., 25.)
            linear.spreadMethod.baseVal <- 2us
            checks.Equal "spread method writes attribute" "reflect" (linear.getAttribute "spreadMethod")
            do! color "reflected spread" blue (10., 25.)
            checks.Fails "invalid spread method" "TypeError" (fun () -> linear.spreadMethod.baseVal <- 9us)
            checks.Equal "rejected spread preserved" 2us linear.spreadMethod.baseVal
            linear.gradientUnits.baseVal <- 1us
            checks.Equal "gradient units writes attribute" "userSpaceOnUse" (linear.getAttribute "gradientUnits")
            linear.gradientUnits.baseVal <- 2us
            checks.Near "radial cx" 0.5 (float radial.cx.baseVal.value)
            checks.Near "radial r" 0.5 (float radial.r.baseVal.value)
            checks.Near "radial fr" 0. (float radial.fr.baseVal.value)
            do! color "radial center" red (250., 50.)
            do! color "radial corner" blue (205., 5.)
            do! color "radial inner ring" red (270., 50.)
            radial.r.baseVal.value <- 0.2f
            do! color "edited radial radius" blue (270., 50.)
            do! color "edited radial center" red (250., 50.)
            radial.cx.baseVal.value <- 0.2f
            radial.fx.baseVal.valueAsString <- "0.2"
            checks.Equal "focal edit replaces percentage" "0.2" (radial.getAttribute "fx")
            do! color "moved radial center" red (220., 50.)
            do! color "moved radial old center" blue (250., 50.)
            checks.Equal "pattern units" 1us pattern.patternUnits.baseVal
            checks.Equal "pattern content units" 1us pattern.patternContentUnits.baseVal
            checks.Near "pattern width" 20. (float pattern.width.baseVal.value)
            checks.Equal "pattern transform empty" 0u pattern.patternTransform.baseVal.numberOfItems
            do! color "pattern tile" lime (5., 75.)
            do! color "pattern gap" white (15., 75.)
            pattern.x.baseVal.value <- 10.f
            do! color "moved pattern gap" white (5., 75.)
            do! color "moved pattern tile" lime (15., 75.)
            pattern.setAttribute ("patternTransform", "translate(10,0)")
            checks.Equal "pattern transform count" 1u pattern.patternTransform.baseVal.numberOfItems
            do! color "transformed pattern tile" lime (5., 75.)
            return checks.Count
        finally
            document.body.removeChild root |> ignore
    }
