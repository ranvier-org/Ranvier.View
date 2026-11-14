# Ranvier.View.WebCore.Hmr

Hot reload for native [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) apps in development. The dev host
keeps the SDL window and the WebCore page alive for the whole session and patches edited components into them,
keeping their state.

Experimental. It runs under the JIT only (FSI, reflection), so reference it from Debug builds; the app's normal
path is unchanged and stays NativeAOT-compatible.

```sh
dotnet add package Ranvier.View.WebCore.Hmr --version 0.1.1
```

## Usage

Replace `Window.run` in the entry point:

```fsharp
[<EntryPoint>]
let main argv =
    Ranvier.View.WebCore.Hmr.Dev.Run (argv, "My app", 640, 480, App.app, ())
```

```sh
dotnet run                       # the app
dotnet run -- dev                # the dev host, fine-grained patching
dotnet run -- dev -hmr fsi       # remount after an FSI evaluation of the edited file
dotnet run -- dev -hmr alc       # remount after an in-process compile into a collectible load context
```

Components stay ordinary `Component.define` code. The window title shows each reload's path and time.

A root whose props need the window (a GPU context, for instance) uses `Dev.RunWith (argv, title, width, height, root, fun window -> props)`:
the props are built once per window and survive every reload. Props that implement `IDisposable` belong to the host,
which disposes them after the tree unmounts and before the window.
[examples/Ranvier.HmrWgpu](../../examples/Ranvier.HmrWgpu) hot-reloads a Ranvier.Wgpu scene this way.

## Strategies

| Strategy | Save to presented | Keeps |
| --- | --- | --- |
| `fine` (default) | ~80–130 ms; comment-only edits ~22 ms | DOM nodes, instances and their signals, public module-level values |
| `fsi` | ~80–100 ms, growing with the remounted tree | signal values; setups re-run on every save |
| `alc` | ~550–700 ms | signal values; any F# construct, every file recompiled |

`fine` mounts each definition through an instrumented copy of its template: static attribute values and text are
signal cells, and every reader, handler and region function goes through a swappable cell. A reload re-evaluates
the changed declarations in FSI (only the `let`s, against the types in use, unless a type changed), writes the
cells whose value or code changed, and remounts with state transplant when the template structure changed.
[Ranvier.FSharp.Checker](https://github.com/ranvier-org/ranvier-fsharp) gates reloads on errors and supplies the
declarations that depend on a change, in any file.

An edit with compile errors never reloads; the next passing save is compared with the last applied text.

## Constraints

- Arm (`matchWith`/`branch`) components update when their parent is re-evaluated.
- `Hooks.mounted` does not re-run on a patch.
- Private or mutable module-level values reset when their file is re-evaluated.
- Instances are paired by setup order on a remount; reordering instances of one component can cross their state.

The measurements, edge cases and design record are in
[docs/research/native-hmr.md](../../docs/research/native-hmr.md).
