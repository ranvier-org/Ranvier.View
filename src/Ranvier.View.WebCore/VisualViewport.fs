namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>The visual viewport of a native host's document, with the host's page scale and obscured insets.</summary>
module VisualViewport =
    [<Literal>]
    let private Detached = -10

    [<Literal>]
    let private Unsupported = -9

    let private input (name: string) (status: int) =
        match status with
        | 0 -> Ok ()
        | Unsupported -> Error (VisualViewportUnavailable.Unsupported (name + " rejected the value"))
        | other -> raise (Interop.failure name other)

    /// <summary>The provider over the visual viewport of <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Operations run on the host's creating thread. Reads report <c>Detached</c> once the document's window has no
    /// frame, as after the host loads another page, and after the host is disposed.
    /// </remarks>
    /// <exception cref="T:System.NotSupportedException">The native library has no visual-viewport functions.</exception>
    let provider (host: WebCoreHost) : VisualViewportProvider =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "visual-viewport") then
            Interop.unsupported<unit> "visual-viewport"

        let document = box host.Document :?> NativeObject
        let mutable handle = 0n

        NativeDom.rv_dom_window_get_visual_viewport (document.Handle, &handle)
        |> Interop.check "Window.visualViewport"

        let viewport =
            host.Session.Object (handle, Kind.VisualViewport, document, Func<_, _, _>(fun s h -> VisualViewportObject (s, h))) :?> NativeObject

        let read () =
            if viewport.Handle = 0n then
                Error VisualViewportUnavailable.Detached
            else
                host.EnsureActive ()
                let values = Array.zeroCreate<float> 7

                let statuses =
                    [|
                        NativeDom.rv_dom_visual_viewport_get_offset_left (viewport.Handle, &values[0])
                        NativeDom.rv_dom_visual_viewport_get_offset_top (viewport.Handle, &values[1])
                        NativeDom.rv_dom_visual_viewport_get_page_left (viewport.Handle, &values[2])
                        NativeDom.rv_dom_visual_viewport_get_page_top (viewport.Handle, &values[3])
                        NativeDom.rv_dom_visual_viewport_get_width (viewport.Handle, &values[4])
                        NativeDom.rv_dom_visual_viewport_get_height (viewport.Handle, &values[5])
                        NativeDom.rv_dom_visual_viewport_get_scale (viewport.Handle, &values[6])
                    |]

                if Array.contains Detached statuses then
                    Error VisualViewportUnavailable.Detached
                else
                    statuses
                    |> Array.iter (Interop.check "VisualViewport")

                    Ok
                        {
                            OffsetLeft = values[0]
                            OffsetTop = values[1]
                            PageLeft = values[2]
                            PageTop = values[3]
                            Width = values[4]
                            Height = values[5]
                            Scale = values[6]
                        }

        {
            Viewport = unbox<Ranvier.Dom.VisualViewport>(box viewport)
            Read = read
            SetPageScale =
                fun (scale, originX, originY) ->
                    host.EnsureActive ()
                    input "rv_host_set_page_scale" (Native.rv_host_set_page_scale (host.Session.Host, scale, originX, originY))
            SetObscuredInsets =
                fun insets ->
                    host.EnsureActive ()

                    input
                        "rv_host_set_obscured_insets"
                        (Native.rv_host_set_obscured_insets (host.Session.Host, insets.Top, insets.Right, insets.Bottom, insets.Left))
        }

    /// <summary>A visual viewport service for <c>host</c>'s current document.</summary>
    let forHost (host: WebCoreHost) : VisualViewportService =
        VisualViewports.create (provider host)
