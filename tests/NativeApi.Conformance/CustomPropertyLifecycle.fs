module internal NativeApiConformance.CustomPropertyLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks native registration providers against host, wrapper and owning-thread disposal.</summary>
let run () =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let failure action =
        try
            action ()
            "accepted"
        with error ->
            error.GetType().Name

    equal
        "null registration host"
        "ArgumentNullException"
        (failure (fun () ->
            CustomProperties.forHost Unchecked.defaultof<WebCoreHost>
            |> ignore))

    let host = WebCoreHost.Create (32, 32)
    let session = host.Session
    let service = CustomProperties.forHost host

    try
        let definition = PropertyDefinition.Create ("--c12-thread", false)
        let mutable registration = "not-run"
        let mutable disposal = "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    registration <- failure (fun () -> service.Register definition)
                    disposal <- failure service.Dispose)
            )

        worker.Start ()
        worker.Join ()
        equal "registration owning thread" "InvalidOperationException" registration
        equal "disposal owning thread" "InvalidOperationException" disposal
        equal "wrong-thread failures preserve provider and registry" "accepted" (failure (fun () -> service.Register definition))
        service.Dispose ()
        equal "repeat disposal is idempotent" "accepted" (failure service.Dispose)
        let borrowed = CustomProperties.forHost host
        (host :> IDisposable).Dispose()
        equal "disposed host rejects registration" "ObjectDisposedException" (failure (fun () -> borrowed.Register definition))
        equal "disposed host rejects factory" "ObjectDisposedException" (failure (fun () -> CustomProperties.forHost host |> ignore))
        borrowed.Dispose ()
    finally
        service.Dispose ()
        (host :> IDisposable).Dispose()

    equal "registration host teardown" 0 session.LiveObjects

    use wrapperHost = WebCoreHost.Create (32, 32)
    let wrapperSession = wrapperHost.Session
    use borrowed = CustomProperties.forHost wrapperHost
    wrapperSession.Dispose ()

    equal
        "disposed borrowed document session"
        "ObjectDisposedException"
        (failure (fun () -> borrowed.Register (PropertyDefinition.Create ("--c12-disposed-document", false))))

    (wrapperHost :> IDisposable).Dispose()
    equal "document session teardown" 0 wrapperSession.LiveObjects
    checks
