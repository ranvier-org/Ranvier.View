module Ranvier.View.WebCore.Tests.MutationManaged

open System
open System.Runtime.ExceptionServices
open System.Threading

let private native () =
    if OperatingSystem.IsMacOS () then
        ObservationChecks.runNative ()
    else
        let mutable result = 0
        let mutable failure: ExceptionDispatchInfo = null

        let thread =
            Thread (
                (fun () ->
                    try
                        result <- ObservationChecks.runNative ()
                    with error ->
                        failure <- ExceptionDispatchInfo.Capture error),
                64 * 1024 * 1024
            )

        thread.Start ()
        thread.Join ()

        if not (isNull failure) then
            failure.Throw ()

        result

[<EntryPoint>]
let main arguments =
    try
        let checks = CleanupChecks.runPure ()
        printfn "Mutation cleanup: %d assertions PASS" checks

        printfn "Mutation marshalling: %d assertions PASS" (ObservationChecks.runMarshalling ())

        if arguments <> [| "--pure-only" |] then
            printfn "Mutation native: %d assertions PASS" (native ())

        0
    with error ->
        eprintfn "%O" error
        1
