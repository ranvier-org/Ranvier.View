// Shared F# harness executing real Ranvier.View, Ranvier.View.Dom, Ranvier.View.Twin,
// Ranvier.View.WebCore, and Ranvier.View.WebCore.Sdl types and comparing with the Rocq models.
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Fable.Core.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.Dom.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.View.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.View.Dom.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.View.Testing.dll"
#r "../../experiments/Ranvier.View.Twin/bin/Release/net10.0/Ranvier.View.Twin.dll"
#r "../../src/Ranvier.View.WebCore.Sdl/bin/Release/net10.0/Ranvier.View.WebCore.dll"
#r "../../src/Ranvier.View.WebCore.Sdl/bin/Release/net10.0/Ranvier.View.WebCore.Sdl.dll"

open System
open System.Collections.Generic
open System.Reflection
open System.Threading
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Dom.Scheduling
open Ranvier.View.Twin

let private flags =
    BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

let getField (target: obj) (name: string) : obj =
    target.GetType().GetField(name, flags).GetValue target

let getProp (target: obj) (name: string) : obj =
    target.GetType().GetProperty(name, flags).GetValue target

let setProp (target: obj) (name: string) (value: obj) : unit =
    target.GetType().GetProperty(name, flags).SetValue(target, value)

let callMethod (ty: Type) (target: obj) (name: string) (args: obj array) : obj =
    let methods =
        ty.GetMethods flags
        |> Array.filter (fun m ->
            m.Name = name
            && m.GetParameters().Length = args.Length)

    methods[0].Invoke(target, args)

let strBool (b: bool) =
    if b then "true" else "false"

let fmtInts (xs: seq<int>) =
    "["
    + String.Join (";", xs |> Seq.map string)
    + "]"

type Sexp =
    | Atom of string
    | Node of Sexp list

let parseSexp (text: string) : Sexp =
    let n = text.Length
    let mutable i = 0

    let skip () =
        while i < n
              && (text[i] = ' ' || text[i] = '\t' || text[i] = '\r') do
            i <- i + 1

    let rec one () =
        skip ()

        if text[i] = '(' then
            i <- i + 1
            let items = ResizeArray<Sexp>()
            skip ()

            while text[i] <> ')' do
                items.Add (one ())
                skip ()

            i <- i + 1
            Node (List.ofSeq items)
        else
            let start = i

            while i < n
                  && text[i] <> ' '
                  && text[i] <> '\t'
                  && text[i] <> '\r'
                  && text[i] <> '('
                  && text[i] <> ')' do
                i <- i + 1

            Atom (text.Substring (start, i - start))

    one ()

let atomInt =
    function
    | Atom s -> int s
    | Node _ -> failwith "expected int atom"

let atomBool =
    function
    | Atom "1"
    | Atom "true" -> true
    | Atom "0"
    | Atom "false" -> false
    | _ -> failwith "expected bool atom"

let intList =
    function
    | Node xs -> xs |> List.map atomInt
    | Atom _ -> failwith "expected list"

// Assembly type handles for internal types
let private viewAsm = typeof<ViewScope>.Assembly
let private domAsm = typeof<Hydration.HydrationOptions>.Assembly
let private webCoreAsm = Assembly.Load "Ranvier.View.WebCore"
let private sdlAsm = Assembly.Load "Ranvier.View.WebCore.Sdl"

let private departuresType = viewAsm.GetType "Ranvier.View.Departures"
let private teardownWindowType = viewAsm.GetType "Ranvier.View.TeardownWindow"
let private templateDomType = domAsm.GetType "Ranvier.View.Dom.TemplateDom"

let private cursorType =
    domAsm.GetType "Ranvier.View.Dom.TemplateDom+HydrationCursor"

let private placementType = domAsm.GetType "Ranvier.View.Dom.Placement"
let private owqType = webCoreAsm.GetType "Ranvier.View.WebCore.OwnerWorkQueue"
let private cfType = sdlAsm.GetType "Ranvier.View.WebCore.Sdl.CompletionFence"

let private gpuFailType =
    sdlAsm.GetType "Ranvier.View.WebCore.Sdl.GpuCompletionFailure"

// 1. Layout + Departures over real Twin.mountView + Component.define + Departures
type LayoutCmd =
    | LRegExit of int
    | LReconcile of int list
    | LFinExit of int
    | LRfBefore
    | LRfPost of int

