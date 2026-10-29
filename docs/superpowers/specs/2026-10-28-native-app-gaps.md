# Native app gaps: implementation and release evidence

Written 2026-10-28, at `webcore-backend` `3556cdc5` and Ranvier.WebCore `v0.1.0-preview.4` (ABI 20). The
[backend design](2026-10-28-native-webcore-backend-design.md) records the initial implementation. This document
tracks the settled app requirements and their verification: a window showing Ranvier components, taking
input, with a native executable and the accompanying platform libraries/resources.

The implementation is merged through [View#5](https://github.com/ranvier-org/Ranvier.View/pull/5)
(`09686e7`); [release PR3](https://github.com/ranvier-org/Ranvier.View/pull/3) is merged as `66b657a`.
[Release main CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40790683482),
[docs deployment](https://github.com/ranvier-org/Ranvier.View/actions/runs/40790683308) and
[trusted OIDC publication](https://github.com/ranvier-org/Ranvier.View/actions/runs/40791927016) passed.
All twelve new View/native package versions are public with that exact release source. The
[fully public normal/traced acceptance run](https://github.com/ranvier-org/Ranvier.View/actions/runs/40793629803)
passed all four CPU/GPU jobs: 16 JIT/NativeAOT app runs, 120 smoke assertions, eight independent Windows
UIA checks and 16 output-payload verifications. Both package-only Fable/browser counters also passed
0-to-click-to-1 using public View 0.1.1 and the explicit public Fable DOM alpha.2 companion. Every
non-paused implementation and release gate below is closed; qualifications remain explicit.

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

ABI 21 and the shared DOM migration have now landed as recorded below. Paused work remains paused.

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
the interface-package part of G8. The paired View/backend migration landed through View#5.

[Ranvier#250](https://github.com/ranvier-org/Ranvier/pull/250), merged as `ef19d86b`, restores the
`CSSStyleProperties` contract and both inline-style getters omitted from alpha.1. The corrected source is
pinned to generator `e81b32b`; all PR checks passed, including the IDL drift check, packed normal/traced
.NET and Fable consumers, and ARM/x64 performance jobs. [Main CI 40715311270](https://github.com/ranvier-org/Ranvier/actions/runs/40715311270)
and [OIDC publication 40716357543](https://github.com/ranvier-org/Ranvier/actions/runs/40716357543) passed.
All four alpha.2 packages are public; fresh official-NuGet consumers passed normal/traced .NET
assembly-identity and style-contract checks, Fable compilation and JavaScript execution.

## Landed: ABI 21 and the native View migration

[Ranvier.WebCore#5](https://github.com/ranvier-org/Ranvier.WebCore/pull/5), merged as `6222b6f9`, contains ABI 21,
canonical managed interop, the five-RID aggregate, Windows UI Automation and shared D3D11 transport.
[Pre-merge run 40706181592](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40706181592) passed all
five native builds/runtime checks and managed aggregation. Immutable tag `v0.1.0-preview.6` points to that
merged revision. [Release run 40722932631](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40722932631)
was stopped before publication when package-only testing found cross-RID resource-path corruption. The tag
remains unchanged. [Native correction PR6](https://github.com/ranvier-org/Ranvier.WebCore/pull/6), merged as
`eef227e2`, fixes target scoping and restores inline-style contracts. Its [pre-merge run 40729363033](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40729363033)
passed all five native runtime jobs, the real multi-RID resource regression and managed aggregation.
Immutable tag `v0.1.0-preview.7` points to that exact merged tree, retaining ABI 21. [Release run 40746907938](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40746907938)
passed all five native jobs, managed aggregation and OIDC publication of the corrected Slim cohort.
[Windows GPU run 40766367608](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40766367608) also
passed runtime checks and OIDC publication on the same immutable tag and source. Fresh official-NuGet
consumers verified all seven Slim/managed/meta packages and the matching GPU package,
all five matching clean-source manifests and exact payload hashes. Both apps passed normal/traced JIT and
NativeAOT runs with public native packages and locally packed View libraries. The final fully public
View package matrix also passed, with no producer project references or CI/local package feed.

[View#5](https://github.com/ranvier-org/Ranvier.View/pull/5) removes copied interop, uses the shared DOM
contracts on both targets, and enables all four previously pending conformance cases. Its
[full source CI 40774290136](https://github.com/ranvier-org/Ranvier.View/actions/runs/40774290136)
passed all 17 enabled jobs, including 781 backend tests per Windows x64, Windows ARM64, Linux x64 and macOS
26 job, with 12 existing ignored and no failures or errors. Regressions cover namespace creation, secondary documents,
union/sequence/variadic transport, nested template cloning/import, CSS defaults, popover dictionary handles
and passive/capture/once listeners. The expanded smoke app passed 15 checks under JIT and NativeAOT,
including hydration retaining its server node and a subsequent native click updating state.

The [strict public-runtime GPU integration](https://github.com/ranvier-org/Ranvier.View/actions/runs/40784192253)
passed all 18 jobs. JIT and NativeAOT each presented two shared D3D11 frames at 480x320/scale 1, passed
clipboard checks and an independent OS client's UIA tree/bounds/Invoke/reactive-name/teardown checks.
Strict GPU mode requires nonzero shared frames, so CPU-only fallback cannot pass. Windows ARM64 verifies
UIA attachment/smoke without that external presenter client.

The public package versions are `Ranvier.View`/`.Dom`/browser adapters 0.1.1, backend/presenter
0.1.0-alpha.1, shared DOM contracts 0.1.0-alpha.2, and canonical managed/native/GPU runtime preview.7.
Normal and `.Traced` variants are published. Package-only native/browser acceptance passed after
official-feed indexing; no local feed or CI artifact was substituted for that proof.

## Where main stands

| Piece | State |
|---|---|
| Ranvier.WebCore native host | ABI 21, canonical managed interop, five-RID aggregate and Windows x64 GPU package are public at preview.7, all from clean release `eef227e2`; native Slim/GPU runtime checks and OIDC publication passed. Unpublished preview.6 remains an unchanged failed-attempt tag |
| `Ranvier.View.WebCore` | Merged and released at 0.1.0-alpha.1; runs on win-x64, real win-arm64, linux-x64 and osx-arm64 (macOS 26). 781 backend tests pass per platform, with 12 existing ignored and no failures/errors |
| Conformance | All four ABI21 namespace/secondary-document cases are enabled. The remaining 12 ignored cases are existing browser-specific cases, not pending ABI21 members |
| Shared DOM | Ranvier owns the common generated source; .NET/Fable thin projects publish normal/traced alpha.2 contracts with the same CLR identity. View public APIs use them on both targets. Native closure excludes Xantham and Fable.Ranvier; standard Fable.Core metadata attributes remain |
| AOT | Both apps publish and run with NativeAOT locally on win-x64 and in CI on win-x64, win-arm64, linux-x64 and osx-arm64. Mount, paint, native click, repaint and the visible sample smoke pass. FSharp.Core/view reflection, trimming and generic-recursion warnings remain visible |
| Window | `Ranvier.View.WebCore.Sdl` runs on Windows x64/ARM64, Linux x64 and macOS 26: paint, mouse, wheel, keys, IME event forwarding, focus, cursor and text/plain clipboard. Hidden-window tests and visible sample smoke pass (480x320, scale 1). Both NativeAOT apps run in all four CI jobs. Retina scale 2 and a real IME remain unverified |
| Accessibility | Native Windows UI Automation and the SDL attachment are merged and published. Eight fully public Windows x64 Counter runs verify discovery, geometry, Invoke, reactive names and teardown under normal/traced JIT/AOT; ARM64 presenter checks attachment only |
| Windows GPU | Matching public preview.7 GPU runtime and SDL shared D3D11 presentation. All four fully public normal/traced JIT/AOT GPU counters record two shared frames with actual-backbuffer/UIA checks. Scale/device/renderer CPU fallback remains available |

The implementation, publication and public-consumer critical path is complete.

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

The landed backend defaults to native package version `0.1.0-preview.7` through the five-RID aggregate,
selecting assets for the publish target's runtime identifier or the SDK host's. `RanvierWebCoreNativeDir`
is an opt-in local-build override. Fresh CI
checkouts restore packages and pass backend and publish-and-run checks on Windows x64/ARM64, Linux x64
and macOS arm64.

The ABI 21 native release publishes the five-RID `Ranvier.WebCore.Native` meta-package (G5) and canonical
managed `Ranvier.WebCore` at preview.7. Fresh official-source consumers verified exact five-RID dependencies,
matching clean release manifests and all payload hashes, then ran both apps under JIT and NativeAOT.
View#5 replaces `RanvierWebCoreNativeRid` package selection with this aggregate and consumes canonical
managed interop, with no copied `Native.fs`/`NativeDom.fs`. Both normal/traced backend and presenter
packages are public, and final package-only execution passed with fresh caches and NuGet as the sole
external feed. SDK-injected library-pack paths are standard local SDK sources, not private package feeds.

## 3. Landing on master

Completed: [Ranvier.View#1](https://github.com/ranvier-org/Ranvier.View/pull/1) landed the preview backend
and SDL presenter on `main`; [Ranvier.View#2](https://github.com/ranvier-org/Ranvier.View/pull/2) landed macOS
and NativeAOT coverage. Main CI restores the native package and runs the backend on Windows and macOS.

[Ranvier.View#4](https://github.com/ranvier-org/Ranvier.View/pull/4) adds Linux x64 and real Windows ARM64
to those backend, visible sample and NativeAOT gates.

Completed by [View#5](https://github.com/ranvier-org/Ranvier.View/pull/5): canonical managed interop (G6),
the retained `Ranvier.View.WebCore` name (G7), and shared interfaces/Fable decoupling (G8).
[Release PR3](https://github.com/ranvier-org/Ranvier.View/pull/3) publishes the coherent package cohort.

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
   and in the then-copied view wrapper through [View#2](https://github.com/ranvier-org/Ranvier.View/pull/2).
   That batch matched all 175 baseline header declarations. View#5 removes the copy; current declarations
   are validated and published by the canonical native release pipeline.
3. The published smoke executable verified mount, paint, a native click, repaint and listener errors. The
   published counter passed its bounded visible-window, backing-size, painted-content and clipboard checks.
4. [Windows CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137299) and
   [macOS CI](https://github.com/ranvier-org/Ranvier.View/actions/runs/40649337023/job/122686137302) published
   and ran both apps successfully. Both counter runs reported 480x320 backing pixels and display scale 1.

This closes the publish-and-run gap for these apps. The remaining AOT warnings are retained in the CI logs;
support for every view feature under AOT is not established by these smoke runs.

## 5. ABI 21 DOM members

All four previously pending namespace/secondary-document conformance cases are enabled and pass.
`createElementNS`, `DOMImplementation.createHTMLDocument`, typed unions, sequences and variadics now use
canonical ABI21 transport. Inputs expose overloads per union arm and ParamArray variadics; union results
use named F# discriminated unions. Result ownership and secondary-document lifetimes are exercised by
the native suite, native driver and JIT/NativeAOT smoke.

ABI 21 implements the native work through WebCore#5 (one ABI version per release batch), with corrected
preview.7 published through WebCore#6. View#5 consumes it:

1. `createElementNS` and `createHTMLDocument` in `gen/dom.json`; the generator's handling of a second document's
   lifetime in the object table.
2. Unions, sequences and variadics are mapped in the generator (G11), including UTF-8 strings, node identity,
   container release, optional IDL defaults and dictionary handles.
3. Canonical `Native.fs` and `NativeDom.fs` are generated in Ranvier.WebCore and consumed from its managed
   package. CI regenerates committed wrappers from the restored canonical model/assembly and DOM contracts.

The [generated coverage report](../../../src/Ranvier.View.WebCore/Generated/report.txt) records 1,082
mapped member implementations, 13 event adapters and 14 inherited SVG/area-mixin members outside the
concrete native export scope. The curated scope is preserved as settled in round 3; no full-browser DOM
coverage is claimed. Four event exports use adapters and one select/remove export is redundant with the
inherited mapped operation.

Size accounting uses public Windows x64 preview.4/ABI20 and preview.7/ABI21 packages with the same WebKit
pin. Curated interfaces grow 55 to 62 and mapped functions 1,002 to 1,070; shared contracts contain 229
interfaces. Generated glue grows 607,942 to 696,350 bytes and NativeDom.fs 128,198 to 150,639 bytes; the new
shared F# source is 139,867 bytes. Compressed package size grows 30,611,419 to 30,713,807 bytes (+102,388);
native/resource payload grows 78,285,521 to 78,556,369 (+270,848). These are combined DOM/UIA/GPU-ABI and
local-to-CI build-cohort differences, not an isolated DOM-only binary cost. No trimming work was started.

## 6. Other platforms

Completed by [Ranvier.View#4](https://github.com/ranvier-org/Ranvier.View/pull/4) and
[run 40675534807](https://github.com/ranvier-org/Ranvier.View/actions/runs/40675534807): Linux x64 loading,
the complete backend suite, the visible SDL3 sample and both NativeAOT apps pass. The same checks pass on
a real hosted Windows ARM64 runner, alongside Windows x64 and macOS 26. Each platform passed 773 tests,
with 16 existing ignored and no failures or errors. Linux clipboard and input checks use the WPE host
under Xvfb. This proves the specified Linux x64 and Windows ARM64 execution; it does not establish a
Linux ARM64 backend/presenter run. The final ABI21 source/release CI repeats the specified four-platform
checks with 781 passed, 12 existing ignored and 15 NativeAOT smoke assertions per platform.

Retina scale 2 and a real IME remain manual qualifications. macOS 15 still lacks
`libswiftCompatibilitySpan.dylib`; decision G12 keeps that work paused and ships macOS 26 support.

## 7. Accessibility bridges

Windows UI Automation is implemented in native main through WebCore#5. View#5 attaches it to
the SDL HWND and detaches it before page/window teardown. Independent OS-client checks pass locally under
JIT and NativeAOT for discovery, geometry, Invoke, reactive names and stale-provider disconnection. Native
and presenter packages are published. Strict public-native GPU integration also passes those independent
Windows x64 OS-client checks. Fully public normal/traced JIT/AOT presenter checks passed all eight
independent Counter discovery/action/teardown runs.
ARM64 presenter coverage verifies attachment/smoke. This meets G13's one-working-bridge implementation
requirement; NSAccessibility and AT-SPI remain contributor work.

## 8. Optional: GPU compositing

WebCore#5 includes the Windows shared D3D11 transport. View#5 imports its frames into SDL's
`direct3d11` renderer, flushes before releasing frame leases, and falls back to CPU painting for unsupported
devices/renderers/scales. The coordinated GPU package is public at preview.7 from the same clean source as
Slim. Strict public-runtime CI passed both JIT and NativeAOT with two shared frames, actual-backbuffer
and independent OS UIA checks. The fully public normal/traced package matrix also passed: all four
GPU Counter runs directly recorded two shared frames, with durable NativeAOT stdout/stderr evidence.
CPU runs recorded zero GPU frames. Linux and macOS compositing remain paused.

## Acceptance evidence and qualifications

The final [public package matrix](https://github.com/ranvier-org/Ranvier.View/actions/runs/40793629803)
restores only published packages in four fresh normal/traced by Slim/GPU caches. Each job builds and
executes both apps with JIT, then publishes and executes both with NativeAOT. Eight Smoke runs each pass
15 checks; eight Counter runs verify the 480x320/scale1 backbuffer, clipboard and independent OS UIA.
All sixteen output checks verify ABI21, exact runtime versions/cohorts and all native/resource hashes
(24 Slim or 26 GPU files). GPU consumers explicitly suppress conflicting Slim win-x64 assets while
retaining the five-RID aggregate. Restore graphs contain no producer project references, Xantham or
legacy Fable.Ranvier dependencies. Standard Fable.Core metadata attributes remain in the shared View path.

The first public matrix exited successfully but did not capture the GUI NativeAOT counter's stdout.
The final run adds workflow-only stdout/stderr capture and artifact retention, with the same published
apps and strict assertions; the final counts above come from direct logs, not inferred missing output.

The twelve published View package nuspecs identify release `66b657a`. Independent audits verify sibling
versions and source/README payloads against the reviewed release, allowing NuGet's repository signature
to differ from the unsigned CI ZIP. Normal/traced browser consumers use separate fresh official-only
caches, compile packaged Fable sources without project references and exercise the counter in jsdom.
Local detailed proofs are recorded in the closure ledger; CI run links remain the durable public record.

Physical Retina scale 2 and a real IME remain unverified hardware qualifications. Synthetic input,
fractional-scale damage/alignment and CPU-fallback tests pass; CI reports display scale 1. Linux ARM64
has native build/runtime coverage but no claimed SDL/backend/AOT qualification. Windows ARM64 presenter
UIA coverage is attachment/smoke; the independent presenter client is verified on Windows x64. Existing
FSharp.Core/View trimming, reflection and generic-recursion AOT warnings remain visible; these app runs
do not prove every View feature AOT-safe. The DOM surface remains curated as documented in the report.

Explicitly paused: the native anime port, macOS 15, Linux/macOS GPU, size trims, dependency mirror and
sccache. NSAccessibility and AT-SPI remain contributor work under the settled one-bridge G13 requirement.

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
6. Complete through WebCore#5/#6 and View#5: ABI 21 `createElementNS`, `createHTMLDocument`, union,
   sequence and variadic mapping, canonical managed interop, the five-RID aggregate and shared F# generation.
   Coordinated OIDC publication and fully public package execution pass (gaps 5, 2; G5, G6).
7. Complete: shared DOM interfaces and Fable decoupling landed, published and passed four-platform CI
   (781 tests per platform), twelve-package audits, normal/traced public native execution and packaged
   Fable/browser counters (G8). The four ABI21 conformance cases are enabled.
8. Complete: Linux x64 and real Windows ARM64 backend/presenter/AOT execution (gap 6), the working Windows
   UIA bridge with independent OS verification (gap 7; G13), and public Windows shared D3D11 presentation
   under normal/traced JIT/NativeAOT (gap 8). Hardware qualifications and paused tracks are listed above.
