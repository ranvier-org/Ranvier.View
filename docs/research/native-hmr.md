# Hot reload for native Ranvier apps

Question: can a native `Ranvier.View.WebCore` app (SDL3 window + WebCore page) hot reload edits into the
running process, how fast, and how fine-grained? Three prototypes, one shared dev host, published packages only
(`Ranvier 0.1.1-preview.3`, `Ranvier.View.WebCore.Sdl 0.1.0-rc.13`, `Ranvier.FSharp.Checker 0.1.0-preview.3`).

```text
dotnet run                          # the app, unchanged
dotnet run -- dev                   # dev host, fine-grained (P3)
dotnet run -- dev -hmr fsi|alc      # P2 / P1
dotnet run -- dev -nochecker        # P3 without the Ranvier.FSharp.Checker gate
dotnet build -p:HmrStamp=true       # dev build through the fingerprinting compiler (experiments/Ranvier.View.Hmr.Fsc)
```

The app opts in with one call, `Ranvier.View.WebCore.Hmr.Dev.Run (argv, title, w, h, App.app, ())` in `Program.fs`.
Components stay plain `Component.define` code. The dev host owns the window and page for the whole session;
reloads swap code underneath them.

## Ranking

| # | Prototype | Steady-state save → presented | Apply cost at 1,000 rows | Keeps | Loses |
|---|---|---|---|---|---|
| **1** | **P3 `fine`**: instrumented templates + declaration-scoped FSI + Checker gate | **~90–130 ms**; comment-only **22 ms**; compile errors gated in **~80 ms** | **1–4 ms** | DOM nodes, focus/scroll/input, instance objects and their signals, module-level values (public, immutable) | private/mutable module values; falls back to a remount on template-structure changes |
| 2 | P2 `fsi`: whole-file FSI `#load` + remount with signal transplant | ~80–100 ms, but **~200–290 ms** at 1,000 rows | 13–30 ms (grows with app) | signal values (by field name) | DOM state, setup side effects re-run on *every* save, module-level state |
| 3 | P1 `alc`: in-process FCS compile of the project → collectible ALC → remount | ~550–700 ms (compile-bound) | 6–21 ms | signal values (user types via JSON) | as P2; plus compile latency |
| ref | cold restart (`dotnet watch` for F# today) | ~2.6 s incremental build + ~0.9 s relaunch | – | nothing | all state |

Ergonomics are identical at the call site (one `Dev.Run`). P3 needs a JIT dev host with FSI, the Checker and
reflection over Ranvier.View internals. P1 is the most faithful to a real build (any F# construct, private types,
nested modules) and the slowest. P2 is the simplest and degrades with app size and side-effectful setups.

## Measurements (Windows x64, software renderer, medians, first reload of a session excluded)

Main scenario (38 steps: click to build state, then each edit class, verify the change and that state survived;
all strategies 38/38 on every run):

| edit | fine | fine-nochecker | fsi | alc |
|---|---|---|---|---|
| static text | 90 (1) | 86 (1) | 83 (2) | 647 (6) |
| static attribute | 101 (1) | 102 (1) | 90 (3) | 702 (6) |
| reader logic | 101 (1) | 97 (2) | 89 (2) | 658 (5) |
| handler logic | 98 (1) | 99 (1) | 82 (2) | 667 (5) |
| structure (new element) | 109 (10) | 104 (11) | 104 (2) | 565 (6) |
| compile error (blocked) | 80 (0) | 220 (0) | 86 (0) | 317 (0) |
| state type change | 129 (1) | 114 (1) | 96 (3) | 541 (6) |
| comment only | 22 (0) | 21 (0) | 87 (2) | 494 (5) |
| list row reader | 127 (1) | 135 (1) | 205 (13) | 544 (6) |

`total (apply)` ms. Total = file write → first frame after the change. Detect (watcher + 10 ms debounce) ≈ 10 ms.
The rest is delivery: FSI eval ≈ 60–110 ms, the Checker ≈ 10–40 ms in parallel with eval, in-process FCS compile
≈ 450–700 ms. First reload of a session: fine 125, fsi 689, alc 800 ms. P3 pre-JITs FSI with a baseline eval at
warm-up; P2 could do the same.

Scale (1,000 seeded rows): fine 112 / 184 ms (apply 1 / 4 ms) for a static edit / a row-reader edit, fsi 187 / 290
(17 / 30), alc 746 / 696 (19 / 21).

Warm-up (background; the window shows first): fine ≈ 4–6 s (FSI 2.5–3.3 s, FSI baseline ≈ 1 s, Checker ≈ 1 s),
fsi ≈ 3 s, alc ≈ 2.7 s. Dev-host first frame 360–600 ms vs 530–660 ms for the plain app.

Correctness suites:

| suite | fine | fsi | alc |
|---|---|---|---|
| main (`examples/Ranvier.HmrCounter/bench/scenarios.json`) | 38/38 | 38/38 | 38/38 |
| reactive-graph edge cases (`bench/edge.json`) | 35/48 → **48/48** after fixes | 39/48 | 39/48 |
| shared component rendered 3×, across 2 files (`bench/shared.json`) | 43/43 | 43/43 | 43/43 |

The P2/P1 edge failures are the two constraints inherent to whole-module delivery (every setup re-runs on
every save; module-level state resets), not bugs.

## How P3 gets fine-grained

1. **Dev-mode template instrumentation.** `TemplateNode` trees are data, so each definition is mounted through
   a rewritten copy (`src/Ranvier.View.WebCore.Hmr/Fine.fs`). Static attribute values and static text become `Signal<string>`
   cells. Every slot reader, event handler, region items/key/row-conversion and child-props reader goes through a
   swappable cell. Cells are per definition key, so every instance of a component, in any file, updates from one
   write.
2. **Diff, then write only what changed.** A reload walks the new definition. Same structure → write the static
   cells whose value changed and rebind only the closures whose fingerprint changed (a static edit is one signal
   write; a reader edit re-runs that reader's slots). Different structure → remount with state transplant.
3. **Declaration-scoped delivery.** The edited file is split into top-level declarations (FCS untyped AST). If
   no `type` declaration changed since the last *applied* text, only the `let`s are evaluated, in a shadow module
   that `open`s the module in use. New closures then bind to the *same* types, so live instances are reused as
   they are (same signal objects). A changed type declaration falls back to whole-file eval plus a bridge (new
   setup per instance, signal values moved across).
4. **Ranvier.FSharp.Checker as the gate**, in parallel with eval: errors in ~40–80 ms (vs ~250 ms from FSI),
   and the exact re-checked declaration keys (`HmrCounter.Counter.counter:Value`,
   `HmrCounter.Counter.CounterState:Type`, and cross-file dependents such as `HmrCounter.App.app:Value` on a type
   change).

## Constraints learned at the reactive-graph boundary

- **Signals are free-standing; memos and effects are owned.** Signals survive an unmount, so their values can
  be carried. Memos and effects belong to the setup's scope and must be rebuilt by the new setup. P3 runs every
  setup in a nested `createRoot` scope (with `OnCleanup` redirected into it) and disposes it on a bridge.
  Without that, each setup edit leaked an effect that kept firing on shared state (E3).
- **Transplant before effects run.** Effects are queued for their first run. Running setup + value transplant
  in one `Api.batch` lets new effects derive from the moved state. Outside a batch, the transplant overwrote
  an effect-derived signal with the old value (P2/P1 E3, fixed).
- **A bridged instance needs all of its readers re-run**, even readers whose code didn't change, because they
  now read a different instance. P3 bumps a per-component epoch signal that every bridged call reads.
- **Module-level values split on re-evaluation.** Re-running `let shared = lazy (Api.createSignal 0)` creates
  a second store: new readers read it while unchanged handlers still write the old one (P3 before fix:
  `shared is 0` forever). P3 aliases unchanged public, immutable values to the generation that last defined them.
  Private or mutable module values cannot be aliased from FSI and reset. P2/P1 always reset module state.
- **Setup re-runs.** Remount strategies re-run every component's setup on every save (side effects
  re-execute). P3 re-runs a setup only when its own code changed (and then for every live instance).
- **Instances are paired by ordinal on remount.** Correct while setup order is stable (verified for 3
  instances of one component across 2 files). Reordering instances in the template can cross their state.
  P3's in-place patch never re-pairs.
- **Dynamic dependencies are free.** A reader that starts reading a new signal tracks it after rebinding
  (E2), because Ranvier re-collects dependencies on each run.
- **Errors never reload.** An error report leaves the mounted UI and code as they were. The next passing save
  is compared with the last *applied* text, so a broken intermediate state (S6a) doesn't confuse the
  type-change test (S6b). A multi-file batch applies atomically.
- **Fingerprints must compare like with like.** fsc and FSI name and emit closures differently, so P3 baselines
  every file through FSI at warm-up and keeps those baselines across fallback remounts. Fingerprints follow
  nested closure bodies (a memo or effect lambda is a `newobj`, not a call) and ignore the line numbers in
  closure names.
- **Checker promotion is asynchronous.** A file is checked whole until its by-declaration build finishes.
  "Nothing re-checked" only means "no change" when the check ran in declaration mode (`ProjectGraph.Mode`), so
  the gate waits on `WhenPromoted`.
- **Not covered yet:** components selected through `matchWith`/`branch` arms (arm definitions are user values
  read at runtime, so they update only when the parent re-evaluates), `Hooks.mounted` (not re-run on a patch),
  and keyed rows whose props type is redefined (P3 falls back to a remount when a value had to cross via JSON).
  Helper function bodies used from other files are covered by the follow-up below.

## Follow-up: the fingerprinting dev compiler (P3 + stamps)

Built: `experiments/Ranvier.View.Hmr.Fsc`, a compiler driver MSBuild runs in place of `fsc.dll`
(`dotnet build -p:HmrStamp=true` sets `DotnetFscCompilerPath`). It compiles with the fickle FCS and embeds one
stamp per top-level declaration as `AssemblyMetadata`: the fickle declaration splitter's key, plus an FNV-1a hash
of the declaration text. `ParsedDeclaration.SyntaxHash` is an `int`, so it isn't assumed stable across processes.
`src/Ranvier.View.WebCore.Hmr/Stamps.fs` is compiled into both the driver and the dev host, so both sides compute identical
stamps.

What the dev host does with them (`deliverFine` in `Dev.fs`):

- **Declaration-level change sets from stamps.** Each reload reports what changed (`changed hint:Value`).
  Comment-only edits are no-ops in ~22 ms without the Checker. Lets-only versus whole-file is decided from
  stamp kinds (`Value`/`Open` vs `Type`/`Attribute`/`Do`).
- **Dependents from the Checker.** `DeclarationResult.TypedUses` plus `References` are walked to a fixpoint.
  `References` is internal in the fickle build and read by reflection; a cross-file `DeclarationRef` has no `Key`,
  so the key is rebuilt from its module path and name. Uses of *components* don't propagate (components are
  found by key at mount).
