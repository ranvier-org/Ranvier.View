module internal NativeApiConformance.GeometryLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks borrowed geometry ownership, secondary documents, thread guards and retained quad points.</summary>
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

    equal "null host" "ArgumentNullException" (failure (fun () -> Geometry.forHost Unchecked.defaultof<_> |> ignore))

    equal
        "null document"
        "ArgumentNullException"
        (failure (fun () ->
            Geometry.forDocument Unchecked.defaultof<_>
            |> ignore))

    let host = WebCoreHost.Create (32, 32)
    let session = host.Session
    let service = Geometry.forHost host

    try
        let point = service.CreatePoint (x = 7.)
        let rect = service.CreateRect (width = 8.)
        let quad = service.CreateQuad (p1 = DOMPointInit (x = 9.))
        let corner = quad.p1

        let mutable createError, readError, writeError, disposeError =
            "not-run", "not-run", "not-run", "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    createError <- failure (fun () -> service.CreateRect () |> ignore)
                    readError <- failure (fun () -> corner.x |> ignore)
                    writeError <- failure (fun () -> point.x <- 2.)
                    disposeError <- failure service.Dispose)
            )

        worker.Start ()
        worker.Join ()
        equal "constructor wrong thread" "InvalidOperationException" createError
        equal "point read wrong thread" "InvalidOperationException" readError
        equal "point write wrong thread" "InvalidOperationException" writeError
        equal "provider disposal wrong thread" "InvalidOperationException" disposeError
        equal "thread rejection preserves value" 7. point.x
        let secondary = host.Document.implementation.createHTMLDocument "geometry"
        use second = Geometry.forDocument secondary
        let secondaryPoint = second.CreatePoint (w = 0.)
        equal "secondary document constructor" 0. secondaryPoint.w
        second.Dispose ()
        equal "secondary value survives disposal" 0. secondaryPoint.w
        service.Dispose ()
        equal "provider leaves point" 7. point.x
        equal "provider leaves rectangle" 8. rect.width
        equal "provider leaves quad" 9. quad.p1.x
        equal "idempotent service disposal" "accepted" (failure service.Dispose)
        equal "disposed provider" "ObjectDisposedException" (failure (fun () -> service.CreateQuad () |> ignore))

        let retainedCorner, weakQuad =
            use temporaryService = Geometry.forHost host
            let temporaryQuad = temporaryService.CreateQuad ()
            temporaryQuad.p1, WeakReference (box temporaryQuad)

        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        retainedCorner.x <- 13.
        equal "point survives external quad reference removal" 13. retainedCorner.x
        equal "point anchors owning quad wrapper" true weakQuad.IsAlive
        let borrowed = Geometry.forHost host
        (host :> IDisposable).Dispose()
        equal "point after host disposal" "ObjectDisposedException" (failure (fun () -> point.x |> ignore))
        equal "rectangle after host disposal" "ObjectDisposedException" (failure (fun () -> rect.width |> ignore))
        equal "corner after host disposal" "ObjectDisposedException" (failure (fun () -> corner.x |> ignore))
        equal "secondary point after host disposal" "ObjectDisposedException" (failure (fun () -> secondaryPoint.w |> ignore))
        equal "borrowed service after host disposal" "ObjectDisposedException" (failure (fun () -> borrowed.CreatePoint () |> ignore))
        equal "factory after host disposal" "ObjectDisposedException" (failure (fun () -> Geometry.forHost host |> ignore))
        borrowed.Dispose ()
    finally
        service.Dispose ()
        (host :> IDisposable).Dispose()

    equal "host teardown" 0 session.LiveObjects
    use secondHost = WebCoreHost.Create (16, 16)
    use secondProvider = Geometry.forHost secondHost
    let value = secondProvider.CreatePoint ()
    secondHost.Session.Dispose ()
    equal "session disposal invalidates value" "ObjectDisposedException" (failure (fun () -> value.x |> ignore))
    equal "session disposal invalidates provider" "ObjectDisposedException" (failure (fun () -> secondProvider.CreateRect () |> ignore))
    equal "session teardown" 0 secondHost.Session.LiveObjects
    checks
