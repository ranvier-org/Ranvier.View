# CSS keyframe rule conformance

## Core and view state

- Core primitives used: shared DOM interfaces and borrowed computed-style provider.
- View-owned state: temporary stylesheet/probe nodes and captured declarations, removed before returning.

## Scope

C11 ([WebCore issue 36](https://github.com/ranvier-org/Ranvier.WebCore/issues/36)) covers CSSKeyframesRule name, live rule list/length/indexed getter, append/find/delete, and CSSKeyframeRule keyText/style. The acceptance fixture must verify updated offsets/declarations through an associated CSS animation. Paused CSS animation with a negative delay fixes its progress at 25%; no Anime or Web Animations implementation is involved.

## Tasks

- [x] Read pinned CSSKeyframesRule/CSSKeyframeRule IDL and exact shared signatures after a clean FCS check.
- [x] Add shared assertions for identity/parent/list order, key aliases, missing/duplicate/invalid operations, retained deleted rules and exact exception/no-op semantics.
- [x] Observe computed width changes after key offset/declaration/list/name edits with paused deterministic progress.
- [x] Run actual Windows JIT, Chromium and Windows NativeAOT under owned leases using official DOM alpha.12 and WebCore preview.12 packages after its repair is published. Root focused fixture passes82 in each engine/mode; native teardown passes with zero objects. Native restore/publish explicitly selects preview.12; no private engine substitution is used.
- [x] Verify cleanup, formatting/XML/comments, independent review and complete FCS snapshot; platform/profile/published-Views acceptance remains open. Final copied-source/integration review is clear, fresh native FCS has zero diagnostics and XML audit has zero findings.

## Source qualifications

- Pinned keyframe APIs have no upstream commented members. CSSKeyframesRule length/indexed getter must be qualified explicitly if a tested browser does not expose them.
- findRule is nullable. Invalid append/delete inputs are tested against the pinned implementation instead of inventing exceptions. CSSRule.cssText remains the pinned inherited no-op; keyText and style.cssText are the effective mutators.
- The existing SageFs project-session custom-obj refusal applies to this checkout. An owned bare session handles type/pure assertion checks; separate CLI processes handle the real engine. The native project has already been built successfully before session creation.

## Runtime evidence

- Chromium passes all 82 C11 assertions with the existing official DOM alpha.12 interfaces. Its length property and indexed getter are available; invalid keyText raises SyntaxError while preserving the complete rule.
- Official native preview.11 fails the real offset-edit assertion: paused 25% width remains 60px after moving its middle key from 25% to 50%, rather than becoming 40px. A separate raw ABI process reproduces the failure without the managed wrapper.
- Native patch 0015 enters the parent keyframes mutation scope for offset edits, deep-copies keyframe children and reattaches retained declaration wrappers during stylesheet copy-on-write, and invalidates a removed keyframes animation when deleting its stylesheet rule. The permanent raw regression passes 93 assertions with the private patched runtime; official preview.11 fails it.
- The full shared C11 sequence passes 82 assertions on Windows JIT and NativeAOT with the private patched runtime. Native length/indexing are available; invalid keyText is a no-op in the pinned source. Complete shared runner, Tab traversal, cleanup, existing lifetime checks and teardown pass. This is private engine validation, not public preview.12 acceptance.
- The native patch series applies cleanly to the pin and source verification passes with the existing allowed Tools/TestWebKitAPI/CMakeLists.txt exception. That unrelated file was not edited by this lane.
- Existing native CSSOM 407, support queries 72, computed styles 93, fragment rectangles 140 and all 32 generator tests pass against the patched runtime. Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 NativeAOT warnings remain.
- The independently reviewed native repair [PR121](https://github.com/ranvier-org/Ranvier.WebCore/pull/121) merged as97a952ed. Its [five-RID PR CI](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40935420336) and [preview.12 trusted publication](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40948622989) succeeded. All seven official package IDs are indexed; the CI lane independently verified all five RID payload manifests/hashes and ran the official Windows raw93-keyframe regression.
- Root imported the reviewed source from4eb4f560 and plan fromc9136ea8 into the C19/C21 Views batch. The numeric comparison now rejects NaN/infinity through a negated bounded-distance predicate. Focused official Windows JIT/AOT report82, length=true, invalidKeyTextError empty; Chromium156.0.8078.4 reports82, length=true and SyntaxError. Required runner guards preserve these observed profiles.
- Remaining: final shared runner integration review/checks, applicable traced/platform/published Views acceptance with the tracker. This group remains open.
