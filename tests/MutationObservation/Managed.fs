module Ranvier.View.WebCore.Tests.MutationManaged

open System

[<EntryPoint>]
let main arguments =
    try
        let checks = CleanupChecks.runPure ()
        printfn "Mutation cleanup: %d assertions PASS" checks

        if
            arguments.Length <> 0
            && arguments <> [| "--pure-only" |]
        then
            invalidArg "arguments" "This executable checks the shared page/session cleanup implementation"

        0
    with error ->
        eprintfn "%O" error
        1
