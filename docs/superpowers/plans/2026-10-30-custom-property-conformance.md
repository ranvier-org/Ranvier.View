# C12 custom-property registration and rule inspection

## Core and view state

- Core primitives used: the shared Ranvier.Dom registration contract; the existing WebCore host/session and InputScope lifetime checks.
- View-owned state: one provider disposal flag. Registrations belong to the engine's document registry and are never mirrored or unregistered by the facade.

## Scope

Implement CustomProperties.forHost and generated readonly CSSPropertyRule wrappers against Native ABI22/preview.14 and Core alpha.14. The browser provider uses its existing document's current Window. CustomPropertyInitialValue.Omitted and Value preserve absence, empty, null and NUL through the Fable compiler; Value null follows WebIDL text-null conversion.

The Native implementation uses explicit byte lengths and pins through the synchronous call. It validates the borrowed host/document on every call, wraps engine exceptions as CustomPropertyException and forbids further registration after disposal. Disposing a provider does not unregister document definitions.

## Required proof

- [x] Duplicate registration, invalid names/syntax/initial values and computationally dependent initial values retain exact engine exception names.
- [x] Omitted, empty, explicit null, embedded NUL and Unicode inputs retain their selected semantics.
- [x] Initial and inherited/noninherited custom properties affect actual matching-node dimensions.
- [x] A registered length interpolates at a deterministic paused midpoint while the unregistered control remains discrete.
- [x] Inline priority changes are restored with their original value and priority.
- [x] CSSPropertyRule exposes readonly name/syntax/inherits/nullable initialValue, retained identity and descriptors after removal.
- [x] Native host/thread/session invalidation and provider disposal are checked; document registrations survive provider disposal. Individual DOM wrappers do not implement IDisposable; the public session disposal releases their handles.
- [x] Actual Fable old-document navigation cannot register into a replacement document.
- [x] Focused Windows JIT, NativeAOT and Chromium run the shared fixture.
- [ ] Combined default-suite CI, all applicable platform profiles and official-package consumption remain coordinated with root.

## Dependency and evidence qualifications

Native preview.14 is initially stacked on Native123 (context/counter preview.13) and canonical Native122. Core generation preserves alpha.13 named-getter emits. Private source/candidate checks are not public-package acceptance. Version/publication coordination remains with root; frozen existing profiles are unchanged.

The final focused shared fixture passes50 assertions on Windows JIT, Windows NativeAOT and Chromium156.0.8078.4. Native lifecycle10 and browser document-context3 pass, with exact count guards before PASS markers in focused and full modes. Teardown is clean. Fable5.13 compiled the actual browser facade using locally packed candidate Core14 packages through an isolated feed/cache; initial source-reference mixing produced duplicate DOM identities through the CSS adapter, resolved by package consumption and a fresh compiler cache.

The priority restoration test exposed a preexisting native CSSOM defect on released preview.11: important custom properties ignored explicit normal-priority assignments. Reviewed Native patch0018 changes only the single-custom-property setter parse path; the private candidate passes231 raw assertions including registered/unregistered changed/same-value importance removal, invalid priority/tokens and unchanged stylesheet cascade precedence. CSS supports72 and computed-style93 controls also pass. All19 patches apply cleanly to the pinned source. Native producer commitf18d943cebfad3f348bc818c4f02433f68e21aed includes the repair.

Generator35 tests and an exact additive-contract comparison preserve1702 prior function records and15 prior structures, adding exactly5 exports. Managed generation reports120 classes,1723 members and zero unmapped members. Fresh Native FCS and XML/comment audits are clean. AOT publication reports existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 warnings; this work does not claim a warning-free whole backend.

Commands: `dotnet .../Native.dll --custom-properties-only`; `dotnet publish tests/NativeApi.Conformance/Native.fsproj -c Release -r win-x64 -p:PublishAot=true -p:OS=Windows_NT -o artifacts/c12-aot`, followed by `artifacts/c12-aot/Native.exe --custom-properties-only`; and `node tests/NativeApi.Conformance/browser.mjs --custom-properties-only` after compiling Browser.fsproj. These are focused proofs, not the default-suite acceptance gate. Public packages, other platform profiles and the combined release remain unverified.

After forward-merging the parent's reviewed C11/C19/C21 source, the default suite passes 1529 group assertions plus 35 lifetime assertions and Tab on Windows JIT and NativeAOT. Chromium passes 1542 group assertions, 7 context assertions, 196609 escape-oracle comparisons and Tab with installed repo-local Fickl 0.6.9/Fable 5.24.0. The older global Fable 5.13 focused C12 proof remains extra compatibility evidence; its default run fails an existing BCL exception catch, so it is not the canonical default-suite compiler result. No existing assertion was weakened. Exact commands, untruncated outputs, candidate hashes and remaining release qualifications are recorded in `docs/research/native-api-delivery/custom-properties-runtime-evidence.json`.
