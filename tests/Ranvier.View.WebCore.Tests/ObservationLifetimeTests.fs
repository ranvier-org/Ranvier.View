module Ranvier.View.WebCore.Tests.ObservationLifetimeTests

open System
open System.Runtime.CompilerServices
open Expecto
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private collect () =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private resizePayload (service: ResizeObservationService) =
    let payload = obj ()
    let weak = WeakReference payload
    let observer = service.Create (fun _ -> GC.KeepAlive payload)
    observer.Dispose ()
    weak, observer

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private intersectionPayload (service: IntersectionObservationService) =
    let payload = obj ()
    let weak = WeakReference payload

    let observer =
        service.Create ((fun _ -> GC.KeepAlive payload), IntersectionObserverInit.Default)

    observer.Dispose ()
    weak, observer

[<Tests>]
let tests =
    testList
        "Observation lifetime"
        [
            hostCase "disposed observers release callbacks while host and observers remain live"
            <| fun host ->
                Expect.isTrue (WebCoreHost.Has "resize-entries") "resize observation available"
                Expect.isTrue (WebCoreHost.Has "intersection-entries") "intersection observation available"
                let resize = ResizeObservation.forHost host
                let intersection = IntersectionObservation.forHost host
                let resized = [| for _ in 1..4 -> resizePayload resize |]
                let intersected = [| for _ in 1..4 -> intersectionPayload intersection |]
                collect ()

                Expect.isTrue
                    (resized
                     |> Array.forall (fun (weak, _) -> not weak.IsAlive))
                    "resize callback payloads released"

                Expect.isTrue
                    (intersected
                     |> Array.forall (fun (weak, _) -> not weak.IsAlive))
                    "intersection callback payloads released"

                resize.Dispose ()
                intersection.Dispose ()
                GC.KeepAlive resized
                GC.KeepAlive intersected
                GC.KeepAlive host

            hostCase "observers may dispose themselves and install replacements during delivery"
            <| fun host ->
                let resize = ResizeObservation.forHost host
                let intersection = IntersectionObservation.forHost host
                let target = host.Document.createElement "div" :?> HTMLElement
                target.style.cssText <- "width:40px;height:40px"
                host.Document.body.appendChild target |> ignore
                let mutable resizeFirst: ResizeObserver option = None
                let mutable resizeNext: ResizeObserver option = None
                let mutable intersectionFirst: IntersectionObserver option = None
                let mutable intersectionNext: IntersectionObserver option = None
                let mutable resizeCalls = 0
                let mutable intersectionCalls = 0
                let mutable resizeReplacementCalls = 0
                let mutable intersectionReplacementCalls = 0

                resizeFirst <-
                    Some (
                        resize.Create (fun _ ->
                            resizeCalls <- resizeCalls + 1
                            resizeFirst.Value.Dispose ()
                            collect ()

                            let next =
                                resize.Create (fun _ -> resizeReplacementCalls <- resizeReplacementCalls + 1)

                            resizeNext <- Some next
                            next.Observe target)
                    )

                intersectionFirst <-
                    Some (
                        intersection.Create (
                            (fun _ ->
                                intersectionCalls <- intersectionCalls + 1
                                intersectionFirst.Value.Dispose ()
                                collect ()

                                let next =
                                    intersection.Create (
                                        (fun _ -> intersectionReplacementCalls <- intersectionReplacementCalls + 1),
                                        IntersectionObserverInit.Default
                                    )

                                intersectionNext <- Some next
                                next.Observe target),
                            IntersectionObserverInit.Default
                        )
                    )

                resizeFirst.Value.Observe target
                intersectionFirst.Value.Observe target

                for _ in 1..8 do
                    host.Frame ()
                    host.Cycle ()

                Expect.equal resizeCalls 1 "resize observer self-disposed once"
                Expect.equal intersectionCalls 1 "intersection observer self-disposed once"
                Expect.isGreaterThan resizeReplacementCalls 0 "replacement resize observer delivers"
                Expect.isGreaterThan intersectionReplacementCalls 0 "replacement intersection observer delivers"

                resizeNext
                |> Option.iter (fun observer -> observer.Dispose ())

                intersectionNext
                |> Option.iter (fun observer -> observer.Dispose ())

                resize.Dispose ()
                intersection.Dispose ()
        ]
