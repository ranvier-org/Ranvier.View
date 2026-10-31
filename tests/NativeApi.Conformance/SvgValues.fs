module internal NativeApiConformance.SvgValues

open System
open Ranvier.Dom

/// <summary>Checks SVG factory values, units, live matrices, checked list edits and animated read-only values.</summary>
/// <remarks>Profile flags retain measured matrix-input and list-view identity differences; mutations share the same checks.</remarks>
let run (document: Document) (errorName: exn -> string) finiteDictionaryOnly stableListItemViews =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let near label expected actual =
        checks <- checks + 1

        if
            Double.IsNaN actual
            || abs (expected - actual) > 0.0001
        then
            failwithf "%s: expected %g, got %g" label expected actual

    let same label expected actual =
        equal label true (obj.ReferenceEquals (box expected, box actual))

    let rejects label expected action =
        let actual =
            try
                action ()
                "accepted"
            with error ->
                errorName error

        equal label expected actual

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

    let create tag attributes =
        let value = document.createElementNS ("http://www.w3.org/2000/svg", tag)

        for name, text in attributes do
            value.setAttribute (name, text)

        value

    let root = create "svg" [ "width", "300"; "height", "200" ] :?> SVGSVGElement
    document.body.appendChild root |> ignore

    try
        let number = root.createSVGNumber ()
        let length = root.createSVGLength ()
        let angle = root.createSVGAngle ()
        let point = root.createSVGPoint ()
        let rectangle = root.createSVGRect ()
        let valueMatrix = root.createSVGMatrix ()
        let transform = root.createSVGTransform ()
        near "number default" 0. (float number.value)
        equal "length default unit" 1us length.unitType
        near "length default" 0. (float length.value)
        equal "angle default unit" 1us angle.unitType
        near "angle default" 0. (float angle.value)
        near "point default x" 0. (float point.x)
        near "point default y" 0. (float point.y)
        near "rect default width" 0. (float rectangle.width)
        near "rect default height" 0. (float rectangle.height)
        matrix "matrix default" (1., 0., 0., 1., 0., 0.) valueMatrix
        matrix "transform default" (1., 0., 0., 1., 0., 0.) transform.matrix
        equal "transform default matrix type" 1us transform.``type``
        point.x <- 16777217.f
        near "point float precision" 16777216. (float point.x)
        rectangle.x <- 16777217.f
        near "rect float precision" 16777216. (float rectangle.x)
        valueMatrix.a <- 16777217.
        near "matrix double precision" 16777217. valueMatrix.a

        for invalid in [ Single.NaN; Single.PositiveInfinity; Single.NegativeInfinity ] do
            for label, setter in
                [
                    "number", (fun () -> number.value <- invalid)
                    "length", (fun () -> length.value <- invalid)
                    "angle", (fun () -> angle.value <- invalid)
                    "point", (fun () -> point.x <- invalid)
                    "rect", (fun () -> rectangle.width <- invalid)
                ] do
                rejects (label + " restricted setter") "TypeError" setter

            near "number rejects atomically" 0. (float number.value)
            near "length rejects atomically" 0. (float length.value)
            near "angle rejects atomically" 0. (float angle.value)
            near "point rejects atomically" 16777216. (float point.x)
            near "rect rejects atomically" 0. (float rectangle.width)

        for invalid in [ Double.NaN; Double.PositiveInfinity; Double.NegativeInfinity ] do
            rejects "matrix restricted setter" "TypeError" (fun () -> valueMatrix.a <- invalid)
            near "matrix rejects atomically" 16777217. valueMatrix.a

        length.newValueSpecifiedUnits (8us, 1.f)
        near "inch conversion" 96. (float length.value)
        length.convertToSpecifiedUnits 6us
        near "centimetre conversion" 2.54 (float length.valueInSpecifiedUnits)
        length.valueAsString <- "12px"
        equal "length string" "12px" length.valueAsString
        rejects "length invalid text" "SyntaxError" (fun () -> length.valueAsString <- "invalid")
        rejects "length invalid unit" "NotSupportedError" (fun () -> length.newValueSpecifiedUnits (99us, 1.f))
        near "length invalid edits preserve value" 12. (float length.value)
        angle.newValueSpecifiedUnits (3us, float32 Math.PI)
        near "radians become degrees" 180. (float angle.value)
        angle.convertToSpecifiedUnits 4us
        near "angle gradians" 200. (float angle.valueInSpecifiedUnits)
        angle.valueAsString <- "45deg"
        near "angle string" 45. (float angle.value)

        root.className.baseVal <- "svg-value-fixture"
        equal "animated string reflects attribute" "svg-value-fixture" (root.getAttribute "class")
        equal "animated string animVal" "svg-value-fixture" root.className.animVal
        same "animated string identity" root.className root.className
        let marker = create "marker" [] :?> SVGMarkerElement
        root.appendChild marker |> ignore
        marker.setOrientToAngle angle
        near "animated angle base" 45. (float marker.orientAngle.baseVal.value)
        near "animated angle current" 45. (float marker.orientAngle.animVal.value)
        same "animated angle wrapper identity" marker.orientAngle marker.orientAngle
        same "animated angle value identity" marker.orientAngle.baseVal marker.orientAngle.baseVal
        equal "animated enumeration angle state" 2us marker.orientType.baseVal
        marker.setOrientToAuto ()
        equal "animated enumeration auto state" 1us marker.orientType.baseVal
        equal "animated enumeration animVal" 1us marker.orientType.animVal
        same "animated enumeration identity" marker.orientType marker.orientType
        marker.markerWidth.baseVal.value <- 5.f
        near "animated length base" 5. (float marker.markerWidth.baseVal.value)
        near "animated length current" 5. (float marker.markerWidth.animVal.value)
        rejects "animated angle read-only" "NoModificationAllowedError" (fun () -> marker.orientAngle.animVal.value <- 7.f)
        rejects "animated length read-only" "NoModificationAllowedError" (fun () -> marker.markerWidth.animVal.value <- 7.f)

        let retainedMatrix = transform.matrix
        same "transform matrix identity" retainedMatrix transform.matrix
        transform.setTranslate (7.f, 9.f)
        matrix "live translation" (1., 0., 0., 1., 7., 9.) retainedMatrix
        transform.setScale (2.f, 3.f)
        matrix "retained matrix stays live" (2., 0., 0., 3., 0., 0.) retainedMatrix
        let translated = retainedMatrix.translate (4.f, 5.f)
        matrix "local matrix translation" (2., 0., 0., 3., 8., 15.) translated
        matrix "matrix inverse" (0.5, 0., 0., 1. / 3., -4., -5.) (translated.inverse ())
        point.x <- 1.f
        point.y <- 2.f
        let projected = point.matrixTransform translated
        near "projected point x" 10. (float projected.x)
        near "projected point y" 21. (float projected.y)
        near "projection preserves source" 1. (float point.x)
        matrix "matrix multiply" (1., 0., 0., 1., 0., 0.) (translated.multiply (translated.inverse ()))
        matrix "matrix flipX" (-2., 0., 0., 3., 8., 15.) (translated.flipX ())
        matrix "matrix flipY" (2., 0., 0., -3., 8., 15.) (translated.flipY ())

        let group = create "g" [] :?> SVGGraphicsElement
        let text = create "text" [] :?> SVGTextPositioningElement
        let polygon = create "polygon" [] :?> SVGPolygonElement
        let other = create "polyline" [] :?> SVGPolylineElement

        for element in [ group :> Node; text :> Node; polygon :> Node; other :> Node ] do
            root.appendChild element |> ignore

        let transforms = group.transform.baseVal
        let numbers = text.rotate.baseVal
        let lengths = text.x.baseVal
        let points = polygon.points
        same "animated transform wrapper identity" group.transform group.transform
        same "transform list identity" transforms group.transform.baseVal
        same "number list identity" numbers text.rotate.baseVal
        same "length list identity" lengths text.x.baseVal
        same "point list identity" points polygon.points

        let list label createItem readItem writeItem count length clear initialize get insert replace remove append set =
            let identity operation expected actual =
                equal (label + " " + operation + " wrapper identity") stableListItemViews (obj.ReferenceEquals (box expected, box actual))

            clear ()
            equal (label + " clear") 0u (count ())
            let first = createItem ()
            writeItem first 7.
            let initialized = initialize first
            identity "initialize" first initialized
            identity "getter" initialized (get 0u)
            near (label + " initialized value") 7. (readItem initialized)
            writeItem first 11.
            near (label + " input mutation reaches result") 11. (readItem initialized)
            writeItem (get 0u) 13.
            near (label + " getter mutation reaches input") 13. (readItem first)
            near (label + " getter mutation reaches result") 13. (readItem initialized)
            let inserted = createItem ()
            writeItem inserted 23.
            let insertedView = insert inserted 0u
            identity "insert" inserted insertedView
            near (label + " inserted value") 23. (readItem insertedView)
            equal (label + " insertion length") 2u (length ())
            let replacement = createItem ()
            writeItem replacement 31.
            let replacementView = replace replacement 1u
            identity "replace" replacement replacementView
            near (label + " replacement value") 31. (readItem replacementView)
            let removed = remove 0u
            identity "remove" insertedView removed
            near (label + " removed value") 23. (readItem removed)
            writeItem inserted 24.
            near (label + " removed view still aliases input") 24. (readItem removed)
            let appended = append removed
            identity "append" removed appended
            equal (label + " append length") 2u (count ())
            near (label + " appended value") 24. (readItem appended)
            writeItem removed 25.
            near (label + " reattached view retains alias") 25. (readItem appended)
            let beforeFirst, beforeSecond = get 0u, get 1u
            writeItem beforeFirst 101.
            writeItem beforeSecond 202.
            rejects (label + " missing get") "IndexSizeError" (fun () -> get 99u |> ignore)
            rejects (label + " invalid replace") "IndexSizeError" (fun () -> replace (createItem ()) 99u |> ignore)
            equal (label + " invalid edit preserves count") 2u (count ())
            identity "preserved first" beforeFirst (get 0u)
            identity "preserved second" beforeSecond (get 1u)
            near (label + " invalid edit preserves first value") 101. (readItem (get 0u))
            near (label + " invalid edit preserves second value") 202. (readItem (get 1u))
            near (label + " first retained view preserved") 101. (readItem beforeFirst)
            near (label + " second retained view preserved") 202. (readItem beforeSecond)
            writeItem (get 0u) 303.
            near (label + " invalid edit preserves first alias") 303. (readItem beforeFirst)
            let indexed = createItem ()
            writeItem indexed 401.
            set 0u indexed
            identity "indexed setter" indexed (get 0u)
            near (label + " indexed setter value") 401. (readItem (get 0u))
            writeItem indexed 402.
            near (label + " indexed input aliases stored value") 402. (readItem (get 0u))
            writeItem (get 0u) 403.
            near (label + " indexed getter aliases input") 403. (readItem indexed)
            equal (label + " length aliases count") (count ()) (length ())

        list
            "number"
            root.createSVGNumber
            (fun (v: SVGNumber) -> float v.value)
            (fun (v: SVGNumber) n -> v.value <- float32 n)
            (fun () -> numbers.numberOfItems)
            (fun () -> numbers.length)
            numbers.clear
            numbers.initialize
            numbers.getItem
            (fun v i -> numbers.insertItemBefore (v, i))
            (fun v i -> numbers.replaceItem (v, i))
            numbers.removeItem
            numbers.appendItem
            (fun i v -> numbers.setItem (i, v))

        list
            "length"
            root.createSVGLength
            (fun (v: SVGLength) -> float v.value)
            (fun (v: SVGLength) n -> v.value <- float32 n)
            (fun () -> lengths.numberOfItems)
            (fun () -> lengths.length)
            lengths.clear
            lengths.initialize
            lengths.getItem
            (fun v i -> lengths.insertItemBefore (v, i))
            (fun v i -> lengths.replaceItem (v, i))
            lengths.removeItem
            lengths.appendItem
            (fun i v -> lengths.setItem (i, v))

        list
            "point"
            root.createSVGPoint
            (fun (v: SVGPoint) -> float v.x)
            (fun (v: SVGPoint) n -> v.x <- float32 n)
            (fun () -> points.numberOfItems)
            (fun () -> points.length)
            points.clear
            points.initialize
            points.getItem
            (fun v i -> points.insertItemBefore (v, i))
            (fun v i -> points.replaceItem (v, i))
            points.removeItem
            points.appendItem
            (fun i v -> points.setItem (i, v))

        list
            "transform"
            root.createSVGTransform
            (fun (v: SVGTransform) -> v.matrix.e)
            (fun (v: SVGTransform) n -> v.setTranslate (float32 n, 0.f))
            (fun () -> transforms.numberOfItems)
            (fun () -> transforms.length)
            transforms.clear
            transforms.initialize
            transforms.getItem
            (fun v i -> transforms.insertItemBefore (v, i))
            (fun v i -> transforms.replaceItem (v, i))
            transforms.removeItem
            transforms.appendItem
            (fun i v -> transforms.setItem (i, v))

        let attached = points.getItem 0u
        let cloned = other.points.appendItem attached
        equal "cross-list insertion clones" false (obj.ReferenceEquals (attached, cloned))
        equal "cross-list source count" 2u points.length
        cloned.x <- 42.f
        equal "clone mutation independent" false (attached.x = 42.f)
        rejects "animated point list read-only" "NoModificationAllowedError" polygon.animatedPoints.clear
        rejects "animated point item read-only" "NoModificationAllowedError" (fun () -> (polygon.animatedPoints.getItem 0u).x <- 12.f)
        rejects "animated number list read-only" "NoModificationAllowedError" text.rotate.animVal.clear
        rejects "animated length list read-only" "NoModificationAllowedError" text.x.animVal.clear
        rejects "animated transform list read-only" "NoModificationAllowedError" group.transform.animVal.clear

        let strings = group.requiredExtensions
        strings.clear ()
        equal "string initialize" "urn:one" (strings.initialize "urn:one")
        equal "string insert" "urn:zero" (strings.insertItemBefore ("urn:zero", 0u))
        equal "string replace" "urn:two" (strings.replaceItem ("urn:two", 1u))
        equal "string remove" "urn:zero" (strings.removeItem 0u)
        equal "string append" "urn:three" (strings.appendItem "urn:three")
        equal "string order first" "urn:two" (strings.getItem 0u)
        equal "string order second" "urn:three" (strings.getItem 1u)
        rejects "string missing get" "IndexSizeError" (fun () -> strings.getItem 9u |> ignore)
        equal "string failed get preserves length" 2u strings.length

        let aliases =
            DOMMatrix2DInit (m11 = Some 2., m12 = Some 3., m21 = Some 4., m22 = Some 5., m41 = Some 6., m42 = Some 7.)

        matrix "root matrix dictionary aliases" (2., 3., 4., 5., 6., 7.) (root.createSVGTransformFromMatrix aliases).matrix
        matrix "list matrix dictionary aliases" (2., 3., 4., 5., 6., 7.) (transforms.createSVGTransformFromMatrix aliases).matrix
        transform.setMatrix aliases
        matrix "setter matrix dictionary aliases" (2., 3., 4., 5., 6., 7.) retainedMatrix
        matrix "root omitted matrix dictionary" (1., 0., 0., 1., 0., 0.) (root.createSVGTransformFromMatrix ()).matrix
        matrix "list omitted matrix dictionary" (1., 0., 0., 1., 0., 0.) (transforms.createSVGTransformFromMatrix ()).matrix
        transform.setMatrix ()
        matrix "setter omitted matrix dictionary" (1., 0., 0., 1., 0., 0.) retainedMatrix
        let nullDictionary = Unchecked.defaultof<DOMMatrix2DInit>
        matrix "root null dictionary" (1., 0., 0., 1., 0., 0.) (root.createSVGTransformFromMatrix nullDictionary).matrix
        let inconsistent = DOMMatrix2DInit (a = Some 2., m11 = Some 9.)
        transform.setTranslate (7.f, 9.f)

        for label, call in
            [
                "root",
                (fun () ->
                    root.createSVGTransformFromMatrix inconsistent
                    |> ignore)
                "list",
                (fun () ->
                    transforms.createSVGTransformFromMatrix inconsistent
                    |> ignore)
                "setter", (fun () -> transform.setMatrix inconsistent)
            ] do
            rejects (label + " inconsistent matrix aliases") "TypeError" call
            matrix (label + " rejected input preserves receiver") (1., 0., 0., 1., 7., 9.) retainedMatrix

        for invalid in [ Double.NaN; Double.PositiveInfinity; Double.NegativeInfinity ] do
            let dictionary = DOMMatrix2DInit (a = Some invalid, m11 = Some invalid)

            for label, call in
                [
                    "root", (fun () -> (root.createSVGTransformFromMatrix dictionary).matrix)
                    "list", (fun () -> (transforms.createSVGTransformFromMatrix dictionary).matrix)
                    "setter",
                    (fun () ->
                        transform.setMatrix dictionary
                        retainedMatrix)
                ] do
                if finiteDictionaryOnly then
                    rejects (label + " browser legacy finite-copy limit") "TypeError" (fun () -> call () |> ignore)
                else
                    let actual = (call ()).a

                    equal
                        (label + " unrestricted native dictionary")
                        true
                        (actual = invalid
                         || (Double.IsNaN actual && Double.IsNaN invalid))

        checks
    finally
        document.body.removeChild root |> ignore
