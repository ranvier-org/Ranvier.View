module internal NativeApiConformance.StyleValueLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks typed-value provider validation, host ownership and release boundaries.</summary>
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
        "null host"
        "ArgumentNullException"
        (failure (fun () ->
            StyleValues.forHost Unchecked.defaultof<_>
            |> ignore))

    let host = WebCoreHost.Create (32, 32)
    let session = host.Session
    let service = StyleValues.forHost host

    try
        let mutable parse, dispose = "not-run", "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    parse <- failure (fun () -> service.Parse ("width", "1px") |> ignore)
                    dispose <- failure service.Dispose)
            )

        worker.Start ()
        worker.Join ()
        equal "parser thread" "InvalidOperationException" parse
        equal "provider disposal thread" "InvalidOperationException" dispose
        equal "null segment array" "ArgumentNullException" (failure (fun () -> service.CreateUnparsed null |> ignore))

        equal
            "null union container"
            "ArgumentNullException"
            (failure (fun () ->
                service.CreateUnparsed [| Unchecked.defaultof<_> |]
                |> ignore))

        equal
            "null reference arm"
            "ArgumentNullException"
            (failure (fun () ->
                service.CreateUnparsed
                    [|
                        USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue Unchecked.defaultof<_>
                    |]
                |> ignore))

        let fallback =
            service.CreateUnparsed [| USVStringOrCSSOMVariableReferenceValue.USVString "red" |]

        let reference = service.CreateVariableReference ("--foreign", Some fallback)

        let foreignChecks () =
            use other = WebCoreHost.Create (32, 32)
            use foreign = StyleValues.forHost other

            equal
                "foreign fallback"
                "ArgumentException"
                (failure (fun () ->
                    foreign.CreateVariableReference ("--x", Some fallback)
                    |> ignore))

            equal
                "foreign reference"
                "ArgumentException"
                (failure (fun () ->
                    foreign.CreateUnparsed
                        [|
                            USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue reference
                        |]
                    |> ignore))

            let values =
                foreign.CreateUnparsed [| USVStringOrCSSOMVariableReferenceValue.USVString "own" |]

            equal "foreign setter reference" "ArgumentException" (failure (fun () -> values.setItem (0u, reference)))

            equal
                "failed foreign setter preserves segment"
                "own"
                (match values.item 0u with
                 | Some (USVStringOrCSSOMVariableReferenceValue.USVString value) -> value
                 | _ -> "wrong arm")

            equal
                "null setter reference"
                "ArgumentNullException"
                (failure (fun () -> values.setItem (0u, Unchecked.defaultof<CSSVariableReferenceValue>)))

        foreignChecks ()
        service.Dispose ()
        equal "idempotent provider disposal" "accepted" (failure service.Dispose)
        let borrowed = StyleValues.forHost host
        (host :> IDisposable).Dispose()
        equal "disposed host parse" "ObjectDisposedException" (failure (fun () -> borrowed.ParseAll ("width", "1px") |> ignore))
        equal "disposed host factory" "ObjectDisposedException" (failure (fun () -> StyleValues.forHost host |> ignore))
        equal "retained fallback invalid after host disposal" "ObjectDisposedException" (failure (fun () -> fallback.length |> ignore))
        equal "retained reference invalid after host disposal" "ObjectDisposedException" (failure (fun () -> reference.variable |> ignore))
        borrowed.Dispose ()
    finally
        service.Dispose ()
        (host :> IDisposable).Dispose()

    equal "host teardown" 0 session.LiveObjects
    use wrapperHost = WebCoreHost.Create (32, 32)
    use borrowed = StyleValues.forHost wrapperHost
    wrapperHost.Session.Dispose ()
    equal "disposed borrowed document session" "ObjectDisposedException" (failure (fun () -> borrowed.CreateUnit (1., "px") |> ignore))
    equal "document session teardown" 0 wrapperHost.Session.LiveObjects
    checks
