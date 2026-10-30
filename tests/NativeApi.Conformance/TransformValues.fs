module internal NativeApiConformance.TransformValues

open System
open Ranvier.Dom

/// <summary>Checks actual Typed OM components, ordered matrix conversion, mutation and preserved failures.</summary>
/// <param name="create">Creates a borrowed typed transform provider.</param>
/// <param name="createValues">Creates the existing typed scalar provider.</param>
/// <param name="createGeometry">Creates the existing matrix provider.</param>
/// <param name="errorName">Reads actual observable engine exception names.</param>
let run
    (create: unit -> TransformValueService)
    (createValues: unit -> StyleValueService)
    (createGeometry: unit -> GeometryService)
    (errorName: exn -> string)
    =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let near label expected actual =
        checks <- checks + 1

        if
            not (
                expected = actual
                || abs (expected - actual) < 1e-12
            )
        then
            failwithf "%s: expected %.17g, got %.17g" label expected actual

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    let reject label expected action =
        let mutable outcome = "accepted"

        try
            action ()
        with
        | StyleValueException (name, _) -> outcome <- name
        | error -> outcome <- errorName error

        equal label expected outcome

    let rejected label action =
        let mutable failed = false

        try
            action ()
        with _ ->
            failed <- true

        equal label true failed

    let components (value: DOMMatrixReadOnly) =
        [|
            value.m11
            value.m12
            value.m13
            value.m14
            value.m21
            value.m22
            value.m23
            value.m24
            value.m31
            value.m32
            value.m33
            value.m34
            value.m41
            value.m42
            value.m43
            value.m44
        |]

    let identity = [| 1.; 0.; 0.; 0.; 0.; 1.; 0.; 0.; 0.; 0.; 1.; 0.; 0.; 0.; 0.; 1. |]

    let matrix label expected actual =
        Array.iteri (fun index value -> near $"{label}[{index}]" value (components actual).[index]) expected

    let affine a b c d e f =
        [| a; b; 0.; 0.; c; d; 0.; 0.; 0.; 0.; 1.; 0.; e; f; 0.; 1. |]

    use service = create ()
    use values = createValues ()
    use geometry = createGeometry ()

    let unitValue number unit =
        values.CreateUnit (number, unit) :> CSSNumericValue

    let px number =
        unitValue number "px"

    let degrees number =
        unitValue number "deg"

    let number value =
        DoubleOrCSSNumericValue.Double value

    let numeric value =
        DoubleOrCSSNumericValue.CSSNumericValue value

    reject "empty transform list" "TypeError" (fun () -> service.CreateList [||] |> ignore)

    let x, y, z = px 10., px 20., px 30.
    let translate = service.CreateTranslate (x, y)
    equal "translate omitted z is2D" true translate.is2D
    same "translate x retains numeric identity" x translate.x
    same "translate y retains numeric identity" y translate.y
    same "translate repeated z identity" translate.z translate.z
    matrix "translate2" (affine 1. 0. 0. 1. 10. 20.) (translate.toMatrix ())
    let translate3 = service.CreateTranslate (x, y, z)
    equal "translate explicit z is3D" false translate3.is2D
    same "translate explicit z identity" z translate3.z
    let expected3 = Array.copy identity
    expected3[12] <- 10.
    expected3[13] <- 20.
    expected3[14] <- 30.
    matrix "translate3" expected3 (translate3.toMatrix ())
    translate3.is2D <- true
    matrix "translate forced2D excludes stored z" (affine 1. 0. 0. 1. 10. 20.) (translate3.toMatrix ())
    same "forcing2D preserves stored z" z translate3.z
    translate3.is2D <- false
    translate3.x <- px 11.
    translate3.y <- px 22.
    translate3.z <- px 33.
    expected3[12] <- 11.
    expected3[13] <- 22.
    expected3[14] <- 33.
    matrix "translate setters" expected3 (translate3.toMatrix ())
    let angle = degrees 90.
    let rotate = service.CreateRotate angle
    equal "angle-only rotate is2D" true rotate.is2D
    same "rotation angle retained" angle rotate.angle
    matrix "rotate90" (affine 0. 1. -1. 0. 0. 0.) (rotate.toMatrix ())

    let axis =
        service.CreateRotate (number 0., numeric (unitValue 0. "number"), number 1., angle)

    equal "axis rotate is3D" false axis.is2D
    matrix "axis z rotation" (affine 0. 1. -1. 0. 0. 0.) (axis.toMatrix ())
    axis.x <- number 0.
    axis.y <- number 0.
    axis.z <- numeric (unitValue 1. "number")
    axis.angle <- degrees 180.
    matrix "axis setters" (affine -1. 0. 0. -1. 0. 0.) (axis.toMatrix ())
    let scale = service.CreateScale (number 2., numeric (unitValue 3. "number"))
    equal "scale omitted z is2D" true scale.is2D
    matrix "scale2" (affine 2. 0. 0. 3. 0. 0.) (scale.toMatrix ())
    let scale3 = service.CreateScale (number 2., number 3., number 4.)
    equal "scale explicit z is3D" false scale3.is2D
    let scaled3 = Array.copy identity
    scaled3[0] <- 2.
    scaled3[5] <- 3.
    scaled3[10] <- 4.
    matrix "scale3" scaled3 (scale3.toMatrix ())
    scale3.x <- number 5.
    scale3.y <- numeric (unitValue 6. "number")
    scale3.z <- number 7.
    scaled3[0] <- 5.
    scaled3[5] <- 6.
    scaled3[10] <- 7.
    matrix "scale setters" scaled3 (scale3.toMatrix ())
    let skew = service.CreateSkew (degrees 45., degrees 45.)
    matrix "skew xy" (affine 1. 1. 1. 1. 0. 0.) (skew.toMatrix ())
    skew.ax <- degrees 0.
    skew.ay <- degrees 0.
    matrix "skew setters" identity (skew.toMatrix ())
    let skewX = service.CreateSkewX (degrees 45.)
    let skewY = service.CreateSkewY (degrees 45.)
    matrix "skewX" (affine 1. 0. 1. 1. 0. 0.) (skewX.toMatrix ())
    matrix "skewY" (affine 1. 1. 0. 1. 0. 0.) (skewY.toMatrix ())
    skewX.ax <- degrees 0.
    skewY.ay <- degrees 0.
    matrix "skewX setter" identity (skewX.toMatrix ())
    matrix "skewY setter" identity (skewY.toMatrix ())

    for value in
        [
            skew :> CSSTransformComponent
            skewX :> CSSTransformComponent
            skewY :> CSSTransformComponent
        ] do
        value.is2D <- false
        equal "skew is2D setter no-op" true value.is2D

    let length = px 400.

    let perspective =
        service.CreatePerspective (CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSNumericValue length)

    equal "perspective is3D" false perspective.is2D
    let perspectiveMatrix = Array.copy identity
    perspectiveMatrix[11] <- -1. / 400.
    matrix "perspective" perspectiveMatrix (perspective.toMatrix ())
    perspective.length <- CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString "none"
    matrix "perspective none" identity (perspective.toMatrix ())
    equal "perspective none matrix is3D" false (perspective.toMatrix ()).is2D
    perspective.length <- CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSOMKeywordValue (values.CreateKeyword "none")
    matrix "perspective keyword none" identity (perspective.toMatrix ())
    perspective.is2D <- true
    equal "perspective is2D setter no-op" false perspective.is2D
    let original = geometry.CreateMatrix ([| 2.; 0.; 0.; 3.; 4.; 5. |])
    let matrixValue = service.CreateMatrixComponent original
    equal "matrix default is2D" true matrixValue.is2D
    equal "matrix constructor copies input" false (obj.ReferenceEquals (original, matrixValue.matrix))
    same "matrix getter same identity" matrixValue.matrix matrixValue.matrix
    original.e <- 99.
    near "constructor copy independence" 4. matrixValue.matrix.e
    let replacement = geometry.CreateMatrix ([| 6.; 0.; 0.; 7.; 8.; 9. |])
    matrixValue.matrix <- replacement
    same "matrix setter aliases input" replacement matrixValue.matrix
    replacement.e <- 10.
    near "matrix setter alias is live" 10. matrixValue.matrix.e
    let converted = matrixValue.toMatrix ()
    equal "toMatrix independent result" false (obj.ReferenceEquals (matrixValue.matrix, converted))
    converted.e <- 77.
    near "toMatrix edit leaves component" 10. matrixValue.matrix.e
    let component3 = service.CreateMatrixComponent (replacement, is2D = false)
    equal "matrix explicit false" false component3.is2D
    let converted3 = component3.toMatrix ()
    equal "3D toMatrix flag" false converted3.is2D
    equal "3D toMatrix independent result" false (obj.ReferenceEquals (component3.matrix, converted3))
    converted3.e <- 88.
    near "3D toMatrix edit leaves component" 10. component3.matrix.e
    let list = service.CreateList [| translate; scale; rotate |]
    equal "ordered list length" 3u list.length
    equal "ordered list is2D" true list.is2D
    same "list component identity" translate (list.item 0u)
    same "list repeated component identity" (list.item 1u) (list.item 1u)
    same "shared Get present" scale (service.Get(list, 1u).Value)
    equal "shared Get missing" true (service.Get(list, 3u).IsNone)
    equal "shared Get max missing" true (service.Get(list, UInt32.MaxValue).IsNone)
    matrix "ordered translate-scale-rotate" (affine 0. 3. -2. 0. 10. 20.) (list.toMatrix ())
    let prior = list.toString ()
    reject "list out-of-range set" "RangeError" (fun () -> list.setItem (4u, skew))
    reject "list maximum set" "RangeError" (fun () -> list.setItem (UInt32.MaxValue, skew))
    equal "failed index operations preserve serialization" prior (list.toString ())
    equal "failed index operations preserve length" 3u list.length
    list.setItem (3u, perspective)
    equal "append at length" 4u list.length
    same "appended component identity" perspective (list.item 3u)
    equal "3D member changes list flag" false list.is2D
    let list3 = list.toMatrix ()
    equal "3D list conversion retains flag" false list3.is2D
    matrix "3D list conversion preserves ordered coefficients" (affine 0. 3. -2. 0. 10. 20.) list3
    equal "list matrix conversion returns independent copies" false (obj.ReferenceEquals (list3, list.toMatrix ()))
    list.setItem (0u, scale)
    same "replacement identity" scale (list.item 0u)
    let invalid = degrees 1.

    for label, action, read in
        [
            "translate x", (fun () -> translate3.x <- invalid), (fun () -> translate3.toString ())
            "translate y", (fun () -> translate3.y <- invalid), (fun () -> translate3.toString ())
            "translate z", (fun () -> translate3.z <- invalid), (fun () -> translate3.toString ())
            "rotate angle", (fun () -> rotate.angle <- px 1.), (fun () -> rotate.toString ())
            "rotate x", (fun () -> axis.x <- numeric (px 1.)), (fun () -> axis.toString ())
            "rotate y", (fun () -> axis.y <- numeric (px 1.)), (fun () -> axis.toString ())
            "rotate z", (fun () -> axis.z <- numeric (px 1.)), (fun () -> axis.toString ())
            "scale x", (fun () -> scale.x <- numeric (px 1.)), (fun () -> scale.toString ())
            "scale y", (fun () -> scale.y <- numeric (px 1.)), (fun () -> scale.toString ())
            "scale z", (fun () -> scale.z <- numeric (px 1.)), (fun () -> scale.toString ())
            "skew ax", (fun () -> skew.ax <- px 1.), (fun () -> skew.toString ())
            "skew ay", (fun () -> skew.ay <- px 1.), (fun () -> skew.toString ())
            "skewX ax", (fun () -> skewX.ax <- px 1.), (fun () -> skewX.toString ())
            "skewY ay", (fun () -> skewY.ay <- px 1.), (fun () -> skewY.toString ())
            "perspective length",
            (fun () -> perspective.length <- CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSNumericValue invalid),
            (fun () -> perspective.toString ())
        ] do
        let before = read ()
        reject (label + " invalid dimension") "TypeError" action
        equal (label + " invalid preserves value") before (read ())

    for value in [ Double.NaN; Double.PositiveInfinity; Double.NegativeInfinity ] do
        reject "nonfinite constructor arm" "TypeError" (fun () ->
            service.CreateScale (number value, number 1.)
            |> ignore)

        let before = scale.toString ()
        reject "nonfinite setter arm" "TypeError" (fun () -> scale.x <- number value)
        equal "nonfinite setter preserves value" before (scale.toString ())

    let unresolved =
        service.CreateTranslate (unitValue 10. "percent", unitValue 2. "em")

    reject "relative matrix resolution" "TypeError" (fun () -> unresolved.toMatrix () |> ignore)

    rejected "null translate explicit z" (fun () ->
        service.CreateTranslate (x, y, Unchecked.defaultof<CSSNumericValue>)
        |> ignore)

    rejected "null scale explicit z container" (fun () ->
        service.CreateScale (number 1., number 2., Unchecked.defaultof<DoubleOrCSSNumericValue>)
        |> ignore)

    rejected "null scale explicit z reference" (fun () ->
        service.CreateScale (number 1., number 2., numeric Unchecked.defaultof<CSSNumericValue>)
        |> ignore)

    rejected "null component array" (fun () -> service.CreateList null |> ignore)

    rejected "null array component" (fun () ->
        service.CreateList [| Unchecked.defaultof<CSSTransformComponent> |]
        |> ignore)

    rejected "null numeric reference setter" (fun () -> scale.x <- numeric Unchecked.defaultof<CSSNumericValue>)
    rejected "null list component setter" (fun () -> list.setItem (0u, Unchecked.defaultof<CSSTransformComponent>))
    service.Dispose ()
    same "component numeric identity after provider disposal" x translate.x
    same "list identity after provider disposal" scale (list.item 0u)
    matrix "value survives provider disposal" (affine 1. 0. 0. 1. 10. 20.) (translate.toMatrix ())
    rejected "disposed provider" (fun () -> service.CreateTranslate (x, y) |> ignore)
    checks

