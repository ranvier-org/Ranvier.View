namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>A native host's resource service over the host's resource handler, image decode and font readiness.</summary>
[<RequireQualifiedAccess>]
module Resources =
    let private outcomeFailure (outcome: int) (kind: string) =
        match outcome with
        | 1 -> ResourceFailure.LoadFailed (kind + " load failed")
        | 2 -> ResourceFailure.DecodeFailed (kind + " decode failed")
        | 3 -> ResourceFailure.Cancelled
        | _ -> ResourceFailure.Invalidated (kind + " source, target or document changed")

    let private copy (response: ResourceResponse) =
        {
            ContentType = response.ContentType
            Bytes = Array.copy response.Bytes
        }

    let private validate (request: ResourceRequest) =
        if isNull (box request) then
            nullArg "request"

        match Uri.TryCreate (request.Url, UriKind.Absolute) with
        | true, uri when
            uri.Scheme = "http"
            || uri.Scheme = "https"
            || uri.Scheme = "data"
            ->
            ()
        | _ -> invalidArg "request" "Expected an absolute http, https or data URL."

    /// <summary>Completions from native operations, keyed by the context value passed to the host.</summary>
    type private NativeCompletions() =
        let gate = obj ()
        let handlers = Dictionary<int64, int -> int -> int -> unit>()
        let mutable next = 0L

        let callback =
            Native.ResourceCompletion (fun context outcome width height ->
                let handler =
                    lock gate (fun () ->
                        match handlers.TryGetValue (int64 context) with
                        | true, handler ->
                            handlers.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome width height))

        member _.Pointer =
            Marshal.GetFunctionPointerForDelegate<Native.ResourceCompletion> callback

        member _.Add(handler) =
            lock gate (fun () ->
                next <- next + 1L
                handlers[next] <- handler
                next)

        member _.Remove(key: int64) =
            lock gate (fun () -> handlers.Remove key |> ignore)

        member _.Keep() =
            GC.KeepAlive callback

    let private completions = NativeCompletions ()

    /// <summary>Creates a service whose terminal callbacks run on the host's owner context.</summary>
    /// <remarks>The service installs the host's resource handler; disposing it, or the host, removes the handler,
    /// fails the loads it still holds, clears its cache and transport table and releases every native operation.</remarks>
    let forHost (host: WebCoreHost) (provider: ResourceProvider) : ResourceService =
        if isNull (box host) then
            nullArg "host"

        if isNull (box provider) then
            nullArg "provider"

        host.EnsureActive ()

        if
            WebCoreHost.Has "image-decode" <> true
            || WebCoreHost.Has "font-load" <> true
        then
            raise (NotSupportedException "The native host lacks image-decode or font-load readiness (ABI 30).")

        let cache = Dictionary<string, ResourceResponse>(StringComparer.Ordinal)
        let transport = Dictionary<string, ResourceResponse>(StringComparer.Ordinal)
        let heldLoads = HashSet<nativeint>()
        let live = HashSet<IDisposable>(HashIdentity.Reference)
        let mutable transportNext = 0L
        let mutable disposed = false

        let ensure () =
            if disposed then
                raise (ObjectDisposedException "ResourceService")

            host.EnsureActive ()

        let complete (load: nativeint) (result: Result<ResourceResponse, string>) =
            if heldLoads.Remove load then
                match result with
                | Ok response ->
                    Native.rv_resource_load_finish (load, response.Bytes, response.Bytes.Length, response.ContentType)
                    |> ignore
                | Error _ -> Native.rv_resource_load_fail load |> ignore

        let handler =
            Native.ResourceHandler (fun _ load url ->
                let url = Marshal.PtrToStringUTF8 url
                heldLoads.Add load |> ignore

                match transport.TryGetValue url with
                | true, response -> complete load (Ok response)
                | _ ->
                    let mutable first = 0

                    provider
                        {
                            Url = url
                            CachePolicy = ResourceCachePolicy.UseCache
                        }
                        (fun result ->
                            if Interlocked.Exchange (&first, 1) = 0 then
                                host.PostToOwner (fun () -> complete load result))
                    |> ignore)

        Native.rv_host_set_resource_handler (host.Handle, Marshal.GetFunctionPointerForDelegate<Native.ResourceHandler> handler, 0n)
        |> Interop.check "rv_host_set_resource_handler"

        let track (arbiter: ResourceArbiter<'T>) =
            live.Add arbiter |> ignore
            arbiter

        let untrack (arbiter: IDisposable) =
            live.Remove arbiter |> ignore

        /// Loads bytes through the cache and provider, then continues on the owner context.
        let loadBytes (request: ResourceRequest) (onResult: Result<ResourceResponse, string> -> unit) : IDisposable =
            match request.CachePolicy, cache.TryGetValue request.Url with
            | ResourceCachePolicy.UseCache, (true, cached) ->
                let response = copy cached
                host.PostToOwner (fun () -> onResult (Ok response))

                { new IDisposable with
                    member _.Dispose() = ()
                }
            | _ ->
                let mutable first = 0

                provider request (fun result ->
                    if Interlocked.Exchange (&first, 1) = 0 then
                        let result = result |> Result.map copy

                        host.PostToOwner (fun () ->
                            match result with
                            | Ok response when
                                request.CachePolicy <> ResourceCachePolicy.NoStore
                                && not disposed
                                ->
                                cache[request.Url] <- copy response
                            | _ -> ()

                            onResult result))

        /// Starts a native operation and settles the arbiter from its completion.
        let native (arbiter: ResourceArbiter<'T>) (start: nativeint -> nativeint -> struct (int * uint64)) (value: int -> int -> 'T) (kind: string) =
            let key =
                completions.Add (fun outcome width height ->
                    if outcome = 0 then
                        arbiter.Settle (ResourceOutcome.Succeeded (value width height))
                        |> ignore
                    else
                        arbiter.Settle (ResourceOutcome.Failed (outcomeFailure outcome kind))
                        |> ignore)

            let struct (status, operation) = start completions.Pointer (nativeint key)

            if status <> 0 then
                completions.Remove key

                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed $"{kind} start returned {status}"))
                |> ignore

            operation

        let releaseNative (operation: uint64) =
            if operation <> 0UL then
                Native.rv_resource_release operation |> ignore

        let createArbiter terminal withdraw release =
            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<_>>

            arbiter <-
                new ResourceArbiter<_> (
                    host.PostToOwner,
                    (fun outcome ->
                        untrack arbiter
                        terminal outcome),
                    withdraw,
                    release
                )

            track arbiter

        let mutable service = Unchecked.defaultof<ResourceService>

        let disposeAll () =
            if not disposed then
                disposed <- true

                for operation in List.ofSeq live do
                    operation.Dispose ()

                live.Clear ()

                for load in List.ofSeq heldLoads do
                    complete load (Error "resource service disposed")

                Native.rv_host_set_resource_handler (host.Handle, 0n, 0n)
                |> ignore

                cache.Clear ()
                transport.Clear ()
                GC.KeepAlive handler

        let hostDisposal = host.Disposed.Subscribe (fun () -> disposeAll ())

        service <-
            { new ResourceService with
                member _.Load(request, terminal) =
                    ensure ()
                    validate request
                    let mutable pending: IDisposable = null
                    let mutable arbiter = Unchecked.defaultof<ResourceArbiter<ResourceResponse>>

                    arbiter <-
                        createArbiter
                            terminal
                            (fun () ->
                                if not (isNull pending) then
                                    pending.Dispose ())
                            ignore

                    pending <-
                        loadBytes request (fun result ->
                            match result with
                            | Ok response ->
                                arbiter.Settle (ResourceOutcome.Succeeded response)
                                |> ignore
                            | Error message ->
                                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed message))
                                |> ignore)

                    arbiter :> ResourceOperation<_>

                member _.LoadImage(request, terminal) =
                    ensure ()
                    validate request
                    let mutable pending: IDisposable = null
                    let mutable operation = 0UL
                    let mutable image = 0n
                    let mutable url = null

                    let clean () =
                        releaseNative operation
                        operation <- 0UL

                        if image <> 0n then
                            Native.rv_node_release image
                            image <- 0n

                        if not (isNull url) then
                            transport.Remove url |> ignore

                    let mutable arbiter = Unchecked.defaultof<ResourceArbiter<ResourceImage>>

                    arbiter <-
                        createArbiter
                            (fun outcome ->
                                match outcome with
                                | ResourceOutcome.Failed _ -> clean ()
                                | ResourceOutcome.Succeeded _ -> ()

                                terminal outcome)
                            (fun () ->
                                if not (isNull pending) then
                                    pending.Dispose ()

                                if operation <> 0UL then
                                    Native.rv_resource_cancel operation |> ignore)
                            (fun _ -> clean ())

                    pending <-
                        loadBytes request (fun result ->
                            match result with
                            | Error message ->
                                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed message))
                                |> ignore
                            | Ok _ when
                                arbiter.Snapshot <> ResourceState.Pending
                                || disposed
                                ->
                                ()
                            | Ok response ->
                                transportNext <- transportNext + 1L
                                url <- $"https://ranvier-resource.invalid/{transportNext}"
                                transport[url] <- response
                                image <- Native.rv_document_create_element (host.Handle, "img")

                                Native.rv_element_set_attribute (image, "src", Native.utf8 url, Native.utf8Length url)
                                |> Interop.check "rv_element_set_attribute"

                                let transportUrl = url

                                operation <-
                                    native
                                        arbiter
                                        (fun pointer context ->
                                            let mutable token = 0UL
                                            let status = Native.rv_image_decode (host.Handle, image, pointer, context, &token)
                                            struct (status, token))
                                        (fun width height ->
                                            {
                                                Width = width
                                                Height = height
                                                SourceUrl = request.Url
                                                TransportUrl = transportUrl
                                            })
                                        "image")

                    arbiter :> ResourceOperation<_>

                member _.DecodeImage(target, terminal) =
                    ensure ()

                    let element =
                        match box target with
                        | :? NativeObject as element when element.Handle <> 0n -> element
                        | _ -> invalidArg "target" "Expected an element of this host."

                    let mutable operation = 0UL
                    let mutable arbiter = Unchecked.defaultof<ResourceArbiter<ResourceImageInfo>>

                    arbiter <-
                        createArbiter
                            (fun outcome ->
                                releaseNative operation
                                operation <- 0UL
                                terminal outcome)
                            (fun () ->
                                if operation <> 0UL then
                                    Native.rv_resource_cancel operation |> ignore)
                            ignore

                    let source () =
                        let current =
                            Interop.text "HTMLImageElement.currentSrc" (fun buffer capacity length ->
                                NativeDom.rv_dom_html_image_element_get_current_src (element.Handle, buffer, capacity, length))

                        current

                    let sourceUrl () =
                        try
                            source ()
                        with _ ->
                            Interop.text "Element.getAttribute" (fun buffer capacity length ->
                                Native.rv_element_get_attribute (element.Handle, "href", buffer, capacity, length))

                    operation <-
                        native
                            arbiter
                            (fun pointer context ->
                                let mutable token = 0UL

                                let status =
                                    Native.rv_image_decode (host.Handle, element.Handle, pointer, context, &token)

                                struct (status, token))
                            (fun width height ->
                                {
                                    Width = width
                                    Height = height
                                    SourceUrl = sourceUrl ()
                                })
                            "image"

                    arbiter :> ResourceOperation<_>

                member _.LoadFont(family, request, terminal) =
                    ensure ()
                    validate request

                    if String.IsNullOrEmpty family then
                        invalidArg "family" "Expected a nonempty family name."

                    let mutable pending: IDisposable = null
                    let mutable operation = 0UL

                    let clean () =
                        releaseNative operation
                        operation <- 0UL

                    let mutable arbiter = Unchecked.defaultof<ResourceArbiter<ResourceFont>>

                    arbiter <-
                        createArbiter
                            (fun outcome ->
                                match outcome with
                                | ResourceOutcome.Failed _ -> clean ()
                                | ResourceOutcome.Succeeded _ -> ()

                                terminal outcome)
                            (fun () ->
                                if not (isNull pending) then
                                    pending.Dispose ()

                                if operation <> 0UL then
                                    Native.rv_resource_cancel operation |> ignore)
                            (fun _ -> clean ())

                    pending <-
                        loadBytes request (fun result ->
                            match result with
                            | Error message ->
                                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed message))
                                |> ignore
                            | Ok _ when
                                arbiter.Snapshot <> ResourceState.Pending
                                || disposed
                                ->
                                ()
                            | Ok response ->
                                operation <-
                                    native
                                        arbiter
                                        (fun pointer context ->
                                            let mutable token = 0UL

                                            let status =
                                                Native.rv_font_load (
                                                    host.Handle,
                                                    family,
                                                    response.Bytes,
                                                    response.Bytes.Length,
                                                    pointer,
                                                    context,
                                                    &token
                                                )

                                            struct (status, token))
                                        (fun _ _ -> { Family = family })
                                        "font")

                    arbiter :> ResourceOperation<_>

                member _.ClearCache() =
                    cache.Clear ()

                member _.Dispose() =
                    hostDisposal.Dispose ()
                    disposeAll ()
            }

        completions.Keep ()
        service
