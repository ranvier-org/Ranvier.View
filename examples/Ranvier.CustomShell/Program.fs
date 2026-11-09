// A custom window shell. The page draws the title bar; Window provides moving, resizing, snapping and window state.
module Ranvier.CustomShell.Program

open System
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

let private css =
    """
html, body { margin: 0; height: 100%; font: 13px "Segoe UI", sans-serif; background: #1e1f24; color: #d7d9e0; overflow: hidden; }
#shell { display: flex; flex-direction: column; height: 100vh; }
.titlebar { display: flex; align-items: stretch; height: 36px; flex: none; background: #2a2c33; user-select: none; }
.titlebar.inactive { background: #24252a; color: #80828c; }
.logo { display: flex; align-items: center; padding: 0 12px; font-weight: 700; color: #7aa2ff; }
.tab { border: 0; background: transparent; color: inherit; font: inherit; padding: 0 14px; }
.tab:hover { background: #363842; }
.spacer { flex: 1; }
.controls { display: flex; }
.control { width: 46px; border: 0; background: transparent; color: inherit; font-size: 13px; }
.control:hover { background: #3c3f4a; }
.control:active { background: #4a4e5c; }
.control.close:hover { background: #d13438; color: #fff; }
.body { flex: 1; padding: 16px; display: flex; flex-direction: column; gap: 12px; overflow: auto; }
.state { font: 12px Consolas, monospace; white-space: pre; background: #15161a; padding: 10px; border-radius: 4px; }
#spin { width: 24px; height: 24px; background: #7aa2ff; }
"""

type private Shell =
    {
        Window: Window
        Clicks: Signal<int>
        Stats: Signal<string>
    }

    // A record's generated display uses sprintf "%A", which native AOT rejects.
    override _.ToString() = "Shell"

let private app =
    Component.define
        (fun _ (shell: Shell) -> shell)
        (fun t ->
            t.div
                [| t.Attrs.attribute ("id", "shell") |]
                [|
                    t.div
                        [|
                            t.Attrs.windowRegion WindowRegion.Drag
                            t.Attrs.attribute (
                                "class",
                                fun (s: Shell) -> Some (if s.Window.Focused.Value then "titlebar" else "titlebar inactive")
                            )
                        |]
                        [|
                            t.span [| t.Attrs.attribute ("class", "logo") |] [| t.text (fun _ -> "R") |]
                            t.button
                                [|
                                    t.Attrs.windowRegion WindowRegion.Content
                                    t.Attrs.attribute ("class", "tab")
                                    t.Events.on ("click", fun s _ -> s.Clicks.Value <- s.Clicks.Value + 1)
                                |]
                                [| t.text (fun s -> "Tab (" + s.Clicks.Value.ToString () + ")") |]
                            t.div [| t.Attrs.attribute ("class", "spacer") |] [||]
                            t.div
                                [|
                                    t.Attrs.windowRegion WindowRegion.Content
                                    t.Attrs.attribute ("class", "controls")
                                |]
                                [|
                                    t.button
                                        [|
                                            t.Attrs.attribute ("class", "control")
                                            t.Events.on ("click", fun s _ -> s.Window.Minimize ())
                                        |]
                                        [| t.text (fun _ -> "—") |]
                                    t.button
                                        [|
                                            t.Attrs.windowRegion WindowRegion.Maximize
                                            t.Attrs.attribute ("class", "control")
                                            t.Events.on ("click", fun s _ -> s.Window.ToggleMaximize ())
                                        |]
                                        [|
                                            t.text (fun s ->
                                                if s.Window.Placement.Value = WindowPlacement.Maximized then
                                                    "❐"
                                                else
                                                    "□")
                                        |]
                                    t.button
                                        [|
                                            t.Attrs.attribute ("class", "control close")
                                            t.Events.on ("click", fun s _ -> s.Window.Close ())
                                        |]
                                        [| t.text (fun _ -> "✕") |]
                                |]
                        |]
                    t.div
                        [| t.Attrs.attribute ("class", "body") |]
                        [|
                            t.div [| t.Attrs.attribute ("id", "spin") |] [||]
                            t.div [| t.Attrs.attribute ("class", "state") |] [| t.text (fun s -> s.Stats.Value) |]
                        |]
                |])

let private mount (window: Window) =
    let host = window.Host
    let graph = window.Graph
    let style = host.Document.createElement "style"
    style.textContent <- css
    host.Document.head.appendChild style |> ignore
    let container = host.Document.createElement "div"
    host.Document.body.appendChild container |> ignore

    let shell =
        graph.Run (fun () ->
            {
                Window = window
                Clicks = Api.createSignal 0
                Stats = Api.createSignal ""
            })

    let mounted =
        Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
            Program.run (Component.render app shell) context.Backend)

    // The spinner and the longest gap between frames show the page running during a move or resize.
    let mutable last = 0.0
    let mutable longestGap = 0.0
    let mutable lastPublish = 0.0
    let mutable running = true

    let rec tick (now: float) =
        if running then
            if last > 0.0 then
                longestGap <- max longestGap (now - last)

            last <- now

            match host.Document.getElementById "spin" with
            | null -> ()
            | spin -> spin.setAttribute ("style", "transform: rotate(" + (now / 8.0 % 360.0).ToString "F0" + "deg)")

            if now - lastPublish > 200.0 then
                lastPublish <- now
                let struct (width, height) = host.PaintSize

                graph.Run (fun () ->
                    shell.Stats.Value <-
                        String.Join (
                            "\n",
                            [|
                                "placement    " + window.Placement.Value.ToString ()
                                "focused      " + window.Focused.Value.ToString ()
                                "page size    " + width.ToString () + " x " + height.ToString ()
                                "longest gap  " + longestGap.ToString "F0" + " ms"
                            |]
                        )

                    Api.flush ())

            host.RequestAnimationFrame tick |> ignore

    host.RequestAnimationFrame tick |> ignore

    { new IDisposable with
        member _.Dispose() =
            running <- false

            graph.Run (fun () -> mounted.Dispose ())
    }

[<EntryPoint>]
let main _ =
    use window =
        Window.Create ("Ranvier custom shell", 900, 600, hidden = true, frame = WindowFrame.Custom 6, minimumSize = struct (480, 320))

    use mounted = mount window
    window.Present ()
    window.Show ()

    if Environment.GetEnvironmentVariable "RANVIER_SMOKE" = "1" then
        for _ in 1..60 do
            window.Step 16 |> ignore

        window.ToggleMaximize ()

        for _ in 1..60 do
            window.Step 16 |> ignore

        if window.Placement.Value <> WindowPlacement.Maximized then
            failwith "The window did not report its maximized placement."

        window.ToggleMaximize ()

        for _ in 1..60 do
            window.Step 16 |> ignore

        if window.Placement.Value <> WindowPlacement.Normal then
            failwith "The window did not report its restored placement."

        Console.WriteLine "RANVIER_SMOKE ok"
    else
        window.Run ()

    0
