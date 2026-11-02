module Ranvier.View.WebCore.Tests.WindowCleanupTests

open Expecto
open System
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open RanvierWebCoreAbi

[<Tests>]
let tests =
    testList
        "native cleanup ownership"
        [
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
            testCase "wrong-thread window disposal preserves a complete owner-thread retry" (fun () ->
                Support.onHostThread WindowCleanupChecks.runNative
                |> ignore)
            testCase "host failures finish real native destruction exactly once" (fun () ->
                Support.onHostThread CleanupFaultChecks.runHost
                |> ignore)
            testCase "window phase failures finish real page cleanup and preserve SDL retry" (fun () ->
                Support.onHostThread CleanupFaultChecks.runWindow
                |> ignore)
            testCase "EGL ReleasePending failure retains real frame and owner retry" (fun () ->
                if
                    not (
                        System.OperatingSystem.IsLinux ()
                        && Ranvier.View.WebCore.WebCoreHost.Has "presentation-egl"
                    )
                then
                    skiptest "Linux EGL presentation is required"

                CleanupFaultChecks.runEglIsolated ())
        ]
