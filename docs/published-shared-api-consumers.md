# Published shared API consumers

The **Published shared API packages** manual workflow runs release fixtures against official
NuGet packages. It complements **Published native packages**, whose default still verifies all
runtime flavors and both trace variants. This shared API gate declares the Windows CPU slim
profile; it does not verify GPU presentation or close the [GPU cohort issue](https://github.com/ranvier-org/Ranvier.WebCore/issues/120).

The `0.1.2` profile pins Views 0.1.2, backend alpha.2, Core DOM alpha.11, native preview.10,
CSS DOM alpha.1 and CSS bindings 0.1.1-alpha.1. Its fixture source is release commit
`a097f8ec448f8974022f8416ccbf3fabf20493a4`. The gate obtains the compile lists, fixtures,
browser runner and tool versions from that commit. Each restored View package must report the
same repository commit. A later release needs a new profile with its own fixture revision,
versions and assertion totals; current development fixtures are never substituted.

Each normal/traced native job executes all 685 shared assertions in JIT and NativeAOT,
including 50 common hyperlink authoring checks, plus 17 lifetime checks, teardown and Tab
traversal. Each browser job compiles the official package sources with Fickl and executes the
same 685 shared assertions in Chromium, plus four provider context checks, Tab traversal and
196,609 CSS identifier oracle comparisons. Native jobs also check five assembly trace markers
and all 24 preview.10 native/resource hashes with ABI21. NativeAOT currently emits FSharp.Core
IL3053/IL2104 and View IL2104 warnings; successful execution is checked independently.

Every job uses a new external working directory, isolated NuGet caches, an official-only source
mapping and package references. It rejects source project references, mixed normal/traced
parents, stale package commits and incomplete assertion output. Artifacts retain source hashes,
the exact profile, restore graph, consumer project and command logs. The native runner adds one
explicit assembly trace precheck before its original entry point; the shared fixture bodies
come unchanged from the release commit.

## Public Fable package selection

`Ranvier.View` supports .NET and Fable and its published dependency graph includes the native
`Ranvier.Dom` package. The native and browser DOM packages intentionally share the
`Ranvier.Dom` assembly identity. A package-only Fable consumer must explicitly select the browser
package and suppress the native package's assets:

```xml
<PackageReference Include="Ranvier.View" Version="[0.1.2]" />
<PackageReference Include="Ranvier.View.Dom" Version="[0.1.2]" />
<PackageReference Include="Ranvier.Dom" Version="[0.1.0-alpha.11]" ExcludeAssets="all" />
<PackageReference Include="Fable.Ranvier.Dom" Version="[0.1.0-alpha.11]" />
<PackageReference Include="Fable.Ranvier.Css.Dom" Version="[0.1.0-alpha.1]" />
<PackageReference Include="Ranvier.Css.Bindings" Version="[0.1.1-alpha.1]" />
<PackageReference Include="Fable.Core" Version="[5.3.0]" />
<PackageReference Update="FSharp.Core" Version="10.1.401" />
```

For traced builds, append `.Traced` to each Ranvier package ID above, including both DOM IDs,
and define `RANVIER_TRACE` in the project and Fickl invocation. `Fable.Core` keeps its package ID.
The gate confirms that the native DOM compile asset is suppressed and exactly one browser
`Ranvier.Dom.dll` is selected. Without this qualification, Fickl can choose the native DOM source
graph, causing missing browser providers and CSS adapter type errors.

```powershell
rtk dotnet fickl Consumer.fsproj --configuration Release --define RANVIER_TRACE --outDir generated --noPackageCache
```

## Local execution

Use a clean checkout with the release commit available in its Git object database. Choose an
output path outside the checkout that does not yet exist; the tool refuses existing output
directories so cache provenance remains observable. The native profile requires Windows x64
with NativeAOT build prerequisites. Browser runs use Node.js and the locked Playwright package.

```powershell
rtk python tools/check-published-shared-api.py --profile tests/native-packages/shared-api-0.1.2.json --output C:/wk/shared-api-normal-native --variant normal --target native
rtk python tools/check-published-shared-api.py --profile tests/native-packages/shared-api-0.1.2.json --output C:/wk/shared-api-traced-browser --variant traced --target browser
```

The initial public cohort passed [trusted publication](https://github.com/ranvier-org/Ranvier.View/actions/runs/40927498585)
and [normal/traced slim smoke, visible counter and accessibility acceptance](https://github.com/ranvier-org/Ranvier.View/actions/runs/40930899252).
The full package-only fixture independently passed all six normal/traced JIT, NativeAOT and
Chromium executions. Behavioral scope beyond these assertions remains in the
[acceptance project](https://github.com/orgs/ranvier-org/projects/1).
