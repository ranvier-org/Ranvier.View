module internal NativeApiConformance.SvgShapes

open System
open Ranvier.Dom

let private create<'T> (document: Document) name attributes : 'T =
    let element = document.createElementNS ("http://www.w3.org/2000/svg", name)

    for key, value in attributes do
        element.setAttribute (key, value)

    unbox<'T> element

/// <summary>Checks live shape attributes, authored path parsing and real Boolean/Integer value providers.</summary>
/// <param name="nullError">The provider's error name for a rejected null argument.</param>
/// <param name="presentNullDistinct">True when <c>Some null</c> remains distinct from <c>None</c> at runtime.</param>
let run (document: Document) (errorName: exn -> string) (nullError: string) (presentNullDistinct: bool) =
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
            || abs (expected - actual) > 0.0001
        then
            failwithf "%s: expected %g, got %g" label expected actual

    let failure label expected action =
        let outcome =
            try
                action ()
                "accepted"
            with error ->
                errorName error

        equal label expected outcome

    let root =
        create<SVGSVGElement> document "svg" [ "width", "500"; "height", "300"; "viewBox", "0 0 500 300" ]

    document.body.appendChild root |> ignore

    let append (node: Node) =
        root.appendChild node |> ignore

    let bounds label (x, y, width, height) (shape: SVGGeometryElement) =
        let value = shape.getBBox ()
        near (label + ".x") x (float value.x)
        near (label + ".y") y (float value.y)
        near (label + ".width") width (float value.width)
        near (label + ".height") height (float value.height)

    try
        let circle = create<SVGCircleElement> document "circle" []
        let ellipse = create<SVGEllipseElement> document "ellipse" []
        let line = create<SVGLineElement> document "line" []
        let rect = create<SVGRectElement> document "rect" []
        append circle
        append ellipse
        append line
        append rect
        circle.cx.baseVal.value <- 40.f
        circle.cy.baseVal.value <- 30.f
        circle.r.baseVal.value <- 10.f
        bounds "circle" (30., 20., 20., 20.) circle
        circle.r.baseVal.valueAsString <- "15"
        bounds "circle edited" (25., 15., 30., 30.) circle
        equal "circle authored radius" "15" (circle.getAttribute "r")
        ellipse.cx.baseVal.value <- 90.f
        ellipse.cy.baseVal.value <- 40.f
        ellipse.rx.baseVal.value <- 20.f
        ellipse.ry.baseVal.value <- 10.f
        bounds "ellipse" (70., 30., 40., 20.) ellipse
        ellipse.ry.baseVal.value <- 20.f
        bounds "ellipse edited" (70., 20., 40., 40.) ellipse
        line.x1.baseVal.value <- 10.f
        line.y1.baseVal.value <- 15.f
        line.x2.baseVal.value <- 90.f
        line.y2.baseVal.value <- 65.f
        bounds "line" (10., 15., 80., 50.) line
        rect.x.baseVal.value <- 10.f
        rect.y.baseVal.value <- 20.f
        rect.width.baseVal.value <- 80.f
        rect.height.baseVal.value <- 40.f
        rect.rx.baseVal.value <- 10.f
        rect.ry.baseVal.value <- 5.f
        bounds "rounded rectangle" (10., 20., 80., 40.) rect
        equal "rounded rectangle length changes" true (rect.getTotalLength () < 240.f)
        rect.rx.baseVal.value <- 0.f
        rect.ry.baseVal.value <- 0.f
        near "unrounded rectangle length" 240. (float (rect.getTotalLength ()))

        for label, get in
            [
                "circle.cx", (fun () -> circle.cx)
                "circle.cy", (fun () -> circle.cy)
                "circle.r", (fun () -> circle.r)
                "ellipse.cx", (fun () -> ellipse.cx)
                "ellipse.cy", (fun () -> ellipse.cy)
                "ellipse.rx", (fun () -> ellipse.rx)
                "ellipse.ry", (fun () -> ellipse.ry)
                "line.x1", (fun () -> line.x1)
                "line.y1", (fun () -> line.y1)
                "line.x2", (fun () -> line.x2)
                "line.y2", (fun () -> line.y2)
                "rect.x", (fun () -> rect.x)
                "rect.y", (fun () -> rect.y)
                "rect.width", (fun () -> rect.width)
                "rect.height", (fun () -> rect.height)
                "rect.rx", (fun () -> rect.rx)
                "rect.ry", (fun () -> rect.ry)
            ] do
            let value = get ()
            equal (label + " animated wrapper identity") true (obj.ReferenceEquals (value, get ()))
            equal (label + " base identity") true (obj.ReferenceEquals (value.baseVal, value.baseVal))
            near (label + " reflected animation value") (float value.baseVal.value) (float value.animVal.value)
            let before = value.baseVal.value
            failure (label + " animation value readonly") "NoModificationAllowedError" (fun () -> value.animVal.value <- 999.f)
            equal (label + " rejected edit is atomic") before value.baseVal.value

        let polygon =
            create<SVGPolygonElement> document "polygon" [ "points", "0,0 30,0 30,40" ]

        let polyline =
            create<SVGPolylineElement> document "polyline" [ "points", "0,0 20,0 20,20" ]

        append polygon
        append polyline
        bounds "polygon" (0., 0., 30., 40.) polygon
        near "polygon closes its path" 120. (float (polygon.getTotalLength ()))
        polygon.points.getItem(2u).y <- 60.f
        bounds "polygon live point edit" (0., 0., 30., 60.) polygon
        failure "polygon animated point readonly" "NoModificationAllowedError" (fun () -> polygon.animatedPoints.getItem(0u).x <- 99.f)
        bounds "polygon failed edit preserved bounds" (0., 0., 30., 60.) polygon
        near "polyline remains open" 40. (float (polyline.getTotalLength ()))
        let endpoint = root.createSVGPoint ()
        endpoint.x <- 40.f
        endpoint.y <- 20.f
        polyline.points.appendItem endpoint |> ignore
        equal "polyline count" 4u polyline.points.numberOfItems
        bounds "polyline appended point" (0., 0., 40., 20.) polyline
        near "polyline appended segment" 60. (float (polyline.getTotalLength ()))
        endpoint.x <- 50.f
        bounds "polyline inserted value remains live" (0., 0., 50., 20.) polyline
        failure "polyline animated point readonly" "NoModificationAllowedError" (fun () -> polyline.animatedPoints.getItem(0u).y <- 99.f)
        polyline.points.clear ()
        equal "cleared polyline count" 0u polyline.points.numberOfItems
        bounds "cleared polyline" (0., 0., 0., 0.) polyline

        let path = create<SVGPathElement> document "path" []
        append path
        equal "missing authored path" None (SvgPaths.getData path)
        SvgPaths.setData path (Some "")
        equal "present empty authored path" (Some "") (SvgPaths.getData path)
        bounds "empty authored path" (0., 0., 0., 0.) path
        SvgPaths.setData path (Some "M10 20 H110 V70 H10 Z")
        equal "authored syntax retained" (Some "M10 20 H110 V70 H10 Z") (SvgPaths.getData path)
        bounds "authored rectangle path" (10., 20., 100., 50.) path
        near "authored path length" 300. (float (path.getTotalLength ()))
        SvgPaths.setData path (Some "M10 20 H110 Lbad")
        equal "invalid suffix remains authored" (Some "M10 20 H110 Lbad") (SvgPaths.getData path)
        bounds "engine preserves valid prefix" (10., 20., 100., 0.) path
        near "engine valid-prefix length" 100. (float (path.getTotalLength ()))
        SvgPaths.setData path (Some "garbage")
        bounds "engine rejects invalid path geometry" (0., 0., 0., 0.) path
        near "invalid path length" 0. (float (path.getTotalLength ()))
        SvgPaths.setData path None
        equal "removed authored path" None (SvgPaths.getData path)
        failure "null path read" nullError (fun () -> SvgPaths.getData null |> ignore)
        failure "null path write" nullError (fun () -> SvgPaths.setData null None)
        failure "present null authored string" (if presentNullDistinct then nullError else "accepted") (fun () -> SvgPaths.setData path (Some null))
        equal "null rejection preserves missing value" None (SvgPaths.getData path)

        let defs = create<Element> document "defs" []
        let filter = create<Element> document "filter" []

        let convolve =
            create<SVGFEConvolveMatrixElement> document "feConvolveMatrix" [ "order", "3 5"; "targetX", "1"; "targetY", "2" ]

        append defs
        defs.appendChild filter |> ignore
        filter.appendChild convolve |> ignore
        let boolean = convolve.preserveAlpha
        equal "real Boolean wrapper identity" true (obj.ReferenceEquals (boolean, convolve.preserveAlpha))
        equal "Boolean default" false boolean.baseVal
        convolve.setAttribute ("preserveAlpha", "true")
        equal "Boolean follows authored edit" true boolean.baseVal
        equal "Boolean animation value" true boolean.animVal
        boolean.baseVal <- false
        equal "Boolean writes authored attribute" "false" (convolve.getAttribute "preserveAlpha")
        equal "Boolean animation follows base edit" false boolean.animVal

        for label, get, expected in
            [
                "orderX", (fun () -> convolve.orderX), 3
                "orderY", (fun () -> convolve.orderY), 5
                "targetX", (fun () -> convolve.targetX), 1
                "targetY", (fun () -> convolve.targetY), 2
            ] do
            let integer = get ()
            equal (label + " real Integer wrapper identity") true (obj.ReferenceEquals (integer, get ()))
            equal (label + " parsed base value") expected integer.baseVal
            equal (label + " parsed animation value") expected integer.animVal

        convolve.orderX.baseVal <- 7
        equal "paired orderX update" 7 convolve.orderX.baseVal
        equal "paired orderY preserved" 5 convolve.orderY.baseVal
        equal "paired order animation update" 7 convolve.orderX.animVal
        convolve.targetX.baseVal <- -7
        equal "signed Integer base value" -7 convolve.targetX.baseVal
        equal "signed Integer animation value" -7 convolve.targetX.animVal
        checks
    finally
        if not (isNull (box root.parentNode)) then
            root.parentNode.removeChild root |> ignore
