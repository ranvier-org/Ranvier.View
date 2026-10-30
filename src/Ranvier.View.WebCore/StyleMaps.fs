namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed providers for actual inline, declared and computed typed style maps.</summary>
module StyleMaps =
    /// <summary>Creates a provider bound to a live document wrapper and its owning native session.</summary>
    /// <param name="document">The real document supplying ownerless declared-map context.</param>
    /// <remarks>Same-session secondary documents are supported. Cached maps preserve their first acquisition document.</remarks>
    let forDocument (document: Document) : StyleMapService =
        if isNull (box document) then
            nullArg (nameof document)

        let documentOwner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        document.nodeType |> ignore

        if
            not (
                WebCoreHost.Has "dom-type-query:StylePropertyMapReadOnly"
                && WebCoreHost.Has "dom-css-style-map-entries"
            )
        then
            Interop.unsupported<unit> "CSS typed style maps"

        let session = documentOwner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A style-map service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "StyleMapService")

            Interop.handle (box documentOwner) |> ignore

        let native (name: string) (value: obj) : NativeObject =
            if isNull value then
                nullArg name

            match value with
            | :? NativeObject as target when obj.ReferenceEquals (session, target.Session) ->
                Interop.handle value |> ignore
                target
            | _ -> invalidArg name "The object must belong to the same native session"

        let map (value: StylePropertyMapReadOnly) =
            active ()
            let target = native (nameof value) (box value)

            if target.Kind <> Kind.StyleMap then
                invalidArg (nameof value) "The object is not a native typed style map"

            Interop.contextHandle target |> ignore
            target

        let engine action =
            try
                action ()
            with WebCoreDomException (name, detail) ->
                raise (StyleValueException (name, detail))

        let wrapMap (owner: NativeObject) (context: NativeObject) (handle: nativeint) =
            session.Object (
                handle,
                Kind.StyleMap,
                owner,
                Func<Session, nativeint, NativeObject>(fun targetSession value ->
                    let target = Classes.StylePropertyMapReadOnly (targetSession, value)
                    target.BindDocumentContext context
                    target)
            )
            |> unbox<StylePropertyMap>

        let wrapValue (context: NativeObject) (handle: nativeint) =
            session.Object (
                handle,
                Kind.StyleValue,
                context,
                Func<Session, nativeint, NativeObject>(fun targetSession value -> Classes.CSSStyleValue (targetSession, value))
            )
            |> unbox<CSSStyleValue>

        { new StyleMapService with
            member _.Attribute element =
                active ()
                native (nameof element) (box element) |> ignore
                engine (fun () -> element.attributeStyleMap)

            member _.Computed element =
                active ()
                native (nameof element) (box element) |> ignore
                engine (fun () -> element.computedStyleMap ())

            member _.Declared rule =
                active ()
                let owner = native (nameof rule) (box rule)

                engine (fun () ->
                    let sheet = rule.parentStyleSheet
                    let node = if isNull sheet then null else sheet.ownerNode

                    let context =
                        if isNull node then
                            documentOwner
                        else
                            let document =
                                match box node with
                                | :? Document as value -> value
                                | _ -> node.ownerDocument

                            native "acquisition document" (box document)

                    Interop.handle (box context) |> ignore
                    let mutable result = 0n

                    NativeDom.rv_dom_css_style_rule_get_style_map (owner.Handle, &result)
                    |> Interop.check "CSSStyleRule.styleMap"

                    wrapMap owner context result)

            member _.Get(value, property) =
                map value |> ignore

                engine (fun () ->
                    match value.get property with
                    | UndefinedOrCSSStyleValue.Undefined () -> None
                    | UndefinedOrCSSStyleValue.CSSStyleValue value -> Some value)

            member _.Entries value =
                let target = map value
                let context = Interop.contextDocument target
                let contextHandle = Interop.handle (box context)

                engine (fun () ->
                    let mutable result =
                        NativeDom.RvDomSequencePairUSVStringAndSequenceCSSStyleValueNullableElement ()

                    try
                        NativeDom.rv_dom_style_property_map_read_only_entries (target.Handle, contextHandle, &result)
                        |> Interop.check "StylePropertyMapReadOnly.entries"

                        if
                            result.present <> 1
                            || result.count < 0
                            || (result.count > 0 && result.items = 0n)
                        then
                            invalidOp "WebCore returned an invalid entry snapshot"

                        Array.init result.count (fun index ->
                            let entry =
                                Marshal.PtrToStructure<NativeDom.RvDomPairUSVStringAndSequenceCSSStyleValueNullableElement>(
                                    result.items
                                    + nativeint (
                                        index
                                        * Marshal.SizeOf<NativeDom.RvDomPairUSVStringAndSequenceCSSStyleValueNullableElement>()
                                    )
                                )

                            if
                                entry.present <> 1
                                || entry.value.present <> 1
                                || entry.value.count < 0
                                || (entry.value.count > 0 && entry.value.items = 0n)
                            then
                                invalidOp "WebCore returned an invalid entry value sequence"

                            let property = Interop.spanText entry.key

                            if isNull property then
                                invalidOp "WebCore returned a null entry property"

                            let values =
                                Array.init entry.value.count (fun at ->
                                    let handle = Marshal.ReadIntPtr (entry.value.items, at * IntPtr.Size)

                                    if handle = 0n then
                                        Unchecked.defaultof<CSSStyleValue>
                                    else
                                        NativeDom.rv_dom_css_style_value_retain handle
                                        |> ignore

                                        wrapValue context handle)

                            { Property = property; Values = values })
                    finally
                        NativeDom.rv_dom_sequence_pair_usv_string_and_sequence_css_style_value_nullable_element_release &result)

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    invalidOp "A style-map service is disposed on its document's owning thread"

                disposed <- true
        }

    /// <summary>Creates a borrowed provider using the host's live document.</summary>
    /// <param name="host">The live host owning the maps and value handles.</param>
    let forHost (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
