module internal NativeApiConformance.SvgText

open System
open Ranvier.Dom

let private create<'T> (document: Document) name attributes : 'T =
    let element = document.createElementNS ("http://www.w3.org/2000/svg", name)

    for key, value in attributes do
        element.setAttribute (key, value)

    unbox<'T> element

/// <summary>Checks real Ahem SVG text geometry, positioning, selection, text paths and painted glyph centers.</summary>
let run
    (document: Document)
    (fontData: string)
    (wait: int -> Async<unit>)
    (paint: float * float -> Async<bool>)
    (errorName: exn -> string)
    (nativeVertical: bool)
    =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwithf "%s: expected %A, got %A" label expected actual

        let near label expected actual =
            checks <- checks + 1

            if
                Double.IsNaN actual
                || Double.IsInfinity actual
                || abs (expected - actual) > 0.001
            then
                failwithf "%s: expected %g, got %g" label expected actual

        let failure label action =
            let outcome =
                try
                    action ()
                    "accepted"
                with error ->
                    errorName error

            equal label "IndexSizeError" outcome

        let root =
            create<SVGSVGElement> document "svg" [ "width", "500"; "height", "400"; "viewBox", "0 0 500 400" ]

        let style = document.createElement "style"
        style.textContent <- $"@font-face{{font-family:RanvierSurfacesAhem;src:url(data:font/ttf;base64,{fontData})}}"
        document.head.appendChild style |> ignore
        document.body.appendChild root |> ignore

        let text attributes =
            let node =
                create<SVGTextElement> document "text" ([ "x", "10"; "y", "50" ] @ attributes)

            node.setAttribute ("style", "font:20px RanvierSurfacesAhem;fill:black")
            node.textContent <- "ABC"
            root.appendChild node |> ignore
            node

        let point label x y (value: SVGPoint) =
            near (label + " x") x (float value.x)
            near (label + " y") y (float value.y)

        let probe = text []

        try
            let mutable attempts = 0

            while abs (float (probe.getComputedTextLength ()) - 60.) > 0.001
                  && attempts < 300 do
                do! wait 10
                attempts <- attempts + 1

            near "actual Ahem font readiness" 60. (float (probe.getComputedTextLength ()))
            root.removeChild probe |> ignore

            for label, attributes, starts, advance, total in
                [
                    "plain", [], [| 10.; 30.; 50. |], 20., 60.
                    "spacing", [ "textLength", "120"; "lengthAdjust", "spacing" ], [| 10.; 60.; 110. |], 20., 60.
                    "glyph scaling", [ "textLength", "120"; "lengthAdjust", "spacingAndGlyphs" ], [| 10.; 50.; 90. |], 40., 120.
                ] do
                let node = text attributes
                equal (label + " character count") 3 (node.getNumberOfChars ())
                near (label + " computed length") total (float (node.getComputedTextLength ()))
                near (label + " substring") (advance * 2.) (float (node.getSubStringLength (0u, 2u)))
                near (label + " substring count clamps") (advance * 2.) (float (node.getSubStringLength (1u, 100u)))

                for index in 0..2 do
                    let position = uint32 index
                    point (label + " start") starts[index] 50. (node.getStartPositionOfChar position)
                    point (label + " end") (starts[index] + advance) 50. (node.getEndPositionOfChar position)
                    near (label + " rotation") 0. (float (node.getRotationOfChar position))
                    let rect = node.getExtentOfChar position
                    near (label + " extent x") starts[index] (float rect.x)
                    near (label + " extent y") 34. (float rect.y)
                    near (label + " extent width") advance (float rect.width)
                    near (label + " extent height") 20. (float rect.height)
                    let x = float rect.x + float rect.width / 2.
                    let y = float rect.y + float rect.height / 2.
                    equal (label + " center hit") index (node.getCharNumAtPosition (DOMPointInit (x = x, y = y)))
                    let sample = root.createSVGPoint ()
                    sample.x <- float32 x
                    sample.y <- float32 y
                    let screen = sample.matrixTransform (root.getScreenCTM ())
                    let! black = paint (float screen.x, float screen.y)
                    equal (label + " actual painted glyph center") true black

                equal (label + " outside text hit") -1 (node.getCharNumAtPosition (DOMPointInit (x = -100., y = -100.)))
                failure (label + " invalid start") (fun () -> node.getStartPositionOfChar 3u |> ignore)
                failure (label + " invalid end") (fun () -> node.getEndPositionOfChar 3u |> ignore)
                failure (label + " invalid extent") (fun () -> node.getExtentOfChar 3u |> ignore)
                failure (label + " invalid rotation") (fun () -> node.getRotationOfChar 3u |> ignore)
                failure (label + " invalid substring") (fun () -> node.getSubStringLength (3u, 1u) |> ignore)
                failure (label + " invalid selection") (fun () -> node.selectSubString (3u, 1u))
                node.selectSubString (1u, 2u)
                let selection = document.getSelection ()
                equal (label + " selection exists") 1u selection.rangeCount
                let range = selection.getRangeAt 0u
                equal (label + " selection start") 1u range.startOffset
                equal (label + " selection end") 3u range.endOffset
                selection.removeAllRanges ()
                root.removeChild node |> ignore

            let positioned =
                text
                    [
                        "x", "10 40 80"
                        "y", "50 55 60"
                        "dx", "1 2 3"
                        "dy", "4 5 6"
                        "rotate", "0 30 60"
                    ]

            for label, list in
                [
                    "x", positioned.x
                    "y", positioned.y
                    "dx", positioned.dx
                    "dy", positioned.dy
                ] do
                equal (label + " position list length") 3u list.baseVal.numberOfItems
                equal (label + " position list identity") true (obj.ReferenceEquals (list.baseVal, list.baseVal))

            equal "rotation list length" 3u positioned.rotate.baseVal.numberOfItems

            for index, x, y, angle in [ 0u, 11., 54., 0.; 1u, 42., 60., 30.; 2u, 83., 66., 60. ] do
                point "positioned start" x y (positioned.getStartPositionOfChar index)
                near "positioned rotation" angle (float (positioned.getRotationOfChar index))
                let box = positioned.getExtentOfChar index

                equal
                    "positioned center hit"
                    (int index)
                    (positioned.getCharNumAtPosition (DOMPointInit (x = float box.x + float box.width / 2., y = float box.y + float box.height / 2.)))

            positioned.x.baseVal.getItem(0u).value <- 20.f
            point "live positioning list mutation" 21. 54. (positioned.getStartPositionOfChar 0u)
            root.removeChild positioned |> ignore

            let parent = text []
            parent.textContent <- ""
            let span = create<SVGTSpanElement> document "tspan" [ "x", "20"; "dy", "10" ]
            span.textContent <- "ABC"
            parent.appendChild span |> ignore
            equal "tspan character count" 3 (span.getNumberOfChars ())
            near "tspan computed length" 60. (float (span.getComputedTextLength ()))
            point "tspan positioning" 20. 60. (span.getStartPositionOfChar 0u)

            span.dx.baseVal.initialize (root.createSVGLength ())
            |> ignore

            span.dx.baseVal.getItem(0u).value <- 5.f
            point "tspan live positioning edit" 25. 60. (span.getStartPositionOfChar 0u)
            root.removeChild parent |> ignore

            let path =
                create<SVGPathElement> document "path" [ "id", "surfaces-text-path"; "d", "M10 150 H310" ]

            root.appendChild path |> ignore
            let pathText = text []
            pathText.removeAttribute "x"
            pathText.removeAttribute "y"
            pathText.textContent <- ""

            let onPath =
                create<SVGTextPathElement> document "textPath" [ "href", "#surfaces-text-path" ]

            onPath.textContent <- "ABC"
            pathText.appendChild onPath |> ignore
            equal "textPath href value" "#surfaces-text-path" onPath.href.baseVal
            equal "textPath character count" 3 (onPath.getNumberOfChars ())
            near "textPath computed length" 60. (float (onPath.getComputedTextLength ()))
            point "textPath actual start" 10. 150. (onPath.getStartPositionOfChar 0u)
            onPath.startOffset.baseVal.value <- 20.f
            point "textPath offset mutation" 30. 150. (onPath.getStartPositionOfChar 0u)
            equal "textPath method default" 1us onPath.method.baseVal
            equal "textPath spacing default" 2us onPath.spacing.baseVal
            onPath.method.baseVal <- 2us
            equal "textPath method mutation" 2us onPath.method.baseVal
            onPath.method.baseVal <- 1us
            onPath.spacing.baseVal <- 1us
            equal "textPath spacing mutation" 1us onPath.spacing.baseVal
            onPath.spacing.baseVal <- 2us
            root.removeChild pathText |> ignore
            root.removeChild path |> ignore

            let vertical =
                text [ "x", "300"; "y", "40"; "textLength", "90"; "lengthAdjust", "spacing" ]

            vertical.setAttribute ("style", "font:20px RanvierSurfacesAhem;fill:black;writing-mode:vertical-rl")
            near "vertical computed length" 60. (float (vertical.getComputedTextLength ()))

            for index in 0..2 do
                let number = uint32 index
                let start = vertical.getStartPositionOfChar number
                let finish = vertical.getEndPositionOfChar number

                if nativeVertical then
                    point "pinned native vertical start" 294. (46. + 35. * float index) start
                    point "pinned native vertical end" 274. (46. + 35. * float index) finish
                else
                    point "Chromium vertical start" 300. (40. + 35. * float index) start
                    point "Chromium vertical end" 300. (60. + 35. * float index) finish

                let box = vertical.getExtentOfChar number
                near "vertical extent x" 290. (float box.x)
                near "vertical extent y" (40. + 35. * float index) (float box.y)
                near "vertical extent width" 20. (float box.width)
                near "vertical extent height" 20. (float box.height)
                equal "vertical center hit" index (vertical.getCharNumAtPosition (DOMPointInit (x = float box.x + 10., y = float box.y + 10.)))

            root.removeChild vertical |> ignore
            return checks
        finally
            if not (isNull (box root.parentNode)) then
                root.parentNode.removeChild root |> ignore

            if not (isNull (box style.parentNode)) then
                style.parentNode.removeChild style |> ignore
    }
