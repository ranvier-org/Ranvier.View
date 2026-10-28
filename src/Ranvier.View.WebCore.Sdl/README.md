# Ranvier.View.WebCore.Sdl

Shows a [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) page in an SDL3 window, through
[ppy.SDL3-CS](https://github.com/ppy/SDL3-CS). The window paints the page when it asks for a frame. It forwards
mouse, wheel, keyboard, input method and focus events to the page. It also sets the page's cursor and moves its
`text/plain` clipboard data through SDL.

On Windows, a native GPU runtime with the `presentation-d3d11` capability uses shared D3D11 textures with
SDL's `direct3d11` renderer at display scale 1. Other renderers, unsupported devices and other display scales
use CPU painting. An explicit `renderer = "software"` keeps CPU presentation. GPU frames are released after
SDL submits its drawing, including readback, and before renderer teardown.

The `accessibility-uia` capability attaches the page's Windows UI Automation tree to the SDL window. It
detaches before page or window disposal. The bridge uses native COM and also works in a NativeAOT app.

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

## Windows presentation and accessibility acceptance

The visible smoke reads SDL's actual backbuffer. `RANVIER_SMOKE_GPU=1` requires at least one shared D3D11
frame; `RANVIER_SMOKE_UIA=1` requires an attached UI Automation tree. Build the independent OS client with
`python tools/native-window-probe.py`, then set `RANVIER_UIA_PROBE` to the absolute path of
`artifacts/native-window/UiAutomationProbe.exe`. The smoke keeps pumping the owner thread while the client
finds the counter button, invokes it, observes its new accessible name and closes the window. It verifies
that the retained UIA element becomes unavailable after teardown.

Local JIT and NativeAOT checks passed with a private Windows x64 GPU host/WebCore build: the SDL backbuffer
was 480x320 at scale 1, shared frames were presented, and the independent tree/action/teardown checks and
clipboard round-trip passed. The persisted presenter also passed software-renderer and scale-2 CPU fallback
checks, then resumed shared presentation at scale 1. These checks precede the coordinated package release.

The manual CI `webcore-gpu` lane takes an exact `gpu_runtime_version`, public NuGet v3 `gpu_runtime_source`
and `gpu_runtime_package` (default `Ranvier.WebCore.Native.Gpu.win-x64`). It verifies the package's GPU flavor,
RID, version, clean source revision and file hashes before staging it. Managed interop uses that same version.
Both JIT and NativeAOT must pass the strict shared-frame, actual-backbuffer and independent UIA checks, and
a failing enabled GPU lane fails the aggregate CI result. An empty version disables the lane; CPU CI does
not count as GPU verification. A successful published-cohort GPU CI run remains required before closing
the Windows presentation gap.
