module internal NativeApiConformance.CanvasStateChecks

open System
open Ranvier.Dom

/// <summary>
/// Checks transforms and getTransform, setTransform with a dictionary, save/restore of styles, transforms and clips,
/// reset, globalAlpha validation, the composite operation names and the filter string on one provider; the result is
/// the assertion count. The expected values are Chromium 156's (Ranvier.WebCore docs/canvas-state-evidence/red-chromium.json).
/// Pixels, canvas layers and WebKit's own composite names are checked by the native canvas_state probe.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>TypeError</c>.</param>
let run (create: unit -> CanvasContextService) (document: Document) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

    let round (value: float) = Math.Round (value, 6) + 0.0

    let matrix (ctx: CanvasRenderingContext2D) =
        let m = ctx.getTransform ()
        [ round m.a; round m.b; round m.c; round m.d; round m.e; round m.f ], m.is2D, m.isIdentity

    use service = create ()
    let element = unbox<HTMLCanvasElement> (document.createElement "canvas")
    element.width <- 60u
    element.height <- 40u
    document.body.appendChild element |> ignore

    let ctx =
        match service.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
        | CanvasContextOutcome.Context value -> value
        | _ -> failwith "no 2D context for the state fixture"

    let fillText () =
        match ctx.fillStyle with
        | DOMStringOrCanvasGradientOrCanvasPattern.DOMString value -> value
        | _ -> "(not a string)"

    equal "default transform" ([ 1.0; 0.0; 0.0; 1.0; 0.0; 0.0 ], true, true) (matrix ctx)
    equal "default globalAlpha" 1.0 ctx.globalAlpha
    equal "default composite" "source-over" ctx.globalCompositeOperation
    equal "default filter" "none" ctx.filter

    // Transforms.
    ctx.translate (10.0, 5.0)
    equal "translate" ([ 1.0; 0.0; 0.0; 1.0; 10.0; 5.0 ], true, false) (matrix ctx)
    ctx.scale (2.0, 3.0)
    equal "scale" ([ 2.0; 0.0; 0.0; 3.0; 10.0; 5.0 ], true, false) (matrix ctx)
    ctx.resetTransform ()
    ctx.rotate (Math.PI / 2.0)
    equal "rotate" ([ 0.0; 1.0; -1.0; 0.0; 0.0; 0.0 ], true, false) (matrix ctx)
    ctx.setTransform (1.0, 0.0, 0.0, 1.0, 0.0, 0.0)
    ctx.transform (1.0, 0.0, 0.0, 1.0, 5.0, 5.0)
    ctx.transform (2.0, 0.0, 0.0, 2.0, 0.0, 0.0)
    equal "transform" ([ 2.0; 0.0; 0.0; 2.0; 5.0; 5.0 ], true, false) (matrix ctx)
    ctx.setTransform (DOMMatrix2DInit (a = Some 1.0, b = Some 0.0, c = Some 0.0, d = Some 1.0, e = Some 7.0, f = Some 8.0))
    equal "setTransform(init)" ([ 1.0; 0.0; 0.0; 1.0; 7.0; 8.0 ], true, false) (matrix ctx)
    ctx.setTransform (DOMMatrix2DInit (m41 = Some 3.0, m42 = Some 4.0))
    equal "setTransform(m41, m42)" ([ 1.0; 0.0; 0.0; 1.0; 3.0; 4.0 ], true, false) (matrix ctx)
    ctx.setTransform ()
    equal "setTransform()" ([ 1.0; 0.0; 0.0; 1.0; 0.0; 0.0 ], true, true) (matrix ctx)

    let conflict =
        try
            ctx.setTransform (DOMMatrix2DInit (a = Some 1.0, m11 = Some 2.0))
            "no error"
        with error ->
            errorName error

    equal "setTransform conflicting members" "TypeError" conflict
    ctx.translate (nan, 1.0)
    ctx.scale (infinity, 1.0)
    ctx.setTransform (nan, 0.0, 0.0, 1.0, 0.0, 0.0)
    equal "non-finite transform calls are ignored" ([ 1.0; 0.0; 0.0; 1.0; 0.0; 0.0 ], true, true) (matrix ctx)
    let held = ctx.getTransform ()
    ctx.translate (1.0, 1.0)
    equal "getTransform returns a copy" 0.0 held.e

    // The current path keeps its construction transform; a Path2D hit test applies the current one.
    ctx.setTransform (1.0, 0.0, 0.0, 1.0, 40.0, 0.0)
    ctx.beginPath ()
    ctx.rect (0.0, 0.0, 10.0, 10.0)
    ctx.resetTransform ()
    equal "current path under its construction transform" (true, false) (ctx.isPointInPath (45.0, 5.0), ctx.isPointInPath (5.0, 5.0))
    let square = service.CreatePath ()
    square.rect (0.0, 0.0, 10.0, 10.0)
    ctx.translate (40.0, 0.0)

    equal
        "Path2D under the current transform"
        (true, false)
        (ctx.isPointInPath (square, 45.0, 5.0), ctx.isPointInPath (square, 5.0, 5.0))

    ctx.resetTransform ()

    // save/restore.
    element.width <- 60u
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#0000ff"
    ctx.globalAlpha <- 0.5
    ctx.translate (5.0, 5.0)
    ctx.save ()
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#00ff00"
    ctx.globalAlpha <- 1.0
    ctx.translate (10.0, 0.0)
    ctx.globalCompositeOperation <- "copy"
    ctx.save ()
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#ff0000"
    ctx.restore ()
    equal "nested restore" "#00ff00" (fillText ())
    ctx.restore ()

    equal
        "restored state"
        ("#0000ff", 0.5, "source-over", ([ 1.0; 0.0; 0.0; 1.0; 5.0; 5.0 ], true, false))
        (fillText (), ctx.globalAlpha, ctx.globalCompositeOperation, matrix ctx)

    ctx.restore ()
    equal "an unmatched restore" "#0000ff" (fillText ())

    // reset().
    ctx.save ()
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#123456"
    ctx.globalAlpha <- 0.25
    ctx.globalCompositeOperation <- "xor"
    ctx.beginPath ()
    ctx.rect (0.0, 0.0, 10.0, 10.0)
    ctx.reset ()

    equal
        "reset"
        ("#000000", 1.0, "source-over", ([ 1.0; 0.0; 0.0; 1.0; 0.0; 0.0 ], true, true), false)
        (fillText (), ctx.globalAlpha, ctx.globalCompositeOperation, matrix ctx, ctx.isPointInPath (5.0, 5.0))

    ctx.restore ()
    equal "restore after reset" "#000000" (fillText ())

    // globalAlpha.
    ctx.globalAlpha <- 0.5

    for value in [ 2.0; -1.0; nan; infinity ] do
        ctx.globalAlpha <- value

    equal "out-of-range globalAlpha is ignored" 0.5 ctx.globalAlpha
    ctx.globalAlpha <- 1.0

    // Composite operations.
    let names =
        [
            "source-over"; "source-in"; "source-out"; "source-atop"; "destination-over"; "destination-in"; "destination-out"
            "destination-atop"; "lighter"; "copy"; "xor"; "multiply"; "screen"; "overlay"; "darken"; "lighten"; "color-dodge"
            "color-burn"; "hard-light"; "soft-light"; "difference"; "exclusion"; "hue"; "saturation"; "color"; "luminosity"
        ]

    let accepted =
        names
        |> List.filter (fun name ->
            ctx.globalCompositeOperation <- name
            ctx.globalCompositeOperation = name)

    equal "every composite operation" names accepted
    ctx.globalCompositeOperation <- "multiply"

    for invalid in [ "bogus"; "Copy"; "" ] do
        ctx.globalCompositeOperation <- invalid

    equal "invalid composite operations are ignored" "multiply" ctx.globalCompositeOperation

    // filter.
    ctx.filter <- "blur(2px)"
    equal "filter blur" "blur(2px)" ctx.filter
    ctx.filter <- "not a filter"
    equal "an invalid filter is ignored" "blur(2px)" ctx.filter
    ctx.filter <- "none"
    element.remove ()
    checks
