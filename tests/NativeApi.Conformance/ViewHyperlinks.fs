module internal NativeApiConformance.ViewHyperlinks

open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom

module H = Ranvier.View.Dom.Generated.Html

let private href =
    "https://fixture-user:fixture-pass@initial.example:8443/base/path?fixture=1#initial"

let private template =
    Component.define (fun _ (_: unit) -> ()) (fun t ->
        t.div
            [||]
            [|
                t.a
                    [|
                        t.Props.HyperlinkElementUtils.href href
                        t.Props.HyperlinkElementUtils.hash "#template-anchor"
                    |]
                    [||]
                t.area
                    [|
                        t.Props.HyperlinkElementUtils.href href
                        t.Props.HyperlinkElementUtils.hash "#template-area"
                    |]
            |])

let private generated =
    H.Program.div
        [||]
        [|
            H.Program.a
                [|
                    H.Properties.HTMLHyperlinkElementUtils.hrefStatic href
                    H.Properties.HTMLHyperlinkElementUtils.hashStatic "#generated-anchor"
                |]
                [||]
            H.Program.area
                [|
                    H.Properties.HTMLHyperlinkElementUtils.hrefStatic href
                    H.Properties.HTMLHyperlinkElementUtils.hashStatic "#generated-area"
                |]
        |]

/// <summary>Checks common hyperlink authoring setters/getters on real Anchor and Area elements through both View APIs.</summary>
/// <param name="document">The live document; mounted fixture containers are removed before returning.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    use graph = new Graph ()

    for label, view in [ "template", Component.render template (); "generated", generated ] do
        let container = document.createElement "div" :?> HTMLElement
        document.body.appendChild container |> ignore
        let mutable mounted: Mounting.Mounted option = None

        try
            let result =
                Mounting.mount graph None (fun errors -> failwith $"View hyperlink mount failed: {errors}") container None (fun context ->
                    Program.run view context.Backend)

            mounted <- Some result
            let anchor = container.querySelector "a" :?> HTMLAnchorElement
            let area = container.querySelector "area" :?> HTMLAreaElement

            let verify suffix (element: HTMLHyperlinkElementUtils) =
                equal (label + suffix + " helper hash") ("#" + label + "-" + suffix) (H.Properties.HTMLHyperlinkElementUtils.hash element)
                equal "helper protocol" "https:" (H.Properties.HTMLHyperlinkElementUtils.protocol element)
                equal "helper host" "initial.example:8443" (H.Properties.HTMLHyperlinkElementUtils.host element)
                equal "helper hostname" "initial.example" (H.Properties.HTMLHyperlinkElementUtils.hostname element)
                equal "helper port" "8443" (H.Properties.HTMLHyperlinkElementUtils.port element)
                equal "helper pathname" "/base/path" (H.Properties.HTMLHyperlinkElementUtils.pathname element)
                equal "helper search" "?fixture=1" (H.Properties.HTMLHyperlinkElementUtils.search element)
                equal "helper username" "fixture-user" (H.Properties.HTMLHyperlinkElementUtils.username element)
                equal "helper password" "fixture-pass" (H.Properties.HTMLHyperlinkElementUtils.password element)
                equal "helper readonly origin" "https://initial.example:8443" (H.Properties.HTMLHyperlinkElementUtils.origin element)

                equal
                    "helper href reflects hash setter"
                    ("https://fixture-user:fixture-pass@initial.example:8443/base/path?fixture=1#"
                     + label
                     + "-"
                     + suffix)
                    element.href

            verify "anchor" anchor
            verify "area" area
            equal "anchor native typed hash" ("#" + label + "-anchor") anchor.hash
            equal "area native typed hash" ("#" + label + "-area") area.hash
            result.Dispose ()
            mounted <- None
            equal "hyperlink view disposal" 0u container.childNodes.length
        finally
            mounted
            |> Option.iter (fun current -> current.Dispose ())

            document.body.removeChild container |> ignore

    checks
