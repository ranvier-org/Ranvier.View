module Ranvier.NativeCounter.Program

#nowarn "9"
#nowarn "51"

open System
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

type State =
    { Count: Signal<int>
      Name: Signal<string> }

let private app =
    Component.define
        (fun _ () ->
            { Count = Api.createSignal 0
              Name = Api.createSignal "" })
        (fun t ->
            t.div
                [| t.Attrs.attribute ("style", "font-family:sans-serif;padding:24px;display:flex;flex-direction:column;gap:16px") |]
                [| t.button
                       [| t.Attrs.attribute ("style", "font-size:32px;padding:8px 24px;align-self:flex-start")
                          t.Events.on ("click", fun state _ -> state.Count.Value <- state.Count.Value + 1) |]
                       [| t.text (fun state -> $"Clicked {state.Count.Value} times") |]
                   t.input
                       [| t.Attrs.attribute ("placeholder", "Your name")
                          t.Attrs.attribute ("style", "font-size:20px;padding:6px")
                          t.Events.on ("input", fun state (event: Event) -> state.Name.Value <- (unbox<HTMLInputElement> event.target).value) |]
                   t.p
                       [| t.Attrs.attribute ("style", "font-size:20px;margin:0") |]
                       [| t.text (fun state ->
                              if String.IsNullOrWhiteSpace state.Name.Value then
                                  "Hello, whoever you are."
                              else
                                  $"Hello, {state.Name.Value}.") |] |])

let private mount (host: Ranvier.View.WebCore.WebCoreHost) =
    let graph = new Graph ()
    let container = host.Document.createElement "div"
    host.Document.body.appendChild container |> ignore

    let mounted =
        Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
            Program.run (Component.render app ()) context.Backend)

    { new IDisposable with
        member _.Dispose() =
            graph.Run (fun () -> mounted.Dispose ())
            graph.Dispose () }

let private smoke (window: Window) (beforeClose: unit -> unit) =
    for _ in 1..60 do
        if not (window.Step (timeout = 16)) then
            failwith "The sample window closed before the smoke completed"

    let pointer =
        Microsoft.FSharp.NativeInterop.NativePtr.ofNativeInt<SDL.SDL_Window> window.SdlWindow

    let scale = SDL.SDL3.SDL_GetWindowDisplayScale pointer

    let nativeWindow =
        if SDL.SDL3.SDL_GetCurrentVideoDriver () = "x11" then
            use key = fixed (System.Text.Encoding.UTF8.GetBytes ("SDL.window.x11.window\000"))

            SDL.SDL3.SDL_GetNumberProperty (SDL.SDL3.SDL_GetWindowProperties pointer, key, 0L)
            <> 0L
        else
            window.NativeHandle <> 0n

    let hidden =
        (SDL.SDL3.SDL_GetWindowFlags pointer
         &&& SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN) = SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN

    let struct (width, height) = window.Host.PaintSize
    let mutable pixelWidth = 0
    let mutable pixelHeight = 0

    if not (SDL.SDLBool.op_Implicit (SDL.SDL3.SDL_GetWindowSizeInPixels (pointer, &&pixelWidth, &&pixelHeight))) then
        failwith (SDL.SDL3.SDL_GetError ())

    if
        struct (width, height)
        <> struct (pixelWidth, pixelHeight)
    then
        failwith "The WebCore paint size must match the SDL window backing pixels"

    if
        hidden
        || not nativeWindow
        || scale <= 0.0f
        || width <= 0
        || height <= 0
    then
        failwith "The sample must present a visible native window with positive dimensions and display scale"

    let struct (capturedWidth, capturedHeight, pixels) = window.ReadPixels ()

    if capturedWidth <> width || capturedHeight <> height then
        failwith "SDL backbuffer dimensions must match the native page"

    if
        not (
            seq { 4..4 .. pixels.Length - 4 }
            |> Seq.exists (fun offset ->
                pixels[offset] <> pixels[0]
                || pixels[offset + 1] <> pixels[1]
                || pixels[offset + 2] <> pixels[2])
        )
    then
        failwith "The SDL backbuffer is uniform"

    let gpuFrames = window.GpuFramesPresented
    let accessibility = window.AccessibilityAttached

    if
        Environment.GetEnvironmentVariable "RANVIER_SMOKE_GPU" = "1"
        && gpuFrames = 0L
    then
        failwith "The GPU smoke must present shared D3D11 frames"

    if
        Environment.GetEnvironmentVariable "RANVIER_SMOKE_UIA" = "1"
        && not accessibility
    then
        failwith "The UIA smoke must attach the native accessibility bridge"

    if
        OperatingSystem.IsWindows ()
        && Ranvier.View.WebCore.WebCoreHost.Has "accessibility-uia"
        && not accessibility
    then
        failwith "The supported UI Automation bridge did not attach"

    let previous = SDL.SDL3.SDL_GetClipboardText ()

    let setText (text: string) =
        use bytes = fixed (System.Text.Encoding.UTF8.GetBytes (text + "\000"))

        if not (SDL.SDLBool.op_Implicit (SDL.SDL3.SDL_SetClipboardText bytes)) then
            failwith (SDL.SDL3.SDL_GetError ())

    try
        setText "Ranvier clipboard smoke"

        if
            SDL.SDL3.SDL_GetClipboardText ()
            <> "Ranvier clipboard smoke"
        then
            failwith "SDL text/plain clipboard round-trip failed"
    finally
        setText previous

    let probePath = Environment.GetEnvironmentVariable "RANVIER_UIA_PROBE"

    if
        not (String.IsNullOrWhiteSpace probePath)
        && accessibility
    then
        let info = System.Diagnostics.ProcessStartInfo (probePath)
        info.UseShellExecute <- false
        info.CreateNoWindow <- true
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        info.ArgumentList.Add (window.NativeHandle.ToString ())
        use probe = new System.Diagnostics.Process (StartInfo = info)

        if not (probe.Start ()) then
            failwith "The independent UIA probe did not start"

        try
            let timer = System.Diagnostics.Stopwatch.StartNew ()
            let mutable tornDown = false

            while not probe.HasExited
                  && timer.Elapsed < TimeSpan.FromSeconds 25.0 do
                if not window.Closed then
                    if not (window.Step (timeout = 16)) then
                        beforeClose ()
                        (window :> IDisposable).Dispose ()
                        tornDown <- true
                else
                    System.Threading.Thread.Sleep 10

            if not probe.HasExited then
                failwith "The independent UIA probe timed out"

            let output = probe.StandardOutput.ReadToEnd ()
            let errors = probe.StandardError.ReadToEnd ()

            if probe.ExitCode <> 0 then
                failwith $"Independent UIA probe failed: {output}{errors}"

            if not tornDown then
                failwith "The UIA probe did not verify window teardown"

            printf "%s" output
        finally
            if not probe.HasExited then
                probe.Kill ()
                probe.WaitForExit (5000) |> ignore

    printfn
        $"Native counter smoke passed: SDL backbuffer {width}x{height}, display scale {scale}, GPU frames {gpuFrames}, UIA attached {accessibility}, text/plain clipboard"

    window.Close ()

[<EntryPoint>]
let main _ =
    if Environment.GetEnvironmentVariable "RANVIER_SMOKE" = "1" then
        use window = Window.create ("Ranvier", 480, 320)
        let mounted = mount window.Host
        let mutable mountedAlive = true

        let cleanup () =
            if mountedAlive then
                mountedAlive <- false
                mounted.Dispose ()

        try
            smoke window cleanup
        finally
            cleanup ()
    else
        Window.run ("Ranvier", 480, 320) mount

    0
