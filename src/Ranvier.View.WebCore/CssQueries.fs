namespace Ranvier.View.WebCore

open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Document-scoped CSS support queries over a WebCore host.</summary>
module CssQueries =
    /// <summary>Creates a borrowed provider whose queries run on the live host's creating thread.</summary>
    /// <param name="host">The host whose document settings and CSS parser evaluate each query.</param>
    let forHost (host: WebCoreHost) : CssQueryService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-css-supports") then
            Interop.unsupported<unit> "dom-css-supports"

        let document = box host.Document :?> NativeObject

        let bytes value =
            Encoding.UTF8.GetBytes (if isNull value then "null" else value)

        { new CssQueryService with
            member _.Supports(property, value) =
                host.EnsureActive ()
                let propertyBytes = bytes property
                let valueBytes = bytes value
                let mutable result = 0

                NativeDom.rv_dom_css_supports_2 (document.Handle, propertyBytes, propertyBytes.Length, valueBytes, valueBytes.Length, &result)
                |> Interop.check "CSS.supports(property, value)"

                result <> 0

            member _.Supports(conditionText) =
                host.EnsureActive ()
                let conditionBytes = bytes conditionText
                let mutable result = 0

                NativeDom.rv_dom_css_supports_1 (document.Handle, conditionBytes, conditionBytes.Length, &result)
                |> Interop.check "CSS.supports(conditionText)"

                result <> 0
        }
