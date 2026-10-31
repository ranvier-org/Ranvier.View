module internal NativeApiConformance.DiagnosticsChecks

open System
open Ranvier.Dom

/// <summary>The engine whose diagnostics the fixture expects.</summary>
type Engine =
    /// <summary>The native host: render tree, matched rules and box model.</summary>
    | Native
    /// <summary>Chromium: the box model only.</summary>
    | Chromium

let private Css =
    "@layer base, top;"
    + "#dx-target{position:absolute;left:11.3px;top:13.7px;width:50.5px;height:20.25px;margin:3px 4px 5px 6px;"
    + "border-style:solid;border-width:1px 2px 3px 4px;padding:7px 8px 9px 10px;--order:main}"
    + "div.dx#dx-target{--order:compound}"
    + ":where(#dx-target){--order:where}"
    + ":is(#dx-target, .dx){--order:is}"
    + ".dx, #dx-none, div{--order:list}"
    + "@media (min-width: 1px){.dx{--order:media}}"
    + "@media (max-width: 1px){.dx{--order:excluded}}"
    + "@layer top{#dx-target{--order:top}}"
    + "@layer base{#dx-target{--order:base}}"
    + "#dx-scroller{position:absolute;left:200px;top:0;width:100px;height:100px;overflow:hidden;border:0;padding:0;margin:0}"
    + "#dx-child{position:absolute;left:5px;top:60px;width:20px;height:10px;border:1px solid;padding:2px;margin:0}"
    + "#dx-hidden{display:none}"

// The author rules of #dx-target in cascade order: layer base, layer top, then unlayered by specificity and position.
let private Author =
    [
        "base", "#dx-target", (1, 0, 0)
        "top", "#dx-target", (1, 0, 0)
        "where", ":where(#dx-target)", (0, 0, 0)
        "list", ".dx", (0, 1, 0)
        "media", ".dx", (0, 1, 0)
        "main", "#dx-target", (1, 0, 0)
        "is", ":is(#dx-target, .dx)", (1, 0, 0)
        "compound", "div.dx#dx-target", (1, 1, 1)
    ]

