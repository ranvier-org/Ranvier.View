namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
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
        let mutable result =
            NativeDom.RvDomUnionUnrestrictedDoubleOrCSSNumericValueOrDOMString ()

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

    /// <summary>Completions of native animation promise operations by context key; the delegate lives with the process.</summary>
    type private Completions() =
        let gate = obj ()
        let handlers = Dictionary<int64, int -> unit>()
        let mutable next = 0L

        let callback =
            Native.ResourceCompletion (fun context outcome _ _ ->
                let handler =
                    lock gate (fun () ->
                        match handlers.TryGetValue (int64 context) with
                        | true, handler ->
                            handlers.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome))

        member _.Pointer =
            Marshal.GetFunctionPointerForDelegate<Native.ResourceCompletion> callback

        member _.Add(handler: int -> unit) =
            lock gate (fun () ->
                next <- next + 1L
                handlers[next] <- handler
                next)

        member _.Remove(key: int64) =
            lock gate (fun () -> handlers.Remove key |> ignore)

    let private completions = Completions ()

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
            with WebCoreDomException (name, detail) ->
                raise (AnimationException (name, detail))

        let effect handle =
            session.Object (handle, Kind.AnimationEffect, owner, Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationEffect (s, h)))
            |> unbox<KeyframeEffect>

        let animation handle =
            session.Object (handle, Kind.WebAnimation, owner, Func<Session, nativeint, NativeObject>(fun s h -> WebAnimationObject (s, h)))
            |> unbox<WebAnimation>

        // Pending Ready and Finished operations; their completions arrive on the owning thread from rv_run_loop_cycle,
        // or from rv_resource_release, rv_host_destroy and a new document.
        let live = HashSet<IDisposable>(HashIdentity.Reference)

        let failure (outcome: int) =
            match outcome with
            | 3 -> ResourceFailure.Cancelled
            | 4 -> ResourceFailure.Invalidated "The animation's document was replaced or destroyed"
            | 5 -> ResourceFailure.Aborted "AbortError"
            | other ->
                ResourceFailure.Invalidated (
                    "The animation promise ended with outcome "
                    + string other
                )

        let observe (target: WebAnimation) (finished: bool) (terminal: ResourceOutcome<WebAnimation> -> unit) : ResourceOperation<WebAnimation> =
            active () |> ignore

            if isNull (box target) then
                nullArg "animation"

            let handle = Interop.handle (box target)
            let mutable operation = 0UL

            let release () =
                if operation <> 0UL then
                    let current = operation
                    operation <- 0UL
                    Native.rv_resource_release current |> ignore

            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<WebAnimation>>

            arbiter <-
                new ResourceArbiter<WebAnimation> (
                    (fun action -> action ()),
                    (fun outcome ->
                        live.Remove arbiter |> ignore
                        release ()
                        terminal outcome),
                    release,
                    ignore
                )

            let key =
                completions.Add (fun outcome ->
                    if outcome = 0 then
                        arbiter.Settle (ResourceOutcome.Succeeded target)
                        |> ignore
                    else
                        arbiter.Settle (ResourceOutcome.Failed (failure outcome))
                        |> ignore)

            let pointer = completions.Pointer
            let mutable token = 0UL

            let status =
                if finished then
                    Native.rv_animation_finished (session.Host, handle, pointer, nativeint key, &token)
                else
                    Native.rv_animation_ready (session.Host, handle, pointer, nativeint key, &token)

            if status <> 0 then
                completions.Remove key

                if status = -12 then
                    invalidArg "animation" "The animation belongs to another document"

                Interop.check (if finished then "Animation.finished" else "Animation.ready") status

            operation <- token
            live.Add arbiter |> ignore
            arbiter :> ResourceOperation<_>

        let create target frames (options: NativeDom.RvDomUnionUnrestrictedDoubleOrKeyframeEffectOptions) (scope: InputScope) =
            let documentHandle = active ()
            let mutable handle = 0n

            let target =
                if isNull (box target) then
                    0n
                else
                    Interop.handle (box target)

            engine "KeyframeEffect constructor" (fun () ->
                NativeDom.rv_dom_keyframe_effect_create_3 (documentHandle, target, [| keyframes scope frames |], [| options |], &handle))

            effect handle

        let construct (effect: AnimationEffect) (timeline: nativeint) present =
            let documentHandle = active ()
            let mutable handle = 0n

            let effect =
                if isNull (box effect) then
                    0n
                else
                    Interop.handle (box effect)

            engine "Animation constructor" (fun () -> NativeDom.rv_dom_web_animation_create (documentHandle, effect, timeline, present, &handle))

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
                | Some timeline when not (isNull (box timeline)) -> construct (defaultArg effect null) (Interop.handle (box timeline)) 1
                | _ -> construct (defaultArg effect null) 0n 0

            member _.CreateAnimationWithoutTimeline?effect =
                construct (defaultArg effect null) 0n 1

            member _.CreateDocumentTimeline?originTime =
                let documentHandle = active ()
                let mutable options = NativeDom.RvDomDocumentTimelineOptions ()
                options.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDocumentTimelineOptions>())
                options.origin_time <- defaultArg originTime 0.
                let mutable handle = 0n

                engine "DocumentTimeline constructor" (fun () -> NativeDom.rv_dom_document_timeline_create (documentHandle, [| options |], &handle))

                session.Object (
                    handle,
                    Kind.AnimationTimeline,
                    owner,
                    Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationTimeline (s, h))
                )
                |> unbox<DocumentTimeline>

            member _.Ready(target, terminal) =
                observe target false terminal

            member _.Finished(target, terminal) =
                observe target true terminal

          interface IDisposable with
              member _.Dispose() =
                  if not disposed then
                      disposed <- true

                      for operation in List.ofSeq live do
                          operation.Dispose ()

                      live.Clear ()
        }

    /// <summary>Creates a borrowed animation provider bound to the live host's document.</summary>
    let forHost (host: WebCoreHost) : AnimationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
