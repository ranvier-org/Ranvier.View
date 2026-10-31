namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Screen and ScreenOrientation of a native host's document.</summary>
module Screen =
    let private read (host: WebCoreHost) =
        let document = Interop.handle (box host.Document)
        let mutable width = 0
        let mutable height = 0
        let mutable availLeft = 0
        let mutable availTop = 0
        let mutable availWidth = 0
        let mutable availHeight = 0
        let mutable colorDepth = 0u
        let mutable pixelDepth = 0u
        let mutable orientation = 0
        let mutable angle = 0us
        NativeDom.rv_dom_screen_get_width (document, &width) |> Interop.check "Screen.width"
        NativeDom.rv_dom_screen_get_height (document, &height) |> Interop.check "Screen.height"
        NativeDom.rv_dom_screen_get_avail_left (document, &availLeft) |> Interop.check "Screen.availLeft"
        NativeDom.rv_dom_screen_get_avail_top (document, &availTop) |> Interop.check "Screen.availTop"
        NativeDom.rv_dom_screen_get_avail_width (document, &availWidth) |> Interop.check "Screen.availWidth"
        NativeDom.rv_dom_screen_get_avail_height (document, &availHeight) |> Interop.check "Screen.availHeight"
        NativeDom.rv_dom_screen_get_color_depth (document, &colorDepth) |> Interop.check "Screen.colorDepth"
        NativeDom.rv_dom_screen_get_pixel_depth (document, &pixelDepth) |> Interop.check "Screen.pixelDepth"
        NativeDom.rv_dom_screen_orientation_get_type (document, &orientation) |> Interop.check "ScreenOrientation.type"
        NativeDom.rv_dom_screen_orientation_get_angle (document, &angle) |> Interop.check "ScreenOrientation.angle"

        {
            Width = width
            Height = height
            AvailLeft = availLeft
            AvailTop = availTop
            AvailWidth = availWidth
            AvailHeight = availHeight
            ColorDepth = int colorDepth
            PixelDepth = int pixelDepth
            Orientation = enum<ScreenOrientationType> orientation
            Angle = int angle
        }

    /// <summary>A screen service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>
    /// The screen is the one <c>WebCoreHost.SetScreen</c> set, or the viewport until then. A change is delivered after
    /// <c>SetScreen</c> or <c>Resize</c> when the screen differs from the last one delivered. Orientation locking is
    /// Unsupported. Raises NotSupportedException when the native library lacks the screen functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ScreenService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "screen" && WebCoreHost.Has "dom-screen" && WebCoreHost.Has "dom-screen-orientation") then
            raise (NotSupportedException "The native library has no screen functions")

        let disposed = ref false
        let subscriptions = ResizeArray<IDisposable>()

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof ScreenService))

            host.EnsureActive ()

        { new ScreenService with
            member _.Read() =
                check ()
                read host

            member _.Subscribe handler =
                check ()
                let handler = ScreenContract.handler handler
                let last = ref (read host)

                let subscription =
                    host.ScreenMayHaveChanged.Subscribe (fun () ->
                        let next = read host

                        if next <> last.Value then
                            last.Value <- next
                            handler next)

                subscriptions.Add subscription

                { new IDisposable with
                    member _.Dispose() =
                        if subscriptions.Remove subscription then
                            subscription.Dispose ()
                }

            member _.Lock orientation =
                check ()
                ScreenContract.lockType orientation |> ignore
                async { return ScreenContract.unsupportedLock }

            member _.Unlock() = check ()

            member _.Dispose() =
                disposed.Value <- true

                for subscription in List.ofSeq subscriptions do
                    subscription.Dispose ()

                subscriptions.Clear ()
        }