let runLayoutFSharp (initRows: int list) (cmds: LayoutCmd list) : string =
    let finishes = Dictionary<int, unit -> unit>()
    let elemById = Dictionary<int, HTMLLIElement>()

    let rowComp =
        Component.define (fun _ (item: unit -> int) -> item) (fun t ->
            t.li
                [|
                    t.Hooks.mounted (fun (item: unit -> int) (el: HTMLLIElement) ->
                        let id = item ()
                        el.setAttribute ("data-row-id", string id)
                        elemById[id] <- el
                        ignore)
                |]
                [| t.text (fun item -> string (item ())) |])

    let listComp =
        Component.define (fun _ (sigItems: Signal<int array>) -> sigItems) (fun t ->
            t.ul [||] [| t.each id (fun (s: Signal<int array>) -> s.Value) rowComp |])

    let sigRef = ref Unchecked.defaultof<Signal<int array>>

    use session =
        Twin.mountView TwinMount.defaults (fun context ->
            sigRef.Value <- Api.createSignal (List.toArray initRows)
            Program.run (Component.render listComp sigRef.Value) context.Backend)

    // Dedicated reflow element + start marker exercising real Departures.registerReflow / seam.Before
    let doc = session.Document.Native :?> Document
    let rfParent = doc.createElement "div"
    let rfStart = doc.createComment "rf-start"
    rfParent.appendChild rfStart |> ignore
    let mutable beforeRuns = 0
    let mutable afterRuns = 0

    let reflowSpec: Reflow =
        {
            Before = fun () -> beforeRuns <- beforeRuns + 1
            After = fun () -> afterRuns <- afterRuns + 1
        }

    let _unregRf =
        callMethod departuresType null "registerReflow" [| box (rfParent :> Node); box reflowSpec |] :?> (unit -> unit)

    let postedCallbacks = ResizeArray<unit -> unit>()

    let registerExitOn (rowId: int) =
        match elemById.TryGetValue rowId with
        | true, el ->
            let exitFn =
                Func<unit -> unit, unit -> unit>(fun finish ->
                    finishes[rowId] <- finish
                    ignore)

            let _cleanup =
                callMethod departuresType null "register" [| box (el :> Node); box exitFn |] :?> (unit -> unit)

            ()
        | false, _ -> ()

    for cmd in cmds do
        match cmd with
        | LRegExit n -> registerExitOn n
        | LReconcile next -> session.Act (fun () -> sigRef.Value.Value <- List.toArray next)
        | LFinExit n ->
            match finishes.TryGetValue n with
            | true, fin ->
                finishes.Remove n |> ignore
                fin ()
            | false, _ -> ()
        | LRfBefore ->
            let triggerPost =
                Departures.seam.Before.Invoke (rfStart :> Node, fun cb -> postedCallbacks.Add cb)

            triggerPost ()
        | LRfPost idx ->
            if idx >= 0 && idx < postedCallbacks.Count then
                postedCallbacks[idx]()

    let ul = (doc.querySelector "ul")
    let domIds = ResizeArray<int>()
    let liveIds = ResizeArray<int>()
    let mutable cur = ul.firstChild

    while not (isNull cur) do
        if cur.nodeType = 1us then
            let el = cur :?> Element
            let v = el.getAttribute "data-row-id"

            if not (isNull v) then
                domIds.Add (int v)

        cur <- cur.nextSibling

    // Walk live nodes via Departures.seam.NextLive if enabled, else standard nextSibling
    let stepNext (n: Node) =
        if Departures.seam.Enabled then
            Departures.seam.NextLive.Invoke n
        else
            n.nextSibling

    // First child of <ul> in compiled template t.each: check firstChild (if comment marker) or walk from synthetic start
    let dummyStart = doc.createComment "probe"

    ul.insertBefore (dummyStart, ul.firstChild)
    |> ignore

    let mutable liveCur = stepNext dummyStart

    while not (isNull liveCur) do
        if liveCur.nodeType = 1us then
            let el = liveCur :?> Element
            let v = el.getAttribute "data-row-id"

            if not (isNull v) then
                liveIds.Add (int v)

        liveCur <- stepNext liveCur

    dummyStart.remove ()

    // Read rf_open from Departures private `reflows` weakMap
    let reflowsTable =
        match departuresType.GetProperty ("reflows", flags) with
        | null -> departuresType.GetField("reflows", flags).GetValue(null)
        | prop -> prop.GetValue (null)

    let lookupMethod =
        departuresType.GetMethods (flags)
        |> Array.find (fun m -> m.Name = "lookup" && m.IsGenericMethodDefinition)
        |> fun m -> m.MakeGenericMethod ([| typeof<Node>; departuresType.GetNestedType ("ReflowEntry", flags) |])

    let entryObj =
        lookupMethod.Invoke (null, [| reflowsTable; box (rfParent :> Node) |])

    let isOpen = getProp entryObj "Open" :?> bool

    sprintf "dom=%s;live=%s;open=%s;before=%d;after=%d" (fmtInts domIds) (fmtInts liveIds) (strBool isOpen) beforeRuns afterRuns

