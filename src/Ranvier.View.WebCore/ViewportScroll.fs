namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Viewport scrolling of a native host's document through its Window.</summary>
module ViewportScroll =
    let private behavior (value: ScrollBehavior) =
        match value with
        | ScrollBehavior.Auto -> 0
        | ScrollBehavior.Instant -> 1
        | ScrollBehavior.Smooth -> 2

    /// <summary>A scroll service over the host's current document. Operations run on the host's creating thread.</summary>
    /// <remarks>Every operation raises <see cref="T:System.ObjectDisposedException"/> after the host is disposed.</remarks>
    /// <exception cref="T:System.NotSupportedException">The native library has no dom-window-scrolling functions.</exception>
    let forHost (host: WebCoreHost) : ViewportScrollService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-window-scrolling") then
            Interop.unsupported<unit> "dom-window-scrolling"

        let document () =
            (box host.Document :?> NativeObject).Handle

        let scroll name (call: nativeint * NativeDom.RvDomScrollToOptions[] -> int) (request: ScrollRequest) =
            host.EnsureActive ()

            if isNull (box request) then
                nullArg (nameof request)

            let coordinates = [| defaultArg request.Left 0.0; defaultArg request.Top 0.0 |]

            let pinned = GCHandle.Alloc (coordinates, GCHandleType.Pinned)

            try
                let address = pinned.AddrOfPinnedObject ()
                let mutable options = NativeDom.RvDomScrollToOptions ()
                options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomScrollToOptions>())
                options.behavior <- behavior request.Behavior
                options.left <- if request.Left.IsSome then address else 0n

                options.top <-
                    if request.Top.IsSome then
                        address + nativeint sizeof<float>
                    else
                        0n

                call (document (), [| options |])
                |> Interop.check name
            finally
                pinned.Free ()

        { new ViewportScrollService with
            member _.Position() =
                host.EnsureActive ()
                let mutable x = 0.0
                let mutable y = 0.0

                NativeDom.rv_dom_window_get_scroll_x (document (), &x)
                |> Interop.check "Window.scrollX"

                NativeDom.rv_dom_window_get_scroll_y (document (), &y)
                |> Interop.check "Window.scrollY"

                { X = x; Y = y }

            member _.ScrollTo request =
                scroll "Window.scrollTo" NativeDom.rv_dom_window_scroll_to_1 request

            member _.ScrollBy request =
                scroll "Window.scrollBy" NativeDom.rv_dom_window_scroll_by_1 request
        }
