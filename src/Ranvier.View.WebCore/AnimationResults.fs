namespace Ranvier.View.WebCore

open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>The owned Web Animations effect results (A02): EffectTiming, ComputedEffectTiming and computed keyframes.</summary>
/// <remarks>Each result is copied into its contract dictionary, then released with its paired release.</remarks>
module internal AnimationResults =
    let private text (value: NativeDom.RvDomUtf8StringInput) =
        if value.present = 0 then
            null
        else
            let span = NativeDom.RvDomUtf8StringSpan ()
            let mutable span = span
            span.data <- value.data
            span.length <- value.length
            Interop.spanText span

    let private number (pointer: nativeint) =
        if pointer = 0n then
            None
        else
            Some (Marshal.PtrToStructure<float> pointer)

    let private fill value =
        match value with
        | 0 -> FillMode.None
        | 1 -> FillMode.Forwards
        | 2 -> FillMode.Backwards
        | 3 -> FillMode.Both
        | 4 -> FillMode.Auto
        | _ -> invalidOp "Unknown FillMode value from WebCore"

    let private direction value =
        match value with
        | 0 -> PlaybackDirection.Normal
        | 1 -> PlaybackDirection.Reverse
        | 2 -> PlaybackDirection.Alternate
        | 3 -> PlaybackDirection.AlternateReverse
        | _ -> invalidOp "Unknown PlaybackDirection value from WebCore"

    let private duration (pointer: nativeint) =
        let value =
            Marshal.PtrToStructure<NativeDom.RvDomUnionUnrestrictedDoubleOrCSSNumericValueOrDOMString> pointer

        match value.tag with
        | 1 -> UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble value.arm0
        | 3 -> UnrestrictedDoubleOrCSSNumericValueOrDOMString.DOMString (Interop.spanText value.arm2)
        | _ -> invalidOp "A typed duration from WebCore is not supported"

    let private numberish (pointer: nativeint) =
        if pointer = 0n then
            None
        else
            let value =
                Marshal.PtrToStructure<NativeDom.RvDomUnionDoubleOrCSSNumericValue> pointer

            if value.present = 0 then
                None
            elif value.tag = 1 then
                Some (DoubleOrCSSNumericValue.Double value.arm0)
            else
                invalidOp "A typed time from WebCore is not supported"

    let private nullableNumberish (pointer: nativeint) =
        if pointer = 0n then
            None
        else
            let value =
                Marshal.PtrToStructure<NativeDom.RvDomUnionDoubleOrCSSNumericValueNullable> pointer

            if value.present = 0 then
                None
            elif value.tag = 1 then
                Some (DoubleOrCSSNumericValue.Double value.arm0)
            else
                invalidOp "A typed time from WebCore is not supported"

    /// <summary>AnimationEffect.getTiming.</summary>
    let timing (target: obj) : EffectTiming =
        let mutable result = 0n

        NativeDom.rv_dom_animation_effect_get_timing (Interop.handle target, &result)
        |> Interop.check "AnimationEffect.getTiming"

        try
            let value = Marshal.PtrToStructure<NativeDom.RvDomEffectTiming> result
            let timing = EffectTiming ()
            timing.delay <- value.delay
            timing.endDelay <- value.end_delay
            timing.fill <- fill value.fill
            timing.iterationStart <- value.iteration_start
            timing.iterations <- value.iterations
            timing.duration <- duration value.duration
            timing.direction <- direction value.direction
            timing.easing <- text value.easing
            timing
        finally
            NativeDom.rv_dom_effect_timing_release result

    /// <summary>AnimationEffect.getComputedTiming.</summary>
    let computed (target: obj) : ComputedEffectTiming =
        let mutable result = 0n

        NativeDom.rv_dom_animation_effect_get_computed_timing (Interop.handle target, &result)
        |> Interop.check "AnimationEffect.getComputedTiming"

        try
            let value = Marshal.PtrToStructure<NativeDom.RvDomComputedEffectTiming> result
            let timing = ComputedEffectTiming ()
            timing.delay <- value.delay
            timing.endDelay <- value.end_delay
            timing.fill <- fill value.fill
            timing.iterationStart <- value.iteration_start
            timing.iterations <- value.iterations
            timing.duration <- duration value.duration
            timing.direction <- direction value.direction
            timing.easing <- text value.easing

            timing.endTime <- numberish value.end_time
            timing.activeDuration <- numberish value.active_duration

            timing.localTime <- nullableNumberish value.local_time
            timing.progress <- number value.progress
            timing.currentIteration <- number value.current_iteration
            timing
        finally
            NativeDom.rv_dom_computed_effect_timing_release result

    /// <summary>KeyframeEffect.getKeyframes.</summary>
    let keyframes (target: obj) : ComputedKeyframe array =
        let mutable result = NativeDom.RvDomSequenceComputedKeyframe ()

        NativeDom.rv_dom_keyframe_effect_get_keyframes (Interop.handle target, &result)
        |> Interop.check "KeyframeEffect.getKeyframes"

        try
            Array.init result.count (fun index ->
                let pointer = Marshal.ReadIntPtr (result.items, index * System.IntPtr.Size)
                let value = Marshal.PtrToStructure<NativeDom.RvDomComputedKeyframe> pointer
                let frame = ComputedKeyframe ()
                frame.offset <- number value.offset
                frame.computedOffset <- value.computed_offset
                frame.easing <- text value.easing
                frame.composite <- text value.composite

                frame.properties <-
                    if value.properties = 0n then
                        [||]
                    else
                        let properties =
                            Marshal.PtrToStructure<NativeDom.RvDomSequenceKeyframeProperty> value.properties

                        Array.init properties.count (fun item ->
                            let entry =
                                Marshal.PtrToStructure<NativeDom.RvDomKeyframeProperty>(
                                    Marshal.ReadIntPtr (properties.items, item * System.IntPtr.Size)
                                )

                            let property = KeyframeProperty ()
                            property.name <- text entry.name
                            property.value <- text entry.value
                            property)

                frame)
        finally
            NativeDom.rv_dom_sequence_computed_keyframe_release &result
