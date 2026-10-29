# Shared native API conformance

`Tables.fs` and `Reflection.fs` execute the same D10/D11 assertions through packaged shared DOM interfaces on native WebCore and a real Chromium document. Native teardown also checks that the host released its managed native objects.

From the repository root, with the matching DOM and native packages published:

```text
dotnet fsi build.fsx -- native-api
dotnet fsi build.fsx -- native-api-browser
```

The equivalent individual steps are:

```text
dotnet run --project tests/NativeApi.Conformance/Native.fsproj -c Release
dotnet restore tests/NativeApi.Conformance/Browser.fsproj
dotnet fickl tests/NativeApi.Conformance/Browser.fsproj --outDir tests/NativeApi.Conformance/generated --configuration Release
npm ci --prefix tests/NativeApi.Conformance
npm exec --prefix tests/NativeApi.Conformance -- playwright install chromium
node tests/NativeApi.Conformance/browser.mjs
```

For development, the native runner accepts the backend's existing `UseLocalRanvierWebCore`, `RanvierWebCoreRoot` and `RanvierWebCoreNativeDir` properties. Use an isolated restore configuration for unpublished contract packages; do not place local package overrides in committed configuration. `NATIVE_API_BROWSER_PACKAGE` optionally identifies an existing package.json with the pinned Playwright installation.

The native runner can also be published with `-r <rid> --self-contained -p:PublishAot=true`. These fixtures cover table behavior and HTML reflection; the remaining selected groups and whole view acceptance suite have separate gates.

`Reflection.fs` contains static typed calls suitable for NativeAOT. Regenerate it with `python tools/generate-native-api-reflection-tests.py <canonical-dom-api.json> tests/NativeApi.Conformance/Reflection.fs`. Its cases verify property/attribute reflection in both directions and live datalist/map collections. The test inventory does not add reflection-based dispatch to the application runtime.
