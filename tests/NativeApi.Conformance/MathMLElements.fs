module internal NativeApiConformance.MathMLElements

open Ranvier.Dom

[<Literal>]
let MathML = "http://www.w3.org/1998/Math/MathML"

/// <summary>The MathML elements of the fixture, by local name.</summary>
let names =
    [ "math"; "mi"; "mo"; "mn"; "ms"; "mtext"; "mrow"; "mfrac"; "msqrt"; "mroot"; "msub"; "msup"; "mspace" ]

/// <summary>
/// Checks typed MathML elements on the supplied engine document. <c>interfaceOf</c> names the most derived DOM
/// interface of an element as the provider exposes it; <c>mathInterface</c> is the one expected for <c>math</c>.
/// </summary>
let run (document: Document) (interfaceOf: Element -> string) (mathInterface: string) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let host = document.createElement "div"
    host.id <- "native-api-mathml"
    document.body.appendChild host |> ignore

    try
        // Created elements.
        for local in names do
            let element = document.createElementNS (MathML, local)
            equal $"{local} namespace" MathML element.namespaceURI
            equal $"{local} localName" local element.localName
            equal $"{local} tagName" local element.tagName

            equal
                $"{local} interface"
                (if local = "math" then mathInterface else "MathMLElement")
                (interfaceOf element)

            let typed = element :?> MathMLElement
            equal $"{local} default tabIndex" -1 typed.tabIndex
            equal $"{local} default autofocus" false typed.autofocus
            typed.dataset.setNamedItem ("kind", local)
            equal $"{local} dataset attribute" local (element.getAttribute "data-kind")
            typed.style.setProperty ("padding-left", "3px")
            equal $"{local} style" "3px" (typed.style.getPropertyValue "padding-left")

        // Parsed elements.
        host.innerHTML <- "<math id=\"mathml-root\"><mfrac><mi>x</mi><mn>2</mn></mfrac><msqrt><mo>+</mo></msqrt></math><p>after</p>"
        let parsed = document.getElementById "mathml-root"
        equal "parsed math namespace" MathML parsed.namespaceURI
        equal "parsed math interface" mathInterface (interfaceOf parsed)
        let fraction = parsed.firstElementChild
        equal "parsed mfrac" "mfrac" fraction.localName
        equal "parsed mfrac interface" "MathMLElement" (interfaceOf fraction)
        equal "parsed mi interface" "MathMLElement" (interfaceOf fraction.firstElementChild)
        equal "parsed sibling" "HTMLParagraphElement" (interfaceOf parsed.nextElementSibling)

        // The typed math element exposes the MathMLElement members.
        let math = parsed :?> MathMLMathElement
        math.tabIndex <- 0
        equal "math tabIndex attribute" "0" (parsed.getAttribute "tabindex")
        math.nonce <- "mathml-nonce"
        equal "math nonce" "mathml-nonce" math.nonce
        math.focus ()
        equal "math focus" true (document.activeElement.isSameNode parsed)
        math.blur ()
        equal "math blur" false (document.activeElement.isSameNode parsed)

        // An element in another namespace with a MathML local name is not MathML.
        let html = document.createElement "math"
        equal "HTML math namespace" "http://www.w3.org/1999/xhtml" html.namespaceURI
        equal "HTML math is not MathML" false ((interfaceOf html).StartsWith "MathML")
        checks
    finally
        host.remove ()
