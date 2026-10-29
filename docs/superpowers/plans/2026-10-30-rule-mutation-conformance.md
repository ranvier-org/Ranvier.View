# Rule mutation conformance implementation plan

> **For agentic workers:** Use superpowers:executing-plans for this bounded C08 batch. Continue authorized execution without permission prompts.

**Goal:** Verify the complete selected rule enumeration/mutation behavior, including actual nested cascade changes and rejected edits.

**Architecture:** A shared synchronous F# fixture uses existing CSSStyleSheet, CSSGroupingRule, CSSStyleRule and CSSRuleList contracts with a borrowed ComputedStyleService. It creates an isolated style/probe subtree, inspects live rules and detached retained rule objects, and removes its nodes in finally. No new bridge is assumed necessary.

**Tech stack:** Core DOM alpha.12, CSSOM wrappers, public WebCore preview.11, Fable/Chromium and Windows NativeAOT.

## Files

- `tests/NativeApi.Conformance/RuleMutations.fs`: shared nested rule enumeration/editing/error/computed-result fixture.
- Native.fs, Browser.fs, both project files and browser.mjs: invoke and report the fixture, preserving existing teardown.
- Contracts/capabilities, fixture README and a public API snapshot: record exact selected signatures, results and remaining acceptance.

## Tasks

- [x] Inspect clean FCS/public signatures and pinned CSSRule/CSSRuleList/CSSStyleRule/CSSGroupingRule/CSSStyleSheet IDL contracts, including nullable indexing, default insert index, readonly members and DOM errors.
- [x] Prove assertion/error-check shapes in SageFs, then add nested insertion, selector/declaration/cssText editing and deletion with live computed width/height effects and rule/list/parent identity checks.
- [x] Test invalid syntax and out-of-range insert/delete on stylesheet/grouping rules. Compare full rule text, child identity, counts and computed result before/after rejection rather than accepting count preservation alone.
- [x] Run actual Windows JIT, Chromium and Windows NativeAOT with public dependencies, verify teardown, review/XML/format and record precise coverage. Do not repeat the completed full backend/analyzer gates for fixture-only additions.
- [x] Have the acceptance owner audit C08's original scope and remaining platform/traced/published-backend gates; keep the issue open until proven complete.

## Review focus

Exercise nested edits that affect rendered results. Distinguish malformed rule insertion errors from invalid selector assignment's specified no-op behavior. Rule enumeration and parent relationships must remain live and stable; detached objects must retain their owned lifetime. Default insertion index is zero. Error comparisons must detect rule replacement, reordered siblings or lost declarations. Treat cssText setter behavior according to the pinned engine contract and actual browser behavior; do not invent an edit that the interface does not implement.

## Verified results and remaining acceptance

All131 assertions pass in Windows x64 JIT, NativeAOT and Chromium, with official Corealpha12, CSSadapteralpha1 and WebCore/nativepreview11 dependencies. Combined shared coverage is995native/1006browser, plus25native lifetime/context checks,4browser context checks and actual Tab traversal. Native teardown retains zero objects. Views remains local source; applicable traced/platform and released paired backend acceptance remain pending.

The complete public snapshot covers14 declared members acrossCSSRule/CSSRuleList/CSSStyleRule/CSSGroupingRule. Fresh FCS returns0 diagnostics; XML audit and formatting pass. SageFs proved types/pure assertion shapes and rebuilt persisted source successfully. Its internal fixture module cannot be called directly from a separate FSI assembly; that accessibility diagnostic is recorded, and actual runtime verification used the normal runner. The owned session is stopped and all leases are released.

Independent read-only acceptance review found no actionable correctness or scope issue. The reviewer inspected selected source declarations and assertions; actual runtime counts come from the executing root. No production bridge change was required. Existing NativeAOT FSharp.Core IL3053/IL2104 and SameValueZero IL2075 warnings remain.
