# C03: document-scoped CSS support queries

The active goal retains all 98 required and five conditional groups. C03 requires both CSS.supports overloads, evaluated by the actual engine parser/settings.

## Fixed contract and owners

- Core: `Ranvier.Dom.CssQueryService` is a borrowed, document-scoped abstract service with `Supports(property: string, value: string): bool` and `Supports(conditionText: string): bool`. Browser `CssQueries.forDocument(document)` and native Views `CssQueries.forHost(host)` supply it. It owns no callbacks or native handles; native calls require the live owning host thread. The service and factory module use distinct names, following StyleSheetService/StyleSheets and avoiding a cross-file F# type/module collision.
- Null string inputs follow DOMString conversion to the text `null`. Complete input is preserved, including embedded U+0000. Unsupported syntax returns false; absent bindings/providers and invalid/disposed contexts raise errors rather than report false support.
- Native source: pinned `css/DOMCSSNamespace.idl` and `DOMCSSNamespace.{h,cpp}`. Both overloads are static `DOMCSSNamespace::supports(Document&, ...)`; the engine creates CSSParserContext with the document's settings and standard parsing mode. No hardcoded supported-property or feature table.
- Native generator: model real IDL namespaces and partial namespaces separately from interfaces; explicitly configure selected namespace operations with a checked Document receiver. Do not create a fake CSS object/handle or expose the rest of the namespace implicitly.
- String transport: new selected namespace calls take input byte pointers and explicit signed byte lengths. Validate negative lengths, null/nonzero spans and invalid UTF-8; zero-length spans represent empty strings. Existing ABI parameter signatures remain unchanged. This avoids the current null-terminated transport's embedded-NUL truncation.
- Exports: `rv_dom_css_supports_2(document, propertyBytes, propertyLength, valueBytes, valueLength, result)` and `rv_dom_css_supports_1(document, conditionBytes, conditionLength, result)`, with `dom-css-supports` capability. Metadata records the source namespace, receiver and length-aware transport. A namespace feature is advertised only when all its configured overloads are compiled.
- Native branch `codex/native-api-css-queries` starts from PR13's head without modifying that branch. Its future release version is preview.9; preview.8 must still be released from the verified merged PR13 source.
- Core contract/provider follows C05's alpha.9 branch as a stack, with a subsequent coordinated contract version; existing CSS.escape remains unchanged.
- Views owns the native provider, shared runtime fixture, browser runner, capability/contract/public-API evidence and platform/AOT acceptance.

## Acceptance sequence

1. Observe a generator regression fail for missing configured namespace exports, then implement namespace parsing, source-based overload mapping, checked context and complete-string transport. Preserve namespace/partial gates and all 1,695 prior function records.
2. Compile/link the generated native code against the pinned source. Real ABI calls must verify true/false declarations, compound/selector conditions, empty/invalid input, embedded NUL, non-ASCII, wrong receiver/thread, invalid spans and result preservation on errors.
3. Fix/check the additive common public API before editing its signatures; package and consume normal/traced native/Fable providers.
4. Run the same shared declaration/condition vectors against native WebCore and real browser CSS.supports, including native context lifetime checks and NativeAOT. Record actual target differences rather than substitute a parser approximation.
5. Publish only after required CI and review; consume official packages and verify all platform lanes before marking C03 complete. No deferred or paused group is pulled in by this task.

Native namespace generation is committed as af71cfcebbea107d0addc07723edf812fa528269: 29 generator tests and 72 real ABI assertions pass. All 1,695 prior API records remain unchanged. Shared contracts and browser/native providers pass the same 32 query assertions in Windows JIT, Windows NativeAOT and Chromium. Four native factory/lifetime checks and two browser invalid-context checks pass. Fable interface name mangling preserves overloads. Core PR258 supplies alpha.10; traced/official package consumers, platform CI and publication remain pending. This evidence does not mark C03 or the full goal complete.
