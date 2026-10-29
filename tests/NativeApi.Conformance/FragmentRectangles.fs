module internal NativeApiConformance.FragmentRectangles

open System
open Ranvier.Dom

/// <summary>Checks retained line fragments, range carets and nullable item identity against the live layout engine.</summary>
/// <param name="document">The live document; the fixture removes its elements before returning.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let near label expected actual =
        equal label true (abs (expected - actual) < 0.02)

    let container = document.createElement "div" :?> HTMLElement
    let span = document.createElement "span" :?> HTMLElement
    let hidden = document.createElement "span" :?> HTMLElement
    container.style.cssText <- "position:absolute;left:10.25px;top:20.5px;width:80px;font:10px/12px monospace;"
    span.textContent <- "alpha beta gamma delta epsilon zeta eta theta"
    hidden.style.cssText <- "display:none"
    hidden.textContent <- "hidden"
    container.appendChild span |> ignore
    container.appendChild hidden |> ignore
    document.body.appendChild container |> ignore

    try
        let rectangles = span.getClientRects ()
        equal "wrapped text has multiple fragments" true (rectangles.length > 1u)
        equal "out-of-range item is null" true (isNull (rectangles.item rectangles.length))
        equal "maximum index is null" true (isNull (rectangles.item UInt32.MaxValue))
        let first = rectangles.item 0u
        equal "repeated item preserves identity" true (Object.ReferenceEquals (first, rectangles.item 0u))

        let values =
            [|
                for index in 0u .. rectangles.length - 1u do
                    let rectangle = rectangles.item index
                    equal "fragment positive width" true (rectangle.width > 0.)
                    equal "fragment positive height" true (rectangle.height > 0.)
                    equal "fragment fits container" true (rectangle.width <= 80.02)
                    near "fractional fragment x" 10.25 rectangle.left

                    if index > 0u then
                        equal "fragment order" true (rectangle.top > (rectangles.item (index - 1u)).top)

                    yield (rectangle.left, rectangle.top, rectangle.right, rectangle.bottom)
            |]

        let union = span.getBoundingClientRect ()

        near
            "union left"
            (values
             |> Array.map (fun (v, _, _, _) -> v)
             |> Array.min)
            union.left

        near
            "union top"
            (values
             |> Array.map (fun (_, v, _, _) -> v)
             |> Array.min)
            union.top

        near
            "union right"
            (values
             |> Array.map (fun (_, _, v, _) -> v)
             |> Array.max)
            union.right

        near
            "union bottom"
            (values
             |> Array.map (fun (_, _, _, v) -> v)
             |> Array.max)
            union.bottom

        equal "hidden element has no fragments" 0u (hidden.getClientRects ()).length
        let detached = document.createElement "span"
        equal "detached element has no fragments" 0u (detached.getClientRects ()).length
        let range = document.createRange ()
        equal "empty range has no fragments" 0u (range.getClientRects ()).length
        range.selectNodeContents span
        let selected = range.getClientRects ()
        equal "range fragment count matches text" rectangles.length selected.length

        for index in 0u .. selected.length - 1u do
            let actual = selected.item index
            let left, top, right, bottom = values[int index]
            near "range fragment left" left actual.left
            near "range fragment top" top actual.top
            near "range fragment right" right actual.right
            near "range fragment bottom" bottom actual.bottom

        range.setStart (span.firstChild, 3u)
        range.setEnd (span.firstChild, 3u)
        let caret = range.getClientRects ()
        equal "collapsed text range has caret fragment" 1u caret.length
        near "caret width" 0. (caret.item 0u).width
        equal "caret height positive" true ((caret.item 0u).height > 0.)
        near "caret on first line" first.top (caret.item 0u).top

        equal
            "caret inside first line"
            true
            ((caret.item 0u).left >= first.left
             && (caret.item 0u).left <= first.right)

        container.style.setProperty ("width", "40px", "")
        let resized = span.getClientRects ()
        equal "resize returns independent snapshot" false (Object.ReferenceEquals (rectangles, resized))
        equal "narrower container wraps at least as much" true (resized.length >= rectangles.length)
        equal "original snapshot retains count" (uint32 values.Length) rectangles.length

        for index in 0u .. rectangles.length - 1u do
            let actual = rectangles.item index
            let left, top, right, bottom = values[int index]
            near "snapshot left unchanged" left actual.left
            near "snapshot top unchanged" top actual.top
            near "snapshot right unchanged" right actual.right
            near "snapshot bottom unchanged" bottom actual.bottom

        document.body.removeChild container |> ignore
        equal "removed element has no fragments" 0u (span.getClientRects ()).length
        equal "retained snapshot survives detach" (uint32 values.Length) rectangles.length
        near "retained item survives detach" 10.25 first.left
        checks
    finally
        if not (isNull container.parentNode) then
            container.parentNode.removeChild container
            |> ignore
