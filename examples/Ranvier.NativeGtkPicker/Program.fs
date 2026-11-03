module Ranvier.NativeGtkPicker.Program

open System
open Ranvier.View.WebCore.Sdl

[<EntryPoint>]
let main _ =
    use window = Window.Create ("Ranvier GTK file picker", 480, 240)
    let host = window.Host
    let document = host.Document
    let description = document.createElement "p"
    document.body.appendChild description |> ignore
    let input = document.createElement "input"
    input.setAttribute ("type", "file")
    input.setAttribute ("accept", ".txt,image/png")
    document.body.appendChild input |> ignore

    match GtkFilePicker.tryInstall host with
    | None ->
        description.textContent <- "The optional GTK file picker is unavailable."
        while window.Step () do ()
    | Some picker ->
        use picker = picker
        description.textContent <- "Choose one text or PNG file."

        while window.Step () do
            picker.Pump ()

    0
