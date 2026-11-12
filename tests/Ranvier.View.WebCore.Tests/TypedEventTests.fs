module Ranvier.View.WebCore.Tests.TypedEventTests

open Expecto
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support
// Last, so that Events is the sugar's event parts and not WebCore's listener registry.
open Ranvier.View.Sugar

type Slider = { Inputs: Signal<int> }

let private sliderStyle = "display:block;width:200px;height:20px;margin:0"

type TypedSlider =
    {
        Slider: Slider
    }

    static member Create(_: Setup, slider: Slider) =
        { Slider = slider }

    static member View =
        div () {
            input (
                Attrs.Input.``type`` "range",
                Attrs.attribute ("style", sliderStyle),
                Events.onInput (fun s _ -> s.Slider.Inputs.Value <- s.Slider.Inputs.Value + 1)
            )
        }

type NamedSlider =
    {
        Slider: Slider
    }

    static member Create(_: Setup, slider: Slider) =
        { Slider = slider }

    static member View =
        div () {
            input (
                Attrs.Input.``type`` "range",
                Attrs.attribute ("style", sliderStyle),
                Events.on ("input", fun s (_: Event) -> s.Slider.Inputs.Value <- s.Slider.Inputs.Value + 1)
            )
        }

type BrokenImage =
    {
        Errors: Signal<int>
    }

    static member Create(_: Setup, errors: Signal<int>) =
        { Errors = errors }

    static member View =
        div () { img (Attrs.attribute ("src", "missing://image.png"), Events.onError (fun s _ -> s.Errors.Value <- s.Errors.Value + 1)) }

let private typedSlider = Component.ofMembers<TypedSlider, _>()
let private namedSlider = Component.ofMembers<NamedSlider, _>()
let private brokenImage = Component.ofMembers<BrokenImage, _>()

/// Presses the range track three quarters along, away from the thumb's starting position.
let private pressTrack (host: WebCoreHost) (mounted: Mount) =
    let x, y, width, height = rectOf (mounted.Container.querySelector "input")
    let px = int (x + width * 0.75)
    let py = int (y + height / 2.0)
    host.Mouse (0, px, py, -1, 0, 0) |> ignore
    host.Mouse (1, px, py, 0, 1, 0) |> ignore
    host.Mouse (2, px, py, 0, 1, 0) |> ignore
    host.Cycle ()

let private inputsAfterPress (host: WebCoreHost) (definition: Component<Slider>) =
    use graph = new Graph ()
    let slider = graph.Run (fun () -> { Inputs = Api.createSignal 0 })
    use mounted = mount host graph definition slider
    pressTrack host mounted
    slider.Inputs.Value

[<Tests>]
let tests =
    testList
        "Typed events"
        [
            hostCase "a named input part runs for a range input's track press"
            <| fun host -> Expect.isGreaterThan (inputsAfterPress host namedSlider) 0 "the handler ran"

            hostCase "the typed onInput part runs for a range input's track press"
            <| fun host -> Expect.isGreaterThan (inputsAfterPress host typedSlider) 0 "the handler ran"

            hostCase "the typed onError part runs for an image that fails to load"
            <| fun host ->
                use graph = new Graph ()
                let errors = graph.Run (fun () -> Api.createSignal 0)
                use mounted = mount host graph brokenImage errors

                for _ in 1..10 do
                    host.Cycle 20

                Expect.isGreaterThan errors.Value 0 "the handler ran"
        ]
