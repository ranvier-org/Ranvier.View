module internal NativeApiConformance.CoordinateChecks

open System
open Ranvier.Dom

/// <summary>The engine whose coordinate conversions the fixture expects.</summary>
type Engine =
    /// <summary>The native host: every chain converts.</summary>
    | Native
    /// <summary>Chromium: perspective chains answer <c>Unsupported</c>.</summary>
    | Chromium

let private Css =
    "#cs-o{position:absolute;left:100px;top:50px;width:200px;height:100px;box-sizing:border-box;border:3px solid;"
    + "padding:5px;margin:0;transform:rotate(30deg) scale(1.5)}"
    + "#cs-i{margin:0 0 0 20px;width:40px;height:40px;transform:translate(10px,5px) rotate(45deg)}"
    + "#cs-b{position:absolute;left:0;top:40px;width:50px;height:50px;margin:0}"
    + "#cs-m{position:absolute;left:300px;top:400px;width:60px;height:40px;border:2px solid;padding:4px 6px;margin:3px 5px 7px 9px}"
    + "#cs-z{position:absolute;left:700px;top:400px;width:20px;height:20px;transform:scale(0)}"
    + "#cs-p{position:absolute;left:400px;top:50px;width:200px;height:200px;perspective:400px}"
    + "#cs-q{width:100px;height:100px;transform:rotateY(40deg)}"
    + "#cs-s{position:absolute;left:0;top:300px;width:150px;height:100px;overflow:hidden;margin:0;border:0;padding:0}"
    + "#cs-t{margin:150px 0 0 0;width:20px;height:20px;transform:rotate(10deg)}"
    + "#cs-r{position:absolute;left:150px;top:150px;width:80px;height:50px;margin:0;transform:rotateY(-35deg) rotateX(20deg)}"
    + "#cs-n{display:none}"

let private radians (degrees: float) =
    degrees * Math.PI / 180.0

/// <summary>A 2D affine map as (a, b, c, d, e, f): x' = a x + c y + e, y' = b x + d y + f.</summary>
type private Affine = float * float * float * float * float * float

let private apply ((a, b, c, d, e, f): Affine) (p: CoordinatePoint) : CoordinatePoint =
    {
        X = a * p.X + c * p.Y + e
        Y = b * p.X + d * p.Y + f
    }

let private compose ((a1, b1, c1, d1, e1, f1): Affine) ((a2, b2, c2, d2, e2, f2): Affine) : Affine =
    a1 * a2 + c1 * b2, b1 * a2 + d1 * b2, a1 * c2 + c1 * d2, b1 * c2 + d1 * d2, a1 * e2 + c1 * f2 + e1, b1 * e2 + d1 * f2 + f1

let private translate x y : Affine =
    1.0, 0.0, 0.0, 1.0, x, y

let private linear a b c d : Affine =
    a, b, c, d, 0.0, 0.0

let private rotate degrees =
    let r = radians degrees
    linear (cos r) (sin r) (-sin r) (cos r)

/// <summary>offset, then the transform about origin, as an element's local-to-parent map.</summary>
let private level (ox, oy) (originX, originY) (transform: Affine) =
    compose (translate (ox + originX) (oy + originY)) (compose transform (translate -originX -originY))

/// <summary>Labels without generic formatting, which NativeAOT cannot print.</summary>
let private show (p: CoordinatePoint) =
    string p.X + ", " + string p.Y

let private spaceName (space: CoordinateSpace) =
    match space with
    | CoordinateSpace.Element _ -> "element"
    | CoordinateSpace.Page -> "page"
    | CoordinateSpace.Client -> "client"
    | CoordinateSpace.Host -> "host"
    | CoordinateSpace.Device -> "device"

let private boxName (cssBox: CoordinateBox) =
    match cssBox with
    | CoordinateBox.Border -> "border"
    | CoordinateBox.Padding -> "padding"
    | CoordinateBox.Content -> "content"
    | CoordinateBox.Margin -> "margin"

