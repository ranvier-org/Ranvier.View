/// <summary>The inspection registry: per region start marker and template instance root, closures over the state the mount keeps.</summary>
module internal Ranvier.View.Dom.Inspect

#if RANVIER_VIEW_INSPECT || !FABLE_COMPILER
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Template

[<NoEquality; NoComparison>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type Probe =
    /// <summary>A list region: the keys of its laid-out rows, in order, and each row's first node.</summary>
    | ListProbe of path: InstancePath * rows: (unit -> (obj * Node) array) * stale: (unit -> bool)
    /// <summary>An arm region: the selected key and definition, or null when empty or the arm is authored.</summary>
    | ArmProbe of path: InstancePath * selected: (unit -> struct (obj * TemplateComponent)) * stale: (unit -> bool)
    | BoundaryProbe of path: InstancePath * state: (unit -> BoundaryProbeState)
    | HoleProbe of path: InstancePath
    /// <summary>A template instance: its definition, instance value, owner and path; keyed by its first root node.</summary>
    | InstanceProbe of TemplateComponent * instance: obj * owner: Owner * path: InstancePath
    /// <summary>A mount: its phase, commit mode, whether a pass or its queue is flushing, its identity prefix and its graph; keyed by its range's start marker.</summary>
    | MountProbe of phase: (unit -> ScopePhase) * queued: bool * flushing: (unit -> bool) * prefix: string * graph: Graph

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: NativeDisplay.Context, depth: int, precedence: int) =
        NativeDisplay.union context depth precedence (box (this)) (fun n0 ->
            match this with
            | Probe.ListProbe (v0_0, v0_1, v0_2) ->
                "ListProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.InstancePath>" (box (v0_0)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_1)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_2)))
                ]
            | Probe.ArmProbe (v0_0, v0_1, v0_2) ->
                "ArmProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.InstancePath>" (box (v0_0)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_1)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_2)))
                ]
            | Probe.BoundaryProbe (v0_0, v0_1) ->
                "BoundaryProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.InstancePath>" (box (v0_0)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_1)))
                ]
            | Probe.HoleProbe (v0_0) ->
                "HoleProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.InstancePath>" (box (v0_0)))
                ]
            | Probe.InstanceProbe (v0_0, v0_1, v0_2, v0_3) ->
                "InstanceProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.Template.TemplateComponent>" (box (v0_0)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<System.Object>" (box (v0_1)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.Owner>" (box (v0_2)))
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.View.InstancePath>" (box (v0_3)))
                ]
            | Probe.MountProbe (v0_0, v0_1, v0_2, v0_3, v0_4) ->
                "MountProbe",
                [
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_0)))
                    Some (fun p -> NativeDisplay.boolean context n0 (v0_1))
                    Some (fun p -> NativeDisplay.opaque context n0 "<function>" (box (v0_2)))
                    Some (fun p -> NativeDisplay.text context n0 (v0_3))
                    Some (fun p -> NativeDisplay.opaque context n0 "<Ranvier.Graph>" (box (v0_4)))
                ])

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

and [<System.Diagnostics.DebuggerDisplay("{ToString(),nq}"); NoEquality; NoComparison>] BoundaryProbeState =
    | Ready
    | Pending of waiting: int * revealed: bool
    | Failed of error: exn * generation: int

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: NativeDisplay.Context, depth: int, precedence: int) =
        NativeDisplay.union context depth precedence (box (this)) (fun n0 ->
            match this with
            | BoundaryProbeState.Ready -> "Ready", []
            | BoundaryProbeState.Pending (v0_0, v0_1) ->
                "Pending",
                [
                    Some (fun p -> NativeDisplay.int32 context n0 (v0_0))
                    Some (fun p -> NativeDisplay.boolean context n0 (v0_1))
                ]
            | BoundaryProbeState.Failed (v0_0, v0_1) ->
                "Failed",
                [
                    Some (fun p -> NativeDisplay.exceptionValue context n0 (v0_0))
                    Some (fun p -> NativeDisplay.int32 context n0 (v0_1))
                ])

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

