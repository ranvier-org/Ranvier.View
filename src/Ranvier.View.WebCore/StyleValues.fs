namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Document-scoped CSS Typed OM parsing and construction over a WebCore host.</summary>
module StyleValues =
    /// <summary>Creates a borrowed typed-value provider bound to the host's document and creating thread.</summary>
    /// <param name="host">The live host whose engine parses and owns the returned value handles.</param>
    /// <remarks>Disposal invalidates this provider without changing previously returned values.</remarks>
    let forHost (host: WebCoreHost) : StyleValueService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-css-style-values") then
            Interop.unsupported<unit> "dom-css-style-values"

        let thread = Environment.CurrentManagedThreadId
        let session = host.Session
        let document = box host.Document :?> NativeObject
        let mutable disposed = false

        let active () =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException "StyleValueService")

            Interop.handle (box document)

        let engine action =
            try
                action ()
            with WebCoreDomException (name, detail) ->
                raise (StyleValueException (name, detail))

        let wrap handle =
            session.Object (
                handle,
                Kind.StyleValue,
                document,
                Func<Session, nativeint, NativeObject>(fun owner value -> Classes.CSSStyleValue (owner, value))
            )
            |> unbox<CSSStyleValue>

        let wrapReference handle =
            session.Object (
                handle,
                Kind.VariableReferenceValue,
                document,
                Func<Session, nativeint, NativeObject>(fun owner value -> CSSVariableReferenceValueObject (owner, value))
            )
            |> unbox<CSSVariableReferenceValue>

        { new StyleValueService with
            member _.Parse(property, cssText) =
                let target = active ()
                use scope = new InputScope ()
                let propertyBytes, textBytes = scope.Bytes property, scope.Bytes cssText

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_css_style_value_parse (target, propertyBytes, propertyBytes.Length, textBytes, textBytes.Length, &result)
                    |> Interop.check "CSSStyleValue.parse"

                    wrap result)

            member _.ParseAll(property, cssText) =
                let target = active ()
                use scope = new InputScope ()
                let propertyBytes, textBytes = scope.Bytes property, scope.Bytes cssText

                engine (fun () ->
                    let mutable result = NativeDom.RvDomSequenceCSSStyleValue ()

                    try
                        NativeDom.rv_dom_css_style_value_parse_all (
                            target,
                            propertyBytes,
                            propertyBytes.Length,
                            textBytes,
                            textBytes.Length,
                            &result
                        )
                        |> Interop.check "CSSStyleValue.parseAll"

                        Array.init result.count (fun index ->
                            let handle = Marshal.ReadIntPtr (result.items, index * IntPtr.Size)

                            NativeDom.rv_dom_css_style_value_retain handle
                            |> ignore

                            wrap handle)
                    finally
                        NativeDom.rv_dom_sequence_css_style_value_release &result)

            member _.CreateKeyword value =
                let target = active ()
                use scope = new InputScope ()
                let bytes = scope.Bytes value

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_cssom_keyword_value_create (target, bytes, bytes.Length, &result)
                    |> Interop.check "CSSKeywordValue.constructor"

                    wrap result :?> CSSKeywordValue)

            member _.CreateUnit(value, unitName) =
                let target = active ()
                use scope = new InputScope ()
                let bytes = scope.Bytes unitName

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_css_unit_value_create (target, value, bytes, bytes.Length, &result)
                    |> Interop.check "CSSUnitValue.constructor"

                    wrap result :?> CSSUnitValue)

            member _.CreateUnparsed segments =
                let target = active ()

                if isNull segments then
                    nullArg (nameof segments)

                use scope = new InputScope ()

                let values =
                    segments
                    |> Array.map (fun segment ->
                        if isNull (box segment) then
                            nullArg (nameof segment)

                        let mutable value = NativeDom.RvDomUnionUSVStringOrCSSOMVariableReferenceValue ()
                        value.present <- 1

                        match segment with
                        | USVStringOrCSSOMVariableReferenceValue.USVString text ->
                            value.tag <- 1
                            value.arm0 <- scope.TextSpan text
                        | USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue reference ->
                            value.tag <- 2
                            value.arm1 <- Interop.valueHandle session (box reference)

                        value)

                let mutable members =
                    NativeDom.RvDomSequenceUSVStringOrCSSOMVariableReferenceValue ()

                members.present <- 1
                members.count <- values.Length
                members.items <- scope.Pin values

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_css_unparsed_value_create (target, [| members |], &result)
                    |> Interop.check "CSSUnparsedValue.constructor"

                    wrap result :?> CSSUnparsedValue)

            member _.CreateVariableReference(variable, fallback) =
                let target = active ()

                let fallback =
                    match fallback with
                    | None -> 0n
                    | Some value when isNull (box value) -> 0n
                    | Some value -> Interop.valueHandle session (box value)

                use scope = new InputScope ()
                let bytes = scope.Bytes variable

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_cssom_variable_reference_value_create (target, bytes, bytes.Length, fallback, &result)
                    |> Interop.check "CSSVariableReferenceValue.constructor"

                    wrapReference result)

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    invalidOp "A typed-value service is disposed on its host thread"

                disposed <- true
        }
