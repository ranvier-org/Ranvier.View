/// Fine-grained hot reload. In dev mode every component definition is mounted through an
/// instrumented copy of its template:
///   - static attribute values and static text become Signal<string> cells (one slot each);
///   - every slot reader, event handler, region items/key/row-conversion and child-props reader is called
///     through a swappable cell.
/// A reload diffs the new definition against the cells. Same structure: write only the cells whose value or
/// code fingerprint changed (no remount, no DOM rebuild, instances and their signals kept). Different
/// structure: report it, and the host falls back to a remount with state transplant.
module Ranvier.View.WebCore.Hmr.Fine

open System
open System.Collections.Generic
open System.Text
open Ranvier
open Ranvier.View

type Instance =
    {
        Inst: obj
        mutable Current: obj
        /// The context the backend passed to setup; each setup run gets a copy whose cleanups go to its scope.
        Original: Setup
        Props: obj
        /// The instance's owner (the mount's scope for this instance).
        Owner: Owner voption
        /// The nested scope of the current setup run: its effects, memos and cleanups. Disposed on a bridge.
        mutable Scope: Owner
    }

/// Runs a setup in a nested scope of the current owner, with `OnCleanup` redirected into that scope, so the
/// run's effects, memos and cleanups can be disposed when a reload replaces it. Effects created by setup are
/// queued, and the batch runs them after `after` (the transplant), so they derive from the moved state.
let runScoped (setup: Func<Setup, obj, obj>) (context: Setup) (props: obj) (after: obj -> unit) : obj * Owner =
    let scope = ref Unchecked.defaultof<Owner>

    let context' =
        { context with
            OnCleanup = fun f -> Api.runWithOwner scope.Value (fun () -> Api.onCleanup f)
        }

    let inst =
        Api.batch (fun () ->
            let inst =
                Api.createRoot (fun o ->
                    scope.Value <- o
                    setup.Invoke (context', props))

            after inst
            inst)

    inst, scope.Value

type Cells(key: string) =
    member val Key = key
    member val Shape = "" with get, set
    member val Setup: Func<Setup, obj, obj> = null with get, set
    member val SetupPrint = "" with get, set
    member val Statics = ResizeArray<Signal<string>>()
    member val Funcs = ResizeArray<Signal<obj>>()
    member val FuncPrints = ResizeArray<string>()
    member val Handlers = ResizeArray<obj ref>()
    member val HandlerPrints = ResizeArray<string>()
    member val Live = Dictionary<obj, Instance>(HashIdentity.Reference)
    member val Order = ResizeArray<obj>()
    /// Read by every bridged call: a bridge bumps it, so readers that kept their code still re-run against
    /// the new instance. Cells are built inside graph.Run.
    member val Epoch = Api.createSignal 0

    member this.Bridge(inst: obj) =
        if isNull inst then
            inst
        else
            this.Epoch.Value |> ignore

            match this.Live.TryGetValue inst with
            | true, i -> i.Current
            | _ -> inst

/// What a walk does with each swappable part: create cells (mount) or collect values (diff).
type Sink =
    abstract Static: string -> (unit -> string)
    abstract Func: obj -> (unit -> obj)
    abstract Handler: obj -> (unit -> obj)
    abstract Child: TemplateComponent * string -> TemplateComponent * string

let private sameOption =
    Func<obj, obj, bool>(fun a b -> (unbox<string option> a) = (unbox<string option> b))

let private sameString =
    Func<obj, obj, bool>(fun a b -> String.Equals (unbox<string> a, unbox<string> b))

let private tagName tag =
    match tag with
    | Tag.Div -> "div"
    | Tag.Button -> "button"
    | Tag.Input -> "input"
    | Tag.Select -> "select"
    | Tag.Option -> "option"
    | Tag.HtmlTag s -> s

let private targetName target =
    match target with
    | AttributeTarget n -> "attr:" + n
    | PropertyTarget _ -> "prop"
    | TextTarget -> "text"
    | ContentTarget -> "content"

let private modeName mode =
    match mode with
    | RenderMode -> "render"
    | IsolatedMode -> "isolated"
    | DirectMode -> "direct"

let private finer mode =
    match mode with
    | RenderMode -> IsolatedMode
    | m -> m

let rec private walk (cells: Cells) (sink: Sink) (shape: StringBuilder) (path: string) (node: TemplateNode) : TemplateNode =
    match node with
    | ElementNode (tag, attrs, props, slots, events, hooks, children) ->
        shape.Append('<').Append(tagName tag) |> ignore

        let dynamicNames =
            HashSet<string>(
                slots
                |> Seq.choose (fun s ->
                    match s.Target with
                    | AttributeTarget n -> Some n
                    | _ -> None)
            )

        let kept = ResizeArray ()
        let attrSlots = ResizeArray ()

        for (name, value) in attrs do
            if dynamicNames.Contains name then
                // A static part merged with a dynamic one on the same attribute stays static (structure).
                shape.Append(" a!").Append(name).Append('=').Append(value)
                |> ignore

                kept.Add ((name, value))
            else
                shape.Append(" a:").Append(name) |> ignore
                let read = sink.Static value

                attrSlots.Add
                    {
                        Target = AttributeTarget name
                        Mode = IsolatedMode
                        Read = Func<obj, obj>(fun _ -> box (Some (read ())))
                        Same = sameOption
                    }

        for (p, v) in props do
            shape.Append(" p:").Append(string p.Html).Append('=').Append(string v)
            |> ignore

        let slots' =
            slots
            |> Array.map (fun s ->
                shape.Append(" s:").Append(targetName s.Target).Append('/').Append(modeName s.Mode)
                |> ignore

                let get = sink.Func (box s.Read)

                { s with
                    Read = Func<obj, obj>(fun inst -> (get () :?> Func<obj, obj>).Invoke(cells.Bridge inst))
                    Mode = finer s.Mode
                })

        let events' =
            events
            |> Array.map (fun (name, f) ->
                shape.Append(" e:").Append(name) |> ignore
                let get = sink.Handler (box f)
                name, Func<obj, obj, unit>(fun inst ev -> (get () :?> Func<obj, obj, unit>).Invoke(cells.Bridge inst, ev)))

        for (kind, _) in hooks do
            shape.Append(" h:").Append(string kind) |> ignore

        shape.Append '>' |> ignore

        let children' =
            children
            |> Array.mapi (fun i c -> walk cells sink shape $"{path}/{i}" c)

        shape.Append "</>" |> ignore
        ElementNode (tag, kept.ToArray (), props, Array.append (attrSlots.ToArray ()) slots', events', hooks, children')

    | TextNode text ->
        shape.Append 'T' |> ignore
        let read = sink.Static text

        TextSlotNode
            {
                Target = TextTarget
                Mode = IsolatedMode
                Read = Func<obj, obj>(fun _ -> box (read ()))
                Same = sameString
            }

    | TextSlotNode s ->
        shape.Append("S/").Append(modeName s.Mode)
        |> ignore

        let get = sink.Func (box s.Read)

        TextSlotNode
            { s with
                Read = Func<obj, obj>(fun inst -> (get () :?> Func<obj, obj>).Invoke(cells.Bridge inst))
                Mode = finer s.Mode
            }

    | FragmentNode children ->
        shape.Append "F[" |> ignore

        let children' =
            children
            |> Array.mapi (fun i c -> walk cells sink shape $"{path}/{i}" c)

        shape.Append ']' |> ignore
        FragmentNode children'

    | RegionNode r ->
        shape.Append "R(" |> ignore
        let items = sink.Func (box r.Items)
        let key = sink.Func (box r.Key)

        let row =
            match r.Row with
            | ItemRow (definition, conversion) ->
                let child, childKey = sink.Child (definition, path + "/row")
                shape.Append("row:").Append(childKey) |> ignore
                let conv = sink.Func (box conversion)
                ItemRow (child, Func<(unit -> obj), obj>(fun current -> (conv () :?> Func<(unit -> obj), obj>).Invoke current))
            | ViewRow ->
                shape.Append "view" |> ignore
                ViewRow
            | ArmRow ->
                shape.Append "arm" |> ignore
                ArmRow
            | BoundaryRow ->
                shape.Append "boundary" |> ignore
                BoundaryRow

        shape.Append ')' |> ignore

        RegionNode
            {
                Items = Func<obj, obj array>(fun inst -> (items () :?> Func<obj, obj array>).Invoke(cells.Bridge inst))
                Key = Func<obj, obj>(fun item -> (key () :?> Func<obj, obj>).Invoke item)
                Row = row
            }

    | ChildNode (definition, read) ->
        let child, childKey = sink.Child (definition, path + "/child")

        shape.Append("C(").Append(childKey).Append ')'
        |> ignore

        let get = sink.Func (box read)
        ChildNode (child, Func<obj, obj>(fun inst -> (get () :?> Func<obj, obj>).Invoke(cells.Bridge inst)))

    | HeadNode _ ->
        shape.Append 'H' |> ignore
        node

type PatchResult =
    | Patched of statics: int * funcs: int * handlers: int * rebound: int * bridged: int
    | Unchanged
    | Structural of reason: string
    | NotMounted

type Engine(registry: Remount.Registry) =
    let cells = Dictionary<string, Cells>()
    let instrumented = Dictionary<string, TemplateComponent>()
    // Fallback remount: the previous mount's current instances per key, in setup order.
    let previous = Dictionary<string, obj array>()
    let mutable outcome = Transplant.Outcome.Create ()
    // FSI baselines of keys still running their compiled definition; re-applied whenever cells are rebuilt.
    let baselines = Dictionary<string, TemplateComponent>()
    let reloaded = HashSet<string>()

    member _.Outcome = outcome

    member _.CellCount =
        cells.Values
        |> Seq.sumBy (fun c -> c.Statics.Count + c.Funcs.Count + c.Handlers.Count)

    member _.Keys = cells.Keys |> Seq.toArray

    member private this.Resolve(definition: TemplateComponent, fallbackKey: string) =
        let key =
            registry.KeyOf definition
            |> ValueOption.defaultValue fallbackKey

        let latest =
            registry.Latest key
            |> ValueOption.defaultValue definition

        key, latest

    /// The instrumented definition for a key (built once per mount generation). Call inside graph.Run.
    member this.Instrument(definition: TemplateComponent, fallbackKey: string) : TemplateComponent =
        let key, definition = this.Resolve (definition, fallbackKey)

        match instrumented.TryGetValue key with
        | true, d -> d
        | _ ->
            let c = Cells key
            c.Setup <- definition.Setup
            c.SetupPrint <- Fingerprint.ofObject definition.Setup

            let sink =
                { new Sink with
                    member _.Static value =
                        let s = Api.createSignal value
                        c.Statics.Add s
                        fun () -> s.Value

                    member _.Func f =
                        let s = Api.createSignal f
                        c.Funcs.Add s
                        c.FuncPrints.Add (Fingerprint.ofObject f)
                        fun () -> s.Value

                    member _.Handler f =
                        let r = ref f
                        c.Handlers.Add r
                        c.HandlerPrints.Add (Fingerprint.ofObject f)
                        fun () -> r.Value

                    member this'.Child(child, path) =
                        let childKey, _ = this.Resolve (child, path)
                        this.Instrument (child, path), childKey
                }

            let shape = StringBuilder ()
            let root = walk c sink shape key definition.Root
            c.Shape <- shape.ToString ()

            let setup' =
                Func<Setup, obj, obj>(fun context props ->
                    let owner = Api.tryGetOwner ()

                    let transplant inst =
                        match previous.TryGetValue key with
                        | true, olds when c.Order.Count < olds.Length -> Transplant.copyInto outcome olds[c.Order.Count] inst
                        | _ -> ()

                    let inst, scope = runScoped c.Setup context props transplant

                    // A unit instance (null) has no state to keep or bridge.
                    if not (isNull inst) then
                        c.Live[inst] <-
                            {
                                Inst = inst
                                Current = inst
                                Original = context
                                Props = props
                                Owner = owner
                                Scope = scope
                            }

                        c.Order.Add inst

                        context.OnCleanup (fun () ->
                            c.Live.Remove inst |> ignore
                            c.Order.Remove inst |> ignore)

                    inst)

            let d = Reflect.rebuild definition setup' root
            instrumented[key] <- d
            cells[key] <- c

            match baselines.TryGetValue key with
            | true, b when not (reloaded.Contains key) -> this.Rebaseline (key, b) |> ignore
            | _ -> ()

            d

    /// A key whose latest definition came from a reload: its fingerprints are FSI's own from now on.
    member _.MarkReloaded(key: string) =
        reloaded.Add key |> ignore
        baselines.Remove key |> ignore

    /// Diffs a new definition for `key` against its mounted cells and writes what changed. Call inside graph.Run.
    member this.Patch(key: string, definition: TemplateComponent, typesChanged: bool) : PatchResult =
        match cells.TryGetValue key with
        | false, _ -> NotMounted
        | true, c ->
            let statics = ResizeArray<string>()
            let funcs = ResizeArray<obj>()
            let handlers = ResizeArray<obj>()

            let collect =
                { new Sink with
                    member _.Static value =
                        statics.Add value
                        fun () -> value

                    member _.Func f =
                        funcs.Add f
                        fun () -> f

                    member _.Handler f =
                        handlers.Add f
                        fun () -> f

                    member _.Child(child, path) =
                        let childKey, _ = this.Resolve (child, path)
                        child, childKey
                }

            let shape = StringBuilder ()
            walk c collect shape key definition.Root |> ignore
            let shape = shape.ToString ()

            if shape <> c.Shape then
                Structural $"{key}: template structure changed"
            else
                let funcPrints =
                    funcs
                    |> Seq.map Fingerprint.ofObject
                    |> Seq.toArray

                let handlerPrints =
                    handlers
                    |> Seq.map Fingerprint.ofObject
                    |> Seq.toArray

                let setupPrint = Fingerprint.ofObject definition.Setup

                if Environment.GetEnvironmentVariable "RANVIER_HMR_DEBUG_PRINTS" = "1" then
                    for i in 0 .. funcPrints.Length - 1 do
                        let a, b = c.FuncPrints[i], funcPrints[i]

                        if a <> b then
                            let at =
                                Seq.zip a b
                                |> Seq.tryFindIndex (fun (x, y) -> x <> y)
                                |> Option.defaultValue (min a.Length b.Length)

                            let cut (s: string) =
                                s.Substring (max 0 (at - 120), min 260 (s.Length - max 0 (at - 120)))

                            Console.WriteLine ($"[prints] {key} func {i} differs at {at}:\n  old: {cut a}\n  new: {cut b}")

                let changedFuncs =
                    funcPrints
                    |> Seq.indexed
                    |> Seq.filter (fun (i, p) -> p <> c.FuncPrints[i])
                    |> Seq.length

                let changedHandlers =
                    handlerPrints
                    |> Seq.indexed
                    |> Seq.filter (fun (i, p) -> p <> c.HandlerPrints[i])
                    |> Seq.length

                let setupChanged = setupPrint <> c.SetupPrint

                let codeChanged =
                    changedFuncs + changedHandlers > 0
                    || setupChanged
                    || typesChanged

                let changedStatics =
                    [
                        for i in 0 .. statics.Count - 1 do
                            if c.Statics[i].Peek <> statics[i] then
                                yield i
                    ]

                if not codeChanged && changedStatics.IsEmpty then
                    Unchanged
                else
                    let convertedBefore = outcome.Converted
                    let mutable bridged = 0
                    let mutable rebound = 0

                    Api.batch (fun () ->
                        if codeChanged then
                            // New setup logic or a new instance type (whole-file eval): replace each live instance's
                            // setup run. The old run's scope goes first (its effects, memos, cleanups), then the new
                            // setup runs in the instance's owner and receives the old run's signal values.
                            let typeChanged = typesChanged

                            if setupChanged || typeChanged then
                                for i in Seq.toArray c.Live.Values do
                                    if not (obj.ReferenceEquals (i.Scope, null)) then
                                        i.Scope.Dispose ()

                                    let previousInst = i.Current

                                    let run () =
                                        Api.untrack (fun () ->
                                            runScoped definition.Setup i.Original i.Props (fun fresh ->
                                                Transplant.copyInto outcome previousInst fresh))

                                    let fresh, scope =
                                        match i.Owner with
                                        | ValueSome o -> Api.runWithOwner o run
                                        | ValueNone -> run ()

                                    i.Current <- fresh
                                    i.Scope <- scope
                                    bridged <- bridged + 1

                                c.Epoch.Value <- c.Epoch.Value + 1

                            c.Setup <- definition.Setup
                            c.SetupPrint <- setupPrint

                            // All of a component's code swaps together: old and new closures disagree on the
                            // instance type once it has changed.
                            for i in 0 .. funcs.Count - 1 do
                                if typeChanged || funcPrints[i] <> c.FuncPrints[i] then
                                    c.Funcs[i].Value <- funcs[i]
                                    c.FuncPrints[i] <- funcPrints[i]
                                    rebound <- rebound + 1

                            for i in 0 .. handlers.Count - 1 do
                                if
                                    typeChanged
                                    || handlerPrints[i] <> c.HandlerPrints[i]
                                then
                                    c.Handlers[i].Value <- handlers[i]
                                    c.HandlerPrints[i] <- handlerPrints[i]

                        for i in changedStatics do
                            c.Statics[i].Value <- statics[i])

                    if outcome.Converted > convertedBefore then
                        Structural $"{key}: state of a redefined type crossed by value; rows need a remount"
                    else
                        Patched (changedStatics.Length, changedFuncs, changedHandlers, rebound, bridged)

    /// Drops every cell for a fallback remount; the current instances seed the transplant.
    member _.Reset() =
        previous.Clear ()

        for c in cells.Values do
            previous[c.Key] <-
                c.Order
                |> Seq.map (fun i -> c.Live[i].Current)
                |> Seq.toArray

        cells.Clear ()
        instrumented.Clear ()
        outcome <- Transplant.Outcome.Create ()

    /// Replaces a key's code fingerprints with those of the same source compiled by FSI, without swapping
    /// anything. fsc and FSI number closures and emit IL differently, so only FSI-to-FSI comparisons are exact.
    member this.Rebaseline(key: string, definition: TemplateComponent) : bool =
        match cells.TryGetValue key with
        | false, _ -> false
        | true, c ->
            let funcs = ResizeArray<obj>()
            let handlers = ResizeArray<obj>()

            let collect =
                { new Sink with
                    member _.Static value =
                        fun () -> value

                    member _.Func f =
                        funcs.Add f
                        fun () -> f

                    member _.Handler f =
                        handlers.Add f
                        fun () -> f

                    member _.Child(child, path) =
                        let childKey, _ = this.Resolve (child, path)
                        child, childKey
                }

            let shape = StringBuilder ()
            walk c collect shape key definition.Root |> ignore

            if shape.ToString () <> c.Shape then
                false
            else
                if not (reloaded.Contains key) then
                    baselines[key] <- definition

                for i in 0 .. funcs.Count - 1 do
                    c.FuncPrints[i] <- Fingerprint.ofObject funcs[i]

                for i in 0 .. handlers.Count - 1 do
                    c.HandlerPrints[i] <- Fingerprint.ofObject handlers[i]

                c.SetupPrint <- Fingerprint.ofObject definition.Setup
                true
