module internal NativeApiConformance.TextSegmentationChecks

open System
open Ranvier.Dom

/// <summary>The segmenter whose measured differences from the other engine the fixture expects.</summary>
type Parser =
    /// <summary>WebKit's ICU 77 iterators (UAX #29 word rules).</summary>
    | WebKit
    /// <summary>Chromium 156's Intl.Segmenter, which breaks words at a full stop between letters.</summary>
    | Chromium

// Locale and text from the Ranvier.WebCore T06 fixture (tests/fixtures/text-segmentation-cases.json).
let private cases =
    [
        "en", "é ä ñ"
        "en", "👨‍👩‍👧‍👦 family, 🇯🇵🇺🇸 flags, 👍🏽 skin tone"
        "en", "\r\n line\nbreaks\r"
        "en", "क्षि and ठ्ठ"
        "ko", "한국어 텍스트 ᄀᄀᄀ각"
        "en", "Don't stop. It's 3.14, not 3,14! Mr. Smith left at 5 p.m. Then?"
        "th", "ภาษาไทยไม่มีช่องว่างระหว่างคำ"
        "ja", "今日は良い天気ですね。明日も晴れるでしょう。"
        "zh", "我们今天去北京大学。你呢？"
        "ar", "مرحبا بالعالم. كيف حالك؟"
        "hi", "नमस्ते दुनिया। आप कैसे हैं?"
        "en", "...wait... What?! Really?? Yes."
        "en", "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F tag flag"
    ]

