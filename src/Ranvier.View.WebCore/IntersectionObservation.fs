namespace Ranvier.View.WebCore

#nowarn "9"

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Intersection observation in a host's document through <c>rv_host_observe_intersection_entries</c>.</summary>
module IntersectionObservation =
    [<Literal>]
    let private DomException = -3

    /// <summary>An intersection observation service of the document <c>host</c> shows when each observer is
    /// created.</summary>
    /// <remarks>
    /// Callbacks run during <c>WebCoreHost.Frame</c>, in the order each observer gained its first target. Each entry is
    /// copied before its callback runs; an exception the callback raises is added to <c>WebCoreHost.Errors</c> and
    /// leaves other observers delivering. <c>Now</c> is the document's <c>performance.now()</c> in whole
    /// milliseconds. The engine's root bounds include the scroll margin, and with an explicit root the engine clips
    /// by the scroll containers between target and root without it. An observer of a replaced document delivers
    /// nothing. Host disposal disposes the service. Raises NotSupportedException when the native library lacks the
    /// intersection-entries functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : IntersectionObservationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "intersection-entries") then
            raise (NotSupportedException "The native library has no intersection-entries functions")

        let session = host.Session
        // The callbacks stay reachable until host disposal: a callback can release its own observer while it runs.
        let callbacks = List<Native.IntersectionEntriesCallback>()
        let releases = List<obj * (unit -> unit)>()

        let handleOf (name: string) (value: obj) =
            match value with
            | :? NativeObject as native when obj.ReferenceEquals (native.Session, session) -> native.Handle
            | _ -> invalidArg name "The element must be an object of this host"

        let rect x y width height : IntersectionObserverRect =
            {
                X = x
                Y = y
                Width = width
                Height = height
            }

        let copy (entry: Native.IntersectionEntry) : IntersectionObserverEntry =
            let target = session.Node (Native.rv_node_retain entry.target)

            {
                Target = box target :?> Element
                Time = entry.time
                RootBounds =
                    if entry.hasRootBounds = 1 then
                        Some (rect entry.rootX entry.rootY entry.rootWidth entry.rootHeight)
                    else
                        None
                BoundingClientRect = rect entry.boundsX entry.boundsY entry.boundsWidth entry.boundsHeight
                IntersectionRect = rect entry.intersectionX entry.intersectionY entry.intersectionWidth entry.intersectionHeight
                IsIntersecting = entry.isIntersecting = 1
                IntersectionRatio = entry.intersectionRatio
            }

        let copyAll (entries: nativeint) (count: int) =
            let first = NativePtr.ofNativeInt<Native.IntersectionEntry> entries
            Array.init count (fun index -> copy (NativePtr.get first index))

        let now () =
            host.EnsureActive ()
            let mutable value = 0.0

            Native.rv_host_now (host.Handle, &value)
            |> Interop.check "rv_host_now"

            value

        let create (init: IntersectionObserverInit) (deliver: IntersectionObserverEntry array -> unit) : IntersectionObserverHandle =
            host.EnsureActive ()

            let root =
                match init.Root with
                | IntersectionObserverRoot.Implicit -> 0n
                | IntersectionObserverRoot.Document -> handleOf "init" (box host.Document)
                | IntersectionObserverRoot.Element element -> handleOf "init" (box element)

            let callback =
                Native.IntersectionEntriesCallback (fun _ _ entries count ->
                    try
                        deliver (copyAll entries count)
                    with error ->
                        session.ListenerErrors.Add error)

            let rootMargin = Encoding.UTF8.GetBytes init.RootMargin
            let scrollMargin = Encoding.UTF8.GetBytes init.ScrollMargin
            let observer = [| 0n |]

            let status =
                Native.rv_host_observe_intersection_entries (
                    host.Handle,
                    Marshal.GetFunctionPointerForDelegate<Native.IntersectionEntriesCallback> callback,
                    0n,
                    root,
                    rootMargin,
                    rootMargin.Length,
                    scrollMargin,
                    scrollMargin.Length,
                    init.Threshold,
                    init.Threshold.Length,
                    observer
                )

            if status = DomException then
                match Interop.failure "rv_host_observe_intersection_entries" status with
                | WebCoreDomException (name, detail) -> raise (IntersectionObserverException (name, detail))
                | error -> raise error

            Interop.check "rv_host_observe_intersection_entries" status
            let mutable observer = observer[0]
            callbacks.Add callback
            let key = obj ()

            let release () =
                if observer <> 0n then
                    Native.rv_observer_release observer
                    observer <- 0n

                    releases.RemoveAll (fun (owner, _) -> obj.ReferenceEquals (owner, key))
                    |> ignore

            releases.Add ((key, release))

            let active () =
                host.EnsureActive ()

                if observer = 0n then
                    raise (ObjectDisposedException (nameof IntersectionObserver))

            {
                Observe =
                    fun target ->
                        active ()

                        Native.rv_observer_observe (observer, handleOf "target" (box target))
                        |> Interop.check "rv_observer_observe"
                Unobserve =
                    fun target ->
                        active ()

                        Native.rv_observer_unobserve (observer, handleOf "target" (box target))
                        |> Interop.check "rv_observer_unobserve"
                Disconnect =
                    fun () ->
                        active ()

                        Native.rv_observer_disconnect observer
                        |> Interop.check "rv_observer_disconnect"
                TakeRecords =
                    fun () ->
                        active ()
                        let taken = ref [||]

                        let sink =
                            Native.IntersectionEntriesCallback (fun _ _ entries count -> taken.Value <- copyAll entries count)

                        Native.rv_observer_take_records (observer, Marshal.GetFunctionPointerForDelegate<Native.IntersectionEntriesCallback> sink, 0n)
                        |> Interop.check "rv_observer_take_records"

                        GC.KeepAlive sink
                        taken.Value
                Release =
                    fun () ->
                        // Host disposal releases its observers after the host stops accepting calls.
                        try
                            host.EnsureActive ()
                        with :? ObjectDisposedException ->
                            ()

                        release ()
            }

        let service =
            Ranvier.Dom.IntersectionObservation.create { Now = now; Create = create }

        // Observers are freed before rv_host_destroy, as the ABI requires.
        host.Disposed.Add (fun () ->
            service.Dispose ()

            for _, release in releases.ToArray () do
                release ()

            callbacks.Clear ())

        service
