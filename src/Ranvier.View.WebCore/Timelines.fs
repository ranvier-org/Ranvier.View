namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for native scroll-driven and view-driven animation timelines.</summary>
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

        for feature in [ "dom-constructor:ScrollTimeline"; "dom-constructor:ViewTimeline" ] do
            if not (WebCoreHost.Has feature) then
                Interop.unsupported<unit> feature

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
            with WebCoreDomException (name, detail) ->
                raise (AnimationException (name, detail))

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

                engine "ScrollTimeline constructor" (fun () -> NativeDom.rv_dom_scroll_timeline_create (documentHandle, [| options |], &handle))

                session.Object (
                    handle,
                    Kind.AnimationTimeline,
                    owner,
                    Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationTimeline (s, h))
                )
                |> unbox<ScrollTimeline>
            finally
                Marshal.FreeHGlobal cell

        let view
            (subject: Element)
            (direction: ScrollAxis)
            (inset: InputScope -> NativeDom.RvDomUnionDOMStringOrSequenceOfCSSNumericValueOrCSSOMKeywordish)
            =
            let documentHandle = active ()
            use scope = new InputScope ()
            let mutable options = NativeDom.RvDomViewTimelineOptions ()
            options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomViewTimelineOptions>())

            options.subject <-
                if isNull (box subject) then
                    0n
                else
                    Interop.handle (box subject)

            options.axis <- axis direction
            options.inset <- scope.Pin [| inset scope |]
            let mutable handle = 0n

            engine "ViewTimeline constructor" (fun () -> NativeDom.rv_dom_view_timeline_create (documentHandle, [| options |], &handle))

            session.Object (
                handle,
                Kind.AnimationTimeline,
                owner,
                Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationTimeline (s, h))
            )
            |> unbox<ViewTimeline>

        let item (scope: InputScope) (value: CSSNumericValueOrDOMStringOrCSSOMKeywordValue) =
            let mutable result =
                NativeDom.RvDomUnionCSSNumericValueOrDOMStringOrCSSOMKeywordValue ()

            result.present <- 1

            match value with
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSNumericValue numeric ->
                result.tag <- 1
                result.arm0 <- Interop.handle (box numeric)
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString text ->
                result.tag <- 2
                result.arm1 <- scope.TextSpan text
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSOMKeywordValue keyword ->
                result.tag <- 3
                result.arm2 <- Interop.handle (box keyword)

            result

        { new TimelineService with
            member _.CreateScrollTimeline(?source, ?axis) =
                let source =
                    match source with
                    | Some element when not (isNull (box element)) -> Some (Interop.handle (box element))
                    | _ -> None

                scroll source (defaultArg axis ScrollAxis.Block)

            member _.CreateScrollTimelineWithoutSource?axis =
                scroll (Some 0n) (defaultArg axis ScrollAxis.Block)

            member _.CreateViewTimeline(subject, ?axis, ?inset) =
                view subject (defaultArg axis ScrollAxis.Block) (fun scope ->
                    let mutable result =
                        NativeDom.RvDomUnionDOMStringOrSequenceOfCSSNumericValueOrCSSOMKeywordish ()

                    result.present <- 1
                    result.tag <- 1
                    result.arm0 <- scope.TextSpan (defaultArg inset "auto")
                    result)

            member _.CreateViewTimelineWithInsets(subject, insets, ?axis) =
                if isNull insets then
                    nullArg (nameof insets)

                view subject (defaultArg axis ScrollAxis.Block) (fun scope ->
                    let mutable sequence = NativeDom.RvDomSequenceCSSNumericValueOrCSSOMKeywordish ()
                    sequence.present <- 1
                    sequence.count <- insets.Length
                    sequence.items <- scope.Pin (insets |> Array.map (item scope))

                    let mutable result =
                        NativeDom.RvDomUnionDOMStringOrSequenceOfCSSNumericValueOrCSSOMKeywordish ()

                    result.present <- 1
                    result.tag <- 2
                    result.arm1 <- sequence
                    result)

          interface IDisposable with
              member _.Dispose() =
                  disposed <- true
        }

    /// <summary>Creates a borrowed timeline provider bound to the live host's document.</summary>
    let forHost (host: WebCoreHost) : TimelineService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