- **Cross-file re-evaluation.** A dependent file is re-evaluated lets-only with `module Shared =
  HmrCounter.Shared__hmrN` abbreviations, so its calls bind to the latest generation of the helper.
- **Patch filtering.** Only components whose declaration changed or depends on a change are diffed. The rest of
  the file is "not touched" (no walk, no rebind, no bridge).
- **Drift sync.** Embedded stamps vs the sources on disk at startup. A binary older than its sources is
  hot-reloaded before the first interaction ("1 files newer than the build", 219 ms).

The experiment's answer: **compiling with the same compiler as FSI does not make IL fingerprints match.**
Reloaded code always lives in a different assembly from the types it uses. The in-app closure reads
`ldfld Todo.Id@` directly; the FSI closure has to `call Todo::get_Id`, since the backing field is internal to
its assembly. FSI also emits `tail.` prefixes. So closure-level comparison has to stay FSI-to-FSI. The warm-up
baseline became a **lazy first-touch baseline** per file (and per dependent file) instead.

| | P3 (warm-up FSI baseline, fsc build) | P3 + stamps (fickle build, lazy baseline) |
|---|---|---|
| suites (main / edge / shared / cross-file) | 38 / 48 / 43 / – | 38 / 48 / 43 / **17 of 17** |
| cross-file helper edit (C1) | not handled | dependent re-evaluated; exactly the one reader rebound |
| warm-up | 4.2–5.9 s | 3.9–4.0 s |
| first edit of a file | ~125 ms | ~230–300 ms (first-touch baseline ~120–200 ms) |
| steady state (static / reader / handler) | 90 / 101 / 98 ms | 91 / 82 / 88 ms |
| incremental build | 2.4 s (fsc) | 4.6 s (cold fickle driver per build) |

