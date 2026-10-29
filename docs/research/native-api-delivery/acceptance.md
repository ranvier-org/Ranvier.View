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

## D11 native reflection batch

- Scope: 23 configured HTML semantic/reflection interfaces; 107 new native functions (91 interfaces/1281 functions total). No new generator omissions; all 1174 prior ABI parameter signatures preserved.
- Observed RED/GREEN on configured scope and checked multi-tag casts; generator batch16/16 tests pass. Generated shared contracts compile net10.0/netstandard2.1 with zero warnings/errors.
- Real C++ compile first failed for missing TypeCastTraits of HTMLModElement/HTMLPreElement/HTMLQuoteElement. Existing HTMLElement type check plus namespace-qualified tag guards resolve those three casts; all other casts retain their original behavior. Glue and host compile/link pass.
- Native fixture is RED against preview.7 (missing rv_dom_html_base_element_set_href), GREEN against the private build:269 assertions for setters/getters, UTF-8, wrong-interface rejection, legacy tag aliases and live datalist/map collections. Datalist initially failed at runtime because its factory setting was disabled; enabling the existing engine feature fixes creation and live options. The table regression fixture remains44/44 pass.
- Core/Views/browser/AOT/platform matrix integration for D11 remains incomplete; no claim of complete delivery.

- D11 shared contracts: Core PR253 pins native27a1fe8b, 23 public interfaces/56 declared members, all four alpha.4 package variants built and consumed successfully; actual missing dateTime assertion is RED against alpha.3 and GREEN against alpha.4. Complete paginated API snapshot is d11-public-api.json.
- D11 shared behavior: static F# calls test property/attribute reflection both ways, tag aliases and live datalist/map collections. All205 assertions pass in real Chromium, native Windows x64 JIT and NativeAOT; D10 remains30/30 in the same fixture and teardown is clean. Generated wrappers map all D11 members with zero additional omissions.
- Core PR252 merged at efaa13cc after both CI and required Perf result succeeded. Main CI40820661010 passed. Trusted publish40821109843 reports all four alpha.3 uploads accepted; public NuGet version indexes still omit alpha.3 at this check, so published-package consumption remains pending indexing. No credentials were handled.

## D09 SVG/MathML bridge in progress

- Configured SVGElement and MathMLElement shared style/data/nonce/autofocus/tabIndex/focus/blur operations. Native model93 interfaces/1302 functions,21 new functions over D11. SVG animated className/root geometry remain assigned to their selected SVG groups.
- Observed scope regression RED/GREEN; native runtime RED against preview.7 missing rv_dom_svg_element_get_style. Before interface-guard correction, private Windows runtime passed95 assertions including namespace identity, style priority/removal, data/nonce/autofocus/tabIndex, wrong-HTMLElement rejection, focus/blur transitions and real Tab traversal from SVG to MathML.
- This batch exposed a generator bug: interface-level Conditional/EnabledBySetting gates were omitted from both own and mixed-in members. New regression test observedRED, propagation fixed, generator18/18 tests pass and gated C++ glue compiles. Runtime after the guard correction remains to rerun; no completed D09 claim yet.
- Core PR253 independently reviewed with no findings and merged at789c7d3d after CI/Perf/conventional gates succeeded. Main CI40822783781 is running before trusted alpha.4 publication.

- D09 after interface-guard correction: private host relink succeeds; SVG/MathML95 assertions, HTML reflection269 and tables44 all pass. All1281 previous native parameter signatures remain unchanged. Shared contract package/adapter/browser/AOT work for D09 is still pending.

- D09 shared integration: Core PR254 provides alpha.5; four native/Fable normal/traced package consumers pass. Real shared Native fixture observedRED InvalidCastException because MathML nodes received generic ElementObject. Namespace-aware MathMLElementObject dispatch fixes it. The same24 namespace assertions now pass native JIT, Chromium and Windows x64 NativeAOT; actual TabSVG-to-MathML traversal passes both native and Chromium. D10/D11 remain30/205, teardown clean. Aggregate FSharp.Core IL3053/IL2104 warnings remain reported.
- Browser compile initially failed to resolve createElementNS because the fixture omitted open Ranvier.Dom, which activates the existing auto-open union adapters. Adding the namespace import resolves it; no primitive or API redesign was needed.

