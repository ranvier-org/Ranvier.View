module internal NativeApiConformance.SvgChecks

open System
open Ranvier.Dom

/// <summary>An engine pixel reader over viewport coordinates, returning red, green and blue bytes.</summary>
type Pixel = float * float -> Async<int * int * int>

/// <summary>Counts assertions shared by the SVG paint, filter, clip and resource fixtures.</summary>
type Checks(errorName: exn -> string) =
    let mutable count = 0

    member _.Count = count

    member _.Equal (label: string) (expected: 'T) (actual: 'T) =
        count <- count + 1

        if expected <> actual then
            failwithf "%s: expected %A, got %A" label expected actual

    member _.Near (label: string) (expected: float) (actual: float) =
        count <- count + 1

        if
            Double.IsNaN actual
            || Double.IsInfinity actual
            || abs (expected - actual) > 0.001
        then
            failwithf "%s: expected %g, got %g" label expected actual

    member this.Fails (label: string) (expected: string) (action: unit -> unit) =
        let outcome =
            try
                action ()
                "accepted"
            with error ->
                errorName error

        this.Equal label expected outcome

    /// <summary>Asserts the pixel matches <c>expected</c> within 24 per channel.</summary>
    member _.Color (label: string) (expected: int * int * int) (actual: int * int * int) =
        count <- count + 1
        let r, g, b = expected
        let ar, ag, ab = actual

        if
            abs (r - ar) > 24
            || abs (g - ag) > 24
            || abs (b - ab) > 24
        then
            failwithf "%s: expected rgb%A, got rgb%A" label expected actual

/// <summary>Creates an SVG element with authored attributes.</summary>
let create<'T> (document: Document) (name: string) (attributes: (string * string) list) : 'T =
    let element = document.createElementNS ("http://www.w3.org/2000/svg", name)

    for key, value in attributes do
        element.setAttribute (key, value)

    unbox<'T> element

/// <summary>Reads the pixel at root-local coordinates of an unscaled root.</summary>
let at (pixel: Pixel) (root: SVGSVGElement) (x: float, y: float) =
    let origin = root.getBoundingClientRect ()
    pixel (origin.x + x, origin.y + y)

let red = 255, 0, 0
let lime = 0, 255, 0
let blue = 0, 0, 255
let white = 255, 255, 255
let black = 0, 0, 0
