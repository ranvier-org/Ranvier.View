namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvWindowMetrics</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal WindowMetrics =
    val mutable Size: int
    val mutable InnerWidth: int
    val mutable InnerHeight: int
    val mutable OuterWidth: int
    val mutable OuterHeight: int
    val mutable DevicePixelRatio: float

/// <summary>Viewport dimensions, visibility and change delivery of a native host.</summary>
module Viewport =

    let private require (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "viewport") then
            raise (NotSupportedException "The native library has no viewport functions")

    let private metrics (host: WebCoreHost) () =
        host.EnsureActive ()
        let mutable value = WindowMetrics ()
        value.Size <- sizeof<WindowMetrics>

        Native.rv_host_window_metrics (host.Session.Host, NativePtr.toNativeInt &&value)
        |> Interop.check "rv_host_window_metrics"

        {
            InnerWidth = value.InnerWidth
            InnerHeight = value.InnerHeight
            OuterWidth = value.OuterWidth
            OuterHeight = value.OuterHeight
            DevicePixelRatio = value.DevicePixelRatio
        }

    let private matchMedia (host: WebCoreHost) (query: string) : ViewportMediaQuery =
        host.EnsureActive ()

        if isNull query then
            nullArg (nameof query)

        let handle = Native.rv_host_match_media (host.Session.Host, query)

        if handle = 0n then
            raise (InvalidOperationException "rv_host_match_media returned NULL")

        let target = host.Session.Target handle

        { new ViewportMediaQuery with
            member _.Media =
                host.EnsureActive ()
                let length = [| 0 |]

                Native.rv_media_query_media (target.Handle, null, 0, length)
                |> ignore

                let buffer = Array.zeroCreate<byte> length[0]

                Native.rv_media_query_media (target.Handle, buffer, buffer.Length, length)
                |> Interop.check "MediaQueryList.media"

                Text.Encoding.UTF8.GetString (buffer, 0, length[0])

            member _.Matches =
                host.EnsureActive ()
                let mutable matches = 0

                Native.rv_media_query_matches (target.Handle, &matches)
                |> Interop.check "MediaQueryList.matches"

                matches = 1

            member _.Target = unbox<EventTarget> target
            member _.Dispose() = ()
        }

    /// <summary>The provider over <c>host</c>'s window, which the host keeps across document replacement.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the "viewport" functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let provider (host: WebCoreHost) : ViewportProvider =
        require host
        let handle = Native.rv_host_window_event_target host.Session.Host

        if handle = 0n then
            raise (InvalidOperationException "rv_host_window_event_target returned NULL")

        {
            Window = unbox<EventTarget>(host.Session.Target handle)
            Document = host.Document
            Metrics = metrics host
            MatchMedia = matchMedia host
        }

    /// <summary>A viewport service for <c>host</c>'s current document.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ViewportService =
        Ranvier.Dom.Viewport.create (provider host)
