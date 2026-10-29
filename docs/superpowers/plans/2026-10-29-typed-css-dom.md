# C04: typed CSS declarations on shared DOM sinks

The full goal remains all98 required and5 conditional groups, with53 queries deferred. C04 reuses the existing Ranvier.Css grammar, property, literal, unit and serialization model. It adds an optional Ranvier.Css.Dom adapter; the core DOM and CSS authoring packages retain their existing dependency direction.

## Exact contract

CSS repository owns src/Ranvier.Css.Dom/{Ranvier.Css.Dom.fsproj,StyleDeclaration.fs,README.md} and src/Fable.Ranvier.Css.Dom/Fable.Ranvier.Css.Dom.fsproj. The companion compiles the identical source with AssemblyName Ranvier.Css.Dom against Fable.Ranvier.Dom. The normal/traced package references the matching Ranvier.Css runtime and Ranvier.Dom alpha.11; generated grammar bindings remain an authoring dependency of the caller. The same F# adapter runs through the shared CSSStyleDeclaration interface in browser and native code; it introduces no native ABI or renderer implementation.

- Ranvier.Css.Dom.StyleDeclaration.apply : CSSStyleDeclaration -> Declaration -> unit. Use Declaration.name/value/isImportant and CSSStyleDeclaration.setProperty; preserve the engine's validation, read-only errors and cascade semantics.
- StyleDeclaration.applyMany : CSSStyleDeclaration -> seq<Declaration> -> unit. Apply in enumeration order, preserving fallback order. It is sequential and does not claim atomic rollback on an exception.
- StyleDeclaration.read : CSSStyleDeclaration -> Declaration -> string * string. The declaration selects its generated property name; return the engine's serialized value and priority. Computed output remains serialized engine data, not a falsely grammar-typed parsed value.
- StyleDeclaration.capture : CSSStyleDeclaration -> StyleSnapshot and StyleDeclaration.restore : CSSStyleDeclaration -> StyleSnapshot -> unit. Opaque snapshot stores the entire serialized declaration. Restoration replaces the entire declaration, including order, shorthands, importance and absence; it deliberately restores the snapshot rather than merging later mutations. No automatic disposal callback or extra reactive owner is introduced.

All functions reject a null style. apply/read reject a null declaration; applyMany rejects a null sequence and validates each declaration at its turn. Restore rejects a null snapshot. Native declaration lifetime/creating-thread checks remain enforced by the shared wrapper. No runtime reflection/property-name dispatch is introduced.

## Source and compatibility evidence

Fresh FCS checks of the existing CSS runtime and generated bindings are clean. FCS public API confirms Declaration.name/value/isImportant/create and existing numeric metadata. Refactor impact reported18 Declaration sites in5 runtime files; its publicness result is incorrect for this union (the public API snapshot explicitly includes it). The implementation adds a separate module/package and does not change Declaration's signature.

Sage RED proves Ranvier.Css.Dom is absent. The existing expression Css.width(Css.px42.) retains width/42px/importance; Css.width(Css.deg20.) is rejected by the compiler, preserving the existing grammar checks.

## Verification and delivery

1. Prove adapter signatures and declaration/numeric metadata in SageFs, then persist the adapter and snapshot.
2. Add the package to the CSS solution/build/pack inventory. Build normal/traced and verify packed Fable source/dependency metadata. Add a focused executable package consumer.
3. Views owns tests/NativeApi.Conformance/TypedStyles.fs. Run the exact same declarations and assertions in Chromium/native Windows JIT/NativeAOT: typed values/units/keywords/custom properties, duplicate declaration order, inline important, computed effects, whole snapshot restoration including shorthand and absence, read-only errors and lifetime.
4. Preserve compiler-negative grammar rejection and numeric metadata assertions. Do not implement deferred Typed OM math/color operations or resume Anime.
5. Review, publish the additive CSS adapter through trusted CI, consume official packages and run the supported platform batch gates before closing C04. Bounded work uses focused checks; full gates run at batch boundaries.

## Progress

Contract fixed; adapter absent RED and grammar mismatch rejection observed. Implementation and runtime acceptance remain pending. Source checkout C:/wk/native-typed-css isolates the CSS work; the native worktree tool rejected this repository because it lies outside the chat's registered workspace, so the ordinary git worktree fallback was used. Existing checkout and its untracked state were preserved.

## Implemented checkpoint

CSS PR2 https://github.com/ranvier-org/Ranvier.Css/pull/2 is revision813490adae7248f2001ccfd9ba4c1ce952ef4b56. All four normal/traced native/Fable adapters build net8/net10 and package identical Fable source. Normal and Fable package dependency identities match their target DOM companion. Both adapters pass fresh FCS checks, public API has2 entities/5 functions, XMLaudit0 and formatting passes. Independent review found no critical/important defects.

Shared fixture39 assertions pass Windows JIT, Windows NativeAOT and real Chromium; the combined shared batch is635 plus Tab traversal. Coverage includes whole snapshots/absence, shorthand order/importance, typed keywords/numeric metadata/custom properties, read-only apply and restore, every public function's null-style precondition, mid-enumeration null and failure preserving earlier writes. Two additional native wrong-thread/disposal tests pass under JIT and NativeAOT, bringing native lifetime checks to17. The first browser compile exposed native/Fable DOM assembly conflict; the source-identical companion corrects the dependency graph and actual browser compilation/execution passes. Existing locked Playwright dependencies were installed with npm ci after --quick correctly skipped setup.

NativeAOT initially rejected the Windows RID because MSBuild OS was empty while NETCoreSdkRuntimeIdentifier was win-x64. Supplying OS=Windows_NT restores the actual host setting; publication/execution then passes. Existing FSharp.Core IL3053/IL2104 and SameValueZero.Normal IL2075 warnings remain. No source compiler workaround or warning-free claim is made.

Core master CI40881215249 and trusted publish40882196652 succeeded; all four official alpha.11 packages download. The task restore configuration now uses those downloaded official packages and a fresh isolated cache; CSS adapters and native interop/runtime are still local. CSS PR CI, trusted adapter publication, official adapter consumption, paired Views acceptance and other-platform shared fixtures remain required. C04 is in progress, not complete.

Official Core alpha.11 isolated normal/traced .NET identity/service and Fable executable consumers all pass, including computed-style dispatch and CSS query overloads. Fresh Native fixture FCS check is clean with zero warnings/errors. Browser fixtures pass635 shared assertions plus Tab and provider-context checks; Windows JIT/NativeAOT pass635 plus Tab and17 lifetime checks. Native runtime and CSS adapters remain local.
