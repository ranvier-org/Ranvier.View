namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Text segmentation with the native engine's ICU, through rv_text_segment.</summary>
module TextSegmentation =
    let private granularityCode (value: SegmentGranularity) =
        match value with
        | SegmentGranularity.Grapheme -> 0
        | SegmentGranularity.Word -> 1
        | SegmentGranularity.Sentence -> 2
        | SegmentGranularity.Line -> 3

    let private read (text: string) (granularity: SegmentGranularity) (result: Native.TextSegments) =
        if
            result.count < 0
            || (result.count > 0 && result.starts = 0n)
        then
            invalidOp "WebCore returned an invalid segment list"

        let starts = Array.zeroCreate<int> result.count

        if result.count > 0 then
            Marshal.Copy (result.starts, starts, 0, result.count)

        let wordLike = Array.zeroCreate<int> result.count

        if
            granularity = SegmentGranularity.Word
            && result.count > 0
        then
            Marshal.Copy (result.word_like, wordLike, 0, result.count)

        let segments =
            starts
            |> Array.mapi (fun index start ->
                let finish =
                    if index + 1 < starts.Length then
                        starts[index + 1]
                    else
                        text.Length

                {
                    Index = start
                    Segment = text.Substring (start, finish - start)
                    IsWordLike = wordLike[index] <> 0
                })

        {
            Segments = segments
            Locale =
                if result.locale = 0n then
                    ""
                else
                    Marshal.PtrToStringUTF8 result.locale
        }

    /// <summary>A text segmenter for <c>host</c>'s engine.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks text segmentation.</remarks>
    /// <param name="host">The live host whose creating thread runs every segmentation.</param>
    let forHost (host: WebCoreHost) : TextSegmenter =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "text-segmentation") then
            raise (NotSupportedException "The native library has no text segmentation")

        let disposed = ref false

        { new TextSegmenter with
            member _.Segment(text, granularity, locale) =
                if disposed.Value then
                    raise (ObjectDisposedException (nameof TextSegmenter))

                let text = TextSegmentationContract.text text
                let mutable result = Native.TextSegments ()

                try
                    Native.rv_text_segment (
                        granularityCode granularity,
                        Option.toObj locale,
                        text.ToCharArray () |> Array.map uint16,
                        text.Length,
                        &result
                    )
                    |> Interop.check "Intl.Segmenter"

                    SegmentationResult.Segmented (read text granularity result)
                finally
                    Native.rv_text_segments_release &result

            member _.Dispose() =
                disposed.Value <- true
        }
