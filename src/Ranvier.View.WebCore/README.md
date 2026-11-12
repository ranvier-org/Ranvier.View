# Ranvier.View.WebCore

Runs [Ranvier.View](../Ranvier.View/README.md) components on .NET against a native WebCore page from
[Ranvier.WebCore](https://github.com/ranvier-org/Ranvier.WebCore), with no browser. `WebCoreHost` owns the page:
its `Document` takes the same `Mounting.mount` as the browser renderer, and the app forwards window input to it and
paints it into its window.

Experimental, on Windows x64, Windows ARM64, Linux x64 and macOS arm64 (macOS 26). The backend references
the canonical `Ranvier.WebCore` managed interop and `Ranvier.WebCore.Native` aggregate, which selects native
assets for the application's RID. On macOS, create the host and run WebCore on the process main thread,
shared with SDL's event pump.

## Background snapshots

`Snapshots.forHost` retains synchronous capture and encoding, with deferred completion.
`BackgroundSnapshots.forHost host` opts into detached worker encoding (native ABI 70,
`detached-image-encoding-v1`). Call it, its operations, and disposal on the host thread.

`Capture(subject, options, terminal)` paints immediately and rejects with
`InvalidOperationException` if this host already has a capture/encode in progress.
`Preview(subject, options, terminal)` can wait in one pending slot; a newer preview cancels
the previous pending preview. A waiting preview paints when admitted, so it observes the
document at that point. Capacity is shared across services for the same host.

With native ABI 71 (`snapshot-pixels-v1`), encoded requests transfer an immutable native pixel handle
to the worker without an intermediate managed RGBA array. Raw requests copy once into the owned public
array and do not dispatch an encoder. ABI 70 retains the copied-pixel fallback. Completion returns to the owner queue. Cancellation,
service disposal, host disposal and document replacement suppress stale completion; a codec
already running keeps its slot until it returns. Encoding supports PNG, JPEG, WEBP and raw RGBA,
subject to the host's advertised formats, dimensions up to 8192 and 64 MiB of input pixels.
Layout and first/changed capture painting still run on the owner thread. ABI 71 can reuse an unchanged
viewport capture with the same crop, scale and color space; rendering invalidations and updates discard
that cache. Element captures always repaint. The native cache retains at most one 64 MiB pixel buffer
per host, in addition to admitted capture/codec memory. Retained public images never change when the
cache is invalidated.

`BackgroundSnapshots.forHostWithByteBudget maximumRawBytes host` adds a raw-pixel ceiling between 1 byte and 64 MiB. Smaller budgets are checked before capture and rechecked after a queued preview waits through a resize. Viewports use CSS dimensions and the requested scale; crops are conservatively rounded. Uncropped elements reserve 64 MiB because their paint bounds can include overflow and shadows. Codec workspace and encoded output are additional memory; retained public results remain caller-owned. The default ceiling uses native clipping and native size validation.

## Bounded owner admission

`host.TryPost(action, maximumPending)` returns `Accepted`, `Full`, or `Closed` without blocking the producer. Retry or retain lossless application work on `Full`; `Closed` leaves ownership with the caller. Accepted callbacks keep FIFO order, but shutdown discards queued callbacks. Existing synchronization-context posting remains compatible and unbounded. Pending, peak, rejected and discarded counters expose queue pressure. Admission bounds pending callback count, not the duration of a callback already running.

Scheduling traces expose `CpuBufferCreate` and `CpuUpload` separately, with byte counts in
`Count`. CPU staging arrays and textures persist across same-size repaints. Native capture
traces separately measure buffer creation, rasterization, image encoding and pixel copying.

## Package installation

```sh
dotnet add package Ranvier.View.WebCore --prerelease
```

Use `Ranvier.View.WebCore.Traced` for the traced build. The package brings the shared `Ranvier.Dom`
contracts, canonical managed interop and five-RID native aggregate; no native download or copied interop
is needed. Publish with the supported target RID so the SDK selects its libraries and resources. For an
SDL window, install the [presenter package](../Ranvier.View.WebCore.Sdl/README.md) instead.

[macOS CI](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40645038498/job/122671771869) passed
770 tests, with 16 ignored and no failures, including the SDL hidden-window tests.

The subsequent [four-platform CI run](https://github.com/ranvier-org/Ranvier.View/actions/runs/40675534807)
passed 773 tests per platform, with 16 existing ignored and no failures, plus the visible sample and both
NativeAOT apps. Windows ARM64 ran on a real ARM runner; Linux used Ubuntu 24.04 with Xvfb.

The ABI 21 integration uses shared `Ranvier.Dom` contracts and canonical packaged interop, including
namespace creation, secondary documents, node/string unions, sequences and variadic operations. Against
the corrected preview.7 CI packages from [run 40729363033](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40729363033),
the full Windows x64 backend suite passed 781 tests, with 12 existing ignored and no failures or errors.
Fresh normal/traced package-only consumers also passed both apps under JIT and NativeAOT; each smoke run
passed 15 checks, including hydration retaining its server node and a native click updating state.
Fresh runs against all seven public preview.7 native packages also pass both apps under normal/traced
JIT and NativeAOT, with exact release-source and payload checks. View libraries are locally packed in
those runs. [Full source CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40774290136) passed
all 17 enabled jobs, with 781 backend tests and 12 existing ignored per supported platform, both AOT apps
and the browser/analyzer/package gates. [Strict public-runtime GPU integration](https://github.com/ranvier-org/Ranvier.View/actions/runs/40784192253)
also passed all 18 jobs. Fully published normal/traced CPU/GPU consumers are checked by the
[package acceptance workflow](../../.github/workflows/published-native-packages.yml).

[Final public-package acceptance](https://github.com/ranvier-org/Ranvier.View/actions/runs/40793629803)
passed all four normal/traced CPU/GPU jobs: 16 app runs, 120 smoke assertions, eight independent UIA
checks and 16 native/resource payload verifications, with fresh official-feed restores and no producer
project references. Package-only normal/traced Fable/browser counters also pass with the explicit
Fable DOM companion. The shared View metadata retains standard Fable.Core attributes; native runtime
graphs contain no Xantham or legacy Fable.Ranvier binding packages.

```fsharp
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore

use host = WebCoreHost.Create (320, 200)
use graph = new Graph ()
let container = host.Document.createElement "div"
host.Document.body.appendChild container |> ignore

let mounted =
    Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
        Program.run (Component.render counter 1) context.Backend)

host.Click (40, 30) |> ignore // input in page pixels
host.Cycle () // run WebCore's timers and the posted callbacks
host.Frame () // lay out and paint

let struct (width, height) = host.PaintSize
let pixels = Array.zeroCreate<byte> (width * height * 4)
host.Paint pixels // RGBA, row by row
```

All calls go on the thread that created the host.

### Native file picker

On Windows, an SDL window can use the system file dialog for `<input type="file">`:

```fsharp
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

use window = Window.Create ("Files", 800, 600)
use picker = WindowsFilePickers.install window.Host window.NativeHandle
while window.Step () do ()
```

Install it only when `WindowsFilePickers.available` and `WebCoreHost.Has "file-picker"` are true. The provider
supports single and multiple files and extension filters. Directory requests are cancelled; MIME-only filters show
all files. Disposing the provider or removing its input dismisses an open dialog. Applications on other platforms
can install their own provider with `FilePickers.install`.

`CssQueries.forHost host` provides document-scoped `CSS.supports` queries through the engine parser.
`ComputedStyles.forHost host` returns live, read-only declarations for an element or pseudo-element
through the shared `Ranvier.Dom.ComputedStyleService` contract. Both providers borrow the host and
require elements from its document; returned declarations expire when the host is disposed.
Use the matching `forDocument` providers in `Fable.Ranvier.Dom` for browser code.

`Element.getClientRects()` and `Range.getClientRects()` return retained fragment snapshots through
`DOMRectList`. Its `length` and `item(uint32)` preserve engine ordering and double-precision rectangle
coordinates; an out-of-range item is null. Repeated reads of an item preserve wrapper identity.
Snapshots remain readable after layout changes or element removal and expire with their owning host.
Collapsed ranges inside rendered text may have a zero-width caret rectangle.

The shared [conformance fixtures](../../tests/NativeApi.Conformance/README.md) cover element, cascade,
pseudo-element, live-update and read-only behaviour in Chromium, Windows JIT and Windows NativeAOT.
Other-platform conformance and official package acceptance are tracked separately.

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
