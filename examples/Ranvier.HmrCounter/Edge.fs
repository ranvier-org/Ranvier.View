/// HMR edge cases at the boundary of the reactive graph: memos and effects created in setup, a module-level
/// store, dynamic dependencies, and per-row component state.
module HmrCounter.Edge

open System
open Ranvier
open Ranvier.View

/// Counts runs in AppDomain data, which every generation of this module (compiled, FSI, reloaded) shares.
let probe (name: string) =
    let n =
        match AppDomain.CurrentDomain.GetData name with
        | :? int as n -> n
        | _ -> 0

    AppDomain.CurrentDomain.SetData (name, n + 1)

/// A module-level store, created on first use (a signal needs the ambient graph).
let shared = lazy (Api.createSignal 0)

type EdgeState =
    {
        Count: Signal<int>
        Step: Signal<int>
        Doubled: Memo<int>
        Echo: Signal<string>
    }

type RowState =
    {
        Label: unit -> string
        Done: Signal<bool>
    }

let edgeRow: Component<unit -> string> =
    Component.define
        (fun _ (read: unit -> string) ->
            {
                Label = read
                Done = Api.createSignal false
            })
        (fun t ->
            t.li
                [|
                    t.Attrs.attribute ("data-row", fun (r: RowState) -> Some (r.Label ()))
                    t.Events.on ("click", fun r _ -> r.Done.Value <- not r.Done.Value)
                |]
                [|
                    t.text (fun r ->
                        (if r.Done.Value then "[x] " else "[ ] ")
                        + r.Label ())
                |])

let private rows = [| "a"; "b"; "c" |]

let edge: Component<unit> =
    Component.define
        (fun _ () ->
            let tag = "v1"
            let count = Api.createSignal 0
            let echo = Api.createSignal ""

            Api.createEffect (fun () ->
                let v = shared.Value.Value
                probe ("edge.effect." + tag)
                echo.Value <- $"{tag} saw shared={v}")

            {
                Count = count
                Step = Api.createSignal 1
                Doubled = Api.createMemo (fun _ -> count.Value * 2)
                Echo = echo
            })
        (fun t ->
            t.div
                [| t.Attrs.attribute ("id", "edge") |]
                [|
                    t.button
                        [|
                            t.Attrs.attribute ("id", "edge-inc")
                            t.Events.on ("click", fun s _ -> s.Count.Value <- s.Count.Value + 1)
                        |]
                        [| t.text "inc" |]
                    t.button
                        [|
                            t.Attrs.attribute ("id", "edge-step")
                            t.Events.on ("click", fun s _ -> s.Step.Value <- s.Step.Value + 1)
                        |]
                        [| t.text "step" |]
                    t.button
                        [|
                            t.Attrs.attribute ("id", "edge-shared")
                            t.Events.on ("click", fun _ _ -> shared.Value.Value <- shared.Value.Value + 1)
                        |]
                        [| t.text "shared" |]
                    t.span [| t.Attrs.attribute ("id", "edge-memo") |] [| t.text (fun s -> $"memo={s.Doubled.Value}") |]
                    t.span [| t.Attrs.attribute ("id", "edge-read") |] [| t.text (fun s -> $"count={s.Count.Value}") |]
                    t.span [| t.Attrs.attribute ("id", "edge-shared-value") |] [| t.text (fun _ -> $"shared={shared.Value.Value}") |]
                    t.span [| t.Attrs.attribute ("id", "edge-echo") |] [| t.text (fun s -> s.Echo.Value) |]
                    t.render Shared.badge (fun _ -> "gamma")
                    t.ul [| t.Attrs.attribute ("id", "edge-rows") |] [| t.each (fun (x: string) -> x) (fun _ -> rows) edgeRow |]
                |])
