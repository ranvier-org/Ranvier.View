# Ranvier.View.WebCore.Sdl

Shows a [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) page in an SDL3 window, through
[ppy.SDL3-CS](https://github.com/ppy/SDL3-CS). The window paints the page when it asks for a frame. It forwards
mouse, wheel, keyboard, input method and focus events to the page. It also sets the page's cursor and moves its
`text/plain` clipboard data through SDL.

## Install

```sh
dotnet add package Ranvier.View.WebCore.Sdl --version 0.1.0-rc.16
```

Use `Ranvier.View.WebCore.Sdl.Traced` for a traced build. Backend, shared contracts, canonical interop,
SDL3 and the five-RID native aggregate are transitive dependencies. The aggregate carries the GPU runtime for
win-x64, linux-x64, linux-arm64 and osx-arm64, and the CPU runtime for win-arm64. Windows uses shared GPU presentation
by default.

For the CPU-only Slim runtime on Windows x64 (the smallest payload, or pixels independent of the GPU), suppress the
aggregate's win-x64 GPU assets and add the matching CPU runtime in the application project:

```xml
<ItemGroup>
  <PackageReference Include="Ranvier.WebCore.Native.win-x64" Version="[0.1.1]" ExcludeAssets="all" />
  <PackageReference Include="Ranvier.WebCore.Native.Cpu.win-x64" Version="[0.1.1]" />
</ItemGroup>
```

The other four native RID dependencies remain available; the explicit exclusion prevents conflicting Windows native
payloads.

On Windows, a native GPU runtime with the `presentation-d3d11` capability copies shared D3D11 textures to a
flip-model swap chain owned by the window, and `SDL_GetRenderer` returns null for that window. A separate thread
copies and presents each frame, so a present that blocks in the driver or the compositor leaves input and animation
running; the next frame shows the page's latest state. This applies with no `renderer` argument and with
`renderer = "direct3d11"`. SDL's `direct3d11` renderer is the fallback when the swap chain cannot be created.
Unsupported devices and display scales other than 1 use CPU painting. An explicit `renderer = "software"` keeps CPU presentation through SDL. GPU frames are released after their present
finishes, and `ReadPixels` and disposal wait for a running present.

The `accessibility-uia` capability attaches the page's Windows UI Automation tree to the SDL window. It
detaches before page or window disposal. The bridge uses native COM and also works in a NativeAOT app.

## Linux input methods

SDL forwards composition and committed text to the focused page. For an X11 application that needs IBus
surrounding text, an optional patched SDL build selects SDL's direct IBus context when
`SDL_RANVIER_IBUS=1` is set before the first SDL window is created. Query
`window.ImeSurroundingAvailable` to determine whether surrounding text is currently available. It is false
when the context is unavailable or cleared, including while a password field is focused. If direct IBus
cannot initialize, X11 falls back to XIM; that fallback does not provide this extension.

The optional `Ranvier.View.WebCore.Sdl.Ibus` package and its SDL build are currently private qualification
artifacts, not published packages. [The SDL build and packaging instructions](../../tools/sdl/README.md)
describe the pinned source, prerequisites, explicit `linux-x64` publish target, and current limitations.
Wayland text-input-v3 and XIM do not use this direct-IBus extension; single-file publish and other
architectures have not been qualified for the optional package.

## Platform support

Experimental, on Windows x64, Windows ARM64, Linux x64 and macOS arm64 (macOS 26), like the backend.
WebCore's run loop and SDL's event pump share the process main thread on macOS, with the same
`Window.Step` implementation.

[macOS CI](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40645038498/job/122671771869) passed
770 tests, with 16 ignored and no failures. Those tests cover a hidden window, paint readback and SDL input;
[the visible sample smoke](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40647793710/job/122680570079)
also passed (480x320 backing pixels, scale 1, painted content and clipboard). `RANVIER_SMOKE=1` closes the
sample automatically. Retina scale 2 and a real IME still need
manual verification; the clipboard bridge supports `text/plain` only.

The subsequent [four-platform CI run](https://github.com/ranvier-org/Ranvier.View/actions/runs/40675534807)
passed 773 tests per platform, with 16 existing ignored and no failures, plus the visible sample and both
NativeAOT apps. Windows ARM64 ran on a real ARM runner; Linux used Ubuntu 24.04 with Xvfb.

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

## Custom window shells

`WindowFrame.Custom` removes the platform's title bar and borders, and the page draws its own. Mount on `window.Graph`
to read the window's state:

```fsharp
use window =
    Window.Create ("Shell", 900, 600, hidden = true, frame = WindowFrame.Custom 6, minimumSize = struct (480, 320))

use mounted = mount window // Mounting.mount window.Graph ...
window.Present ()
window.Show ()
window.Run ()
```

```fsharp
t.div [| t.Attrs.windowRegion WindowRegion.Drag |] [|
    t.span [||] [| t.text (fun _ -> "Title") |]
    t.div [| t.Attrs.windowRegion WindowRegion.Content |] [|
        t.button [| t.Events.on ("click", fun s _ -> s.Window.Minimize ()) |] [| t.text (fun _ -> "—") |]
        t.button [|
            t.Attrs.windowRegion WindowRegion.Maximize
            t.Events.on ("click", fun s _ -> s.Window.ToggleMaximize ())
        |] [| t.text (fun s -> if s.Window.Placement.Value = WindowPlacement.Maximized then "❐" else "□") |]
        t.button [| t.Events.on ("click", fun s _ -> s.Window.Close ()) |] [| t.text (fun _ -> "✕") |]
    |]
|]
```

| `WindowRegion` | Behaviour |
| --- | --- |
| `Drag` | Moves the window, snaps it, and maximizes it on a double click. A right click opens the window menu. |
| `Maximize` | An ordinary page button, which Windows 11 also treats as the maximize button for Snap Layouts. `Content` on other platforms. |
| `Content` | The page receives the input. It marks an interactive element inside a `Drag` region. |

The nearest marked ancestor decides an element's region, and the window reads the page on each hit test, so a reflowed
title bar or an overlay above it takes effect at once. The outer `Custom` border, in window units, resizes a `Normal`
window ahead of the page's regions.

| Member | Contract |
| --- | --- |
| `Graph` | The graph holding `Placement` and `Focused`. A computation on another graph reads their first value and never re-runs. |
| `Placement`, `Focused` | Read-only memos on `Graph`. |
| `Minimize`, `ToggleMaximize`, `Close`, `Show`, `Run` | Commands and the window's loop. |
| `ShowSystemMenu (x, y)` | The platform's window menu at a CSS position. |
| `Title`, `SetIcon (width, height, rgba)` | Set after creation. |

On Windows 11 a custom frame has rounded corners and the compositor's shadow; earlier versions have square corners.
[`examples/Ranvier.CustomShell`](../../examples/Ranvier.CustomShell) is a complete shell. macOS and Linux have not been
run with a custom frame; macOS has no traffic lights in one. [The design record](../../docs/research/custom-window-shell-api.md)
holds the evidence and the open items.

## External GPU surfaces

`window.BindGpuSurface element` binds an external 3D or compute renderer to a composited DOM element with zero third-party
GPU package dependencies in `Ranvier.View.WebCore.Sdl`:

- Promotes `element` to a composited stacking context so HTML, CSS and SVG children composite naturally over the surface.
- Disables accidental text/element drag-highlighting across `element` by default (`preventSelection = true`) while keeping pointer, wheel and focus events intact.
- Uses WebCore's native GPU texture transport when available (`D3D11SharedTexture` with keyed-mutex retention and `AcquireSharedStagingHandle` / `CommitSharedStaging` NT-handle interop on Windows, `MetalIOSurface` on macOS, `EglImage` on Linux) and falls back automatically to an internal DOM `<canvas>` (`CpuFallback`) on software or Slim CPU runtimes.
- Disposing the returned `GpuSurface` (or closing the `Window`) unbinds native resources on the owner thread.

## Moving and resizing

Every window, with either frame, keeps rendering during the platform's modal move or resize loop: the page's frames run
and the page reflows to the new size mid-gesture. Other windows wait for the gesture to end. A failure during such a
turn surfaces from the next `Step`.

## Input rates

A gaming mouse reports about 1,000 motions a second, and a precision touchpad sends many small wheel events. By
default the window dispatches high-rate input to the page at most once per display refresh, as a browser does. Set
a rate per kind with `Window.Create`'s `inputRates` argument or the `InputRates` property:

```fsharp
use window = Window.Create ("Paint", 480, 320, inputRates = { InputRates.Default with PenMotion = InputRate.Raw })
window.InputRates <- InputRates.Raw
```

| `InputRates` field | Held input |
| --- | --- |
| `PointerMotion` | The latest mouse motion, per mouse. |
| `Wheel` | One wheel event at the latest position, with the deltas summed. |
| `PenMotion` | The latest sample per pen, including axis changes such as pressure and tilt, with movement summed. |
| `TouchMotion` | The latest touch frame, with every contact that moved in the frames it replaced. |
| `Resize` | The latest window pixel size and display scale. |

| `InputRate` | Dispatch |
| --- | --- |
| `PerFrame` (default) | At most once per display refresh: before the frame's animation callbacks, or one refresh interval after the previous dispatch when no frame is due. |
| `PerDrain` | Once per run in each `Step`'s drain of SDL's queue. |
| `Raw` | Every event SDL reports. |

A resize under `PerFrame` or `PerDrain` applies once per drain. Held input is dispatched before any button, key,
focus, pen or touch contact event and before input of another kind, so the page receives events in SDL's order: a
press that ends a run of motion follows that run's latest `mousemove`, at the press's position.

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

### Size-oriented publishing

For a smaller NativeAOT executable, opt in to the .NET SDK's size optimization at publish time:

```sh
dotnet publish examples/Ranvier.NativeCounter -c Release -r win-x64 -p:OptimizationPreference=Size -o artifacts/native-aot/counter-size
```

Apply the same property to your own application's project and target RID. The default publish remains unchanged;
size optimization can trade execution speed for a smaller executable, so measure your own workload.

In a matched Windows x64 Release comparison using public Ranvier.Dom alpha.55, View source `5435298` and the
private ABI 62 GPU native runtime `590e299`, the Counter executable changed from 6,450,176 to 5,701,120 bytes
(−11.61%). The backend smoke executable changed from 6,181,376 to 5,463,040 bytes (−11.62%). Both profiles
passed the same Counter and 30-check smoke runs with NativeAOT warnings treated as errors. The 89,778,897-byte
WebCore native payload was identical in both profiles; the Counter runtime files excluding symbols and XML
documentation therefore fell from 99,350,737 to 98,601,681 bytes (−0.75%). These are measurements of one
private Windows GPU cohort, not a size guarantee for other runtimes, applications or platforms.

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

Corrected preview.7 native CI packages from [run 40729363033](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40729363033)
also passed normal/traced package-only CPU consumers under JIT and NativeAOT. Each counter run verified
the 480x320 actual backbuffer, clipboard and independent UIA discovery, bounds, Invoke, reactive name
updates and provider disconnection. Native and resource payload hashes matched the packages, including
the corrected nested icon paths. The same eight normal/traced Smoke/Counter JIT/NativeAOT runs also pass
with native packages from official NuGet, matching the clean preview.7 release source. View libraries
are locally packed in these runs. The [package acceptance workflow](../../.github/workflows/published-native-packages.yml)
checks fully published normal/traced CPU and GPU consumers.

[Full source CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40774290136) passed all four
platform jobs with 781 backend tests, 12 existing ignored, 15 NativeAOT smoke checks and both visible
counter runs each. Windows x64 verifies the independent external UIA client; Windows ARM64 verifies
UIA attachment/smoke without that client.

The manual CI `webcore-gpu` lane takes an exact `gpu_runtime_version`, public NuGet v3 `gpu_runtime_source`
and `gpu_runtime_package` (default `Ranvier.WebCore.Native.win-x64`). It verifies the package's GPU flavor,
RID, version, clean source revision and file hashes before staging it. Managed interop uses that same version.
Both JIT and NativeAOT must pass the strict shared-frame, actual-backbuffer and independent UIA checks, and
a failing enabled GPU lane fails the aggregate CI result. An empty version disables the lane; CPU CI does
not count as GPU verification. [Run 40784192253](https://github.com/ranvier-org/Ranvier.View/actions/runs/40784192253)
passed all 18 jobs with public GPU preview.7: JIT and NativeAOT each presented two shared frames at
480x320/scale 1 and passed clipboard and independent UIA tree/action/teardown checks.

[Final fully public acceptance](https://github.com/ranvier-org/Ranvier.View/actions/runs/40793629803)
passed all four normal/traced Slim/GPU jobs. Both apps ran under JIT and NativeAOT in each job: all
eight visible Counter runs passed clipboard/backbuffer and independent UIA checks. Each GPU Counter
recorded two shared frames; Slim Counters recorded zero. NativeAOT stdout/stderr logs are preserved as
artifacts. Every selected native/resource file matches its public package manifest (24 Slim or 26 GPU).

### Optional Linux accessibility

An optional `libRanvierWebCoreAtspi.so` sidecar mirrors the native `rv_ax_*` tree through the desktop AT-SPI bus.
It uses the existing host loop and stays outside the renderer library. Place the matching module beside the app or
on its native library search path. The provider discovers `AT_SPI_BUS_ADDRESS` or `org.a11y.Bus` on the session bus;
`Window.AccessibilityAttached` is false when the module or desktop service is unavailable.

The Window owns the provider lease through the Host's cleanup installations. Direct Host disposal and Window disposal
release it before destroying the native page. SDL movement and display-scale changes update its geometry. X11 exposes
physical screen positions; Wayland reports screen coordinates unsupported while window coordinates remain available.
All operations run on the host owner thread. This integration does not replace Windows UI Automation.
