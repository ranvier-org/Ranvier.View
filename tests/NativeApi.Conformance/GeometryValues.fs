module internal NativeApiConformance.GeometryValues

open System
open Ranvier.Dom

/// <summary>Checks actual geometry construction, mutable coordinates, bounds, copies and retained identities.</summary>
/// <param name="create">Creates a borrowed document provider.</param>
/// <param name="mutableKinds">Reports actual rectangle and point mutable subtype membership.</param>
/// <param name="errorName">Reads observable provider disposal failures.</param>
/// <param name="disposedError">The provider-specific observable disposal failure.</param>
let run (create: unit -> GeometryService) (mutableKinds: obj -> bool * bool) (errorName: exn -> string) disposedError =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith (sprintf "%s: expected %A, got %A" label expected actual)

    let number label expected actual =
        checks <- checks + 1

        if
            not (
                expected = actual
                || (Double.IsNaN expected && Double.IsNaN actual)
            )
        then
            failwith $"{label}: expected {expected}, got {actual}"

        if expected = 0. && 1. / expected <> 1. / actual then
            failwith $"{label}: zero sign changed"

    let point label (x, y, z, w) (value: DOMPointReadOnly) =
        number (label + " x") x value.x
        number (label + " y") y value.y
        number (label + " z") z value.z
        number (label + " w") w value.w

    let rect label (x, y, width, height) (value: DOMRectReadOnly) =
        number (label + " x") x value.x
        number (label + " y") y value.y
        number (label + " width") width value.width
        number (label + " height") height value.height

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    use service = create ()
    let emptyPoint = service.CreatePoint ()
    point "point defaults" (0., 0., 0., 1.) emptyPoint
    let readonlyPoint = service.CreateReadOnlyPoint (x = 2., y = 3., z = 4., w = 5.)
    point "readonly constructor" (2., 3., 4., 5.) readonlyPoint
    equal "readonly point is not mutable" (false, false) (mutableKinds (box readonlyPoint))
    equal "mutable point subtype" (false, true) (mutableKinds (box emptyPoint))

    let explicitPoint =
        service.CreatePoint (x = -0., y = Double.PositiveInfinity, z = Double.NaN, w = 0.)

    point "unrestricted constructor" (-0., Double.PositiveInfinity, Double.NaN, 0.) explicitPoint
    let fractional = 1.0000000000000002
    explicitPoint.x <- fractional
    explicitPoint.y <- Double.NegativeInfinity
    explicitPoint.z <- -0.
    explicitPoint.w <- Double.NaN
    point "all point setters" (fractional, Double.NegativeInfinity, -0., Double.NaN) explicitPoint

    let partial =
        { DOMPointInit.Empty with
            X = Some 6.
            W = Some 0.
        }

    point "partial point dictionary" (6., 0., 0., 0.) (service.PointFrom partial)
    point "readonly point dictionary" (6., 0., 0., 0.) (service.ReadOnlyPointFrom partial)
    point "empty point dictionary" (0., 0., 0., 1.) (service.PointFrom DOMPointInit.Empty)
    point "null point dictionary" (0., 0., 0., 1.) (service.PointFrom Unchecked.defaultof<_>)
    point "readonly null point dictionary" (0., 0., 0., 1.) (service.ReadOnlyPointFrom Unchecked.defaultof<_>)

    point
        "unrestricted point dictionary"
        (Double.NaN, -0., Double.PositiveInfinity, Double.NegativeInfinity)
        (service.PointFrom
            {
                X = Some Double.NaN
                Y = Some -0.
                Z = Some Double.PositiveInfinity
                W = Some Double.NegativeInfinity
            })

    let emptyRect = service.CreateRect ()
    rect "rectangle defaults" (0., 0., 0., 0.) emptyRect
    equal "mutable rectangle subtype" (true, false) (mutableKinds (box emptyRect))

    let readonlyRect =
        service.CreateReadOnlyRect (x = 9., y = 8., width = -7., height = -6.)

    rect "readonly rectangle constructor" (9., 8., -7., -6.) readonlyRect
    equal "readonly rectangle is not mutable" (false, false) (mutableKinds (box readonlyRect))
    number "negative width left" 2. readonlyRect.left
    number "negative width right" 9. readonlyRect.right
    number "negative height top" 2. readonlyRect.top
    number "negative height bottom" 8. readonlyRect.bottom
    emptyRect.x <- fractional
    emptyRect.y <- -0.
    emptyRect.width <- Double.PositiveInfinity
    emptyRect.height <- Double.NaN
    rect "all rectangle setters" (fractional, -0., Double.PositiveInfinity, Double.NaN) emptyRect

    rect
        "partial rectangle dictionary"
        (0., 2., -3., 0.)
        (service.RectFrom
            { DOMRectInit.Empty with
                Y = Some 2.
                Width = Some -3.
            })

    rect
        "readonly rectangle dictionary"
        (0., 2., -3., 0.)
        (service.ReadOnlyRectFrom
            { DOMRectInit.Empty with
                Y = Some 2.
                Width = Some -3.
            })

    rect "null rectangle dictionary" (0., 0., 0., 0.) (service.RectFrom Unchecked.defaultof<_>)
    rect "readonly null rectangle dictionary" (0., 0., 0., 0.) (service.ReadOnlyRectFrom Unchecked.defaultof<_>)

    rect
        "unrestricted rectangle dictionary"
        (-0., Double.NaN, Double.NegativeInfinity, Double.PositiveInfinity)
        (service.RectFrom
            {
                X = Some -0.
                Y = Some Double.NaN
                Width = Some Double.NegativeInfinity
                Height = Some Double.PositiveInfinity
            })

    let quad = service.CreateQuad ()

    for index, p in [ 1, quad.p1; 2, quad.p2; 3, quad.p3; 4, quad.p4 ] do
        point $"quad default corner {index}" (0., 0., 0., 1.) p

    same "quad p1 identity" quad.p1 quad.p1
    same "quad p2 identity" quad.p2 quad.p2
    same "quad p3 identity" quad.p3 quad.p3
    same "quad p4 identity" quad.p4 quad.p4
    equal "quad corners are distinct" false (obj.ReferenceEquals (quad.p1, quad.p2))

    let signed =
        service.QuadFromRect
            {
                X = Some 5.
                Y = Some 7.
                Width = Some -3.
                Height = Some -4.
            }

    point "signed quad p1" (5., 7., 0., 1.) signed.p1
    point "signed quad p2" (2., 7., 0., 1.) signed.p2
    point "signed quad p3" (2., 3., 0., 1.) signed.p3
    point "signed quad p4" (5., 3., 0., 1.) signed.p4
    let bounds = signed.getBounds ()
    rect "quad bounds" (2., 3., 3., 4.) bounds
    signed.p1.x <- 10.
    rect "bounds copy stays independent" (2., 3., 3., 4.) bounds
    rect "fresh bounds observe live point" (2., 3., 8., 4.) (signed.getBounds ())
    bounds.width <- 99.
    number "bounds mutation leaves corner" 2. signed.p2.x

    let nested =
        { DOMQuadInit.Empty with
            P2 = Some partial
            P4 = Some Unchecked.defaultof<_>
        }

    let nestedQuad = service.QuadFrom nested
    point "nested omitted p1" (0., 0., 0., 1.) nestedQuad.p1
    point "nested present p2" (6., 0., 0., 0.) nestedQuad.p2
    point "nested omitted p3" (0., 0., 0., 1.) nestedQuad.p3
    point "nested null p4" (0., 0., 0., 1.) nestedQuad.p4
    let copy = service.QuadFrom nested
    nestedQuad.p2.x <- 29.
    number "quad dictionary construction copies" 6. copy.p2.x

    let constructorQuad =
        service.CreateQuad (p1 = partial, p3 = { DOMPointInit.Empty with Z = Some 8. }, p4 = Unchecked.defaultof<_>)

    point "quad constructor partial" (6., 0., 0., 0.) constructorQuad.p1
    point "quad constructor omitted" (0., 0., 0., 1.) constructorQuad.p2
    point "quad constructor z" (0., 0., 8., 1.) constructorQuad.p3
    point "quad constructor null" (0., 0., 0., 1.) constructorQuad.p4
    point "null quad dictionary" (0., 0., 0., 1.) (service.QuadFrom Unchecked.defaultof<_>).p1
    rect "null quad rectangle" (0., 0., 0., 0.) ((service.QuadFromRect Unchecked.defaultof<_>).getBounds())
    let retained = signed.p1
    service.Dispose ()
    retained.y <- 31.
    point "value survives provider disposal" (10., 31., 0., 1.) retained
    number "quad remains live after provider disposal" 31. signed.p1.y
    let mutable failure = "accepted"

    try
        service.CreatePoint () |> ignore
    with error ->
        failure <- errorName error

    equal "disposed provider rejected" disposedError failure
    service.Dispose ()
    checks
