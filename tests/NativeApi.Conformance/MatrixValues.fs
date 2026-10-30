module internal NativeApiConformance.MatrixValues

open System
open Ranvier.Dom

/// <summary>Checks actual matrix construction, element aliases, engine transforms, copies and failure preservation.</summary>
/// <param name="create">Creates a borrowed geometry provider.</param>
/// <param name="mutableMatrix">Reports actual mutable matrix subtype membership.</param>
/// <param name="errorName">Reads actual engine exception names.</param>
/// <param name="nullArrayError">The provider-specific observable null numeric array error.</param>
let run (create: unit -> GeometryService) (mutableMatrix: DOMMatrixReadOnly -> bool) (errorName: exn -> string) nullArrayError =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith (sprintf "%s: expected %A, got %A" label expected actual)

    let near label expected actual =
        checks <- checks + 1

        if
            not (
                expected = actual
                || (Double.IsNaN expected && Double.IsNaN actual)
                || abs (expected - actual) < 1e-12
            )
        then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    let reject label expected action =
        let mutable outcome = "accepted"

        try
            action ()
        with
        | GeometryException (name, _) -> outcome <- name
        | error -> outcome <- errorName error

        equal label expected outcome

    let components (m: DOMMatrixReadOnly) =
        [|
            m.m11
            m.m12
            m.m13
            m.m14
            m.m21
            m.m22
            m.m23
            m.m24
            m.m31
            m.m32
            m.m33
            m.m34
            m.m41
            m.m42
            m.m43
            m.m44
        |]

    let identity = [| 1.; 0.; 0.; 0.; 0.; 1.; 0.; 0.; 0.; 0.; 1.; 0.; 0.; 0.; 0.; 1. |]

    let matrix label expected (actual: DOMMatrixReadOnly) =
        Array.iteri (fun i value -> near $"{label} [{i}]" value (components actual).[i]) expected

    let point label (x, y, z, w) (actual: DOMPointReadOnly) =
        near (label + " x") x actual.x
        near (label + " y") y actual.y
        near (label + " z") z actual.z
        near (label + " w") w actual.w

    use service = create ()
    let initial = service.CreateMatrix ()
    matrix "mutable omitted identity" identity initial
    equal "mutable identity flag" true initial.isIdentity
    equal "mutable omitted is2D" true initial.is2D
    equal "mutable matrix subtype" true (mutableMatrix initial)
    let readonly = service.CreateReadOnlyMatrix ()
    matrix "readonly omitted identity" identity readonly
    equal "readonly subtype" false (mutableMatrix readonly)

    for source in
        [
            service.CreateMatrix ("") :> DOMMatrixReadOnly
            service.CreateReadOnlyMatrix ("")
            service.MatrixFrom (DOMMatrixInit ()) :> DOMMatrixReadOnly
            service.ReadOnlyMatrixFrom (DOMMatrixInit ())
            service.MatrixFrom null :> DOMMatrixReadOnly
            service.ReadOnlyMatrixFrom null
        ] do
        matrix "empty/default initializer" identity source
        equal "default dictionary is2D" true source.is2D

    let values = [| 2.; 0.; 0.; 3.; 10.; 20. |]
    let affine = service.CreateMatrix values
    let readonlyAffine = service.CreateReadOnlyMatrix values
    values[0] <- 9.
    near "numeric array copied" 2. affine.a
    near "readonly numeric array copied" 2. readonlyAffine.a
    point "2D transform" (12., 23., 0., 1.) (affine.transformPoint (DOMPointInit (x = 1., y = 1.)))
    let literal = service.CreateMatrix ("matrix(1,2,3,4,5,6)")
    equal "six aliases from literal" [| 1.; 2.; 3.; 4.; 5.; 6. |] [| literal.a; literal.b; literal.c; literal.d; literal.e; literal.f |]
    matrix "serialization roundtrip" (components literal) (service.CreateReadOnlyMatrix (literal.toString ()))
    let parsed = service.CreateMatrix ("translate(10px,20px) scale(2,3)")
    point "transform list parse" (12., 23., 0., 1.) (parsed.transformPoint (DOMPointInit (x = 1., y = 1.)))
    let inverse = parsed.inverse ()
    let inversePoint = inverse.transformPoint (DOMPointInit (x = 12., y = 23.))

    for expected, actual in
        [
            1., inversePoint.x
            1., inversePoint.y
            0., inversePoint.z
            1., inversePoint.w
        ] do
        equal "inverse roundtrip within measured browser engine precision" true (abs (expected - actual) < 1e-6)

    for invalid in [ [||]; [| 1. |]; Array.create 7 1.; Array.create 15 1.; Array.create 17 1. ] do
        reject "wrong sequence length" "TypeError" (fun () -> service.CreateMatrix invalid |> ignore)
        reject "readonly wrong sequence length" "TypeError" (fun () -> service.CreateReadOnlyMatrix invalid |> ignore)

    for invalid in
        [
            "translate(10%)"
            "nonsense"
            "matrix(1,2)"
            "matrix(1,0,0,1,0,0)\000invalid"
            null
        ] do
        reject "invalid transform string" "SyntaxError" (fun () -> service.CreateMatrix invalid |> ignore)
        reject "readonly invalid transform string" "SyntaxError" (fun () -> service.CreateReadOnlyMatrix invalid |> ignore)

    reject "null numeric array" nullArrayError (fun () ->
        service.CreateMatrix (Unchecked.defaultof<float array>)
        |> ignore)

    reject "readonly null numeric array" nullArrayError (fun () ->
        service.CreateReadOnlyMatrix (Unchecked.defaultof<float array>)
        |> ignore)

    let all = service.CreateMatrix identity
    equal "sixteen elements retain 3D flag" false all.is2D

    let setters: (DOMMatrix -> float -> unit) array =
        [|
            (fun m v -> m.m11 <- v)
            (fun m v -> m.m12 <- v)
            (fun m v -> m.m13 <- v)
            (fun m v -> m.m14 <- v)
            (fun m v -> m.m21 <- v)
            (fun m v -> m.m22 <- v)
            (fun m v -> m.m23 <- v)
            (fun m v -> m.m24 <- v)
            (fun m v -> m.m31 <- v)
            (fun m v -> m.m32 <- v)
            (fun m v -> m.m33 <- v)
            (fun m v -> m.m34 <- v)
            (fun m v -> m.m41 <- v)
            (fun m v -> m.m42 <- v)
            (fun m v -> m.m43 <- v)
            (fun m v -> m.m44 <- v)
        |]

    for index, set in setters |> Array.indexed do
        set all (float index + 0.125)

    matrix "all sixteen element setters" (Array.init 16 (fun i -> float i + 0.125)) all
    equal "element aliases" [| all.m11; all.m12; all.m21; all.m22; all.m41; all.m42 |] [| all.a; all.b; all.c; all.d; all.e; all.f |]
    all.a <- 2.
    all.b <- 3.
    all.c <- 4.
    all.d <- 5.
    all.e <- 6.
    all.f <- 7.
    equal "all alias setters" [| 2.; 3.; 4.; 5.; 6.; 7. |] [| all.m11; all.m12; all.m21; all.m22; all.m41; all.m42 |]

    let precise =
        service.CreateMatrix ([| 16777217.125; 0.000000000000003125; 0.; 1.; 0.; 0. |])

    equal "double precision a" 16777217.125 precise.a
    equal "double precision b" 0.000000000000003125 precise.b
    let dictionary = DOMMatrixInit (a = Some 2., m11 = Some 2., e = Some 9.)
    let from = service.MatrixFrom dictionary
    let fromReadonly = service.ReadOnlyMatrixFrom dictionary
    dictionary.a <- Some 44.
    dictionary.m11 <- Some 44.
    equal "dictionary copied" [| 2.; 9.; 2.; 9. |] [| from.a; from.e; fromReadonly.a; fromReadonly.e |]
    equal "dictionary readonly subtype" false (mutableMatrix fromReadonly)
    equal "dictionary mutable subtype" true (mutableMatrix from)
    equal "explicit false is2D preserves 3D state" false (service.MatrixFrom (DOMMatrixInit (is2D = Some false))).is2D

    for invalid in
        [
            DOMMatrixInit (a = Some 1., m11 = Some 1.0000000000000002)
            DOMMatrixInit (is2D = Some true, m13 = 1.)
        ] do
        reject "conflicting dictionary" "TypeError" (fun () -> service.MatrixFrom invalid |> ignore)
        reject "readonly conflicting dictionary" "TypeError" (fun () -> service.ReadOnlyMatrixFrom invalid |> ignore)
        let before = components parsed
        let flag = parsed.is2D
        reject "invalid multiply atomic" "TypeError" (fun () -> parsed.multiplySelf invalid |> ignore)
        matrix "invalid multiply preserves all components" before parsed
        equal "invalid multiply preserves flag" flag parsed.is2D

    let nanAlias =
        service.MatrixFrom (DOMMatrixInit (a = Some Double.NaN, m11 = Some Double.NaN))

    equal "same value zero NaN aliases" true (Double.IsNaN nanAlias.a)
    near "same value zero signed aliases" 0. (service.MatrixFrom (DOMMatrixInit (a = Some -0., m11 = Some 0.))).a
    let unrestricted = service.CreateMatrix ()
    unrestricted.a <- Double.PositiveInfinity
    unrestricted.b <- Double.NegativeInfinity
    unrestricted.e <- Double.NaN
    unrestricted.f <- -0.
    near "positive infinity setter" Double.PositiveInfinity unrestricted.a
    near "negative infinity setter" Double.NegativeInfinity unrestricted.b
    equal "NaN setter" true (Double.IsNaN unrestricted.e)
    equal "negative zero setter" Double.NegativeInfinity (1. / unrestricted.f)
    let sticky = service.CreateMatrix ()
    sticky.m13 <- 2.
    sticky.m13 <- 0.
    equal "is2D stays false after restoring 3D element" false sticky.is2D
    let before = components all
    reject "invalid replacement" "SyntaxError" (fun () -> all.setMatrixValue ("translate(20%)") |> ignore)
    matrix "invalid replacement preserves all elements" before all
    equal "invalid replacement preserves 3D" false all.is2D
    same "valid replacement returns self" all (all.setMatrixValue ("translate(7px,9px)"))
    point "replacement resets components" (8., 10., 0., 1.) (all.transformPoint (DOMPointInit (x = 1., y = 1.)))
    equal "replacement resets is2D" true all.is2D
    let translation = service.CreateMatrix ([| 1.; 0.; 0.; 1.; 10.; 20. |])
    let scale = DOMMatrixInit (a = Some 2., d = Some 3.)
    same "multiplySelf identity" translation (translation.multiplySelf scale)
    point "ordered multiplySelf" (12., 23., 0., 1.) (translation.transformPoint (DOMPointInit (x = 1., y = 1.)))
    let pre = service.CreateMatrix ([| 1.; 0.; 0.; 1.; 10.; 20. |])
    same "preMultiplySelf identity" pre (pre.preMultiplySelf scale)
    point "ordered preMultiplySelf" (22., 63., 0., 1.) (pre.transformPoint (DOMPointInit (x = 1., y = 1.)))
    let immutable = service.CreateReadOnlyMatrix ([| 1.; 0.; 0.; 1.; 10.; 20. |])
    let copy = immutable.multiply scale
    equal "immutable multiply new identity" false (obj.ReferenceEquals (immutable, copy))
    point "immutable multiply values" (12., 23., 0., 1.) (copy.transformPoint (DOMPointInit (x = 1., y = 1.)))
    point "immutable multiply preserves original" (11., 21., 0., 1.) (immutable.transformPoint (DOMPointInit (x = 1., y = 1.)))
    let three = [| 2.; 0.; 0.; 0.5; 0.; 3.; 0.; 0.; 0.; 0.; 4.; 0.; 5.; 6.; 7.; 2. |]
    let threeMatrix = service.CreateMatrix three

    let parsed3d =
        service.CreateReadOnlyMatrix ("matrix3d(2,0,0,0.5,0,3,0,0,0,0,4,0,5,6,7,2)")

    matrix "literal matrix3d all elements" three parsed3d
    equal "literal matrix3d flag" false parsed3d.is2D
    let roundtrip3d = service.CreateMatrix (parsed3d.toString ())
    matrix "finite matrix3d serialization roundtrip" three roundtrip3d
    equal "finite matrix3d roundtrip flag" false roundtrip3d.is2D
    point "homogeneous no divide" (22., 30., 40., 8.5) (threeMatrix.transformPoint (DOMPointInit (x = 1., y = 2., z = 3., w = 4.)))
    point "default transform point" (5., 6., 7., 2.) (threeMatrix.transformPoint ())
    point "null transform point dictionary" (5., 6., 7., 2.) (threeMatrix.transformPoint (Unchecked.defaultof<_>))
    let sourcePoint = service.CreatePoint (x = 2., y = 3., z = 4.)

    point "point omitted matrix dictionary" (2., 3., 4., 1.) (sourcePoint.matrixTransform ())
    point "point null matrix dictionary" (2., 3., 4., 1.) (sourcePoint.matrixTransform (null))
    matrix "omitted multiply dictionary" identity (readonly.multiply ())
    matrix "null multiply dictionary" identity (readonly.multiply (null))
    let nullMultiply = service.CreateMatrix ()
    same "null preMultiplySelf returns self" nullMultiply (nullMultiply.preMultiplySelf (null))
    matrix "null preMultiplySelf defaults" identity nullMultiply
    point "point matrixTransform" (9., 16., 4., 1.) (sourcePoint.matrixTransform (DOMMatrixInit (a = Some 2., d = Some 3., e = Some 5., f = Some 7.)))
    point "point matrixTransform source unchanged" (2., 3., 4., 1.) sourcePoint

    let transforms
        : (string * (DOMMatrixReadOnly -> DOMMatrix) * (DOMMatrix -> DOMMatrix) * (float * float * float * float) * (float * float * float * float)) list =
        [
            "translate", (fun m -> m.translate (2., 3., 4.)), (fun m -> m.translateSelf (2., 3., 4.)), (1., 1., 1., 1.), (3., 4., 5., 1.)
            "scale omitted y", (fun m -> m.scale (2.)), (fun m -> m.scaleSelf (2.)), (1., 1., 1., 1.), (2., 2., 1., 1.)
            "scale present zero y", (fun m -> m.scale (2., 0.)), (fun m -> m.scaleSelf (2., 0.)), (1., 1., 1., 1.), (2., 0., 1., 1.)
            "scale3d", (fun m -> m.scale3d (2.)), (fun m -> m.scale3dSelf (2.)), (1., 1., 1., 1.), (2., 2., 2., 1.)
            "rotate omitted yz", (fun m -> m.rotate (90.)), (fun m -> m.rotateSelf (90.)), (1., 0., 0., 1.), (0., 1., 0., 1.)
            "rotate present zero yz", (fun m -> m.rotate (90., 0., 0.)), (fun m -> m.rotateSelf (90., 0., 0.)), (0., 1., 0., 1.), (0., 0., 1., 1.)
            "rotate vector", (fun m -> m.rotateFromVector (0., 1.)), (fun m -> m.rotateFromVectorSelf (0., 1.)), (1., 0., 0., 1.), (0., 1., 0., 1.)
            "rotate axis",
            (fun m -> m.rotateAxisAngle (0., 0., 1., 90.)),
            (fun m -> m.rotateAxisAngleSelf (0., 0., 1., 90.)),
            (1., 0., 0., 1.),
            (0., 1., 0., 1.)
            "skewX", (fun m -> m.skewX (45.)), (fun m -> m.skewXSelf (45.)), (1., 1., 0., 1.), (2., 1., 0., 1.)
            "skewY", (fun m -> m.skewY (45.)), (fun m -> m.skewYSelf (45.)), (1., 1., 0., 1.), (1., 2., 0., 1.)
        ]

    for label, transform, mutate, (x, y, z, w), expected in transforms do
        let original = service.CreateReadOnlyMatrix ()
        let changed = transform original
        equal (label + " new copy") false (obj.ReferenceEquals (original, changed))
        point (label + " pure result") expected (changed.transformPoint (DOMPointInit (x = x, y = y, z = z, w = w)))
        matrix (label + " original unchanged") identity original
        let original = service.CreateMatrix ()
        same (label + " returns self") original (mutate original)
        point (label + " self result") expected (original.transformPoint (DOMPointInit (x = x, y = y, z = z, w = w)))

    point "flipX" (-1., 1., 0., 1.) ((readonly.flipX ()).transformPoint(DOMPointInit (x = 1., y = 1.)))
    point "flipY" (1., -1., 0., 1.) ((readonly.flipY ()).transformPoint(DOMPointInit (x = 1., y = 1.)))
    point "nonuniform scale" (2., 3., 1., 1.) ((readonly.scaleNonUniform (2., 3.)).transformPoint(DOMPointInit (x = 1., y = 1., z = 1.)))
    let singular = service.CreateMatrix ([| 0.; 0.; 0.; 0.; 0.; 0. |])
    same "singular inverse self" singular (singular.invertSelf ())

    for number in components singular do
        equal "singular component NaN" true (Double.IsNaN number)

    equal "singular not2D" false singular.is2D
    reject "nonfinite serialization" "InvalidStateError" (fun () -> singular.toString () |> ignore)
    service.Dispose ()
    same "matrix value survives provider disposal" all (all.translateSelf (1.))
    near "matrix remains mutable after provider disposal" 8. all.e
    checks
