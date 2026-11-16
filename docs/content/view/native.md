---
title: Native deployment with WebCore
description: Run Ranvier views in a native window on WebCore, with no browser and no JavaScript.
---

:::info Experimental
The native packages are published on nuget.org and marked experimental: their APIs may change. This page
says what exists today and where its limits are.
:::

A Ranvier view can run natively. The application code is .NET, and WebCore, the layout and rendering engine of
WebKit, provides the DOM: style, layout, text and painting. No browser is involved and no JavaScript runs. You keep
the same view patterns: elements, attributes, events and bindings that update the parts of the document that read
reactive state.

![A native Ranvier application updates a DOM through WebCore, without a JavaScript compilation step.](/Ranvier.View/diagrams/native-webcore.svg)

A counter still has a button, a click handler and a text binding. A click changes a signal; the binding updates
the button's text. What changes is how those operations reach the DOM: each is a call into the native engine
instead of JavaScript.

## Packages

| Package | Version | Use it for |
|---|---|---|
| `Ranvier.View.WebCore.Sdl` | 0.2.0 | A window: it paints the page, forwards mouse, keyboard and input method events, and bridges the cursor and the clipboard. |
| `Ranvier.View.WebCore` | 0.1.2 | A page with no window, for tests, snapshots and offscreen rendering. The window package brings it. |
| `Ranvier.View.WebCore.Hmr` | 0.1.2 | Hot reload under `dotnet run -- dev`. Debug builds only. |

```sh
dotnet add package Ranvier.View.WebCore.Sdl --version 0.2.0
```

The native engine (`Ranvier.WebCore.Native` 0.1.3) arrives as a dependency, for the application's runtime
identifier.

## A window

```fsharp
open System
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

// app is the root component and props its props.
use window = Window.Create("Counter", 480, 320, hidden = true)
let graph = window.Graph
let doc = window.Host.Document
let container = doc.createElement "div"
doc.body.appendChild container |> ignore

let mounted =
    Mounting.mount graph None (fun errs -> raise (AggregateException errs)) container None (fun ctx ->
        Program.run (Component.render app props) ctx.Backend)

try
    window.Show()
    window.Present()
    while window.Step() do
        ()
finally
    graph.Run(fun () -> mounted.Dispose())
    container.remove ()
```

The mount is the same `Mounting.mount` the browser renderer takes. The window is created hidden and shown after the
mount, so the first frame on screen is the app.

`examples/Ranvier.NativeCounter` is a complete app, and `examples/Ranvier.HmrCounter` adds hot reload.

## Where it runs

| Runtime | Engine |
|---|---|
| Windows x64 | GPU |
| Windows ARM64 | CPU |
| Linux x64 | GPU |
| macOS arm64 (macOS 26) | GPU |

The GPU engine composites and presents without reading pixels back, and paints on the CPU where no GPU context can
be created. Linux ARM64 has native packages and is still being qualified in the view layer.

Build or publish with a runtime identifier (`-r win-x64`). Without one the output carries every platform's engine.

## Limits today

- The APIs are experimental.
- A page's own scripts do not run: the engine is driven from .NET.
- The clipboard bridge moves `text/plain` only.
- Drags out of the page have no view API yet.
- There is no macOS accessibility bridge. Windows uses UI Automation and Linux an optional AT-SPI module.
- A display scale of 2 on macOS and a real input method are manual checks.
- NativeAOT publishes and runs the exercised paths, and still reports FSharp.Core and view reflection, trimming
  and generic-recursion warnings.

## More

- The [window package's README](https://github.com/ranvier-org/Ranvier.View/blob/main/src/Ranvier.View.WebCore.Sdl/README.md)
  covers renderers, input rates, custom frames and accessibility.
- The [backend's README](https://github.com/ranvier-org/Ranvier.View/blob/main/src/Ranvier.View.WebCore/README.md)
  covers the windowless host, snapshots and NativeAOT.
- The [engine's documentation](https://github.com/ranvier-org/Ranvier.WebCore/blob/main/docs/README.md) covers its
  packages, flavors and C ABI.

The [browser target](getting-started.md) compiles the same views to JavaScript with Fable, through
[Ranvier.View.Dom](../guide/introduction.md#installation).
