module internal NativeApiConformance.CanvasPaintChecks

open System
open Ranvier.Dom

/// <summary>
/// Checks line, dash, shadow and smoothing styles with their validation, gradient and pattern creation, addColorStop
/// and createPattern errors, and gradient and pattern paint styles on one provider; the result is the assertion count.
/// The expected values are Chromium 156's (Ranvier.WebCore docs/canvas-paints-evidence/red-chromium.json). Pixels are
/// checked by the native canvas_paints probe.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>IndexSizeError</c>.</param>
let run (create: unit -> CanvasContextService) (document: Document) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

    let raises label (name: string) (action: unit -> unit) =
        let raised =
            try
                action ()
                "no error"
            with error ->
                errorName error

        equal label name raised

    use service = create ()

    let canvas width height =
        let element = unbox<HTMLCanvasElement> (document.createElement "canvas")
        element.width <- width
        element.height <- height
        document.body.appendChild element |> ignore
        element

    let context element =
        match service.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
        | CanvasContextOutcome.Context value -> value
        | _ -> failwith "no 2D context for the paint fixture"

    let element = canvas 60u 40u
    let source = canvas 4u 4u
    let ctx = context element
    let sourceContext = context source
    sourceContext.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#ff0000"
    sourceContext.fillRect (0.0, 0.0, 2.0, 4.0)

    let text style =
        match style with
        | DOMStringOrCanvasGradientOrCanvasPattern.DOMString value -> value
        | DOMStringOrCanvasGradientOrCanvasPattern.CanvasGradient _ -> "gradient"
        | DOMStringOrCanvasGradientOrCanvasPattern.CanvasPattern _ -> "pattern"

    // Defaults.
    equal "default strokeStyle" "#000000" (text ctx.strokeStyle)
    equal "default line" (1.0, CanvasLineCap.Butt, CanvasLineJoin.Miter, 10.0) (ctx.lineWidth, ctx.lineCap, ctx.lineJoin, ctx.miterLimit)
    equal "default dash" (0, 0.0) (ctx.getLineDash().Length, ctx.lineDashOffset)
    equal "default shadow" (0.0, 0.0, 0.0, "rgba(0, 0, 0, 0)") (ctx.shadowOffsetX, ctx.shadowOffsetY, ctx.shadowBlur, ctx.shadowColor)
    equal "default smoothing" (true, ImageSmoothingQuality.Low) (ctx.imageSmoothingEnabled, ctx.imageSmoothingQuality)

    // Validation.
    for value in [ 4.0; 0.0; -1.0; nan; infinity ] do
        ctx.lineWidth <- value

    equal "lineWidth keeps the last valid value" 4.0 ctx.lineWidth

    for value in [ 3.0; 0.0; -2.0 ] do
        ctx.miterLimit <- value

    equal "miterLimit keeps the last valid value" 3.0 ctx.miterLimit
    ctx.lineCap <- CanvasLineCap.Round
    ctx.lineJoin <- CanvasLineJoin.Bevel
    equal "cap and join" (CanvasLineCap.Round, CanvasLineJoin.Bevel) (ctx.lineCap, ctx.lineJoin)
    ctx.setLineDash [| 4.0; 2.0 |]
    equal "setLineDash" [| 4.0; 2.0 |] (ctx.getLineDash ())
    ctx.setLineDash [| 1.0; 2.0; 3.0 |]
    equal "an odd dash list repeats" [| 1.0; 2.0; 3.0; 1.0; 2.0; 3.0 |] (ctx.getLineDash ())
    ctx.setLineDash [| 1.0; -1.0 |]
    ctx.setLineDash [| 1.0; nan |]
    equal "invalid dash lists are ignored" [| 1.0; 2.0; 3.0; 1.0; 2.0; 3.0 |] (ctx.getLineDash ())
    ctx.lineDashOffset <- 1.5
    ctx.lineDashOffset <- nan
    equal "lineDashOffset" 1.5 ctx.lineDashOffset
    ctx.shadowBlur <- 3.0
    ctx.shadowBlur <- -1.0
    equal "shadowBlur" 3.0 ctx.shadowBlur
    ctx.shadowColor <- "rgba(0, 0, 255, 0.5)"
    ctx.shadowColor <- "nope"
    equal "shadowColor" "rgba(0, 0, 255, 0.5)" ctx.shadowColor
    ctx.imageSmoothingEnabled <- false
    ctx.imageSmoothingQuality <- ImageSmoothingQuality.High
    equal "smoothing" (false, ImageSmoothingQuality.High) (ctx.imageSmoothingEnabled, ctx.imageSmoothingQuality)
    ctx.strokeStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#00ff00"
    ctx.strokeStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "bogus"
    equal "strokeStyle" "#00ff00" (text ctx.strokeStyle)
    ctx.reset ()
    equal "reset restores the line width" 1.0 ctx.lineWidth

    // Gradients.
    let linear = ctx.createLinearGradient (0.0, 0.0, 60.0, 0.0)
    linear.addColorStop (0.0, "#ff0000")
    linear.addColorStop (1.0, "#0000ff")
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.CanvasGradient linear
    equal "fillStyle holds the gradient" "gradient" (text ctx.fillStyle)
    ctx.fillRect (0.0, 0.0, 60.0, 10.0)
    let radial = ctx.createRadialGradient (30.0, 20.0, 0.0, 30.0, 20.0, 10.0)
    radial.addColorStop (0.5, "lime")
    let conic = ctx.createConicGradient (0.0, 30.0, 35.0)
    conic.addColorStop (0.25, "blue")
    ctx.strokeStyle <- DOMStringOrCanvasGradientOrCanvasPattern.CanvasGradient conic
    equal "strokeStyle holds the gradient" "gradient" (text ctx.strokeStyle)
    raises "addColorStop below 0" "IndexSizeError" (fun () -> linear.addColorStop (-0.1, "red"))
    raises "addColorStop above 1" "IndexSizeError" (fun () -> linear.addColorStop (1.1, "red"))
    raises "addColorStop NaN" "TypeError" (fun () -> linear.addColorStop (nan, "red"))
    raises "addColorStop colour" "SyntaxError" (fun () -> linear.addColorStop (0.5, "not a colour"))
    raises "createLinearGradient NaN" "TypeError" (fun () -> ctx.createLinearGradient (nan, 0.0, 1.0, 1.0) |> ignore)
    raises "createRadialGradient negative radius" "IndexSizeError" (fun () -> ctx.createRadialGradient (0.0, 0.0, -1.0, 0.0, 0.0, 1.0) |> ignore)
    raises "createConicGradient Infinity" "TypeError" (fun () -> ctx.createConicGradient (infinity, 0.0, 0.0) |> ignore)

    // Patterns.
    let repeat = ctx.createPattern (source, "repeat")
    equal "createPattern(canvas)" false (isNull (box repeat))
    equal "an empty repetition means repeat" false (isNull (box (ctx.createPattern (source, ""))))

    for repetition in [ "repeat-x"; "repeat-y"; "no-repeat" ] do
        equal ("createPattern " + repetition) false (isNull (box (ctx.createPattern (source, repetition))))

    raises "createPattern bad repetition" "SyntaxError" (fun () -> ctx.createPattern (source, "diagonal") |> ignore)
    repeat.setTransform (DOMMatrix2DInit (a = Some 4.0, d = Some 4.0))
    repeat.setTransform ()
    raises "CanvasPattern.setTransform conflict" "TypeError" (fun () -> repeat.setTransform (DOMMatrix2DInit (a = Some 1.0, m11 = Some 2.0)))
    let empty = canvas 0u 4u
    raises "createPattern zero-size canvas" "InvalidStateError" (fun () -> ctx.createPattern (empty, "repeat") |> ignore)
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.CanvasPattern repeat
    equal "fillStyle holds the pattern" "pattern" (text ctx.fillStyle)
    ctx.fillRect (0.0, 0.0, 20.0, 8.0)
    element.remove ()
    source.remove ()
    empty.remove ()
    checks
