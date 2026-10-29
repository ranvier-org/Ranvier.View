# Shared API acceptance tracking

The shared tracker is [Shared browser/native API delivery](https://github.com/orgs/ranvier-org/projects/1). Its current-goal scope is 98 required `x` groups and five conditional `!` groups. The 53 `?` groups remain deferred to the next goal.

## Agent workflow

Every agent working on this goal must use the existing stable API-group issue on the project board. The tracking issues live in [Ranvier.WebCore](https://github.com/ranvier-org/Ranvier.WebCore/issues); implementation may belong to Ranvier, Ranvier.View, Ranvier.Css or Ranvier.WebCore. The issue body records these ownership boundaries. Do not create duplicate issues for the same group.

Before work, read the issue's complete selected scope, acceptance criteria, recorded evidence and remaining work. Identify the actual implementation repository and branch. Preserve the selection marks and deferred boundaries in the repository task document.

During work, add each delivery PR to the project and set its `API groups` field. Record related PR links on the API issue. A PR item has its own merge status; its completion does not close the group issue. Include the source revision, exact fixture/test counts, target, package configuration and CI/run link in the issue's evidence. Identify local/private runtime results separately from released-package consumption.

Use ordinary related-PR links for partial delivery. Reserve automatic issue-closing references for a change that actually satisfies the complete group acceptance; merging one dependency must not close an outstanding acceptance issue.

After a meaningful change, update the issue's `Evidence`, `Acceptance phase` and remaining work. Keep `Status` as Todo, In Progress or Done. Record a review's actual scope and findings; a read-only reviewer must not claim runtime checks they did not execute.

Preserve accepted groups during later imports or bulk updates. Initial seeding scripts and old manifest snapshots are not authority to reset a completed issue or its Accepted phase; reopening requires new contradictory evidence and an explicit recorded reason.

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

The verified board contains 103 API-group issues, 22 delivery PR items and four linked repositories. API status is 83 Todo, 19 In Progress and one Done (C05). Acceptance phases are 78 Not started, five Conditional evaluation, six Implementation, six Verification, seven Release pending and one Accepted. Required/conditional labels match the selected scope exactly: 98 and five.

C05 has complete acceptance evidence for its Core-only scope. Every other incomplete group remains open. Some manifest state labels and `remaining` notes predate later implementation; the issue audit describes the actual specific gaps instead of treating every started group as release-only work.

- D09 and D10 have shared browser/Windows JIT/AOT behavior and authored View-tag/focus tests. All official preview.10 runtime payloads verify. Views PR7's exact2a0e719 CI is now fully green, including supported-platform shared/JIT/AOT and package lanes. Group-specific release/public-consumption acceptance is still audited independently.
- D11 now has 50 shared authored Anchor/Area assertions in PR7 commit2a0e71945f8abde21f7f75e3945d5b4b27110233. Official Corealpha11/nativepreview10 Windows JIT and Chromium each pass 685 aggregate shared assertions, plus existing context/lifetime/Tab/teardown checks. The typed native receiver facades restore common hash/hashStatic/templatecommon.hash without losing browser direct access, readonly origin or explicit unsupported-receiver errors. Generator checks and five focused compiler cases pass. Exact-head CI40923137860 is fully green; its logs verify the 50 authoring assertions on all four native JIT/AOT targets and Chromium. Complete published Views backend acceptance and the full selected-scope reconciliation before closure. Private G06/Core12 AOT proof remains separate.
- D12 still needs external link-sheet association and loading completion/error behavior, plus the selected member and Ranvier.Css association audit.
- C01, C02 and C03 have shared behavior and context/lifetime tests. All seven official preview.10 packages now download, and all five native RID payloads verify against hashes, clean source99d5a772, CI builder and ABI21. Actual official Windows runtime execution passes 72 CSS-support, 93 computed-style and 407 CSSOM assertions. Paired Views package consumption and supported-platform shared/AOT gates remain.
- C04 has shared typed-style behavior and official package/cohort checks, including traced Chromium and traced Windows actual-engine execution using official DOM/CSS packages with the private G06 runtime. Official native/runtime and paired backend/platform acceptance remain.
- C05 is Accepted: its public Core-only facade passes 35 serialization vectors and ten real selector round trips under JIT and NativeAOT on all four required native targets. Chromium passes the same 45 assertions plus 196,609 oracle comparisons. Official normal/traced native/Fable Core consumers pass. A newly published Views backend is not a dependency of this function; the earlier tracking requirement for one was incorrect.
- C06 has live sheet discovery, metadata, disabled/media layout effects and removal tests. Reconcile exact member scope and verify the released cohort/platform gates.
- C07 has construction, replacement, adoption and lifecycle tests. Review the explicit-base browser profile limit against the selected contract, then complete released/platform gates.
- C08 needs complete nested mutation and invalid syntax/index behavior with actual computed effects.
- C09's strengthened 44 assertions now pass Windows JIT/NativeAOT and Chromium using public Corealpha12/nativepreview11/CSSDomalpha1 in a fresh cache with private/source overrides removed. They cover media/supports/container metadata and viewport/container computed effects, MediaList order/null/errors and invalid nested-insert atomicity. The earlier verified 41 was not relabeled as 44; the added checks passed afterward. Combined fixtures pass 864 native/875 browser plus Tab, 25 native/four browser context checks, viewport restoration and teardown. Applicable broader normal/traced, supported-platform and published Views backend acceptance remain; see the exact scope audit below.
- C10 needs actual cascade effects and selected editing/inspection across layers, scopes, nested declarations, imports and namespaces, with truthful optional-feature outcomes.
- C11 needs an associated CSS animation to observe edited keyframes. This engine test is selected work; the paused Anime implementation remains paused.
- C18's corrected fixture passes 51 assertions on Windows JIT/NativeAOT and 49 in Chromium. Native fontWidth is available; Chromium unavailability is explicit. Full nested feature-body comparison preserves definitions after family mutation, fixing review P2 through RED/GREEN verification. Palette descriptors remain readonly and change through stylesheet replacement. The pinned feature interface has fontFamily only; commented upstream feature maps are not missing wrappers. ASCII hex case normalization is test-only; production strings are preserved. Applicable platform/released-backend acceptance remains; font loading is separate.
- C19 needs shared custom-counter mutation and actual list-marker output for symbols, range, prefix and fallback.
- C21 needs a constrained anchored element to select the expected fallback, beyond descriptor editing, or evidenced unsupported state.
- C22's corrected fixture passes 23 assertions on Windows JIT/NativeAOT and Chromium: starting-style child edits, view-transition navigation and ordered types, parent/identity links, detachment and replacement. Commit2d7b13e2190808dee08061fd4ab6030964a792b0 records the font/transition tests. That batch passed 770 native/781 browser assertions plus Tab, 25 native lifetime/context and four browser context checks with zero retained objects. XML/FCS/formatting passed. Production was unchanged in that batch, so no redundant G06 full782/12 or analyzer rerun was required for it. Subsequent typed native facade changes do warrant the root's new analyzer gates. Applicable platform/released-backend acceptance remains; conditional A11 execution is independent.
- G06 passes the same fragment fixture in Chromium (74 assertions) and Windows normal/traced JIT and normal NativeAOT (61 each), plus eight native thread/disposal checks. Native PR16 mergedab03a2fc20c07a4f03127f791e60dbf66912a5ab after all five native platforms and managed passed. The preview.11 release is successful; all seven official packages, five RID payload hashes, model1702, clean source58a9980bf69196752a070fc832095249d26fca2b, CI builder and ABI21 verify. Actual official Windows raw probes pass 712 assertions with released handles. Public Core12/native11 Windows JIT/NativeAOT and Chromium now pass the expanded 864/875 aggregate fixtures. Broader normal/traced and supported-platform G06 shared behavior and published Views backend acceptance remain. G06 Views code is outside the Core11/native10 PR7 delivery.

## Release and CI checkpoint

- [Preview.10 release40886912646](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40886912646) is green. Verified native payloads are win-x64:24 files/79,028,433 bytes; win-arm64:24/76,190,929; linux-x64:1/79,842,033; linux-arm64:1/80,312,553; osx-arm64:4/87,897,424. Payload verification on all RIDs does not claim actual local execution on every platform.
- [Views PR7](https://github.com/ranvier-org/Ranvier.View/pull/7) is squash-merged as a097f8ec448f8974022f8416ccbf3fabf20493a4 after exact head2a0e71945f8abde21f7f75e3945d5b4b27110233 passed [CI40923137860](https://github.com/ranvier-org/Ranvier.View/actions/runs/40923137860) completely. Its 50 authored Anchor/Area assertions pass all four native JIT/AOT targets and Chromium. Earlier generator/owner-constraint fixes a3d9ecd and typed native facade repair5c092cc are included. The release title triggers trusted Views0.1.2/WebCorealpha2 publication after main CI; the CI owner handles publication and official consumers, which are not inferred from the merge. Prior [CI40920730701](https://github.com/ranvier-org/Ranvier.View/actions/runs/40920730701) failed only an analyzer timing budget (Template.fs index1684ms versus500ms;231 tests passed/one failed). The older40907189877 green C05-specific lanes remain valid closure evidence below despite its unrelated sugar failure.
- [Native PR16 validation40889860311](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40889860311) passed all five native targets plus managed. [Preview.11 trusted release40905413545](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40905413545) is fully successful. All seven official packages and native payloads verify: win-x64 24 files/79,030,993 bytes; win-arm64 24/76,193,489; linux-x64 1/79,846,561; linux-arm64 1/80,312,985; osx-arm64 4/87,898,896. Actual official Windows raw probes pass CSSsupports72, computed93, fragments140 and CSSOM407 (712 total), each in an independent process with handles released. Payload verification on all five RIDs does not claim local execution on every platform. Published Views backend acceptance remains pending.

These are closure requirements, not authorization to narrow scope. Update this checkpoint when new evidence changes the next action. Exact issue URLs are recorded in `project-items.json` after the initial draft-to-issue conversion is verified.

## Issue and link audit on 2026-10-30

The live audit found no missing or duplicate API issues: all 103 stable IDs, original scope/acceptance text, 98 required/five conditional labels, issue states, repository ownership paragraphs and 22 PR group references match. All four implementation repositories remain linked to the project. No new tracking issues were needed.

Two actual backlinks were missing from C05's closed issue after its closure evidence rewrite: [Core PR257](https://github.com/ranvier-org/Ranvier/pull/257) and [Views PR7](https://github.com/ranvier-org/Ranvier.View/pull/7). Both were restored as ordinary related links. C05 stayed closed/Accepted; no initial-import script was rerun and no other group was closed. The latest PR7 head/CI and native preview.11 release/probe evidence were refreshed.

## C09 exact selected-scope audit

The original source ledger and pinned IDL agree on the active C09-owned surface:

- MediaList: mediaText get/set with LegacyNullToEmptyString, length, nullable item, appendMedium and deleteMedium.
- CSSMediaRule: SameObject media; the PutForwards operation is expressed through media.mediaText in the typed common API.
- CSSConditionRule: inherited readonly conditionText, used by media/supports/container rules.
- CSSContainerRule: readonly containerName and containerQuery.
- CSSSupportsRule: no own parsed members; it inherits the condition/grouping surface.

The 44-check fixture represents all these own operations and verifies renderer metadata, MediaList ordering/nullability/errors, viewport-driven media/nested-supports effects, container-size effects, invalid-insert atomicity and viewport cleanup. No additional missing active own primitive was found. CSSMediaRule.matches and CSSSupportsRule.matches are commented upstream FIXME declarations, not omitted active wrappers. The source declares no conditionText/container-query setter to generate. Inherited grouping-rule editing remains C08 cross-group work; C09 is not forced to wait for every C08 or G01 API before its own acceptance can close.

C09 remains Verification because applicable broader normal/traced, supported-platform shared and published Views backend evidence is still pending. The earlier 41-check result was preserved as historical evidence until the revised 44 actually passed Windows JIT, NativeAOT and Chromium; 44 was never inferred from a planned count.

## C05 closure on 2026-10-30

[C05 issue30](https://github.com/ranvier-org/Ranvier.WebCore/issues/30) is closed as completed. The accepted scope is `Ranvier.Dom.CSS.escape : string -> string`, using the declared CSSOM26August2021 identifier-serialization profile. It adds no native ABI entry or new Views API. The appropriate public package is Core; a new published Views backend was an invented dependency and is removed from the acceptance gate.

All four official Corealpha11 packages (normal/traced, native/Fable) have recorded isolated native and executable Fable consumption. This audit reverified each downloaded package's repositorycommit939df32b6af78ef207a9e95e5d1c0e7aee53c35a and identical normalized `fable/CSS.fs` SHA-256. The public artifacts are at `C:/wk/ranvier-dom-official-alpha11`. [Core package-consumer CI](https://github.com/ranvier-org/Ranvier/actions/runs/40881215249/job/123473012510) contains four DOM_PACKAGE_CSS_ESCAPE PASS records across normal/traced native/Fable consumers; that hosted job checks staged release packages and is distinguished from the recorded local official-feed consumption. [Corealpha11 publication](https://github.com/ranvier-org/Ranvier/actions/runs/40882196652) succeeded.

The actual 45-assertion fixture contains 35 serialization vectors and ten escaped-querySelector identity round trips. CI at8fdf9fd uses public Corealpha11 and nativepreview10 references without normal-lane local overrides. All required actual native JIT/AOT targets pass, along with backend tests, teardown and representative NativeAOT smoke/counter execution:

- [Windows x64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40907189877/job/123566826923):45 JIT and45 NativeAOT.
- [Windows ARM64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40907189877/job/123566827203):45 JIT and45 NativeAOT.
- [Linux x64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40907189877/job/123566826382):45 JIT and45 NativeAOT.
- [macOS ARM64](https://github.com/ranvier-org/Ranvier.View/actions/runs/40907189877/job/123566826870):45 JIT and45 NativeAOT.
- [Chromium](https://github.com/ranvier-org/Ranvier.View/actions/runs/40907189877/job/123566827438):45 shared assertions and196609 independent CSS.escape oracle comparisons.

The C05 implementation, public Core cohort, normal/traced consumers, actual selector interoperability, required platforms/AOT, relevant build/analyzer/format checks and pure ownership-free behavior satisfy its task and applicable global gates. The unrelated Views sugar generator-output failure does not change CSS.escape or invalidate its green jobs. It remains a required CI repair for the overall goal. No other API group is closed, no newer CSSOM draft is claimed, and this closure does not claim universal raw DOM string-transport parity.
