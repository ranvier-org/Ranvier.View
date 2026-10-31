module internal NativeApiConformance.CanvasPathChecks

open System
open Ranvier.Dom

/// <summary>
/// Checks the current path and Path2D construction, reuse, transformed addPath, fill rules, curve and rounded-rectangle
/// hit tests, isPointInStroke and argument errors on one provider; the result is the assertion count. The expected
/// values are Chromium 156's (Ranvier.WebCore docs/canvas-paths-evidence/red-chromium.json). Pixels are checked by the
/// native canvas_paths probe and the Chromium oracle, since R02 exposes no pixel read-back.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>IndexSizeError</c>.</param>
let run (create: unit -> CanvasContextService) (document: Document) (errorName: exn -> string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (String.Concat ([| label; ": expected "; string (box expected); ", got "; string (box actual) |]))

    let raises label (name: string) (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                None
            with error ->
                Some (errorName error)

        match raised with
        | Some actual when actual = name -> ()
        | Some actual -> failwith (String.Concat ([| label; ": expected "; name; ", got "; actual |]))
        | None -> failwith (label + ": no error")

    use service = create ()
    let element = unbox<HTMLCanvasElement> (document.createElement "canvas")
    element.width <- 60u
    element.height <- 40u
    document.body.appendChild element |> ignore

    let ctx =
        match service.GetContext (element, CanvasContextType.TwoD, Canvas2DSettings.Default) with
        | CanvasContextOutcome.Context value -> value
        | _ -> failwith "no 2D context for the path fixture"

    // The current path.
    ctx.beginPath ()
    ctx.moveTo (10.0, 10.0)
    ctx.lineTo (50.0, 10.0)
    ctx.lineTo (30.0, 30.0)
    ctx.closePath ()
    ctx.fill ()
    equal "inside the triangle" true (ctx.isPointInPath (30.0, 15.0))
    equal "outside the triangle" false (ctx.isPointInPath (5.0, 35.0))
    equal "on the triangle's edge" true (ctx.isPointInStroke (30.0, 10.0))
    equal "inside, off the stroke" false (ctx.isPointInStroke (30.0, 15.0))
    ctx.beginPath ()
    equal "beginPath empties the path" false (ctx.isPointInPath (30.0, 15.0))

    // Path2D reuse and a translated copy.
    let square = service.CreatePath ()
    square.rect (0.0, 0.0, 10.0, 10.0)
    ctx.fill square
    let moved = service.CreatePath ()
    moved.addPath (square, DOMMatrix2DInit (e = Some 40.0, f = Some 0.0))
    ctx.fill (moved, CanvasFillRule.Nonzero)
    equal "the square" true (ctx.isPointInPath (square, 5.0, 5.0))
    equal "the moved square at the origin" false (ctx.isPointInPath (moved, 5.0, 5.0))
    equal "the moved square" true (ctx.isPointInPath (moved, 45.0, 5.0))
    equal "addPath leaves the source" false (ctx.isPointInPath (square, 45.0, 5.0))
    let identity = service.CreatePath ()
    identity.addPath square
    equal "addPath without a transform" true (ctx.isPointInPath (identity, 5.0, 5.0))

    // Path data and copies.
    let data = service.ParsePath "M20 20 h10 v10 h-10 Z"
    let copy = service.CopyPath data
    data.rect (50.0, 30.0, 5.0, 5.0)
    equal "inside the path data" true (ctx.isPointInPath (data, 25.0, 25.0))
    equal "outside the path data" false (ctx.isPointInPath (data, 35.0, 25.0))
    equal "inside the copy" true (ctx.isPointInPath (copy, 25.0, 25.0))
    equal "the copy misses a later rect" false (ctx.isPointInPath (copy, 52.0, 32.0))
    equal "the original has the later rect" true (ctx.isPointInPath (data, 52.0, 32.0))
    equal "invalid path data is an empty path" false (ctx.isPointInPath (service.ParsePath "M 0 0 L bogus", 0.0, 0.0))

    // Fill rules.
    let nested = service.CreatePath ()
    nested.rect (0.0, 20.0, 20.0, 20.0)
    nested.rect (5.0, 25.0, 10.0, 10.0)
    equal "nonzero hole" true (ctx.isPointInPath (nested, 10.0, 30.0))
    equal "evenodd hole" false (ctx.isPointInPath (nested, 10.0, 30.0, CanvasFillRule.Evenodd))
    equal "evenodd ring" true (ctx.isPointInPath (nested, 2.0, 30.0, CanvasFillRule.Evenodd))
    ctx.fill (nested, CanvasFillRule.Evenodd)

    // Shapes on the current path.
    let hits (build: unit -> unit) (points: (float * float) list) =
        ctx.beginPath ()
        build ()
        points |> List.map (fun (x, y) -> ctx.isPointInPath (x, y))

    equal "arc" [ true; false; true ] (hits (fun () -> ctx.arc (30.0, 20.0, 10.0, 0.0, Math.PI * 2.0)) [ 30.0, 20.0; 30.0, 32.0; 37.0, 20.0 ])
    equal "half arc" [ true; false ] (hits (fun () -> ctx.arc (30.0, 20.0, 10.0, 0.0, Math.PI)) [ 30.0, 25.0; 30.0, 15.0 ])
    equal "anticlockwise arc" [ false; true ] (hits (fun () -> ctx.arc (30.0, 20.0, 10.0, 0.0, Math.PI, true)) [ 30.0, 25.0; 30.0, 15.0 ])

    equal
        "ellipse"
        [ true; true; false ]
        (hits (fun () -> ctx.ellipse (30.0, 20.0, 20.0, 5.0, 0.0, 0.0, Math.PI * 2.0)) [ 45.0, 20.0; 30.0, 24.0; 30.0, 27.0 ])

    equal
        "rotated ellipse"
        [ false; true ]
        (hits (fun () -> ctx.ellipse (30.0, 20.0, 20.0, 5.0, Math.PI / 2.0, 0.0, Math.PI * 2.0)) [ 45.0, 20.0; 30.0, 35.0 ])

    equal
        "quadratic"
        [ true; false ]
        (hits
            (fun () ->
                ctx.moveTo (0.0, 0.0)
                ctx.quadraticCurveTo (30.0, 40.0, 60.0, 0.0)
                ctx.closePath ())
            [ 30.0, 10.0; 30.0, 25.0 ])

    equal
        "bezier"
        [ true; false ]
        (hits
            (fun () ->
                ctx.moveTo (0.0, 0.0)
                ctx.bezierCurveTo (0.0, 40.0, 60.0, 40.0, 60.0, 0.0)
                ctx.closePath ())
            [ 30.0, 20.0; 30.0, 35.0 ])

    equal
        "arcTo"
        [ true; false; true ]
        (hits
            (fun () ->
                ctx.moveTo (0.0, 0.0)
                ctx.arcTo (40.0, 0.0, 40.0, 40.0, 20.0)
                ctx.lineTo (40.0, 40.0)
                ctx.lineTo (0.0, 40.0)
                ctx.closePath ())
            [ 10.0, 10.0; 38.0, 2.0; 25.0, 5.0 ])

    equal "rect" [ true; false ] (hits (fun () -> ctx.rect (10.0, 10.0, 20.0, 10.0)) [ 15.0, 15.0; 35.0, 15.0 ])

    equal
        "roundRect list"
        [ false; true; true ]
        (hits (fun () -> ctx.roundRect (0.0, 0.0, 40.0, 40.0, [| UnrestrictedDoubleOrDOMPointInit.UnrestrictedDouble 10.0 |])) [ 1.0, 1.0; 20.0, 20.0; 5.0, 20.0 ])

    equal "roundRect value" [ false; true ] (hits (fun () -> ctx.roundRect (0.0, 0.0, 40.0, 40.0, 10.0)) [ 1.0, 1.0; 20.0, 20.0 ])

    equal
        "roundRect point"
        [ true; false; true ]
        (hits (fun () -> ctx.roundRect (0.0, 0.0, 40.0, 40.0, DOMPointInit (x = 20.0, y = 5.0))) [ 2.0, 10.0; 2.0, 2.0; 20.0, 20.0 ])

    equal
        "roundRect four radii"
        [ true; true; false ]
        (hits
            (fun () ->
                ctx.roundRect (
                    0.0,
                    0.0,
                    40.0,
                    40.0,
                    [| 0.0; 0.0; 0.0; 20.0 |] |> Array.map UnrestrictedDoubleOrDOMPointInit.UnrestrictedDouble
                ))
            [ 1.0, 1.0; 39.0, 39.0; 1.0, 39.0 ])

    // Errors.
    raises "arc with a negative radius" "IndexSizeError" (fun () -> ctx.arc (0.0, 0.0, -1.0, 0.0, 1.0))
    raises "ellipse with a negative radius" "IndexSizeError" (fun () -> ctx.ellipse (0.0, 0.0, -1.0, 1.0, 0.0, 0.0, 1.0))
    raises "arcTo with a negative radius" "IndexSizeError" (fun () -> ctx.arcTo (0.0, 0.0, 1.0, 1.0, -1.0))

    raises "roundRect with a negative radius" "RangeError" (fun () ->
        ctx.roundRect (0.0, 0.0, 10.0, 10.0, [| UnrestrictedDoubleOrDOMPointInit.UnrestrictedDouble -1.0 |]))

    raises "roundRect with no radii" "RangeError" (fun () -> ctx.roundRect (0.0, 0.0, 10.0, 10.0, [||]))
    raises "addPath with conflicting members" "TypeError" (fun () -> service.CreatePath().addPath (square, DOMMatrix2DInit (a = Some 1.0, m11 = Some 2.0)))

    // Non-finite values.
    ctx.beginPath ()
    ctx.rect (0.0, 0.0, 10.0, 10.0)
    ctx.lineTo (nan, 5.0)
    ctx.moveTo (infinity, 0.0)
    equal "non-finite segments are ignored" true (ctx.isPointInPath (5.0, 5.0))
    equal "a NaN point is outside" false (ctx.isPointInPath (nan, 5.0))
    equal "a NaN point is off the stroke" false (ctx.isPointInStroke (5.0, nan))

    // Stroke on a Path2D.
    let line = service.CreatePath ()
    line.moveTo (0.0, 5.5)
    line.lineTo (60.0, 5.5)
    ctx.stroke line
    ctx.strokeRect (10.5, 10.5, 20.0, 20.0)
    equal "on the line" true (ctx.isPointInStroke (line, 30.0, 5.5))
    equal "off the line" false (ctx.isPointInStroke (line, 30.0, 8.0))

    // Clips.
    element.width <- 60u
    ctx.clip square
    ctx.beginPath ()
    ctx.rect (0.0, 0.0, 30.0, 40.0)
    ctx.clip CanvasFillRule.Nonzero
    ctx.stroke ()
    element.remove ()
    checks
