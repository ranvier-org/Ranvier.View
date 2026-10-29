# Conditional rule conformance implementation plan

> **For agentic workers:** Use superpowers:executing-plans for this bounded C09 acceptance batch. Continue the authorized inline workflow without permission prompts.

**Goal:** Verify the selected media/supports/container rule APIs against actual changing viewport and container layout conditions.

**Architecture:** Reuse the existing generated CSSRule/MediaList contracts and live computed-style provider. One shared async fixture receives a viewport-resize callback: native WebCore uses its existing Resize/Frame methods; Chromium uses a controlled Playwright viewport callback. The fixture restores viewport and removes its nodes before returning.

**Tech stack:** F#, Ranvier.Dom alpha.12, WebCore wrappers, Fable/Chromium and NativeAOT.

## Files and contract

- Views owns tests/NativeApi.Conformance/ConditionalRules.fs, Native.fs and Browser.fs runners/project compile lists, and browser.mjs viewport callback. The contract manifest and capability/evidence files record exact C09 outcomes.
- Fixture signature: Document -> ComputedStyleService -> struct(int * int) -> (int -> int -> Async<unit>) -> (exn -> string) -> Async<int>. The original viewport comes from the actual host/window, so scrollbar-dependent element measurements cannot corrupt restoration. Resizing is a test-host operation, not a new public DOM facade.
- Exercise CSSMediaRule.media/conditionText; MediaList.mediaText/length/item/appendMedium/deleteMedium; nested CSSSupportsRule condition and parent/list relationships; CSSContainerRule name/query/condition and nested rules. Mutable conditions use the supported media-list or stylesheet-editing route.
- Existing native/Core public signatures and exports remain unchanged unless an actual failing runtime assertion proves a bridge defect.

## Tasks

- [x] Verify selected signatures and pinned IDL before fixture calls; check exact nullable media indexing and DOM error semantics.
- [x] Add a mixed nested rule fixture and verify actual media/supports layout at wide/narrow viewport sizes, live media-list edits, and missing-medium errors.
- [x] Resize a named inline-size container and verify its child changes computed size across the query threshold while inspecting the real container-rule metadata.
- [x] Wire deterministic viewport resize/restore in native and browser runners, sharing inputs/assertions and preserving owning-thread execution.
- [x] Run native JIT, NativeAOT and actual Chromium; review/format/XML checks and teardown. Batch full suites only if implementation changes justify repeating the completed G06 gates.
- [x] Record target capabilities, exact counts and remaining platform/package acceptance; ask the acceptance owner to audit the original complete C09 scope before closure.

## Review focus

Viewport/container changes must affect real computed results, rather than only parser metadata. Media-list edits must preserve order, nullable indexing and errors. Restore the actual original viewport on both success and failure. Browser callbacks belong only to the controlled test harness. Do not claim G01 subscription delivery or other unimplemented observer APIs from these layout assertions.

## Verified results and remaining acceptance

All 44 assertions pass in Windows x64 JIT, Windows NativeAOT and Chromium. The combined shared fixtures pass864 native and875 browser assertions, plus Tab traversal,25 native lifetime/context checks and4 browser context checks. Dependencies are official Core alpha.12, CSS adapter alpha.1 and WebCore/native preview.11 in a fresh isolated cache; all source/private runtime overrides were removed. Views remains local source. Other supported-platform/traced checks and published paired backend consumption remain outstanding, so C09 is in Verification, not complete.

Independent review found a weak invalid-insertion atomicity assertion. The corrected fixture checks complete serialized rule text, retained child identity and computed height after IndexSizeError. The final count44 includes these three additional checks. Fresh FCS checks are clean; formatting passes. Existing NativeAOT FSharp.Core IL3053/IL2104 and SameValueZero IL2075 warnings persist.

The selected pinned interface declares readonly conditionText/container metadata. MediaList editing is writable; media/supports matches members are commented upstream FIXME declarations. G01 subscriptions and C08 grouping-rule acceptance remain separately tracked rather than invented prerequisites for this fixture's direct computed-result checks.
