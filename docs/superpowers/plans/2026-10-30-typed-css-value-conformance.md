# Typed CSS value shared conformance

## Selected scope

C14 exposes the pinned engine's `CSSStyleValue.parse`/`parseAll`, serialization, keyword values, image identity, unparsed string/reference segments, and variable-reference names/fallbacks. A numeric base and unit value provide the minimum representation required by parsing and typed maps. C15 arithmetic and C17 color operations remain deferred. Native parsing uses a checked document handle; browser parsing uses the supplied document's active realm. No native JavaScript is introduced.

The authoritative producer design is Core's `docs/superpowers/plans/2026-10-30-typed-css-value-parsing.md`. The approved service returns actual generated interfaces. Provider disposal leaves values usable; native host/session disposal invalidates their handles. `None` and `Some null` both express a nullable fallback. Null arrays, union containers and reference arms are rejected; a null string uses WebIDL text `null`.

## Bounded implementation

- [x] Add the shared service and browser provider using the selected realm, USV conversion, actual constructors and context validation.
- [x] Append native value handle kinds without renumbering existing kinds. Preserve wrapper identity for fallback/reference results and actual selected parsed subtypes.
- [x] Extend the backend generator using existing interface-alias and transport metadata. Length-delimited inputs/results preserve embedded NUL; old string transports remain unchanged.
- [x] Regenerate wrappers after independent generator review. There are 125 classes, 1,723 mapped members, 13 handwritten members and zero unmapped members. Every pre-existing object block is unchanged; seven value wrapper blocks are added.
- [x] Add shared parser/list order, opaque serialization, nested fallback identity, factory/setter, invalid input recovery, finite unit, USV conversion and disposal cases.
- [x] Add native thread, null/reference/session validation and retained-wrapper host-disposal cases.
- [x] Add browser navigation/context and retained-value lifetime proof.
- [x] Resolve the actual generated Fable reference-setter null conversion defect and maximum-index property escape; preserve both append-at-length overloads.
- [x] Run focused Windows JIT, NativeAOT and Chromium with exact count guards and teardown checks: 99 shared assertions each, 19 native lifetime checks each and three browser context checks.
- [x] Run the combined native/browser batch once after focused providers are ready; audit XML/comments and independently review the final provider/fixture/wiring. Native JIT/AOT each pass 1,628 group plus 54 lifetime checks and Tab/teardown. Chromium passes 1,641 group plus ten context checks, the 196,609-case escape oracle and Tab.
- [ ] Integrate the final canonical producer source pins, then coordinate platform CI and released normal/traced consumers with the parent/release lane.

## Current evidence and qualifications

The initial canonical Fickl 0.6.9/Fable 5.24 compilation passes. A literal unmatched-surrogate string exposed an output-writer limitation; the fixture constructs the exact UTF-16 code unit at runtime instead. Fable cannot type-test erased generated interfaces (`FABLE0112`), so subtype observation uses real native interface membership and actual browser `instanceof` callbacks.

The coherent private Native14 candidate passes the final 99 shared assertions and 19 native lifecycle assertions on Windows JIT and NativeAOT, with zero wrapper teardown. Chromium passes the same 99 shared assertions plus three navigation/context checks. The AOT executable builds with the existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 warnings.

The expanded browser fixture first correctly failed when a null typed reference setter silently became a string through raw WebIDL union conversion. Canonical Core commit `1831d1c3` adds a null and actual reference-brand guard. A second observed failure occurs at `uint32.MaxValue`, where JavaScript creates an ordinary property instead of invoking the indexed setter. Pinned `CSSUnparsedValue.cpp` rejects an index greater than segment count and appends at equality; canonical commit `5c0fbb68` closes the maximum-index escape while retaining ordinary engine validation. Final shared proof checks both string/reference rejection, unchanged value/length, and successful append-at-length identity. Full/focused runners require 99 shared assertions, 19 native lifetime assertions, and three browser context assertions before PASS markers.

Complete outputs and exact private cohort/runtime hashes are in `docs/research/native-api-delivery/typed-css-values-runtime-evidence.json`. The combined normal source batch passes on all three targets. Other provider profile differences, including C10 import security and C11 invalid key text, remain explicit in the complete output rather than being flattened into parity.

The first copied private stage contained a JavaScriptCore DLL that did not match the cached import library. Its three DLL hashes are preserved in ignored `artifacts/c14-first-stage-hashes.json`; that stage is not used for final proof. A fresh `artifacts/c14-coherent-stage` uses the parent-verified matching JavaScriptCore SHA256 `95149cdcba0751d0960786872635a7f3ec17771fd5281a6f06bac75d9ec84d42`. The private managed packages and package cache are isolated from C12 and public caches. No Native14 package release, other-platform acceptance, full combined acceptance or completed C14 claim is made here.
