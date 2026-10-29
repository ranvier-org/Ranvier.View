# Position fallback rule conformance implementation plan

## Core and view state

Core primitives used: the existing shared DOM contracts and host lifetime. View-owned state: fixture DOM nodes and retained CSSOM objects, removed before returning. No reactive or ownership primitive is added.

> For agentic workers: use superpowers:executing-plans for the bounded fixture and request independent review before integration.

**Goal:** Fulfil C21 in `2026-10-29-shared-native-api-tasks.md`: inspect/edit position-try descriptors and prove an overflowing anchored element selects the authored fallback.

**Architecture:** Add `tests/NativeApi.Conformance/PositionTry.fs`, shared by the native and Fable runners. Use the existing CSSPositionTryRule/CSSPositionTryDescriptors contracts and actual rectangle reads. A small fixed-size containing block makes below-anchor placement overflow; a named fallback selects top, then left after a typed descriptor edit. No production change is planned unless actual engine behavior exposes a missing primitive or defect.

**Tech stack:** F#, Core DOM alpha.12, published WebCore preview.11, Chromium and the existing native conformance harness.

## Global constraints

- Required x group; unavailable engine features must be recorded explicitly, never replaced with invented geometry.
- Keep the paused work excluded and preserve the frozen published 0.1.2/0.1.3 consumer profiles.
- Use FCS for semantic/member inspection and SageFs for pure type probes; actual host/layout tests use the executable gate.
- Use leases, CRLF, exact edits, source-comment/XML checks and message-file commits.

## Review focus

- Prove initial placement overflows with the fallback disabled; successful fallback placement alone does not prove selection.
- Observe typed descriptor edits through actual geometry and preserve retained declaration/rule identities.
- Invalid declaration edits preserve complete rule text and actual placement.
- Keep containing-block coordinates independent of body margins and platform fonts.
- Remove fixture nodes and stylesheets and verify retained objects after rule removal without claiming a public package result from local source.

## Task: Shared position fallback proof

- [x] Inspect pinned CSSPositionTryRule/Descriptors IDL and implementation; fresh native project FCS check is clean and referenced members resolve.
- [x] Build the isolated baseline once: zero warnings/errors. SageFs project loading refuses the per-project intermediate directory; bare session type probes confirm positionArea, width and the hyphenated margin alias. Owned session stopped.
- [x] Add the shared fixture for parsed metadata, style identity/parent, default overflow, top/left fallback geometry, width/margin updates, invalid input and removal. The numeric helper rejects NaN/infinity through a negated bounded-distance predicate.
- [x] Run focused Windows JIT and Chromium probes against official dependencies; investigate actual failures before adding production fixes. Chromium passes44; official native preview.11 reproduces the retained-style defect below.
- [x] Wire the permanent native/browser runners and exact count/capability guards, then run the batch JIT/AOT/browser gate. After merging main f97da4cd forward, the private Windows candidate passes1333 API assertions in each JIT/NativeAOT execution, plus25 lifetime checks, Tab traversal and teardown; Chromium passes1346 plus4 context checks, Tab traversal and196609 CSS escaping oracle cases.
- [x] Request independent review and commit the source without changing frozen cohort profiles. The fixture, guards, complete API snapshot and additive integration review are clear.
- [ ] Stack the source PR after Native preview.13 is available for CI. Platform and public-package acceptance remain later gates; the repair is tracked in [WebCore PR123](https://github.com/ranvier-org/Ranvier.WebCore/pull/123).

## Current evidence and limitations

Baseline source is Views main `653be0b2646dd4423bfbb7ddbcce70f35554d161`. FCS resolves CSSPositionTryRule.name/style and all 70 descriptor members in the published DOM package; the complete snapshot is `api-snapshots/position-try-public-api.json`. SageFs 0.6.906 rejected project creation after the successful build because it expected `obj/project.assets.json` and `obj/Native.fsproj.nuget.g.props`; the fixture configures `obj/$(MSBuildProjectName)/`. Bare session 633403d9 proved the typed setters and was stopped.

## Focused probe findings

Chromium passes all 44 assertions through the shared published alpha.12 contracts. Official native preview.11 passes the descriptor edits and geometry assertions, then fails at the retained `rule.style` read after deleting the rule: `NotSupportedException: CSSPositionTryRule.style is not supported by this WebCore build`.

The original generated guard obtains settings exclusively from the current parent sheet's owner/constructor document; deletion clears that parent, so the guard returns Unsupported. The repair is implemented in WebCore PR123. The fixture retains its browser/native common expectation; no unsupported result is substituted for retained-object access.

The private counter/context candidate in `C:/wk/native-cssom-detached-context/artifacts/cssom-context/runtime` repairs this defect: the shared fixture passes44 assertions plus zero-object teardown in Windows JIT and NativeAOT. Its direct lifecycle regression passes389 assertions, counter styles86, and existing CSS regressions805. NativeAOT emits the existing FSharp.Core IL3053/IL2104 warnings. These results do not prove released-package or other-platform acceptance; Native preview.13 is reserved for the repair and the full shared batch remains pending.

Two initial fixture assumptions were corrected using actual results and primary source. Default area alignment shifts an oversized target to fit, so explicit unsafe alignment is required to demonstrate overflow. The native frame flush also records the last successful fallback while a synchronous browser layout read stays within one frame. Anchor movement alone need not reset that preference. The fixture explicitly disables fallback, measures the newly fitting default, restores fallback and verifies subsequent overflow. This follows the [fallback-sensitive change and remembered option rules](https://drafts.csswg.org/css-anchor-position-1/#last-successful-position-option) and pinned StyleTreeResolver.cpp165-174/1779-1792. That timing difference did not demonstrate an engine defect.
