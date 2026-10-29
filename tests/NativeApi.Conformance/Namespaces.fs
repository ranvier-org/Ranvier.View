module internal NativeApiConformance.Namespaces

open Ranvier.Dom

/// <summary>Checks SVG and MathML shared members on the supplied engine document.</summary>
/// <param name="document">A live document with a body on the owning engine thread.</param>
/// <param name="flush">Updates style and layout before focus assertions.</param>
let run (document: Ranvier.Dom.Document) (flush: unit -> unit) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let verify (node: Ranvier.Dom.Element) (styled: Ranvier.Dom.ElementCSSInlineStyle) (focus: Ranvier.Dom.HTMLOrSVGElement) ns =
        equal "namespace" ns node.namespaceURI
        let style = styled.style
        style.setProperty ("color", "red", "important")
        equal "style priority" "important" (style.getPropertyPriority ("color"))
        equal "style removed value" "red" (style.removeProperty ("color"))
        equal "style priority removed" "" (style.getPropertyPriority ("color"))
        focus.dataset.setNamedItem ("probe", "native-λ")
        equal "dataset" "native-λ" (focus.dataset.namedItem ("probe"))
        equal "dataset attribute" "native-λ" (node.getAttribute ("data-probe"))
        focus.nonce <- "native-nonce"
        equal "nonce" "native-nonce" focus.nonce
        focus.autofocus <- true
        equal "autofocus" true focus.autofocus
        focus.tabIndex <- 0
        equal "tab index" 0 focus.tabIndex
        node.textContent <- "x"
        style.setProperty ("width", "40px")
        style.setProperty ("height", "20px")
        document.body.appendChild (node) |> ignore
        flush ()
        focus.focus ()
        equal "focus identity" true (document.activeElement.isSameNode (node))
        focus.blur ()
        equal "blur identity" false (document.activeElement.isSameNode (node))
        equal "namespace retained" ns node.namespaceURI

    let svg =
        document.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> Ranvier.Dom.SVGElement

    svg.id <- "native-api-svg"
    verify svg svg svg "http://www.w3.org/2000/svg"

    let math =
        document.createElementNS ("http://www.w3.org/1998/Math/MathML", "math") :?> Ranvier.Dom.MathMLElement

    math.id <- "native-api-math"
    verify math math math "http://www.w3.org/1998/Math/MathML"
    checks
