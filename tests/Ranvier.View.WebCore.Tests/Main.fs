module Ranvier.View.WebCore.Tests.Main

open System
open Expecto

[<EntryPoint>]
let main argv =
    let run () =
        runTestsInAssemblyWithCLIArgs [ Sequenced ] argv

    // WebKit on macOS owns the process's main thread, and SDL's windows need it too: test bodies run there.
    if OperatingSystem.IsMacOS () then
        Support.serveOnThisThread run
    else
        run ()
