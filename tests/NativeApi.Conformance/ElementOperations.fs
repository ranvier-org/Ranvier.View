module internal NativeApiConformance.ElementOperations

open Ranvier.Dom

/// <summary>Checks closest SVG viewport identity through the shared typed getter.</summary>
/// <param name="document">A live document on its owning engine thread.</param>
let viewport (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let create name =
        document.createElementNS ("http://www.w3.org/2000/svg", name) :?> SVGElement

    let outer = create "svg"
    let nested = create "svg"
    let group = create "g"
    let leaf = create "rect"
    let orphan = create "rect"

    let closest label (expected: SVGElement) (element: SVGElement) =
        let actual = element.viewportElement
        equal (label + " nonnull") false (isNull actual)
        equal (label + " identity") true (actual.isSameNode expected)
        equal (label + " repeated identity") true (actual.isSameNode element.viewportElement)

    equal "detached outer viewport" true (isNull outer.viewportElement)
    equal "orphan viewport" true (isNull orphan.viewportElement)
    outer.appendChild nested |> ignore
    nested.appendChild group |> ignore
    group.appendChild leaf |> ignore
    closest "detached nested root" outer nested
    closest "detached grouped leaf" nested leaf
    document.body.appendChild outer |> ignore

    try
        equal "attached outer viewport" true (isNull outer.viewportElement)
        closest "attached nested root" outer nested
        closest "attached group" nested group
        closest "attached grouped leaf" nested leaf
        outer.appendChild leaf |> ignore
        closest "reparented leaf" outer leaf
        outer.removeChild leaf |> ignore
        equal "removed leaf viewport" true (isNull leaf.viewportElement)
        outer.removeChild nested |> ignore
        equal "removed nested root viewport" true (isNull nested.viewportElement)
        closest "retained detached group" nested group
        closest "retained detached descendant" nested (group.appendChild leaf :?> SVGElement)
    finally
        document.body.removeChild outer |> ignore

    equal "removed outer viewport" true (isNull outer.viewportElement)
    checks

/// <summary>Checks marquee movement, stopping and resumption, and records the conditional source getter result.</summary>
/// <param name="document">A live document on its owning engine thread.</param>
/// <param name="checkpoint">Yields time while the owning engine continues processing timers and layout.</param>
/// <param name="flush">Updates layout before reading geometry.</param>
/// <param name="errorName">Returns the provider's stable exception name.</param>
/// <param name="classifyString">Distinguishes a string, null and browser undefined without coercion.</param>
/// <param name="attachmentOutcomes">Exact source-getter outcomes allowed by the tested provider profile.</param>
let semantic
    (document: Document)
    (checkpoint: unit -> Async<unit>)
    (flush: unit -> unit)
    (errorName: exn -> string)
    (classifyString: string -> string)
    (attachmentOutcomes: string list)
    =
    async {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let marquee = document.createElement "marquee" :?> HTMLMarqueeElement
        let marker = document.createElement "span" :?> HTMLElement
        marquee.width <- "200"
        marquee.height <- "30"
        marquee.scrollAmount <- 3u
        marquee.scrollDelay <- 20u
        marquee.trueSpeed <- true
        marquee.direction <- "left"
        marquee.loop <- -1
        marker.style.cssText <- "display:inline-block;width:20px;height:10px"
        marker.textContent <- "moving"
        marquee.appendChild marker |> ignore
        marquee.stop ()
        marquee.stop ()
        marquee.start ()
        marquee.stop ()
        equal "detached marquee calls preserve child" true (marker.parentNode.isSameNode marquee)
        document.body.appendChild marquee |> ignore

        let position () =
            flush ()
            marker.getBoundingClientRect().left

        let waitForMovement baseline =
            async {
                let mutable current = position ()
                let mutable attempts = 0

                while abs (current - baseline) <= 0.5 && attempts < 100 do
                    do! checkpoint ()
                    current <- position ()
                    attempts <- attempts + 1

                equal "marquee content moves after start" true (abs (current - baseline) > 0.5)
            }

        let assertStopped () =
            async {
                marquee.stop ()
                marquee.stop ()
                do! checkpoint ()
                let baseline = position ()

                for _ in 1..8 do
                    do! checkpoint ()
                    equal "stopped marquee content remains fixed" true (abs (position () - baseline) < 0.1)
            }

        try
            flush ()
            do! assertStopped ()
            marquee.start ()
            marquee.start ()
            let baseline = position ()
            do! waitForMovement baseline
            do! assertStopped ()
            marquee.start ()
            let stopped = position ()
            do! waitForMovement stopped
            marquee.stop ()
            equal "marquee operations preserve content" "moving" marker.textContent
        finally
            marquee.stop ()
            document.body.removeChild marquee |> ignore

        marquee.start ()
        marquee.stop ()
        equal "removed marquee has no parent" true (isNull marquee.parentNode)
        let source = document.createElement "source" :?> HTMLSourceElement

        let readAttachment () =
            try
                let value = source.attachmentIdentifier
                let kind = classifyString value
                if kind = "string" then "string:" + value else kind
            with error ->
                "error:" + errorName error

        let attachment = readAttachment ()
        equal "source attachment outcome is qualified" true (List.contains attachment attachmentOutcomes)
        equal "source attachment outcome repeats" attachment (readAttachment ())
        equal "source attachment probe preserves tag" "SOURCE" source.tagName
        document.body.appendChild source |> ignore

        try
            equal "attached source attachment outcome" attachment (readAttachment ())
        finally
            document.body.removeChild source |> ignore

        equal "removed source attachment outcome" attachment (readAttachment ())
        return checks, attachment
    }
