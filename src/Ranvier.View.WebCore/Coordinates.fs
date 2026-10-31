namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Coordinate conversion in a host's document through <c>rv_node_convert_points</c> and
/// <c>rv_node_box_quad</c>.</summary>
module Coordinates =
    [<Literal>]
    let private Unsupported = -9

    [<Literal>]
    let private Detached = -10

    [<Literal>]
    let private WrongHost = -12

    let private noFrames = "The native host has no subframe documents."

    /// <summary>A coordinate service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>
    /// <c>Host</c> is the space of the host's mouse and wheel input and dirty rectangles; while page-scaled it is the
    /// client point times the page scale. <c>Device</c> is the paint image's pixels. An element of another host reads
    /// <c>WrongDocument</c>; one removed from the document, of a replaced document or of a disposed host reads
    /// <c>Detached</c>. The engine maps and paints perspective and preserve-3d chains in its software path, and the
    /// conversion follows that paint. Raises NotSupportedException when the native library lacks the
    /// coordinate-spaces functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : CoordinateService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "coordinate-spaces") then
            raise (NotSupportedException "The native library has no coordinate-spaces functions")

        let handleOf (element: Element) =
            match box element with
            | :? NativeObject as value -> value.Handle
            | _ -> invalidArg "element" "The element must be an object of the native backend"

        // The anchor node and RV_SPACE_* value of a space; a zero anchor is an element of a disposed host.
        let anchor (space: CoordinateSpace) =
            match space with
            | CoordinateSpace.Element element -> handleOf element, 0
            | CoordinateSpace.Page -> Interop.handle host.Document, 1
            | CoordinateSpace.Client -> Interop.handle host.Document, 2
            | CoordinateSpace.Host -> Interop.handle host.Document, 3
            | CoordinateSpace.Device -> Interop.handle host.Document, 4

        let rendered (element: Element) =
            let quad = Array.zeroCreate<float> 8

            Native.rv_node_box_quad (handleOf element, 0, 0n, 1, &quad[0])
            <> Unsupported

        // RV_ERR_UNSUPPORTED covers an element space without a CSS box and a singular inverse map.
        let failure name (from: CoordinateSpace) (target: CoordinateSpace) status =
            match status with
            | Detached -> CoordinateUnavailable.Detached
            | WrongHost -> CoordinateUnavailable.WrongDocument
            | Unsupported ->
                match from, target with
                | CoordinateSpace.Element element, _ when not (rendered element) -> CoordinateUnavailable.NotRendered
                | _, CoordinateSpace.Element element when not (rendered element) -> CoordinateUnavailable.NotRendered
                | _, CoordinateSpace.Element _ -> CoordinateUnavailable.NonInvertible
                | _ -> CoordinateUnavailable.Unsupported noFrames
            | status ->
                Interop.check name status
                CoordinateUnavailable.Unsupported name

        let convert (from: CoordinateSpace) (target: CoordinateSpace) (points: CoordinatePoint array) =
            host.EnsureActive ()
            let source, fromSpace = anchor from
            let destination, toSpace = anchor target

            if source = 0n || destination = 0n then
                Error CoordinateUnavailable.Detached
            else
                let xy = Array.zeroCreate<float>(2 * points.Length)

                for index in 0 .. points.Length - 1 do
                    xy[2 * index] <- points[index].X
                    xy[2 * index + 1] <- points[index].Y

                match Native.rv_node_convert_points (source, fromSpace, destination, toSpace, &xy[0], points.Length) with
                | 0 ->
                    Ok (
                        Array.init points.Length (fun index ->
                            ({
                                X = xy[2 * index]
                                Y = xy[2 * index + 1]
                            }
                            : CoordinatePoint))
                    )
                | status -> Error (failure "rv_node_convert_points" from target status)

        Ranvier.Dom.Coordinates.create
            {
                Convert = fun (from, target, points) -> convert from target points
                BoxQuad =
                    fun (element, cssBox, target) ->
                        host.EnsureActive ()
                        let node = handleOf element
                        let destination, toSpace = anchor target

                        if node = 0n || destination = 0n then
                            Error CoordinateUnavailable.Detached
                        else
                            let quad = Array.zeroCreate<float> 8

                            let edge =
                                match cssBox with
                                | CoordinateBox.Border -> 0
                                | CoordinateBox.Padding -> 1
                                | CoordinateBox.Content -> 2
                                | CoordinateBox.Margin -> 3

                            match Native.rv_node_box_quad (node, edge, destination, toSpace, &quad[0]) with
                            | 0 ->
                                let point index : CoordinatePoint =
                                    {
                                        X = quad[2 * index]
                                        Y = quad[2 * index + 1]
                                    }

                                Ok
                                    {
                                        P1 = point 0
                                        P2 = point 1
                                        P3 = point 2
                                        P4 = point 3
                                    }
                            | status -> Error (failure "rv_node_box_quad" (CoordinateSpace.Element element) target status)
            }
