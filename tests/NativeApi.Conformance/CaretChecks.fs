module internal NativeApiConformance.CaretChecks

open System
open Ranvier.Dom

/// <summary>Host changes the fixture drives.</summary>
type Driver =
    {
        /// <summary>Sets the device scale at a 320x240 CSS viewport, then lets rendering updates run.</summary>
        SetScale: float -> Async<unit>
        /// <summary>Lets rendering updates and queued tasks run.</summary>
        Settle: unit -> Async<unit>
        /// <summary>The pinned Ahem font, base64.</summary>
        Font: string
        /// <summary>The backend's measured differences from Chromium, keyed by <c>scroll|point|member</c>.</summary>
        Qualified: Map<string, string>
    }

// Ranvier.WebCore tests/fixtures/caret-hit.html, without its document skeleton.
let private style =
    "html,body{margin:0}body{font:20px/20px RvAhem;width:600px;height:1000px}"
    + ".p{position:absolute;left:0;margin:0;white-space:normal}"
    + "#wrap{top:0;width:100px}#inline{top:80px}#bidi{top:110px}#rtl{top:140px;left:180px;width:120px}"
    + "#host{position:absolute;top:190px;left:0}#closed{position:absolute;top:190px;left:200px}"
    + "#field{position:absolute;top:220px;left:0;width:100px;height:20px;font:20px/20px RvAhem;border:0;padding:0;margin:0}"
    + "#empty{position:absolute;top:0;left:200px;width:50px;height:50px}#far{top:600px}"

let private markup =
    """<div id="wrap" class="p">AAAA BBBB CCCC</div><div id="inline" class="p">AA<b id="b">BB</b><i id="i">CC</i>DD</div>"""
    + """<div id="bidi" class="p">XX<bdo id="bdo" dir="rtl">ABCD</bdo>YY</div><div id="rtl" class="p" dir="rtl">ABCD EFGH</div>"""
    + """<div id="host"></div><div id="closed"></div><input id="field" value="ABCD"><div id="empty"></div><div id="far" class="p">FFFF</div>"""

let private number (value: float) =
    if value = Math.Floor value then
        string (int64 value)
    else
        sprintf "%.2f" value

let private rectangle (rect: DOMRect) =
    if isNull rect then
        "[null]"
    else
        "["
        + String.Join (
            ",",
            [ rect.x; rect.y; rect.width; rect.height ]
            |> List.map number
        )
        + "]"

/// <summary>WebIDL <c>long</c> conversion of a coordinate: non-finite values become 0, others truncate.</summary>
let private long (value: float) =
    if Double.IsNaN value || Double.IsInfinity value then
        0
    else
        int (Math.Truncate value)

