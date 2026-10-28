# Ranvier.View.WebCore.Sdl

Shows a [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) page in an SDL3 window, through
[ppy.SDL3-CS](https://github.com/ppy/SDL3-CS). The window paints the page when it asks for a frame. It forwards
mouse, wheel, keyboard, input method and focus events to the page. It also sets the page's cursor and moves its
`text/plain` clipboard data through SDL.

Experimental, on Windows x64 and macOS arm64 (macOS 26), like the backend. WebCore's run loop and SDL's
event pump share the process main thread on macOS, with the same `Window.Step` implementation.

[macOS CI](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40645038498/job/122671771869) passed
770 tests, with 16 ignored and no failures. Those tests cover a hidden window, paint readback and SDL input;
[the visible sample smoke](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40647793710/job/122680570079)
also passed (480x320 backing pixels, scale 1, painted content and clipboard). `RANVIER_SMOKE=1` closes the
sample automatically. Retina scale 2 and a real IME still need
manual verification; the clipboard bridge supports `text/plain` only.

```fsharp
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

Window.run ("Counter", 480, 320) (fun host ->
    let graph = new Graph ()
    let container = host.Document.createElement "div"
    host.Document.body.appendChild container |> ignore

    Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
        Program.run (Component.render counter 0) context.Backend))
```

`Window.run` returns when the window closes. An app that owns its loop calls `Window.create` and then `Step` each turn
instead. `Step` waits for window events until the next timer is due, dispatches them, cycles the host and presents a
frame when one is wanted:

```fsharp
use window = Window.create ("Counter", 480, 320)
// mount into window.Host.Document
while window.Step () do
    () // the app's own work
```

The window's size is in the platform's window units. The page's CSS pixels follow the display's scale, so a 480 by
320 window shows 480 by 320 CSS pixels on any display. `NativeHandle` is the platform's window (an `HWND` on
Windows), for an accessibility bridge.

All calls go on the thread that created the window. That thread is the host's thread, and the main thread on macOS.

[`examples/Ranvier.NativeCounter`](../../examples/Ranvier.NativeCounter) is a complete app, published with NativeAOT.

## NativeAOT

Publish the smoke app and counter on the target OS:

```sh
dotnet publish tests/Ranvier.View.WebCore.AotSmoke -c Release -r win-x64 -o artifacts/native-aot/smoke
dotnet publish examples/Ranvier.NativeCounter -c Release -r win-x64 -o artifacts/native-aot/counter
```

Use `-r osx-arm64` on macOS 26. Windows needs the .NET 10 SDK, Visual Studio C++ Build Tools (the Desktop
development with C++ workload) and a Windows SDK; macOS needs the .NET 10 SDK and Xcode command-line tools.
Run the published smoke executable directly; it verifies mount, paint, native click and repaint. Run the
published counter with `RANVIER_SMOKE=1` for its bounded visible-window check. Keep all native libraries and
`WebKit.resources` from the publish folder beside the executable. The app is native; the WebCore libraries
and resources remain separate files.

Both apps published and ran on Windows x64 locally and in
[Windows](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137299) and
[macOS arm64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137302) CI.
The counter opened a visible 480x320 window, with scale 1, matching backing dimensions, painted content
and a successful text/plain clipboard round-trip on each platform. Retina scale 2 and a real IME remain
manual checks. NativeAOT still reports FSharp.Core/view reflection, trimming and generic-recursion
warnings; the exercised app paths pass, and broader AOT coverage remains work to do.
