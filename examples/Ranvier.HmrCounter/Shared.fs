/// One component rendered several times: twice from App.fs and once from Edge.fs, each with its own state.
module HmrCounter.Shared

open Ranvier
open Ranvier.View

type BadgeState = { Label: string; Clicks: Signal<int> }

/// A helper used from another file (Counter.fs): editing it must update its callers.
let hint (n: int) =
    $"hint: {n}"

let badge: Component<string> =
    Component.define
        (fun _ (label: string) ->
            {
                Label = label
                Clicks = Api.createSignal 0
            })
        (fun t ->
            t.span
                [|
                    t.Attrs.attribute ("data-badge", fun (s: BadgeState) -> Some s.Label)
                    t.Attrs.attribute ("class", "badge")
                |]
                [|
                    t.button
                        [| t.Events.on ("click", fun s _ -> s.Clicks.Value <- s.Clicks.Value + 1) |]
                        [| t.text (fun s -> $"{s.Label}: {s.Clicks.Value}") |]
                    t.text " clicks"
                |])