For comparison on the cross-file suite: P2 `fsi` 15/17 (only the edited file re-evaluates, so callers keep the
old helper), P1 `alc` 17/17 (recompiles everything).

## 3D through Ranvier.Wgpu

The sample is `examples/Ranvier.HmrWgpu`, on the published `Ranvier.Wgpu` / `Ranvier.Wgpu.Sdl` 0.1.0-alpha.2
(`.Traced` in traced builds) and this repository's `GpuSurface` compositing (#145), on the GPU path: DX12 renders
into a D3D11 shared texture that the page composites beneath its HTML. It is a lit sphere on a `<canvas>` with an
HTML overlay; it renders on demand, and the bench (`pwsh bench/run.ps1 -Strategy fine`) counts renders and samples
the presented frame at the canvas.

The shape that keeps 3D state across reloads:

- `Dev.RunWith` builds the root props from the window, so the `WgpuContext` lives as long as the window and no
  reload creates a device.
- The props type lives in a file reloads do not redefine: every new setup receives the original props object.
- Meshes and batches are created in setup and released through `OnCleanup`, so a setup edit rebuilds them and the
  bridge disposes the old ones with the old setup's scope.
- The surface is bound in the canvas's `mounted` hook and held in a signal; the render effect in setup reads it. A
  bridged setup renders into the same surface; a remount binds a new one.
