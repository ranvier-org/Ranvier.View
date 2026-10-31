namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Custom highlights of a native host's document, from WebKit's Highlight and HighlightRegistry.</summary>
module Highlights =
    let inline private items<'T, 'U when 'T: struct> (items: nativeint) (count: int) ([<InlineIfLambda>] read: 'T -> 'U) =
        if count < 0 || (count > 0 && items = 0n) then
            invalidOp "WebCore returned an invalid highlight sequence"

        Array.init count (fun index -> read (Marshal.PtrToStructure<'T>(items + nativeint (index * Marshal.SizeOf<'T>()))))

    /// <summary>A highlight service for the document <c>host</c> shows when the service is created.</summary>
    /// <remarks>
    /// <c>HighlightsFromPoint</c> answers <c>HighlightHitTest.Unsupported</c>: the pinned WebKit has no
    /// <c>highlightsFromPoint</c>. Replacing the document, or disposing the host, clears the document's registry and
    /// empties each highlight registered in it. Raises NotSupportedException when the native library lacks the
    /// highlight functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : HighlightService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "dom:Highlight"
                && WebCoreHost.Has "dom:HighlightRegistry"
                && WebCoreHost.Has "dom-custom-highlights"
            )
        then
            raise (NotSupportedException "The native library has no custom highlight functions")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof HighlightService))

            host.EnsureActive ()

            if not (obj.ReferenceEquals (host.Document, document)) then
                raise (InvalidOperationException "The highlight document is no longer active")

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle value
            | _ -> invalidArg name $"The {name} belongs to another provider"

        let owner = unbox<NativeObject>(box document)

        let highlight (handle: nativeint) : Highlight =
            session.Object (handle, Kind.Highlight, owner, Func<Session, nativeint, NativeObject>(fun s h -> HighlightObject (s, h)))
            |> unbox<Highlight>

        // A range enters a highlight through Create or Add, which remembers its wrapper; a range whose wrapper was
        // collected comes back as a new Range or StaticRange.
        let remember (range: AbstractRange) =
            session.Remember (unbox<NativeObject>(box range))

        let range (handle: nativeint) : AbstractRange =
            let mutable common = 0n

            let isRange =
                NativeDom.rv_dom_range_get_common_ancestor_container (handle, &common) = 0

            if common <> 0n then
                Native.rv_node_release common

            session.Object (
                handle,
                Kind.Range,
                owner,
                Func<Session, nativeint, NativeObject>(fun s h ->
                    if isRange then
                        RangeObject (s, h) :> NativeObject
                    else
                        StaticRangeObject (s, h) :> NativeObject)
            )
            |> unbox<AbstractRange>

        let rangeHandle (value: AbstractRange) =
            native (box (HighlightContract.instance "range" value)) "range"

        let highlightHandle (value: Highlight) =
            native (box (HighlightContract.instance "highlight" value)) "highlight"

        let registryHandle (value: HighlightRegistry) =
            native (box (HighlightContract.instance "registry" value)) "registry"

        let name (value: string) =
            Encoding.UTF8.GetBytes (HighlightContract.name value)

        let registry =
            let mutable result = 0n

            NativeDom.rv_dom_css_get_highlights (Interop.handle (box document), &result)
            |> Interop.check "CSS.highlights"

            session.Object (result, Kind.HighlightRegistry, owner, Func<Session, nativeint, NativeObject>(fun s h -> HighlightRegistryObject (s, h)))
            |> unbox<HighlightRegistry>

        { new HighlightService with
            member _.Registry =
                check ()
                registry

            member _.Create initialRanges =
                check ()
                let ranges = HighlightContract.ranges initialRanges
                let handles = ranges |> Array.map rangeHandle
                let pinned = GCHandle.Alloc (handles, GCHandleType.Pinned)

                try
                    let mutable sequence = NativeDom.RvDomSequenceAbstractRange ()
                    sequence.present <- 1
                    sequence.count <- handles.Length

                    sequence.items <-
                        if handles.Length = 0 then
                            0n
                        else
                            pinned.AddrOfPinnedObject ()

                    let mutable result = 0n

                    NativeDom.rv_dom_highlight_create (Interop.handle (box document), [| sequence |], &result)
                    |> Interop.check "Highlight constructor"

                    Array.iter remember ranges
                    highlight result
                finally
                    pinned.Free ()

            member _.Size value =
                check ()
                let mutable result = 0u

                NativeDom.rv_dom_highlight_size (highlightHandle value, &result)
                |> Interop.check "Highlight.size"

                int result

            member _.Has(value, item) =
                check ()
                let mutable result = 0

                NativeDom.rv_dom_highlight_has (highlightHandle value, rangeHandle item, &result)
                |> Interop.check "Highlight.has"

                result <> 0

            member _.Add(value, item) =
                check ()

                NativeDom.rv_dom_highlight_add (highlightHandle value, rangeHandle item)
                |> Interop.check "Highlight.add"

                remember item

            member _.Delete(value, item) =
                check ()
                let mutable result = 0

                NativeDom.rv_dom_highlight_delete (highlightHandle value, rangeHandle item, &result)
                |> Interop.check "Highlight.delete"

                result <> 0

            member _.Clear value =
                check ()

                NativeDom.rv_dom_highlight_clear (highlightHandle value)
                |> Interop.check "Highlight.clear"

            member _.Ranges value =
                check ()
                let mutable result = NativeDom.RvDomSequenceAbstractRange ()

                try
                    NativeDom.rv_dom_highlight_values (highlightHandle value, &result)
                    |> Interop.check "Highlight.values"

                    items result.items result.count (fun (handle: nativeint) -> range (NativeDom.rv_dom_abstract_range_retain handle))
                finally
                    NativeDom.rv_dom_sequence_abstract_range_release &result

            member _.Count value =
                check ()
                let mutable result = 0u

                NativeDom.rv_dom_highlight_registry_size (registryHandle value, &result)
                |> Interop.check "HighlightRegistry.size"

                int result

            member _.Contains(value, key) =
                check ()
                let key = name key
                let mutable result = 0

                NativeDom.rv_dom_highlight_registry_has (registryHandle value, key, key.Length, &result)
                |> Interop.check "HighlightRegistry.has"

                result <> 0

            member _.TryGet(value, key) =
                check ()
                let key = name key
                let mutable result = 0n

                NativeDom.rv_dom_highlight_registry_get (registryHandle value, key, key.Length, &result)
                |> Interop.check "HighlightRegistry.get"

                if result = 0n then None else Some (highlight result)

            member _.Set(value, key, item) =
                check ()
                let key = name key

                NativeDom.rv_dom_highlight_registry_set (registryHandle value, key, key.Length, highlightHandle item)
                |> Interop.check "HighlightRegistry.set"

            member _.Remove(value, key) =
                check ()
                let key = name key
                let mutable result = 0

                NativeDom.rv_dom_highlight_registry_delete (registryHandle value, key, key.Length, &result)
                |> Interop.check "HighlightRegistry.delete"

                result <> 0

            member _.ClearRegistry value =
                check ()

                NativeDom.rv_dom_highlight_registry_clear (registryHandle value)
                |> Interop.check "HighlightRegistry.clear"

            member _.Entries value =
                check ()
                let mutable result = NativeDom.RvDomSequencePairDOMStringAndHighlight ()

                try
                    NativeDom.rv_dom_highlight_registry_entries (registryHandle value, &result)
                    |> Interop.check "HighlightRegistry.entries"

                    items result.items result.count (fun (entry: NativeDom.RvDomPairDOMStringAndHighlight) ->
                        Interop.spanText entry.key, highlight (NativeDom.rv_dom_highlight_retain entry.value))
                finally
                    NativeDom.rv_dom_sequence_pair_dom_string_and_highlight_release &result

            member _.HighlightsFromPoint(_, _) =
                check ()
                HighlightHitTest.Unsupported

            member _.Dispose() =
                disposed.Value <- true
        }
