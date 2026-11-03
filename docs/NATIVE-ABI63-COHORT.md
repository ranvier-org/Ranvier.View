# Managed ABI63 cohort preparation

Core retains reactive scheduling and cleanup. The reviewed SDL surrounding-text bridge and optional AT-SPI lease
keep native tokens, coordinates and window handles in View. This adds no Core ownership, scheduling or reconciliation
primitive and needs no Core Native pin or version bump.

The tested implementation checkpoint is `caf2f145`. Main `6b1119c8` contributes its alpha45 release record and CI
checks without changing any CAF production F# blob. Candidate View/View.Dom versions are `0.1.46`; WebCore/SDL use
`0.1.0-alpha.46`, each normal and traced. Public Core preview.1, DOM alpha.56, CSS alpha.2 and CSS add-ons alpha.1
remain unchanged. The candidate and remaining gates are recorded in `docs/research/native-api-delivery/views-0.1.46-plan.json`.

The default native dependency is allocated P8 `0.1.1-preview.8`, tag source
`b3073f604a4fba7a1a565d2893ff5791c603b759`, ABI63 with 347 declarations. Its header, canonical Native.fs and DOM model
are byte-identical to reviewed da11. All original 344 signatures and surrounding 344/delete 345/style 346 remain
intact. P8 integrates the reviewed manifest/worker-owner/IME source fixes; a source tag does not qualify its produced
runtime or public package graph.

## Isolated compilation

An unpublished interop build at the allocated P8 version may supply declarations while actual producer/public
packages are pending. Build from exact b307 into an owned feed/cache. Keep the older `0.0.0-private347.1` immutable;
use another fresh cache for public acceptance. Do not put private artifacts into a global/public cache or infer
public artifact identity from a private build of the same source.

Use `UseLocalRanvier=false`, `UseLocalRanvierCss=false` and `UseLocalRanvierWebCore=false`. Keep public dependency
minimums in `Directory.Build.props`, without higher direct consumer overrides. Public DOM56 packages have no native
dependency, so they need no new Core version. Select an owned `RestorePackagesPath`/`RestoreConfigFile`, and separate
`--artifacts-path` outputs for `RanvierTrace=false` and `true`.

`RanvierWebCoreNativeDir=<owned-root>` expects `runtimes/<rid>/native/**/*`, with `RanvierWebCoreNativeRid` explicit.
Passing the directory containing SO files directly does not stage them. Record actual Host/SDL hashes and scope.
A build or AOT link is compilation proof until that output executes with verified native assets.

Build SDL/WebCore tests in both modes. Run View's `build.fsx` analyzer pipeline and source-built analyzers over
WebCore/SDL. Publish `AccessibilityAot` without IL warning suppressions. CAF's normal/traced Windows/Linux AOT
compilation, private Linux provider controls and full managed suites remain scoped evidence, not P8 acceptance.

## Optional provider and actual consumers

The optional public ID is `Ranvier.WebCore.Accessibility.Atspi`; its matching P8 version is coordinated separately.
No provider version is selected by default and the renderer has no unconditional GIO dependency. Private qualification
may explicitly override the fixture package ID. Use ordinary PackageReference and origin-relative native loading;
do not add environment library search paths that hide discovery failures.

The cohort graph/consumer guards compare exact source claims, RID, architecture, package graph and runtime hashes
with independent producer receipts. A source label or ABI number does not authenticate a build. Windows UIA success,
absent/admission and teardown controls are distinct from Linux provider-present, module-absent and no-bus controls.
The optional patched SDL IBus package retains its separate upstream pin and runtime identity.

ARM64 is retained active qualification work. On actual P8 ARM producer availability, verify source/ABI/hash/ELF,
build its provider, run real protocol/SDK checks and normal/traced strict NativeAOT Window/Host controls. Its job
stays separately disabled until exact inputs are reviewed. x64 proof does not substitute for ARM execution, and the
ARM publication gap does not create an x64 dependency. Independent runtime/package/consumer acceptance remains required.
