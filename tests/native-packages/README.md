# Published native package acceptance

Run the **Published native packages** manual Actions workflow after a coordinated release exists on
NuGet. Supply `view_package_version` for the exact published backend and SDL version, including their
`.Traced` variants. `native_runtime_version` defaults to `0.1.0-preview.7`; managed interop, the native
meta-package, all five RID packages and the Windows GPU package must be from that same release.
An absent package or mismatched cohort fails acceptance.

The four Windows jobs cover normal/traced packages with CPU (`slim`) or strict GPU presentation. They
restore into separate fresh caches using only the official NuGet source. Both projects link the
existing smoke/counter programs and reference packages; their local build files isolate them from
the repository's source-reference and native-stage settings. The counter keeps the assembly identity
required by SDL's friend assembly contract.

GPU fixtures exclude every asset of the direct slim Windows package and select the GPU package at
the same exact version. The five-RID meta-package remains in the dependency graph. No runtime files
are downloaded separately or copied over a build output.

For each JIT and NativeAOT output, `tools/verify-published-native-runtime.py` checks the restore graph,
official cache provenance, clean source commit, WebKit commit, ABI 21, version, RID and flavor. It
hashes every native/resource file in both the selected package and the final output. GPU and slim
manifests must identify the same release cohort. Native graphs reject `Xantham.*` and
`Fable.Ranvier*`; `Fable.Core` compile attributes remain permitted and contracts resolve `Ranvier.Dom`.

Both execution modes run the expanded smoke and the actual counter window with an independently
built OS accessibility probe. GPU jobs also require shared GPU presentation. The workflow requires
real package versions and performs no publication.

The hermetic verifier tests do not contact NuGet or execute native code:

```powershell
rtk proxy python -I tests/native-packages/test_verify_published_native_runtime.py
```

These tests exercise 317-file CPU and 319-file GPU fixtures, including corruption, missing resources,
mixed cohorts, active slim assets, private caches and browser-binding dependencies. Passing them or
evaluating the MSBuild fixtures does not establish that a published release runs; the manual workflow
provides that evidence after publication.
