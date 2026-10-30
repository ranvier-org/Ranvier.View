module internal NativeApiConformance.MatrixLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks native matrix context anchoring, self-return identity, thread boundaries and retained value lifetime.</summary>
let run () =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith (sprintf "%s: expected %A, got %A" label expected actual)

    let failure action =
        try
            action ()
            "accepted"
        with error ->
            error.GetType().Name

    let host = WebCoreHost.Create (32, 32)
    let session = host.Session
    use service = Geometry.forHost host

    try
        let value = service.CreateMatrix ("translate(2px,3px)")
        let readonly = service.CreateReadOnlyMatrix ()

        let mutable createError, readError, writeError, contextError =
            "not-run", "not-run", "not-run", "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    createError <- failure (fun () -> service.CreateMatrix () |> ignore)
                    readError <- failure (fun () -> readonly.m11 |> ignore)
                    writeError <- failure (fun () -> value.a <- 9.)
                    contextError <- failure (fun () -> value.setMatrixValue ("scale(8)") |> ignore))
            )

        worker.Start ()
        worker.Join ()
        equal "matrix constructor wrong thread" "InvalidOperationException" createError
        equal "matrix read wrong thread" "InvalidOperationException" readError
        equal "matrix write wrong thread" "InvalidOperationException" writeError
        equal "matrix context method wrong thread" "InvalidOperationException" contextError
        equal "thread rejection preserves all matrix state" "matrix(1, 0, 0, 1, 2, 3)" (value.toString ())
        let secondary = host.Document.implementation.createHTMLDocument "matrix"
        use provider = Geometry.forDocument secondary
        let fromSecondary = provider.CreateMatrix ("translate(4px,5px)")
        let returned = fromSecondary.translate (2., 3.)
        provider.Dispose ()
        equal "secondary factory" 4. fromSecondary.e
        equal "secondary derived matrix" 6. returned.e
        equal "secondary context method identity" true (obj.ReferenceEquals (returned, returned.setMatrixValue ("translate(7px,8px)")))
        equal "secondary context method state" 8. returned.f
        let nested = returned.inverse().translate(1.)
        equal "nested matrix context remains bound" true (obj.ReferenceEquals (nested, nested.setMatrixValue ("scale(2)")))
        equal "nested context result" 2. nested.a
        let point = nested.transformPoint (DOMPointInit (x = 3.))
        equal "point result from matrix" 6. point.x
        service.Dispose ()
        equal "borrowed service leaves matrix" 2. value.e
        equal "borrowed service leaves readonly matrix" 1. readonly.m11
        let borrowed = Geometry.forHost host
        (host :> IDisposable).Dispose()
        equal "matrix after host disposal" "ObjectDisposedException" (failure (fun () -> value.a |> ignore))
        equal "readonly matrix after host disposal" "ObjectDisposedException" (failure (fun () -> readonly.m11 |> ignore))
        equal "secondary context after host disposal" "ObjectDisposedException" (failure (fun () -> returned.setMatrixValue ("") |> ignore))
        equal "point result after host disposal" "ObjectDisposedException" (failure (fun () -> point.x |> ignore))
        equal "matrix provider after host disposal" "ObjectDisposedException" (failure (fun () -> borrowed.CreateMatrix () |> ignore))
        borrowed.Dispose ()
    finally
        (host :> IDisposable).Dispose()

    equal "matrix host teardown" 0 session.LiveObjects
    use secondHost = WebCoreHost.Create (16, 16)
    use secondProvider = Geometry.forHost secondHost
    let second = secondProvider.CreateMatrix ()
    secondHost.Session.Dispose ()
    equal "matrix session expiry" "ObjectDisposedException" (failure (fun () -> second.setMatrixValue ("") |> ignore))
    equal "matrix factory session expiry" "ObjectDisposedException" (failure (fun () -> secondProvider.CreateReadOnlyMatrix () |> ignore))
    equal "matrix session teardown" 0 secondHost.Session.LiveObjects
    checks
