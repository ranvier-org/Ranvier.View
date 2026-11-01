namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for native custom animation effects (CustomEffect, DocumentTimeline.animate).</summary>
/// <remarks>
/// Each effect owns one <c>RvCustomEffectCallback</c>: the engine queues its progress during <c>rv_host_update</c> and the
/// run loop delivers it, so a tick runs only from <c>rv_run_loop_cycle</c>. A tick runs as a listener: its exception joins
/// <see cref="P:Ranvier.View.WebCore.Session.ListenerErrors"/> and later ticks still run.
/// </remarks>
module CustomEffects =
    [<AllowNullLiteral>]
    type private Registration(session: Session, tick: float -> unit) =
        member val Callback = 0n with get, set
        member val Live = true with get, set
        member _.Session = session
        member _.Tick = tick

    let private run (context: nativeint) (progress: float) =
        let registration = GCHandle.FromIntPtr(context).Target :?> Registration

        if registration.Live then
            let session = registration.Session
            session.ScriptDepth <- session.ScriptDepth + 1

            try
                try
                    registration.Tick progress
                with error ->
                    session.ListenerErrors.Add error
            finally
                session.ScriptDepth <- session.ScriptDepth - 1

                if
                    session.ScriptDepth = 0
                    && not (isNull session.AfterListener)
                then
                    session.AfterListener.Invoke ()

    let private tickDelegate =
        Native.CustomEffectTick (fun context progress -> run context progress)

    let private releaseDelegate =
        Native.CustomEffectRelease (fun context -> GCHandle.FromIntPtr(context).Free())

    let private tickPointer =
        Marshal.GetFunctionPointerForDelegate<Native.CustomEffectTick> tickDelegate

    let private releasePointer =
        Marshal.GetFunctionPointerForDelegate<Native.CustomEffectRelease> releaseDelegate

    // Stops the ticks and drops the registration's callback reference; the engine's effect keeps its own.
    let private revoke (registration: Registration) =
        if registration.Live then
            registration.Live <- false
            let status = Native.rv_custom_effect_callback_revoke registration.Callback
            NativeDom.rv_dom_custom_effect_callback_release registration.Callback
            status |> Interop.check "CustomEffect.revoke"

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

    let private fill =
        function
        | FillMode.None -> 0
        | FillMode.Forwards -> 1
        | FillMode.Backwards -> 2
        | FillMode.Both -> 3
        | FillMode.Auto -> 4

    let private direction =
        function
        | PlaybackDirection.Normal -> 0
        | PlaybackDirection.Reverse -> 1
        | PlaybackDirection.Alternate -> 2
        | PlaybackDirection.AlternateReverse -> 3

    let private timing (scope: InputScope) (options: EffectTiming) =
        let options = if isNull options then EffectTiming () else options
        let mutable value = NativeDom.RvDomEffectTiming ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomEffectTiming>())
        value.delay <- options.delay
        value.end_delay <- options.endDelay
        value.fill <- fill options.fill
        value.iteration_start <- options.iterationStart
        value.iterations <- options.iterations
        value.duration <- scope.Pin [| duration scope options.duration |]
        value.direction <- direction options.direction
        value.easing <- scope.TextInput (Some options.easing)
        value

    let private animationOptions (scope: InputScope) (options: CustomAnimationOptions) =
        let options =
            if isNull options then
                CustomAnimationOptions ()
            else
                options

        let mutable value = NativeDom.RvDomCustomAnimationOptions ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomCustomAnimationOptions>())
        value.delay <- options.delay
        value.end_delay <- options.endDelay
        value.fill <- fill options.fill
        value.iteration_start <- options.iterationStart
        value.iterations <- options.iterations
        value.duration <- scope.Pin [| duration scope options.duration |]
        value.direction <- direction options.direction
        value.easing <- scope.TextInput (Some options.easing)
        value.id <- scope.TextInput (Some options.id)
        value

    /// <summary>The custom effects of a native document.</summary>
    let forDocument (document: Document) : CustomEffectService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        for feature in [ "animation-custom-effects"; "dom-constructor:CustomEffect" ] do
            if not (WebCoreHost.Has feature) then
                Interop.unsupported<unit> feature

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false
        let registrations = ResizeArray<Registration>()

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A custom effect service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "CustomEffectService")

            Interop.handle (box owner)

        let engine label (call: unit -> int) =
            try
                call () |> Interop.check label
            with WebCoreDomException (name, detail) ->
                raise (AnimationException (name, detail))

        let effect handle =
            session.Object (handle, Kind.AnimationEffect, owner, Func<Session, nativeint, NativeObject>(fun s h -> Classes.AnimationEffect (s, h)))
            |> unbox<AnimationEffect>

        // Registers the tick with a native callback of the host; the registration owns one callback reference until revoked.
        let register (tick: float -> unit) =
            if isNull (box tick) then
                nullArg (nameof tick)

            let registration = Registration (session, tick)
            let context = GCHandle.Alloc registration

            let callback =
                Native.rv_custom_effect_callback_create (session.Host, tickPointer, GCHandle.ToIntPtr context, releasePointer)

            if callback = 0n then
                context.Free ()
                raise (InvalidOperationException "rv_custom_effect_callback_create returned NULL")

            registration.Callback <- callback
            registrations.Add registration
            registration

        let created (registration: Registration) (value: AnimationEffect) =
            { new CustomEffectRegistration with
                member _.Effect = value

                member _.Dispose() =
                    revoke registration
            }

        let create tick (union: NativeDom.RvDomUnionUnrestrictedDoubleOrEffectTiming) =
            let documentHandle = active ()
            let registration = register tick
            let mutable handle = 0n

            try
                engine "CustomEffect constructor" (fun () ->
                    NativeDom.rv_dom_custom_effect_create (documentHandle, registration.Callback, [| union |], &handle))
            with _ ->
                revoke registration
                reraise ()

            created registration (effect handle)

        { new CustomEffectService with
            member _.CreateCustomEffect(tick, ?options) =
                use scope = new InputScope ()
                let mutable union = NativeDom.RvDomUnionUnrestrictedDoubleOrEffectTiming ()
                union.present <- 1
                union.tag <- 2
                union.arm1 <- scope.Pin [| timing scope (defaultArg options null) |]
                create tick union

            member _.CreateCustomEffectFor(tick, milliseconds) =
                let mutable union = NativeDom.RvDomUnionUnrestrictedDoubleOrEffectTiming ()
                union.present <- 1
                union.tag <- 1
                union.arm0 <- milliseconds
                create tick union

            member _.Animate(tick, ?options) =
                let documentHandle = active ()
                use scope = new InputScope ()

                let mutable union =
                    NativeDom.RvDomUnionUnrestrictedDoubleOrCustomAnimationOptions ()

                union.present <- 1
                union.tag <- 2
                union.arm1 <- scope.Pin [| animationOptions scope (defaultArg options null) |]
                let mutable timeline = 0n
                engine "Document.timeline" (fun () -> NativeDom.rv_dom_document_get_timeline (documentHandle, &timeline))
                let registration = register tick
                let mutable handle = 0n

                try
                    try
                        engine "DocumentTimeline.animate" (fun () ->
                            NativeDom.rv_dom_document_timeline_animate (timeline, registration.Callback, [| union |], &handle))
                    with _ ->
                        revoke registration
                        reraise ()
                finally
                    NativeDom.rv_dom_animation_timeline_release timeline

                let playing = session.Track (WebAnimationObject (session, handle)) :> WebAnimation
                let mutable effectHandle = 0n

                NativeDom.rv_dom_web_animation_get_effect (handle, &effectHandle)
                |> Interop.check "Animation.effect"

                let value = effect effectHandle

                { new CustomAnimation with
                    member _.Effect = value
                    member _.Animation = playing

                    member _.Dispose() =
                        revoke registration
                }

          interface IDisposable with
              member _.Dispose() =
                  if not disposed then
                      disposed <- true

                      for registration in registrations do
                          revoke registration

                      registrations.Clear ()
        }

    /// <summary>Creates a borrowed custom effect provider bound to the live host's document.</summary>
    let forHost (host: WebCoreHost) : CustomEffectService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
