module Ranvier.View.WebCore.Hmr.Tests.Main

open Expecto

[<EntryPoint>]
let main argv =
    runTestsInAssemblyWithCLIArgs [ Sequenced ] argv
