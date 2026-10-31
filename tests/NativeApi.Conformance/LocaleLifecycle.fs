module internal NativeApiConformance.LocaleLifecycle

open System
open System.Globalization
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

let run () =
    let mutable assertions = 0

    let check name condition =
        assertions <- assertions + 1

        if not condition then
            failwith ("H06 lifecycle " + name)

    let cultureBefore = CultureInfo.CurrentCulture.Name
    let languageBefore = CultureInfo.CurrentUICulture.Name
    let mutable nullRejected = false

    try
        Locales.forHost Unchecked.defaultof<WebCoreHost>
        |> ignore
    with :? ArgumentNullException ->
        nullRejected <- true

    check "null host rejects" nullRejected

    use host = WebCoreHost.Create (16, 16)
    let session = host.Session
    let service = Locales.forHost host

    let settings: LocaleOverrides =
        {
            Locale = LocalePreference.Override "fr-FR"
            Languages = LocalePreference.Override [ "fr-FR" ]
            TimeZone = LocalePreference.Override "UTC"
        }

    let operations =
        [
            (fun () -> service.Current |> ignore)
            (fun () -> service.Host |> ignore)
            (fun () -> service.Overrides |> ignore)
            (fun () -> service.ProviderName |> ignore)
            (fun () -> service.LocaleCanonicalization |> ignore)
            (fun () -> service.TimeZoneCanonicalization |> ignore)
            (fun () -> service.CanonicalizeLocale "en-US" |> ignore)
            (fun () -> service.CanonicalizeTimeZone "UTC" |> ignore)
            (fun () -> service.SetOverrides settings)
            (fun () -> service.ClearOverrides ())
            (fun () -> service.RefreshHost ())
        ]

    for operation in operations do
        let mutable rejected = false

        let worker =
            Thread (
                ThreadStart (fun () ->
                    try
                        operation ()
                    with :? InvalidOperationException ->
                        rejected <- true)
            )

        worker.Start ()
        worker.Join ()
        check "wrong thread rejects" rejected

    service.SetOverrides settings
    check "application locale preserves process culture" (CultureInfo.CurrentCulture.Name = cultureBefore)
    check "application languages preserve process UI culture" (CultureInfo.CurrentUICulture.Name = languageBefore)
    (host :> IDisposable).Dispose()

    for operation in operations do
        let mutable rejected = false

        try
            operation ()
        with :? ObjectDisposedException ->
            rejected <- true

        check "disposed host rejects" rejected

    check "session has no retained objects" (session.LiveObjects = 0)
    assertions