- On exit the host disposes the tree, then props that implement `IDisposable` (the sample's `GpuProps` disposes its
  context), then the window.

| Edit (fine) | Applied as | GPU renders | Save → presented |
| --- | --- | --- | --- |
| overlay text, overlay reader, camera handler | cell write or rebind | 0 | ~122–163 ms |
| batch colour in setup | bridge: new batch, old one disposed | 1, into the same surface | ~125–136 ms (apply 3–6 ms) |
| module-level value read by the render effect | forced rebind of its dependents | 1 | ~133–141 ms |
| structure around the canvas | remount; surface bound again | 1 | ~142–169 ms (apply 23–34 ms) |
| compile error | blocked | 0 | ~88 ms |

All 43 steps pass under `fine` on the published packages. In the prototype runs (0.1.0-alpha.1), `fsi` passed every
visual check but remounted and rebuilt the GPU batch and surface on each save: 14 renders for the scenario against
8. Under `alc` every reload fails with
`InvalidCastException: [A]GpuProps cannot be cast to [B]GpuProps`: the reloaded assembly has its own copy of the
props type, so `alc` needs root props types in an assembly it does not reload.

Three host changes came out of it. A module-level value lives in a static field, so a change to it leaves the IL of
the closures that read it unchanged: their components are now rebound when the checker reports them as dependents
of a changed value. An FSI baseline is compiled with the same aliasing as a reload, or the first edit of a file
reads an alias getter where the baseline read a static field and bridges for nothing. And the host releases what
it built in order on exit. Before that, it returned without disposing the window or the props, and neither of two GPU
runs exited cleanly: one hung until the runner killed it, the other reported exit code 0 but stayed resident in
kernel teardown for about ten minutes. While it stayed, `taskkill` found no running instance and every new D3D11
device in any process failed to create. With the ordered teardown, three bench runs and two plain runs each exited
within seconds.

## Recommended next steps

1. **The remaining piece of a fingerprinting FSC is a per-lambda identity that survives the assembly boundary.**
   If the fork's IlxGen stamped each closure class with the syntax hash of its lambda, in both fsc and FSI modes,
   the dev host would compare attributes instead of IL and the FSI baseline would disappear. Declaration stamps
   alone don't need the compiler swap: the dev host computes them, and drift detection only needs a per-file
   content hash at build. MSBuild's `GetFileHash` could embed that at no cost, versus +2.2 s per build for the
   cold driver. Make `DeclarationResult.References` public in the fork.
2. **Productise P3's cell layer inside Ranvier.View.Dom.** A dev flag that compiles templates with cells
   natively removes the reflection over the internal `Component` constructor and the static-to-slot rewrite.
3. **Watch for CLR deltas.** FCS 43.13 / SDK 11 RC1 ship `HotReloadBaseline` and the delta metadata writer
   internally, but no public driver, and `dotnet watch` only drives Roslyn. Once F# exposes it,
   `MetadataUpdater.ApplyUpdate` could replace method bodies in place; P3's epoch would still be needed to
   re-run readers.
4. Keep the dev host out of release graphs (a Debug-only reference). It pulls in FCS, MSBuild and FSI, and it
   is JIT-only.

## Layout

- `src/Ranvier.View.WebCore.Hmr/`: `Dev.fs` (host, watcher, strategies, bench runner), `Fine.fs` (P3 engine),
  `Remount.fs` (registry + P1/P2 remount), `Delivery.fs` (FSI, in-process compile + ALC, declaration splitter,
  shadow modules, Checker gate and dependents), `Stamps.fs` (declaration stamps), `Fingerprint.fs`,
  `Transplant.fs`, `Reflect.fs`.
- `experiments/Ranvier.View.Hmr.Fsc/`: the fingerprinting dev compiler.
- `examples/Ranvier.HmrCounter/`: the app (`Shared.fs`, `Counter.fs`, `Todos.fs`, `Edge.fs`, `App.fs`).
- `examples/Ranvier.HmrCounter/bench/`: scenarios (`scenarios.json`, `scale.json`, `edge.json`, `shared.json`, `cross.json`, `drift.json`),
  `run.ps1` (timeout, log, restores sources), `summarize.py`, `results/`.
- `examples/Ranvier.HmrWgpu/`: the 3D sample (`Gpu.fs` props, `Scene.fs`) and its scenario, `bench/wgpu.json`.

Unattended bench runs use `-software`: one GPU-presented run hung in a kernel wait (D3D11 present)
and could not be killed from user mode. That's worth a separate look.
