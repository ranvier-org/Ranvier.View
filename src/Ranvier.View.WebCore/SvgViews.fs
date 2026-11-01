namespace Ranvier.View.WebCore

open Ranvier.Dom

/// <summary>Observes current-view properties supplied by the native SVG root.</summary>
module SvgViews =
    /// <summary>Reads actual view properties while preserving native access and lifetime errors.</summary>
    /// <param name="root">The nonnull SVG root owned by its live native host and calling thread.</param>
    /// <exception cref="T:System.ArgumentNullException">The root is null.</exception>
    let inspect (root: SVGSVGElement) : SvgViewState =
        if isNull (box root) then
            nullArg "root"

        let read action =
            try
                SvgViewAvailability.Available (action ())
            with WebCoreDomException (name, detail) when name = "NotSupportedError" ->
                SvgViewAvailability.Unsupported detail

        let view =
            match read (fun () -> root.currentView) with
            | SvgViewAvailability.Available value when isNull (box value) -> SvgViewAvailability.Unknown "The currentView getter returned no view."
            | value -> value

        {
            CurrentView = view
            UseCurrentView = read (fun () -> root.useCurrentView)
        }
