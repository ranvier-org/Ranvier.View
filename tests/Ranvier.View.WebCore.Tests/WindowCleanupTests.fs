module Ranvier.View.WebCore.Tests.WindowCleanupTests

open Expecto

[<Tests>]
let tests =
    testList
        "native cleanup ownership"
        [
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
