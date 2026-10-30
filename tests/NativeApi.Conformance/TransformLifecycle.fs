module internal NativeApiConformance.TransformLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks typed transform session/thread boundaries, retained inputs and first-bound matrix identity.</summary>
let run () =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    let failure action =
        try
            action ()
            "accepted"
        with error ->
            error.GetType().Name

    equal
        "null host provider"
        "ArgumentNullException"
        (failure (fun () ->
            TransformValues.forHost Unchecked.defaultof<_>
            |> ignore))

    equal
        "null document provider"
        "ArgumentNullException"
        (failure (fun () ->
            TransformValues.forDocument Unchecked.defaultof<_>
            |> ignore))

    let host = WebCoreHost.Create (32, 32)
    let session = host.Session
    use service = TransformValues.forHost host
    use values = StyleValues.forHost host
    use geometry = Geometry.forHost host

    try
        let x = values.CreateUnit (2., "px") :> CSSNumericValue
        let y = values.CreateUnit (3., "px") :> CSSNumericValue
        let translate = service.CreateTranslate (x, y)
        let list = service.CreateList [| translate |]

        let mutable constructorError, readError, writeError, listError, matrixError, disposeError =
            "not-run", "not-run", "not-run", "not-run", "not-run", "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    constructorError <- failure (fun () -> service.CreateTranslate (x, y) |> ignore)
                    readError <- failure (fun () -> translate.x |> ignore)
                    writeError <- failure (fun () -> translate.x <- y)
                    listError <- failure (fun () -> list.setItem (0u, translate))
                    matrixError <- failure (fun () -> translate.toMatrix () |> ignore)
                    disposeError <- failure (fun () -> service.Dispose ()))
            )

        worker.Start ()
        worker.Join ()

        for label, outcome in
            [
                "constructor", constructorError
                "read", readError
                "write", writeError
                "list mutation", listError
                "matrix conversion", matrixError
                "provider disposal", disposeError
            ] do
            equal (label + " wrong thread") "InvalidOperationException" outcome

        same "wrong thread preserves x" x translate.x
        same "wrong thread preserves component" translate (list.item 0u)

        let secondary =
            host.Document.implementation.createHTMLDocument "transform-secondary"

        use secondService = TransformValues.forDocument secondary
        use secondGeometry = Geometry.forDocument secondary
        let secondaryMatrix = secondGeometry.CreateMatrix ("translate(4px,5px)")
        let matrixComponent = service.CreateMatrixComponent (geometry.CreateMatrix ())
        matrixComponent.matrix <- secondaryMatrix
        same "setter matrix identity across documents" secondaryMatrix matrixComponent.matrix
        let fromSecond = secondService.CreateMatrixComponent (secondaryMatrix, is2D = false)
        equal "constructor matrix remains independent across documents" false (obj.ReferenceEquals (secondaryMatrix, fromSecond.matrix))
        same "repeated getter preserves cross-document cache" secondaryMatrix matrixComponent.matrix
        same "cached matrix context method returns self" secondaryMatrix (matrixComponent.matrix.setMatrixValue ("translate(6px,7px)"))
        equal "cached matrix remains mutable" 7. secondaryMatrix.f
        let derived = fromSecond.toMatrix ()
        equal "secondary toMatrix independent" false (obj.ReferenceEquals (derived, fromSecond.matrix))
        same "derived matrix context method returns self" derived (derived.setMatrixValue ("scale(3)"))
        equal "derived matrix context result" 3. derived.a
        secondService.Dispose ()
        secondGeometry.Dispose ()
        same "cached matrix survives secondary provider disposal" secondaryMatrix matrixComponent.matrix
        same "matrix context survives provider disposal" secondaryMatrix (secondaryMatrix.setMatrixValue ("translate(8px,9px)"))
        equal "matrix context after secondary disposal" 9. secondaryMatrix.f
        use foreign = WebCoreHost.Create (16, 16)
        use foreignValues = StyleValues.forHost foreign
        use foreignTransforms = TransformValues.forHost foreign
        use foreignGeometry = Geometry.forHost foreign
        let foreignNumber = foreignValues.CreateUnit (4., "px") :> CSSNumericValue

        let foreignComponent =
            foreignTransforms.CreateTranslate (foreignNumber, foreignNumber)

        let foreignMatrix = foreignGeometry.CreateMatrix ()

        equal
            "foreign numeric constructor"
            "ArgumentException"
            (failure (fun () ->
                service.CreateTranslate (foreignNumber, y)
                |> ignore))

        equal "foreign numeric setter" "ArgumentException" (failure (fun () -> translate.x <- foreignNumber))

        equal
            "foreign component array"
            "ArgumentException"
            (failure (fun () ->
                service.CreateList [| translate; foreignComponent |]
                |> ignore))

        equal "foreign component setter" "ArgumentException" (failure (fun () -> list.setItem (0u, foreignComponent)))

        equal
            "foreign matrix constructor"
            "ArgumentException"
            (failure (fun () ->
                service.CreateMatrixComponent foreignMatrix
                |> ignore))

        equal "foreign matrix setter" "ArgumentException" (failure (fun () -> matrixComponent.matrix <- foreignMatrix))
        same "foreign rejection preserves x" x translate.x
        same "foreign rejection preserves component" translate (list.item 0u)
        same "foreign rejection preserves matrix" secondaryMatrix matrixComponent.matrix
        (foreign :> IDisposable).Dispose()

        let retained () =
            let localX = values.CreateUnit (22., "px") :> CSSNumericValue
            let localY = values.CreateUnit (33., "px") :> CSSNumericValue
            let local = service.CreateTranslate (localX, localY)
            service.CreateList [| local |]

        let retained = retained ()
        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        GC.Collect ()
        let retainedTranslate = retained.item 0u :?> CSSTranslate
        equal "list retains component after input GC" "22px" (retainedTranslate.x.toString ())
        equal "component retains numeric after input GC" "33px" (retainedTranslate.y.toString ())
        let converted = retained.toMatrix ()
        equal "retained list matrix x" 22. converted.e
        equal "retained list matrix y" 33. converted.f
        service.Dispose ()
        equal "disposed service factory" "ObjectDisposedException" (failure (fun () -> service.CreateTranslate (x, y) |> ignore))
        equal "disposed service Get" "ObjectDisposedException" (failure (fun () -> service.Get (list, 0u) |> ignore))
        same "disposed provider retains numeric identity" x translate.x
        equal "disposed provider leaves component conversion" 2. (translate.toMatrix ()).e
        let borrowed = TransformValues.forHost host
        (host :> IDisposable).Dispose()
        equal "component after host disposal" "ObjectDisposedException" (failure (fun () -> translate.x |> ignore))
        equal "list after host disposal" "ObjectDisposedException" (failure (fun () -> list.item 0u |> ignore))
        equal "derived matrix after host disposal" "ObjectDisposedException" (failure (fun () -> converted.a |> ignore))

        equal
            "cached secondary matrix after host disposal"
            "ObjectDisposedException"
            (failure (fun () -> secondaryMatrix.setMatrixValue ("") |> ignore))

        equal "provider after host disposal" "ObjectDisposedException" (failure (fun () -> borrowed.CreateList [| translate |] |> ignore))
        borrowed.Dispose ()
    finally
        (host :> IDisposable).Dispose()

    equal "transform host teardown" 0 session.LiveObjects
    use expired = WebCoreHost.Create (16, 16)
    use expiredValues = StyleValues.forHost expired
    use expiredService = TransformValues.forHost expired
    let unit = expiredValues.CreateUnit (1., "px") :> CSSNumericValue
    let expiredComponent = expiredService.CreateTranslate (unit, unit)
    expired.Session.Dispose ()
    equal "component after session expiry" "ObjectDisposedException" (failure (fun () -> expiredComponent.toMatrix () |> ignore))

    equal
        "factory after session expiry"
        "ObjectDisposedException"
        (failure (fun () ->
            expiredService.CreateList [| expiredComponent |]
            |> ignore))

    equal "expired session teardown" 0 expired.Session.LiveObjects
    checks
