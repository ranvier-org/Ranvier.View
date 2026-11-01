module internal NativeApiConformance.SvgLifecycle

open System
open System.Runtime.CompilerServices
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore
open RanvierWebCoreAbi

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private retained (host: WebCoreHost) =
    let document = host.Document.implementation.createHTMLDocument "svg-retained"

    let root =
        document.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> SVGSVGElement

    let polygon =
        document.createElementNS ("http://www.w3.org/2000/svg", "polygon") :?> SVGPolygonElement

    document.body.appendChild root |> ignore
    root.appendChild polygon |> ignore
    let list = polygon.points
    let item = root.createSVGPoint ()
    item.x <- 22.f
    item.y <- 33.f
    list.appendItem item |> ignore

    let handle (value: obj) =
        (value :?> NativeObject).Handle

    let documentWatch = Native.rv_node_watch (handle document)
    let polygonWatch = Native.rv_node_watch (handle polygon)
    list, documentWatch, polygonWatch

/// <summary>Checks SVG thread/session guards, retained owners and values, host disposal and final native expiry.</summary>
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
    let mutable watches = []

    try
        let root =
            host.Document.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> SVGSVGElement

        host.Document.body.appendChild root |> ignore

        let polygon =
            host.Document.createElementNS ("http://www.w3.org/2000/svg", "polygon") :?> SVGPolygonElement

        root.appendChild polygon |> ignore
        let point = root.createSVGPoint ()
        let matrix = root.createSVGMatrix ()
        point.x <- 7.f
        polygon.points.initialize point |> ignore
        let mutable outcomes = [||]

        let worker =
            Thread (
                ThreadStart (fun () ->
                    outcomes <-
                        [|
                            failure (fun () -> point.x |> ignore)
                            failure (fun () -> point.x <- 99.f)
                            failure (fun () -> matrix.inverse () |> ignore)
                            failure (fun () -> polygon.points.getItem 0u |> ignore)
                            failure (fun () -> polygon.points.clear ())
                            failure (fun () -> root.createSVGPoint () |> ignore)
                        |])
            )

        worker.Start ()
        worker.Join ()
        equal "thread checks ran" 6 outcomes.Length

        for outcome in outcomes do
            equal "wrong thread rejected" "InvalidOperationException" outcome

        equal "wrong thread preserves value" 7.f point.x
        equal "wrong thread preserves list" 1u polygon.points.length

        do
            use foreign = WebCoreHost.Create (32, 32)

            let other =
                foreign.Document.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> SVGSVGElement

            foreign.Document.body.appendChild other |> ignore
            let foreignPoint = other.createSVGPoint ()
            let foreignMatrix = other.createSVGMatrix ()
            let foreignRect = other.createSVGRect ()
            equal "foreign matrix multiply" "ArgumentException" (failure (fun () -> matrix.multiply foreignMatrix |> ignore))
            equal "foreign point matrix" "ArgumentException" (failure (fun () -> point.matrixTransform foreignMatrix |> ignore))
            equal "foreign list append" "ArgumentException" (failure (fun () -> polygon.points.appendItem foreignPoint |> ignore))

            equal
                "foreign rectangle input"
                "ArgumentException"
                (failure (fun () ->
                    root.checkIntersection (polygon, foreignRect)
                    |> ignore))

            equal
                "foreign SVG node input"
                "ArgumentException"
                (failure (fun () ->
                    root.checkIntersection (other, root.createSVGRect ())
                    |> ignore))

            equal "foreign rejection preserves point" 7.f point.x
            equal "foreign rejection preserves list" 1u polygon.points.length

        equal
            "null required matrix"
            "ArgumentNullException"
            (failure (fun () ->
                point.matrixTransform Unchecked.defaultof<_>
                |> ignore))

        equal
            "null required list item"
            "ArgumentNullException"
            (failure (fun () ->
                polygon.points.appendItem Unchecked.defaultof<_>
                |> ignore))

        equal "nullable SVG reference element" 0u (root.getIntersectionList (root.createSVGRect (), Unchecked.defaultof<_>)).length
        let held, documentWatch, polygonWatch = retained host
        watches <- [ documentWatch; polygonWatch ]
        equal "document watch created" true (documentWatch <> 0n)
        equal "owner watch created" true (polygonWatch <> 0n)
        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        GC.Collect ()
        session.Drain ()
        Native.rv_collect_garbage ()
        equal "retained document stays alive" 1 (Native.rv_watch_alive documentWatch)
        equal "retained owner stays alive" 1 (Native.rv_watch_alive polygonWatch)
        equal "retained list length" 1u held.length
        equal "retained point x" 22.f (held.getItem 0u).x
        equal "retained point y" 33.f (held.getItem 0u).y
        let removed = held.removeItem 0u
        removed.x <- 44.f
        equal "removed item remains mutable" 44.f removed.x
        equal "removal updates retained list" 0u held.length
        held.appendItem removed |> ignore
        equal "reattached item identity" true (obj.ReferenceEquals (removed, held.getItem 0u))
        (host :> IDisposable).Dispose()
        equal "point after host disposal" "ObjectDisposedException" (failure (fun () -> point.x |> ignore))
        equal "matrix after host disposal" "ObjectDisposedException" (failure (fun () -> matrix.a |> ignore))
        equal "list after host disposal" "ObjectDisposedException" (failure (fun () -> held.length |> ignore))
        equal "removed value after host disposal" "ObjectDisposedException" (failure (fun () -> removed.x |> ignore))
        equal "factory after host disposal" "ObjectDisposedException" (failure (fun () -> root.createSVGPoint () |> ignore))
        equal "host managed teardown" 0 session.LiveObjects
        Native.rv_run_loop_cycle 100 |> ignore
        Native.rv_release_memory ()
        Native.rv_collect_garbage ()
        equal "document expires after final release" 0 (Native.rv_watch_alive documentWatch)
        equal "owner expires after final release" 0 (Native.rv_watch_alive polygonWatch)
    finally
        (host :> IDisposable).Dispose()

        for watch in watches do
            Native.rv_watch_release watch

    checks