// 2. SSR marker matching over real TwinHost + TemplateDom.logicalNext
type SsrTree =
    | SsrNode of int
    | SsrReg of int * SsrTree list

let rec appendSsrTree (doc: Document) (parent: Node) (t: SsrTree) =
    match t with
    | SsrNode id ->
        let sp = doc.createElement "span"
        sp.setAttribute ("data-id", string id)
        parent.appendChild sp |> ignore
    | SsrReg (tag, children) ->
        parent.appendChild (doc.createComment ("[" + string tag))
        |> ignore

        for c in children do
            appendSsrTree doc parent c

        parent.appendChild (doc.createComment "]")
        |> ignore

let runSsrFSharp (tree: SsrTree) (suf: int list) : string =
    let host = TwinHost.Create ()
    use _doc = host.Document
    let doc = host.Document.Native :?> Document
    let container = doc.createElement "div"
    appendSsrTree doc container tree

    for id in suf do
        let sp = doc.createElement "span"
        sp.setAttribute ("data-id", string id)
        container.appendChild sp |> ignore

    let first = container.firstChild

    let nextNode =
        callMethod templateDomType null "logicalNext" [| box first |] :?> Node

    let toks = ResizeArray<string>()
    let mutable cur = nextNode

    while not (isNull cur) do
        if cur.nodeType = 8us then
            let d = (cur :?> CharacterData).data

            if d = "]" then
                toks.Add "E"
            elif d.StartsWith "[" then
                toks.Add ("S" + d.Substring 1)
        elif cur.nodeType = 1us then
            let id = (cur :?> Element).getAttribute "data-id"
            toks.Add ("N" + id)

        cur <- cur.nextSibling

    "[" + String.Join (";", toks) + "]"

// 3. HydrationCursor LIFO undo + nested cursors + commit over real TemplateDom.HydrationCursor
type HydOp =
    | HOpAdd of int
    | HOpScope of HydOp list

let rec countHeldReal (cursor: obj) : int =
    let undo = getField cursor "undo" :?> ResizeArray<unit -> unit>
    let nested = getField cursor "nested" :?> System.Collections.IEnumerable

    let nestedSum =
        if isNull nested then
            0
        else
            let mutable s = 0

            for child in nested do
                s <- s + countHeldReal child

            s

    // Note: in HydrationCursor.Nest, each nested cursor ALSO adds 1 wrapper undo lambda to `undo`,
    // matching `NestedCursor child => S (held_list child)` in Hydration.v!
    let ownOnly =
        if isNull nested then
            undo.Count
        else
            let mutable childCount = 0

            for _ in nested do
                childCount <- childCount + 1

            undo.Count - childCount

    ownOnly
    + nestedSum
    + (if isNull nested then
           0
       else
           (let mutable c = 0 in

            (for _ in nested do
                c <- c + 1)

            c))

let runCursorFSharp (s0: int) (doCommit: bool) (ops: HydOp list) : string =
    let state = ref s0
    let ctor = cursorType.GetConstructors(flags)[0]

    let makeCursor () =
        ctor.Invoke ([| null; box true |])

    let rec execOps (cur: obj) (items: HydOp list) =
        for item in items do
            match item with
            | HOpAdd d ->
                state.Value <- state.Value + d
                let inv: unit -> unit = fun () -> state.Value <- state.Value - d

                callMethod cursorType cur "Undo" [| box inv |]
                |> ignore
            | HOpScope sub ->
                let child = makeCursor ()
                execOps child sub

                callMethod cursorType cur "Nest" [| child |]
                |> ignore

    let rootCursor = makeCursor ()
    execOps rootCursor ops
    let sMid = state.Value

    if doCommit then
        callMethod cursorType rootCursor "Commit" [||]
        |> ignore

        let held = countHeldReal rootCursor
        sprintf "mid=%d;after=%d;held=%d" sMid state.Value held
    else
        let held = countHeldReal rootCursor

        callMethod cursorType rootCursor "Restore" [||]
        |> ignore

        sprintf "mid=%d;after=%d;held=%d" sMid state.Value held

