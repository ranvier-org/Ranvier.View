module internal NativeApiConformance.Native

open Ranvier.View.WebCore

[<EntryPoint>]
let main _ =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    try
        let checks = Tables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10 PASS assertions={checks}"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"D10 leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine "NATIVE_API D10 TEARDOWN PASS"
    0