/// <summary>
/// Checks graphemes, words, sentences and line breaks on one provider: every segment list covers its text, every
/// boundary is a grapheme boundary, and the measured segments; the result is the assertion count.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>RangeError</c>.</param>
let run (create: unit -> TextSegmenter) (errorName: exn -> string) (parser: Parser) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected %A{expected}, got %A{actual}"

    let fails label (expected: string) (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                None
            with error ->
                Some (errorName error)

        if name <> Some expected then
            failwith $"{label}: expected {expected}, got %A{name}"

    let pick webkit chromium =
        match parser with
        | WebKit -> webkit
        | Chromium -> chromium

    use segmenter = create ()

    let name granularity =
        match granularity with
        | SegmentGranularity.Grapheme -> "grapheme"
        | SegmentGranularity.Word -> "word"
        | SegmentGranularity.Sentence -> "sentence"
        | SegmentGranularity.Line -> "line"

    let segment granularity locale (text: string) =
        match segmenter.Segment (text, granularity, locale) with
        | SegmentationResult.Segmented result -> result.Segments
        | SegmentationResult.NotSupported _ -> failwith (name granularity + " is not supported")

    let words locale text =
        segment SegmentGranularity.Word locale text
        |> Array.filter (fun s -> s.IsWordLike)
        |> Array.map (fun s -> s.Segment)

    let texts granularity locale text =
        segment granularity locale text
        |> Array.map (fun s -> s.Segment)

    for locale, text in cases do
        let graphemes = segment SegmentGranularity.Grapheme (Some locale) text

        let boundaries =
            graphemes
            |> Array.map (fun s -> s.Index)
            |> Set.ofArray

        for granularity in
            [
                SegmentGranularity.Grapheme
                SegmentGranularity.Word
                SegmentGranularity.Sentence
                SegmentGranularity.Line
            ] do
            let result = segment granularity (Some locale) text
            let label = name granularity + " " + locale + " " + text

            equal
                (label + ": covers the text")
                text
                (result
                 |> Array.map (fun s -> s.Segment)
                 |> String.concat "")

            equal
                (label + ": indices")
                (result
                 |> Array.scan (fun index s -> index + s.Segment.Length) 0
                 |> Array.take result.Length)
                (result |> Array.map (fun s -> s.Index))

            equal
                (label + ": every boundary is a grapheme boundary")
                true
                (result
                 |> Array.forall (fun s -> boundaries.Contains s.Index))

            if granularity <> SegmentGranularity.Word then
                equal (label + ": only words are word-like") false (result |> Array.exists (fun s -> s.IsWordLike))

    // Graphemes: combining marks, emoji sequences, CRLF, Indic conjuncts (UAX #29 GB9c) and Hangul.
    equal "combining marks" [| "é"; " "; "ä"; " "; "ñ" |] (texts SegmentGranularity.Grapheme (Some "en") "é ä ñ")

    equal
        "ZWJ family"
        "👨‍👩‍👧‍👦"
        (texts SegmentGranularity.Grapheme (Some "en") "👨‍👩‍👧‍👦 x"
         |> Array.head)

    equal "flags" [| "🇯🇵"; "🇺🇸" |] (texts SegmentGranularity.Grapheme None "🇯🇵🇺🇸")
    equal "CRLF" [| "\r\n"; "x" |] (texts SegmentGranularity.Grapheme None "\r\nx")
    equal "Indic conjuncts" [| "क्षि"; " "; "ठ्ठ" |] (texts SegmentGranularity.Grapheme (Some "hi") "क्षि ठ्ठ")

    equal
        "tag sequence"
        "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F"
        (texts SegmentGranularity.Grapheme None "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F tag"
         |> Array.head)

    // Words: dictionaries, contractions, numbers, and the measured full-stop rule.
    equal "Thai words" [| "ภาษา"; "ไทย"; "ไม่มี"; "ช่อง"; "ว่าง"; "ระหว่าง"; "คำ" |] (words (Some "th") "ภาษาไทยไม่มีช่องว่างระหว่างคำ")
    equal "Japanese words" [| "今日"; "は"; "良い"; "天気"; "です"; "ね" |] (words (Some "ja") "今日は良い天気ですね。")
    equal "Chinese words" [| "我们"; "今天"; "去"; "北京"; "大学" |] (words (Some "zh") "我们今天去北京大学。")
    equal "contractions" [| "can't"; "won't"; "O'Neil"; "rock'n'roll" |] (words (Some "en") "can't won't O'Neil rock'n'roll")
    equal "numbers" [| "3.14"; "3,14"; "1,234.56" |] (words (Some "en") "3.14 3,14 1,234.56")
    equal "a full stop between letters" (pick [| "p.m"; "Z.B" |] [| "p"; "m"; "Z"; "B" |]) (words (Some "en") "p.m. Z.B.")
    equal "Hangul jamo and a syllable stay one segment" [| "ᄀᄀᄀ각" |] (words (Some "ko") "ᄀᄀᄀ각")
    equal "spaces and punctuation are not word-like" [| "a"; "b" |] (words (Some "en") "a, b!")

    // Sentences.
    equal
        "sentences"
        [| "Don't stop. "; "It's 3.14, not 3,14! "; "Mr. "; "Smith left." |]
        (texts SegmentGranularity.Sentence (Some "en") "Don't stop. It's 3.14, not 3,14! Mr. Smith left.")

    equal "Japanese sentences" [| "今日は良い天気ですね。"; "明日も晴れるでしょう。" |] (texts SegmentGranularity.Sentence (Some "ja") "今日は良い天気ですね。明日も晴れるでしょう。")
    equal "Hindi danda" [| "नमस्ते दुनिया। "; "आप कैसे हैं?" |] (texts SegmentGranularity.Sentence (Some "hi") "नमस्ते दुनिया। आप कैसे हैं?")

    // Line-break opportunities (normal).
    equal "line breaks" [| "A "; "line-"; "breaking "; "test." |] (texts SegmentGranularity.Line (Some "en") "A line-breaking test.")
    equal "no break before closing punctuation" [| "(hi) "; "there" |] (texts SegmentGranularity.Line (Some "en") "(hi) there")
    equal "CJK breaks between ideographs" 5 (texts SegmentGranularity.Line (Some "ja") "今日は良い").Length

    // Locales.
    fails "a malformed locale" "RangeError" (fun () ->
        segment SegmentGranularity.Word (Some "en_US") "x"
        |> ignore)

    fails "an empty subtag" "RangeError" (fun () ->
        segment SegmentGranularity.Word (Some "en--US") "x"
        |> ignore)

    equal "an unknown locale still segments" [| "a"; "b" |] (words (Some "xx-YY") "a b")
    equal "an empty text" 0 (segment SegmentGranularity.Word (Some "en") "").Length
    equal "a Some null locale segments as None does" (words None "a b") (words (Some null) "a b")

    segmenter.Dispose ()
    checks
