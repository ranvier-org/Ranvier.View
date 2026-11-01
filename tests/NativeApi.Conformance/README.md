# Shared native API conformance

The fixtures execute the same typed assertions through shared DOM contracts on native WebCore and a real Chromium document. The integrated Core18/Views8 candidate passes 3,293 shared checks on Windows JIT and NativeAOT, and 3,306 in Chromium. The H02 slice contributes 180 shared checks and a separate 181-check deterministic run. Native H02 lifetime/frame-clock/concurrency coverage adds 25 checks and 500 duplicate-name marks; its native teardown count is zero. Earlier groups retain their own lifetime, context, CSS.escape oracle and real Tab-traversal checks. Current proof is a private Native17-backed candidate; official package and other-platform acceptance remain separate.

From the repository root, with the matching DOM and native packages published:

```text
dotnet fsi build.fsx -- native-api
dotnet fsi build.fsx -- native-api-browser
```

The equivalent individual steps are:

```text
dotnet run --project tests/NativeApi.Conformance/Native.fsproj -c Release
dotnet restore tests/NativeApi.Conformance/Browser.fsproj -p:Configuration=Release
dotnet fickl tests/NativeApi.Conformance/Browser.fsproj --outDir tests/NativeApi.Conformance/generated --extension .js --configuration Release
npm ci --prefix tests/NativeApi.Conformance
npm exec --prefix tests/NativeApi.Conformance -- playwright install chromium
node tests/NativeApi.Conformance/browser.mjs
```

For development, the native runner accepts the backend's existing `UseLocalRanvierWebCore`, `RanvierWebCoreRoot` and `RanvierWebCoreNativeDir` properties. Use an isolated restore configuration for unpublished contract packages; do not place local package overrides in committed configuration. `NATIVE_API_BROWSER_PACKAGE` optionally identifies an existing package.json with the pinned Playwright installation.

Let Fickl perform its own browser restore. Using `--noRestore` after an ordinary native restore can combine native and Fable DOM assemblies and hide browser providers.

Browser candidate runs consume privately packed Fable DOM contracts with `UseLocalRanvier=false`; native and analyzer runs use the Core source checkout. This keeps the browser's packaged contract identity separate from native source project references. The timing browser fixture also runs 11 actual document-navigation/interoperability checks in CI:

```text
rtk proxy dotnet fickl tests/UserTiming/Browser.fsproj --outDir tests/UserTiming/generated --extension .js --configuration Release
rtk proxy node tests/UserTiming/browser.mjs
```

`UserTiming.fs` checks entry ordering, explicit timestamps, endpoint/duration combinations, duplicate names, copied portable details, malformed inputs, error precedence and clears. Native providers share the existing managed host frame/timer clock and a fixed Unix-millisecond origin captured at host construction. Browser providers delegate to the active document realm's real Performance object. Correspondence with native DOM Event.timeStamp remains unverified and is not claimed. Exact source, executed-file and per-run payload hashes are in `docs/research/native-api-delivery/user-timing-runtime-evidence.json`.

`FragmentRectangles.fs` verifies retained Element/Range line fragments, nullable indexing, repeated item identity, fractional coordinates, bounding union, collapsed-text caret geometry and snapshots after resize/removal. Native checks also reject list/item/rectangle reads from the wrong thread and after host disposal. These are geometry invariants within each engine, rather than golden measurements from an identical bundled font.

`FontTransitionRules.fs` verifies parsed font-face descriptors/aliases, feature-family mutation preserving complete definitions, readonly palette metadata and rule replacement, starting-style child editing, and view-transition navigation/types. It records actual engine capabilities and serialized values; rule inspection does not claim font loading or transition execution.

`CascadeRules.fs` verifies actual normal/important layer priority and order replacement, bounded/implicit scopes, post-nesting declarations, namespace-qualified matching and imported stylesheet effects. It passes 91 assertions on Windows JIT/NativeAOT and 93 on Chromium. Both providers support scopes/nested declarations. WebCore rejects inspection of data-imported sheet contents with `SecurityError`; Chromium permits inspection and mutation. Both verify import metadata/identity, media changes, URL replacement and cleanup. Import URL replacement uses whole stylesheet text because live insertion around a leading layer statement has different hierarchy restrictions in these engine versions.

