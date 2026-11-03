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
    testCase "optional AT-SPI owns the actual Window and Host cleanup" (fun () ->
        if
            Environment.GetEnvironmentVariable "RANVIER_ATSPI_OWNED_BUS_TEST"
            <> "1"
        then
            skiptest "Runs on an isolated owned accessibility bus with its optional provider"

        Support.onHostThread run |> ignore)