/// <summary>Proves parsed and constructed transform values reach typed declarations and actual element geometry.</summary>
/// <param name="document">The live document containing the temporary element.</param>
/// <param name="transforms">Constructs the selected engine components.</param>
/// <param name="values">Parses actual typed transform values and creates lengths.</param>
/// <param name="maps">Acquires the element's actual inline typed map.</param>
/// <param name="checkpoint">Completes owning-thread style and layout work.</param>
/// <param name="transformType">Checks the provider's actual transform-list subtype.</param>
/// <param name="translateType">Checks the provider's actual translate-component subtype.</param>
let runEffects
    (document: Document)
    (transforms: TransformValueService)
    (values: StyleValueService)
    (maps: StyleMapService)
    (checkpoint: unit -> unit)
    (transformType: CSSStyleValue -> bool)
    (translateType: CSSTransformComponent -> bool)
    =
    let mutable checks = 0

    let near label expected actual =
        checks <- checks + 1

        if
            not (
                expected = actual
                || abs (expected - actual) < 1e-12
            )
        then
            failwithf "%s: expected %g, got %g" label expected actual

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let target = document.createElement "div" :?> HTMLElement
    target.style.cssText <- "position:absolute;left:0;top:0;width:10px;height:20px;transform-origin:0 0"
    document.body.appendChild target |> ignore

    try
        checkpoint ()
        let original = target.getBoundingClientRect ()
        let attribute = maps.Attribute (target :> ElementCSSInlineStyle)

        let px value =
            values.CreateUnit (value, "px") :> CSSNumericValue

        let translate = transforms.CreateTranslate (px 10., px 20.)

        let scale =
            transforms.CreateScale (DoubleOrCSSNumericValue.Double 2., DoubleOrCSSNumericValue.Double 3.)

        let list = transforms.CreateList [| translate; scale |]
        attribute.set ("transform", CSSStyleValueOrUSVString.CSSStyleValue list)
        checkpoint ()
        let actual = target.getBoundingClientRect ()
        near "constructed transform rendered left" (original.left + 10.) actual.left
        near "constructed transform rendered top" (original.top + 20.) actual.top
        near "constructed transform rendered width" 20. actual.width
        near "constructed transform rendered height" 60. actual.height
        let acquired = maps.Get(attribute, "transform").Value
        equal "typed map preserves transform subtype" true (transformType acquired)
        let acquired = acquired :?> CSSTransformValue
        equal "typed map transform length" 2u acquired.length
        near "typed map matrix scale x" 2. (acquired.toMatrix ()).a
        near "typed map matrix translation y" 20. (acquired.toMatrix ()).f
        let parsed = values.Parse ("transform", "translate(7px,11px) scale(2,4)")
        equal "parsed transform actual subtype" true (transformType parsed)
        let parsed = parsed :?> CSSTransformValue
        equal "parsed transform component subtype" true (translateType (parsed.item 0u))
        attribute.set ("transform", CSSStyleValueOrUSVString.CSSStyleValue parsed)
        checkpoint ()
        let actual = target.getBoundingClientRect ()
        near "parsed transform rendered left" (original.left + 7.) actual.left
        near "parsed transform rendered top" (original.top + 11.) actual.top
        near "parsed transform rendered width" 20. actual.width
        near "parsed transform rendered height" 80. actual.height
        let changed = parsed.item 0u :?> CSSTranslate
        changed.x <- px 13.
        attribute.set ("transform", CSSStyleValueOrUSVString.CSSStyleValue parsed)
        checkpoint ()
        let actual = target.getBoundingClientRect ()
        near "edited parsed component rendered left" (original.left + 13.) actual.left
        near "edited parsed component preserves height" 80. actual.height
        near "edited parsed component matrix" 13. (parsed.toMatrix ()).e
        attribute.delete "transform"
        checkpoint ()
        let restored = target.getBoundingClientRect ()
        near "removed transform restores left" original.left restored.left
        near "removed transform restores top" original.top restored.top
        near "removed transform restores width" original.width restored.width
        near "removed transform restores height" original.height restored.height
        checks
    finally
        target.remove ()
