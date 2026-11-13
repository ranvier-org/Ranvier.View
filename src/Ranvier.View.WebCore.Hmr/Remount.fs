/// Coarse hot reload: rebuild the definition tree from the latest definitions, wrap every setup so
/// instances are logged by (key, ordinal), remount, and move Signal values from the previous mount's instances.
module Ranvier.View.WebCore.Hmr.Remount

open System
open System.Collections.Generic
open Ranvier.View

/// The latest definition for each module-level component key, and the key of every definition seen.
type Registry() =
    let latest = Dictionary<string, TemplateComponent>()
    let keys = Dictionary<obj, string>(HashIdentity.Reference)

    member _.Register(key: string, definition: TemplateComponent) =
        latest[key] <- definition
        keys[box definition] <- key

    /// Records a definition's key without making it the latest (an FSI baseline of mounted code).
    member _.Alias(key: string, definition: TemplateComponent) =
        keys[box definition] <- key

    member _.KeyOf(definition: TemplateComponent) =
        match keys.TryGetValue (box definition) with
        | true, k -> ValueSome k
        | _ -> ValueNone

    member _.Latest(key: string) =
        match latest.TryGetValue key with
        | true, d -> ValueSome d
        | _ -> ValueNone

    member _.Count = latest.Count

/// Instances in setup order per key, for this mount and the one before it.
type InstanceLog() =
    let mutable previous = Dictionary<string, ResizeArray<obj>>()
    let mutable current = Dictionary<string, ResizeArray<obj>>()

    member _.Rotate() =
        previous <- current
        current <- Dictionary ()

    /// Records a fresh instance and returns the previous mount's instance at the same ordinal, if any.
    member _.Record(key: string, fresh: obj) : obj voption =
        let list =
            match current.TryGetValue key with
            | true, l -> l
            | _ ->
                let l = ResizeArray ()
                current[key] <- l
                l

        let ordinal = list.Count
        list.Add fresh

        match previous.TryGetValue key with
        | true, olds when ordinal < olds.Count -> ValueSome olds[ordinal]
        | _ -> ValueNone

    member _.Replace(key: string, items: obj seq) =
        current[key] <- ResizeArray items

type Remounter(registry: Registry) =
    let log = InstanceLog ()
    let wrapped = Dictionary<obj, TemplateComponent>(HashIdentity.Reference)
    let mutable outcome = Transplant.Outcome.Create ()

    member _.Outcome = outcome
    member _.Log = log

    /// The root to mount next: every reachable definition replaced by its latest version and wrapped.
    member this.Prepare(rootKey: string) : TemplateComponent =
        log.Rotate ()
        wrapped.Clear ()
        outcome <- Transplant.Outcome.Create ()

        match registry.Latest rootKey with
        | ValueSome root -> this.Wrap (root, rootKey)
        | ValueNone -> failwith $"No component registered for {rootKey}"

    member this.Wrap(definition: TemplateComponent, fallbackKey: string) : TemplateComponent =
        let key =
            registry.KeyOf definition
            |> ValueOption.defaultValue fallbackKey

        let definition =
            registry.Latest key
            |> ValueOption.defaultValue definition

        match wrapped.TryGetValue (box definition) with
        | true, w -> w
        | _ ->
            let setup = definition.Setup

            let setup' =
                Func<Setup, obj, obj>(fun context props ->
                    // One batch: effects created by setup are queued and run after the transplant, so they derive
                    // from the moved state instead of being overwritten by it.
                    Ranvier.Api.batch (fun () ->
                        let fresh = setup.Invoke (context, props)

                        match log.Record (key, fresh) with
                        | ValueSome old -> Transplant.copyInto outcome old fresh
                        | ValueNone -> ()

                        fresh))

            let w = Reflect.rebuild definition setup' (this.Rewrite (definition.Root, key))
            wrapped[box definition] <- w
            w

    member this.Rewrite(node: TemplateNode, path: string) : TemplateNode =
        match node with
        | ElementNode (tag, attrs, props, slots, events, hooks, children) ->
            ElementNode (
                tag,
                attrs,
                props,
                slots,
                events,
                hooks,
                children
                |> Array.mapi (fun i c -> this.Rewrite (c, $"{path}/{i}"))
            )
        | FragmentNode children ->
            FragmentNode (
                children
                |> Array.mapi (fun i c -> this.Rewrite (c, $"{path}/{i}"))
            )
        | ChildNode (definition, read) -> ChildNode (this.Wrap (definition, path + "/child"), read)
        | RegionNode region ->
            match region.Row with
            | ItemRow (definition, conversion) ->
                RegionNode
                    { region with
                        Row = ItemRow (this.Wrap (definition, path + "/row"), conversion)
                    }
            | _ -> node
        | other -> other
