# Shared native API conformance

The fixtures execute the same typed assertions through shared DOM contracts on native WebCore and a real Chromium document. Current slice coverage is 995 WebCore assertions and 1,006 Chromium assertions: DOM and authored View tables/hyperlinks, HTML reflection, Area hyperlinks, SVG/MathML members, CSSOM and nested rule mutation, SVG stylesheets, constructed/adopted sheets, CSS identifier escaping, document-scoped CSS support queries, live computed styles/pseudo-elements, typed Ranvier.Css declaration sinks, fragment rectangles, font/transition rules and conditional rules. Host monospace fonts produce different line counts in the geometry fixture; each engine must satisfy the same geometry invariants. The font-width alias is explicitly available on WebCore and unavailable on the tested Chromium. Chromium additionally compares 196,609 cases with its native CSS.escape implementation. Native teardown and 25 stylesheet/query/computed-style/typed-style/geometry lifetime checks run separately, together with real Tab traversal. Chromium also checks four invalid document contexts.

From the repository root, with the matching DOM and native packages published:

```text
dotnet fsi build.fsx -- native-api
dotnet fsi build.fsx -- native-api-browser
```

The equivalent individual steps are:

```text
dotnet run --project tests/NativeApi.Conformance/Native.fsproj -c Release
dotnet restore tests/NativeApi.Conformance/Browser.fsproj -p:Configuration=Release
dotnet fickl tests/NativeApi.Conformance/Browser.fsproj --outDir tests/NativeApi.Conformance/generated --configuration Release
npm ci --prefix tests/NativeApi.Conformance
npm exec --prefix tests/NativeApi.Conformance -- playwright install chromium
node tests/NativeApi.Conformance/browser.mjs
```

For development, the native runner accepts the backend's existing `UseLocalRanvierWebCore`, `RanvierWebCoreRoot` and `RanvierWebCoreNativeDir` properties. Use an isolated restore configuration for unpublished contract packages; do not place local package overrides in committed configuration. `NATIVE_API_BROWSER_PACKAGE` optionally identifies an existing package.json with the pinned Playwright installation.

Let Fickl perform its own browser restore. Using `--noRestore` after an ordinary native restore can combine native and Fable DOM assemblies and hide browser providers.

`FragmentRectangles.fs` verifies retained Element/Range line fragments, nullable indexing, repeated item identity, fractional coordinates, bounding union, collapsed-text caret geometry and snapshots after resize/removal. Native checks also reject list/item/rectangle reads from the wrong thread and after host disposal. These are geometry invariants within each engine, rather than golden measurements from an identical bundled font.

`FontTransitionRules.fs` verifies parsed font-face descriptors/aliases, feature-family mutation preserving complete definitions, readonly palette metadata and rule replacement, starting-style child editing, and view-transition navigation/types. It records actual engine capabilities and serialized values; rule inspection does not claim font loading or transition execution.

`ConditionalRules.fs` performs 44 assertions for media/supports/container rules, media-list mutation and errors, and actual computed effects after viewport/container changes. It restores the original viewport and verifies rejected insertion preserves rule text, child identity and computed style. `ViewHyperlinks.fs` performs 50 real-engine assertions for common href/hash authoring and URL getters on Anchor/Area through both View authoring APIs; other URL setters have compiler coverage rather than runtime assertions in this fixture.

`RuleMutations.fs` performs 131 assertions for nested grouping and style-rule insert/edit/delete operations with live width/height effects. Syntax, index and hierarchy errors preserve complete rule text, counts, every sibling identity and four computed dimensions. It verifies default insertion index zero, nullable indexing, detached retained declarations, successful no-op CSSRule.cssText assignment and silent invalid-selector rejection according to the pinned source profile.

The native runner can also be published with `-r <rid> --self-contained -p:PublishAot=true`. The current shared fixtures pass as Windows x64 JIT/NativeAOT with official Core alpha.12, CSS adapter alpha.1 and WebCore preview.11 dependencies, without private runtime/source overrides. Views itself remains local source for this batch; traced/platform and published paired backend acceptance have separate gates. Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 analysis warnings remain recorded.

`Reflection.fs` contains static typed calls suitable for NativeAOT. Regenerate it with `python tools/generate-native-api-reflection-tests.py <canonical-dom-api.json> tests/NativeApi.Conformance/Reflection.fs`. Its cases verify property/attribute reflection in both directions and live datalist/map collections. The test inventory does not add reflection-based dispatch to the application runtime.
