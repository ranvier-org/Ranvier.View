# Ranvier.View.WebCore

Runs [Ranvier.View](../Ranvier.View/README.md) components on .NET against a native WebCore page from
[Ranvier.WebCore](https://github.com/ranvier-org/Ranvier.WebCore), with no browser. `WebCoreHost` owns the page:
its `Document` takes the same `Mounting.mount` as the browser renderer, and the app forwards window input to it and
paints it into its window.

Experimental, on Windows x64 and macOS arm64 (macOS 26). The native library comes from
`Ranvier.WebCore.Native.win-x64` or `Ranvier.WebCore.Native.osx-arm64`. On macOS, create the host and run
WebCore on the process main thread, shared with SDL's event pump.

[macOS CI](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40645038498/job/122671771869) passed
770 tests, with 16 ignored and no failures, including the SDL hidden-window tests.

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

Both apps published and ran on Windows x64 locally. NativeAOT still reports FSharp.Core reflection/trimming
and generic-recursion warnings; the exercised app paths pass, and broader AOT coverage remains work to do.
The publish and executable checks run in the Windows and macOS CI jobs.
