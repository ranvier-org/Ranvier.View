# Native API delivery acceptance evidence

## Initialization and native table generation

- Required scope: 98 x groups; conditional scope: A06, A07, A08, A09, A11; 53 ? groups deferred to the next goal.
- Fresh remote inspection: no open PRs in Ranvier, Ranvier.Views or Ranvier.WebCore at initialization.
- Work branches: Views codex/native-api-delivery; Native codex/native-api-dom-expansion. Existing Core worktree codex/dom-style-contract is left untouched.
- Generator baseline: 13 tests passed.
- D10 RED: the new configured-table conformance test failed because rv_dom_html_table_element_insert_row was absent.
- D10 GREEN: the configured-table conformance test passed after exposing six table interfaces.
- Generator batch: 14 tests passed, 0 failed; generated native surface grew from 62 interfaces/1070 functions to 68 interfaces/1174 functions, adding 104 operations.
- The generated C++ glue compiles against the pinned WebCore headers/PCH using the existing Windows toolchain (exit 0); the output is isolated under the Native worktree's artifacts/native-api-probes directory.
- Runtime RED against the shipped preview.7 package fails because rv_dom_html_table_element_get_rows is absent. This confirms the runtime fixture exercises the new exported surface.
- Native runtime GREEN: the new fixture passed 44 assertions against privately linked Windows x64 WebCore/host libraries. It verifies live table/section/cell collections, row/cell mutation, caption creation, row indices, cell/column span reflection, invalid-index rejection and retained-handle release.
- The expanded generated shared contracts compile for net10.0 and netstandard2.1 with zero warnings/errors.
- All four shared alpha.3 packages (normal/traced, native/Fable) build for net10.0 and netstandard2.1. Isolated .NET identity/member consumers and Fable member-access consumers pass for both variants. Fresh Core FCS check is clean, and the canonical generator check matches. Six table interfaces expose 62 declared public members; see d10-public-api.json.
- Generated native wrappers map all members of all six table classes. The backend builds with zero warnings/errors against the local alpha.3 packages and updated canonical interop.
- The same F# table fixture passes 30 assertions on native WebCore Windows x64 and real Chromium, including live collections, editing, reflected spans, captions/sections and invalid indices. Native host teardown reports zero retained managed objects.
- The NativeAOT Windows x64 executable passes the same 30 assertions and teardown check. Initial execution exposed an AOT-incompatible generic printf in the test runner; replacement with Console.WriteLine fixes the runtime failure. Publish still reports aggregate IL3053/IL2104 warnings from FSharp.Core; these are not claimed clean.
- Local AOT invocation requires OS=Windows_NT because the tool process environment leaves MSBuild's OS property empty; this corrects host detection rather than suppressing an unsupported-compilation check.
- D10 remains incomplete: existing View-tag construction, other-platform shared fixtures, batched full acceptance and published-package integration remain to verify.
- Delivery PRs: https://github.com/ranvier-org/Ranvier.WebCore/pull/7 (native exports), https://github.com/ranvier-org/Ranvier/pull/252 (shared packages), https://github.com/ranvier-org/Ranvier.View/pull/7 (Views delivery). Native run 40816842079 is live; its package-contract job passed. Core run 40818127843 passed packages/Fable/AOT/contracts but found stale docs-map browser annotations; a targeted fix is in progress.
- Generated public parameter signatures for all 1070 baseline functions are preserved by a structural comparison; new APIs are additive.

- Repeatable build.fsx native-api/browser actions are wired into Windows x64/ARM64, macOS and Linux native lanes plus a Chromium lane. Browser action passes locally. The draft Views stack requires Core alpha.3 and Native preview.8 publication before official-package CI can pass; these versions are not yet published.
- Core docs-map stale-event fix passes all 106 traced tests locally; the follow-up commit is pushed to PR #252 for CI verification.
