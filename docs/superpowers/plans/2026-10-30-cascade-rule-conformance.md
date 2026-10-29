# Cascade structure conformance

## Core and view state

- Core primitives used: shared DOM contracts and borrowed computed-style service.
- View-owned state: temporary document nodes and stylesheets, removed after each fixture.

## Selected scope

C10 ([WebCore issue 35](https://github.com/ranvier-org/Ranvier.WebCore/issues/35)) includes layer blocks/statements, scopes, nested declarations, imports and namespace rules. Read/edit representative structures and verify their actual cascade effects. Optional rule syntax must report a concrete capability outcome if unavailable; bridge failures remain failures.

## Execution

- [x] Read pinned IDL and verify the six shared interfaces through fresh FCS after a clean official-package baseline build.
- [x] Add one shared engine fixture for layer priority/order, scoped bounds, post-nesting declarations, namespace matching and imported stylesheet effects.
- [x] Verify borrowed objects, live identity, retained detached rules and cleanup. Generic rejected-edit preservation is covered by C08/C09.
- [x] Run Windows JIT, Chromium and Windows NativeAOT using official Core alpha.12, CSS DOM alpha.1 and WebCore preview.11 packages.
- [x] Verify formatting/XML comments and record exact counts, limitations and independent review.

## Evidence and rulings

- Baseline native conformance project: Release build passed (five projects, zero errors/warnings); fresh FCS check clean with zero diagnostics.
- SageFs project-session creation rejected the completed build because it expected restore files in `obj`, while the conformance project uses `obj/Native`. An owned bare session references the built contract assembly for pure/type checks; actual engine execution uses the CLI under an owned lease. The shared daemon is unchanged.
- Production code changes require a failing real-engine assertion first. Full backend/analyzer gates are batched by the parent lane; this fixture batch does not repeat them.
- Local success does not close the project card. Traced/platform/released paired Views package acceptance remains with the acceptance owner.
- Windows JIT: 91 new C10 assertions passed; scope and nested-declaration capabilities are available. The complete shared runner passed, including Tab traversal, existing lifetime checks and native teardown.
- Chromium: 93 new C10 assertions passed, plus the complete shared runner and Tab traversal. Data-imported stylesheet contents are readable/editable in Chromium; native WebCore rejects this access with the exact `SecurityError`. Both providers still prove import href/media/layer/supports metadata, stable imported-sheet identity, actual cascade, media toggling, deletion and URL replacement. This qualification concerns origin-clean access to data imports, not CSSImportRule availability.
- Live import insertion around a leading layer statement differs: inserting after it passed WebCore and raised Chromium `HierarchyRequestError`; inserting before it passed Chromium and raised WebCore `HierarchyRequestError`. Pinned WebCore `StyleSheetContents::wrapperInsertRule` stores leading layer statements separately. The shared replacement uses stylesheet text replacement, preserving genuine CSSImportRule editing through its media list and stylesheet structure.
- Official restored packages: Ranvier.Dom 0.1.0-alpha.12, Ranvier.Css.Dom 0.1.0-alpha.1, Ranvier.WebCore and Ranvier.WebCore.Native 0.1.0-preview.11. The Windows host DLL SHA-256 matches the verified official runtime: `1b401d382add034921d822b650cd11e85f12d3911f121f5e17dd0ccd508dccc1`.
- NativeAOT initially rejected the tool process because its `OS` environment variable is absent. MSBuild confirmed `OS` empty and target OS `win`. The established publish command passes `-p:OS=Windows_NT`; this changes a build property and installs no tools.
- Fantomas check passed; XML documentation audit reported zero findings; the installed comment-hygiene plugin reported no FOR-REVIEW comments. Windows PowerShell policy rejected the plugin script, so it ran successfully with the installed `pwsh` host.
- Windows NativeAOT: final publication and execution passed, with the same 91 C10 assertions and capability profile as JIT. The complete shared runner, existing lifetime checks, Tab traversal and teardown passed. Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 analysis warnings remain.
- Independent review found one assertion-strength improvement: compare computed outer width with its pre-nesting baseline after removing the nested width declaration. Added that check and the initial computed 37px check; JIT now passes 91 and Chromium 93. Initial import readiness also waits for the expected computed 71px result. Final NativeAOT revalidation includes these two assertions.