- Batched Windows backend acceptance after D09/D10/D11: dotnet test tests/Ranvier.View.WebCore.Tests (unfiltered) passed781, skipped12 existing jsdom-only/profile cases, failed0, total793. It used the local alpha.5 contracts, updated interop and private native runtime; this is not official-package/platform matrix acceptance.
- Cache-busted public NuGet index now lists alpha.3 and alpha.4. Earlier plain index reads were stale; trusted publish runs40821109843/40823270724 and the fresh public index establish availability for those versions.

## Integrated contract releases and area reflection follow-up

- Core namespace contracts: PR [Ranvier#254](https://github.com/ranvier-org/Ranvier/pull/254), merged as `0071ca7df52b93ae12ee88be68b51b60be47175f`. Main [CI 40827715155](https://github.com/ranvier-org/Ranvier/actions/runs/40827715155) and trusted [publish 40828256054](https://github.com/ranvier-org/Ranvier/actions/runs/40828256054) succeeded. The fresh public NuGet index lists alpha.5. This establishes publication availability; isolated official-package consumption remains separate verification.
- Native table exports: PR [Ranvier.WebCore#7](https://github.com/ranvier-org/Ranvier.WebCore/pull/7), merged as `5560c8d`. [CI 40816842079](https://github.com/ranvier-org/Ranvier.WebCore/actions/runs/40816842079) passed all five native targets: Windows x64/arm64, Linux x64/arm64 and macOS arm64, plus package-contract and managed packaging.
- Native reflection CI initially failed on Windows x64/arm64 and macOS because the existing negative `rv_abi_has` test used `HTMLMarqueeElement`, which D11 now exports. Reproduced the exact failing assertion against the private engine, then verified positive marquee and unknown-interface negative checks. The fix is pushed through PRs 8 and 9; their new platform matrices remain unverified until terminal success.
- D11 area expansion: 38 generated functions, 94 total interfaces / 1,340 functions. Private Windows glue/host compile and link pass. Real-engine area fixture passes **117 assertions**, covering attribute reflection, valid URL component normalization, live relList, live map.areas and wrong-interface errors; all retained handles are released. Initial fixture failed on the missing href export, then on disabled download-attribute reflection. Enabled the existing download-attribute setting; actual navigation/download execution is not claimed. Shared wrappers, browser/AOT fixtures and published platform binaries for this expansion remain incomplete.
- Fresh generator suite after area configuration: **19 passed**. No required group is marked complete by these bounded results.

## CSSOM native delivery slice

[Native PR10](https://github.com/ranvier-org/Ranvier.WebCore/pull/10), revision 32740b8e721aee940a61359f22a46498c0473974, adds 379 functions over the previous 1,302 without changing their C parameter signatures. Current total:126 interfaces /1,681 functions. Generator:22/22. Shared contracts:net10.0 and netstandard2.1 compile with zero warnings/errors. Private Windows glue and host compile/link pass. Batched real-engine fixtures:tables44 +HTML reflection269 +SVG/MathML95 +area117 +CSSOM201 =726 assertions, retained handles released. CSSOM tests cover conditional/grouping rules, layers/scopes/imports/namespaces/nesting, keyframe edits, font aliases and metrics, counters, position descriptors and starting/view-transition rule metadata. New fixtures are wired into ABI CI.

Constructors, async replace, adopted sheets, shared wrapper/browser/AOT fixtures and published platform/package conformance remain incomplete. Gated detached objects without owner/constructor documents return unsupported. None of the broad CSS groups is completed by this slice.

Core [PR255](https://github.com/ranvier-org/Ranvier/pull/255), revision `bad60bb1f2eb878f6ae2d8381f872caccaafeb8a`, provides the canonical contracts as alpha.6. The package consumer observed RED against alpha.5 (missing stylesheet insertion) and GREEN against all four alpha.6 native/Fable normal/traced variants. Both framework targets build. Fresh FCS checks are clean. The CSS public snapshot has 28 interfaces /173 declared members, compared with 3 /7 before; inherited members and non-CSS namespaces are explicitly outside that snapshot. Independent native and Core reviews found no actionable findings. PR/platform CI and trusted release remain pending.
