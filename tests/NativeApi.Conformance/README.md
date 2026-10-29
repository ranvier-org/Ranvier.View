# Shared native API conformance

The fixtures execute the same typed assertions through shared DOM contracts on native WebCore and a real Chromium document. Current slice coverage is 477 assertions: DOM and authored View tables, HTML reflection, Area hyperlinks, SVG/MathML members, CSSOM, SVG stylesheets and constructed/adopted sheets. Native teardown and stylesheet lifetime checks run separately.

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

The native runner can also be published with `-r <rid> --self-contained -p:PublishAot=true`. The current shared fixtures also pass as Windows x64 NativeAOT; remaining platform/package acceptance, selected groups and the whole view acceptance suite have separate gates.

`Reflection.fs` contains static typed calls suitable for NativeAOT. Regenerate it with `python tools/generate-native-api-reflection-tests.py <canonical-dom-api.json> tests/NativeApi.Conformance/Reflection.fs`. Its cases verify property/attribute reflection in both directions and live datalist/map collections. The test inventory does not add reflection-based dispatch to the application runtime.
