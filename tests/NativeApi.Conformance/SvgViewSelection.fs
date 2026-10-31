module internal NativeApiConformance.SvgViewSelection

open System
open Ranvier.Dom

/// <summary>Standalone SVG markup with two named views over a 200 by 200 viewport.</summary>
let markup =
    "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200' viewBox='0 0 200 200'><view id='half' viewBox='0 0 100 100'/><view id='other' viewBox='0 0 400 400'/><rect id='r' x='0' y='0' width='50' height='50'/></svg>"

/// <summary>Checks named-view and svgView selection, clearing, view edits, view controls and intersection queries.</summary>
/// <param name="select">Selects a fragment; native providers return the engine's selected flag, browsers return None.</param>
/// <param name="settle">Completes pending layout and rendering for the SVG document.</param>
/// <param name="activeEditsApply">True when edits to the selected view element update rendering before reselection.</param>
/// <param name="scaleZoomsContent">True when currentScale zooms the SVG content inside an unchanged page.</param>
let run
    (document: Document)
    (select: string -> Async<bool option>)
    (settle: unit -> Async<unit>)
    (inspect: SVGSVGElement -> SvgViewState)
    (activeEditsApply: bool)
    (scaleZoomsContent: bool)
    =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwithf "%s: expected %A, got %A" label expected actual

        let near label (expected: float) (actual: float) =
            checks <- checks + 1

            if
                Double.IsNaN actual
                || Double.IsInfinity actual
                || abs (expected - actual) > 0.001
            then
                failwithf "%s: expected %g, got %g" label expected actual

        equal "standalone SVG content type" "image/svg+xml" document.contentType
        let root = document.documentElement :?> SVGSVGElement
        let rect = document.getElementById "r" :?> SVGGraphicsElement
        let half = document.getElementById "half"

        let rendered label (x, width, scale, translate) =
            async {
                do! settle ()
                let box = rect.getBoundingClientRect ()
                near (label + " rendered x") x box.x
                near (label + " rendered width") width box.width
                near (label + " rendered height") width box.height
                let matrix = root.getScreenCTM ()
                near (label + " CTM scale x") scale (float matrix.a)
                near (label + " CTM scale y") scale (float matrix.d)
                near (label + " CTM translate x") translate (float matrix.e)
            }

        let choose label fragment expected =
            async {
                let! selected = select fragment

                match selected with
                | Some flag -> equal (label + " selected flag") expected flag
                | None -> ()
            }

        let state label (expected: string option) =
            let observed = inspect root

            match observed.UseCurrentView, observed.CurrentView with
            | SvgViewAvailability.NotExposed, SvgViewAvailability.NotExposed -> equal (label + " view getters not exposed") true true
            | SvgViewAvailability.Available active, SvgViewAvailability.Available view ->
                equal (label + " useCurrentView") expected.IsSome active

                match expected with
                | Some viewBox -> equal (label + " current view box") viewBox view.viewBoxString
                | None -> ()
            | other -> failwithf "%s: unexpected view exposure %A" label other

        do! rendered "default view" (0., 50., 1., 0.)
        state "default view" None
        do! choose "named view" "half" true
        do! rendered "named view" (0., 100., 2., 0.)
        state "named view" (Some "0 0 100 100")
        do! choose "empty fragment" "" false
        do! rendered "cleared view" (0., 50., 1., 0.)
        state "cleared view" None
        do! choose "svgView fragment" "svgView(viewBox(0,0,400,400))" true
        do! rendered "svgView fragment" (0., 25., 0.5, 0.)
        state "svgView fragment" (Some "0 0 400 400")
        do! choose "reselect named view" "half" true
        do! choose "missing fragment" "nope" false
        do! rendered "missing fragment resets" (0., 50., 1., 0.)
        do! choose "reselect before malformed" "half" true
        do! choose "malformed svgView" "svgView(bad" false
        do! rendered "malformed fragment resets" (0., 50., 1., 0.)
        do! choose "second named view" "other" true
        do! rendered "second named view" (0., 25., 0.5, 0.)
        do! choose "former view" "half" true
        do! choose "clear before edit" "" false
        half.setAttribute ("viewBox", "0 0 50 50")
        do! rendered "cleared view stays inactive after edit" (0., 50., 1., 0.)
        state "cleared view stays inactive after edit" None
        do! choose "edited view" "half" true
        do! rendered "edited view" (0., 200., 4., 0.)
        half.setAttribute ("viewBox", "0 0 100 100")

        if activeEditsApply then
            do! rendered "active view edit applies" (0., 100., 2., 0.)
        else
            do! rendered "active view edit waits for reselection" (0., 200., 4., 0.)

        do! choose "clear before reselection" "" false
        do! choose "reselected view" "half" true
        do! rendered "reselected view" (0., 100., 2., 0.)
        do! choose "final clear" "" false
        do! rendered "final cleared view" (0., 50., 1., 0.)

        let area = root.createSVGRect ()
        area.width <- 30.f
        area.height <- 30.f
        equal "intersection" true (root.checkIntersection (rect, area))
        equal "partial enclosure" false (root.checkEnclosure (rect, area))
        equal "intersection list" 1u (root.getIntersectionList (area, null)).length
        equal "partial enclosure list" 0u (root.getEnclosureList (area, null)).length
        area.width <- 60.f
        area.height <- 60.f
        equal "full enclosure" true (root.checkEnclosure (rect, area))
        equal "full enclosure list" 1u (root.getEnclosureList (area, null)).length
        equal "full enclosure list identity" true (obj.ReferenceEquals (rect, (root.getEnclosureList (area, null)).item 0u))
        equal "zoomAndPan magnify" 2us root.zoomAndPan
        equal "suspendRedraw handle" 1u (root.suspendRedraw 100u)
        root.unsuspendRedraw 1u
        root.unsuspendRedrawAll ()
        root.forceRedraw ()
        root.deselectAll ()
        do! rendered "deprecated redraw methods are no-ops" (0., 50., 1., 0.)
        near "initial currentScale" 1. (float root.currentScale)
        near "initial currentTranslate" 0. (float root.currentTranslate.x)
        let translate = root.currentTranslate
        equal "currentTranslate identity" true (obj.ReferenceEquals (translate, root.currentTranslate))
        translate.x <- 10.f
        do! rendered "currentTranslate pans content" (10., 50., 1., 10.)
        translate.x <- 0.f
        root.currentScale <- 2.f
        near "currentScale reflects" 2. (float root.currentScale)

        if scaleZoomsContent then
            do! rendered "currentScale zooms SVG content" (0., 100., 2., 0.)
        else
            do! settle ()
            near "currentScale changes page zoom only" 50. (rect.getBoundingClientRect ()).width

        root.currentScale <- 1.f
        do! rendered "restored scale" (0., 50., 1., 0.)
        return checks
    }
