module Ranvier.View.WebCore.Tests.WindowCleanupTests

open Expecto

[<Tests>]
let tests =
    testCase "wrong-thread window disposal preserves a complete owner-thread retry" (fun () ->
        Support.onHostThread WindowCleanupChecks.runNative
        |> ignore)
