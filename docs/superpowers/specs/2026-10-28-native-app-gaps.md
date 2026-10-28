# From the WebCore backend to a Ranvier native app: the remaining gaps

Written 2026-10-28, at `webcore-backend` `3556cdc5` and Ranvier.WebCore `v0.1.0-preview.4` (ABI 20). The
[backend design](2026-10-28-native-webcore-backend-design.md) covers what exists; this lists what stands between
it and an app a person runs: a window showing Ranvier components, taking input, shipped as one executable.

## Landed: macOS and NativeAOT

The macOS presenter and end-to-end AOT publish work (order-of-work items 4 and 5) landed on `main`:

- [Ranvier.View#2](https://github.com/ranvier-org/Ranvier.View/pull/2), `ccf74165`: macOS main-thread support,
  the visible counter smoke, both apps' Windows/macOS NativeAOT publish-and-run checks, and the documentation.
- [Ranvier.WebCore#4](https://github.com/ranvier-org/Ranvier.WebCore/pull/4), `615960e6`: static delegate
  marshalling in the canonical ABI wrapper. All five native platform jobs passed before merge.

The backend suite passed 770 tests with 16 ignored and no failures. Both published apps ran on Windows x64
and macOS 26 arm64, including a visible counter window. Hardware qualification at Retina scale 2 and with
a real IME is still unverified; the CI runner reports scale 1. Broader AOT analysis warnings remain recorded
below. These qualifications do not change the completed publish-and-run acceptance checks.

The next implementation batch is ABI 21 in order-of-work item 6. Paused work remains paused.

## Landed: Linux and Windows ARM64

[Ranvier.View#4](https://github.com/ranvier-org/Ranvier.View/pull/4), merged as `688feec`, completes the
Linux x64 and real Windows ARM64 execution checks. [Run 40675534807](https://github.com/ranvier-org/Ranvier.View/actions/runs/40675534807)
passed all checks: each Windows x64, Windows ARM64, Linux x64 and macOS 26 backend job reported 773 passed,
16 existing ignored and no failures or errors. All four platforms ran the visible regular sample and
published and ran both NativeAOT apps. Windows ARM64 used the hosted `windows-11-arm` runner; Linux used
`ubuntu-24.04` with Xvfb. The visible samples reported 480x320 backing pixels and scale 1.

The changes add Linux runtime dependencies, conservative WPE glyph damage bounds and device-pixel-aligned
partial painting at fractional scales. Three regressions cover scale 1.25, scale 1.2 fallback and resized
viewport bounds. Physical Retina scale 2 and a real IME remain unverified.

## Landed: shared DOM interface packages

[Ranvier#249](https://github.com/ranvier-org/Ranvier/pull/249), merged as `f371df76`, publishes
`Ranvier.Dom`, `Fable.Ranvier.Dom` and their traced variants at `0.1.0-alpha.1`. The two projects include
the same generated source, pinned to WebCore generator `7213a5d`. Both .NET target frameworks build;
the packages share a CLR assembly identity, and the native contracts do not depend on Fable or Xantham.

[Master CI 40694699765](https://github.com/ranvier-org/Ranvier/actions/runs/40694699765), the DOM drift
check and performance checks passed. [OIDC publication 40695381647](https://github.com/ranvier-org/Ranvier/actions/runs/40695381647)
succeeded. All four packages appeared on the public NuGet feed; fresh public-source consumers then
passed normal/traced assembly-identity checks, Fable compilation and JavaScript execution. This lands
the interface-package part of G8. View/backend migration and native package publication remain in progress.

[Ranvier#250](https://github.com/ranvier-org/Ranvier/pull/250), merged as `ef19d86b`, restores the
`CSSStyleProperties` contract and both inline-style getters omitted from alpha.1. The corrected source is
pinned to generator `e81b32b`; all PR checks passed, including the IDL drift check, packed normal/traced
.NET and Fable consumers, and ARM/x64 performance jobs. Alpha.2 publication follows main-branch CI.

## In integration: ABI 21 and the native View migration

[Ranvier.WebCore#5](https://github.com/ranvier-org/Ranvier.WebCore/pull/5) contains ABI 21, canonical managed
interop, the five-RID aggregate, Windows UI Automation and shared D3D11 transport. Its coordinated native
build and publication are still in progress; the private runtime checks do not substitute for that release.

The composed View branch removes copied interop, uses the shared DOM contracts on both targets, and enables
the four previously pending conformance cases. Its full Windows x64 backend suite passed 781 tests, with
12 existing ignored and no failures or errors. Regressions cover namespace creation, secondary documents,
union/sequence/variadic transport, nested template cloning/import, CSS defaults, popover dictionary handles
and passive/capture/once listeners. The expanded smoke app passed 15 checks under JIT and NativeAOT,
including hydration retaining its server node and a subsequent native click updating state.

The composed visible counter also passed JIT and NativeAOT checks against the private GPU runtime:
480x320 SDL backbuffer, scale 1, shared D3D11 frames, clipboard and an independent OS client verifying
the UI Automation tree, Invoke, reactive name changes and provider disconnection after teardown.
Public-package consumers, final four-platform integration CI, merges and publication remain required.

## Where main stands

| Piece | State |
|---|---|
| Ranvier.WebCore native host | ABI 20 on main. Builds on win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64 in CI; preview.4 consumed from nuget.org by all four backend platform jobs |
| `Ranvier.View.WebCore` | Runs on win-x64, real win-arm64, linux-x64 and osx-arm64 (macOS 26); 773 backend tests passed per platform, 16 existing ignored, no failures |
| Conformance | ConformanceCases' fifteen modules match the DOM twin, except four pending cases |
| AOT | Both apps publish and run with NativeAOT locally on win-x64 and in CI on win-x64, win-arm64, linux-x64 and osx-arm64. Mount, paint, native click, repaint and the visible sample smoke pass. FSharp.Core/view reflection, trimming and generic-recursion warnings remain visible |
| Window | `Ranvier.View.WebCore.Sdl` runs on Windows x64/ARM64, Linux x64 and macOS 26: paint, mouse, wheel, keys, IME event forwarding, focus, cursor and text/plain clipboard. Hidden-window tests and visible sample smoke pass (480x320, scale 1). Both NativeAOT apps run in all four CI jobs. Retina scale 2 and a real IME remain unverified |
| Accessibility | ABI complete; no platform bridge ([help wanted](https://github.com/ranvier-org/Ranvier.WebCore/pull/2)) |

Critical path for a first Windows app: gaps 1, 2 and 3. The rest can follow in any order.

## 1. A window: the SDL3 presenter

The backend paints into a buffer and takes input events; the design leaves putting the pixels on screen to
the app. SDL3, through `ppy.SDL3-CS` (plain P/Invoke, natives for all five RIDs), is the chosen layer.

Implemented:

1. **Project.** `src/Ranvier.View.WebCore.Sdl` (decision G1), referencing `Ranvier.View.WebCore` and `ppy.SDL3-CS`.
   Trimmable and AOT-compatible, like the backend.
2. **Window and texture.** `SDL_CreateWindow` with `SDL_WINDOW_HIGH_PIXEL_DENSITY | SDL_WINDOW_RESIZABLE`, an
   `SDL_Renderer`, and a streaming `SDL_PIXELFORMAT_ABGR8888` texture (bytes R, G, B, A in memory, as
   `rv_host_paint_rgba` writes them) at the window's pixel size.
3. **Frame.** After `rv_host_update`, paint the dirty rectangles (or the whole page until dirty rectangles
   are used) into the texture with `SDL_UpdateTexture`, then `SDL_RenderTexture` and `SDL_RenderPresent`. Paint
   only when the page has asked for a frame.
4. **Loop.** One thread, the host thread: `SDL_WaitEventTimeout` with the timeout to the next WebCore timer,
   dispatch the events, `rv_run_loop_cycle`, then the frame. The wrapper table's finalizer releases also drain
   here, so the loop must keep cycling while the window is open.
5. **Input.** Mouse motion, buttons and wheel to `rv_host_mouse` and the wheel member; keys to the key member,
   with SDL keycodes mapped to DOM `key` and `code`; `SDL_EVENT_TEXT_INPUT` to text insertion; IME composition
   (`SDL_EVENT_TEXT_EDITING`) to the ABI's IME composition members (`rv_abi_has("ime")`, decision G4). Focus in and
   out of the window to the page's focus.
6. **Size and density.** `SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED` resizes the host and the texture;
   `SDL_GetWindowDisplayScale` sets the page's device scale factor, so CSS pixels stay CSS pixels on HiDPI.
7. **Cursor.** Map the page's cursor (the `cursor` CSS value at the pointer) to `SDL_CreateSystemCursor`.
8. **Clipboard.** Bridge the host's clipboard (docs/CLIPBOARD-DND.md in Ranvier.WebCore) to `SDL_SetClipboardText`
   and `SDL_GetClipboardText`.
9. **API.** One entry point an app calls, for example `Window.run (title, width, height) (fun host -> mount ...)`,
   returning when the window closes (decision G2).
10. **Sample.** `examples/Ranvier.NativeCounter`: a counter and a text input, published and run with
    `PublishAot` on Windows x64 and macOS 26 arm64.

Verified: hidden-window mounting, presentation, pixel readback and SDL input tests pass. The regular and
NativeAOT sample opens a visible window in macOS CI; Windows NativeAOT sample checks also pass. WebCore's
run loop and SDL's event pump share the macOS process main thread (G3).

## 2. The native package from nuget.org

The landed backend defaults to native package version `0.1.0-preview.4`, selecting the publish target's
runtime identifier or the SDK host's. `RanvierWebCoreNativeDir` is an opt-in local-build override. Fresh CI
checkouts restore packages and pass backend and publish-and-run checks on Windows x64/ARM64, Linux x64
and macOS arm64.

Remaining for the ABI 21 batch: the five-RID `Ranvier.WebCore.Native` meta-package (G5), replacing the
current `RanvierWebCoreNativeRid` selection. This is order-of-work item 6, outside the completed macOS/AOT batch.

## 3. Landing on master

Completed: [Ranvier.View#1](https://github.com/ranvier-org/Ranvier.View/pull/1) landed the preview backend
and SDL presenter on `main`; [Ranvier.View#2](https://github.com/ranvier-org/Ranvier.View/pull/2) landed macOS
and NativeAOT coverage. Main CI restores the native package and runs the backend on Windows and macOS.

[Ranvier.View#4](https://github.com/ranvier-org/Ranvier.View/pull/4) adds Linux x64 and real Windows ARM64
to those backend, visible sample and NativeAOT gates.

The settled later decisions remain: move the copied interop to a managed `Ranvier.WebCore` package (G6),
keep the `Ranvier.View.WebCore` name (G7), and decouple the Fable interfaces before stable release (G8).

## 4. AOT publish end to end

Both apps published and ran locally on win-x64 with NativeAOT on 2026-10-28. The smoke app verified mount,
paint, native click, repaint and listener errors; the sample passed its visible-window smoke. The machine already
has VS C++ Build Tools. This chat's shell omitted `OS`; setting `OS=Windows_NT` for the publish process fixed the
initial cross-OS compilation error. NativeAOT reported a non-generic delegate-marshalling warning from
`Interop/Native.fs`; the generic overload fixes it in the upstream ABI wrapper and the copied wrapper.
FSharp.Core reflection/trimming and generic-recursion warnings remain visible; these successful app runs cover
the exercised paths, rather than proving every view feature AOT-safe.

Completed, 2026-10-28:

1. Both apps published locally with .NET 10 and the installed VS C++ Build Tools and Windows SDK. The
   READMEs record the Windows and macOS prerequisites and publish commands. Both Windows executables have
   native PE headers with no CLR header.
2. The warning was IL3050 from the non-generic `Marshal.GetFunctionPointerForDelegate` overload. The three
   callbacks now use its generic overload. The fix is merged in [WebCore#4](https://github.com/ranvier-org/Ranvier.WebCore/pull/4)
   and in the copied view wrapper through [View#2](https://github.com/ranvier-org/Ranvier.View/pull/2); the hand-written callback section is outside the generated declarations.
   The header/declaration check still matches all 175 upstream functions.
3. The published smoke executable verified mount, paint, a native click, repaint and listener errors. The
   published counter passed its bounded visible-window, backing-size, painted-content and clipboard checks.
4. [Windows CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137299) and
   [macOS CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137302) published
   and ran both apps successfully. Both counter runs reported 480x320 backing pixels and display scale 1.

This closes the publish-and-run gap for these apps. The remaining AOT warnings are retained in the CI logs;
support for every view feature under AOT is not established by these smoke runs.

## 5. DOM members the ABI lacks

Four conformance cases are pending: three need a second document (`DOMImplementation.createHTMLDocument`),
one needs `createElementNS`. Members taking unions of nodes, sequences or variadic arguments
(`append(...nodes)`) throw `NotSupportedException`. The view's own paths use none of them, so apps are not
blocked, but user code calling them is.

Work, in the Ranvier.WebCore repo as the next ABI batch (ABI 21; one ABI version per release batch):

1. `createElementNS` and `createHTMLDocument` in `gen/dom.json`; the generator's handling of a second document's
   lifetime in the object table.
2. Mapping unions, sequences and variadics in the generator (decision G11: now, or when a user hits one).
3. Regenerate canonical `Native.fs` and `NativeDom.fs` in Ranvier.WebCore, consume the managed package in
   Ranvier.View, remove copied interop, and un-pend the four cases.

## 6. Other platforms

Completed by [Ranvier.View#4](https://github.com/ranvier-org/Ranvier.View/pull/4) and
[run 40675534807](https://github.com/ranvier-org/Ranvier.View/actions/runs/40675534807): Linux x64 loading,
the complete backend suite, the visible SDL3 sample and both NativeAOT apps pass. The same checks pass on
a real hosted Windows ARM64 runner, alongside Windows x64 and macOS 26. Each platform passed 773 tests,
with 16 existing ignored and no failures or errors. Linux clipboard and input checks use the WPE host
under Xvfb. This proves the specified Linux x64 and Windows ARM64 execution; it does not establish a
Linux ARM64 backend/presenter run.

Retina scale 2 and a real IME remain manual qualifications. macOS 15 still lacks
`libswiftCompatibilitySpan.dylib`; decision G12 keeps that work paused and ships macOS 26 support.

## 7. Accessibility bridges

The ABI is complete; the bridges (UI Automation, NSAccessibility, AT-SPI) are documented as help wanted in
Ranvier.WebCore's `docs/ACCESSIBILITY.md`. The SDL3 presenter must expose its native window handle so a bridge
can attach. Not on the critical path (decision G13).

## 8. Optional: GPU compositing

PR #1 in Ranvier.WebCore (`gpu-compositing`) adds accelerated compositing on Windows. The CPU paint works
without it; with it, the presenter would take a GPU texture instead of copying pixels. Linux and macOS
compositing are paused. After the first app works.

## Decisions

Numbered as above; answers recorded in the next section.

| # | Decision |
|---|---|
| G1 | The presenter's project: a package in Ranvier, or a sample only |
| G2 | The presenter's API shape |
| G3 | macOS: the main thread's run loop shared by WebCore and SDL |
| G4 | IME composition in the first presenter |
| G5 | Native packages: RID-conditional references or a meta-package |
| G6 | Where the managed interop lives |
| G7 | The package name |
| G8 | The `Fable.Ranvier` seam change |
| G9 | Master's CI runners for the backend |
| G10 | AOT publish in CI |
| G11 | Unions, sequences, variadics: now or on demand |
| G12 | macOS 15 support |
| G13 | Accessibility timing |

## Answers

Round 1, 2026-10-28:

- **First app's platforms:** Windows, then macOS; Linux once CI runs the backend tests there.
- **G1:** a package, `Ranvier.View.WebCore.Sdl`, so the backend stays free of a window toolkit.
- **G5:** a `Ranvier.WebCore.Native` meta-package in the WebCore repo depending on the five RID packages.
- **G6:** a managed `Ranvier.WebCore` package from the WebCore repo, versioned with the ABI; Ranvier stops copying
  `Native.fs` and `NativeDom.fs`.
- **G7:** `Ranvier.View.WebCore`.
- **G8:** reshaped. Before any stable release the native path drops its dependency on Fable: today the generated
  wrappers implement `Fable.Core.TS.Dom` interfaces from `Xantham.Fable.Core.TS`, and `Ranvier.View.WebCore`
  references `Fable.Ranvier`. The user proposes a conditionally compiled project publishing `Fable.Ranvier` and a
  native equivalent; the shape is decided in round 2.
- **G10:** add the Windows AOT publish of the smoke app to CI once it works locally.
- **G11:** an idiomatic F# surface now, not on demand: consumers go where convenience lies. Unions, sequences and
  variadics map in ABI 21 with `createElementNS` and `createHTMLDocument`; the mapping is decided in round 2.
- **G12:** ship macOS 26 only; revisit when macOS 15 is needed.
- **G13:** bridges left to contributors, the presenter exposing the native window handle; a stable release
  waits for at least one bridge.

Round 2, 2026-10-28:

- **G8, shape:** one set of DOM interface sources, two thin projects (one for Fable, one for .NET) that include
  them, with `#if FABLE_COMPILER` only where the two differ (attributes, `Emit`). Not one project switched by a
  property: one package per project file keeps restore, `obj/` and the IDE sane. Fable compiles an interface
  member call to a member access by name, so one definition serves browser objects and native wrappers.
- **Who owns the interfaces:** Ranvier. Xantham stays Fable-only (the user owns it; separating Fable from it is
  not its concern). The source of truth is WebKit's IDL as `gen/domgen.py` reads it for `gen/dom.json`, so the
  ABI, the wrappers and the interfaces come from one model and cannot drift; `tools/Ranvier.View.WebCore.Generate`
  moves off reading `Xantham.Fable.Core.TS`. Xantham's generator is not needed: it reads TypeScript's
  `lib.dom.d.ts`, which describes the browser, not what this WebCore build implements (IDL `Conditional`,
  `EnabledBySetting`, the members `dom.json` scopes).
- **Timing:** land `webcore-backend` on master as a preview on the current Fable interfaces; decouple before the
  first stable release.
- **G11, mapping:** overloads per union arm, `[<ParamArray>]` for variadics, discriminated unions for union
  results. No erased `U2` types.
- **G2:** both: `Window.create`/`Step` for apps that own their loop, and `Window.run` built on them.
- **G4:** IME composition wired from the first presenter.
- **G9:** master's CI runs the backend on Windows first; macOS and Linux join with gap 6.
- **G3:** a throwaway macOS spike on the CI runner right after the Windows presenter works.

Round 3, 2026-10-28:

- **Where the interfaces are built:** `gen/domgen.py` in Ranvier.WebCore writes the F# interface sources; they are
  committed into Ranvier, which publishes the interface packages (a .NET `Ranvier.Dom` and its Fable variant) in
  its own release cycle. Ranvier's CI runs the generator's check against a pinned WebCore commit to catch drift.
- **Scope for Fable users:** the view and Ranvier's public API move to the new interfaces on both targets, so a
  component compiles unchanged for the browser and native. Fable code needing a member outside the scope casts
  to Xantham's types, which costs nothing on JavaScript.
- **`dom.json` scope:** curated to what native UI needs (forms, events, layout reads, ranges, selection), widened
  generously on request; `gen/dom-report.txt` lists each member left out and why, and each batch measures the
  size cost.

Round 4, 2026-10-28:

- **G3 result:** WebCore's run loop and SDL's event pump share the macOS process main thread without
  changes to `Window.Step`. [Run 40645038498, macOS job](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40645038498/job/122671771869)
  passed 770 tests, with 16 ignored, 0 failed and 0 errored, on `macos-26` (arm64). The tests use their own
  entry point through `dotnet run` to keep the bodies on the main thread. Hidden-window paint readback and
  events through SDL's queue pass. The original run is fully green.
- **Visible macOS sample:** [run 40647793710](https://github.com/ranvier-org/Ranvier.Views/actions/runs/40647793710/job/122680570079)
  passed the visible counter smoke: 480x320 backing pixels, display scale 1, nonuniform painted content and
  an SDL `text/plain` clipboard round-trip. Retina scale 2 and a real IME remain manual checks.
- **G10 result:** both apps published and ran locally on Windows x64, then in
  [Windows](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137299) and
  [macOS arm64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137302) CI.
  The macOS job also passed 770 tests, with 16 ignored and no failures. The ABI wrapper's delegate-marshalling
  warning is fixed; broader FSharp.Core/view AOT analysis warnings remain documented above.

The frontier is empty: every decision above is settled.

## Order of work

1. Complete for Windows/macOS: consume preview.4 from NuGet (gap 2); the meta-package follows in item 6.
2. Complete: the preview backend landed on `main` through Ranvier.View#1, with Windows CI (gap 3).
3. Complete: the SDL3 presenter, `Window.create`/`Step`/`run`, IME forwarding and the counter sample (gap 1).
4. Complete: the macOS run loop spike (G3) and presenter smoke on macOS 26 (gap 6); Retina and real IME
   checks remain manual.
5. Complete: NativeAOT publish and execution of both apps locally on Windows, then Windows/macOS CI (gap 4).
6. ABI 21 in Ranvier.WebCore: `createElementNS`, `createHTMLDocument`, the union, sequence and variadic mapping,
   the managed `Ranvier.WebCore` package, the `Ranvier.WebCore.Native` meta-package, and `domgen.py` emitting the
   F# interfaces (gaps 5, 2; G5, G6).
7. The Fable decoupling onto the generated interfaces, before the first stable release (G8).
8. Linux x64 and real Windows ARM64 execution is complete through View#4 (gap 6). Accessibility integration,
   with one bridge required for stable (gap 7), and Windows GPU presentation (gap 8) remain in progress.