let private table = Native.newWeakMap ()
let private instances = Native.newWeakMap ()
let private rowKeys = Native.newWeakMap ()
let private instanceRanges = Native.newWeakMap ()

/// <summary>Registers the inclusive root span of an instance until cleanup.</summary>
let registerInstanceRange (first: Node) (last: Node) =
    let entry = [| first; last |]
    let entries = Native.get<Node, Node array array> instanceRanges first
    Native.set instanceRanges first (Array.append (if isNull entries then [||] else entries) [| entry |])

    fun () ->
        let entries = Native.get<Node, Node array array> instanceRanges first

        if not (isNull entries) then
            let remaining =
                entries
                |> Array.filter (fun current -> not (obj.ReferenceEquals (current, entry)))

            if remaining.Length = 0 then
                Native.delete instanceRanges first
            else
                Native.set instanceRanges first remaining

/// <summary>The nearest instance's first root and current end-exclusive sibling around <c>target</c>.</summary>
let rec enclosingInstanceOf (target: Node) : (Node * Node) option =
    if isNull (box target) then
        None
    else
        let parent = Native.parentOrNull target

        let mutable cursor =
            if isNull (box parent) then
                target
            else
                Native.firstChildOf parent

        let mutable found = None
        let mutable searching = true

        while searching && not (isNull (box cursor)) do
            let entries = Native.get<Node, Node array array> instanceRanges cursor

            if not (isNull entries) then
                for entry in entries do
                    let mutable root = entry[0]
                    let mutable contains = false
                    let mutable within = true

                    while within && not (isNull (box root)) do
                        if obj.ReferenceEquals (root, target) then
                            contains <- true

                        if obj.ReferenceEquals (root, entry[1]) then
                            within <- false
                        else
                            root <- Native.nextOrNull root

                    if contains then
                        found <- Some (entry[0], Native.nextOrNull entry[1])

            if obj.ReferenceEquals (cursor, target) then
                searching <- false
            else
                cursor <- Native.nextOrNull cursor

        match found with
        | Some _ -> found
        | None -> enclosingInstanceOf parent

/// <summary>Retains a row's key at its first node through its exit hold.</summary>
let rememberRowKey (node: Node) (key: obj) =
    Native.set rowKeys node [| key |]

/// <summary>The row's key while its first node has a parent; a detached node clears its retained entry.</summary>
let rowKeyOf (node: Node) : obj option =
    if isNull node.parentNode then
        Native.delete rowKeys node
        None
    else
        match Native.get<Node, obj array> rowKeys node with
        | null -> None
        | values -> Some values[0]

/// <summary>The instances rooted at <c>node</c>, in outer-to-inner registration order.</summary>
let instancesOf (node: Node) : Probe array =
    match Native.get<Node, Probe array> instances node with
    | null -> [||]
    | probes -> probes

/// <summary>Whether the node's sources changed after its last run; false for null.</summary>
let isStale (node: INode) =
    not (isNull (box node))
    && node.Status &&& Status.Stale <> Status.None

/// <summary>Registers <c>probe</c> for <c>node</c> until the returned cleanup runs.</summary>
let register (node: Node) (probe: Probe) : unit -> unit =
    match probe with
    | InstanceProbe _ ->
        Native.set instances node (Array.append (instancesOf node) [| probe |])

        fun () ->
            let remaining =
                instancesOf node
                |> Array.filter (fun current -> not (obj.ReferenceEquals (current, probe)))

            if remaining.Length = 0 then
                Native.delete instances node
            else
                Native.set instances node remaining
    | _ ->
        Native.set table node probe

        fun () ->
            match Native.get<Node, Probe> table node with
            | current when obj.ReferenceEquals (current, probe) -> Native.delete table node
            | _ -> ()

let private bound = Native.newWeakSet ()

/// <summary>Marks a text node as a slot target.</summary>
let markBound (node: Node) =
    Native.add bound node

/// <summary>Whether the text node is a slot target.</summary>
let isBound (node: Node) =
    Native.has bound node

let probeOf (node: Node) : Probe option =
    match box (Native.get<Node, Probe> table node) with
    | null -> instancesOf node |> Array.tryHead
    | probe -> Some (unbox<Probe> probe)
#endif
