# Counter style rendering conformance

Required C19 delivers CSSCounterStyleRule inspection/editing and verifies symbols, range, prefix and fallback through actual rendered list markers. The shared fixture also covers fixed, numeric and additive systems, negative formatting, padding, rename, invalid-input preservation, inherited cssText no-op and retained metadata after deletion.

## Ownership and scope

The original fixture was developed in `C:/wk/rv-c10-cascade`. Integration now belongs to root in `C:/wk/rv-position-try`, alongside C21. Native patch0016 synchronizes the parsed counter-style name and is included in [WebCore PR123](https://github.com/ranvier-org/Ranvier.WebCore/pull/123). No new shared authoring or reactive primitive is added.

The marker oracle measures an independently rendered literal reference with the same font. An explicit narrow/wide glyph control rules out character-count estimates. Normal shared execution requires exactly64 assertions and removes both fixture owners in finally. Windows private JIT and NativeAOT pass64 with zero-object host teardown; Chromium also passes64. The browser combined batch passes1410 API assertions plus4 context checks, Tab traversal and196609 oracle comparisons. Released/platform acceptance remains pending.

## Browser crash investigation

The full original sequence completes the attached fallback edit and final marker render, deletes the counter rule, and then crashes at the valid prefix setter on the retained rule. Root traced each boundary rather than inferring the failing statement from the last assertion. A minimal test reproduces the same result for valid prefix, symbols, name and fallback setters after deletion; corresponding attached controls complete. An invalid system edit returns unchanged and does not crash.

This matches the null parentStyleSheet dereference after mutation in Chromium's [CSSCounterStyleRule implementation](https://chromium.googlesource.com/chromium/src/+/b52029b56fc40f450616d5b1bb49d3e5d2c7f227/third_party/blink/renderer/core/css/css_counter_style_rule.cc), lines195/214 and221/238. This is source evidence consistent with the observed failure, not a symbolized crash-stack claim. Prior attempts to disable the sheet or change unrelated edit order did not resolve it because they left the detached mutation intact.

The acceptance review checked the original C19 requirement: attached inspection/editing and rendered symbols/range/prefix/fallback are required; editing a deleted counter rule is extra coverage. The normal shared fixture therefore preserves all required rendering checks and the removal/retained metadata checks. It does not invoke the fatal extra setter or advertise that path as supported. Native raw counter tests retain their additional detached-setter coverage.

The full original sequence remains available as `CounterStyleRules.reproduceDetachedMutation`. After browser generation, run `node tests/NativeApi.Conformance/counter-style-regression.mjs --expect-crash` in an isolated page. The script requires a real renderer crash after an explicit console checkpoint immediately before the detached setter; ordinary exceptions and earlier crashes do not count. A missing generated-source anchor fails strictly. Chromium156.0.8078.4 reports reachedDetachedMutation=true before crashing. A browser fix that completes the sequence fails the expected-crash qualification and requires reassessment. No browser Unsupported exception is claimed for direct setters: the tested browser currently crashes.

## Gates

- [x] Private native rename/render repair: raw86 assertions.
- [x] Shared Windows JIT attached rendering:64 assertions and host teardown.
- [x] Original-sequence tracing and minimal attached/detached controls identify the failing lifecycle operation.
- [x] Independent requirement audit confirms the required attached/rendering coverage remains intact.
- [x] Shared Chromium and NativeAOT64; the separate full original expected-crash regression confirms an actual renderer crash on Chromium156.0.8078.4.
- [x] Independent review of the final split/integration and checkpointed crash harness is clear; formatting, XML/comment audits and fresh native FCS checks pass.
- [ ] Official Native preview.13, platform CI, published normal/traced consumers and board acceptance.
