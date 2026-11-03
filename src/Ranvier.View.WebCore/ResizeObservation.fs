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
    /// Callbacks run during <c>WebCoreHost.Frame</c> in first-observe order. Entries are copied; callback exceptions enter <c>WebCoreHost.Errors</c>.
    /// Device-pixel box support follows the loaded native capability; older runtimes reject that option and return empty device-pixel sizes.
    /// Replacing the document stops its observers delivering. Host disposal disposes the service and its observers.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    /// <exception cref="T:System.NotSupportedException">The native library lacks resize-entry observation.</exception>
    let forHost (host: WebCoreHost) : ResizeObservationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "resize-entries") then
            raise (NotSupportedException "The native library has no resize-entries functions")

        let session = host.Session
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
            let mutable callbackRoot: Native.ResizeEntriesCallback option = None

            let callback =
                Native.ResizeEntriesCallback (fun _ _ entries count ->
                    let inFlight = callbackRoot

                    try
                        try
                            let first = NativePtr.ofNativeInt<Native.ResizeEntry> entries
                            deliver (Array.init count (fun index -> copy (NativePtr.get first index)))
                        with error ->
                            session.ListenerErrors.Add error
                    finally
                        GC.KeepAlive inFlight)

            let mutable observer =
                Native.rv_host_observe_resize_entries (host.Handle, Marshal.GetFunctionPointerForDelegate<Native.ResizeEntriesCallback> callback, 0n)

            if observer = 0n then
                raise (InvalidOperationException "rv_host_observe_resize_entries returned NULL")

            callbackRoot <- Some callback
            callbacks.Add callback

            let key = obj ()

            let release () =
                if observer <> 0n then
                    Native.rv_observer_release observer
                    observer <- 0n

                    callbackRoot
                    |> Option.iter (fun value -> callbacks.Remove value |> ignore)

                    callbackRoot <- None

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
                            | ResizeObserverBox.DevicePixelContentBox -> WebCoreHost.Has "resize-device-pixel-content-box"
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