`ConditionalRules.fs` performs 44 assertions for media/supports/container rules, media-list mutation and errors, and actual computed effects after viewport/container changes. It restores the original viewport and verifies rejected insertion preserves rule text, child identity and computed style. `ViewHyperlinks.fs` performs 50 real-engine assertions for common href/hash authoring and URL getters on Anchor/Area through both View authoring APIs; other URL setters have compiler coverage rather than runtime assertions in this fixture.

`RuleMutations.fs` performs 131 assertions for nested grouping and style-rule insert/edit/delete operations with live width/height effects. Syntax, index and hierarchy errors preserve complete rule text, counts, every sibling identity and four computed dimensions. It verifies default insertion index zero, nullable indexing, detached retained declarations, successful no-op CSSRule.cssText assignment and silent invalid-selector rejection according to the pinned source profile.

The native runner can also be published with `-f net10.0 -r <rid> --self-contained -p:PublishAot=true`. The older Core alpha.12/WebCore preview.11 run is historical. The current candidate uses Core DOM alpha.18, Views 0.1.8/backend alpha.8 and the coherent private Native17 stage. Both NativeAOT outputs are actual native PE executables with empty CLR directories; a successful publish that produces only a managed directory is insufficient. Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 analysis warnings remain recorded. Fresh official normal/traced packages and supported-platform consumption remain acceptance gates.

`SvgShapes.fs`, `SvgText.fs`, `SvgViewSelection.fs` and `SvgAnimations.fs` cover S03, S05, S06 and S07: shape lengths and points, authored path data, real Boolean/Integer values, Ahem text metrics with painted glyph centers, standalone SVG views and SMIL controls. Each engine passes 194 shape, 210 text and 31 animation checks. Native passes 126 view checks plus 7 host-selection argument checks, and Chromium passes 115 plus 3 `requestView` context checks; the native root exposes `currentView`/`useCurrentView`, which Chromium reports `NotExposed`. `SvgDocumentLifecycle.fs` adds 49 native checks for SVG document replacement, retained values, scripts-disabled loading and teardown. `--svg-surfaces-only` runs these groups alone in both runners. Recorded provider differences: Chromium keeps an edited active view until reselection, while WebCore applies the edit at once; a cleared view stays inactive after its element is edited in both. `WebCoreHost.SelectSvgView` takes fragment text without `#`, and the browser hash request accepts either form. WebKit `currentScale` changes page zoom and leaves content CSS size unchanged, while Chromium zooms the SVG content. A new begin on a frozen SMIL animation without an `end` attribute takes effect during a paused seek in Chromium only; Native20 WebKit rejects the interval after a replayed `endElement` time, contrary to the SMIL 3 interval pseudocode, and Native21 patch 0032 corrects it. `Some null` path data removes the attribute on both engines, as `None` does.

`SvgPaintServers.fs`, `SvgFilters.fs`, `SvgClipping.fs` and `SvgResources.fs` cover S08–S11 against each engine's painted pixels: gradient, stop and pattern coordinates, colors, transforms and `href` inheritance; filter regions and primitive parameters for color matrix, component transfer, lighting, blur and offset, plus every primitive's standard attributes; clipPath, mask and marker units, geometry and orientation; and `use`, `image`, `foreignObject`, `a`, `style`, `switch`, title/desc/metadata and script reflection, with image load and error events. Both engines pass 49, 118, 38 and 45 checks on Native21. `--svg-paint-only` runs them alone. Native21 WebKit keeps one empty string in `requiredExtensions` after the attribute is removed, where Chromium empties the list; patch 0034 corrects it in a later native release. Measured in both engines: an already-rendered `switch` keeps its selected child after a condition changes, `setStdDeviation` leaves the `stdDeviation` attribute unwritten, and a failed image paints a placeholder. On Native21 the S07 frozen-animation restart now matches Chromium (patch 0032).

`Reflection.fs` contains static typed calls suitable for NativeAOT. Regenerate it with `python tools/generate-native-api-reflection-tests.py <canonical-dom-api.json> tests/NativeApi.Conformance/Reflection.fs`. Its cases verify property/attribute reflection in both directions and live datalist/map collections. The test inventory does not add reflection-based dispatch to the application runtime.