// 4. Boundary + BindingState over real Ranvier.View.BindingState
type BndOutcome =
    | BVal
    | BPending
    | BErr

type BndCmd =
    | BExpect
    | BStep of int * BndOutcome
    | BDispose of int
    | BEndSetup
    | BReveal
    | BRetry of int

let runBoundaryFSharp (tracks: bool) (cmds: BndCmd list) : string =
    let mutable waiting = 0
    let mutable settingUp = true
    let mutable revealed = false
    let mutable failed = false
    let mutable pendingSig = false
    let mutable wakes = 0
    let mutable clearCalls = 0
    let mutable failCalls = 0
    let mutable gen = 0
    let slots = ResizeArray<int>()

    let scope =
        { new ViewScope with
            member _.Tracks = tracks

            member _.Wait() =
                waiting <- waiting + 1

                if revealed then
                    pendingSig <- true

            member _.Expect() =
                if revealed || failed then
                    false
                else
                    waiting <- waiting + 1
                    true

            member _.Ready() =
                if waiting > 0 then
                    waiting <- waiting - 1

                    if waiting = 0 then
                        if revealed then
                            pendingSig <- false
                        elif not settingUp then
                            wakes <- wakes + 1

            member _.Fail(errors) =
                let real =
                    errors
                    |> List.filter (function
                        | NotReadyException _ -> false
                        | _ -> true)

                if not real.IsEmpty then
                    failed <- true
                    failCalls <- failCalls + 1
                    gen <- gen + 1

            member this.FailAll(errors) =
                if not errors.IsEmpty then
                    failed <- true
                    failCalls <- failCalls + 1
                    gen <- gen + 1
        }

    let dummyNotReady =
        let g = new Graph ()
        let mutable captured: exn = null

        g.Run (fun () ->
            let src = Api.createAsyncSource<int>()

            try
                src.Value |> ignore
            with ex ->
                captured <- ex)

        g.Dispose ()
        captured

    for cmd in cmds do
        match cmd with
        | BExpect ->
            let st = BindingState.started scope
            slots.Add st
        | BStep (idx, out) ->
            if idx >= 0 && idx < slots.Count then
                let st = slots[idx]

                match out with
                | BPending ->
                    let st' = BindingState.rejected scope st dummyNotReady
                    slots[idx] <- st'
                    BindingState.report scope dummyNotReady
                | BErr ->
                    let boom = InvalidOperationException "boom"
                    let st' = BindingState.rejected scope st boom
                    slots[idx] <- st'
                    BindingState.report scope boom
                | BVal ->
                    let st1 = BindingState.recovered (fun () -> clearCalls <- clearCalls + 1) st
                    let st2 = BindingState.released st1
                    slots[idx] <- st2
                    BindingState.ready scope st1
        | BDispose idx ->
            if idx >= 0 && idx < slots.Count then
                let prev = slots[idx]
                slots[idx] <- BindingState.released prev
                BindingState.ready scope prev
        | BEndSetup -> settingUp <- false
        | BReveal ->
            if waiting = 0 && not failed then
                settingUp <- false
                revealed <- true
                pendingSig <- false
        | BRetry g ->
            if failed && g = gen then
                waiting <- 0
                settingUp <- false
                revealed <- false
                failed <- false
                pendingSig <- false
                slots.Clear ()

    sprintf
        "wait=%d;setup=%s;rev=%s;fail=%s;pend=%s;wakes=%d;clear=%d;fails=%d;gen=%d"
        waiting
        (strBool settingUp)
        (strBool revealed)
        (strBool failed)
        (strBool pendingSig)
        wakes
        clearCalls
        failCalls
        gen

// 5. Placement over real Ranvier.View.Dom.Placement
type PlCmd =
    | PEnter
    | PLeave
    | PAdd of int
    | PRelChild of int
    | PPlace
    | PDiscard

