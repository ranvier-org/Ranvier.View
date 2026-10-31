module internal NativeApiConformance.TreeTraversal

open System
open Ranvier.Dom

/// <summary>The provider's measured outcome of a filter that re-enters its own traversal.</summary>
type Reentry =
    {
        Iterator: string
        Walker: string
        /// <summary>The iterator's referenceNode after the re-entered call, or "unread".</summary>
        Reference: string
    }

exception FilterFailure of string

[<Literal>]
let private ShowElement = 0x1u

[<Literal>]
let private ShowText = 0x4u

[<Literal>]
let private ShowComment = 0x80u

/// <summary>
/// Checks NodeIterator, TreeWalker and NodeFilter on the supplied engine document, returning the assertion count and
/// the re-entry outcomes. <c>errorName</c> names an engine exception, such as <c>InvalidStateError</c>.
/// <c>readReference</c> reads the iterator's referenceNode after a re-entered call; Chromium 156 crashes its renderer there.
/// </summary>
let run (document: Document) (errorName: exn -> string) (readReference: bool) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label (expected: obj) (actual: obj) =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: expected the same object"

    let root = document.createElement "div"
    root.id <- "native-api-traversal"

    root.innerHTML <-
        "<p id=\"traversal-a\">one<!--c--><span id=\"traversal-b\">two</span></p><section id=\"traversal-c\"><em id=\"traversal-d\"></em></section>three"

    document.body.appendChild root |> ignore
    let p = document.getElementById "traversal-a"
    let span = document.getElementById "traversal-b"
    let section = document.getElementById "traversal-c"
    let em = document.getElementById "traversal-d"

    let name (node: Node) =
        if isNull node then "null" else node.nodeName

    let iterate (iterator: NodeIterator) =
        [
            let mutable node = iterator.nextNode ()

            while not (isNull node) do
                yield node
                node <- iterator.nextNode ()
        ]

    let names nodes = nodes |> List.map name |> String.concat ","

    let texts nodes =
        nodes
        |> List.map (fun (node: Node) -> node.textContent)
        |> String.concat ","

    try
        // NodeIterator defaults.
        let all = document.createNodeIterator root
        same "iterator root" root all.root
        same "initial referenceNode" root all.referenceNode
        equal "initial pointerBeforeReferenceNode" true all.pointerBeforeReferenceNode
        equal "default whatToShow" 0xFFFFFFFFu all.whatToShow
        equal "default filter" true all.filter.IsNone
        equal "all nodes" "DIV,P,#text,#comment,SPAN,#text,SECTION,EM,#text" (names (iterate all))
        equal "end pointerBeforeReferenceNode" false all.pointerBeforeReferenceNode
        equal "end referenceNode" "three" all.referenceNode.textContent
        equal "end nextNode" "null" (name (all.nextNode ()))
        equal "previousNode after end" "three" (all.previousNode ()).textContent
        equal "previous pointerBeforeReferenceNode" true all.pointerBeforeReferenceNode
        equal "previousNode" "EM" (name (all.previousNode ()))
        all.detach ()
        equal "previousNode after detach" "SECTION" (name (all.previousNode ()))

        // whatToShow.
        equal "elements" "DIV,P,SPAN,SECTION,EM" (names (iterate (document.createNodeIterator (root, ShowElement))))
        equal "texts" "one,two,three" (texts (iterate (document.createNodeIterator (root, ShowText))))
        equal "comments" "c" (texts (iterate (document.createNodeIterator (root, ShowComment))))
        equal "elements and comments" "DIV,P,#comment,SPAN,SECTION,EM" (names (iterate (document.createNodeIterator (root, ShowElement ||| ShowComment))))
        equal "whatToShow getter" (ShowElement ||| ShowComment) (document.createNodeIterator (root, ShowElement ||| ShowComment)).whatToShow

        // Filters.
        let mutable seen = []

        let rejectSpan: NodeFilter =
            fun node ->
                seen <- node :: seen

                if obj.ReferenceEquals (node, span) then
                    NodeFilterResult.Reject
                else
                    NodeFilterResult.Accept

        let filtered = document.createNodeIterator (root, ShowElement, rejectSpan)

        match filtered.filter with
        | Some filter -> same "iterator filter getter" rejectSpan filter
        | None -> failwith "iterator filter getter: expected the filter"

        checks <- checks + 1
        equal "iterator reject" "DIV,P,SECTION,EM" (names (iterate filtered))
        equal "filter calls" 5 seen.Length
        same "filter receives the node object" span (seen |> List.find (fun node -> node.nodeName = "SPAN"))

        let skipP: NodeFilter =
            fun node ->
                if obj.ReferenceEquals (node, p) then
                    NodeFilterResult.Skip
                else
                    NodeFilterResult.Accept

        equal "iterator skip" "DIV,SPAN,SECTION,EM" (names (iterate (document.createNodeIterator (root, ShowElement, skipP))))

        // TreeWalker.
        let plain = document.createTreeWalker (root, ShowElement)
        same "walker root" root plain.root
        same "initial currentNode" root plain.currentNode
        equal "walker whatToShow" ShowElement plain.whatToShow
        equal "walker default filter" true plain.filter.IsNone
        same "firstChild" p (plain.firstChild ())
        same "firstChild moves currentNode" p plain.currentNode
        same "nextSibling" section (plain.nextSibling ())
        same "previousSibling" p (plain.previousSibling ())
        equal "previousSibling at first" "null" (name (plain.previousSibling ()))
        same "parentNode" root (plain.parentNode ())
        equal "parentNode at root" "null" (name (plain.parentNode ()))
        same "lastChild" section (plain.lastChild ())
        plain.currentNode <- em
        same "currentNode setter" em plain.currentNode
        same "previousNode" section (plain.previousNode ())
        same "nextNode" em (plain.nextNode ())
        equal "nextNode at end" "null" (name (plain.nextNode ()))
        plain.currentNode <- document.body
        equal "currentNode outside root" "BODY" (name plain.currentNode)

        let byResult (result: NodeFilterResult) : NodeFilter =
            fun node ->
                if obj.ReferenceEquals (node, section) then
                    result
                else
                    NodeFilterResult.Accept

        let walk (walker: TreeWalker) =
            [
                let mutable node = walker.nextNode ()

                while not (isNull node) do
                    yield node
                    node <- walker.nextNode ()
            ]

        let rejectSection = byResult NodeFilterResult.Reject
        let rejecting = document.createTreeWalker (root, ShowElement, rejectSection)

        match rejecting.filter with
        | Some filter -> same "walker filter getter" rejectSection filter
        | None -> failwith "walker filter getter: expected the filter"

        checks <- checks + 1
        equal "walker reject drops the subtree" "P,SPAN" (names (walk rejecting))
        rejecting.currentNode <- root
        same "walker reject lastChild" p (rejecting.lastChild ())

        let skipping =
            document.createTreeWalker (root, ShowElement, byResult NodeFilterResult.Skip)

        equal "walker skip keeps the children" "P,SPAN,EM" (names (walk skipping))
        same "walker skip parentNode" root (skipping.parentNode ())
        skipping.currentNode <- root
        same "walker skip lastChild" em (skipping.lastChild ())
        skipping.currentNode <- p
        same "walker skip nextSibling" em (skipping.nextSibling ())

        // A filter exception stops the traversal and reaches the caller.
        let mutable failing = true
        let failure = FilterFailure "filter failed"

        let throwAtSpan: NodeFilter =
            fun node ->
                if failing && obj.ReferenceEquals (node, span) then
                    raise failure
                else
                    NodeFilterResult.Accept

        let throwing = document.createNodeIterator (root, ShowElement, throwAtSpan)
        same "iterator before failure" root (throwing.nextNode ())
        same "iterator before failure p" p (throwing.nextNode ())

        let raised =
            try
                throwing.nextNode () |> ignore
                null
            with error ->
                box error

        same "iterator rethrows the filter exception" failure raised
        same "iterator referenceNode after failure" p throwing.referenceNode
        failing <- false
        same "iterator resumes" span (throwing.nextNode ())
        failing <- true
        let throwingWalker = document.createTreeWalker (root, ShowElement, throwAtSpan)
        throwingWalker.currentNode <- p

        let raised =
            try
                throwingWalker.firstChild () |> ignore
                null
            with error ->
                box error

        same "walker rethrows the filter exception" failure raised
        same "walker currentNode after failure" p throwingWalker.currentNode
        failing <- false
        same "walker resumes" span (throwingWalker.firstChild ())

        // Live removal adjusts the iterator's reference node.
        let live = document.createNodeIterator (root, ShowElement ||| ShowComment)

        while not (obj.ReferenceEquals (live.nextNode (), span)) do
            ()

        p.removeChild span |> ignore
        equal "reference after removal" "#comment" (name live.referenceNode)
        same "next after removal" section (live.nextNode ())
        p.appendChild span |> ignore

        // Re-entry from a filter.
        let outcome (action: unit -> unit) =
            try
                action ()
                "reentrant"
            with error ->
                errorName error

        let mutable reentrantIterator: NodeIterator = null
        let mutable iteratorOutcome = "not called"

        let iteratorFilter: NodeFilter =
            fun node ->
                if iteratorOutcome = "not called" && obj.ReferenceEquals (node, p) then
                    iteratorOutcome <- "pending"
                    iteratorOutcome <- outcome (fun () -> reentrantIterator.nextNode () |> ignore)

                NodeFilterResult.Accept

        reentrantIterator <- document.createNodeIterator (root, ShowElement, iteratorFilter)
        same "re-entered iterator first" root (reentrantIterator.nextNode ())
        same "re-entered iterator result" p (reentrantIterator.nextNode ())

        let reference =
            if readReference then
                name reentrantIterator.referenceNode
            else
                "unread"

        let mutable reentrantWalker: TreeWalker = null
        let mutable walkerOutcome = "not called"

        let walkerFilter: NodeFilter =
            fun node ->
                if walkerOutcome = "not called" && obj.ReferenceEquals (node, p) then
                    walkerOutcome <- "pending"
                    walkerOutcome <- outcome (fun () -> reentrantWalker.nextNode () |> ignore)

                NodeFilterResult.Accept

        reentrantWalker <- document.createTreeWalker (root, ShowElement, walkerFilter)
        equal "re-entered walker" "P,SPAN,SECTION,EM" (names (walk reentrantWalker))

        checks,
        {
            Iterator = iteratorOutcome
            Walker = walkerOutcome
            Reference = reference
        }
    finally
        root.remove ()
