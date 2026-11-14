module ComponentsExample

open Ranvier
open Ranvier.View
open Ranvier.View.Dsl
open Ranvier.Dom

type private CounterProps =
    {
        Id: string
        Label: Signal<string>
        InitialCount: int
        Changed: int -> unit
        Child: unit -> string
    }

type private Counter =
    {
        Props: CounterProps
        Count: Signal<int>
    }

    static member Create(_: Setup, props: CounterProps) =
        {
            Props = props
            Count = createSignal props.InitialCount
        }

    static member View: TemplateView<Counter> =
        article (article.id _.Props.Id, article.className "panel", article.title _.Props.Label.Value) {
            h2 () { fun c -> c.Props.Label.Value }
            output (output.id (fun c -> c.Props.Id + "-count"), output.className "count") { fun c -> string c.Count.Value }

            button (
                button.id (fun c -> c.Props.Id + "-increment"),
                button.className "primary",
                button.``type`` "button",
                button.onClick (fun c _ ->
                    c.Count.Value <- c.Count.Value + 1
                    c.Props.Changed c.Count.Value)
            ) {
                "Increment"
            }

            p () { fun c -> c.Props.Child () }
        }

let private counter =
    Component.template (fun setup props -> Counter.Create (setup, props)) Counter.View

type private Example =
    {
        Label: Signal<string>
        Changed: Signal<string>
        Renamed: Memo<bool>
        Selected: Signal<bool>
    }

    static member Create(_: Setup, ()) =
        let label = createSignal "Shared label"

        {
            Label = label
            Changed = createSignal "No changes yet"
            Renamed = createMemo (fun _ -> label.Value = "Renamed")
            Selected = createSignal false
        }

    static member View: TemplateView<Example> =
        section (section.id "component-example") {
            h2 () { "Reusable components" }

            input (
                input.id "component-label",
                input.placeholder "Shared label",
                input.props.value _.Label.Value,
                input.onInput (fun state event -> state.Label.Value <- (unbox<HTMLInputElement> event.currentTarget).value)
            )

            button (
                button.id "component-rename",
                button.``type`` "button",
                button.props.disabled _.Renamed.Value,
                button.onClick (fun state _ -> state.Label.Value <- "Renamed")
            ) {
                "Rename both counters"
            }

            p (p.id "component-changed") { fun state -> state.Changed.Value }

            input (
                input.id "component-checkbox",
                input.``type`` "checkbox",
                input.props.``checked`` _.Selected.Value,
                input.onChange (fun state event -> state.Selected.Value <- (unbox<HTMLInputElement> event.currentTarget).``checked``)
            )

            output (output.id "component-checkbox-state") { fun state -> if state.Selected.Value then "Checked" else "Unchecked" }

            button (
                button.id "component-toggle-checkbox",
                button.``type`` "button",
                button.onClick (fun state _ -> state.Selected.Value <- not state.Selected.Value)
            ) {
                "Toggle checkbox"
            }

            div (div.className "demo-grid") {
                render counter (fun state ->
                    {
                        Id = "component-first"
                        Label = state.Label
                        InitialCount = 0
                        Changed = fun value -> state.Changed.Value <- "First changed to " + string value
                        Child = fun () -> "First counter child content"
                    })

                render counter (fun state ->
                    {
                        Id = "component-second"
                        Label = state.Label
                        InitialCount = 10
                        Changed = fun value -> state.Changed.Value <- "Second changed to " + string value
                        Child = fun () -> "Child sees " + state.Label.Value
                    })
            }
        }

/// <summary>Two independently owned counters sharing a reactive label and reporting changes to their parent.</summary>
let ``component`` =
    Component.template (fun setup props -> Example.Create (setup, props)) Example.View