let runPlacementFSharp (parentPlaced: bool) (cmds: PlCmd list) : string =
    use graph = new Graph ()
    let pCtor = placementType.GetConstructors(flags)[0]
    let parent = pCtor.Invoke ([| null |])
    let executed = ResizeArray<int>()
    let toParent = ResizeArray<int>()

    if parentPlaced then
        callMethod placementType parent "Place" [||]
        |> ignore

    let child = pCtor.Invoke ([| parent |])
    let stack = Stack<int>()

    graph.Run (fun () ->
        Api.createRoot (fun owner ->
            for cmd in cmds do
                match cmd with
                | PEnter ->
                    let prev = callMethod placementType child "Enter" [||] :?> int
                    stack.Push prev
                | PLeave ->
                    if stack.Count > 0 then
                        let prev = stack.Pop ()

                        callMethod placementType child "Leave" [| box prev |]
                        |> ignore
                | PAdd h ->
                    let cb: unit -> unit = fun () -> executed.Add h

                    callMethod placementType child "Add" [| box owner; box cb |]
                    |> ignore
                | PRelChild h ->
                    let cb: unit -> unit = fun () -> executed.Add h

                    callMethod placementType child "Enqueue" [| box owner; box cb; box true |]
                    |> ignore
                | PPlace ->
                    callMethod placementType child "Place" [||]
                    |> ignore

                    Api.flush ()
                | PDiscard ->
                    callMethod placementType child "Discard" [||]
                    |> ignore

            Api.flush ())
        |> ignore)

    // Any hooks forwarded into unplaced `parent` sit in `parent.forwarded`!
    let parentFwd = getField parent "forwarded" :?> System.Collections.IEnumerable

    if not (isNull parentFwd) then
        // Run each forwarded hook in order to read its hook id!
        graph.Run (fun () ->
            for item in parentFwd do
                let hookFn = item.GetType().GetProperty("Item2").GetValue(item) :?> (unit -> unit)
                let before = executed.Count
                hookFn ()

                if executed.Count > before then
                    let added = executed[executed.Count - 1]
                    executed.RemoveAt (executed.Count - 1)
                    toParent.Add added)

    let childHeld = getField child "held" :?> System.Collections.ICollection
    let childFwd = getField child "forwarded" :?> System.Collections.ICollection

    let heldCount =
        (if isNull childHeld then 0 else childHeld.Count)
        + (if isNull childFwd then 0 else childFwd.Count)

    sprintf "held=%d;exec=%s;parent=%s" heldCount (fmtInts executed) (fmtInts toParent)

// 6. MountLifetime tree over real Ranvier.View.MountLifetime
type LtTree = LtNode of int * int list * LtTree list

let runLifetimeFSharp (tree: LtTree) : string =
    use graph = new Graph ()
    let trace = ResizeArray<string>()

    let rec buildChild (parent: MountLifetime) (LtNode (id, cleanups, children)) =
        parent.Child (
            (fun () -> trace.Add (sprintf "R(%d)" id)),
            fun lt ->
                for c in cleanups do
                    lt.OnCleanup (fun () -> trace.Add (sprintf "C(%d,%d)" id c))

                for child in children do
                    buildChild lt child |> ignore
        )
        |> ignore

    let (LtNode (rootId, rootCleanups, rootChildren)) = tree

    let rootLt =
        graph.Run (fun () ->
            Api.createRoot (fun owner ->
                let lt =
                    MountLifetime.Create (graph, owner, ignore, (fun () -> trace.Add (sprintf "R(%d)" rootId)), null)

                for c in rootCleanups do
                    lt.OnCleanup (fun () -> trace.Add (sprintf "C(%d,%d)" rootId c))

                for child in rootChildren do
                    buildChild lt child

                lt))

    rootLt.Dispose ()
    "[" + String.Join (";", trace) + "]"

// 7. TeardownWindow over real Ranvier.View.TeardownWindow
let runWindowFSharp (start: int) (len: int) (nested: (int * int) list) : string =
    use graph = new Graph ()
    let total = start + len

    // Record `total` numbered errors onto `sub` via failing late cleanups on a disposed Owner
    let sub =
        graph.Run (fun () ->
            let owner =
                Api.createRoot (fun o ->
                    o.Dispose ()
                    o)

            for i in 0 .. total - 1 do
                owner.OnCleanup (Action (fun () -> raise (Exception (string i))))

            owner)

    let wCtor = teardownWindowType.GetConstructors(flags)[0]
    let win = wCtor.Invoke ([| box sub; box start; box (ignore: exn list -> unit) |])

    for (s, f) in nested do
        callMethod teardownWindowType win "Exclude" [| box s; box f |]
        |> ignore

    let errs = callMethod teardownWindowType win "Errors" [||] :?> exn list
    let indices = errs |> List.map (fun e -> int e.Message)
    fmtInts indices

