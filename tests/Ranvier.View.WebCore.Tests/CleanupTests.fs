module Ranvier.View.WebCore.Tests.CleanupTests

open Expecto

[<Tests>]
let tests =
    testCase "page and session cleanup retain the first failure and finish every phase" (fun () -> CleanupChecks.runPure () |> ignore)
