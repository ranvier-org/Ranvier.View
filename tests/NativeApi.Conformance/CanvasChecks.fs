module internal NativeApiConformance.CanvasChecks

open System
open Ranvier.Dom

/// <summary>
/// Checks canvas context creation, identity, the supported profile, getContextAttributes, fillStyle serialisation and
/// the reset a dimension assignment performs, on one provider; the result is the assertion count. The expected values
/// are Chromium 156's (Ranvier.WebCore docs/canvas-evidence/red-chromium.json). Pixels are checked by the native
/// canvas_context probe and the Chromium oracle, since R01 exposes no pixel read-back.
/// </summary>
let run (create: unit -> CanvasContextService) (document: Document) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

    let describe (value: Canvas2DSettings) =
        String.Join (",", [| string value.Alpha; string value.Desynchronized; string value.WillReadFrequently; string (int value.ColorSpace); string (int value.ColorType) |])

    let settings label (expected: Canvas2DSettings) (actual: Canvas2DSettings) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; describe expected; ", got "; describe actual |]))

    let same label (expected: obj) (actual: obj) =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith (label + ": a different object")

    let unsupported label outcome =
        checks <- checks + 1

        match outcome with
        | CanvasContextOutcome.Unsupported _ -> ()
        | CanvasContextOutcome.OtherContext -> failwith (label + ": OtherContext instead of Unsupported")
        | CanvasContextOutcome.Context _ -> failwith (label + ": a context instead of Unsupported")

    let context label outcome =
        checks <- checks + 1

        match outcome with
        | CanvasContextOutcome.Context value -> value
        | CanvasContextOutcome.OtherContext -> failwith (label + ": OtherContext")
        | CanvasContextOutcome.Unsupported reason -> failwith (label + ": Unsupported " + reason)

    let style (target: CanvasRenderingContext2D) =
        match target.fillStyle with
        | DOMStringOrCanvasGradientOrCanvasPattern.DOMString value -> value
        | _ -> "(not a string)"

    let canvas (append: bool) =
        let element = unbox<HTMLCanvasElement> (document.createElement "canvas")

        if append then
            document.body.appendChild element |> ignore

        element

    use service = create ()
    let defaults = Canvas2DSettings.Default
    let first = canvas true
    equal "default width" 300u first.width
    equal "default height" 150u first.height

    for kind in [ CanvasContextType.BitmapRenderer; CanvasContextType.WebGL; CanvasContextType.WebGL2; CanvasContextType.WebGpu; enum<CanvasContextType> 99 ] do
        unsupported ("type " + string (int kind)) (service.GetContext (first, kind, defaults))

    unsupported "alpha false" (service.GetContext (first, CanvasContextType.TwoD, { defaults with Alpha = false }))
    unsupported "display-p3" (service.GetContext (first, CanvasContextType.TwoD, { defaults with ColorSpace = CanvasColorSpace.DisplayP3 }))
    unsupported "float16" (service.GetContext (first, CanvasContextType.TwoD, { defaults with ColorType = CanvasColorType.Float16 }))
    let ctx = context "first 2D context" (service.GetContext (first, CanvasContextType.TwoD, defaults))
    same "getContext returns the same context" ctx (context "second 2D request" (service.GetContext (first, CanvasContextType.TwoD, defaults)))

    same
        "later supported settings are ignored"
        ctx
        (context "third 2D request" (service.GetContext (first, CanvasContextType.TwoD, { defaults with WillReadFrequently = true })))

    unsupported "unsupported settings on an existing context" (service.GetContext (first, CanvasContextType.TwoD, { defaults with Alpha = false }))
    unsupported "WebGL on a 2D canvas" (service.GetContext (first, CanvasContextType.WebGL, defaults))
    same "context.canvas" first ctx.canvas
    settings "default attributes" defaults (service.Attributes ctx)

    equal "initial fillStyle" "#000000" (style ctx)

    for given, expected in [ "#ff0000", "#ff0000"; "not a colour", "#ff0000"; "rgba(0, 0, 255, 0.5)", "rgba(0, 0, 255, 0.5)"; "lime", "#00ff00" ] do
        ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString given
        equal ("fillStyle " + given) expected (style ctx)

    ctx.fillRect (0.0, 0.0, 20.0, 20.0)
    ctx.clearRect (5.0, 5.0, 4.0, 4.0)
    ctx.fillRect (Double.NaN, 0.0, 5.0, 5.0)
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#123456"
    first.width <- first.width
    equal "a width assignment resets fillStyle" "#000000" (style ctx)
    same "a reset keeps the context" ctx (context "2D after reset" (service.GetContext (first, CanvasContextType.TwoD, defaults)))
    ctx.fillStyle <- DOMStringOrCanvasGradientOrCanvasPattern.DOMString "#00ff00"
    first.height <- 30u
    equal "a height assignment resets fillStyle" "#000000" (style ctx)
    equal "height after assignment" 30u first.height
    first.width <- 4294967295u
    equal "a width above 2^31-1 falls back to 300" 300u first.width
    first.width <- 0u
    equal "a zero width" 0u first.width
    settings "attributes of a zero-width canvas" defaults (service.Attributes ctx)

    let reading = canvas true
    let readingContext = context "willReadFrequently context" (service.GetContext (reading, CanvasContextType.TwoD, { defaults with WillReadFrequently = true }))
    settings "requested attributes" { defaults with WillReadFrequently = true } (service.Attributes readingContext)

    let fresh = canvas true
    unsupported "WebGL on a fresh canvas" (service.GetContext (fresh, CanvasContextType.WebGL, defaults))
    context "an unsupported request leaves the canvas free" (service.GetContext (fresh, CanvasContextType.TwoD, defaults)) |> ignore

    first.remove ()
    ctx.fillRect (0.0, 0.0, 10.0, 10.0)
    same "a removed canvas keeps its context" ctx (context "2D after removal" (service.GetContext (first, CanvasContextType.TwoD, defaults)))
    document.body.appendChild first |> ignore

    let detached = canvas false
    let detachedContext = context "a never-attached canvas" (service.GetContext (detached, CanvasContextType.TwoD, defaults))
    same "the detached context's canvas" detached detachedContext.canvas
    equal "the detached canvas size" 300u detachedContext.canvas.width
    checks