// 8. Scheduling.Queue over real Ranvier.View.Dom.Scheduling.Queue
type QCmd =
    | QRes of int * bool
    | QEnq of int * int
    | QCan of int
    | QFlu of CommitFailure * int list
    | QTik of int * CommitFailure * int list
    | QDis

let runQueueFSharp (cmds: QCmd list) : string =
    use graph = new Graph ()
    let mutable activeFailing = HashSet<int>()
    let mutable activeMode = CollectAndContinue
    let posted = ResizeArray<unit -> unit>()
    let committed = ResizeArray<int>()
    let droppedCounts = ResizeArray<int>()
    let reportedBatches = ResizeArray<int * int list>()
    let scopes = Dictionary<int, ViewScope>()

    let getScope (sc: int) : ViewScope =
        if sc = 0 then
            Unchecked.defaultof<ViewScope>
        else
            match scopes.TryGetValue sc with
            | true, existing -> existing
            | false, _ ->
                let created =
                    { new ViewScope with
                        member _.Tracks = false
                        member _.Wait() = ()
                        member _.Expect() = false
                        member _.Ready() = ()

                        member _.Fail(errs) =
                            reportedBatches.Add (sc, errs |> List.map (fun e -> int e.Message))

                        member _.FailAll(errs) =
                            reportedBatches.Add (sc, errs |> List.map (fun e -> int e.Message))
                    }

                scopes[sc] <- created
                created

    // Queue's constructor takes `failure: CommitFailure`; since `failure` is a private field on Queue,
    // we update it right before Flush/Ticket so a single trace can test both modes if desired.
    let queue = Queue (graph, CollectAndContinue, fun cb -> posted.Add cb)
    let failureField = typeof<Queue>.GetField("failure", flags)
    let slots = ResizeArray<Slot>()

    for cmd in cmds do
        match cmd with
        | QRes (sc, hasDrop) ->
            let isDisp = getField queue "disposed" :?> bool

            if not isDisp then
                let slotIdx = slots.Count
                droppedCounts.Add 0
                let sl = callMethod typeof<Queue> queue "Reserve" [| box (getScope sc) |] :?> Slot

                if hasDrop then
                    setProp sl "Dropped" (box (fun () -> droppedCounts[slotIdx] <- droppedCounts[slotIdx] + 1))

                slots.Add sl
        | QEnq (idx, act) ->
            if idx >= 0 && idx < slots.Count then
                queue.Enqueue (
                    slots[idx],
                    fun () ->
                        if activeFailing.Contains act then
                            raise (Exception (string act))
                        else
                            committed.Add act
                )
        | QCan idx ->
            if idx >= 0 && idx < slots.Count then
                queue.Cancel slots[idx]
        | QFlu (mode, failing) ->
            reportedBatches.Clear ()
            failureField.SetValue (queue, mode)
            activeMode <- mode
            activeFailing <- HashSet<int>(failing)
            queue.Flush ()
        | QTik (tidx, mode, failing) ->
            if tidx >= 0 && tidx < posted.Count then
                let wasDisposed = getField queue "disposed" :?> bool
                let genBefore = getField queue "generation" :?> int
                failureField.SetValue (queue, mode)
                activeMode <- mode
                activeFailing <- HashSet<int>(failing)
                // Only clear `reportedBatches` if the ticket actually runs a flush!
                let tempBatches = List.ofSeq reportedBatches
                reportedBatches.Clear ()
                posted[tidx]()

                let genAfter = getField queue "generation" :?> int

                if wasDisposed || genBefore = genAfter then
                    reportedBatches.Clear ()
                    reportedBatches.AddRange tempBatches
        | QDis -> queue.Dispose ()

    let pendingList = getField queue "pending" :?> System.Collections.ICollection
    let isDisp = getField queue "disposed" :?> bool

    let repStr =
        "["
        + String.Join (
            ",",
            reportedBatches
            |> Seq.map (fun (sc, es) -> sprintf "%d:%s" sc (fmtInts es))
        )
        + "]"

    sprintf "comm=%s;drop=%s;rep=%s;pend=%d;disp=%s" (fmtInts committed) (fmtInts droppedCounts) repStr pendingList.Count (strBool isDisp)

// 9. OwnerWorkQueue over real Ranvier.View.WebCore.OwnerWorkQueue
type OwqCmd =
    | OEnq of int * int
    | ODeq
    | OWake of bool
    | OStop

