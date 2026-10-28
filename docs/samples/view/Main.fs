/// The mounting sample of docs/content/view/getting-started.md.
module Main

open Fable.Core
open Ranvier.Dom
open global.Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Counter

[<Global "document">]
let private document: Document = jsNative

let start () : unit -> unit =
    let graph = new Graph ()
    let host = document.getElementById "app" |> Option.ofObj |> Option.get

    let report (errors: exn list) =
        for error in errors do
            JS.console.error ("Ranvier mount failure", error)

    let mounted =
        Mounting.mount graph None report host None (fun context ->
            Program.run (Component.render counter { Start = 0; Label = "Clicks" }) context.Backend)

    fun () ->
        mounted.Dispose ()
        graph.Dispose ()
