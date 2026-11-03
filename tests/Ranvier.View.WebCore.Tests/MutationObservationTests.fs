module Ranvier.View.WebCore.Tests.MutationObservationTests

open Expecto

[<Tests>]
let tests =
    testList
        "Mutation observation"
        [
            testCase "UTF-16 and option presence preserve the canonical ABI values" (fun () -> ObservationChecks.runMarshalling () |> ignore)
            testCase "production observers preserve engine records, foreign sessions and callback lifetime" (fun () ->
                Support.onHostThread ObservationChecks.runNative
                |> ignore)
        ]
