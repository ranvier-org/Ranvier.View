module Ranvier.View.WebCore.AotTransitions.Program

open Ranvier.View.WebCore

[<EntryPoint>]
let main _ =
    use host = WebCoreHost.Create (320, 240)

    if not (WebCoreHost.Has "view-transitions") then
        failwith "View transitions are unavailable"

    let nextFrame () =
        async {
            host.Frame () |> ignore
            RanvierWebCoreAbi.Native.rv_run_loop_cycle 16 |> ignore
        }

    let count =
        NativeApiConformance.ViewTransitionChecks.run host.Document (fun () -> ViewTransitions.forHost host) nextFrame
        |> Async.StartImmediateAsTask
        |> fun task -> task.GetAwaiter().GetResult()

    if count <> 35 then
        failwith "View-transition assertions did not run"

    System.Console.WriteLine "NATIVE_API A11 PASS assertions=35"
    0