let runOwqFSharp (cmds: OwqCmd list) : string =
    let owqCtor = owqType.GetConstructors(flags)[0]
    let owq = owqCtor.Invoke ([| None; None |])
    let mutable wakes = 0
    let deqItems = ResizeArray<int>()
    let tryDeqMethod = owqType.GetMethod ("TryDequeue", flags)

    for cmd in cmds do
        match cmd with
        | OEnq (item, cap) ->
            let cb = SendOrPostCallback (ignore)

            callMethod owqType owq "TryEnqueue" [| box cb; box item; box cap |]
            |> ignore
        | ODeq ->
            let args: obj array = [| null |]
            let ok = tryDeqMethod.Invoke (owq, args) :?> bool

            if ok then
                let tup = args[0]
                let item = tup.GetType().GetField("Item2").GetValue(tup) :?> int
                deqItems.Add item
        | OWake hasCb ->
            let cbOpt: (unit -> unit) option =
                if hasCb then Some (fun () -> wakes <- wakes + 1) else None

            callMethod owqType owq "SetWakeCallback" [| box cbOpt |]
            |> ignore
        | OStop -> callMethod owqType owq "Stop" [||] |> ignore

    let cnt = getProp owq "Count" :?> int
    let rej = getProp owq "Rejected" :?> int64
    let disc = getProp owq "Discarded" :?> int64
    let stp = getProp owq "IsStopped" :?> bool

    sprintf "deq=%s;cnt=%d;wakes=%d;rej=%d;disc=%d;stop=%s" (fmtInts deqItems) cnt wakes (int rej) (int disc) (strBool stp)

// 10. CompletionFence over real Ranvier.View.WebCore.Sdl.CompletionFence
type CfPoll =
    | PNotReady
    | PReady
    | PGpuFail

type CfCmd =
    | CTry of CfPoll
    | CReset

let runFenceFSharp (cmds: CfCmd list) : string =
    let mutable nextMarker = 1
    let mutable currentPoll = PNotReady
    let released = ResizeArray<int>()
    let outs = ResizeArray<string>()
    let gpuFailCtor = gpuFailType.GetConstructors(flags)[0]

    let startFn: unit -> nativeint =
        fun () ->
            let m = nextMarker
            nextMarker <- nextMarker + 1
            nativeint m

    let readyFn: nativeint -> bool =
        fun _ ->
            match currentPoll with
            | PNotReady -> false
            | PReady -> true
            | PGpuFail -> raise (gpuFailCtor.Invoke ([| box "gpu boom" |]) :?> exn)

    let waitFn: nativeint -> unit = ignore
    let releaseFn: nativeint -> unit = fun m -> released.Add (int m)

    let cfCtor = cfType.GetConstructors(flags)[0]

    let fence =
        cfCtor.Invoke ([| box startFn; box readyFn; box waitFn; box releaseFn |])

    for cmd in cmds do
        match cmd with
        | CTry p ->
            currentPoll <- p

            try
                let ok = callMethod cfType fence "TryComplete" [||] :?> bool
                outs.Add (if ok then "ok1" else "ok0")
            with :? TargetInvocationException as tie when gpuFailType.IsInstanceOfType tie.InnerException ->
                outs.Add "err"
        | CReset ->
            try
                callMethod cfType fence "Reset" [||] |> ignore
                outs.Add "ok1"
            with :? TargetInvocationException ->
                outs.Add "err"

    let isFailed = getField fence "failed" :?> bool
    sprintf "outs=[%s];rel=%s;fail=%s" (String.Join (";", outs)) (fmtInts released) (strBool isFailed)

// Unified S-expression evaluator for differential runner
let rec parseSsrTree =
    function
    | Node [ Atom "node"; id ] -> SsrNode (atomInt id)
    | Node (Atom "reg" :: tag :: children) -> SsrReg (atomInt tag, children |> List.map parseSsrTree)
    | _ -> failwith "bad ssr_tree"

let rec parseHydOp =
    function
    | Node [ Atom "add"; d ] -> HOpAdd (atomInt d)
    | Node (Atom "scope" :: sub) -> HOpScope (sub |> List.map parseHydOp)
    | _ -> failwith "bad hyd_op"

let rec parseLtTree =
    function
    | Node (Atom "lt" :: id :: cleanups :: children) -> LtNode (atomInt id, intList cleanups, children |> List.map parseLtTree)
    | _ -> failwith "bad lt_tree"

