namespace Ranvier.View.WebCore

open System
open System.Globalization
open Ranvier.Dom

/// <summary>Application locale preferences over the native process's .NET globalization providers.</summary>
module Locales =
    let private locale value =
        if
            String.IsNullOrWhiteSpace value
            || value.Contains '_'
        then
            invalidArg "locale" "Expected a non-empty BCP47 culture identifier."

        CultureInfo.GetCultureInfo(value).Name

    let private timeZone value =
        try
            let zone = TimeZoneInfo.FindSystemTimeZoneById value

            if zone.Id = "UTC" || zone.Id = "Etc/UTC" then
                "UTC"
            elif zone.HasIanaId then
                zone.Id
            else
                let converted, identifier = TimeZoneInfo.TryConvertWindowsIdToIanaId zone.Id
                if converted then identifier else zone.Id
        with
        | :? TimeZoneNotFoundException as error -> invalidArg "timeZone" error.Message
        | :? InvalidTimeZoneException as error -> invalidArg "timeZone" error.Message

    let private culture name (value: CultureInfo) =
        if String.IsNullOrEmpty value.Name then
            LocaleAvailability.Unavailable (
                name
                + " is invariant; no language identifier is reported."
            )
        else
            LocaleAvailability.Available value.Name

    let private hostPreferences () : LocaleSettings =
        let languages =
            match culture "CurrentUICulture" CultureInfo.CurrentUICulture with
            | LocaleAvailability.Available value -> LocaleAvailability.Available [ value ]
            | LocaleAvailability.Unavailable reason -> LocaleAvailability.Unavailable reason

        {
            Locale = culture "CurrentCulture" CultureInfo.CurrentCulture
            Languages = languages
            TimeZone = LocaleAvailability.Available TimeZoneInfo.Local.Id
        }

    /// <summary>Creates borrowed application preferences guarded by the host's lifetime and creating thread.</summary>
    /// <param name="host">The active host that owns the service's access boundary.</param>
    /// <remarks>Languages report the application's CurrentUICulture singleton. Overrides preserve process and OS settings.</remarks>
    let forHost (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        let provider =
            {
                Name = ".NET CurrentCulture, CurrentUICulture singleton and TimeZoneInfo.Local"
                LocaleCanonicalization =
                    "CultureInfo.Name: provider casing and supported cultures; aliases and Unicode extensions may remain distinct."
                TimeZoneCanonicalization = "TimeZoneInfo identifiers; Windows IDs map to IANA when supported by the runtime's ICU database."
                ReadHost = hostPreferences
                CanonicalizeLocale = locale
                CanonicalizeTimeZone = timeZone
            }

        LocaleService (provider, ensureAccess = host.EnsureActive)
