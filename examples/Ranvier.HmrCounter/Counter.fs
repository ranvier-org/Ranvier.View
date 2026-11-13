module HmrCounter.Counter

open Ranvier
open Ranvier.View

type CounterState =
    {
        Count: Signal<int>
        Step: Signal<int>
    }

let counter: Component<string> =
    Component.define
        (fun _ (label: string) ->
            {
                Count = Api.createSignal 0
                Step = Api.createSignal 1
            })
        (fun t ->
            t.div
                [|
                    t.Attrs.attribute ("id", "counter")
                    t.Attrs.attribute ("style", "padding:12px;border:2px solid #4a7;border-radius:8px")
                |]
                [|
                    t.h2 [| t.Attrs.attribute ("id", "counter-title") |] [| t.text "Counter" |]
                    t.button
                        [|
                            t.Attrs.attribute ("id", "counter-button")
                            t.Attrs.attribute ("style", "font-size:20px;padding:6px 16px")
                            t.Events.on ("click", fun s _ -> s.Count.Value <- s.Count.Value + s.Step.Value)
                        |]
                        [| t.text (fun s -> $"Clicked {s.Count.Value} times") |]
                    t.p [| t.Attrs.attribute ("id", "counter-hint") |] [| t.text (fun s -> Shared.hint s.Count.Value) |]
                |])
