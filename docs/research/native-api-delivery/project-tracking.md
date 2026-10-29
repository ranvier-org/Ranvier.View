# Shared API acceptance tracking

The shared tracker is [Shared browser/native API delivery](https://github.com/orgs/ranvier-org/projects/1). Its current-goal scope is 98 required `x` groups and five conditional `!` groups. The 53 `?` groups remain deferred to the next goal.

## Agent workflow

Every agent working on this goal must use the existing stable API-group issue on the project board. The tracking issues live in [Ranvier.WebCore](https://github.com/ranvier-org/Ranvier.WebCore/issues); implementation may belong to Ranvier, Ranvier.View, Ranvier.Css or Ranvier.WebCore. The issue body records these ownership boundaries. Do not create duplicate issues for the same group.

Before work, read the issue's complete selected scope, acceptance criteria, recorded evidence and remaining work. Identify the actual implementation repository and branch. Preserve the selection marks and deferred boundaries in the repository task document.

During work, add each delivery PR to the project and set its `API groups` field. Record related PR links on the API issue. A PR item has its own merge status; its completion does not close the group issue. Include the source revision, exact fixture/test counts, target, package configuration and CI/run link in the issue's evidence. Identify local/private runtime results separately from released-package consumption.

Use ordinary related-PR links for partial delivery. Reserve automatic issue-closing references for a change that actually satisfies the complete group acceptance; merging one dependency must not close an outstanding acceptance issue.

After a meaningful change, update the issue's `Evidence`, `Acceptance phase` and remaining work. Keep `Status` as Todo, In Progress or Done. Record a review's actual scope and findings; a read-only reviewer must not claim runtime checks they did not execute.

The acceptance owner audits all explicit group requirements and relevant global gates before closing the issue or setting its group item to Done. A group can close independently when its own acceptance is proven. Unrelated groups and the overall goal do not need to finish first. Do not invent new prerequisites, silently drop selected members or count exported interfaces as behavioral parity.

For A06, A07, A08, A09 and A11, record the actual extension-gate evaluation. Deferral requires evidence that implementation would reintroduce dependencies or require JavaScript. Missing generator support, build cost or an untested feature is implementation/evaluation work, not an accepted conditional blocker.

## Fields and grouping

- `API groups` preserves stable IDs such as C04 or G06 on issues and PRs.
- `Tracking kind` distinguishes API acceptance groups from delivery PRs.
- `Evidence` summarizes verified behavior and exact outstanding acceptance work.
- `Acceptance phase` distinguishes Not started, Conditional evaluation, Implementation, Verification, Release pending and Accepted.
- Repository links on the project connect Ranvier, Ranvier.WebCore, Ranvier.Css and Ranvier.View.
- Issue labels `native-api`, `selection/required` or `selection/conditional`, and `api/<category letter>` support repository filtering and grouping.

Project tables can be grouped by acceptance phase or filtered by category/selection labels. The built-in Repository field on every API issue identifies its tracking home; use the ownership paragraph to find its actual implementation repositories.

`project-items.json` maps the 103 stable group IDs to issue URLs and project item IDs. Its status/phase values are a checkpoint; read the live issue and project before acting. Related PRs may be dependency or stack parents, so verify the actual PR head and changed files before treating a link as implemented or released behavior.

CLI JSON field names preserve their generated spelling: for example, `aPI groups` and `tracking kind`. Do not assume camel-cased `apiGroups` or `trackingKind` keys when verifying writes. Check current `gh project field-list` and `gh project item-list` output.

## Acceptance audit on 2026-10-29

The verified board contains 103 API-group issues, 22 delivery PR items and four linked repositories. API status is 83 Todo, 20 In Progress and zero Done. Acceptance phases are 78 Not started, five Conditional evaluation, nine Implementation, four Verification and seven Release pending. Required/conditional labels match the selected scope exactly: 98 and five.

None of the API groups has complete acceptance evidence. Some manifest state labels and `remaining` notes predate later implementation; the issue audit describes the actual specific gaps instead of treating every started group as release-only work.

- D09 and D10 have shared browser/Windows JIT/AOT behavior and authored View-tag/focus tests. Released backend/runtime consumption and supported-platform shared gates remain.
- D11 has semantic/reflection and hyperlink fixtures. Its old remaining-scope notes need reconciliation against generated wrappers and the original selected-tag scope before released/platform acceptance.
- D12 still needs external link-sheet association and loading completion/error behavior, plus the selected member and Ranvier.Css association audit.
- C01, C02 and C03 have shared behavior and context/lifetime tests. Official preview.10 Windows runtime consumption now passes 72 CSS-support and 93 computed-style assertions with all 24 payload hashes/source/ABI verified. The release run is fully green; paired backend consumption, full RID indexing/consumption and supported-platform shared/AOT gates remain.
- C04 has shared typed-style behavior and official package/cohort checks, including traced Chromium and traced Windows actual-engine execution using official DOM/CSS packages with the private G06 runtime. Official native/runtime and paired backend/platform acceptance remain.
- C05 has shared escape/selector vectors and 196,609 Chromium oracle comparisons, plus official shared-package consumption. Its applicable platform and packaged View gates can close independently of unrelated CSSOM groups.
- C06 has live sheet discovery, metadata, disabled/media layout effects and removal tests. Reconcile exact member scope and verify the released cohort/platform gates.
- C07 has construction, replacement, adoption and lifecycle tests. Review the explicit-base browser profile limit against the selected contract, then complete released/platform gates.
- C08 needs complete nested mutation and invalid syntax/index behavior with actual computed effects.
- C09 needs viewport/container changes that affect computed styles, beyond inspecting condition text and changing a media list.
- C10 needs actual cascade effects and selected editing/inspection across layers, scopes, nested declarations, imports and namespaces, with truthful optional-feature outcomes.
- C11 needs an associated CSS animation to observe edited keyframes. This engine test is selected work; the paused Anime implementation remains paused.
- C18 needs selected font-feature-values and font-palette-values inspection and mutation only where the pinned interfaces support it. The pinned font-feature-values interface exposes fontFamily; upstream feature-map APIs are commented out and must not become invented required wrappers. Font loading is separate from descriptor inspection.
- C19 needs shared custom-counter mutation and actual list-marker output for symbols, range, prefix and fallback.
- C21 needs a constrained anchored element to select the expected fallback, beyond descriptor editing, or evidenced unsupported state.
- C22 needs shared view-transition rule navigation/types inspection. Conditional A11 transition execution is independent.
- G06 passes the same fragment fixture in Chromium (74 assertions) and Windows normal/traced JIT and normal NativeAOT (61 each); line-wrap/font metrics change the number of per-fragment checks. Eight native thread/disposal checks pass and teardown retains zero objects. The full backend suite passes 782 tests with 12 existing skips, and all three analyzer/build groups pass. Views commit e09f8780b9c46b99530943b9adf8050d4368c874 is held locally. Released native/backend consumption and other-platform shared gates remain.

These are closure requirements, not authorization to narrow scope. Update this checkpoint when new evidence changes the next action. Exact issue URLs are recorded in `project-items.json` after the initial draft-to-issue conversion is verified.
