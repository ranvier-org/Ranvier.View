module internal NativeApiConformance.Native

open Ranvier.View.WebCore

[<EntryPoint>]
let main _ =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    try
        let checks = Tables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10 PASS assertions={checks}"
        let reflection = Reflection.run host.Document
        System.Console.WriteLine $"NATIVE_API D11 PASS assertions={reflection}"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"Shared API fixtures leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine "NATIVE_API TEARDOWN PASS groups=D10,D11"
    0
