module Ranvier.NativeCounter.Program

#nowarn "9"
#nowarn "51"

open System
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
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
            graph.Dispose ()
    }

[<System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)>]
type private InvalidTextureBind = delegate of uint32 * uint32 -> unit

let private eglReleaseRetrySmoke (window: Window) =
    window.Present ()

    let pointer =
        Microsoft.FSharp.NativeInterop.NativePtr.ofNativeInt<SDL.SDL_Window> window.SdlWindow

    let renderer = SDL.SDL3.SDL_GetRenderer pointer

    use presenter =
        new Presenter (renderer, window.Host, EglRenderer.TryCreate renderer)

    let bind =
        System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<InvalidTextureBind>(SDL.SDL3.SDL_GL_GetProcAddress "glBindTexture")

    let action (_: nativeptr<SDL.SDL_Texture>) (_: int) (_: int) =
        bind.Invoke (0xFFFFFFFFu, 0u)

    let flags =
        System.Reflection.BindingFlags.Instance
        ||| System.Reflection.BindingFlags.NonPublic

    let operation =
        typeof<Presenter>.GetMethod("WithGpu", flags).MakeGenericMethod [| typeof<unit> |]

    let mutable failed = false

    try
        operation.Invoke (presenter, [| box action |])
        |> ignore
    with :? System.Reflection.TargetInvocationException as error when error.InnerException.Message.StartsWith "GPU frame release failed" ->
        failed <- true

    if not failed then
        failwith "The EGL release failure fixture did not exercise a retained frame"

    (presenter :> IDisposable).Dispose()
    let before = window.GpuFramesPresented
    window.Present ()

    if
        window.GpuFramesPresented <= before
        || window.CpuFramesPresented <> 0L
    then
        failwith "The retained EGL frame was lost or not released before retry"

    printfn "EGL release retry smoke passed"

let private smoke (window: Window) (beforeClose: unit -> unit) =
    if Environment.GetEnvironmentVariable "RANVIER_SMOKE_EGL_RELEASE_RETRY" = "1" then
        eglReleaseRetrySmoke window

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
         &&& SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN)
            =
            SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN

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
        && (gpuFrames = 0L || window.CpuFramesPresented <> 0L)
    then
        failwith "The GPU smoke must present shared GPU frames without CPU fallback"

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
                        (window :> IDisposable).Dispose()
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

let private runSmoke () =
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

let private runTwoSmoke () =
    use first = Window.create ("Ranvier first", 480, 320)
    use second = Window.create ("Ranvier second", 480, 320)
    let firstMount = mount first.Host
    let secondMount = mount second.Host

    try
        smoke first ignore
        smoke second ignore
    finally
        secondMount.Dispose ()
        firstMount.Dispose ()

    printfn "Two simultaneous GPU windows smoke passed"

let private eglFailureSmoke beforeRegistration =
    if not (OperatingSystem.IsLinux ()) then
        failwith "EGL cohort failure checks require Linux"

    if not (Ranvier.View.WebCore.WebCoreHost.Has "presentation-egl") then
        failwith "EGL cohort failure checks require native EGL transport"

    EglRenderer.Prepare ()

    let blocker =
        if beforeRegistration then
            Some (Ranvier.View.WebCore.WebCoreHost.Create (16, 16))
        else
            None

    let mutable window =
        Microsoft.FSharp.NativeInterop.NativePtr.nullPtr<SDL.SDL_Window>

    let mutable renderer =
        Microsoft.FSharp.NativeInterop.NativePtr.nullPtr<SDL.SDL_Renderer>

    if not (SDL.SDLBool.op_Implicit (SDL.SDL3.SDL_InitSubSystem SDL.SDL_InitFlags.SDL_INIT_VIDEO)) then
        blocker
        |> Option.iter (fun host -> (host :> IDisposable).Dispose())

        failwith (SDL.SDL3.SDL_GetError ())

    try
        try
            use title = fixed (System.Text.Encoding.UTF8.GetBytes "EGL failure fixture\000")
            window <- SDL.SDL3.SDL_CreateWindow (title, 32, 24, SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN)

            if Microsoft.FSharp.NativeInterop.NativePtr.isNullPtr window then
                failwith (SDL.SDL3.SDL_GetError ())

            use driver = fixed (System.Text.Encoding.UTF8.GetBytes "opengles2\000")
            renderer <- SDL.SDL3.SDL_CreateRenderer (window, driver)

            if Microsoft.FSharp.NativeInterop.NativePtr.isNullPtr renderer then
                failwith (SDL.SDL3.SDL_GetError ())

            let registered = (EglRenderer.TryCreate renderer).IsSome

            if registered = beforeRegistration then
                failwith "EGL registration did not respect the existing-host boundary"

            raise (InvalidOperationException "simulated failure before host construction")
        with :? InvalidOperationException as error when error.Message = "simulated failure before host construction" ->
            ()
    finally
        if not (Microsoft.FSharp.NativeInterop.NativePtr.isNullPtr renderer) then
            SDL.SDL3.SDL_DestroyRenderer renderer

        if not (Microsoft.FSharp.NativeInterop.NativePtr.isNullPtr window) then
            SDL.SDL3.SDL_DestroyWindow window

        SDL.SDL3.SDL_QuitSubSystem SDL.SDL_InitFlags.SDL_INIT_VIDEO

        blocker
        |> Option.iter (fun host -> (host :> IDisposable).Dispose())

    let retained =
        SDL.SDL3.SDL_WasInit SDL.SDL_InitFlags.SDL_INIT_VIDEO
        <> Unchecked.defaultof<SDL.SDL_InitFlags>

    if retained = beforeRegistration then
        failwith "EGL cohort owner was released or retained across the wrong failure boundary"

    printfn "EGL cohort failure smoke passed: before registration %b, owner retained %b" beforeRegistration retained

[<EntryPoint>]
let main _ =
    if Environment.GetEnvironmentVariable "RANVIER_SMOKE" = "1" then
        let failure = Environment.GetEnvironmentVariable "RANVIER_SMOKE_EGL_FAILURE"

        if
            failure = "before-registration"
            || failure = "after-registration"
        then
            eglFailureSmoke (failure = "before-registration")

        if failure <> "before-registration" then
            if Environment.GetEnvironmentVariable "RANVIER_SMOKE_EGL_PAIR" = "1" then
                runTwoSmoke ()

            let count =
                match Int32.TryParse (Environment.GetEnvironmentVariable "RANVIER_SMOKE_REOPEN") with
                | true, value when value > 1 && value <= 100 -> value
                | true, 1 -> 3
                | _ -> 1

            for _ in 1..count do
                runSmoke ()
    else
        Window.run ("Ranvier", 480, 320) mount

    0
