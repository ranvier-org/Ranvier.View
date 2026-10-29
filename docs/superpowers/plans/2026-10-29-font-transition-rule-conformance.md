# Font and transition rule conformance implementation plan

> **For agentic workers:** Use superpowers:executing-plans to deliver this bounded acceptance batch. Continue the existing authorized workflow without permission prompts.

**Goal:** Close the missing shared behavioral coverage of C18 and C22 while retaining their complete selected rule inspection/editing scope.

**Architecture:** Exercise the existing generated Ranvier.Dom rule interfaces through one shared F# fixture on Chromium and WebCore. Existing stylesheet insertion/deletion handles immutable rule descriptors; font rendering/loading remains separate T01/T02 work. C22 rule inspection is independent of conditional A11 transition execution.

**Tech stack:** F#, shared DOM alpha.12, generated WebCore wrappers, Fable/Chromium and Windows NativeAOT.

## Source contract and files

- Pinned CSSFontFeatureValuesRule.idl exposes writable fontFamily only; annotation/ornaments/stylistic/swash/characterVariant/styleset/historicalForms maps are commented upstream FIXME declarations. The selected shared contract does not advertise those maps. Inspect feature definitions through the engine's CSSRule serialization and edit through the existing stylesheet rule replacement path; do not invent callable map bindings.
- CSSFontFaceRule.style exposes the existing CSSFontFaceDescriptors declarations and aliases. CSSFontPaletteValuesRule exposes readonly name, fontFamily, basePalette and overrideColors; replacing its rule is the selected edit route.
- CSSStartingStyleRule inherits CSSGroupingRule child inspection/insertion/deletion. CSSViewTransitionRule exposes readonly navigation and FrozenArray string types; inspect and replace through CSSStyleSheet, preserving type/order/parent identity.
- Views owns tests/NativeApi.Conformance/FontTransitionRules.fs, its two runner entries and project compile lists. Contract/capability/evidence files record the exact source profile and target outcomes. No new native/Core export is assumed necessary; actual runtime failures determine any implementation repair.

## Tasks

- [x] Capture the public rule signatures from FCS and pinned IDL before adding fixture calls; retain the exact source limitation above.
- [x] Add C18 shared assertions for font-face descriptor parse/aliases/mutation/removal, font-feature family mutation and serialized feature definitions, and all font-palette getters plus rule replacement/retained rule behavior.
- [x] Add C22 shared assertions for starting-style nested rule identity/edit/insert/delete, and view-transition navigation/types, parent links, replacement and retained rule behavior.
- [x] Execute the shared fixture on actual native WebCore and Chromium. Preserve engine serialization differences explicitly when semantically equivalent; repair actual bridge errors with a failing runtime assertion before source changes.
- [x] Verify NativeAOT and host teardown, XML comments/formatting and independent review. Batch whole-backend/analyzer gates only if source implementation changes justify rerunning them after the completed G06 batch.
- [x] Record exact assertions, official/private package provenance and remaining platform/package acceptance. The acceptance agent independently decides group closure against the original selected scope; no implicit completion from this local fixture alone.

## Review focus

Readonly palette/transition descriptors must remain readonly. Serialized feature definitions must come from the real engine parser. Replacing a rule must preserve the detached old wrapper's owned lifetime and the new list entry's concrete identity. Do not make C22 depend on transition execution, or imply that descriptor inspection proves font loading/rendering.

## Verified results and limits

C18 passes51 assertions in Windows JIT/NativeAOT and49 in Chromium; C22 passes23 on both targets. The font-width descriptor alias exists on WebCore but is undefined on the tested Chromium; the fixture explicitly reports true/false capability and tests it where available. Unicode-range hex letter case and palette named/RGB color serialization differ while preserving the same parsed values. Production getter strings remain unchanged. Omitted transition navigation is empty on both engines because this is declared rule metadata, not the computed execution policy.

Independent review found one P2 assertion weakness: checking only for the decorative identifier could miss lost feature values or a removed stylistic block. A pure REPL RED demonstrated the false acceptance; comparing the complete serialized nested body rejects that loss and accepts a family-only change. The corrected fixture passes actual Windows JIT/NativeAOT and Chromium. Fresh FCS check and XML audit have0 diagnostics/findings; formatting passes. All root-owned bare sessions are stopped.

The original font batch passed770 WebCore assertions and781 Chromium assertions. After adding conditional-rule and common-hyperlink regressions, the combined batch passes864 native JIT/NativeAOT and875 Chromium assertions, plus Tab,25 native lifetime/context checks and4 browser context checks. Native teardown retains zero objects. The final dependency cohort is official Corealpha12, CSSadapteralpha1 and WebCorepreview11 with no source/private runtime overrides; Views remains local source. All three analyzer groups pass after the subsequent authoring-facade repair, and the full backend result is782pass/12ignored. NativeAOT retains its previously recorded warnings. Other-platform/shared/traced and published paired backend acceptance remain outstanding; neither group is complete.
