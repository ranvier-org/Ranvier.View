namespace Ranvier.View.WebCore

open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Reads an animation's range start or end, whose range-offset arm is an owned result dictionary.</summary>
module internal TimelineRanges =
    /// <summary>
    /// The range of the animation handle; <c>wrap</c> turns a retained CSSStyleValue handle into its wrapper. The
    /// union and its dictionary are released before the call returns.
    /// </summary>
    let read (animation: nativeint) (start: bool) (wrap: nativeint -> obj) =
        let label =
            if start then
                "WebAnimation.rangeStart"
            else
                "WebAnimation.rangeEnd"

        let mutable result =
            NativeDom.RvDomUnionTimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString ()

        let style (handle: nativeint) =
            NativeDom.rv_dom_css_style_value_retain handle
            |> ignore

            wrap handle

        try
            Interop.check
                label
                (if start then
                     NativeDom.rv_dom_web_animation_get_range_start (animation, &result)
                 else
                     NativeDom.rv_dom_web_animation_get_range_end (animation, &result))

            match result.tag with
            | 1 ->
                let fields = Marshal.PtrToStructure<NativeDom.RvDomTimelineRangeOffset> result.arm0
                let value = TimelineRangeOffset ()

                value.rangeName <-
                    if fields.range_name.present = 0 then
                        None
                    else
                        Some (Marshal.PtrToStringUTF8 (fields.range_name.data, fields.range_name.length))

                value.offset <-
                    if fields.offset = 0n then
                        None
                    else
                        Some (unbox<CSSNumericValue>(style fields.offset))

                TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.TimelineRangeOffset value
            | 2 -> TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.CSSNumericValue (unbox (style result.arm1))
            | 3 -> TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.CSSOMKeywordValue (unbox (style result.arm2))
            | 4 -> TimelineRangeOffsetOrCSSNumericValueOrCSSOMKeywordValueOrDOMString.DOMString (Interop.spanText result.arm3)
            | _ ->
                invalidOp (
                    "Unknown range union tag from WebCore: "
                    + string result.tag
                )
        finally
            NativeDom.rv_dom_union_timeline_range_offset_or_css_numeric_value_or_cssom_keyword_value_or_dom_string_release (&result)
