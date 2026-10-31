module CapabilityBrowserProof

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Ranvier.Dom

[<Emit("document")>]
let document: Document = jsNative

[<Emit("window.__capabilityResult = $0")>]
let result (value: obj) : unit = jsNative

[<Emit("JSON.stringify({fields:$0,name:$0?.name,message:$0?.message,stack:$0?.stack,text:String($0)})")>]
let errorDetails (value: exn) : string = jsNative

[<Emit("typeof document.defaultView.ImageDecoder?.isTypeSupported === 'function'")>]
let imageQueryAvailable () : bool = jsNative

[<Emit("document.defaultView.ImageDecoder.isTypeSupported($0)")>]
let rawImageSupport (name: string) : JS.Promise<bool> = jsNative

[<Emit("new Promise(resolve => { const i=new document.defaultView.Image(); i.onload=()=>resolve(i.naturalWidth>0); i.onerror=()=>resolve(false); i.src=$0; })")>]
let htmlImageDecoded (data: string) : JS.Promise<bool> = jsNative

[<Emit("document.defaultView.CSS.supports('font-format('+$0+')')")>]
let rawFontSupport (name: string) : bool = jsNative

[<Emit("(() => { const f=document.createElement('iframe'); document.body.append(f); return f; })()")>]
let iframe () : obj = jsNative

[<Emit("$0.contentDocument")>]
let frameDocument (frame: obj) : Document = jsNative

[<Emit("$0.contentWindow.dispatchEvent(new $0.contentWindow.Event('pagehide'))")>]
let hideFrame (frame: obj) : unit = jsNative

[<Emit("$0.remove()")>]
let removeFrame (frame: obj) : unit = jsNative

let run () =
    task {
        let mutable count = 0

        let expect condition message =
            count <- count + 1

            if not condition then
                failwith message

        use service = BrowserCapabilities.forDocument document
        expect (service.KnownKeys().Length > 30) "Browser query catalogue"

        let unknownKey =
            Capabilities.key CapabilityCategory.PlatformFacility "unrecognized-future"

        let! unknown = service.Observe (unknownKey, CancellationToken.None)

        expect
            (unknown.Exported.State = CapabilityState.Unknown
             && unknown.Implemented.State = CapabilityState.Unknown)
            "Unknown key remains unknown"

        let! webgl =
            service.Observe (Capabilities.key CapabilityCategory.PlatformFacility "webgl", CancellationToken.None)

        expect
            (webgl.Implemented.State = CapabilityState.Unknown
             && webgl.Available.State = CapabilityState.Unknown)
            "Constructor presence does not prove GPU behavior or readiness"

        let! font =
            service.Observe (Capabilities.key CapabilityCategory.FontFormat "woff2", CancellationToken.None)

        expect (font.Value = Some (CapabilityValue.Boolean (rawFontSupport "woff2"))) "Actual CSS font-format query parity"
        expect (font.Available.State = CapabilityState.Unknown) "Font query does not certify actual loading"

        let! image =
            service.Observe (Capabilities.key CapabilityCategory.ImageFormat "image/png", CancellationToken.None)

        if imageQueryAvailable () then
            let! expected =
                rawImageSupport "image/png"
                |> Async.AwaitPromise
                |> Async.StartAsTask

            expect (image.Value = Some (CapabilityValue.Boolean expected)) "Actual realm ImageDecoder query parity"
        else
            expect (image.Implemented.State = CapabilityState.Unknown) "Missing ImageDecoder query leaves image behavior unknown"

        expect (image.Available.State = CapabilityState.Unknown) "Image query does not certify arbitrary decoding/rendering"
        let comparisons = ResizeArray<obj>()

        for mime, data in
            [
                "image/png", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="
                "image/svg+xml",
                "data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20width='1'%20height='1'%3E%3Crect%20width='1'%20height='1'%20fill='red'/%3E%3C/svg%3E"
            ] do
            let! html =
                htmlImageDecoded data
                |> Async.AwaitPromise
                |> Async.StartAsTask

            expect html ("Actual HTML image load succeeds: " + mime)

            let! queried =
                service.Observe (Capabilities.key CapabilityCategory.ImageFormat mime, CancellationToken.None)

            expect (queried.Available.State = CapabilityState.Unknown) "ImageDecoder query remains separate from renderer availability"

            let! raw =
                if imageQueryAvailable () then
                    task {
                        let! value =
                            rawImageSupport mime
                            |> Async.AwaitPromise
                            |> Async.StartAsTask

                        return Some value
                    }
                else
                    Task.FromResult None

            expect (queried.Value = Option.map CapabilityValue.Boolean raw) "Facade preserves precise ImageDecoder query result"

            comparisons.Add (
                box
                    {|
                        mime = mime
                        imageDecoderSupported = raw
                        htmlImageLoaded = html
                    |}
            )

        use cancellation = new CancellationTokenSource ()
        cancellation.Cancel ()

        try
            let! _ = service.Observe (unknownKey, cancellation.Token)
            failwith "Expected pre-cancellation"
        with :? OperationCanceledException ->
            count <- count + 1

        let frame = iframe ()
        use other = BrowserCapabilities.forDocument (frameDocument frame)
        let pending = TaskCompletionSource<CapabilityObservation option>()

        use extension =
            other.Register
                {
                    Id = "pending"
                    KnownKeys = [| unknownKey |]
                    Observe = fun _ _ -> pending.Task
                }

        let beforeHide = other.Observe (unknownKey, CancellationToken.None)
        hideFrame frame

        try
            let! _ = beforeHide
            failwith "Expected pagehide disposal"
        with CapabilityContextException _ ->
            count <- count + 1

        pending.SetResult (Some (Capabilities.unknown unknownKey "pending" "fixture" "iframe" "late"))
        removeFrame frame

        result
            {|
                passed = count
                error = ""
                imageComparisons = comparisons.ToArray ()
            |}
    }

task {
    try
        do! run ()
    with error ->
        result
            {|
                passed = 0
                error = errorDetails error
            |}
}
|> ignore
