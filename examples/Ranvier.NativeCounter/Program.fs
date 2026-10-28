module Ranvier.NativeCounter.Program

open System
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

type State =
    {
        Count: Signal<int>
        Name: Signal<string>
    }

let private app =
    Component.define
        (fun _ () ->
            {
                Count = Api.createSignal 0
                Name = Api.createSignal ""
            })
        (fun t ->
            t.div
                [|
                    t.Attrs.attribute ("style", "font-family:sans-serif;padding:24px;display:flex;flex-direction:column;gap:16px")
                |]
                [|
                    t.button
                        [|
                            t.Attrs.attribute ("style", "font-size:32px;padding:8px 24px;align-self:flex-start")
                            t.Events.on ("click", fun state _ -> state.Count.Value <- state.Count.Value + 1)
                        |]
                        [| t.text (fun state -> $"Clicked {state.Count.Value} times") |]
                    t.input
                        [|
                            t.Attrs.attribute ("placeholder", "Your name")
                            t.Attrs.attribute ("style", "font-size:20px;padding:6px")
                            t.Events.on ("input", fun state (event: Event) -> state.Name.Value <- (unbox<HTMLInputElement> event.target).value)
                        |]
                    t.p
                        [| t.Attrs.attribute ("style", "font-size:20px;margin:0") |]
                        [|
                            t.text (fun state ->
                                if String.IsNullOrWhiteSpace state.Name.Value then
                                    "Hello, whoever you are."
                                else
                                    $"Hello, {state.Name.Value}.")
                        |]
                |])

[<EntryPoint>]
let main _ =
    Window.run ("Ranvier", 480, 320) (fun host ->
        let graph = new Graph ()
        let container = host.Document.createElement "div"
        host.Document.body.appendChild container |> ignore

        let mounted =
            Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
                Program.run (Component.render app ()) context.Backend)

        { new IDisposable with
            member _.Dispose() =
                graph.Run (fun () -> mounted.Dispose ())
                graph.Dispose ()
        })

    0
