# Shared browser/native API delivery tasks

> **For agentic workers:** Use superpowers:executing-plans, or an explicitly chosen execution workflow, to deliver these tasks. This document captures scope and acceptance; final per-member signatures are fixed in Task 0 before implementation.

## Core and view state

- **Core primitives used:** Ranvier Owner/createRoot/onCleanup/runWithOwner manage lifetimes; effects/memos and existing graph batching/dispatcher manage reactive updates and scheduling. Cancellation and facade adapters integrate with those primitives instead of introducing another ownership/reactive tree.
- **View-owned state:** DOM handles, listener/observer/effect handles, renderer/layout state, property accessors and per-target DOM caches belong to the relevant View/engine adapter. Business state, keyed identity and general lifecycle remain in core. A browser facade may use browser facilities while the native provider uses .NET or host services.

**Goal:** Fulfil every currently `x` API group through shared engine APIs or browser/native facades, and evaluate/implement each `!` extension when its explicit blocker gate passes. Keep every `?` item for the next goal.

**Architecture:** A common typed F# contract is the portability boundary. Engine-backed operations use browser DOM/native WebCore; facade-backed operations use browser services and Ranvier/.NET/host implementations; qualified/platform extensions report their real capabilities and differences. Do not treat matching names, available IDL or a managed wrapper as behavioural parity.

**Tech stack:** F#, Fable/browser adapters, .NET NativeAOT, Ranvier.Dom, Ranvier.View.WebCore, Ranvier.Css and Ranvier.WebCore native ABI/code generation.

**Spec:** [User-prioritized API checklist](../../research/2026-10-29-native-api-candidates.md), using the marks read on 2026-10-29; [source/export ledger](../../research/2026-10-29-native-api-source-ledger.md). The five conditional items are A06, A07, A08, A09 and A11.

## Scope snapshot

