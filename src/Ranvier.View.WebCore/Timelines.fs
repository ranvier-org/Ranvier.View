namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for native scroll-driven animation timelines.</summary>
module Timelines =
    let private axis (value: ScrollAxis) =
        match value with
        | ScrollAxis.Block -> 0
        | ScrollAxis.Inline -> 1
        | ScrollAxis.X -> 2
        | ScrollAxis.Y -> 3

    /// <summary>The animation timeline constructors of a native document.</summary>
    let forDocument (document: Document) : TimelineService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        if not (WebCoreHost.Has "dom-constructor:ScrollTimeline") then
            Interop.unsupported<unit> "dom-constructor:ScrollTimeline"

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A timeline service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "TimelineService")

            Interop.handle (box owner)

        let engine label (call: unit -> int) =
            try
                call () |> Interop.check label
            with WebCoreDomException(name, detail) ->
                raise (AnimationException(name, detail))

        // source: None leaves the options' source pointer NULL (the scrolling element); Some 0n points it at NULL.
        let scroll (source: nativeint option) (direction: ScrollAxis) =
            let documentHandle = active ()
            let cell = Marshal.AllocHGlobal IntPtr.Size

            try
                let mutable options = NativeDom.RvDomScrollTimelineOptions ()
                options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomScrollTimelineOptions>())
                options.axis <- axis direction

                match source with
                | Some handle ->
                    Marshal.WriteIntPtr (cell, handle)
                    options.source <- cell
                | None -> options.source <- 0n

                let mutable handle = 0n

                engine "ScrollTimeline constructor" (fun () ->
                    NativeDom.rv_dom_scroll_timeline_create (documentHandle, [| options |], &handle))

                session.Object (handle, Kind.AnimationTimeline, owner, Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationTimeline (s, h)))
                |> unbox<ScrollTimeline>
            finally
                Marshal.FreeHGlobal cell

        { new TimelineService with
            member _.CreateScrollTimeline(?source, ?axis) =
                let source =
                    match source with
                    | Some element when not (isNull (box element)) -> Some (Interop.handle (box element))
                    | _ -> None

                scroll source (defaultArg axis ScrollAxis.Block)

            member _.CreateScrollTimelineWithoutSource ?axis = scroll (Some 0n) (defaultArg axis ScrollAxis.Block)

          interface IDisposable with
              member _.Dispose() = disposed <- true }

    /// <summary>Creates a borrowed timeline provider bound to the live host's document.</summary>
    let forHost (host: WebCoreHost) : TimelineService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