let parseMode =
    function
    | Atom "collect" -> CollectAndContinue
    | Atom "failfast" -> FailFastCancel
    | _ -> failwith "bad mode"

let evalSexpFSharp (s: Sexp) : string =
    match s with
    | Node (Atom "layout" :: initRows :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "reg-exit"; n ] -> LRegExit (atomInt n)
                | Node [ Atom "reconcile"; rows ] -> LReconcile (intList rows)
                | Node [ Atom "fin-exit"; n ] -> LFinExit (atomInt n)
                | Node [ Atom "rf-before" ] -> LRfBefore
                | Node [ Atom "rf-post"; idx ] -> LRfPost (atomInt idx)
                | _ -> failwith "bad layout cmd")

        runLayoutFSharp (intList initRows) parsed
    | Node [ Atom "ssr"; tree; suf ] -> runSsrFSharp (parseSsrTree tree) (intList suf)
    | Node (Atom "cursor" :: s0 :: doCommit :: ops) -> runCursorFSharp (atomInt s0) (atomBool doCommit) (ops |> List.map parseHydOp)
    | Node (Atom "boundary" :: tracks :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "expect" ] -> BExpect
                | Node [ Atom "step"; idx; Atom "val" ] -> BStep (atomInt idx, BVal)
                | Node [ Atom "step"; idx; Atom "pending" ] -> BStep (atomInt idx, BPending)
                | Node [ Atom "step"; idx; Atom "err" ] -> BStep (atomInt idx, BErr)
                | Node [ Atom "dispose"; idx ] -> BDispose (atomInt idx)
                | Node [ Atom "end-setup" ] -> BEndSetup
                | Node [ Atom "reveal" ] -> BReveal
                | Node [ Atom "retry"; g ] -> BRetry (atomInt g)
                | _ -> failwith "bad bnd cmd")

        runBoundaryFSharp (atomBool tracks) parsed
    | Node (Atom "placement" :: parentPlaced :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "enter" ] -> PEnter
                | Node [ Atom "leave" ] -> PLeave
                | Node [ Atom "add"; h ] -> PAdd (atomInt h)
                | Node [ Atom "rel-child"; h ] -> PRelChild (atomInt h)
                | Node [ Atom "place" ] -> PPlace
                | Node [ Atom "discard" ] -> PDiscard
                | _ -> failwith "bad pl cmd")

        runPlacementFSharp (atomBool parentPlaced) parsed
    | Node [ Atom "lifetime"; tree ] -> runLifetimeFSharp (parseLtTree tree)
    | Node (Atom "window" :: start :: len :: ivs) ->
        let pairs =
            ivs
            |> List.map (function
                | Node [ a; b ] -> atomInt a, atomInt b
                | _ -> failwith "bad pair")

        runWindowFSharp (atomInt start) (atomInt len) pairs
    | Node (Atom "queue" :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "reserve"; sc; hd ] -> QRes (atomInt sc, atomBool hd)
                | Node [ Atom "enqueue"; idx; act ] -> QEnq (atomInt idx, atomInt act)
                | Node [ Atom "cancel"; idx ] -> QCan (atomInt idx)
                | Node [ Atom "flush"; mode; failing ] -> QFlu (parseMode mode, intList failing)
                | Node [ Atom "ticket"; tidx; mode; failing ] -> QTik (atomInt tidx, parseMode mode, intList failing)
                | Node [ Atom "dispose" ] -> QDis
                | _ -> failwith "bad q cmd")

        runQueueFSharp parsed
    | Node (Atom "owq" :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "enq"; item; cap ] -> OEnq (atomInt item, atomInt cap)
                | Node [ Atom "deq" ] -> ODeq
                | Node [ Atom "wake"; cb ] -> OWake (atomBool cb)
                | Node [ Atom "stop" ] -> OStop
                | _ -> failwith "bad owq cmd")

        runOwqFSharp parsed
    | Node (Atom "fence" :: cmds) ->
        let parsed =
            cmds
            |> List.map (function
                | Node [ Atom "try"; Atom "not-ready" ] -> CTry PNotReady
                | Node [ Atom "try"; Atom "ready" ] -> CTry PReady
                | Node [ Atom "try"; Atom "gpu-fail" ] -> CTry PGpuFail
                | Node [ Atom "reset" ] -> CReset
                | _ -> failwith "bad cf cmd")

        runFenceFSharp parsed
    | _ -> failwith "unknown sexp program"
