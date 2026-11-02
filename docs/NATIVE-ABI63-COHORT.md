# Managed ABI63 cohort preparation

## Core and view state

The existing core owns reactive scheduling and cleanup. This integration combines the reviewed SDL surrounding-text
bridge and optional AT-SPI window lease; their cached native tokens, coordinates and window handles are view-owned
native state. It adds no core ownership, scheduling or reconciliation primitive.

`tools/native-cohort347/graph.json` records the source/package graph and remaining actual runtime inputs. The View
baseline is PR66 source `3e435b3`, including public Dom alpha.56, CSS alpha.2, separate CSS add-on alpha.1, UIA cleanup and source-fresh analyzer
fixes. Public Dom alpha.56's normal/traced nuspecs depend on FSharp.Core, and its Fable variants additionally depend
on Fable.Core. They do not pin native interop, so this preparation requires no new Core version or Core Native pin.

The SDL bridge directly calls the new surrounding/delete bindings. Compile it against the canonical 347-binding
interop, preserving the original 344 signatures and the surrounding 344/delete 345/style 346 tail. The privately staged
`Ranvier.WebCore` `0.0.0-private347.1` comes from the exact da11 source archive and is unpublished. It is an API/source
checkpoint only. The final native source/version/hash stays unset until the ABI metadata correction, reviewed 0068
worker-first-owner fix and reusable GPU oracle are integrated and a matching producer is qualified. Update the final
source pin and use a new private package version when those inputs change; never overwrite a prior package/cache.

## Isolated compile graph

Use owned NuGet and output directories and an owned feed containing that private interop package. Keep the public
Dom/CSS minimums in `Directory.Build.props`; do not replace them with direct higher consumer overrides. For initial
source compilation, set these properties explicitly in both modes:

- `UseLocalRanvier=false`, `UseLocalRanvierCss=false`, `UseLocalRanvierWebCore=false`.
- `RanvierWebCoreNativeVersion=0.0.0-private347.1`, which selects the private managed interop in this preparation.
- `RanvierWebCoreNativeDir=<owned-empty-directory>` to suppress a mismatched native package during compile-only work.
- `RestorePackagesPath=<owned-cache>`, `RestoreConfigFile=<owned-feed-config>`.
- `RanvierTrace=false` or `true`, with separate `--artifacts-path` directories.

The empty directory supplies no native runtime. A successful build or AOT publish with it is compile/link proof only.
The existing default public native pin remains the release baseline until the coordinator allocates the matching
native cohort; it does not provide these new managed declarations. This branch remains a draft integration while
that public dependency pin is pending. No compatibility with ABI62/P7 is implied by the private compile graph.

Build `src/Ranvier.View.WebCore.Sdl/Ranvier.View.WebCore.Sdl.fsproj` and the WebCore tests for normal/traced source
checks. Run the View analyzer pipeline through `build.fsx` and use its source-built analyzer host for separate WebCore
and SDL checks, preserving current analyzer settings. Publish `tests/AccessibilityAot/AccessibilityAot.fsproj` with
its existing IL warning errors for each actual target. Do not introduce warning suppressions to make a graph pass.

## Matching runtime and optional assets

Before executing a native consumer, replace the compile-only override with the exact selected Slim/GPU native
package graph or a hash-verified native directory. Record the real manifest/source claim, package version, ABI,
architecture, exported declaration set, host hash and flavor. Compare producer evidence independently; a caller
source label alone does not authenticate a binary. A matching interop API does not prove native lifecycle behavior.

The AOT consumer selects the reserved public provider ID `Ranvier.WebCore.Accessibility.Atspi` only when a provider
version is explicitly supplied. A private qualification feed can override the ID. Restore the provider ordinarily
through PackageReference; retain its origin-relative load behavior and avoid environment search paths that hide
asset-discovery failures. Windows uses UIA with the provider absent. Linux must run provider-present, missing-module
and unavailable-bus controls on the same exact normal/traced consumer graph. Keep the optional patched SDL IBus
runtime's package/hash and upstream pin distinct from the renderer/provider.

Actual Windows and Linux x64 native/provider/managed cohort tests remain separate from source preparation. ARM64
requires its matching actual 347 native producer, provider/protocol, real Window/Host and strict NativeAOT evidence;
the existing ARM provider preparation is statically unarmed. No producer dispatch, public version allocation or
runtime acceptance is performed by this managed source preparation.
