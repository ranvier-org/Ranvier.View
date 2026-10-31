namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Screen and ScreenOrientation of a native host's document.</summary>
module Screen =
    let private read (host: WebCoreHost) =
        let document = Interop.handle (box host.Document)

        let int32 name call =
            let mutable value = 0
            call (document, &value) |> Interop.check name
            value

        let uint32 name call =
            let mutable value = 0u
            call (document, &value) |> Interop.check name
            int value

        let mutable orientation = 0
        NativeDom.rv_dom_screen_orientation_get_type (document, &orientation)
        |> Interop.check "ScreenOrientation.type"

        let mutable angle = 0us
        NativeDom.rv_dom_screen_orientation_get_angle (document, &angle)
        |> Interop.check "ScreenOrientation.angle"

        {
            Width = int32 "Screen.width" NativeDom.rv_dom_screen_get_width
            Height = int32 "Screen.height" NativeDom.rv_dom_screen_get_height
            AvailLeft = int32 "Screen.availLeft" NativeDom.rv_dom_screen_get_avail_left
            AvailTop = int32 "Screen.availTop" NativeDom.rv_dom_screen_get_avail_top
            AvailWidth = int32 "Screen.availWidth" NativeDom.rv_dom_screen_get_avail_width
            AvailHeight = int32 "Screen.availHeight" NativeDom.rv_dom_screen_get_avail_height
            ColorDepth = uint32 "Screen.colorDepth" NativeDom.rv_dom_screen_get_color_depth
            PixelDepth = uint32 "Screen.pixelDepth" NativeDom.rv_dom_screen_get_pixel_depth
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
