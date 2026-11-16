# Ranvier.View.WebCore.Hmr

Hot reload for native [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) apps in development. The dev host
keeps the SDL window and the WebCore page alive for the whole session and patches edited components into them,
keeping their state.

Experimental. It runs under the JIT only (FSI, reflection), so reference it from Debug builds; the app's normal
path is unchanged and stays NativeAOT-compatible.

```sh
dotnet add package Ranvier.View.WebCore.Hmr --version 0.1.3
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
declarations that depend on a change, in any file; they are evaluated again with it.

An edit with compile errors never reloads; the next passing save is compared with the last applied text.

## What a save reloads

Under `fine`, each save prints one line: `[hmr] #n fine <files>: <outcome> | <timings> | <what was evaluated>`. The
outcome is `patch`, `unchanged`, `fallback-remount`, `error` or `restart`.

| Edit | Outcome |
| --- | --- |
| A `let`: a function, a value or a component | Evaluated again, with every `let` that uses it, in its own file and in later files. Components that use any of them are patched. |
| A `let` inside a nested module that holds only `let`s and `open`s | The same. The nested module is evaluated again as a whole. |
| Several files, saved together or one after another | Applied together in compile order. A later file sees a function an earlier file gained, whichever was saved first. |
| A type, or any declaration other than a `let` or an `open`; a `let` in a nested module that also holds types | The whole file is evaluated again and its module-level values start over. Every `let` in a later file that uses anything the file declares is evaluated again with it. |
| The same, when a type in another file uses what the edited file declares | `restart`: the line reads `restart needed: App.fs: Shelf:Type cannot be evaluated again in place, and the edit to Lib.fs redefines what they use`. Nothing is applied, and every later save repeats the line until the app restarts or the edit is undone. |
| The entry file (the one holding `[<EntryPoint>]`) | `restart`: `restart needed: Program.fs is the entry file, which the host does not reload`. |
| A compile error, in an edited file or in a file below it that the edit broke | `error`, with the file, line and message. Nothing is applied; the next save delivers every file that is still unapplied. |

What keeps its object across a reload: a public, immutable module-level value (a store, a cache), unless its own text
changed or it uses a declaration that did. What starts over whenever its file is evaluated again: private or mutable
module-level values, and private nested modules.

Three cases are named in the line instead of being reloaded:

- `names qualified with a reloaded module bind to the build`: the file uses a name such as `Lib.Page` that the reloaded
  generation of `Lib` does not hold, so its qualified names read the compiled `Lib`. `open Lib` avoids it.
- `not checked by declaration`: the checker could not split the file into declarations, so what it uses of the edit is
  unknown and it was not evaluated again.
- With `-nochecker`, or when the checker fails to start, only the saved files are evaluated: nothing that depends on
  them is, in the same file or in others.

`fsi` evaluates each saved file whole and `alc` recompiles the project, so neither tracks dependents: `fsi` leaves
other files on the old code, and `alc` reloads everything.

## Constraints

- Arm (`matchWith`/`branch`) components update when their parent is re-evaluated.
- `Hooks.mounted` does not re-run on a patch.
- Instances are paired by setup order on a remount; reordering instances of one component can cross their state.
- A value re-evaluated because it uses a change runs its initializer again, side effects included.

The measurements, edge cases and design record are in
[docs/research/native-hmr.md](../../docs/research/native-hmr.md).
