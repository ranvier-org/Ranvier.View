module Ranvier.View.WebCore.Hmr.Tests.ReloadTests

open Expecto

[<Tests>]
let tests =
    // One host at a time: each run compiles in FSI and has deadlines on what the window shows.
    testSequenced
    <| testList
        "Hot reload"
        [
            testCase "an edit reaches the declarations that depend on it, in its file and in another"
            <| fun () -> Host.expectAllPassed (Host.run "dependents")

            testCase "a new function and its use in a later file reload when saved together"
            <| fun () -> Host.expectAllPassed (Host.run "several-together")

            testCase "a new function reloads before its use in a later file"
            <| fun () -> Host.expectAllPassed (Host.run "several-earlier-first")

            testCase "a use saved before its function is an error that names it, and reloads once the function arrives"
            <| fun () -> Host.expectAllPassed (Host.run "several-later-first")

            testCase "an edit that did not compile and was put back does not block the next edit"
            <| fun () -> Host.expectAllPassed (Host.run "error-then-back")

            testCase "a changed type reaches the lets that use it in a later file"
            <| fun () -> Host.expectAllPassed (Host.run "types")

            testCase "an edit to the entry file, and a type under a type of a later file, report that a restart is needed"
            <| fun () -> Host.expectAllPassed (Host.run "restart")
        ]