Live tracking: [GitHub Project: Shared browser/native API delivery](https://github.com/orgs/ranvier-org/projects/1). Agents record acceptance evidence, remaining scope, repository ownership and linked delivery PRs there; see [tracking instructions](../../research/native-api-delivery/project-tracking.md). API-group closure and individual PR completion are separate.

- Current goal: **98 required `x` groups and 5 conditional `!` groups**, each represented once below.
- Next goal: **53 deferred `?` groups**, explicitly listed at the end; no implementation work for them is included here.
- The six blank selections are removed from the working checklist and omitted as tasks. The historical source ledger remains unchanged evidence of the full audit.
- Completion checkboxes in this document start empty because work has not been performed; these are not the removed blank recommendation selections.
- This task document does not create/start an automated goal or initiate implementation, installations, publishing, PRs or merges.

## Global constraints

- Sharing logic through a .NET/host facade is a first-class implementation, not a reason to reject an API. Keep native-only opt-in extensions separate from the portable contract.
- Every group gets exact member scope, browser/native implementation mapping, capability states and contract fixtures before source implementation. Do not silently narrow a broad `x` group and then call it complete.
- Capability results distinguish implemented, built/enabled, available/permitted, unsupported and unknown; never turn missing functionality into empty successful results or fake zeros.
- Preserve observable ordering, cancellation/completion/errors, identity, precision, units, copying/transfer and ownership. Report deliberate differences rather than approximate them silently.
- Keep DOM/native callbacks on the owning thread, guard cross-session handles, dispose owned resources and test teardown. Native facades must remain AOT-safe without runtime reflection-based property dispatch.
- Reuse existing CSS math, path geometry, rendering and animation algorithms in WebCore; reuse Ranvier.Css authoring metadata and the actual core primitives. No duplicate renderer or reactive system.
- Keep engine ABI generation/locks, generated interfaces/wrappers, managed imports, packages and native feature metadata in sync. Review ABI/API compatibility before changing public signatures.
- RTK-prefixed commands, repository SageFs leases/cleanup, CRLF, commit-message files and conventional PR titles apply. Never rebase pushed branches or handle credentials. Follow the session's existing installation and release authorization when execution is actually requested.
- Existing paused work stays paused. Selecting a capability here does not silently resume macOS 15, Linux/macOS GPU, size trimming, the Anime implementation or deferred browser/media subsystems.

## Ownership and files

Paths below identify responsibilities, not permission to edit another agent's active checkout. Select clean attached worktrees at execution time and coordinate repository ownership.

- **Core/shared contract owner:** ranvier-org/Ranvier `src/Ranvier.Dom/` and `src/Fable.Ranvier.Dom/` for DOM contracts/adapters; general facade contracts/providers go in an agreed core-owned location after Task 0. Do not modify another agent's view-removal work.
- **Native engine owner:** ranvier-org/Ranvier.WebCore (inspected at `C:/wk/native-gap-interop06`): `gen/dom.json`, `gen/domgen.py`, `gen/domtypes.py`, `gen/interfaces.py`, generated `gen/dom-api.json`/interfaces, `host/RanvierWebCoreAbi.{h,cpp}`, generated DOM/glue files and managed declarations via `tests/abi/generate_native.py`. Extend generator type/constructor/namespace/callback support where needed, rather than hand-edit generated output.
- **Views/backend owner:** ranvier-org/Ranvier.Views (`C:/wk/ranvier-views`): `src/Ranvier.View.WebCore/{Host,Objects,Events,Marshalling}.fs`, generated objects/report and new focused adapter/provider files recorded in the contract manifest.
- **CSS owner:** ranvier-org/Ranvier.Css (local `C:/Users/shaya/RiderProjects/Ranvier.Css`): `src/Ranvier.Css/`, generated binding metadata under `src/Ranvier.Css.Bindings/Generated/`, and existing spec/compile/browser tests. Changes must retain the existing authoring surface.
- **Contract/acceptance tests:** extend native `tests/generator/test_domgen.py`, `tests/generator/Contracts.fsproj`, native `tests/abi/RanvierWebCoreAbi.Driver.fsproj`, Views `tests/Ranvier.View.WebCore.Tests/`, existing Fable tests and CSS spec/compile/browser projects. Add cross-target fixtures in a dedicated `tests/NativeApi.Conformance/` directory in Views, with native/browser runners wired through its build.fsx workflow.
- **Delivery evidence to create:** `docs/research/native-api-delivery/contracts.json` (per-member routes/signatures/dependencies), `capabilities.json` (per-target support), `blockers.md` (conditional/required blockers and evidence) and `acceptance.md` (fixture/build/CI results). Stable group IDs from this document are the traceability keys.

## Review focus

- Callback-triggered disposal/mutation must preserve traversal order and release resources once; no callbacks after owner disposal.
- Cross-target normalization, event ordering, inline priority and original-style restoration must agree with fixtures, not merely compile.
- A browser provider's denied or unavailable capability must not be misrepresented by a privileged native provider as universal portable behaviour.
- Typed style/geometry wrappers must preserve namespace identity, double precision and the difference between declared/parsed/computed/rendered values.
- Dependencies on deferred groups or paused platform backends must be surfaced explicitly rather than pulled into the current goal or hidden behind a no-op.

## Task 0: Fix contracts, ownership, dependency scope and executable acceptance

- [ ] Capture the checklist marks and define every selected group's per-member surface in `contracts.json`, including source provenance, exact F# public signatures and backing browser/native operations. Take clean FCS API snapshots and run refactor-impact before public signature changes.
- [ ] Assign each source/generated/provider/test file to the repository owner above and record exact paths in the manifest before editing. General facade design must not become a View-local duplicate of a core primitive.
- [ ] Create capability states and observable-result/error conventions; bind owned callbacks/cancellation to the existing Ranvier lifecycle and host dispatcher.
- [ ] Build one cross-target conformance runner through build.fsx. Share fixture inputs/assertions, use deterministic clocks for scheduling and fixed local fonts/images for rendering. Preserve meaningful actual-engine assertions; do not substitute mocks for native/browser integration.
- [ ] Record public/browser and native runtime support separately, including compile-time flags and package profiles. A feature advertised as available must have an executable provider test.
- [ ] Map all cross-group dependencies and apply the explicit deferred boundaries below; unresolved `x` dependencies are required blockers, not an implicit authorization for the next goal.

### Deferred dependency boundaries

- **I11 vs D07/D08/D18:** existing ARIA/relationship reflection can be extended now; genuine ElementInternals needs eligible component semantics. If it requires deferred registration/form-internals work, record that part as blocked, retain the selected requirement and do not mark I11 complete as if ARIA setters were equivalent.
- **C13/C14/C16 vs C15/C17:** support typed values/maps/transforms and the minimal referenced value representation needed by those selected APIs. Do not implement the deferred full numeric-math or multi-space color-operation APIs merely because those types appear in return values. Record any inseparable dependency explicitly.
- **B06 vs B01-B05/B07-B08:** implement truthful capability queries over existing providers, including unsupported/unknown outcomes. Do not enable audio/video/codecs/capture/DRM as a hidden dependency.
- **H11/H05 vs H12/H14:** implement the selected binary/file/resource contracts and their required bounded reading/callback support; do not expand to the entire deferred stream/structured-clone/message-channel families.
- **C25/H08 vs H03/H09:** supply selected inspection and cascade provenance, keeping performance-observer and tracing instrumentation out of this goal.
- **R01-R07 vs R08/R10/R11/R13:** Canvas 2D/bitmap work does not automatically enable offscreen worker ownership, WebGL, WebGPU or new native GPU transport.
- **T04/I03/I04 vs T05/T08/I05:** selected input/event payload fidelity does not claim application undo-history, richer OS IME services or gesture-recognition parity that remains deferred.
- **A06-A09/A11:** only their selected extension gates may admit implementation; they do not revive browser navigation, a JS execution environment or an entire paused graphics backend.

## Conditional extension gate: A06, A07, A08, A09 and A11

**Active execution instruction:** the user's goal narrows conditional deferral to an evidenced requirement to reintroduce dependencies or require JavaScript. Other gaps, build cost, missing generator support or unknown support are implementation work to resolve. PR creation, merging and package publication in ranvier-org are authorized without permission prompts; publish through existing trusted/OIDC workflows and never handle credentials. Focused checks run during bounded work; full suites run at batch boundaries.

For each `!` group, evaluate before substantial implementation and record one of `ready`, `blocked` or `implemented` in blockers.md; preserve the group/member and target-specific outcome.

- [ ] Inspect the pinned engine member/settings and test a minimal real call/callback path; a source declaration or generator omission alone is not a blocker or proof of support.
- [ ] Verify a native no-page-JS bridge, lifecycle/thread ownership, a compatible browser provider and a minimal conformance fixture.
- [ ] If no concrete blocker exists, implement and verify the group as current work. Routine code generation, ordinary bridging, slow builds and unmeasured difficulty are not reasons to skip it.
- [ ] If blocked, record the exact missing backend/contract/deferred dependency, compiler/runtime evidence and what would resolve it; continue independent current tasks. Do not mark the implementation complete or silently substitute a different mechanism.

Under the active instruction, an unavailable pinned implementation or difficult callback bridge is not on its own a permitted deferral: first pursue an implementation using existing dependencies and no JavaScript. A conditional item may be deferred only when evidence establishes that it requires reintroducing dependencies or JavaScript. Browser feature differences require an honest supported-profile contract, not abandonment. Required `x` work is not discretionary.

## Execution cycle for every task below

1. Pin its exact contract/files and prerequisites in Task 0's manifest.
2. Add its stated failing conformance assertion against the browser reference/native surface, then implement only the selected engine bridge or facade/provider behaviour.
3. Run the matching real browser/native assertions, error and teardown cases, then the relevant repository generation/analyzer/build checks. Commit using a message file after verification.

Each task consumes the common contract/capability/ownership conventions and produces the named API group plus evidence. A checkbox is complete only after its stated acceptance and the common definition of done pass. The phase grouping is dependency-oriented; follow the explicit dependency edges where they cross phases.

## 1. Shared contracts, ownership and value/service facades

**Owner/files:** Core shared contracts/facades; native/browser providers; reuse Ranvier lifecycle and scheduling. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **H07: Rendering capability discovery** (required x; Facade).
  - **Deliver:** report supported ABI features, renderer settings, image/font formats and enabled platform facilities instead of treating an IDL declaration as runtime support.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Distinguish implemented, enabled, permitted, available and unknown capabilities, including compiled-out media/graphics; never advertise a stub as working.

- [ ] **G07: Geometry construction/mutation** (required x; Facade).
  - **Deliver:** DOMRect factories/setters, DOMPoint/ReadOnly and DOMQuad provide reusable geometry values and point/quad bounds.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Create/mutate/copy points/rectangles/quads with double precision and compare factory defaults/bounds and mutable versus read-only behaviour.

- [ ] **G08: Double-precision transform matrices** (required x; Facade).
  - **Deliver:** DOMMatrix/ReadOnly parse, multiply, invert and apply 2D/3D transforms; a small native or managed math service may suffice.
  - **Consumes:** G07.
  - **Acceptance:** Compare matrix/matrix3d parsing, pre/post multiplication, inverse, singular matrices and point transformations against browser DOMMatrix fixtures.

- [ ] **I01: Event cancellation signals** (required x; Facade).
  - **Deliver:** AbortController/AbortSignal and listener signal options cancel groups of subscriptions and asynchronous UI work.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Test abort-once, reason/timeout/composition where selected, already-aborted subscription and listener cleanup; map native CancellationToken without changing synchronous observable state.

- [ ] **I02: Standalone/custom event construction** (required x; Qualified).
  - **Deliver:** CustomEvent.detail, typed event constructors/init dictionaries and non-node EventTargets dispatch application-defined payloads without JS objects.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Construct typed custom/UI events and compare synchronous dispatch order, detail, cancellation, target/currentTarget and non-node target lifetimes.

- [ ] **A12: Native frame/timer ownership** (required x; Facade).
  - **Deliver:** expose one consistent animation clock and owned frame/timer/microtask adapters over the existing host scheduling operations.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Run identical manual-clock/frame fixtures for one-shot and rescheduled callbacks, timers and microtasks; cancellation and owner disposal prevent subsequent invocation.

- [ ] **H01: Idle work scheduling** (required x; Qualified).
  - **Deliver:** requestIdleCallback/IdleDeadline/cancelIdleCallback run optional work when the host has spare frame budget.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Schedule/cancel owned idle callbacks, test timeouts and a busy frame; deadline estimates have defined bounds and are not reported as guaranteed future idle time.

- [ ] **H02: Monotonic time and profiling marks** (required x; Facade).
  - **Deliver:** Performance.now/timeOrigin/mark/measure expose a shared timing domain; .NET Stopwatch and existing frame timestamps may meet the requirement.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Verify monotonic units/time origins and mark/measure ordering with a deterministic clock, including reset and concurrent names.

- [ ] **H06: Locale/language/time-zone services** (required x; Qualified).
  - **Deliver:** expose application locale and preferred languages for text, sorting and formatting; use .NET/ICU instead of adding a browser Navigator merely for these values.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Read/update preferred locale/languages/time zone and test canonicalization plus provider differences; no browser Navigator object is required natively.

- [ ] **H11: Binary/file value objects** (required x; Qualified).
  - **Deliver:** Blob/File/FileList/FileReader supply file/image/clipboard/media payloads; .NET buffers/streams/file capabilities can back a shared facade.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Create/slice/read immutable binary and file values with browser/native providers; verify byte content, metadata, cancellation and access/disposal boundaries without implementing the deferred full H12 stream surface.

- [ ] **H13: URL parsing/pattern matching** (required x; Qualified).
  - **Deliver:** URL/URLSearchParams/URLPattern support resource identifiers and route matching without implementing browser navigation.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Compare relative resolution, normalization, encoding, queries and selected URLPattern semantics against browser fixtures; System.Uri/regex differences must be resolved or explicitly qualified.

- [ ] **B22: Crypto** (required x; Qualified).
  - **Deliver:** Web Crypto random/digest/key operations can share a supported algorithm/key contract across browser and established native/.NET providers.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Run published vectors for the agreed algorithm/encoding set and verify random/key lifetime/import/export/error behaviour through established providers; unavailable algorithms return explicit outcomes.

## 2. DOM reflection, forms and component semantics

**Owner/files:** Native IDL generation and shared DOM contracts; Views typed adapters and conformance fixtures. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **D01: Filtered tree traversal** (required x; Engine).
  - **Deliver:** `Document.createTreeWalker/createNodeIterator`, TreeWalker, NodeIterator and NodeFilter traverse selected node kinds without hand-written recursive walks.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** A mixed node tree traverses in identical order for whatToShow/filter accept/skip/reject cases; iterator/walker roots and callback handles are released after disposal.

- [ ] **D09: SVG/MathML shared element members** (required x; Engine).
  - **Deliver:** fill the SVG style/dataset/focus/tabIndex/nonce/autofocus gaps and MathMLElement-specific adapters; low-level SVG style writes already exist.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** An SVG/MathML node supports selected style/data/focus members through the shared interface; test SVG style priority/removal, focus/tab order and namespace identity without treating a node as HTMLElement.

- [x] **D10: HTML table object APIs** (required x; Engine).
  - **Deliver:** HTMLTableElement/Section/Row/Cell/Col/Caption collections, insertion/deletion and reflected properties support grids and rich table editors.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Create a table via existing View tags, insert/delete rows/cells through specialized APIs and verify row/section collections and reflected spans against browser results.

- [ ] **D11: Additional HTML semantic/reflection types** (required x; Engine).
  - **Deliver:** typed properties for html/base/br/hr/headings/quote/time/data/mod/pre/list/menu/map/picture/source/selected-content and other tag classes avoid relying solely on raw attributes.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Create representative existing tag kinds, set properties and attributes in both directions and compare reflected values/defaults; prove this extends typed access without replacing View element construction.

- [ ] **D13: Specialized form collections/events** (required x; Engine).
  - **Deliver:** HTMLFormControlsCollection, HTMLOptionsCollection, FormDataEvent and SubmitEvent expose control collections, submitter and collected form data.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Verify named/indexed form/option lookup and a typed submitter/form-data event, including duplicate names and removed controls.

- [ ] **D14: Form submission semantics** (required x; Qualified).
  - **Deliver:** requestSubmit/submit and form-data construction/serialization run native application validation/submit workflows without implying network navigation.
  - **Consumes:** D13, I02.
  - **Acceptance:** Compare submit versus requestSubmit validation/event ordering and cancellation; native final submission invokes the configured application action and performs no implicit network navigation.

- [ ] **D16: Specialized dialog/popover/command payloads** (required x; Engine).
  - **Deliver:** ToggleEvent, CommandEvent and CloseWatcher expose transition causes, command sources and close requests beyond generic event names.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Capture open/close and command-source payloads and close-request cancellation; removing a listener or disposing the mount prevents later callbacks.

- [ ] **D17: DOM construction and value factories** (required x; Qualified).
  - **Deliver:** fill unsupported constructors/static factories for otherwise exported node/event/geometry types so native code can create required standalone values.
  - **Consumes:** G07, I02.
  - **Acceptance:** Construct independent geometry values and synthetic events, check factory defaults/mutability and dispatch outcomes, and reject cross-session/document handles clearly.

- [ ] **I11: Component accessibility internals** (required x; Qualified).
  - **Deliver:** ElementInternals and reflected ARIA element relationships expose component semantics without duplicating the existing AX tree and ARIA attribute setters.
  - **Consumes:** D09/D11 and the existing ARIA/AX surface; D07/D08 remain deferred (see dependency boundary).
  - **Acceptance:** Verify shared roles/states/element relationships and resulting semantics; record an explicit blocker if ElementInternals eligibility needs deferred D07/D08 rather than claiming ordinary ARIA setters implement those internals.

## 3. Stylesheets and Ranvier.Css integration

**Owner/files:** Native CSSOM bridges; shared DOM/CSS value contracts; Ranvier.Css runtime/bindings; Views style adapters. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **D12: Stylesheet/link element objects** (required x; Engine).
  - **Deliver:** HTMLStyleElement/HTMLLinkElement and LinkStyle.sheet expose associated sheets, media, loading state and enable/disable behaviour.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Discover the actual sheet attached to a style/link element, toggle disabled/media state, replace content and observe completion/error; extracted Ranvier.Css output remains correctly associated.

- [ ] **C01: Computed-style reads** (required x; Engine).
  - **Deliver:** wrap `rv_element_computed_style` as an owned/typed F# operation for resolved style values; the native operation already exists.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Cascade inline/inherited/stylesheet values and assert computed opacity/width/custom properties, including absence and post-disposal error handling.

- [ ] **C02: Pseudo-element computed styles** (required x; Engine).
  - **Deliver:** extend computed-style querying with a pseudo-element argument for `::before`, `::after` and other generated content.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Read distinct ::before/::after computed values; compare absent pseudo-element behaviour and reject invalid pseudo syntax consistently.

- [ ] **C03: CSS property/condition support queries** (required x; Engine).
  - **Deliver:** CSS.supports tests whether the live renderer accepts a property/value or supports-condition; catalogue membership and valid-value testing are different operations.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Test supported, invalid and unknown property/value/condition inputs against the actual renderer; catalogue membership alone must not return support.

- [ ] **C04: Typed CSS property access** (required x; Facade).
  - **Deliver:** reuse Ranvier.Css names, grammar markers, literal/unit metadata and serialization for typed native style/animation operations.
  - **Consumes:** C01 and existing Ranvier.Css metadata.
  - **Acceptance:** Use the same Ranvier.Css property/value expression against browser and native sinks; verify grammar mismatch rejection, numeric/unit metadata and inline priority/restoration.

- [x] **C05: CSS identifier escaping** (required x; Facade). Accepted2026-10-30: [WebCore issue30](https://github.com/ranvier-org/Ranvier.WebCore/issues/30);45 assertions in JIT/NativeAOT on all four supported native platforms,45 Chromium assertions plus196,609 oracle cases, and official normal/traced native/Fable Core consumers. The Core-only function has no new Views-backend publication dependency.
  - **Deliver:** CSS.escape produces safe selector identifiers from arbitrary application strings.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Run shared CSS.escape vectors for leading digits, whitespace, punctuation, null/control and non-ASCII characters, then resolve an element through the resulting selector.

- [ ] **C06: Stylesheet discovery/metadata** (required x; Engine).
  - **Deliver:** StyleSheetList/StyleSheet and Document.styleSheets expose sheets, ownership, media, disabled state and linkage.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Enumerate live sheets with ownership/media/disabled state before and after insertion/removal; do not equate source text with parsed cssRules.

- [ ] **C07: Stylesheet construction/replacement/adoption** (required x; Engine).
  - **Deliver:** CSSStyleSheet constructors/replace/replaceSync and Document/ShadowRoot.adoptedStyleSheets install reusable stylesheets.
  - **Consumes:** C06.
  - **Acceptance:** Construct/replace/adopt a sheet in document and shadow scopes; verify rule effects and replacement errors without corrupting a previously valid sheet.

- [ ] **C08: Rule enumeration and mutation** (required x; Engine).
  - **Deliver:** CSSRule/CSSRuleList, CSSStyleRule and grouping-rule insertRule/deleteRule expose selectors, declaration blocks, cssText and nested rules.
  - **Consumes:** C06/C07.
  - **Acceptance:** Insert/edit/delete nested rules and compare parsed selectors/declarations and resulting computed styles; test invalid syntax/index errors.

- [ ] **C09: Conditional rules and media lists** (required x; Engine).
  - **Deliver:** CSSMediaRule/MediaList, CSSSupportsRule and CSSContainerRule inspect and edit media/supports/container conditions.
  - **Consumes:** C08, G01 for live media changes.
  - **Acceptance:** Inspect media/supports/container condition text and media-list mutation; assert computed changes after a viewport/container update where the feature is enabled.

- [ ] **C10: Cascade structure** (required x; Engine).
  - **Deliver:** CSSLayerBlockRule/StatementRule, CSSScopeRule, CSSNestedDeclarations, CSSImportRule and CSSNamespaceRule expose layers, scopes, nesting and imports.
  - **Consumes:** C08.
  - **Acceptance:** Read and edit representative layers/scopes/nested/import/namespace structures and verify cascade results; unavailable optional rules produce explicit capability outcomes.

- [ ] **C11: CSS keyframe rules** (required x; Engine).
  - **Deliver:** CSSKeyframesRule/CSSKeyframeRule append/find/delete keyframes and edit animation names, offsets and declarations.
  - **Consumes:** C08.
  - **Acceptance:** Create/find/delete/edit a named keyframe rule and verify the associated CSS animation observes updated offsets/declarations.

- [ ] **C12: Custom-property registration** (required x; Engine).
  - **Deliver:** CSS.registerProperty and CSSPropertyRule define custom-property grammar, inheritance and initial values for native interpolation.
  - **Consumes:** C03.
  - **Acceptance:** Register a typed custom property and test initial value, inheritance, invalid registration and midpoint interpolation; restoring inline values keeps original priority.

- [ ] **C13: Typed style maps** (required x; Engine).
  - **Deliver:** attributeStyleMap/computedStyleMap, StylePropertyMap/ReadOnly and CSSStyleRule.styleMap read/write typed values instead of CSS text.
  - **Consumes:** C01/C04; C15 remains deferred.
  - **Acceptance:** Read/write typed declared/computed maps, enumerate entries and compare canonical values; numeric values needed as opaque/map data do not implicitly implement deferred C15 math APIs.

- [ ] **C14: Typed CSS value parsing** (required x; Engine).
  - **Deliver:** CSSStyleValue.parse/parseAll, keyword/image/unparsed values and variable references preserve structured CSS values.
  - **Consumes:** C04; C15/C17 remain deferred.
  - **Acceptance:** Parse valid/invalid representative CSS values with parse/parseAll and verify structure/serialization; distinguish grammar acceptance from interpolatability.

- [ ] **C16: Typed transform values** (required x; Engine).
  - **Deliver:** CSSTransformValue/components represent translate/rotate/scale/skew/perspective/matrix operations and convert them to matrices.
  - **Consumes:** G08, C14.
  - **Acceptance:** Build/read transform components and compare resulting matrices for ordered translate/rotate/scale/skew/perspective, using G08 double-precision conventions.

- [ ] **C18: Font rules/descriptors** (required x; Engine).
  - **Deliver:** CSSFontFaceRule/Descriptors, font-feature-values and font-palette-values rules inspect and edit font configuration.
  - **Consumes:** C08; T01/T02 for load-state checks.
  - **Acceptance:** Inspect parsed @font-face descriptors and feature/palette rules where supported; loaded font/render checks are separate from faithful descriptor inspection.

- [ ] **C19: Counter styles** (required x; Engine).
  - **Deliver:** CSSCounterStyleRule exposes generated list/counter formatting systems, symbols, prefixes and fallbacks.
  - **Consumes:** C08.
  - **Acceptance:** Inspect/edit a custom counter rule and verify symbols/range/prefix/fallback produce the expected list marker representation in the renderer.

- [ ] **C21: Position fallback rules** (required x; Engine).
  - **Deliver:** CSSPositionTryRule/Descriptors expose anchor-positioning fallback geometry and alignment declarations.
  - **Consumes:** C08, G06.
  - **Acceptance:** Inspect/edit supported @position-try descriptors and verify a constrained anchored element chooses the corresponding fallback; report unsupported feature state explicitly.

- [ ] **C22: Starting-style/view-transition rules** (required x; Engine).
  - **Deliver:** CSSStartingStyleRule/CSSViewTransitionRule inspect entry-transition styles and transition types/navigation policy.
  - **Consumes:** C08; A11 is conditional, not required for rule inspection.
  - **Acceptance:** Inspect supported @starting-style/view-transition rule structures; rule inspection passes independently of conditional A11 transition execution.

- [ ] **C25: Matched-rule/cascade diagnostics** (required x; Platform).
  - **Deliver:** expose relevant renderer rule matching, specificity/origin and resolved-property provenance for native inspection tools; existing computed values alone do not explain the cascade.
  - **Consumes:** C06/C08/C01, H08.
  - **Acceptance:** For a conflicting declaration fixture, identify contributing rules/origin/order and the winner; declare browser diagnostics availability instead of fabricating provenance from the final computed value.

## 4. Rendering environment, geometry and observers

**Owner/files:** Native WebCore geometry/observer bridges plus browser/host environment providers. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **G01: Media-query evaluation/subscription** (required x; Engine).
  - **Deliver:** matchMedia and MediaQueryList.matches/change expose viewport, pointer, hover, color-scheme, contrast, reduced-motion and other renderer-supported conditions.
  - **Consumes:** H07 and native host preference delivery.
  - **Acceptance:** Evaluate width, pointer and reduced-motion conditions; change host/viewport state and verify matches plus one owned change subscription with no callback after removal.

- [ ] **G02: Viewport dimensions/device scale** (required x; Qualified).
  - **Deliver:** expose coherent CSS-pixel inner/outer dimensions and devicePixelRatio from the host; existing resize/scale setters and paint-pixel size do not provide the complete read surface.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Resize and change device scale; verify CSS viewport dimensions versus physical paint dimensions and consistent change events, including fractional scale.

- [ ] **G03: Visual viewport** (required x; Qualified).
  - **Deliver:** VisualViewport offsets/size/scale and resize/scroll notifications expose zoomed or occluded viewport geometry.
  - **Consumes:** G02/G12.
  - **Acceptance:** Check offset/size/scale updates for supported zoom/occlusion scenarios; unavailable browser/native features are reported rather than returned as fake zeros.

- [ ] **G04: Screen/display information** (required x; Qualified).
  - **Deliver:** Screen/ScreenOrientation expose monitor/work-area dimensions, color depth, orientation and orientation changes through the application's display integration.
  - **Consumes:** H07/G02.
  - **Acceptance:** Read monitor/work-area/orientation values and deliver changes; unsupported orientation control returns a capability failure without inventing support.

- [ ] **G05: Fractional viewport scrolling** (required x; Engine).
  - **Deliver:** extend integer host scroll positions to double precision and expose scroll options/relative scrolling without subpixel truncation.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Set/read a fractional scroll position and relative scroll/options; compare fractional values and clamping against browser output.

- [ ] **G06: Fragment rectangle lists** (required x; Engine).
  - **Deliver:** Element/Range.getClientRects and DOMRectList expose each rendered line/fragment rather than only the union bounding rectangle.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Measure wrapped text and fragmented inline content; verify each rectangle and its ordering plus the bounding union, including an empty range.

- [ ] **G09: Caret hit testing** (required x; Engine).
  - **Deliver:** Document.caretPositionFromPoint/CaretPosition map a viewport point to a text node/offset and caret rectangle.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Hit-test positions around wrapped text and compare node/offset/caret rectangles, including outside-content and invalid-coordinate outcomes.

- [ ] **G10: Resize observation options/payloads** (required x; Engine).
  - **Deliver:** existing callbacks give width/height; expose requested box options and content/border-box entries, logical dimensions and richer entry data.
  - **Consumes:** G06, I01.
  - **Acceptance:** Observe content/border boxes with logical dimensions, verify notification order/initial observation and stop callbacks after unobserve/dispose.

- [ ] **G11: Intersection observation options/payloads** (required x; Engine).
  - **Deliver:** existing viewport-root observers support root margins/thresholds; add element roots, scrollMargin, root/intersection rectangles, timestamps and queued-record access.
  - **Consumes:** G06/G05, I01.
  - **Acceptance:** Observe viewport and element-root intersections with margins/thresholds; verify rectangles/timestamps/ratio and takeRecords/disconnection without stale target handles.

- [ ] **G12: Viewport/document event routing** (required x; Qualified).
  - **Deliver:** consistently deliver resize, scroll/scrollend, visibility, focus and scale changes from the native window into owned callbacks.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Drive resize/scroll/visibility/focus/scale changes through host adapters and compare documented delivery/order, then verify teardown unsubscribes everything.

- [ ] **G14: Coordinate-space conversion** (required x; Qualified).
  - **Deliver:** expose page/viewport/element/display conversion where existing bounds/matrices are insufficient for transformed overlays and native hit testing.
  - **Consumes:** G02/G05/G08.
  - **Acceptance:** Round-trip named coordinate spaces under nested transforms, scrolling and device scaling; invalid cross-host conversions fail explicitly.

- [ ] **G15: OS appearance and input preferences** (required x; Qualified).
  - **Deliver:** route theme, contrast, accessibility motion, text scale and pointer capabilities into WebCore/media evaluation; querying a preference and supplying its native value are separate tasks.
  - **Consumes:** G01/G12.
  - **Acceptance:** Change supported theme/motion/contrast/input preferences and verify both queries and subscriptions observe the supplied native state; unknown preferences stay explicit.

## 5. Animation control and conditional extensions

**Owner/files:** Native WebCore animation bridges; shared animation contracts and browser/native tests. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **A01: Keyframe animation creation** (required x; Engine).
  - **Deliver:** Animatable.animate, KeyframeEffect and Animation constructors create renderer-managed animations from properties/keyframes/timing options.
  - **Consumes:** C01/C12, A12.
  - **Acceptance:** Create opacity/transform keyframes, pause/seek to start/midpoint/end and compare rendered/computed results with browser, including delay/direction/composition options.

- [ ] **A02: Effect inspection/mutation** (required x; Engine).
  - **Deliver:** getTiming/getComputedTiming/updateTiming, getKeyframes/setKeyframes, target/pseudoElement and composite settings inspect and retarget existing effects.
  - **Consumes:** A01.
  - **Acceptance:** Read/update timings and keyframes, retarget an effect and verify computed timing/result values; test invalid options and disposed targets.

- [ ] **A03: Animation lifecycle/state** (required x; Engine).
  - **Deliver:** extend existing play/pause/reverse/finish/cancel/time/rate controls with startTime, pending/replace state, updatePlaybackRate, persist, ready/finished and finish/cancel/remove callbacks.
  - **Consumes:** A01/A12, I01/I02.
  - **Acceptance:** Compare play/pause/reverse/rate/finish/cancel state and completion ordering, including cancellation during a callback and disposal before completion.

- [ ] **A04: Commit animated styles** (required x; Engine).
  - **Deliver:** Animation.commitStyles writes an effect's current result into inline styles before cancellation.
  - **Consumes:** A01/A02, C01.
  - **Acceptance:** Seek to a midpoint, commitStyles then cancel; the element retains the committed value while unrelated inline properties/priority remain unchanged.

- [ ] **A05: Animation discovery/identity** (required x; Engine).
  - **Deliver:** extend existing document/element enumeration with complete ids, effect/timeline links and CSSAnimation/CSSTransition-specific metadata.
  - **Consumes:** A01/A02.
  - **Acceptance:** Enumerate document/element/subtree animations and correlate stable ids, effect/target/timeline plus CSS animation/transition metadata without leaking retained handles.

- [ ] **A06: Document/custom timelines** (conditional ! - implement if no blockers; Engine).
  - **Deliver:** Document.timeline, DocumentTimeline and AnimationTimeline expose shared time origins, durations, custom timeline construction and frame-rate controls.
  - **Consumes:** A01/A02/A12 and its conditional blocker gate.
  - **Acceptance:** Gate custom timeline/frame-rate members separately; for supported members compare time origin, inactive/null current time, synchronized effects and teardown against browser traces.
  - **Conditional boundary:** Recommend document timeline access; gate custom timeline/frame-rate features individually and specify time-origin/null-time semantics.
  - **Gate evidence:** The group includes custom construction and frame-rate controls beyond simply reading Document.timeline; those members may have different support/settings and ownership rules.
 Resolve this with a real bounded probe; do not assume it is blocked.

- [ ] **A07: Scroll-driven timelines** (conditional ! - implement if no blockers; Engine).
  - **Deliver:** ScrollTimeline binds animation progress to a scroller and axis instead of elapsed time.
  - **Consumes:** G01/G05, A01/A02, conditional timeline gate.
  - **Acceptance:** Gate pinned scroll-timeline support; for supported targets verify axis, source, range start/mid/end, nested scrollers and resizes without substituting an unlabelled timer approximation.
  - **Conditional boundary:** Expose engine timelines when supported; declare an explicit fallback policy instead of silently claiming full equivalence.
  - **Gate evidence:** Feature support, range interpretation and compositor behaviour need validation across the targeted browser and native builds; an emulation is not automatically timing-equivalent.
 Resolve this with a real bounded probe; do not assume it is blocked.

- [ ] **A08: View-driven timelines/ranges** (conditional ! - implement if no blockers; Engine).
  - **Deliver:** ViewTimeline and animation range offsets bind progress to an element's visibility through a scrollport.
  - **Consumes:** G06/G11, A07 where required, conditional timeline gate.
  - **Acceptance:** Gate view-timeline/range support; verify entry/exit offsets, subject geometry, nested scrollports and range transitions against browser measurements.
  - **Conditional boundary:** Test range boundaries, nested scrollers and subject geometry; allow capability-based fallback when a target lacks the feature.
  - **Gate evidence:** The group depends on view-timeline/range support and complex layout/scroll cases, so an export alone does not prove compatibility.
 Resolve this with a real bounded probe; do not assume it is blocked.

- [ ] **A09: Native custom animation effects** (conditional ! - implement if no blockers; Qualified).
  - **Deliver:** CustomEffect and DocumentTimeline.animate drive application callbacks from renderer timing without JS callbacks.
  - **Consumes:** A12/I01, conditional non-JS callback gate.
  - **Acceptance:** Gate a safe native non-JS callback path; verify frame ordering, callback mutation/reentrancy, exception routing and no callbacks after disposal using the same public effect contract.
  - **Conditional boundary:** Keep a small callback/effect contract and use Ranvier ownership; decide whether parity needs a CustomEffect bridge or the already-shared frame scheduler.
  - **Gate evidence:** The existing engine interface is JS-callback-facing; native callback ownership, reentrancy, exception delivery and frame order require a real bridge.
 Resolve this with a real bounded probe; do not assume it is blocked.

- [ ] **A10: Typed animation/transition events** (required x; Engine).
  - **Deliver:** expose animationName/propertyName, elapsedTime, pseudoElement and playback event times; generic event listeners already exist.
  - **Consumes:** I02/I08, A03.
  - **Acceptance:** Capture typed animation/transition and playback events with expected names, times and pseudo-element metadata; generic event subscription behaviour remains intact.

- [ ] **A11: View transitions** (conditional ! - implement if no blockers; Qualified).
  - **Deliver:** Document.startViewTransition/activeViewTransition and ViewTransition ready/finished/types/skipTransition coordinate snapshots between UI states.
  - **Consumes:** A01/A03, G06/R12 as needed, conditional same-document transition gate.
  - **Acceptance:** Gate same-document transition availability; verify update callback completion, ready/finished/skip/rejection paths, pseudo-state cleanup and interruption without adding browser navigation semantics.
  - **Conditional boundary:** Start with same-document transitions, preserve ready/update/finished outcomes, and leave browser navigation transitions outside that initial contract.
  - **Gate evidence:** Browser document/navigation transitions and native snapshot/presentation lifecycles do not automatically match; cancellation, callback completion and feature support need agreement.
 Resolve this with a real bounded probe; do not assume it is blocked.

## 6. Fonts, text and transferable UI payloads

**Owner/files:** Engine font/highlight/input bridges and shared provider contracts; host clipboard/resource integration. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **T01: Font loading/readiness** (required x; Engine).
  - **Deliver:** Document.fonts and FontFaceSet status/ready/check/load/events report when measurements can rely on loaded fonts.
  - **Consumes:** H05/I01.
  - **Acceptance:** Load a delayed local font and verify status/check/load/ready/error notifications, then measure only after actual readiness; disposal cancels owned completion listeners.

- [ ] **T02: Font construction/registration** (required x; Qualified).
  - **Deliver:** FontFace descriptors, byte-backed sources and FontFaceSet add/delete/clear supply application-controlled fonts.
  - **Consumes:** T01/H11.
  - **Acceptance:** Register fonts from bytes and resource URLs, verify descriptor/state errors, add/delete/clear and reload behaviour, with supported format differences reported.

- [ ] **T03: Custom highlights** (required x; Engine).
  - **Deliver:** Highlight/HighlightRegistry and CSS.highlights style arbitrary text ranges for search matches, diagnostics and collaborative cursors.
  - **Consumes:** C08 and existing Range/selection.
  - **Acceptance:** Create overlapping range highlights, update/delete registry entries and assert their styles without modifying original text or leaking range handles.

- [ ] **T04: Rich beforeinput/input payloads** (required x; Engine).
  - **Deliver:** expose InputEvent.dataTransfer and any remaining target-range construction/payload gaps for structured editor operations.
  - **Consumes:** I02/I07 and existing ranges.
  - **Acceptance:** Process a replacement beforeinput operation with target ranges and transfer payloads; cancellation prevents the edit and typed data remains valid only for its documented lifetime.

- [ ] **T06: Text segmentation** (required x; Qualified).
  - **Deliver:** expose/reuse ICU word/grapheme/line boundaries or equivalent .NET services for text splitting and caret movement; Intl.Segmenter is JS-runtime functionality, not a missing WebCore IDL interface.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Compare grapheme/word/line fixtures with combining marks, emoji and locale-sensitive words; record Unicode/provider version differences and never split a grapheme accidentally.

- [ ] **T09: Clipboard formats and file payloads** (required x; Qualified).
  - **Deliver:** connect ClipboardEvent/DataTransfer structured formats, images and files to existing host clipboard callbacks.
  - **Consumes:** H11/I07.
  - **Acceptance:** Round-trip text/HTML/image/file payloads and cancellation through browser event access/native clipboard providers; permission denial and unavailable formats are explicit.

- [ ] **H05: Font/image/resource completion and failures** (required x; Qualified).
  - **Deliver:** expose owned load/decode/error completion through the existing host resource mechanism, with cancellation and cache control where needed.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Exercise successful/failed/cancelled font/image/resource loads with exactly one terminal outcome and correct delivery on the owning UI context.

## 7. SVG and MathML

**Owner/files:** Native scoped SVG/MathML generation, value handles and Views typed wrappers. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **S01: SVG graphics bounds/transforms** (required x; Engine).
  - **Deliver:** SVGGraphicsElement.getBBox/getCTM/getScreenCTM and viewBox/aspect-ratio state expose actual SVG coordinate systems and bounds.
  - **Consumes:** G06/G08.
  - **Acceptance:** Measure transformed/nested SVG graphics bounds and CTM/screenCTM; compare viewBox/aspect-ratio effects with browser results.

- [ ] **S02: SVG geometry sampling/hit testing** (required x; Engine).
  - **Deliver:** SVGGeometryElement getTotalLength/getPointAtLength/isPointInFill/isPointInStroke supply path metrics and precise fill/stroke tests.
  - **Consumes:** S01.
  - **Acceptance:** For a 100-unit line verify length 100 and midpoint (50,0); test curved paths and fill/stroke hit testing in the correct coordinate space.

- [ ] **S03: SVG shape-specific reflection** (required x; Engine).
  - **Deliver:** path/circle/ellipse/line/rect/polygon/polyline properties and animated lengths/point lists expose editable shape geometry.
  - **Consumes:** S02.
  - **Acceptance:** Change shape-specific base/animated geometry and verify attributes, lists and resulting bounds without an HTMLElement cast.

- [ ] **S04: SVG value/list factories** (required x; Engine).
  - **Deliver:** SVGLength/Angle/Number/Point/Rect/Matrix/Transform, list mutation and animated wrappers manipulate native SVG values and unit conversions.
  - **Consumes:** G07/G08.
  - **Acceptance:** Create/convert SVG lengths/angles/transforms and edit value lists; compare unit conversions and base/animated/read-only behaviour.

- [ ] **S05: SVG text metrics/positioning** (required x; Engine).
  - **Deliver:** SVGTextContentElement/TextPositioningElement/TextPath expose character extents, substring lengths, positions, rotation and text-path properties.
  - **Consumes:** S01/T01.
  - **Acceptance:** Measure per-character positions/extents/rotation and substring lengths on plain and path text; verify invalid-index handling.

- [ ] **S06: SVG root/view controls** (required x; Engine).
  - **Deliver:** SVGSVGElement/SVGViewSpec/SVGViewElement expose root factories, viewport/viewBox, intersection/enclosure queries and view state.
  - **Consumes:** S01/S04.
  - **Acceptance:** Exercise SVG root/view factories, viewport/viewBox and intersection/enclosure queries on a known diagram; dispose all returned handles.

- [ ] **S07: SVG animation controls** (required x; Engine).
  - **Deliver:** SVGAnimationElement and SVGSVGElement timing methods control SMIL start/end, current time, pause and target information.
  - **Consumes:** A03/A12.
  - **Acceptance:** Start/end/seek/pause/resume an SVG animation and verify its target/current time and rendered result against browser output.

- [ ] **S08: SVG gradients/patterns/stops** (required x; Engine).
  - **Deliver:** gradient, pattern and stop APIs edit paint-server coordinates, transforms, colors and inheritance.
  - **Consumes:** S03/S04.
  - **Acceptance:** Edit gradient/pattern/stop coordinates and colors and verify both reflected values and rendered paint-server usage.

- [ ] **S09: SVG filters** (required x; Engine).
  - **Deliver:** SVGFilterElement and all filter primitive/lighting/component-transfer types expose filter parameters without reconstructing markup strings.
  - **Consumes:** C03/S03.
  - **Acceptance:** Change representative blur/color-matrix/lighting/component-transfer parameters and verify introspected values and corresponding rendered changes.

- [ ] **S10: SVG clipping/masks/markers** (required x; Engine).
  - **Deliver:** clipPath/mask/marker APIs expose geometry, units, orientation and reference behaviour.
  - **Consumes:** S01/S03.
  - **Acceptance:** Change clip/mask/marker units, geometry and orientation and verify reflected state plus actual clipping/marker output.

- [ ] **S11: SVG resource/reuse/content types** (required x; Engine).
  - **Deliver:** use/image/foreignObject/a/style/symbol/defs/switch/metadata/desc/title and URI-reference/test mixins expose remaining specialized SVG state.
  - **Consumes:** D09/H05.
  - **Acceptance:** Exercise modern SVG use/image/foreignObject/link/style/metadata interfaces and URI references; verify specialized typed reflection and resource/error handling.

- [ ] **S13: MathML typed element surface** (required x; Engine).
  - **Deliver:** MathMLElement/MathMLMathElement and shared style/focus/data members expose native mathematical markup beyond generic attributes.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Construct mathematical markup and verify selected typed/style/focus/data members and namespace identity against browser.

## 8. Canvas, image readiness and output

**Owner/files:** Native canvas/image bridges; common binary/bitmap ownership and browser/native rendering fixtures. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **R01: Canvas 2D context lifecycle** (required x; Engine).
  - **Deliver:** HTMLCanvasElement.getContext and CanvasRenderingContext2D expose a renderer-backed immediate drawing surface.
  - **Consumes:** H07/I01.
  - **Acceptance:** Create/release a 2D context and draw a deterministic surface; verify context identity/reset and explicit failure for unsupported context types.

- [ ] **R02: Canvas paths and hit testing** (required x; Engine).
  - **Deliver:** CanvasPath/Path2D and fill/stroke/clip/isPointInPath/isPointInStroke draw and test reusable paths.
  - **Consumes:** R01/G08.
  - **Acceptance:** Reuse Path2D paths for fill/stroke/clip and hit testing; compare points inside/outside transformed paths.

- [ ] **R03: Canvas state/transforms/compositing** (required x; Engine).
  - **Deliver:** save/restore/reset, transforms, blend modes, alpha, filters, clipping and layers control drawing state.
  - **Consumes:** R01/G08.
  - **Acceptance:** Verify save/restore/reset and transform/alpha/composite/filter state across consecutive drawing operations with pixel assertions.

- [ ] **R04: Canvas paints** (required x; Engine).
  - **Deliver:** gradients, patterns, line/fill/stroke/shadow/smoothing styles configure drawing appearance.
  - **Consumes:** R01.
  - **Acceptance:** Draw deterministic gradients/patterns/strokes/shadows and assert paint parameters plus representative pixels within defined raster tolerances.

- [ ] **R05: Canvas text** (required x; Engine).
  - **Deliver:** draw/measure text and TextMetrics expose widths, baselines and bounding boxes for custom controls/charts.
  - **Consumes:** R01/T01.
  - **Acceptance:** Draw/measure text with a bundled ready font and compare width/baseline/bounds within stated tolerances; unknown/deferred glyph-run diagnostics are not fabricated.

- [ ] **R06: Canvas image/pixel operations** (required x; Qualified).
  - **Deliver:** drawImage, ImageData, get/putImageData and color-space/pixel-format choices move raster data into/out of canvases.
  - **Consumes:** R01/H11.
  - **Acceptance:** Round-trip a small pixel buffer through get/put/draw operations with explicit format/stride/alpha/color space and bounds/error checks.

- [ ] **R07: Canvas export/bitmap transport** (required x; Qualified).
  - **Deliver:** toBlob/toDataURL, ImageBitmap/createImageBitmap, bitmap-rendering contexts and close/transfer operations exchange image results.
  - **Consumes:** R06/H11.
  - **Acceptance:** Export/import a bitmap/blob and verify dimensions/content, disposal and copy/transfer ownership; unsupported formats return explicit errors.

- [ ] **R09: Image readiness and decoding** (required x; Engine).
  - **Deliver:** HTMLImageElement.decode provides asynchronous decode completion beyond already exported image properties/load events.
  - **Consumes:** H05.
  - **Acceptance:** Wait for a delayed image decode, assert completion/failure and use it for drawing; no stale callback survives owner disposal.

- [ ] **R12: Snapshot/export options** (required x; Qualified).
  - **Deliver:** extend existing host/element RGBA snapshots with desired scale, crop, color-space, encoding and asynchronous readback options.
  - **Consumes:** H11/G02/R07.
  - **Acceptance:** Capture/crop/scale/encode supported snapshots; report arbitrary DOM capture unavailable in ordinary browser contexts rather than silently change the requested subject.

## 9. Input, host services and diagnostics

**Owner/files:** Host/browser providers with shared contracts; native diagnostic extensions remain explicitly optional. Use the exact files fixed in the manifest and the shared/native/test owners above.

- [ ] **I03: Touch event payloads and injection** (required x; Qualified).
  - **Deliver:** Touch/TouchList/TouchEvent expose coordinates, identity, force/radii and changed touches, with a matching native input delivery path.
  - **Consumes:** I02/I12 and native input injection.
  - **Acceptance:** Deliver touch start/move/end/cancel with changed-touch identity/coordinates/force and verify the host input path, not just fabricated object getters.

- [ ] **I04: Pen/pointer fidelity** (required x; Qualified).
  - **Deliver:** audit/extend pointer type/pressure/tilt/twist/contact geometry, device ids and input injection beyond existing Mouse/PointerEvent getters and pointer capture.
  - **Consumes:** I02/I12 and native input injection.
  - **Acceptance:** Deliver pointer/pen type, pressure, tilt/twist/contact geometry and capture changes where hardware/provider supports them; unavailable fields/capabilities are explicit.

- [ ] **I06: Relative pointer capture/lock** (required x; Qualified).
  - **Deliver:** pointer-lock requests/state and relative movement support drag-intensive editors or viewports under native window policy.
  - **Consumes:** I12/I02/H07.
  - **Acceptance:** Acquire/lose relative pointer capture and verify movement, activation/denial and teardown; distinguish browser pointer lock from ordinary element capture.

- [ ] **I07: Drag/drop object model** (required x; Qualified).
  - **Deliver:** DataTransfer/Item/ItemList/FileList and DragEvent expose files, format lists, effects and drag images beyond existing string-oriented transport.
  - **Consumes:** H11/I02.
  - **Acceptance:** Drag/drop multiple formats and files, inspect item lists/effects/images and verify file capability lifetime plus cancellation and cleanup.

- [ ] **I08: Event detail adapters** (required x; Engine).
  - **Deliver:** typed ErrorEvent, ProgressEvent, animation/transition, content-visibility, form/toggle/command and other UI event records expose useful fields instead of only event names.
  - **Consumes:** I02.
  - **Acceptance:** Exercise representative dialog/form/content-visibility/animation/error event payloads and defaults, proving typed access beyond event names.

- [ ] **I12: Native focus/activation/input capability state** (required x; Qualified).
  - **Deliver:** expose host window activation, keyboard modality, touch-point capacity and capture changes consistently to UI logic.
  - **Consumes:** Task 0 contract/capability/ownership infrastructure and existing host/DOM primitives.
  - **Acceptance:** Change activation/focus/input modality/capture through host/browser providers and verify shared state and unavailable-capability reporting.

- [ ] **B06: Media capability queries** (required x; Qualified).
  - **Deliver:** MediaCapabilities tests supported playback/encoding formats and configurations.
  - **Consumes:** H07; playback/capture/codec providers remain deferred.
  - **Acceptance:** Query selected formats/configurations against real providers; compiled-out video/audio is unsupported/unknown as appropriate and no deferred playback subsystem is silently enabled.

- [ ] **B09: Notifications/app badges** (required x; Qualified).
  - **Deliver:** native Notification and badge integration can serve application alerts without a browser permission model.
  - **Consumes:** H07/I01.
  - **Acceptance:** Request permission/capability, post/replace/close notifications and badges and route supported actions; denial, persistence and browser/native differences are documented.

- [ ] **B10: Wake locks** (required x; Qualified).
  - **Deliver:** keep displays awake during presentation/playback through a native application policy.
  - **Consumes:** H07/I01.
  - **Acceptance:** Acquire/release/revoke an owned display-wake request and verify lifecycle transitions and unsupported/denied outcomes on both target types.

- [ ] **H08: Native inspector hooks** (required x; Platform).
  - **Deliver:** expose selected DOM/style/layout/accessibility/render-tree diagnostics without requiring a browser developer-tools frontend or JS evaluation.
  - **Consumes:** C01/G06 and existing AX/DOM inspection; H09 remains deferred.
  - **Acceptance:** Inspect a live DOM/style/layout/accessibility fixture through native tooling and expose browser-tooling availability honestly; diagnostics remain opt-in and release handles.

## Current-goal integration and definition of done

- [ ] Every required `x` group has its exact selected member scope implemented, or an explicit unresolved blocker retained for user disposition. Do not claim current-goal completion while required work is merely omitted or capability-labelled.
- [ ] Every `!` group has a recorded blocker evaluation and, where unblocked, an implemented provider plus acceptance evidence. A blocked conditional implementation stays unchecked and is listed in the final report with its evidence.
- [ ] Each shared fixture runs against the browser provider and native provider; platform-specific extensions use their declared native/tooling tests and never claim ordinary-browser availability.
- [ ] Required negative/lifetime cases cover cancellation during callbacks, disposed handles, cross-host access, unsupported settings, invalid input and permission denial where relevant.
- [ ] Native ABI/export generation, managed declarations, shared contract/browser adapters and package capability metadata are synchronized and deterministic; public API compatibility is reviewed.
- [ ] Run full repository test/analyzer/build gates appropriate to affected Core, Views, CSS and Native groups through their normal build workflows. Compile and run the native conformance consumer and representative UI sample under NativeAOT.
- [ ] Exercise supported native targets (Windows x64/arm64, Linux x64 and macOS arm64 at the existing platform baseline) and browser CI; report any actual runtime/platform gaps rather than infer them from cross-compilation.
- [ ] Verify normal/traced package configurations where affected, public-package consumers and install-free runtime execution; retain run links, package revisions, test counts and any supported-profile limitations in acceptance.md.
- [ ] Check the work against the 103 selected IDs and report implemented/blocked conditional/blocked required counts. No deferred `?` group is implemented implicitly and no blank selection is reintroduced.
- [ ] Release/PR/merge actions follow the authorization in effect at execution time; this task-list creation itself performs none.

## Explicitly deferred to the next goal: 53 query items

**No implementation or exploratory spike for these groups belongs to the current goal.** Keep their questions for the next goal; inspecting a selected task's dependency enough to identify a blocker is allowed, but does not authorize implementing that deferred subsystem. These are backlog entries, not completion checkboxes.

- **D02: Mutation observation** - deferred to the next goal (Engine). Decision to revisit: Use the engine observer with native callback ownership and matching delivery/takeRecords semantics; keep Ranvier effects as the default for signal-driven view updates.
- **D03: Document types and XML node kinds** - deferred to the next goal (Engine). Decision to revisit: Keep outside the required UI surface; add explicit low-level bindings if a consumer needs these already-present engine types. Templates construct DOM directly, but the native renderer still lays out HTML/SVG DOM and can parse markup fragments.
- **D04: Standalone DOM parsing/serialization** - deferred to the next goal (Qualified). Decision to revisit: Prefer WebCore parsing when results must be live engine DOM; use a portable document-data facade only when node identity and browser parsing recovery are outside the contract.
- **D07: Custom element registration/lifecycle** - deferred to the next goal (Qualified). Decision to revisit: Do not require registration for ordinary Ranvier components. Offer an optional, specified native component-reaction bridge only if interoperability or D08 form-associated elements need it; this is not simply another template API.
- **D08: Element internals/form participation** - deferred to the next goal (Qualified). Decision to revisit: Recommend the capability, query the exact mechanism: either support the required D07 registration path or define a portable form-control contract with browser custom-element and native form adapters. Specify participation, validity and accessibility separately.
- **D15: Native control pickers** - deferred to the next goal (Qualified). Decision to revisit: Expose capability-tested picker requests with typed results/errors and an owned host adapter; do not promise every browser input picker on every platform.
- **D18: Template/component registry scoping** - deferred to the next goal (Qualified). Decision to revisit: Decide with D07, not as a prerequisite for templates; document which create/import operations carry the registry.
- **C15: Typed numeric CSS operations** - deferred to the next goal (Qualified). Decision to revisit: Use Ranvier.Css for portable authoring and engine Typed OM for runtime values/conversions; retain double precision and test the supported operation set in both engines before claiming full Typed OM parity.
- **C17: Typed color operations** - deferred to the next goal (Qualified). Decision to revisit: Agree a capability-tested set of spaces/conversions and use renderer results or a common numerical implementation; make unsupported spaces explicit instead of guessing a conversion.
- **C20: CSS function rules** - deferred to the next goal (Engine). Decision to revisit: Expose only with renderer capability checks and tests for the pinned feature version; do not emulate CSS function evaluation outside the engine.
- **C23: Paged-media rules** - deferred to the next goal (Qualified). Decision to revisit: Recommend rule inspection; keep actual print/PDF execution capability-qualified and coordinate its contract with R14.
- **G13: Rendering/layout lifecycle notifications** - deferred to the next goal (Platform). Decision to revisit: Define a portable frame lifecycle only where both targets can honour it, and keep exact engine phase notifications as an optional diagnostic extension.
- **T05: Undo transaction integration** - deferred to the next goal (Qualified). Decision to revisit: Define which history owns a transaction, how native text edits join it and how redo/reset work; prefer explicit application transactions over an unsupported promise of global browser undo control.
- **T07: Text shaping/measurement diagnostics** - deferred to the next goal (Platform). Decision to revisit: Keep portable measurements in Canvas/range APIs; expose deeper shaping information as a capability-qualified diagnostic extension.
- **T08: Rich IME state** - deferred to the next goal (Qualified). Decision to revisit: Specify a common composition/edit model and platform extensions; acceptance must include real IME use, not only injected event payloads.
- **T10: Spellchecking and writing assistance** - deferred to the next goal (Qualified). Decision to revisit: Offer a common text-check/suggestion contract with optional native/browser providers and explicit availability; do not promise control over browser-owned menus.
- **R08: Offscreen drawing** - deferred to the next goal (Qualified). Decision to revisit: Specify transferable vs thread-affine surfaces, copy/transfer costs and supported contexts; native drawing jobs may need a dedicated render owner instead of arbitrary Tasks.
- **R10: WebGL 1/2** - deferred to the next goal (Qualified). Decision to revisit: Use an optional graphics package/build, agree a supported WebGL version/extensions set and test identical shaders/context-loss behaviour. Do not enable it in every native package automatically.
- **R11: WebGPU** - deferred to the next goal (Qualified). Decision to revisit: Use an optional backend with explicit limits/features and WGSL/resource semantics; validate the supported profile before promising portability.
- **R13: Compositor/presentation integration** - deferred to the next goal (Platform). Decision to revisit: Expose a separate native presentation capability; any platform expansion requires a separate authorized implementation task, not merely this checkbox.
- **R14: Printing/PDF/pagination** - deferred to the next goal (Qualified). Decision to revisit: Share page-style and print-request abstractions, qualify actual output capabilities, and distinguish preview/print dialog from deterministic PDF generation.
- **R15: Shape detection** - deferred to the next goal (Qualified). Decision to revisit: Use capability-tested detector interfaces with explicit formats/options and documented result variance; keep providers optional.
- **R16: Model elements** - deferred to the next goal (Qualified). Decision to revisit: Agree supported assets/camera interactions and an optional fallback renderer; do not imply cross-platform model support from one IDL declaration.
- **I05: Gesture and force input** - deferred to the next goal (Qualified). Decision to revisit: Define portable gesture units/phases and pointer fallback rules; expose provider-specific gestures as extensions instead of copying vendor event names as universal API.
- **I09: Gamepad and haptics** - deferred to the next goal (Qualified). Decision to revisit: Expose raw standard mappings plus explicit capabilities and optional action mapping; unavailable haptics must be reported.
- **I10: Accessibility platform adapters** - deferred to the next goal (Qualified). Decision to revisit: Share semantic/action contracts, keep native bridge details private and qualify platform readiness; this recommendation does not resume paused platform work.
- **I13: DOM Observable facade** - deferred to the next goal (Facade). Decision to revisit: Use a small owned event-stream facade or adapt an existing common implementation; expose full DOM Observable only if a consumer needs its exact operators/terminal semantics.
- **H03: Performance observation** - deferred to the next goal (Qualified). Decision to revisit: Expose supported entry types and common fields with native extensions; validate units/time origins and avoid claiming cross-engine timing equivalence.
- **H04: Resource timing** - deferred to the next goal (Qualified). Decision to revisit: Expose optional phase fields and provenance, report unavailable values explicitly and keep request-control semantics separate from diagnostics.
- **H09: Paint/style/layout change tracing** - deferred to the next goal (Platform). Decision to revisit: Keep an opt-in diagnostic extension with overhead/provenance metadata, and share only the common trace envelope across providers.
- **H10: Error/reporting records** - deferred to the next goal (Qualified). Decision to revisit: Define common severity/source/cause fields with provider-specific payloads; support browser-only categories as optional extensions.
- **H12: Text encoding/compression/streams** - deferred to the next goal (Qualified). Decision to revisit: Specify the portable reader/writer/transform contract and exact formats; test closure/cancellation/backpressure rather than declare full browser stream parity from a wrapper.
- **H14: Message channels/structured copy** - deferred to the next goal (Qualified). Decision to revisit: Start with an explicit serializable message/value set and documented copy/transfer rules; add exact structured-clone features only when required.
- **B01: Audio playback/processing** - deferred to the next goal (Qualified). Decision to revisit: Split basic playback from Web Audio processing; optional provider packages should expose a tested common graph/profile only where needed.
- **B02: Video/media elements** - deferred to the next goal (Qualified). Decision to revisit: Define a portable playback/caption/frame profile and optional native media package, with explicit supported formats and capabilities.
- **B03: Codecs and raw frames** - deferred to the next goal (Qualified). Decision to revisit: Use explicit format/profile/capability negotiation and conformance fixtures; do not promise every browser codec on every native target.
- **B04: Capture and recording** - deferred to the next goal (Qualified). Decision to revisit: Agree common track/permission/cancellation semantics and optional providers; retain platform extensions for capture modes with no browser counterpart.
- **B05: Media source buffering** - deferred to the next goal (Qualified). Decision to revisit: Select only with a concrete streaming consumer; implement or qualify the supported source-buffer state machine and formats separately from B02 playback.
- **B07: OS media session/remote playback** - deferred to the next goal (Qualified). Decision to revisit: Recommend common media-session actions/metadata; select PiP and remote playback individually with capabilities and explicit user interaction rules.
- **B08: Encrypted media** - deferred to the next goal (Qualified). Decision to revisit: Defer until there is a named content/key-system requirement, then use optional supported providers and explicit unsupported outcomes rather than emulate EME.
- **B11: Speech** - deferred to the next goal (Qualified). Decision to revisit: Define a provider/capability model and common event ordering; keep speech optional and document result variance.
- **B12: Device motion/orientation** - deferred to the next goal (Qualified). Decision to revisit: Expose availability and coordinate/time conventions, with optional host providers and no fabricated data.
- **B13: Window/display modes** - deferred to the next goal (Qualified). Decision to revisit: Separate portable user-initiated intents from capability-gated native positioning/multiwindow extensions; preserve browser denial outcomes.
- **B14: Contact/location/sharing integrations** - deferred to the next goal (Qualified). Decision to revisit: Use typed results, capability checks and cancellation; select each service independently rather than require one all-platform bundle.
- **B15: XR** - deferred to the next goal (Qualified). Decision to revisit: Treat as an optional XR provider/profile after an explicit use case; preserve reference-space/session semantics and platform capability negotiation.
- **B16: Paint/custom worklets** - deferred to the next goal (Qualified). Decision to revisit: Prefer a common pure paint callback/data contract with browser/native adapters; select actual worklet-module compatibility only if required by an integration.
- **B17: Background execution** - deferred to the next goal (Qualified). Decision to revisit: Recommend a portable compute/message facade; decide browser worker/service-worker compatibility separately and do not restore JS execution merely to match surface names.
- **B18: Network/protocol APIs** - deferred to the next goal (Qualified). Decision to revisit: Recommend a bounded HTTP/header/body/cancel facade and selected socket APIs; make browser restrictions explicit and select real-time protocols separately. Do not claim HttpClient reproduces the full Fetch processing model.
- **B19: Browser storage/file-system APIs** - deferred to the next goal (Qualified). Decision to revisit: Recommend portable scoped key/value and explicit transaction/file-capability contracts; choose exact IndexedDB/query compatibility only for a consumer, and leave browser cookie/service-worker cache emulation optional.
- **B20: Browser navigation/lifecycle** - deferred to the next goal (Qualified). Decision to revisit: Recommend a route/navigation facade; keep browsing-context/frame/script globals deliberately outside it and document lifecycle differences rather than pretend to create a browser.
- **B21: Browser identity/security/permissions** - deferred to the next goal (Qualified). Decision to revisit: Split identity, passkeys, payments and capability permissions into optional contracts with explicit trust/provider boundaries; do not emulate a browser origin or bypass browser restrictions.
- **B24: Cross-context locks** - deferred to the next goal (Qualified). Decision to revisit: Define session/application/process scope explicitly and use suitable native synchronization; expose broader browser-style lock semantics only if tested providers can honour them.
- **B25: Browser platform reporting/push** - deferred to the next goal (Qualified). Decision to revisit: Split a portable background-message contract from browser service-worker plumbing; implement only with a concrete delivery/backend requirement, leaving browser-only reports as extensions.

## Planning limits

This is a scoped delivery/task document derived from the user's current selections, not proof of API support or a completed implementation. Exact signatures and file lists are deliberately finalized in Task 0 from clean semantic/API checks and native source signatures. No source compilation, optional bridge spike or new platform capability was performed while creating this document. The source/export ledger retains the complete historical audit even though blank choices have been removed from the active checklist.
