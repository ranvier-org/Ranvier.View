module Ranvier.View.WebCore.Tests.AtspiWindowChecks

open System
open System.Threading
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open RanvierWebCoreAbi

let run () =
    if not (OperatingSystem.IsLinux ()) then
        invalidOp "The AT-SPI ownership control requires Linux"

    let mutable checks = 0

    let check condition label =
        if not condition then
            failwith label

        checks <- checks + 1

    for mode in [ "window"; "host"; "fault"; "wrong-thread" ] do
        use window =
            Window.Create ("Ranvier AT-SPI ownership", 240, 160, hidden = true, renderer = "software")

        let host = window.Host
        let mutable forwarded = 0
        let mutable releasedBeforeDestroy = false

        host.SetNativeDestroy (fun handle ->
            releasedBeforeDestroy <- not window.AtspiLeaseActive
            forwarded <- forwarded + 1
            Native.rv_host_destroy handle)

        check window.AccessibilityAttached (mode + ": optional provider attached")

        if mode = "wrong-thread" then
            let mutable failure: exn = null

            let worker =
                Thread (
                    ThreadStart (fun () ->
                        try
                            (window :> IDisposable).Dispose()
                        with error ->
                            failure <- error)
                )

            worker.Start ()
            check (worker.Join 3000) "wrong-thread disposal completes"
            check (failure :? InvalidOperationException) "wrong-thread disposal rejects before ownership changes"
            check (window.AccessibilityAttached && forwarded = 0) "wrong-thread rejection retains live native ownership"

        let expected = InvalidOperationException "AT-SPI installation cleanup fault"

        if mode = "fault" then
            host.AddCleanupInstallation
                { new IDisposable with
                    member _.Dispose() =
                        raise expected
                }

        let mutable observed: exn = null

        try
            if mode = "host" || mode = "fault" then
                (host :> IDisposable).Dispose()
            else
                (window :> IDisposable).Dispose()
        with error ->
            observed <- error

        if mode = "fault" then
            check (obj.ReferenceEquals (observed, expected)) "the original installation exception is retained"
        else
            check (isNull observed) (mode + ": disposal succeeds")

        check
            (forwarded = 1)
            (mode
             + ": destruction forwards to the actual native host exactly once")

        check
            releasedBeforeDestroy
            (mode
             + ": provider lease ended before native destruction")

        check
            (not window.AtspiLeaseActive
             && not window.AccessibilityAttached)
            (mode + ": closed provider is unavailable")

        (window :> IDisposable).Dispose()

        check
            (forwarded = 1)
            (mode
             + ": repeated Window cleanup does not destroy the host again")

        Native.rv_run_loop_cycle 0 |> ignore

    checks

[<Tests>]
let tests =
    testList
        "platform accessibility ownership"
        [
            testCase "optional AT-SPI owns the actual Window and Host cleanup" (fun () ->
                if
                    Environment.GetEnvironmentVariable "RANVIER_ATSPI_OWNED_BUS_TEST"
                    <> "1"
                then
                    skiptest "Runs on an isolated owned accessibility bus with its optional provider"

                Support.onHostThread run |> ignore)

            testCase "UIA Window cleanup after direct Host disposal does not use its native handle" (fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "Windows UI Automation lifetime control"

                Support.onHostThread (fun () ->
                    use window =
                        Window.Create ("UIA disposal", 80, 60, hidden = true, renderer = "software")

                    let mutable destroyed = 0

                    window.Host.SetNativeDestroy (fun handle ->
                        destroyed <- destroyed + 1
                        Native.rv_host_destroy handle)

                    Expect.isTrue window.AccessibilityAttached "the actual UIA bridge is attached"
                    (window.Host :> IDisposable).Dispose()
                    Expect.equal destroyed 1 "direct disposal forwarded to the actual native host"
                    (window :> IDisposable).Dispose()
                    (window :> IDisposable).Dispose()
                    Expect.equal destroyed 1 "Window cleanup does not destroy the native host again")
                |> ignore)

            testCase "UIA attach failure leaves a host safe to dispose and detach repeatedly" (fun () ->
                if not (OperatingSystem.IsWindows ()) then
                    skiptest "Windows UI Automation attachment failure control"

                Support.onHostThread (fun () ->
                    use host = WebCoreHost.Create (80, 60)
                    let mutable destroyed = 0

                    host.SetNativeDestroy (fun handle ->
                        destroyed <- destroyed + 1
                        Native.rv_host_destroy handle)

                    Expect.throwsT<InvalidOperationException>
                        (fun () ->
                            WindowAccessibility.attach host Unchecked.defaultof<_>
                            |> ignore)
                        "a missing SDL window rejects attachment"

                    WindowAccessibility.detach host
                    (host :> IDisposable).Dispose()
                    WindowAccessibility.detach host
                    WindowAccessibility.detach host
                    Expect.equal destroyed 1 "failed attachment and repeated detachment do not duplicate native destruction")
                |> ignore)
        ]
