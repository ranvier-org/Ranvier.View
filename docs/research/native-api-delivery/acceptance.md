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
- Shared Core package/Views wrappers, browser parity, AOT and other-platform acceptance are not yet proven. D10 remains incomplete.
- Generated public parameter signatures for all 1070 baseline functions are preserved by a structural comparison; new APIs are additive.