/// <summary>
/// Checks element-to-page maps under nested 2D and flattened 3D transforms and a scroller against maps composed from
/// the fixture's CSS, round trips through every ordered pair of spaces, client rectangles, the device ratio, box quads,
/// a singular transform, elements without a CSS box, a perspective chain, a removed element, an element of another
/// document and argument validation; the result is the assertion count.
/// </summary>
let run (create: unit -> CoordinateService) (document: Document) (foreign: unit -> Element) (devicePixelRatio: unit -> float) (engine: Engine) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"{label}: expected {expected}, got {actual}"

    let near label (expected: CoordinatePoint) (actual: CoordinatePoint) =
        checks <- checks + 1

        if
            abs (expected.X - actual.X) > 1.0 / 64.0
            || abs (expected.Y - actual.Y) > 1.0 / 64.0
        then
            failwith (
                label
                + ": expected "
                + show expected
                + ", got "
                + show actual
            )

    // Services raise ArgumentNullException, ArgumentException and ObjectDisposedException; Fable erases the types.
    let raises label (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with _ ->
                true

        if not raised then
            failwith $"{label}: expected an exception"

    let element (tag: string) (id: string) =
        let value = document.createElement tag :?> HTMLElement
        value.id <- id
        value

    let style = element "style" "cs-style"
    style.textContent <- Css
    let o = element "div" "cs-o"
    let i = element "div" "cs-i"
    o.appendChild i |> ignore
    let b = element "div" "cs-b"
    let m = element "div" "cs-m"
    let z = element "div" "cs-z"
    let p = element "div" "cs-p"
    let q = element "div" "cs-q"
    p.appendChild q |> ignore
    let s = element "div" "cs-s"
    let t = element "div" "cs-t"
    let spacer = element "div" "cs-spacer"
    spacer.style.cssText <- "height:400px"
    s.appendChild t |> ignore
    s.appendChild spacer |> ignore
    let r = element "div" "cs-r"
    let n = element "div" "cs-n"
    let k = element "span" "cs-k"
    k.textContent <- "k"

    for value in [ style; o; b; m; z; p; s; r; n; k ] do
        document.body.appendChild value |> ignore

    s.scrollTop <- 40
    let service = create ()

    let point x y : CoordinatePoint =
        { X = x; Y = y }

    let ok label =
        function
        | Ok value -> value
        | Error reason -> failwith $"{label}: {reason}"

    let convert label value from target =
        service.Convert (value, from, target) |> ok label

    try
        let element (value: Element) =
            CoordinateSpace.Element value
        // Local-to-page maps composed from the CSS.
        let oMap =
            level (100.0, 50.0) (100.0, 50.0) (compose (rotate 30.0) (linear 1.5 0.0 0.0 1.5))

        let iMap =
            compose oMap (level (28.0, 8.0) (20.0, 20.0) (compose (translate 10.0 5.0) (rotate 45.0)))

        let tMap = level (0.0, 300.0 + 150.0 - 40.0) (10.0, 10.0) (rotate 10.0)
        let theta, phi = radians -35.0, radians 20.0

        let rMap =
            level (150.0, 150.0) (40.0, 25.0) (linear (cos theta) 0.0 (sin theta * sin phi) (cos phi))

        let corners w h =
            [
                point 0.0 0.0
                point w 0.0
                point w h
                point 0.0 h
                point (w / 2.0) (h / 3.0)
            ]

        for name, value, map, w, h in
            [
                "o", o, oMap, 200.0, 100.0
                "i", i, iMap, 40.0, 40.0
                "t", t, tMap, 20.0, 20.0
                "r", r, rMap, 80.0, 50.0
            ] do
            for local in corners w h do
                near (name + " " + show local + " to page") (apply map local) (convert (name + " to page") local (element value) CoordinateSpace.Page)

        // Every ordered pair of spaces round-trips.
        let spaces (value: Element) =
            [
                element value
                CoordinateSpace.Page
                CoordinateSpace.Client
                CoordinateSpace.Host
                CoordinateSpace.Device
            ]

        for name, value in [ "o", o :> Element; "i", i; "b", b; "t", t; "r", r; "m", m ] do
            for from in spaces value do
                for target in spaces value do
                    if from <> target then
                        let sample = [| point 3.5 7.25; point 31.0 -2.0 |]

                        let there =
                            service.ConvertPoints (sample, from, target)
                            |> ok (
                                name
                                + " "
                                + spaceName from
                                + " to "
                                + spaceName target
                            )

                        let back =
                            service.ConvertPoints (there, target, from)
                            |> ok (
                                name
                                + " "
                                + spaceName target
                                + " back to "
                                + spaceName from
                            )

                        for index in 0..1 do
                            near
                                (name
                                 + " "
                                 + spaceName from
                                 + " -> "
                                 + spaceName target
                                 + " -> back")
                                sample[index]
                                back[index]

        // Client is getBoundingClientRect's space; at page scale 1 Host is Client, and Device is Host times the ratio.
        let bounds = b.getBoundingClientRect ()
        near "b origin in client" (point bounds.x bounds.y) (convert "b to client" (point 0.0 0.0) (element b) CoordinateSpace.Client)

        near
            "b corner in client"
            (point (bounds.x + bounds.width) (bounds.y + bounds.height))
            (convert "b corner to client" (point 50.0 50.0) (element b) CoordinateSpace.Client)

        near "b origin in page" (point 0.0 40.0) (convert "b to page" (point 0.0 0.0) (element b) CoordinateSpace.Page)

        let client =
            convert "b to client" (point 10.0 10.0) (element b) CoordinateSpace.Client

        near "host equals client at page scale 1" client (convert "client to host" client CoordinateSpace.Client CoordinateSpace.Host)
        let ratio = devicePixelRatio ()

        near
            "device is host times the ratio"
            (point (client.X * ratio) (client.Y * ratio))
            (convert "host to device" client CoordinateSpace.Host CoordinateSpace.Device)

        // Box quads of #m (border 2, padding 4 6, margin 3 5 7 9) in page coordinates.
        let mx, my = 309.0, 403.0

        for cssBox, (x, y, w, h) in
            [
                CoordinateBox.Border, (0.0, 0.0, 76.0, 52.0)
                CoordinateBox.Padding, (2.0, 2.0, 72.0, 48.0)
                CoordinateBox.Content, (8.0, 6.0, 60.0, 40.0)
                CoordinateBox.Margin, (-9.0, -3.0, 90.0, 62.0)
            ] do
            let quad =
                service.BoxQuad (m, cssBox, CoordinateSpace.Page)
                |> ok ("m " + boxName cssBox)

            near ("m " + boxName cssBox + " p1") (point (mx + x) (my + y)) quad.P1
            near ("m " + boxName cssBox + " p2") (point (mx + x + w) (my + y)) quad.P2
            near ("m " + boxName cssBox + " p3") (point (mx + x + w) (my + y + h)) quad.P3
            near ("m " + boxName cssBox + " p4") (point (mx + x) (my + y + h)) quad.P4

        let border =
            service.BoxQuad (o, CoordinateBox.Border, CoordinateSpace.Client)
            |> ok "o border"

        let mapped =
            service.ConvertQuad (
                {
                    P1 = point 0.0 0.0
                    P2 = point 200.0 0.0
                    P3 = point 200.0 100.0
                    P4 = point 0.0 100.0
                },
                element o,
                CoordinateSpace.Client
            )
            |> ok "o corners"

        near "o quad p1" mapped.P1 border.P1
        near "o quad p3" mapped.P3 border.P3

        // A singular transform maps forward and refuses points mapped into it.
        near "scale(0) collapses to its origin" (point 710.0 410.0) (convert "z to page" (point 3.0 4.0) (element z) CoordinateSpace.Page)
        equal "page to scale(0)" (Error CoordinateUnavailable.NonInvertible) (service.Convert (point 1.0 1.0, CoordinateSpace.Page, element z))

        equal
            "box quad of scale(0)"
            true
            (service.BoxQuad (z, CoordinateBox.Border, CoordinateSpace.Page)
             |> Result.isOk)

        equal "display:none" (Error CoordinateUnavailable.NotRendered) (service.Convert (point 0.0 0.0, element n, CoordinateSpace.Page))
        equal "inline box" (Error CoordinateUnavailable.NotRendered) (service.Convert (point 0.0 0.0, element k, CoordinateSpace.Page))
        equal "display:none box quad" (Error CoordinateUnavailable.NotRendered) (service.BoxQuad (n, CoordinateBox.Border, CoordinateSpace.Page))
        equal "display:none as a target" (Error CoordinateUnavailable.NotRendered) (service.Convert (point 0.0 0.0, CoordinateSpace.Page, element n))

        let perspective = service.Convert (point 0.0 0.0, element q, CoordinateSpace.Page)

        match engine with
        | Native -> equal "perspective chain converts" true (Result.isOk perspective)
        | Chromium ->
            equal
                "perspective chain is unsupported"
                true
                (match perspective with
                 | Error (CoordinateUnavailable.Unsupported _) -> true
                 | _ -> false)

        let other = foreign ()

        equal
            "element of another document"
            (Error CoordinateUnavailable.WrongDocument)
            (service.Convert (point 0.0 0.0, element other, CoordinateSpace.Page))

        equal
            "into another document"
            (Error CoordinateUnavailable.WrongDocument)
            (service.Convert (point 0.0 0.0, CoordinateSpace.Page, element other))

        equal
            "box quad of another document"
            (Error CoordinateUnavailable.WrongDocument)
            (service.BoxQuad (other, CoordinateBox.Border, CoordinateSpace.Client))

        r.remove ()
        equal "removed element" (Error CoordinateUnavailable.Detached) (service.Convert (point 0.0 0.0, element r, CoordinateSpace.Page))
        equal "into a removed element" (Error CoordinateUnavailable.Detached) (service.Convert (point 0.0 0.0, CoordinateSpace.Page, element r))
        equal "box quad of a removed element" (Error CoordinateUnavailable.Detached) (service.BoxQuad (r, CoordinateBox.Margin, CoordinateSpace.Page))
        equal "no points" (Ok [||]) (service.ConvertPoints ([||], element o, CoordinateSpace.Page))

        raises "non-finite x" (fun () ->
            service.Convert (point Double.NaN 0.0, CoordinateSpace.Page, CoordinateSpace.Client)
            |> ignore)

        raises "infinite y" (fun () ->
            service.Convert (point 0.0 Double.PositiveInfinity, CoordinateSpace.Page, CoordinateSpace.Client)
            |> ignore)

        raises "null element space" (fun () ->
            service.Convert (point 0.0 0.0, CoordinateSpace.Element Unchecked.defaultof<Element>, CoordinateSpace.Page)
            |> ignore)

        raises "null points" (fun () ->
            service.ConvertPoints (null, CoordinateSpace.Page, CoordinateSpace.Client)
            |> ignore)

        raises "null box element" (fun () ->
            service.BoxQuad (Unchecked.defaultof<Element>, CoordinateBox.Border, CoordinateSpace.Page)
            |> ignore)

        service.Dispose ()

        raises "disposed" (fun () ->
            service.Convert (point 0.0 0.0, CoordinateSpace.Page, CoordinateSpace.Client)
            |> ignore)
    finally
        service.Dispose ()

        for value in [ style; o; b; m; z; p; s; n; k ] do
            value.remove ()

    checks