/// <summary>
/// Hit-tests the caret fixture at device scales 1, 1.5 and 2, unscrolled and scrolled, through
/// <c>Document.caretPositionFromPoint</c> (with and without <c>shadowRoots</c>) and <c>caretRangeFromPoint</c>, and
/// compares each outcome with Chromium 156 at the backend's actual scroll offset or with the backend's qualification.
/// Then checks non-finite coordinates, a held CaretPosition across a text change and removal, and a document without
/// a render tree. The result is the assertion count.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>TypeError</c>.</param>
let run (document: Document) (scroller: ViewportScrollService) (errorName: exn -> string) (driver: Driver) =
    async {
        let mutable checks = 0
        let mutable qualified = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let expectOutcome key (chromium: string) (actual: string) =
            match driver.Qualified.TryFind key with
            | Some native ->
                qualified <- qualified + 1
                equal (key + " (qualified)") native actual
            | None -> equal key chromium actual

        let head = document.head
        let sheet = document.createElement "style"

        sheet.textContent <-
            $"@font-face{{font-family:RvAhem;src:url(data:font/ttf;base64,{driver.Font})}}"
            + style

        head.appendChild sheet |> ignore
        let mutable roots: ShadowRoot list = []

        let build () =
            document.body.innerHTML <- markup

            roots <-
                [
                    "host", ShadowRootMode.Open, """<span id="s">SSSS</span>"""
                    "closed", ShadowRootMode.Closed, """<span id="t">TTTT</span>"""
                ]
                |> List.map (fun (id, mode, content) ->
                    let init = ShadowRootInit ()
                    init.mode <- mode
                    let root = (document.getElementById id).attachShadow init
                    root.innerHTML <- content
                    root)

        let scope (node: Node) =
            let root = node.getRootNode ()

            if root.isSameNode document then "document"
            elif root.isSameNode roots[0] then "open"
            elif root.isSameNode roots[1] then "closed"
            else root.nodeName

        let label (node: Node) =
            if node.nodeType = 3us then
                let parent = node.parentNode
                let mutable index = 0
                let mutable sibling = node.previousSibling

                while not (isNull sibling) do
                    index <- index + 1
                    sibling <- sibling.previousSibling

                let id = (parent :?> Element).id
                $"text:{(if String.IsNullOrEmpty id then parent.nodeName else id)}#{index}@{scope node}"
            else
                let id = if node.nodeType = 1us then (node :?> Element).id else ""

                $"{node.nodeName}#{id}@{scope node}"

        let position (x: float) (y: float) (options: CaretPositionFromPointOptions option) =
            try
                let caret =
                    match options with
                    | Some options -> document.caretPositionFromPoint (x, y, options)
                    | None -> document.caretPositionFromPoint (x, y)

                if isNull caret then
                    "null"
                else
                    $"{label caret.offsetNode}:{caret.offset} {rectangle (caret.getClientRect ())}"
            with error ->
                "throws " + errorName error

        let range (x: float) (y: float) =
            let range = document.caretRangeFromPoint (long x, long y)

            if isNull range then
                "null"
            else
                $"{label range.startContainer}:{range.startOffset}"

        let waitForFont () =
            async {
                let mutable tries = 0

                while (document.getElementById "inline").getBoundingClientRect().width
                      <> 160.0
                      && tries < 200 do
                    tries <- tries + 1
                    do! driver.Settle ()

                equal "Ahem applied" 160.0 ((document.getElementById "inline").getBoundingClientRect().width)
            }

        for scale in CaretOracle.scales do
            do! driver.SetScale scale
            build ()
            do! waitForFont ()
            let listed = CaretPositionFromPointOptions ()
            listed.shadowRoots <- Array.ofList roots

            for name, sx, sy in CaretOracle.scrolls do
                scroller.ScrollTo (ScrollRequest.point sx sy)
                do! driver.Settle ()
                let actual = scroller.Position ()
                let offset = number actual.X + "," + number actual.Y

                match CaretOracle.expected.TryFind offset with
                | None -> failwith $"no Chromium case at the backend's scroll offset {offset} (requested {name})"
                | Some expected ->
                    for point, x, y in CaretOracle.points do
                        let chromiumPosition, chromiumShadow, chromiumRange = expected[point]
                        expectOutcome $"{name}|{point}|position" chromiumPosition (position x y None)
                        expectOutcome $"{name}|{point}|shadowRoots" chromiumShadow (position x y (Some listed))
                        expectOutcome $"{name}|{point}|range" chromiumRange (range x y)

            scroller.ScrollTo (ScrollRequest.point 0.0 0.0)
            do! driver.Settle ()

            for x, y in [ nan, 10.0; 10.0, infinity; -infinity, 10.0 ] do
                equal $"caretPositionFromPoint({x}, {y})" "throws TypeError" (position x y None)

            let held = document.caretPositionFromPoint (45.0, 30.0)
            let text = (document.getElementById "wrap").firstChild

            let read step =
                equal $"held {step} offsetNode" true (held.offsetNode.isSameNode text)

                let chromium =
                    CaretOracle.lifetime
                    |> List.find (fun (name, _) -> name = step)
                    |> snd

                let offset, connected, data, rect = chromium
                equal $"held {step} offset" offset held.offset
                equal $"held {step} isConnected" connected text.isConnected
                equal $"held {step} data" data (text :?> CharacterData).data
                expectOutcome $"held|{step}|getClientRect" rect (rectangle (held.getClientRect ()))

            read "before"
            (text :?> CharacterData).data <- "AA"
            do! driver.Settle ()
            read "shortened"
            (document.getElementById "wrap").remove()
            do! driver.Settle ()
            read "removed"
            let detached = document.implementation.createHTMLDocument ""
            detached.body.textContent <- "AAAA"
            equal "no render tree" true (isNull (detached.caretPositionFromPoint (5.0, 10.0)))

        equal "every qualification was observed" (driver.Qualified.Count * CaretOracle.scales.Length) qualified
        document.body.innerHTML <- ""
        sheet.remove ()
        do! driver.SetScale 1.0
        return checks
    }
