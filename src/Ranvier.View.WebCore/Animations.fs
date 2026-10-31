namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for native keyframe effects, animations and document timelines.</summary>
module Animations =
    let private keyframes (scope: InputScope) (frames: Keyframe array) =
        if isNull frames then
            Unchecked.defaultof<NativeDom.RvDomSequenceKeyframeNullable>
        else
            let items =
                frames
                |> Array.map (fun frame ->
                    let properties =
                        (if isNull frame.properties then [||] else frame.properties)
                        |> Array.map (fun property ->
                            let mutable value = NativeDom.RvDomKeyframeProperty ()
                            value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomKeyframeProperty>())
                            value.name <- scope.TextInput (Some property.name)
                            value.value <- scope.TextInput (Some property.value)
                            // A sequence of dictionaries holds a pointer to each dictionary.
                            scope.Pin [| value |])

                    let mutable sequence = NativeDom.RvDomSequenceKeyframeProperty ()
                    sequence.present <- 1
                    sequence.count <- properties.Length
                    sequence.items <- scope.Pin properties
                    let mutable value = NativeDom.RvDomKeyframe ()
                    value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomKeyframe>())

                    value.offset <-
                        match frame.offset with
                        | Some offset -> scope.Pin [| offset |]
                        | None -> 0n

                    value.easing <- scope.TextInput frame.easing
                    value.composite <- scope.TextInput frame.composite
                    value.properties <- scope.Pin [| sequence |]
                    scope.Pin [| value |])

            let mutable value = NativeDom.RvDomSequenceKeyframeNullable ()
            value.present <- 1
            value.count <- items.Length
            value.items <- scope.Pin items
            value

    let private duration (scope: InputScope) (value: UnrestrictedDoubleOrCSSNumericValueOrDOMString) =
        let mutable result = NativeDom.RvDomUnionUnrestrictedDoubleOrCSSNumericValueOrDOMString ()
        result.present <- 1

        match value with
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble milliseconds ->
            result.tag <- 1
            result.arm0 <- milliseconds
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.CSSNumericValue numeric ->
            result.tag <- 2
            result.arm1 <- Interop.handle (box numeric)
        | UnrestrictedDoubleOrCSSNumericValueOrDOMString.DOMString text ->
            result.tag <- 3
            result.arm2 <- scope.TextSpan text

        result

    let private effectOptions (scope: InputScope) (options: KeyframeEffectOptions) =
        let options = if isNull options then KeyframeEffectOptions () else options
        let mutable value = NativeDom.RvDomKeyframeEffectOptions ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomKeyframeEffectOptions>())
        value.delay <- options.delay
        value.end_delay <- options.endDelay

        value.fill <-
            match options.fill with
            | FillMode.None -> 0
            | FillMode.Forwards -> 1
            | FillMode.Backwards -> 2
            | FillMode.Both -> 3
            | FillMode.Auto -> 4

        value.iteration_start <- options.iterationStart
        value.iterations <- options.iterations
        value.duration <- scope.Pin [| duration scope options.duration |]

        value.direction <-
            match options.direction with
            | PlaybackDirection.Normal -> 0
            | PlaybackDirection.Reverse -> 1
            | PlaybackDirection.Alternate -> 2
            | PlaybackDirection.AlternateReverse -> 3

        value.easing <- scope.TextInput (Some options.easing)

        value.iteration_composite <-
            match options.iterationComposite with
            | IterationCompositeOperation.Replace -> 0
            | IterationCompositeOperation.Accumulate -> 1

        value.composite <-
            match options.composite with
            | CompositeOperation.Replace -> 0
            | CompositeOperation.Add -> 1
            | CompositeOperation.Accumulate -> 2

        value.pseudo_element <- scope.TextInput (Option.ofObj options.pseudoElement)
        value

    /// <summary>The Web Animations constructors of a native document.</summary>
    let forDocument (document: Document) : AnimationService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        for feature in
            [
                "dom-constructor:KeyframeEffect"
                "dom-constructor:WebAnimation"
                "dom-constructor:DocumentTimeline"
            ] do
            if not (WebCoreHost.Has feature) then
                Interop.unsupported<unit> feature

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "An animation service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "AnimationService")

            Interop.handle (box owner)

        let engine label (call: unit -> int) =
            try
                call () |> Interop.check label
            with WebCoreDomException(name, detail) ->
                raise (AnimationException(name, detail))

        let effect handle =
            session.Object (handle, Kind.AnimationEffect, owner, Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationEffect (s, h)))
            |> unbox<KeyframeEffect>

        let animation handle =
            session.Track (WebAnimationObject (session, handle)) :> WebAnimation

        let create target frames (options: NativeDom.RvDomUnionUnrestrictedDoubleOrKeyframeEffectOptions) (scope: InputScope) =
            let documentHandle = active ()
            let mutable handle = 0n
            let target = if isNull (box target) then 0n else Interop.handle (box target)

            engine "KeyframeEffect constructor" (fun () ->
                NativeDom.rv_dom_keyframe_effect_create_3 (documentHandle, target, [| keyframes scope frames |], [| options |], &handle))

            effect handle

        let construct (effect: AnimationEffect) (timeline: nativeint) present =
            let documentHandle = active ()
            let mutable handle = 0n
            let effect = if isNull (box effect) then 0n else Interop.handle (box effect)

            engine "Animation constructor" (fun () ->
                NativeDom.rv_dom_web_animation_create (documentHandle, effect, timeline, present, &handle))

            animation handle

        { new AnimationService with
            member _.CreateKeyframeEffect(target, frames, ?options) =
                use scope = new InputScope ()
                let mutable union = NativeDom.RvDomUnionUnrestrictedDoubleOrKeyframeEffectOptions ()
                union.present <- 1
                union.tag <- 2
                union.arm1 <- scope.Pin [| effectOptions scope (defaultArg options null) |]
                create target frames union scope

            member _.CreateKeyframeEffectFor(target, frames, milliseconds) =
                use scope = new InputScope ()
                let mutable union = NativeDom.RvDomUnionUnrestrictedDoubleOrKeyframeEffectOptions ()
                union.present <- 1
                union.tag <- 1
                union.arm0 <- milliseconds
                create target frames union scope

            member _.CopyKeyframeEffect source =
                if isNull (box source) then
                    nullArg (nameof source)

                let documentHandle = active ()
                let mutable handle = 0n

                engine "KeyframeEffect constructor" (fun () ->
                    NativeDom.rv_dom_keyframe_effect_create_1 (documentHandle, Interop.handle (box source), &handle))

                effect handle

            member _.CreateAnimation(?effect, ?timeline) =
                match timeline with
                | Some timeline when not (isNull (box timeline)) ->
                    construct (defaultArg effect null) (Interop.handle (box timeline)) 1
                | _ -> construct (defaultArg effect null) 0n 0

            member _.CreateAnimationWithoutTimeline ?effect = construct (defaultArg effect null) 0n 1

            member _.CreateDocumentTimeline ?originTime =
                let documentHandle = active ()
                let mutable options = NativeDom.RvDomDocumentTimelineOptions ()
                options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDocumentTimelineOptions>())
                options.origin_time <- defaultArg originTime 0.
                let mutable handle = 0n

                engine "DocumentTimeline constructor" (fun () ->
                    NativeDom.rv_dom_document_timeline_create (documentHandle, [| options |], &handle))

                session.Object (handle, Kind.AnimationTimeline, owner, Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationTimeline (s, h)))
                |> unbox<DocumentTimeline>

          interface IDisposable with
              member _.Dispose() = disposed <- true }

    /// <summary>Creates a borrowed animation provider bound to the live host's document.</summary>
    let forHost (host: WebCoreHost) : AnimationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
