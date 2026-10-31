namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>URL, URLSearchParams and URLPattern objects of a native host, from WebKit's URL parser.</summary>
module Urls =
    let private bytes (value: string) =
        Encoding.UTF8.GetBytes value

    let private optionalBytes (value: string option) =
        match value with
        | Some text -> bytes text, (bytes text).Length
        | None -> null, -1

    /// <summary>UTF-8 copies in unmanaged memory, freed together when the scope is disposed.</summary>
    [<Sealed>]
    type private Strings() =
        let allocations = List<nativeint>()

        member _.Allocate(size: int) =
            let pointer = Marshal.AllocHGlobal (max size 1)
            allocations.Add pointer
            pointer

        member this.Span(value: string) =
            let data = bytes value
            let pointer = this.Allocate data.Length
            Marshal.Copy (data, 0, pointer, data.Length)
            let mutable span = NativeDom.RvDomUtf8StringSpan ()
            span.data <- pointer
            span.length <- data.Length
            span

        member this.Input(value: string option) =
            let mutable input = NativeDom.RvDomUtf8StringInput ()

            match value with
            | Some text ->
                let span = this.Span text
                input.present <- 1
                input.data <- span.data
                input.length <- span.length
            | None -> ()

            input

        interface IDisposable with
            member _.Dispose() =
                for pointer in allocations do
                    Marshal.FreeHGlobal pointer

                allocations.Clear ()

    let private patternInput (strings: Strings) (value: USVStringOrURLPatternInit) =
        let mutable input = NativeDom.RvDomUnionUSVStringOrURLPatternInit ()
        input.present <- 1

        match UrlContract.patternInput value with
        | USVStringOrURLPatternInit.USVString text ->
            input.tag <- NativeDom.RV_DOM_UNION_USV_STRING_OR_URL_PATTERN_INIT_STRING
            input.arm0 <- strings.Span text
        | USVStringOrURLPatternInit.URLPatternInit value ->
            let mutable init = NativeDom.RvDomURLPatternInit ()
            init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomURLPatternInit>())
            init.protocol <- strings.Input value.protocol
            init.username <- strings.Input value.username
            init.password <- strings.Input value.password
            init.hostname <- strings.Input value.hostname
            init.port <- strings.Input value.port
            init.pathname <- strings.Input value.pathname
            init.search <- strings.Input value.search
            init.hash <- strings.Input value.hash
            init.base_url <- strings.Input value.baseURL
            let pointer = strings.Allocate (Marshal.SizeOf<NativeDom.RvDomURLPatternInit>())
            Marshal.StructureToPtr<NativeDom.RvDomURLPatternInit>(init, pointer, false)
            input.tag <- NativeDom.RV_DOM_UNION_USV_STRING_OR_URL_PATTERN_INIT_URL_PATTERN_INIT
            input.arm1 <- pointer

        input

    let private inputText (value: NativeDom.RvDomUtf8StringInput) =
        if value.present = 0 then
            None
        else
            let mutable span = NativeDom.RvDomUtf8StringSpan ()
            span.data <- value.data
            span.length <- value.length
            Some (Interop.spanText span)

    let inline private structure<'T when 'T: struct> (pointer: nativeint) (name: string) =
        if pointer = 0n then
            invalidOp $"WebCore returned no {name}"

        Marshal.PtrToStructure<'T> pointer

    let inline private items<'T, 'U when 'T: struct> (items: nativeint) (count: int) ([<InlineIfLambda>] read: 'T -> 'U) =
        if count < 0 || (count > 0 && items = 0n) then
            invalidOp "WebCore returned an invalid URL sequence"

        Array.init count (fun index -> read (Marshal.PtrToStructure<'T>(items + nativeint (index * Marshal.SizeOf<'T>()))))

    let private readInit (pointer: nativeint) =
        let value = structure<NativeDom.RvDomURLPatternInit> pointer "URLPatternInit"
        let init = URLPatternInit ()
        init.protocol <- inputText value.protocol
        init.username <- inputText value.username
        init.password <- inputText value.password
        init.hostname <- inputText value.hostname
        init.port <- inputText value.port
        init.pathname <- inputText value.pathname
        init.search <- inputText value.search
        init.hash <- inputText value.hash
        init.baseURL <- inputText value.base_url
        init

    let private componentMatch (pointer: nativeint) =
        let value =
            structure<NativeDom.RvDomURLPatternComponentResult> pointer "URLPatternComponentResult"

        let groups =
            structure<NativeDom.RvDomSequencePairUSVStringAndUnionUndefinedOrUSVString> value.groups "groups"

        {
            Input = inputText value.input |> Option.defaultValue ""
            Groups =
                items groups.items groups.count (fun (pair: NativeDom.RvDomPairUSVStringAndUnionUndefinedOrUSVString) ->
                    let text =
                        if pair.value.tag = NativeDom.RV_DOM_UNION_UNDEFINED_OR_USV_STRING_STRING then
                            Some (Interop.spanText pair.value.arm1)
                        else
                            None

                    Interop.spanText pair.key, text)
        }

    let private matchResult (pointer: nativeint) =
        let value = structure<NativeDom.RvDomURLPatternResult> pointer "URLPatternResult"

        let inputs =
            structure<NativeDom.RvDomSequenceUSVStringOrURLPatternInit> value.inputs "inputs"

        {
            Inputs =
                items inputs.items inputs.count (fun (input: NativeDom.RvDomUnionUSVStringOrURLPatternInit) ->
                    if input.tag = NativeDom.RV_DOM_UNION_USV_STRING_OR_URL_PATTERN_INIT_STRING then
                        USVStringOrURLPatternInit.USVString (Interop.spanText input.arm0)
                    else
                        USVStringOrURLPatternInit.URLPatternInit (readInit input.arm1))
            Protocol = componentMatch value.protocol
            Username = componentMatch value.username
            Password = componentMatch value.password
            Hostname = componentMatch value.hostname
            Port = componentMatch value.port
            Pathname = componentMatch value.pathname
            Search = componentMatch value.search
            Hash = componentMatch value.hash
        }

    /// <summary>A URL service for <c>host</c>'s current document.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the URL functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : UrlService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "dom:DOMURL"
                && WebCoreHost.Has "dom:URLSearchParams"
                && WebCoreHost.Has "dom:URLPattern"
            )
        then
            raise (NotSupportedException "The native library has no URL functions")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof UrlService))

            host.EnsureActive ()

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle value
            | _ -> invalidArg name $"The {name} belongs to another provider"

        let wrap (handle: nativeint) (kind: int) (factory: Session -> nativeint -> NativeObject) : obj =
            session.Object (handle, kind, unbox<NativeObject>(box document), Func<Session, nativeint, NativeObject> factory)

        let url (handle: nativeint) =
            wrap handle Kind.Url (fun s h -> URLObject (s, h))
            |> unbox<URL>

        let receiver () =
            Interop.handle (box document)

        { new UrlService with
            member _.Create(text, baseUrl) =
                check ()
                let text = bytes (UrlContract.text "url" text)
                let baseText, baseLength = optionalBytes (UrlContract.baseUrl baseUrl)
                let mutable result = 0n

                NativeDom.rv_dom_domurl_create (receiver (), text, text.Length, baseText, baseLength, &result)
                |> Interop.check "URL constructor"

                url result

            member _.Parse(text, baseUrl) =
                check ()
                let text = bytes (UrlContract.text "url" text)
                let baseText, baseLength = optionalBytes (UrlContract.baseUrl baseUrl)
                let mutable result = 0n

                NativeDom.rv_dom_domurl_parse (receiver (), text, text.Length, baseText, baseLength, &result)
                |> Interop.check "URL.parse"

                if result = 0n then None else Some (url result)

            member _.CanParse(text, baseUrl) =
                check ()
                let text = bytes (UrlContract.text "url" text)
                let baseText, baseLength = optionalBytes (UrlContract.baseUrl baseUrl)
                let mutable result = 0

                NativeDom.rv_dom_domurl_can_parse (receiver (), text, text.Length, baseText, baseLength, &result)
                |> Interop.check "URL.canParse"

                result <> 0

            member _.CreateSearchParams init =
                check ()
                use strings = new Strings ()

                let mutable union =
                    NativeDom.RvDomUnionSequenceOfSequenceOfUSVStringOrRecordOfUSVStringAndUSVStringOrUSVString ()

                union.present <- 1

                match UrlContract.searchParamsInit init with
                | UrlSearchParamsInit.Query query ->
                    union.tag <- NativeDom.RV_DOM_UNION_SEQUENCE_OF_SEQUENCE_OF_USV_STRING_OR_RECORD_OF_USV_STRING_AND_USV_STRING_OR_USV_STRING_STRING
                    union.arm2 <- strings.Span query
                | UrlSearchParamsInit.Pairs pairs ->
                    // The record arm carries length-delimited pairs in order, as the sequence of pairs would.
                    let size = Marshal.SizeOf<NativeDom.RvDomPairUSVStringAndUSVString>()
                    let buffer = strings.Allocate (pairs.Length * size)

                    pairs
                    |> Array.iteri (fun index (name, value) ->
                        let mutable pair = NativeDom.RvDomPairUSVStringAndUSVString ()
                        pair.present <- 1
                        pair.key <- strings.Span name
                        pair.value <- strings.Span value
                        Marshal.StructureToPtr<NativeDom.RvDomPairUSVStringAndUSVString>(pair, buffer + nativeint (index * size), false))

                    let mutable record = NativeDom.RvDomSequencePairUSVStringAndUSVString ()
                    record.present <- 1
                    record.count <- pairs.Length
                    record.items <- if pairs.Length = 0 then 0n else buffer

                    union.tag <-
                        NativeDom
                            .RV_DOM_UNION_SEQUENCE_OF_SEQUENCE_OF_USV_STRING_OR_RECORD_OF_USV_STRING_AND_USV_STRING_OR_USV_STRING_SEQUENCE_PAIR_USV_STRING_AND_USV_STRING

                    union.arm1 <- record

                let mutable result = 0n

                NativeDom.rv_dom_url_search_params_create (receiver (), [| union |], &result)
                |> Interop.check "URLSearchParams constructor"

                wrap result Kind.UrlSearchParams (fun s h -> URLSearchParamsObject (s, h))
                |> unbox<URLSearchParams>

            member _.Entries value =
                check ()
                let target = native (box (UrlContract.instance "searchParams" value)) "searchParams"
                let mutable result = NativeDom.RvDomSequencePairUSVStringAndUSVString ()

                try
                    NativeDom.rv_dom_url_search_params_entries (target, receiver (), &result)
                    |> Interop.check "URLSearchParams.entries"

                    if result.present <> 1 then
                        invalidOp "WebCore returned an absent URLSearchParams entry snapshot"

                    items result.items result.count (fun (pair: NativeDom.RvDomPairUSVStringAndUSVString) ->
                        Interop.spanText pair.key, Interop.spanText pair.value)
                finally
                    NativeDom.rv_dom_sequence_pair_usv_string_and_usv_string_release &result

            member _.CreatePattern(input, baseUrl, ignoreCase) =
                check ()
                use strings = new Strings ()
                let input = patternInput strings input
                let mutable options = NativeDom.RvDomURLPatternOptions ()
                options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomURLPatternOptions>())
                options.ignore_case <- if ignoreCase then 1 else 0
                let mutable result = 0n

                match UrlContract.baseUrl baseUrl with
                | Some baseUrl ->
                    let baseText = bytes baseUrl

                    NativeDom.rv_dom_url_pattern_create_3 (receiver (), [| input |], baseText, baseText.Length, [| options |], &result)
                    |> Interop.check "URLPattern constructor"
                | None ->
                    NativeDom.rv_dom_url_pattern_create_2 (receiver (), [| input |], [| options |], &result)
                    |> Interop.check "URLPattern constructor"

                wrap result Kind.UrlPattern (fun s h -> URLPatternObject (s, h))
                |> unbox<URLPattern>

            member _.Exec(pattern, input, baseUrl) =
                check ()
                let target = native (box (UrlContract.instance "pattern" pattern)) "pattern"
                use strings = new Strings ()

                let input =
                    input
                    |> Option.map (patternInput strings)
                    |> Option.toArray

                let baseText, baseLength = optionalBytes (UrlContract.baseUrl baseUrl)
                let mutable result = 0n

                try
                    NativeDom.rv_dom_url_pattern_exec (target, (if input.Length = 0 then null else input), baseText, baseLength, &result)
                    |> Interop.check "URLPattern.exec"

                    if result = 0n then None else Some (matchResult result)
                finally
                    if result <> 0n then
                        NativeDom.rv_dom_url_pattern_result_release result

            member _.Dispose() =
                disposed.Value <- true
        }
