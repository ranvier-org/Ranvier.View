namespace Ranvier.View.WebCore

#nowarn "9"

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Resize observation in a host's document through <c>rv_host_observe_resize_entries</c>.</summary>
module ResizeObservation =
    /// <summary>A resize observation service of the document <c>host</c> shows when each observer is created.</summary>
    /// <remarks>
    /// Callbacks run during <c>WebCoreHost.Frame</c>, in the order of each observer's first observation. Each entry is
    /// copied before its callback runs; an exception the callback raises is added to <c>WebCoreHost.Errors</c> and
    /// leaves other observers delivering. The engine has no device-pixel content box (Ranvier.WebCore#146):
    /// <c>Supports DevicePixelContentBox</c> is false and every <c>DevicePixelContentBoxSize</c> is empty. An observer
    /// of a replaced document delivers nothing. Host disposal disposes the service. Raises NotSupportedException when
    /// the native library lacks the resize-entries functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ResizeObservationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "resize-entries") then
            raise (NotSupportedException "The native library has no resize-entries functions")

        let session = host.Session
        // The callbacks stay reachable until host disposal: a callback can release its own observer while it runs.
        let callbacks = List<Native.ResizeEntriesCallback>()
        let releases = List<obj * (unit -> unit)>()

        let handleOf (target: Element) =
            match box target with
            | :? NativeObject as value when obj.ReferenceEquals (value.Session, session) -> value.Handle
            | _ -> invalidArg "target" "The element must be an object of this host"

        let copy (entry: Native.ResizeEntry) : ResizeObserverEntry =
            let target = session.Node (Native.rv_node_retain entry.target)

            {
                Target = box target :?> Element
                ContentRect =
                    {
                        X = entry.contentX
                        Y = entry.contentY
                        Width = entry.contentWidth
                        Height = entry.contentHeight
                    }
                ContentBoxSize =
                    [|
                        {
                            InlineSize = entry.contentInlineSize
                            BlockSize = entry.contentBlockSize
                        }
                    |]
                BorderBoxSize =
                    [|
                        {
                            InlineSize = entry.borderInlineSize
                            BlockSize = entry.borderBlockSize
                        }
                    |]
                DevicePixelContentBoxSize =
                    if entry.hasDevicePixel = 1 then
                        [|
                            {
                                InlineSize = entry.devicePixelInlineSize
                                BlockSize = entry.devicePixelBlockSize
                            }
                        |]
                    else
                        [||]
            }

        let create (deliver: ResizeObserverEntry array -> unit) : ResizeObserverHandle =
            host.EnsureActive ()

            let callback =
                Native.ResizeEntriesCallback (fun _ _ entries count ->
                    try
                        let first = NativePtr.ofNativeInt<Native.ResizeEntry> entries
                        deliver (Array.init count (fun index -> copy (NativePtr.get first index)))
                    with error ->
                        session.ListenerErrors.Add error)

            let mutable observer =
                Native.rv_host_observe_resize_entries (host.Handle, Marshal.GetFunctionPointerForDelegate<Native.ResizeEntriesCallback> callback, 0n)

            if observer = 0n then
                raise (InvalidOperationException "rv_host_observe_resize_entries returned NULL")

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
                    raise (ObjectDisposedException (nameof ResizeObserver))

            {
                Observe =
                    fun target observed ->
                        active ()

                        let code =
                            match observed with
                            | ResizeObserverBox.ContentBox -> 0
                            | ResizeObserverBox.BorderBox -> 1
                            | ResizeObserverBox.DevicePixelContentBox -> 2

                        Native.rv_observer_observe_box (observer, handleOf target, code)
                        |> Interop.check "rv_observer_observe_box"
                Unobserve =
                    fun target ->
                        active ()

                        Native.rv_observer_unobserve (observer, handleOf target)
                        |> Interop.check "rv_observer_unobserve"
                Disconnect =
                    fun () ->
                        active ()

                        Native.rv_observer_disconnect observer
                        |> Interop.check "rv_observer_disconnect"
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
            Ranvier.Dom.ResizeObservation.create
                {
                    Supports =
                        fun observed ->
                            match observed with
                            | ResizeObserverBox.DevicePixelContentBox -> false
                            | _ -> true
                    Create = create
                }

        // Observers are freed before rv_host_destroy, as the ABI requires.
        host.Disposed.Add (fun () ->
            service.Dispose ()

            for _, release in releases.ToArray () do
                release ()

            callbacks.Clear ())

        service
