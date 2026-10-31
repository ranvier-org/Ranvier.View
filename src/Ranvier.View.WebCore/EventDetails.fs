namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Event detail records of a native host.</summary>
module EventDetails =
    let private features =
        [
            "ErrorEvent"
            "ProgressEvent"
            "CSSAnimationEvent"
            "CSSTransitionEvent"
            "ContentVisibilityAutoStateChangeEvent"
            "AnimationPlaybackEvent"
            "PageTransitionEvent"
            "HashChangeEvent"
            "MediaQueryListEvent"
            "PopStateEvent"
            "CompositionEvent"
        ]

    /// <summary>An event detail service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks an event detail constructor. ErrorEvent and
    /// PopStateEvent need the document's browsing context: on a document without one they raise the engine's
    /// InvalidStateError as a DOM exception.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : EventDetailService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                features
                |> List.forall (fun name -> WebCoreHost.Has ("dom-constructor:" + name))
            )
        then
            raise (NotSupportedException "The native library has no event detail constructors")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof EventDetailService))

            host.EnsureActive ()

        let flag value =
            if value then 1 else 0

        let name (eventType: string) =
            Encoding.UTF8.GetBytes (EventDetailContract.eventType eventType)

        let target () =
            Interop.handle (box document)

        let event (label: string) (status: int) (result: nativeint) =
            Interop.check label status
            session.Event result

        // A CSSNumberish? input: an unmanaged arm-0 union for a number, 0 for null; free releases it.
        let numberish (label: string) (value: float option) =
            match value with
            | None -> 0n
            | Some number ->
                let mutable union = NativeDom.RvDomUnionDoubleOrCSSNumericValueNullable ()
                union.present <- 1
                union.tag <- 1
                union.arm0 <- EventDetailContract.finite label number

                let memory =
                    Marshal.AllocHGlobal (Marshal.SizeOf<NativeDom.RvDomUnionDoubleOrCSSNumericValueNullable>())

                Marshal.StructureToPtr<NativeDom.RvDomUnionDoubleOrCSSNumericValueNullable> (union, memory, false)
                memory

        let free (memory: nativeint) =
            if memory <> 0n then
                Marshal.FreeHGlobal memory

        { new EventDetailService with
            member _.CreateErrorEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomErrorEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomErrorEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.message <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Message" fields.Message))
                init.filename <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Filename" fields.Filename))
                init.lineno <- fields.Lineno
                init.colno <- fields.Colno
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_error_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<ErrorEvent>(event "ErrorEvent constructor" status result)

            member _.CreateProgressEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                let mutable init = NativeDom.RvDomProgressEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomProgressEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.length_computable <- flag fields.LengthComputable
                init.loaded <- EventDetailContract.finite "Loaded" fields.Loaded
                init.total <- EventDetailContract.finite "Total" fields.Total
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_progress_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<ProgressEvent>(event "ProgressEvent constructor" status result)

            member _.CreateAnimationEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomAnimationEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomAnimationEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.animation_name <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Name" fields.Name))
                init.elapsed_time <- EventDetailContract.finite "ElapsedTime" fields.ElapsedTime
                init.pseudo_element <- inputs.TextInput (Option.ofObj (EventDetailContract.text "PseudoElement" fields.PseudoElement))
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_css_animation_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<AnimationEvent>(event "AnimationEvent constructor" status result)

            member _.CreateTransitionEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomTransitionEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomTransitionEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.property_name <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Name" fields.Name))
                init.elapsed_time <- EventDetailContract.finite "ElapsedTime" fields.ElapsedTime
                init.pseudo_element <- inputs.TextInput (Option.ofObj (EventDetailContract.text "PseudoElement" fields.PseudoElement))
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_css_transition_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<TransitionEvent>(event "TransitionEvent constructor" status result)

            member _.CreateContentVisibilityEvent(eventType, options, skipped) =
                check ()
                let eventType = name eventType
                let mutable init = NativeDom.RvDomContentVisibilityAutoStateChangeEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomContentVisibilityAutoStateChangeEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.skipped <- flag skipped
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_content_visibility_auto_state_change_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<ContentVisibilityAutoStateChangeEvent>(event "ContentVisibilityAutoStateChangeEvent constructor" status result)

            member _.CreateAnimationPlaybackEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                let current = numberish "CurrentTime" fields.CurrentTime

                try
                    let timeline = numberish "TimelineTime" fields.TimelineTime

                    try
                        let mutable init = NativeDom.RvDomAnimationPlaybackEventInit ()
                        init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomAnimationPlaybackEventInit>())
                        init.bubbles <- flag options.Bubbles
                        init.cancelable <- flag options.Cancelable
                        init.composed <- flag options.Composed
                        init.current_time <- current
                        init.timeline_time <- timeline
                        let mutable result = 0n

                        let status =
                            NativeDom.rv_dom_animation_playback_event_create (target (), eventType, eventType.Length, [| init |], &result)

                        unbox<AnimationPlaybackEvent>(event "AnimationPlaybackEvent constructor" status result)
                    finally
                        free timeline
                finally
                    free current

            member _.CreatePageTransitionEvent(eventType, options, persisted) =
                check ()
                let eventType = name eventType
                let mutable init = NativeDom.RvDomPageTransitionEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomPageTransitionEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.persisted <- flag persisted
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_page_transition_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<PageTransitionEvent>(event "PageTransitionEvent constructor" status result)

            member _.CreateHashChangeEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomHashChangeEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomHashChangeEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.old_url <- inputs.TextInput (Option.ofObj (EventDetailContract.text "OldURL" fields.OldURL))
                init.new_url <- inputs.TextInput (Option.ofObj (EventDetailContract.text "NewURL" fields.NewURL))
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_hash_change_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<HashChangeEvent>(event "HashChangeEvent constructor" status result)

            member _.CreateMediaQueryListEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomMediaQueryListEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomMediaQueryListEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.media <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Media" fields.Media))
                init.matches <- flag fields.Matches
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_media_query_list_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<MediaQueryListEvent>(event "MediaQueryListEvent constructor" status result)

            member _.CreatePopStateEvent(eventType, options, hasUAVisualTransition) =
                check ()
                let eventType = name eventType
                let mutable init = NativeDom.RvDomPopStateEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomPopStateEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.has_ua_visual_transition <- flag hasUAVisualTransition
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_pop_state_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<PopStateEvent>(event "PopStateEvent constructor" status result)

            member _.CreateCompositionEvent(eventType, options, fields) =
                check ()
                let eventType = name eventType
                use inputs = new InputScope ()
                let mutable init = NativeDom.RvDomCompositionEventInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomCompositionEventInit>())
                init.bubbles <- flag options.Bubbles
                init.cancelable <- flag options.Cancelable
                init.composed <- flag options.Composed
                init.detail <- fields.Detail
                init.data <- inputs.TextInput (Option.ofObj (EventDetailContract.text "Data" fields.Data))
                let mutable result = 0n

                let status =
                    NativeDom.rv_dom_composition_event_create (target (), eventType, eventType.Length, [| init |], &result)

                unbox<CompositionEvent>(event "CompositionEvent constructor" status result)

            member _.Dispose() =
                disposed.Value <- true
        }