/// <summary>
/// Checks the opt-in, the features, the box model in viewport coordinates before and after a scroll, and on the native
/// engine the render tree and matched rules; the result is the assertion count. The box-model values follow from the
/// fixture's CSS, quantised to 1/64 px.
/// </summary>
let run (create: unit -> DiagnosticsService) (document: Document) (engine: Engine) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"{label}: expected {expected}, got {actual}"

    let near label (expected: float) (actual: float) =
        checks <- checks + 1

        if abs (expected - actual) > 1.0 / 64.0 + 1e-9 then
            failwith $"{label}: expected {expected}, got {actual}"

    // Providers raise ArgumentNullException, ArgumentException and ObjectDisposedException; Fable erases the types.
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

    let rect label (x, y, width, height) (actual: BoxRect) =
        near (label + " x") x actual.X
        near (label + " y") y actual.Y
        near (label + " width") width actual.Width
        near (label + " height") height actual.Height

    let element (tag: string) (id: string) =
        let value = document.createElement tag :?> HTMLElement
        value.id <- id
        value

    let style = element "style" "dx-style"
    style.textContent <- Css
    let target = element "div" "dx-target"
    target.className <- "dx"
    target.setAttribute ("style", "opacity: 0.5")
    let scroller = element "div" "dx-scroller"
    let spacer = element "div" "dx-spacer"
    spacer.style.cssText <- "height:1000px"
    let child = element "div" "dx-child"
    let hidden = element "div" "dx-hidden"
    let span = element "span" "dx-span"
    span.textContent <- "i"
    scroller.appendChild spacer |> ignore
    scroller.appendChild child |> ignore

    for value in [ style; target; scroller; hidden; span ] do
        document.body.appendChild value |> ignore

    let service = create ()

    try
        let features = service.Features
        equal "box model supported" DiagnosticsAvailability.Supported features.BoxModel
        equal "no developer-tools frontend" Diagnostics.noFrontend features.DeveloperToolsFrontend

        match engine with
        | Native ->
            equal "render tree supported" DiagnosticsAvailability.Supported features.RenderTree
            equal "matched rules supported" DiagnosticsAvailability.Supported features.MatchedRules
        | Chromium ->
            equal
                "render tree unsupported"
                true
                (features.RenderTree
                 <> DiagnosticsAvailability.Supported)

            equal
                "matched rules unsupported"
                true
                (features.MatchedRules
                 <> DiagnosticsAvailability.Supported)

        equal "starts disabled" false service.Enabled
        equal "box model disabled" (Error DiagnosticsUnavailable.Disabled) (service.BoxModel target)
        equal "render tree disabled" (Error DiagnosticsUnavailable.Disabled) (service.DocumentRenderTree RenderTreeOptions.None)
        equal "matched rules disabled" (Error DiagnosticsUnavailable.Disabled) (service.MatchedRules target |> Result.map ignore)

        raises "null element" (fun () ->
            service.BoxModel Unchecked.defaultof<Element>
            |> ignore)

        raises "unknown option" (fun () ->
            service.DocumentRenderTree (enum<RenderTreeOptions> 64)
            |> ignore)

        raises "layer option with an element" (fun () ->
            service.RenderTree (target, RenderTreeOptions.AllLayers)
            |> ignore)

        service.Enable ()
        equal "enabled" true service.Enabled

        match service.BoxModel target with
        | Ok boxes ->
            rect "content" (31.296875, 24.6875, 50.5, 20.25) boxes.Content
            rect "padding" (21.296875, 17.6875, 68.5, 36.25) boxes.Padding
            rect "border" (17.296875, 16.6875, 74.5, 40.25) boxes.Border
            rect "margin" (11.296875, 13.6875, 84.5, 48.25) boxes.Margin
            let bounds = target.getBoundingClientRect ()
            rect "border is the bounding rectangle" (bounds.x, bounds.y, bounds.width, bounds.height) boxes.Border
        | Error reason -> failwith $"box model: {reason}"

        let childBorder () =
            match service.BoxModel child with
            | Ok boxes -> boxes.Border
            | Error reason -> failwith $"child box model: {reason}"

        rect "child before scrolling" (205.0, 60.0, 26.0, 16.0) (childBorder ())
        scroller.scrollTop <- 45
        rect "child after scrolling" (205.0, 15.0, 26.0, 16.0) (childBorder ())
        scroller.scrollTop <- 0
        equal "display:none not rendered" (Error DiagnosticsUnavailable.NotRendered) (service.BoxModel hidden)
        equal "inline box not rendered" (Error DiagnosticsUnavailable.NotRendered) (service.BoxModel span)

        match engine with
        | Native ->
            match service.DocumentRenderTree RenderTreeOptions.IdAndClass with
            | Ok text ->
                let lines = text.Split '\n'
                equal "dump starts with the root layer" true (lines[0].StartsWith "layer at (0,0)")

                equal
                    "one box for the target"
                    1
                    (lines
                     |> Array.filter (fun l -> l.Contains "id=\"dx-target\"")
                     |> Array.length)

                equal
                    "no box for display:none"
                    false
                    (lines
                     |> Array.exists (fun l -> l.Contains "id=\"dx-hidden\""))
            | Error reason -> failwith $"render tree: {reason}"

            match service.RenderTree (scroller, RenderTreeOptions.IdAndClass) with
            | Ok text ->
                let lines = text.Split ('\n', StringSplitOptions.RemoveEmptyEntries)

                equal
                    "element dump starts at its box"
                    true
                    (lines[0].StartsWith "RenderBlock"
                     && lines[0].Contains "id=\"dx-scroller\"")

                equal
                    "children nest below it"
                    true
                    (lines
                     |> Array.skip 1
                     |> Array.forall (fun l -> l.StartsWith "  "))
            | Error reason -> failwith $"element render tree: {reason}"

            equal "no render tree for display:none" (Error DiagnosticsUnavailable.NotRendered) (service.RenderTree (hidden, RenderTreeOptions.None))

            match service.MatchedRules target with
            | Ok rules ->
                let origins = rules |> Array.map (fun r -> int r.Origin)
                equal "origins in cascade order" (Array.sort origins) origins
                equal "a user-agent rule first" CascadeOrigin.UserAgent rules[0].Origin

                let author =
                    rules
                    |> Array.filter (fun r -> r.Origin = CascadeOrigin.Author)

                let order =
                    author
                    |> Array.map (fun r -> r.Declarations.Substring(r.Declarations.IndexOf "--order: " + 9).TrimEnd(';'))
                    |> Array.toList

                equal "author cascade order" (Author |> List.map (fun (n, _, _) -> n)) order

                for rule, (name, matched, (a, b, c)) in Seq.zip author Author do
                    equal (name + " matched selector") matched rule.MatchedSelector
                    equal (name + " specificity") { A = a; B = b; C = c } rule.Specificity

                let last = rules[rules.Length - 1]
                equal "inline entry last" CascadeOrigin.Inline last.Origin
                equal "inline entry has no selector" "" last.SelectorText
                equal "inline declarations" "opacity: 0.5;" last.Declarations

                equal
                    "no excluded rule"
                    false
                    (rules
                     |> Array.exists (fun r -> r.Declarations.Contains "excluded"))
            | Error reason -> failwith $"matched rules: {reason}"
        | Chromium ->
            equal
                "render tree unsupported"
                true
                (service.DocumentRenderTree RenderTreeOptions.None
                 |> Result.isError)

            equal "matched rules unsupported" true (service.MatchedRules target |> Result.isError)

        service.Disable ()
        equal "disabled again" (Error DiagnosticsUnavailable.Disabled) (service.BoxModel target)
        service.Dispose ()
        raises "disposed" (fun () -> service.BoxModel target |> ignore)
        checks
    finally
        service.Dispose ()

        for value in [ style; target; scroller; hidden; span ] do
            value.remove ()
