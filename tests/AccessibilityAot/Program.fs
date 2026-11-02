module private AccessibilityAot

open System
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

let private markup =
    "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60'><text aria-label='Accessible' x='1' y='20'>Accessible</text></svg>"

[<EntryPoint>]
let main arguments =
    let expectAttached = arguments |> Array.contains "--expect-attached"
    let mutable windows = 0
    let mutable hosts = 0

    for index in 1..3 do
        Console.WriteLine ("stage window-create " + string index)

        let window =
            Window.Create ("Accessibility ownership", 80, 60, hidden = true, renderer = "software")

        try
            Console.WriteLine ("stage load " + string index)
            window.Host.LoadSvg markup
            Console.WriteLine ("stage step " + string index)
            window.Step (0) |> ignore

            if window.AccessibilityAttached <> expectAttached then
                invalidOp "Optional accessibility attachment differs from the selected provider profile."

            if index = 2 then
                Console.WriteLine ("stage direct-window-host-dispose " + string index)
                (window.Host :> IDisposable).Dispose()
        finally
            Console.WriteLine ("stage window-dispose " + string index)
            (window :> IDisposable).Dispose()
            Console.WriteLine ("stage window-dispose-repeat " + string index)
            (window :> IDisposable).Dispose()

        windows <- windows + 1

    for _ in 1..3 do
        Console.WriteLine ("stage direct-host-create")
        let host = WebCoreHost.Create (80, 60)
        host.LoadSvg markup
        Console.WriteLine ("stage direct-host-dispose")
        (host :> IDisposable).Dispose()
        (host :> IDisposable).Dispose()
        hosts <- hosts + 1

    Console.WriteLine (
        "{\"windows\":"
        + string windows
        + ",\"directHosts\":"
        + string hosts
        + ",\"expectedAttached\":"
        + (if expectAttached then "true" else "false")
        + ",\"dynamicCodeSupported\":"
        + (if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
               "true"
           else
               "false")
        + "}"
    )

    0
