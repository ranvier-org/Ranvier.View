module internal NativeApiConformance.Resources

open System
open System.Collections.Generic
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Fixture bytes shared by the native and browser runs.</summary>
type Fixture =
    {
        Png: byte array
        CorruptPng: byte array
        Font: byte array
    }

/// <summary>Checks cache policies, single terminal outcomes, cancellation, image and font readiness and disposal.</summary>
/// <param name="create">Creates a service over a provider.</param>
/// <param name="image">Creates an image element in the service's document with the given source URL.</param>
let run (fixture: Fixture) (create: ResourceProvider -> ResourceService) (image: string -> Element) =
    task {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let requests = Dictionary<string, int>()
        let held = Dictionary<string, Result<ResourceResponse, string> -> unit>()
        let mutable withdrawn = 0
        let mutable counter = 0uy

        let count url =
            match requests.TryGetValue url with
            | true, value -> value
            | _ -> 0

        let provider: ResourceProvider =
            fun request complete ->
                requests[request.Url] <- count request.Url + 1

                let respond bytes kind =
                    complete (Ok { ContentType = kind; Bytes = bytes })

                match request.Url with
                | url when url.EndsWith "/counter" ->
                    counter <- counter + 1uy
                    respond [| counter |] "application/octet-stream"
                | url when url.EndsWith "/twice" ->
                    respond [| 7uy |] "application/octet-stream"
                    complete (Error "second result")
                | url when url.EndsWith "/fail" -> complete (Error "fixture failure")
                | url when url.EndsWith "/hold" -> held[url] <- complete
                | url when url.EndsWith ".png" && url.Contains "corrupt" -> respond fixture.CorruptPng "image/png"
                | url when url.EndsWith ".png" -> respond fixture.Png "image/png"
                | url when url.EndsWith "/broken-font" -> respond [| 1uy; 2uy; 3uy |] "font/ttf"
                | url when url.EndsWith ".ttf" -> respond fixture.Font "font/ttf"
                | _ -> complete (Error "unknown fixture URL")

                { new IDisposable with
                    member _.Dispose() =
                        withdrawn <- withdrawn + 1
                }

        let await (start: (ResourceOutcome<'T> -> unit) -> ResourceOperation<'T>) =
            let terminals = List<ResourceOutcome<'T>>()
            let completion = TaskCompletionSource<ResourceOutcome<'T>>()

            let operation =
                start (fun outcome ->
                    terminals.Add outcome
                    completion.TrySetResult outcome |> ignore)

            operation, terminals, completion.Task

        use service = create provider

        let load url policy =
            fun terminal -> service.Load ({ Url = url; CachePolicy = policy }, terminal)

        let bytes outcome =
            match outcome with
            | ResourceOutcome.Succeeded response -> response.Bytes
            | ResourceOutcome.Failed failure -> failwith $"Unexpected failure %A{failure}"

        let counterUrl = "https://h05.test/counter"
        let op1, t1, first = await (load counterUrl ResourceCachePolicy.UseCache)
        let! first = first
        equal "first load" [| 1uy |] (bytes first)
        (bytes first)[0] <- 99uy
        let op2, _, second = await (load counterUrl ResourceCachePolicy.UseCache)
        let! second = second
        equal "cached copy is unaffected by caller mutation" [| 1uy |] (bytes second)
        equal "UseCache reuses the response" 1 (count counterUrl)
        let _, _, reload = await (load counterUrl ResourceCachePolicy.Reload)
        let! reload = reload
        equal "Reload bypasses and replaces" [| 2uy |] (bytes reload)
        let _, _, noStore = await (load counterUrl ResourceCachePolicy.NoStore)
        let! noStore = noStore
        equal "NoStore bypasses" [| 3uy |] (bytes noStore)
        let _, _, cached = await (load counterUrl ResourceCachePolicy.UseCache)
        let! cached = cached
        equal "NoStore leaves the cache" [| 2uy |] (bytes cached)
        service.ClearCache ()
        let _, _, cleared = await (load counterUrl ResourceCachePolicy.UseCache)
        let! cleared = cleared
        equal "ClearCache releases cached responses" [| 4uy |] (bytes cleared)
        equal "request count" 4 (count counterUrl)
        equal "one terminal" 1 t1.Count
        op1.Dispose ()
        op2.Dispose ()

        let failUrl = "https://h05.test/fail"
        let _, _, failed = await (load failUrl ResourceCachePolicy.UseCache)
        let! failed = failed

        equal
            "provider failure"
            true
            (match failed with
             | ResourceOutcome.Failed (ResourceFailure.LoadFailed _) -> true
             | _ -> false)

        let _, _, retried = await (load failUrl ResourceCachePolicy.UseCache)
        let! _ = retried
        equal "failures are not cached" 2 (count failUrl)

        let opTwice, tTwice, twice =
            await (load "https://h05.test/twice" ResourceCachePolicy.NoStore)

        let! twice = twice
        equal "first of repeated results wins" [| 7uy |] (bytes twice)
        opTwice.Dispose ()
        equal "repeated results give one terminal" 1 tTwice.Count

        let holdUrl = "https://h05.test/hold"
        let opHold, tHold, _ = await (load holdUrl ResourceCachePolicy.NoStore)
        equal "pending snapshot" ResourceState<ResourceResponse>.Pending opHold.Snapshot
        let before = withdrawn
        opHold.Cancel ()
        opHold.Cancel ()
        equal "cancel gives one terminal" [ ResourceOutcome<ResourceResponse>.Failed ResourceFailure.Cancelled ] (List.ofSeq tHold)
        equal "cancel withdraws the provider request" (before + 1) withdrawn
        held[holdUrl](Ok { ContentType = "x"; Bytes = [| 1uy |] })
        opHold.Dispose ()
        equal "late result adds no terminal" 1 tHold.Count

        let imageUrl = "https://h05.test/ok.png"

        let opImage, _, loaded =
            await (fun terminal ->
                service.LoadImage (
                    {
                        Url = imageUrl
                        CachePolicy = ResourceCachePolicy.NoStore
                    },
                    terminal
                ))

        let! loaded = loaded

        let transport =
            match loaded with
            | ResourceOutcome.Succeeded value ->
                equal "image size" (3, 2) (value.Width, value.Height)
                equal "image source URL" imageUrl value.SourceUrl
                equal "distinct transport URL" true (value.TransportUrl <> value.SourceUrl)
                value.TransportUrl
            | ResourceOutcome.Failed failure -> failwith $"LoadImage failed %A{failure}"

        opImage.Dispose ()

        let _, _, corrupt =
            await (fun terminal ->
                service.LoadImage (
                    {
                        Url = "https://h05.test/corrupt.png"
                        CachePolicy = ResourceCachePolicy.NoStore
                    },
                    terminal
                ))

        let! corrupt = corrupt

        equal
            "corrupt image"
            true
            (match corrupt with
             | ResourceOutcome.Failed (ResourceFailure.DecodeFailed _) -> true
             | _ -> false)

        let element = image "https://h05.test/element.png"
        let _, _, decoded = await (fun terminal -> service.DecodeImage (element, terminal))
        let! decoded = decoded

        match decoded with
        | ResourceOutcome.Succeeded value ->
            equal "element decode size" (3, 2) (value.Width, value.Height)
            equal "element source" true (value.SourceUrl.EndsWith "/element.png")
        | ResourceOutcome.Failed failure -> failwith $"DecodeImage failed %A{failure}"

        let afterTransport = image transport

        let _, _, released =
            await (fun terminal -> service.DecodeImage (afterTransport, terminal))

        let! _ = released
        equal "a disposed image releases its transport URL" 1 (count transport)

        let opFont, _, font =
            await (fun terminal ->
                service.LoadFont (
                    "RvAhem",
                    {
                        Url = "https://h05.test/ahem.ttf"
                        CachePolicy = ResourceCachePolicy.NoStore
                    },
                    terminal
                ))

        let! font = font
        equal "font" (ResourceOutcome.Succeeded { Family = "RvAhem" }) font
        opFont.Dispose ()

        let _, _, broken =
            await (fun terminal ->
                service.LoadFont (
                    "RvBroken",
                    {
                        Url = "https://h05.test/broken-font"
                        CachePolicy = ResourceCachePolicy.NoStore
                    },
                    terminal
                ))

        let! broken = broken

        equal
            "broken font"
            true
            (match broken with
             | ResourceOutcome.Failed (ResourceFailure.DecodeFailed _) -> true
             | _ -> false)

        let opPending, tPending, _ =
            await (load "https://h05.test/second/hold" ResourceCachePolicy.NoStore)

        service.Dispose ()

        equal
            "service disposal cancels pending operations"
            [ ResourceOutcome<ResourceResponse>.Failed ResourceFailure.Cancelled ]
            (List.ofSeq tPending)

        opPending.Dispose ()

        let disposedCall =
            try
                service.Load (
                    {
                        Url = counterUrl
                        CachePolicy = ResourceCachePolicy.UseCache
                    },
                    ignore
                )
                |> ignore

                false
            with :? ObjectDisposedException ->
                true

        equal "a disposed service rejects new operations" true disposedCall
        return checks
    }
