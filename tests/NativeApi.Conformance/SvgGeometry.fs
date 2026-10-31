module internal NativeApiConformance.SvgGeometry

open System
open Ranvier.Dom

/// <summary>Checks SVG local bounds, viewport transforms, viewBox mutations, path sampling and hit coordinates.</summary>
/// <param name="document">The active HTML document receiving a temporary SVG subtree.</param>
/// <param name="errorName">Reads the engine's observable exception name.</param>
let run (document: Document) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let near label expected actual =
        checks <- checks + 1

        if
            Double.IsNaN actual
            || abs (expected - actual) > 0.001
        then
            failwithf "%s: expected %g, got %g" label expected actual

    let point label x y (value: SVGPoint) =
        near (label + " x") x (float value.x)
        near (label + " y") y (float value.y)

    let matrix label (a, b, c, d, e, f) (value: SVGMatrix) =
        for field, expected, actual in
            [
                "a", a, value.a
                "b", b, value.b
                "c", c, value.c
                "d", d, value.d
                "e", e, value.e
                "f", f, value.f
            ] do
            near (label + " " + field) expected actual

    let bounds label (x, y, width, height) (value: SVGRect) =
        for field, expected, actual in
            [
                "x", x, value.x
                "y", y, value.y
                "width", width, value.width
                "height", height, value.height
            ] do
            near (label + " " + field) expected (float actual)

    let rejects label expected action =
        let actual =
            try
                action ()
                "accepted"
            with error ->
                errorName error

        equal label expected actual

    let create tag attributes =
        let value = document.createElementNS ("http://www.w3.org/2000/svg", tag)

        for name, text in attributes do
            value.setAttribute (name, text)

        value

    let root =
        create "svg" [ "width", "300"; "height", "200"; "viewBox", "0 0 300 200" ] :?> SVGSVGElement

    document.body.appendChild root |> ignore

    try
        let group = create "g" [ "transform", "translate(20 30)" ] :?> SVGGraphicsElement

        let rectangle =
            create
                "rect"
                [
                    "x", "4"
                    "y", "6"
                    "width", "40"
                    "height", "25"
                    "stroke", "black"
                    "stroke-width", "10"
                ]
            :?> SVGGeometryElement

        root.appendChild group |> ignore
        group.appendChild rectangle |> ignore
        bounds "local rectangle" (4., 6., 40., 25.) (rectangle.getBBox ())
        bounds "group excludes its own transform" (4., 6., 40., 25.) (group.getBBox ())
        matrix "rectangle CTM" (1., 0., 0., 1., 20., 30.) (rectangle.getCTM ())
        let viewport = root.getBoundingClientRect ()
        matrix "rectangle screen CTM" (1., 0., 0., 1., viewport.x + 20., viewport.y + 30.) (rectangle.getScreenCTM ())
        equal "CTM snapshots are distinct" false (obj.ReferenceEquals (rectangle.getCTM (), rectangle.getCTM ()))
        let snapshot = rectangle.getBBox ()
        snapshot.width <- 99.f
        bounds "bounds mutation stays independent" (4., 6., 40., 25.) (rectangle.getBBox ())

        equal "viewBox wrapper identity" true (obj.ReferenceEquals (root.viewBox, root.viewBox))
        equal "viewBox base identity" true (obj.ReferenceEquals (root.viewBox.baseVal, root.viewBox.baseVal))
        root.viewBox.baseVal.width <- 600.f
        matrix "viewBox meet scaling" (0.5, 0., 0., 0.5, 0., 50.) (root.getCTM ())
        root.preserveAspectRatio.baseVal.align <- 1us
        matrix "viewBox none scaling" (0.5, 0., 0., 1., 0., 0.) (root.getCTM ())
        equal "aspect change is live" 1us root.preserveAspectRatio.baseVal.align
        rejects "animated viewBox is read-only" "NoModificationAllowedError" (fun () -> root.viewBox.animVal.width <- 1.f)
        near "read-only rejection preserves base" 600. (float root.viewBox.baseVal.width)
        root.viewBox.baseVal.width <- 300.f
        root.preserveAspectRatio.baseVal.align <- 6us
        matrix "restored viewBox" (1., 0., 0., 1., 0., 0.) (root.getCTM ())

        let symbol = create "symbol" [] :?> SVGSymbolElement
        let view = create "view" [] :?> SVGViewElement
        let marker = create "marker" [] :?> SVGMarkerElement
        let pattern = create "pattern" [] :?> SVGPatternElement

        let owners =
            [
                "symbol", symbol :> Element, (fun () -> symbol.viewBox), (fun () -> symbol.preserveAspectRatio)
                "view", view :> Element, (fun () -> view.viewBox), (fun () -> view.preserveAspectRatio)
                "marker", marker :> Element, (fun () -> marker.viewBox), (fun () -> marker.preserveAspectRatio)
                "pattern", pattern :> Element, (fun () -> pattern.viewBox), (fun () -> pattern.preserveAspectRatio)
            ]

        for label, element, getViewBox, getAspect in owners do
            root.appendChild element |> ignore
            element.setAttribute ("viewBox", "1 2 30 40")
            let box = getViewBox ()
            equal (label + " viewBox identity") true (obj.ReferenceEquals (box, getViewBox ()))
            bounds (label + " viewBox values") (1., 2., 30., 40.) box.baseVal
            box.baseVal.width <- 50.f
            equal (label + " live viewBox attribute") "1 2 50 40" (element.getAttribute "viewBox")
            rejects (label + " animated viewBox read-only") "NoModificationAllowedError" (fun () -> box.animVal.width <- 1.f)
            near (label + " failed edit preserves base") 50. (float box.baseVal.width)
            let aspect = getAspect ()
            equal (label + " aspect identity") true (obj.ReferenceEquals (aspect, getAspect ()))
            aspect.baseVal.align <- 1us
            aspect.baseVal.meetOrSlice <- 2us
            equal (label + " aspect align") 1us aspect.baseVal.align
            equal (label + " aspect slice") 2us aspect.baseVal.meetOrSlice
            rejects (label + " animated aspect read-only") "NoModificationAllowedError" (fun () -> aspect.animVal.align <- 2us)

        let nested =
            create
                "svg"
                [
                    "x", "50"
                    "y", "20"
                    "width", "100"
                    "height", "100"
                    "viewBox", "0 0 50 50"
                ]
            :?> SVGSVGElement

        let nestedRectangle =
            create "rect" [ "x", "2"; "y", "3"; "width", "8"; "height", "9" ] :?> SVGGeometryElement

        root.appendChild nested |> ignore
        nested.appendChild nestedRectangle |> ignore
        bounds "nested local bounds" (2., 3., 8., 9.) (nestedRectangle.getBBox ())
        let nestedScreen = nestedRectangle.getScreenCTM ()
        matrix "nested viewport screen CTM" (2., 0., 0., 2., viewport.x + 50., viewport.y + 20.) nestedScreen
        equal "nested owner root" true (obj.ReferenceEquals (nestedRectangle.ownerSVGElement, nested))

        let line = create "path" [ "d", "M0 0L100 0" ] :?> SVGGeometryElement
        let curve = create "path" [ "d", "M0 0Q50 100 100 0" ] :?> SVGGeometryElement
        root.appendChild line |> ignore
        root.appendChild curve |> ignore
        near "line length" 100. (float (line.getTotalLength ()))
        point "line midpoint" 50. 0. (line.getPointAtLength 50.f)
        point "negative distance clamps" 0. 0. (line.getPointAtLength -10.f)
        point "past end clamps" 100. 0. (line.getPointAtLength 200.f)
        let length = curve.getTotalLength ()
        equal "curved length range" true (length > 140.f && length < 150.f)
        point "curve midpoint" 50. 50. (curve.getPointAtLength (length / 2.f))

        for invalid in [ Single.NaN; Single.PositiveInfinity; Single.NegativeInfinity ] do
            rejects "restricted path distance" "TypeError" (fun () -> line.getPointAtLength invalid |> ignore)

        equal "inside fill uses local coordinates" true (rectangle.isPointInFill (DOMPointInit (x = 20., y = 20.)))
        equal "outside fill" false (rectangle.isPointInFill (DOMPointInit (x = -10., y = -10.)))
        equal "stroke edge" true (rectangle.isPointInStroke (DOMPointInit (x = 4., y = 10.)))
        equal "fill center is outside stroke" false (rectangle.isPointInStroke (DOMPointInit (x = 20., y = 20.)))
        line.pathLength.baseVal <- 200.f
        near "pathLength reflection" 200. (float line.pathLength.baseVal)
        near "pathLength leaves geometric length" 100. (float (line.getTotalLength ()))
        checks
    finally
        document.body.removeChild root |> ignore
