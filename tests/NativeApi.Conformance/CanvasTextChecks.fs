module internal NativeApiConformance.CanvasTextChecks

open System
open Ranvier.Dom

/// <summary>The engine whose documented differences the fixture expects.</summary>
[<RequireQualifiedAccess>]
type TextEngine =
    | WebKit
    | Chromium

/// <summary>
/// Checks the text drawing styles with their validation, and measureText over a 20px Ahem face served at
/// <c>fontUrl</c> after the font set reports it loaded; the result is the assertion count. The expected values are
/// Chromium 156's and WebKit's (Ranvier.WebCore docs/canvas-text-evidence): WebKit reports the hanging baseline at the
/// ascent and an inherited direction resolved, Chromium at 0.8 of the ascent and as inherit, without emHeight members.
/// Actual bounding boxes are compared within 1px. Pixels are checked by the native canvas_text probe.
/// </summary>
let run
    (create: unit -> CanvasContextService)
    (document: Document)
    (fonts: unit -> FontSet)
    (fontUrl: string)
    (engine: TextEngine)
    (wait: int -> Async<unit>)
    =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if actual <> expected then
                failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

        let near label (expected: float) (actual: float) tolerance =
            checks <- checks + 1

            if abs (actual - expected) > tolerance then
                failwith (String.Concat ([| label; ": expected "; string expected; ", got "; string actual |]))

        use service = create ()
        use set = fonts ()
        let style = document.createElement "style"

        style.textContent <-
            "@font-face{font-family:RvAhem;src:url("
            + fontUrl
            + ")}"

        document.head.appendChild style |> ignore

        let element = unbox<HTMLCanvasElement>(document.createElement "canvas")
        element.width <- 120u
        element.height <- 60u
        document.body.appendChild element |> ignore

        let ctx =
            match service.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
            | CanvasContextOutcome.Context value -> value
            | _ -> failwith "no 2D context for the text fixture"

        // Defaults.
        equal "default font" "10px sans-serif" ctx.font
        equal "default textAlign" CanvasTextAlign.Start ctx.textAlign
        equal "default textBaseline" CanvasTextBaseline.Alphabetic ctx.textBaseline

        equal
            "default direction"
            (if engine = TextEngine.WebKit then
                 CanvasDirection.Ltr
             else
                 CanvasDirection.Inherit)
            ctx.direction

        equal "default letterSpacing" "0px" ctx.letterSpacing
        equal "default wordSpacing" "0px" ctx.wordSpacing

        // Readiness of the face through the font set; the wait counts no assertion.
        let mutable loaded = None

        use _operation =
            set.Load ("20px RvAhem", " ", (fun outcome -> loaded <- Some outcome))

        let mutable turns = 0

        while loaded.IsNone do
            turns <- turns + 1

            if turns > 2000 then
                failwith "Timed out: Ahem load"

            do! wait 5

        equal
            "Ahem load"
            "Succeeded 1"
            (match loaded.Value with
             | ResourceOutcome.Succeeded faces -> "Succeeded " + string faces
             | ResourceOutcome.Failed _ -> "Failed")

        // Setters and their validation.
        ctx.font <- "20px RvAhem"
        equal "font" "20px RvAhem" ctx.font
        ctx.font <- "bogus"
        equal "invalid font is ignored" "20px RvAhem" ctx.font
        ctx.font <- "italic bold 20px/30px RvAhem"
        equal "font shorthand drops the line height" "italic bold 20px RvAhem" ctx.font
        ctx.textAlign <- CanvasTextAlign.Center
        equal "textAlign" CanvasTextAlign.Center ctx.textAlign
        ctx.textBaseline <- CanvasTextBaseline.Top
        equal "textBaseline" CanvasTextBaseline.Top ctx.textBaseline
        ctx.direction <- CanvasDirection.Rtl
        equal "direction" CanvasDirection.Rtl ctx.direction
        ctx.letterSpacing <- "2px"
        ctx.letterSpacing <- "wide"
        equal "invalid letterSpacing is ignored" "2px" ctx.letterSpacing
        ctx.wordSpacing <- "3px"
        ctx.wordSpacing <- "3"
        equal "unitless wordSpacing is ignored" "3px" ctx.wordSpacing
        ctx.reset ()
        equal "reset font" "10px sans-serif" ctx.font
        equal "reset letterSpacing" "0px" ctx.letterSpacing

        // Metrics of 20px Ahem.
        ctx.font <- "20px RvAhem"

        let metrics label (measured: TextMeasurement) width left right ascent descent shift =
            equal (label + " width") width measured.Width
            near (label + " actualBoundingBoxLeft") left measured.ActualBoundingBoxLeft 1.0
            near (label + " actualBoundingBoxRight") right measured.ActualBoundingBoxRight 1.0
            equal (label + " fontBoundingBoxAscent") (16.0 - shift) measured.FontBoundingBoxAscent
            equal (label + " fontBoundingBoxDescent") (4.0 + shift) measured.FontBoundingBoxDescent
            near (label + " actualBoundingBoxAscent") (ascent - shift) measured.ActualBoundingBoxAscent 1.0
            near (label + " actualBoundingBoxDescent") (descent + shift) measured.ActualBoundingBoxDescent 1.0
            equal (label + " alphabeticBaseline") (-shift) measured.AlphabeticBaseline
            equal (label + " ideographicBaseline") (-4.0 - shift) measured.IdeographicBaseline

            match engine with
            | TextEngine.WebKit ->
                equal (label + " emHeightAscent") (Some (16.0 - shift)) measured.EmHeightAscent
                equal (label + " emHeightDescent") (Some (4.0 + shift)) measured.EmHeightDescent
                equal (label + " hangingBaseline") (16.0 - shift) measured.HangingBaseline
            | TextEngine.Chromium ->
                equal (label + " emHeightAscent") None measured.EmHeightAscent
                equal (label + " emHeightDescent") None measured.EmHeightDescent
                near (label + " hangingBaseline") (12.8 - shift) measured.HangingBaseline 0.001

        metrics "XXX" (CanvasText.measure ctx "XXX") 60.0 0.0 60.0 16.0 4.0 0.0
        metrics "p" (CanvasText.measure ctx "p") 20.0 0.0 20.0 0.0 4.0 0.0
        ctx.textAlign <- CanvasTextAlign.Center
        metrics "centred XX" (CanvasText.measure ctx "XX") 40.0 20.0 20.0 16.0 4.0 0.0
        ctx.textAlign <- CanvasTextAlign.Start
        ctx.textBaseline <- CanvasTextBaseline.Middle
        metrics "middle X" (CanvasText.measure ctx "X") 20.0 0.0 20.0 16.0 4.0 6.0
        ctx.textBaseline <- CanvasTextBaseline.Alphabetic
        ctx.wordSpacing <- "7px"
        equal "word-spaced width" 67.0 (CanvasText.measure ctx "X X").Width
        ctx.wordSpacing <- "0px"
        ctx.letterSpacing <- "5px"
        equal "letter-spaced width" 75.0 (CanvasText.measure ctx "XXX").Width
        ctx.letterSpacing <- "0px"
        ctx.direction <- CanvasDirection.Rtl
        metrics "rtl XX" (CanvasText.measure ctx "XX") 40.0 40.0 0.0 16.0 4.0 0.0

        // Drawing calls with and without maxWidth.
        ctx.fillText ("X", 10.0, 30.0)
        ctx.fillText ("XXXX", 0.0, 30.0, 40.0)
        ctx.strokeText ("X", 10.0, 30.0)
        ctx.strokeText ("X", 10.0, 30.0, 0.0)
        equal "measure after drawing" 20.0 (CanvasText.measure ctx "X").Width
        style.remove ()
        element.remove ()
        return checks
    }
