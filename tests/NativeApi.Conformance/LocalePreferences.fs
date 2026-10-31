module internal NativeApiConformance.LocalePreferences

open System
open Ranvier.Dom

exception private LocaleFixtureAccessDenied

let private inheritAll: LocaleOverrides =
    {
        Locale = LocalePreference.HostDefault
        Languages = LocalePreference.HostDefault
        TimeZone = LocalePreference.HostDefault
    }

let run (create: unit -> LocaleService) =
    let mutable assertions = 0

    let check name condition =
        assertions <- assertions + 1

        if not condition then
            failwith ("H06 " + name)

    let service = create ()
    let independent = create ()
    let reported = service.Host
    let independentBefore = independent.Current
    let initial = service.Current
    check "provider provenance" (not (String.IsNullOrWhiteSpace service.ProviderName))
    check "locale policy" (not (String.IsNullOrWhiteSpace service.LocaleCanonicalization))
    check "time-zone policy" (not (String.IsNullOrWhiteSpace service.TimeZoneCanonicalization))
    check "locale casing" (service.CanonicalizeLocale "EN-us" = "en-US")
    check "UTC normalization" (service.CanonicalizeTimeZone "UTC" = "UTC")
    check "initial preferences inherit host" (initial = reported)

    let changed: LocaleOverrides =
        {
            Locale = LocalePreference.Override "FR-fr"
            Languages = LocalePreference.Override [ "FR-fr"; "fr-FR"; "en-US" ]
            TimeZone = LocalePreference.Override "UTC"
        }

    service.SetOverrides changed
    check "application locale override" (service.Current.Locale = LocaleAvailability.Available "fr-FR")
    check "stable canonical language preferences" (service.Current.Languages = LocaleAvailability.Available [ "fr-FR"; "en-US" ])
    check "application time-zone override" (service.Current.TimeZone = LocaleAvailability.Available "UTC")
    check "reported host remains unchanged" (service.Host = reported)
    check "independent service remains unchanged" (independent.Current = independentBefore)
    check "retained immutable snapshot" (initial = reported)
    check "canonical override snapshot" (service.Overrides.Locale = LocalePreference.Override "fr-FR")

    for invalid in
        [
            { changed with
                Locale = LocalePreference.Override ""
            }
            { changed with
                Locale = LocalePreference.Override null
            }
            { changed with
                Locale = LocalePreference.Override "not_a_locale"
            }
            { changed with
                Languages = LocalePreference.Override [ "en-US"; "" ]
            }
            { changed with
                Languages = LocalePreference.Override Unchecked.defaultof<string list>
            }
            { changed with
                TimeZone = LocalePreference.Override "Never/AZone"
            }
            { changed with
                TimeZone = LocalePreference.Override " "
            }
            { changed with
                TimeZone = LocalePreference.Override null
            }
        ] do
        let before = service.Current
        let overridesBefore = service.Overrides
        let mutable rejected = false

        try
            service.SetOverrides invalid
        with :? ArgumentException ->
            rejected <- true

        check "invalid preference rejects" rejected

        check
            "invalid update is atomic"
            (service.Current = before
             && service.Overrides = overridesBefore)

    service.SetOverrides
        { inheritAll with
            Languages = LocalePreference.Override []
        }

    check "explicit no-language preference" (service.Current.Languages = LocaleAvailability.Available [])

    check
        "other preferences still inherit"
        (service.Current.Locale = reported.Locale
         && service.Current.TimeZone = reported.TimeZone)

    service.RefreshHost ()
    check "refresh preserves override" (service.Current.Languages = LocaleAvailability.Available [])
    service.ClearOverrides ()
    check "reset inherits refreshed host" (service.Current = service.Host)
    check "reset clears every override" (service.Overrides = inheritAll)
    check "independent preferences still unchanged" (independent.Current = independentBefore)
    assertions

let deterministic () =
    let mutable assertions = 0

    let check name condition =
        assertions <- assertions + 1

        if not condition then
            failwith ("H06 deterministic " + name)

    let mutable current: LocaleSettings =
        {
            Locale = LocaleAvailability.Available "en-US"
            Languages = LocaleAvailability.Available [ "en-US" ]
            TimeZone = LocaleAvailability.Available "UTC"
        }

    let provider =
        {
            Name = "Injected preference source"
            LocaleCanonicalization = "Identity fixture identifiers"
            TimeZoneCanonicalization = "Identity fixture identifiers"
            ReadHost = fun () -> current
            CanonicalizeLocale = id
            CanonicalizeTimeZone = id
        }

    let service = LocaleService provider
    let before = service.Current

    current <-
        { current with
            Locale = LocaleAvailability.Available "de-DE"
            Languages = LocaleAvailability.Available [ "de-DE"; "en-US" ]
        }

    check "host changes require explicit refresh" (service.Current = before)

    service.SetOverrides
        { inheritAll with
            TimeZone = LocalePreference.Override "UTC"
        }

    service.RefreshHost ()
    check "fresh locale reported" (service.Host.Locale = LocaleAvailability.Available "de-DE")
    check "fresh preference list reported" (service.Current.Languages = LocaleAvailability.Available [ "de-DE"; "en-US" ])
    check "refresh preserves explicit override" (service.Overrides.TimeZone = LocalePreference.Override "UTC")
    check "old snapshot immutable after refresh" (before.Locale = LocaleAvailability.Available "en-US")

    let fresh = service.Host

    current <-
        { current with
            Locale = LocaleAvailability.Available ""
        }

    let mutable refreshRejected = false

    try
        service.RefreshHost ()
    with :? ArgumentException ->
        refreshRejected <- true

    check "invalid host refresh rejects" refreshRejected
    check "invalid host refresh is atomic" (service.Host = fresh)

    current <-
        {
            Locale = LocaleAvailability.Unavailable "Invariant locale"
            Languages = LocaleAvailability.Unavailable "Host list unavailable"
            TimeZone = LocaleAvailability.Unavailable "Host time zone unavailable"
        }

    service.ClearOverrides ()
    service.RefreshHost ()
    check "unavailable locale remains explicit" (service.Current.Locale = LocaleAvailability.Unavailable "Invariant locale")
    check "unavailable languages remain explicit" (service.Current.Languages = LocaleAvailability.Unavailable "Host list unavailable")
    check "unavailable time zone remains explicit" (service.Current.TimeZone = LocaleAvailability.Unavailable "Host time zone unavailable")

    service.SetOverrides
        { inheritAll with
            Locale = LocalePreference.Override "fr-FR"
        }

    check "application override can supply unavailable host field" (service.Current.Locale = LocaleAvailability.Available "fr-FR")
    check "other unavailable fields preserved" (service.Current.TimeZone = current.TimeZone)

    let mutable disposed = false

    let guarded =
        LocaleService (
            provider,
            ensureAccess =
                fun () ->
                    if disposed then
                        raise LocaleFixtureAccessDenied
        )

    disposed <- true
    let mutable disposedRejected = false

    try
        guarded.Current |> ignore
    with LocaleFixtureAccessDenied ->
        disposedRejected <- true

    check "borrowed guard rejects expired source" disposedRejected
    let mutable nullRejected = false

    try
        LocaleService (Unchecked.defaultof<LocaleProvider>)
        |> ignore
    with :? ArgumentNullException ->
        nullRejected <- true

    check "null provider rejects" nullRejected
    assertions
