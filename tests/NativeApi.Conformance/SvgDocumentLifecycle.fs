module internal NativeApiConformance.SvgDocumentLifecycle

open System
open System.Runtime.CompilerServices
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore
open RanvierWebCoreAbi

let private markup =
    "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'><defs><filter><feConvolveMatrix id='c' order='3 5'/></filter></defs><polygon id='p' points='1,2 3,4 5,6'/></svg>"

let private watch (value: obj) =
    Native.rv_node_watch (value :?> NativeObject).Handle

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private replaceAndWatch (host: WebCoreHost) =
    host.LoadSvg markup
    watch host.Document

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private retained (host: WebCoreHost) =
    host.LoadSvg markup
    let document = host.Document
    let polygon = document.getElementById "p" :?> SVGPolygonElement
    let convolve = document.getElementById "c" :?> SVGFEConvolveMatrixElement
    let value = polygon.points.getItem 0u
    value.x <- 17.f
    let boolean = convolve.preserveAlpha
    let integer = convolve.orderX
    (value, boolean, integer), [ watch document; watch polygon; watch convolve ]

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private readRetained (point: SVGPoint, boolean: SVGAnimatedBoolean, integer: SVGAnimatedInteger) =
    point.x, boolean.baseVal, integer.baseVal

let private collect (host: WebCoreHost) =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()
    host.Session.Drain ()
    Native.rv_run_loop_cycle 100 |> ignore
    Native.rv_release_memory ()
    Native.rv_collect_garbage ()

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private exerciseRetainedValues (host: WebCoreHost) (check: string -> bool -> unit) =
    let values, owners = retained host
    host.LoadSvg "<svg xmlns='http://www.w3.org/2000/svg'/>"
    collect host

    for owner in owners do
        check "retained old value keeps its native owner alive" (Native.rv_watch_alive owner = 1)

    check "old point/Boolean/Integer remain usable" (readRetained values = (17.f, false, 3))
    owners

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private exerciseRetainedDocument (host: WebCoreHost) (check: string -> bool -> unit) =
    host.LoadSvg markup
    let document = host.Document
    let owner = watch document
    host.LoadSvg "<svg xmlns='http://www.w3.org/2000/svg'/>"
    collect host
    check "retained old document remains alive" (Native.rv_watch_alive owner = 1)

    check
        "retained old document remains usable"
        (document.contentType = "image/svg+xml"
         && document.getElementById("p").localName = "polygon")

    owner

/// <summary>Checks HTML and repeated SVG document rooting, XML errors, retained values and final owner expiry.</summary>
let run () =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    let failure action =
        try
            action ()
            "accepted"
        with error ->
            error.GetType().Name

    let host = WebCoreHost.Create (64, 64)
    let session = host.Session
    let watches = ResizeArray<nativeint>()

    try
        equal "initial HTML content type" "text/html" host.Document.contentType
        equal "initial HTML namespace" "http://www.w3.org/1999/xhtml" host.Document.documentElement.namespaceURI
        collect host
        equal "HTML-only current document survives collection" "BODY" host.Document.body.tagName
        let htmlWatch = watch host.Document
        watches.Add htmlWatch
        equal "null SVG text rejects" "ArgumentNullException" (failure (fun () -> host.LoadSvg null))
        equal "null load preserves HTML" "text/html" host.Document.contentType
        let mutable workerOutcome = "not run"

        let worker =
            Thread (ThreadStart (fun () -> workerOutcome <- failure (fun () -> host.LoadSvg markup)))

        worker.Start ()
        worker.Join ()
        equal "SVG load requires host thread" "InvalidOperationException" workerOutcome
        equal "thread rejection preserves HTML" "text/html" host.Document.contentType
        let firstWatch = replaceAndWatch host
        watches.Add firstWatch
        equal "SVG content type" "image/svg+xml" host.Document.contentType
        equal "SVG namespace" "http://www.w3.org/2000/svg" host.Document.documentElement.namespaceURI
        equal "SVG document has no HTML body" true (isNull (box host.Document.body))
        let secondWatch = replaceAndWatch host
        watches.Add secondWatch
        collect host
        equal "superseded initial HTML expires" 0 (Native.rv_watch_alive htmlWatch)
        equal "superseded SVG document expires" 0 (Native.rv_watch_alive firstWatch)
        equal "current SVG document survives collection" 1 (Native.rv_watch_alive secondWatch)

        for _ in 1..6 do
            let old = replaceAndWatch host
            watches.Add old
            host.LoadSvg markup
            collect host
            equal "repeated SVG replacement releases old document" 0 (Native.rv_watch_alive old)

        let ownerWatches =
            exerciseRetainedValues host (fun label condition -> equal label true condition)

        watches.AddRange ownerWatches

        let documentWatch =
            exerciseRetainedDocument host (fun label condition -> equal label true condition)

        watches.Add documentWatch
        collect host

        for owner in documentWatch :: ownerWatches do
            equal "old document/value owner expires after caller release" 0 (Native.rv_watch_alive owner)

        host.LoadSvg
            "<svg xmlns='http://www.w3.org/2000/svg'><text id='unicode'>λ✓</text><script>document.documentElement.setAttribute('ran','bad')</script></svg>"

        host.Cycle 20
        equal "SVG Unicode survives XML parsing" "λ✓" (host.Document.getElementById("unicode").textContent)
        equal "SVG scripts remain disabled" false (host.Document.documentElement.hasAttribute "ran")
        host.LoadSvg "<svg xmlns='http://www.w3.org/2000/svg'><g>"
        equal "XML error load completes with a document" true (not (isNull (box host.Document)))
        equal "malformed XML produces parser-error DOM" true (host.Document.getElementsByTagName("parsererror").length > 0u)
        host.LoadSvg markup
        equal "valid XML replaces error document" "svg" host.Document.documentElement.localName
        (host :> IDisposable).Dispose()
        equal "load after disposal rejects" "ObjectDisposedException" (failure (fun () -> host.LoadSvg markup))
        equal "all managed wrappers detach" 0 session.LiveObjects
        Native.rv_run_loop_cycle 100 |> ignore
        Native.rv_release_memory ()
        Native.rv_collect_garbage ()

        for owner in watches do
            equal "watched document/owner expires at final teardown" 0 (Native.rv_watch_alive owner)
    finally
        (host :> IDisposable).Dispose()

        for owner in watches do
            Native.rv_watch_release owner

    checks
