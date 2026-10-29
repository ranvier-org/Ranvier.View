# Native UI API candidates

Status: user-prioritized scope, 2026-10-29. The user's current `x` and `!` marks define the current task scope; `?` items are explicitly deferred to the next goal. Blank choices have been removed from this working checklist. Written user questions/notes remain. Marks are scope decisions, not completed implementation or release evidence. See the [delivery task document](../superpowers/plans/2026-10-29-shared-native-api-tasks.md).

## Recommendation marks and shared API classifications

- `[x]` **Current scope:** fulfil this shared API group; the recommendation explanation remains for context. This does not mean implemented, cheap, enabled everywhere or required in the default package.
- `[!]` **Conditional current scope:** evaluate with the current tasks and implement if there is no concrete blocker; record evidence for any blocker.
- `[?]` **Next goal:** explicitly deferred; each entry retains the compatibility/cost question and proposed boundary for that later goal.
Blank choices were removed from this active selection list and are not tasks. The full source ledger remains historical evidence, not a request to reintroduce those APIs.

Each item has one primary implementation classification, independent of its existing native-coverage label:

- **Engine** — Shared API, engine-backed: browser DOM/renderer and native WebCore implement the capability; expose their relevant semantics rather than recreate layout/style algorithms.
- **Facade** — Shared API, facade-backed: keep a common F# contract while using browser facilities on one target and Ranvier/.NET/host services on the other.
- **Qualified** — Shared API with qualified compatibility: share a specified subset/capability model, and document behavioural differences, unsupported operations and provider-dependent results.
- **Platform** — Platform-specific extension: useful capability without an equivalent ordinary-browser contract; expose it separately, optionally with browser tooling support.
- **Unsupported** — Deliberately unsupported in the recommended core surface: selected legacy/browser-specific semantics add little portable value; optional compatibility packages can be considered for named consumers.

**Decision principle:** a .NET or host implementation is a reason to build a facade, not a reason to reject the shared API. Sharing names alone is insufficient: define ownership, ordering, cancellation, completion/errors, identity, serialization, units, permissions and capability reporting. Native-only callers can deliberately opt into richer host APIs or different semantics without weakening the portable contract. No recommendation requires reproducing a browser security/execution environment, silently supplying fake values, or bundling every optional dependency in the default runtime.

## Scope and completeness

This audits the APIs that could serve a native DOM/style/layout/animation/graphics/input runtime. Browser terminology does not exclude a useful native operation. Prior-art usage is evidence of usefulness, not an automatic requirement.

The finite source boundary is the pinned WebCore revision `b95c91b1c0ee43cc86d25ff7af73038f67a88e04` with Ranvier's local patch series (including stub IDLs), Ranvier.WebCore ABI 21 at `eef227e280664a10e3af75a7d5c75dc218b37a23`, and the Views adapter at `739cf7774a8b6f042a1953369159d16c4f4c7794`. All **1,811 non-test WebCore IDL files** were inventoried, including Modules, partial interfaces, mixins, dictionaries and namespaces. The parser recognized **1,158 interface declarations and 6,069 non-constant member declarations**, with zero parse failures; those counts include overloads/partials/stub alternatives and are not counts of distinct user APIs. Namespace and iterable/maplike/setlike declarations require the raw-source evidence retained in the ledger; the parser does not model them as ordinary members. The ledger also retains all 25 source files with preprocessor alternatives so platform branches are available for review.

The [source ledger](2026-10-29-native-api-source-ledger.md) lists every inventoried file, its declarations and member names, plus the exact exported-function baseline and explicitly omitted members. It includes peripheral families so nothing silently disappears at the scope boundary. This document groups the candidates into choices people can review. It is not an assertion that every WebCore C++ method, future web standard, operating-system feature or JS-runtime API has been audited.

### Existing native coverage labels

- **Missing:** no corresponding callable capability found in the inspected generated/legacy native exports; WebCore source presence alone does not establish build availability.
- **Partial:** related native operations exist; the line identifies remaining operations or precision/options/payloads.
- **Adapter:** native capability exists; a convenient owned/typed F# service is missing or needs consolidation. This is not a new native-engine requirement.
- **Host:** the native application's window/input/resource integration must supply or expose the service; .NET/OS services can back a shared facade rather than require a new engine export.
- **Build:** configuration/platform/feature-gated and may require more than ABI generation; not assumed usable in today's slim package.
- **Boundary:** adjacent capability included for an explicit shared-contract/provider decision, without assuming every browser-specific semantic is required.

Labels are a source audit, not runtime support certification. A selected group still needs a per-member support matrix against the actual target build. Constructors/static factories, asynchronous completion and collection operations are included in each group unless explicitly separated. Implementing a group need not mirror the browser's object model when a smaller native interface supplies the same capability.

### Already covered: avoid counting these as missing

Core DOM creation/traversal/mutation, selectors, HTML inline style text/property get/set/remove, element bounding rectangles, client/offset dimensions, element scrolling, focus, common form controls/validation, selection/range editing, event listeners and common event payloads, pointer capture, frame/timer/microtask scheduling, resource callbacks, CPU snapshots, clipboard/drag data transport and the AX tree already have native surfaces. Coverage/options/platform adapters can still need extension. CSS rendering, @media, SVG rendering and CSS animations can work without exposing corresponding introspection/control APIs.

## DOM structure, introspection and component capabilities

Source families: dom, html, accessibility, xml; exact declarations in the ledger.

- [x] **D01 [Missing] Filtered tree traversal:** `Document.createTreeWalker/createNodeIterator`, TreeWalker, NodeIterator and NodeFilter traverse selected node kinds without hand-written recursive walks.
  - **Classification:** Engine.
  - **Why recommended:** Share selector-independent traversal and editor/tree tooling against the same live DOM on both targets.
- [?] **D02 [Missing] Mutation observation:** MutationObserver/MutationRecord observe child, attribute and text changes with old values and queued record delivery. {isn't this just the same as using our effect system essentially? if mutation observers have some benefit in performance that we could use then we should. TELL ME MORE ABOUT THIS}
  - **Classification:** Engine.
  - **Why support it:** Observe actual DOM changes, including changes made outside Ranvier signals, and port libraries that depend on mutation records.
  - **Why query/defer it:** Ranvier effects track signal dependencies; they do not observe arbitrary DOM mutation. Observers allocate/queue records and run after mutation, so they are not a general performance replacement for effects; adding both can duplicate work or cause feedback loops.
  - **Recommended boundary:** Use the engine observer with native callback ownership and matching delivery/takeRecords semantics; keep Ranvier effects as the default for signal-driven view updates.
- [?] **D03 [Missing] Document types and XML node kinds:** DocumentType, XMLDocument, CDATASection and ProcessingInstruction represent/import/serialize structured documents beyond HTML nodes. {Since we dont actually parse or render HTML/XML I dont think this is necessary. Unless we can't remove it, and it's already there, in which case, an unsafe binding is ok.}
  - **Classification:** Engine.
  - **Why support it:** Share structured-document/import/serialization logic when XML/SVG document kinds genuinely matter.
  - **Why query/defer it:** Ordinary templates do not need CDATA, processing instructions or doctypes. System.Xml nodes are not WebCore nodes, and an unsafe cast cannot make their identities interchangeable.
  - **Recommended boundary:** Keep outside the required UI surface; add explicit low-level bindings if a consumer needs these already-present engine types. Templates construct DOM directly, but the native renderer still lays out HTML/SVG DOM and can parse markup fragments.
- [?] **D04 [Missing] Standalone DOM parsing/serialization:** DOMParser/XMLSerializer parse detached HTML/XML/SVG documents and serialize nodes; existing fragment parsing is a smaller capability. {If this can be handled natively in dotnet, then there is no reason for this. Unless it exists either way. If so, we might as well at least provide even an unsafe binding}
  - **Classification:** Qualified.
  - **Why support it:** Share detached document import/export, SVG assets and editor interchange across browser and native.
  - **Why query/defer it:** HTML parsing has browser-specific recovery and namespace rules; System.Xml is not an HTML parser and returns a different node model. A separate native parser may need an explicit import into the engine.
  - **Recommended boundary:** Prefer WebCore parsing when results must be live engine DOM; use a portable document-data facade only when node identity and browser parsing recovery are outside the contract.


- [?] **D07 [Missing] Custom element registration/lifecycle:** CustomElementRegistry define/get/whenDefined/upgrade/initialize registers reusable element behaviours; native callback delivery needs a non-JS design decision. {isn't this a moot point? we already have the capacity to define elements and templates etc. TELL ME MORE}
  - **Classification:** Qualified.
  - **Why support it:** Share web-component lifecycle/upgrade behaviour and integrate custom-element libraries when that interoperability is a real goal.
  - **Why query/defer it:** Ranvier templates already define application components; a custom-element registry additionally handles element-name registration, upgrades and reaction ordering. Browser constructors/reactions assume JS-facing semantics that native F# callbacks do not automatically reproduce.
  - **Recommended boundary:** Do not require registration for ordinary Ranvier components. Offer an optional, specified native component-reaction bridge only if interoperability or D08 form-associated elements need it; this is not simply another template API.
- [?] **D08 [Missing] Element internals/form participation:** `attachInternals`, ElementInternals and CustomStateSet give components validity, form values, custom states and accessibility semantics. {some of this sounds important for forms. TELL ME MORE}
  - **Classification:** Qualified.
  - **Why support it:** Share custom-control form values, validity, reset/disabled behaviour, custom states and accessibility defaults.
  - **Why query/defer it:** ElementInternals is associated with eligible custom elements; exposing attachInternals on arbitrary template roots will not provide those semantics. A facade that merely adds attributes would miss form submission/reset and validation behaviour.
  - **Recommended boundary:** Recommend the capability, query the exact mechanism: either support the required D07 registration path or define a portable form-control contract with browser custom-element and native form adapters. Specify participation, validity and accessibility separately.
- [x] **D09 [Partial] SVG/MathML shared element members:** fill the SVG style/dataset/focus/tabIndex/nonce/autofocus gaps and MathMLElement-specific adapters; low-level SVG style writes already exist.
  - **Classification:** Engine.
  - **Why recommended:** Remove real SVG/MathML adapter gaps so shared styling, focus and attribute logic behaves consistently on all element namespaces.
- [x] **D10 [Missing] HTML table object APIs:** HTMLTableElement/Section/Row/Cell/Col/Caption collections, insertion/deletion and reflected properties support grids and rich table editors. {we do want the possible support of rich table editors; otherwise, I thought this is all possible under our own View api already}
  - **Classification:** Engine.
  - **Why recommended:** Share rich-table editor logic through table-specific rows/cells/sections and insertion methods. View templates already render table tags; the missing piece is their typed specialized operations/collections, not the ability to display tables.
- [x] **D11 [Missing] Additional HTML semantic/reflection types:** typed properties for html/base/br/hr/headings/quote/time/data/mod/pre/list/menu/map/picture/source/selected-content and other tag classes avoid relying solely on raw attributes. {TELL ME MORE. This is confusing, we don't have these elements?}
  - **Classification:** Engine.
  - **Why recommended:** Expose specialized reflected properties and interfaces for existing tag kinds. These elements can already be created/rendered; generated View tags and raw attributes do not imply that every type-specific getter/setter is wrapped.
- [x] **D12 [Missing] Stylesheet/link element objects:** HTMLStyleElement/HTMLLinkElement and LinkStyle.sheet expose associated sheets, media, loading state and enable/disable behaviour. {don't we need this to be able to support style sheets? or to support Ranvier.Css (which is the main objective for style sheets)}
  - **Classification:** Engine.
  - **Why recommended:** Support shared sheet discovery, loading/readiness checks, enable/disable controls and Ranvier.Css diagnostics. Supplied stylesheets already render; these objects add programmatic association and inspection rather than unlock basic CSS support.
- [x] **D13 [Missing] Specialized form collections/events:** HTMLFormControlsCollection, HTMLOptionsCollection, FormDataEvent and SubmitEvent expose control collections, submitter and collected form data.
  - **Classification:** Engine.
  - **Why recommended:** Share form/option collection traversal and submitter/form-data event logic without rebuilding those semantics in application code.
- [x] **D14 [Partial, Host] Form submission semantics:** requestSubmit/submit and form-data construction/serialization run native application validation/submit workflows without implying network navigation.
  - **Classification:** Qualified.
  - **Why recommended:** Recommend shared validation, submit-event and form-data behaviour, with a native application callback for the final action. Do not silently navigate or issue a request; browser submit/requestSubmit differences and validation ordering remain explicit.
- [?] **D15 [Partial, Host] Native control pickers:** input/select showPicker connects date/color/select controls to an explicit host popup policy; existing generic popup handling is not the full picker API.
  - **Classification:** Qualified.
  - **Why support it:** Let shared controls request date/color/file/select pickers through one API.
  - **Why query/defer it:** Browser activation rules and picker availability differ from native dialogs; cancellation, accepted values, file capabilities and UI-thread dispatch need adapters. The existing popup ABI covers only part of this.
  - **Recommended boundary:** Expose capability-tested picker requests with typed results/errors and an owned host adapter; do not promise every browser input picker on every platform.
- [x] **D16 [Missing] Specialized dialog/popover/command payloads:** ToggleEvent, CommandEvent and CloseWatcher expose transition causes, command sources and close requests beyond generic event names. {why is this missing}
  - **Classification:** Engine.
  - **Why recommended:** Share dialog/popover/command event handling with typed cause/source fields. These payload types fell outside the selected generator scope; generic events and element rendering did not supply the omitted detail.
- [x] **D17 [Partial] DOM construction and value factories:** fill unsupported constructors/static factories for otherwise exported node/event/geometry types so native code can create required standalone values. {TELL ME MORE, confused as to why there is some implication that we are not covering the full DOM? How}
  - **Classification:** Qualified.
  - **Why recommended:** Provide portable constructors/factories for already-shared geometry/events and document-dependent values. The generator skipped constructors/static factories and uses selected host factories, so coverage was never every constructor in the full DOM corpus; specify owner-document and default-value rules.
- [?] **D18 [Missing, Boundary] Template/component registry scoping:** scoped custom registries and registry-bearing creation/import options isolate component definitions in separate trees.
  - **Classification:** Qualified.
  - **Why support it:** Isolate custom-element definitions when sharing independently authored component trees.
  - **Why query/defer it:** Useful only if D07 registry semantics are selected; ordinary Ranvier template scoping does not require browser-style registry lookup and upgrades. Adding it first creates an unused compatibility dependency.
  - **Recommended boundary:** Decide with D07, not as a prerequisite for templates; document which create/import operations carry the registry.

## Stylesheets, CSS rules and typed values

Source families: css, css/typedom and related DOM partials.

- [x] **C01 [Adapter] Computed-style reads:** wrap `rv_element_computed_style` as an owned/typed F# operation for resolved style values; the native operation already exists.
  - **Classification:** Engine.
  - **Why recommended:** Share effective-style assertions and animation measurements through the renderer; use the already-exported operation rather than parse styles in a facade.
- [x] **C02 [Partial] Pseudo-element computed styles:** extend computed-style querying with a pseudo-element argument for `::before`, `::after` and other generated content.
  - **Classification:** Engine.
  - **Why recommended:** Include generated-content styles in shared assertions and animation/measurement code.
- [x] **C03 [Missing] CSS property/condition support queries:** CSS.supports tests whether the live renderer accepts a property/value or supports-condition; catalogue membership and valid-value testing are different operations.
  - **Classification:** Engine.
  - **Why recommended:** Share feature/value checks against the active renderer instead of assuming that a generated grammar catalogue proves support.
- [x] **C04 [Adapter/Host] Typed CSS property access:** reuse Ranvier.Css names, grammar markers, literal/unit metadata and serialization for typed native style/animation operations.
  - **Classification:** Facade.
  - **Why recommended:** Use Ranvier.Css as the common typed property/value facade, with browser DOM and native ABI sinks; reuse serialization and numeric metadata rather than build another CSS value model.
- [x] **C05 [Missing] CSS identifier escaping:** CSS.escape produces safe selector identifiers from arbitrary application strings.
  - **Classification:** Facade.
  - **Why recommended:** Provide one CSS.escape-compatible algorithm for safe selectors; browser and native implementations must share the same escaping rules.
- [x] **C06 [Missing] Stylesheet discovery/metadata:** StyleSheetList/StyleSheet and Document.styleSheets expose sheets, ownership, media, disabled state and linkage.
  - **Classification:** Engine.
  - **Why recommended:** Share stylesheet enumeration/ownership/state assertions and inspection tooling.
- [x] **C07 [Missing] Stylesheet construction/replacement/adoption:** CSSStyleSheet constructors/replace/replaceSync and Document/ShadowRoot.adoptedStyleSheets install reusable stylesheets.
  - **Classification:** Engine.
  - **Why recommended:** Share dynamic sheet replacement, stylesheet reuse and scoped adoption, including Ranvier.Css integration.
- [x] **C08 [Missing] Rule enumeration and mutation:** CSSRule/CSSRuleList, CSSStyleRule and grouping-rule insertRule/deleteRule expose selectors, declaration blocks, cssText and nested rules.
  - **Classification:** Engine.
  - **Why recommended:** Share parsed-rule assertions and controlled CSS edits using renderer-normalized declarations and selectors.
- [x] **C09 [Missing] Conditional rules and media lists:** CSSMediaRule/MediaList, CSSSupportsRule and CSSContainerRule inspect and edit media/supports/container conditions.
  - **Classification:** Engine.
  - **Why recommended:** Share inspection of the renderer-parsed conditions that govern responsive and container-based UI.
- [x] **C10 [Missing] Cascade structure:** CSSLayerBlockRule/StatementRule, CSSScopeRule, CSSNestedDeclarations, CSSImportRule and CSSNamespaceRule expose layers, scopes, nesting and imports.
  - **Classification:** Engine.
  - **Why recommended:** Share layer/scope/nesting/import inspection and editing; these describe actual cascade structure rather than a separately reconstructed CSS model.
- [x] **C11 [Missing] CSS keyframe rules:** CSSKeyframesRule/CSSKeyframeRule append/find/delete keyframes and edit animation names, offsets and declarations.
  - **Classification:** Engine.
  - **Why recommended:** Share generated keyframe inspection and editing with the same renderer CSS animation semantics.
- [x] **C12 [Missing] Custom-property registration:** CSS.registerProperty and CSSPropertyRule define custom-property grammar, inheritance and initial values for native interpolation.
  - **Classification:** Engine.
  - **Why recommended:** Make typed custom-property interpolation available consistently and verify its registered grammar/default/inheritance rules.
- [x] **C13 [Missing] Typed style maps:** attributeStyleMap/computedStyleMap, StylePropertyMap/ReadOnly and CSSStyleRule.styleMap read/write typed values instead of CSS text.
  - **Classification:** Engine.
  - **Why recommended:** Share typed declared/computed style maps; Ranvier.Css can adapt its values while WebCore remains authoritative for resolved engine values.
- [x] **C14 [Missing] Typed CSS value parsing:** CSSStyleValue.parse/parseAll, keyword/image/unparsed values and variable references preserve structured CSS values.
  - **Classification:** Engine.
  - **Why recommended:** Use the engine parser for accepted CSS structures and normalization; a typed builder alone cannot inspect arbitrary supplied CSS.
- [?] **C15 [Missing] Typed numeric CSS operations:** CSSNumericValue/CSSUnitValue/math nodes, numeric factories and unit conversion expose renderer numeric typing/evaluation; compare with existing Ranvier.Css before selecting duplicates.
  - **Classification:** Qualified.
  - **Why support it:** Share runtime numeric/unit conversion and resolved CSS value operations alongside Ranvier.Css compile-time typing.
  - **Why query/defer it:** Ranvier.Css already covers construction/type algebra, but renderer-resolved percentages, relative units, math trees and Typed OM conversions are not identical services. Wrapping both entire models without a boundary would confuse construction with layout-dependent resolution.
  - **Recommended boundary:** Use Ranvier.Css for portable authoring and engine Typed OM for runtime values/conversions; retain double precision and test the supported operation set in both engines before claiming full Typed OM parity.
- [x] **C16 [Missing] Typed transform values:** CSSTransformValue/components represent translate/rotate/scale/skew/perspective/matrix operations and convert them to matrices.
  - **Classification:** Engine.
  - **Why recommended:** Share structured transform inspection and renderer matrix conversion, with typed Ranvier.Css adapters where appropriate.
- [?] **C17 [Missing, Build] Typed color operations:** CSS RGB/HSL/HWB/Lab/LCH/OKLab/OKLCH objects parse and convert colors across renderer-supported color spaces; runtime setting support needs checking.
  - **Classification:** Qualified.
  - **Why support it:** Share color parsing/conversion and animation values across renderer-supported color spaces.
  - **Why query/defer it:** Source presence does not establish enabled Typed OM color support on either target. Color-space conversions, gamut handling, alpha and serialization cannot be equated with arbitrary platform color helpers.
  - **Recommended boundary:** Agree a capability-tested set of spaces/conversions and use renderer results or a common numerical implementation; make unsupported spaces explicit instead of guessing a conversion.
- [x] **C18 [Missing] Font rules/descriptors:** CSSFontFaceRule/Descriptors, font-feature-values and font-palette-values rules inspect and edit font configuration.
  - **Classification:** Engine.
  - **Why recommended:** Inspect parsed font rules/descriptors for shared assertions; this does not imply fonts were previously unable to render.
- [x] **C19 [Missing] Counter styles:** CSSCounterStyleRule exposes generated list/counter formatting systems, symbols, prefixes and fallbacks.
  - **Classification:** Engine.
  - **Why recommended:** Share inspection/editing of counter and list formatting already understood by the renderer.
- [?] **C20 [Missing, Build] CSS function rules:** CSSFunctionRule/Declarations/Descriptors expose source-defined CSS functions and their parameters/results where enabled.
  - **Classification:** Engine.
  - **Why support it:** Inspect and edit custom CSS functions through the same parsed rule model as other stylesheet APIs.
  - **Why query/defer it:** This is a feature/settings-dependent rule family; browser/native availability may differ and wrapping unsupported source types would create a misleading promise.
  - **Recommended boundary:** Expose only with renderer capability checks and tests for the pinned feature version; do not emulate CSS function evaluation outside the engine.
- [x] **C21 [Missing] Position fallback rules:** CSSPositionTryRule/Descriptors expose anchor-positioning fallback geometry and alignment declarations.
  - **Classification:** Engine.
  - **Why recommended:** Share inspection and editing of anchor-positioning fallbacks for popovers and overlays, subject to renderer feature detection.
- [x] **C22 [Missing] Starting-style/view-transition rules:** CSSStartingStyleRule/CSSViewTransitionRule inspect entry-transition styles and transition types/navigation policy.
  - **Classification:** Engine.
  - **Why recommended:** Keep transition-related rule inspection shared; expose supported rule types without promising that every transition mode is available.
- [?] **C23 [Missing, Host] Paged-media rules:** CSSPageRule/Descriptors expose print/page dimensions and margins; actual page output needs a host printing path.
  - **Classification:** Qualified.
  - **Why support it:** Share page-style authoring/assertions for reports and print/PDF layouts.
  - **Why query/defer it:** Inspecting @page is distinct from creating paginated output; printer selection, page metrics and pagination backends vary across browser and native.
  - **Recommended boundary:** Recommend rule inspection; keep actual print/PDF execution capability-qualified and coordinate its contract with R14.

- [x] **C25 [Host] Matched-rule/cascade diagnostics:** expose relevant renderer rule matching, specificity/origin and resolved-property provenance for native inspection tools; existing computed values alone do not explain the cascade.
  - **Classification:** Platform.
  - **Why recommended:** Useful renderer diagnostics, but matched-rule provenance is not uniformly available to ordinary browser scripts; expose a native/debugger extension and an optional browser tooling adapter rather than claim plain-DOM parity.

## Rendering environment, geometry and scrolling

Source families: page/DOMWindow CSSOM partials, css/DOMMatrix, dom geometry and CSSOM-view partials.

- [x] **G01 [Missing] Media-query evaluation/subscription:** matchMedia and MediaQueryList.matches/change expose viewport, pointer, hover, color-scheme, contrast, reduced-motion and other renderer-supported conditions.
  - **Classification:** Engine.
  - **Why recommended:** Share rendering-environment queries and change subscriptions, with the host supplying native preference/input state to WebCore.
- [x] **G02 [Partial, Host] Viewport dimensions/device scale:** expose coherent CSS-pixel inner/outer dimensions and devicePixelRatio from the host; existing resize/scale setters and paint-pixel size do not provide the complete read surface.
  - **Classification:** Qualified.
  - **Why recommended:** Expose consistent CSS-pixel viewport and scale values through browser/window and native/host adapters; distinguish physical paint size and window decorations.
- [x] **G03 [Missing, Host] Visual viewport:** VisualViewport offsets/size/scale and resize/scroll notifications expose zoomed or occluded viewport geometry.
  - **Classification:** Qualified.
  - **Why recommended:** Share visible viewport measurements for overlays and virtual keyboards; explicitly qualify zoom/occlusion support instead of reporting fabricated values.
- [x] **G04 [Missing, Host] Screen/display information:** Screen/ScreenOrientation expose monitor/work-area dimensions, color depth, orientation and orientation changes through the application's display integration.
  - **Classification:** Qualified.
  - **Why recommended:** Share supported display/orientation information through browser Screen and host monitor services, with capability checks for fields or orientation control unavailable on a target.
- [x] **G05 [Partial] Fractional viewport scrolling:** extend integer host scroll positions to double precision and expose scroll options/relative scrolling without subpixel truncation.
  - **Classification:** Engine.
  - **Why recommended:** Keep scroll-linked logic numerically consistent by preserving fractional viewport positions and options.
- [x] **G06 [Missing] Fragment rectangle lists:** Element/Range.getClientRects and DOMRectList expose each rendered line/fragment rather than only the union bounding rectangle.
  - **Classification:** Engine.
  - **Why recommended:** Share multi-line/fragment measurement for editors, selection overlays and text assertions.
- [x] **G07 [Partial] Geometry construction/mutation:** DOMRect factories/setters, DOMPoint/ReadOnly and DOMQuad provide reusable geometry values and point/quad bounds.
  - **Classification:** Facade.
  - **Why recommended:** Provide common geometry value factories backed by browser objects or native values, preserving mutability, double precision and coordinate conventions.
- [x] **G08 [Missing] Double-precision transform matrices:** DOMMatrix/ReadOnly parse, multiply, invert and apply 2D/3D transforms; a small native or managed math service may suffice.
  - **Classification:** Facade.
  - **Why recommended:** Keep matrix/point algorithms shared through a DOMMatrix-compatible facade; native math need not live in WebCore, but multiplication order, parsing and singular behaviour must match the agreed contract.
- [x] **G09 [Missing] Caret hit testing:** Document.caretPositionFromPoint/CaretPosition map a viewport point to a text node/offset and caret rectangle.
  - **Classification:** Engine.
  - **Why recommended:** Share caret hit-testing and text-position interactions using actual engine layout.
- [x] **G10 [Partial] Resize observation options/payloads:** existing callbacks give width/height; expose requested box options and content/border-box entries, logical dimensions and richer entry data.
  - **Classification:** Engine.
  - **Why recommended:** Share complete resize measurements and box choices rather than maintain application-specific measurement fallbacks.
- [x] **G11 [Partial] Intersection observation options/payloads:** existing viewport-root observers support root margins/thresholds; add element roots, scrollMargin, root/intersection rectangles, timestamps and queued-record access.
  - **Classification:** Engine.
  - **Why recommended:** Share intersection/virtualization logic with the same roots, geometry and delivery semantics.
- [x] **G12 [Adapter/Host] Viewport/document event routing:** consistently deliver resize, scroll/scrollend, visibility, focus and scale changes from the native window into owned callbacks.
  - **Classification:** Qualified.
  - **Why recommended:** Normalize native and browser viewport/document events while documenting unsupported window states and delivery order.
- [?] **G13 [Host] Rendering/layout lifecycle notifications:** expose layout/paint completion, invalidation and presentation timing for measurement and animation coordination without forcing unnecessary layouts.
  - **Classification:** Platform.
  - **Why support it:** Give native layout/paint coordination and inspection reliable completion/invalidation hooks.
  - **Why query/defer it:** Ordinary browser DOM does not expose every internal layout/paint boundary; treating rAF as an exact paint-completion event would mislead shared code.
  - **Recommended boundary:** Define a portable frame lifecycle only where both targets can honour it, and keep exact engine phase notifications as an optional diagnostic extension.
- [x] **G14 [Host] Coordinate-space conversion:** expose page/viewport/element/display conversion where existing bounds/matrices are insufficient for transformed overlays and native hit testing.
  - **Classification:** Qualified.
  - **Why recommended:** Share overlay/drag coordinate conversions with explicitly named spaces, renderer matrices and host display transforms; unsupported conversions must fail rather than mix spaces.
- [x] **G15 [Host] OS appearance and input preferences:** route theme, contrast, accessibility motion, text scale and pointer capabilities into WebCore/media evaluation; querying a preference and supplying its native value are separate tasks.
  - **Classification:** Qualified.
  - **Why recommended:** Share appearance/accessibility/input preference state through browser media queries and native OS adapters, with explicit unknown/unavailable states.

## Animation and transitions

Source family: animation, DOM view-transition partials.

- [x] **A01 [Missing] Keyframe animation creation:** Animatable.animate, KeyframeEffect and Animation constructors create renderer-managed animations from properties/keyframes/timing options.
  - **Classification:** Engine.
  - **Why recommended:** Share keyframe animation creation and let the renderer perform the interpolation/composition it already implements.
- [x] **A02 [Missing] Effect inspection/mutation:** getTiming/getComputedTiming/updateTiming, getKeyframes/setKeyframes, target/pseudoElement and composite settings inspect and retarget existing effects.
  - **Classification:** Engine.
  - **Why recommended:** Share effect/timing/keyframe assertions and retargeting against actual renderer animation state.
- [x] **A03 [Partial] Animation lifecycle/state:** extend existing play/pause/reverse/finish/cancel/time/rate controls with startTime, pending/replace state, updatePlaybackRate, persist, ready/finished and finish/cancel/remove callbacks.
  - **Classification:** Engine.
  - **Why recommended:** Match shared animation state, completion and cancellation handling rather than approximate it with independently scheduled timers.
- [x] **A04 [Missing] Commit animated styles:** Animation.commitStyles writes an effect's current result into inline styles before cancellation.
  - **Classification:** Engine.
  - **Why recommended:** Preserve shared interruption/reversion behaviour by committing actual animated styles before cancellation.
- [x] **A05 [Partial] Animation discovery/identity:** extend existing document/element enumeration with complete ids, effect/timeline links and CSSAnimation/CSSTransition-specific metadata.
  - **Classification:** Engine.
  - **Why recommended:** Share animation discovery and identity for lifecycle cleanup, diagnostics and coordination.
- [!] **A06 [Missing] Document/custom timelines:** Document.timeline, DocumentTimeline and AnimationTimeline expose shared time origins, durations, custom timeline construction and frame-rate controls.
  - **Classification:** Engine.
  - **Why support it:** Share document/timeline timing and synchronize animation effects against the renderer clock.
  - **Why query/defer it:** The group includes custom construction and frame-rate controls beyond simply reading Document.timeline; those members may have different support/settings and ownership rules.
  - **Recommended boundary:** Recommend document timeline access; gate custom timeline/frame-rate features individually and specify time-origin/null-time semantics.
- [!] **A07 [Missing, Build] Scroll-driven timelines:** ScrollTimeline binds animation progress to a scroller and axis instead of elapsed time.
  - **Classification:** Engine.
  - **Why support it:** Reuse shared scroll-driven animations without running a parallel F# scroll-to-progress scheduler.
  - **Why query/defer it:** Feature support, range interpretation and compositor behaviour need validation across the targeted browser and native builds; an emulation is not automatically timing-equivalent.
  - **Recommended boundary:** Expose engine timelines when supported; declare an explicit fallback policy instead of silently claiming full equivalence.
- [!] **A08 [Missing, Build] View-driven timelines/ranges:** ViewTimeline and animation range offsets bind progress to an element's visibility through a scrollport.
  - **Classification:** Engine.
  - **Why support it:** Share view-progress animations for entering/leaving scrollports using renderer measurements.
  - **Why query/defer it:** The group depends on view-timeline/range support and complex layout/scroll cases, so an export alone does not prove compatibility.
  - **Recommended boundary:** Test range boundaries, nested scrollers and subject geometry; allow capability-based fallback when a target lacks the feature.
- [!] **A09 [Missing, Build] Native custom animation effects:** CustomEffect and DocumentTimeline.animate drive application callbacks from renderer timing without JS callbacks.
  - **Classification:** Qualified.
  - **Why support it:** Drive F# animation callbacks using engine timing, with a browser callback implementation of the same public contract.
  - **Why query/defer it:** The existing engine interface is JS-callback-facing; native callback ownership, reentrancy, exception delivery and frame order require a real bridge.
  - **Recommended boundary:** Keep a small callback/effect contract and use Ranvier ownership; decide whether parity needs a CustomEffect bridge or the already-shared frame scheduler.
- [x] **A10 [Missing] Typed animation/transition events:** expose animationName/propertyName, elapsedTime, pseudoElement and playback event times; generic event listeners already exist.
  - **Classification:** Engine.
  - **Why recommended:** Share typed animation/transition event logic and tests without parsing property names from application state.
- [!] **A11 [Missing, Build] View transitions:** Document.startViewTransition/activeViewTransition and ViewTransition ready/finished/types/skipTransition coordinate snapshots between UI states.
  - **Classification:** Qualified.
  - **Why support it:** Share UI-state transition coordination using snapshot transitions where both targets support them.
  - **Why query/defer it:** Browser document/navigation transitions and native snapshot/presentation lifecycles do not automatically match; cancellation, callback completion and feature support need agreement.
  - **Recommended boundary:** Start with same-document transitions, preserve ready/update/finished outcomes, and leave browser navigation transitions outside that initial contract.
- [x] **A12 [Adapter] Native frame/timer ownership:** expose one consistent animation clock and owned frame/timer/microtask adapters over the existing host scheduling operations.
  - **Classification:** Facade.
  - **Why recommended:** Expose the same clock/frame/timer/microtask ownership contract using browser scheduling or the existing native host; keep F# animation logic portable.

## Fonts, text, editing and highlights

Source families: css font loading, dom text/input, page selection/undo, Modules/highlight.

- [x] **T01 [Missing] Font loading/readiness:** Document.fonts and FontFaceSet status/ready/check/load/events report when measurements can rely on loaded fonts.
  - **Classification:** Engine.
  - **Why recommended:** Share font readiness checks so measurement and line splitting wait for real engine font state.
- [x] **T02 [Missing] Font construction/registration:** FontFace descriptors, byte-backed sources and FontFaceSet add/delete/clear supply application-controlled fonts.
  - **Classification:** Qualified.
  - **Why recommended:** Share font registration/loading using URLs or bytes, with host resource delivery and explicit supported-format/error semantics.
- [x] **T03 [Missing] Custom highlights:** Highlight/HighlightRegistry and CSS.highlights style arbitrary text ranges for search matches, diagnostics and collaborative cursors.
  - **Classification:** Engine.
  - **Why recommended:** Share range-based search/diagnostic/collaboration highlighting without mutating document text into wrapper elements.
- [x] **T04 [Partial] Rich beforeinput/input payloads:** expose InputEvent.dataTransfer and any remaining target-range construction/payload gaps for structured editor operations.
  - **Classification:** Engine.
  - **Why recommended:** Share structured editor input handling with actual replacement ranges and transfer payloads.
- [?] **T05 [Missing, Host] Undo transaction integration:** UndoManager/UndoItem and host editing transactions coordinate DOM edits with application undo/redo; existing editing commands alone are not transaction registration.
  - **Classification:** Qualified.
  - **Why support it:** Share labelled undoable editing transactions and form coherent application undo histories.
  - **Why query/defer it:** Browser native editing undo, an engine UndoManager and Ranvier application-state undo are different histories; a naive facade can double-record or lose operations.
  - **Recommended boundary:** Define which history owns a transaction, how native text edits join it and how redo/reset work; prefer explicit application transactions over an unsupported promise of global browser undo control.
- [x] **T06 [Host] Text segmentation:** expose/reuse ICU word/grapheme/line boundaries or equivalent .NET services for text splitting and caret movement; Intl.Segmenter is JS-runtime functionality, not a missing WebCore IDL interface.
  - **Classification:** Qualified.
  - **Why recommended:** Share grapheme/word/line segmentation through browser Intl and native ICU/.NET adapters, specifying locale and Unicode/version differences rather than assuming equal boundaries.
- [?] **T07 [Host] Text shaping/measurement diagnostics:** expose glyph runs, advances, baselines, hit testing or font fallback information where Canvas/TextMetrics and range geometry are insufficient.
  - **Classification:** Platform.
  - **Why support it:** Expose glyph/fallback/baseline diagnostics for text layout investigations and advanced editors.
  - **Why query/defer it:** Ordinary browser APIs do not expose all glyph runs or fallback provenance, and native font backends can produce different runs even for matching CSS.
  - **Recommended boundary:** Keep portable measurements in Canvas/range APIs; expose deeper shaping information as a capability-qualified diagnostic extension.
- [?] **T08 [Partial, Host] Rich IME state:** extend existing composition/commit/caret-rect support with clauses, surrounding-text access and platform text-service queries as required by editors.
  - **Classification:** Qualified.
  - **Why support it:** Share editor composition state and connect it to native text services for high-quality IME behaviour.
  - **Why query/defer it:** Clause attributes, surrounding-text queries and OS text-service protocols differ; synthetic composition events are not equivalent to a real IME integration.
  - **Recommended boundary:** Specify a common composition/edit model and platform extensions; acceptance must include real IME use, not only injected event payloads.
- [x] **T09 [Partial, Host] Clipboard formats and file payloads:** connect ClipboardEvent/DataTransfer structured formats, images and files to existing host clipboard callbacks.
  - **Classification:** Qualified.
  - **Why recommended:** Share clipboard/drop format and binary/file access through browser event objects and native callbacks; distinguish event-granted access from privileged global clipboard requests.
- [?] **T10 [Boundary, Host] Spellchecking and writing assistance:** integrate native suggestions/replacement workflows and text-service annotations; no claim of a complete portable WebCore ABI for these services.
  - **Classification:** Qualified.
  - **Why support it:** Share spellcheck/replacement workflows and improve editor accessibility through host writing services.
  - **Why query/defer it:** Browsers expose limited control over their built-in spelling UI, while OS providers differ in dictionaries, annotations and interaction; exact suggestion parity is unrealistic.
  - **Recommended boundary:** Offer a common text-check/suggestion contract with optional native/browser providers and explicit availability; do not promise control over browser-owned menus.

## SVG and mathematical markup

Source families: svg, mathml; these groups include associated animated/base value types and collections.

- [x] **S01 [Missing] SVG graphics bounds/transforms:** SVGGraphicsElement.getBBox/getCTM/getScreenCTM and viewBox/aspect-ratio state expose actual SVG coordinate systems and bounds.
  - **Classification:** Engine.
  - **Why recommended:** Share SVG bounds and coordinate transforms using the renderer rather than duplicate SVG layout mathematics.
- [x] **S02 [Missing] SVG geometry sampling/hit testing:** SVGGeometryElement getTotalLength/getPointAtLength/isPointInFill/isPointInStroke supply path metrics and precise fill/stroke tests.
  - **Classification:** Engine.
  - **Why recommended:** Share path animation and precise hit testing through WebCore geometry already used by the browser backend.
- [x] **S03 [Missing] SVG shape-specific reflection:** path/circle/ellipse/line/rect/polygon/polyline properties and animated lengths/point lists expose editable shape geometry.
  - **Classification:** Engine.
  - **Why recommended:** Share editable diagram/chart geometry through specialized shape reflection instead of application-specific attribute parsing.
- [x] **S04 [Missing] SVG value/list factories:** SVGLength/Angle/Number/Point/Rect/Matrix/Transform, list mutation and animated wrappers manipulate native SVG values and unit conversions.
  - **Classification:** Engine.
  - **Why recommended:** Share SVG unit/value/list operations and base/animated distinctions with the engine as the authority.
- [x] **S05 [Missing] SVG text metrics/positioning:** SVGTextContentElement/TextPositioningElement/TextPath expose character extents, substring lengths, positions, rotation and text-path properties.
  - **Classification:** Engine.
  - **Why recommended:** Share SVG text placement and measurement for charts, labels and editors.
- [x] **S06 [Missing] SVG root/view controls:** SVGSVGElement/SVGViewSpec/SVGViewElement expose root factories, viewport/viewBox, intersection/enclosure queries and view state.
  - **Classification:** Engine.
  - **Why recommended:** Share SVG viewport, viewBox and root operations for diagram navigation and geometry queries.
- [x] **S07 [Missing] SVG animation controls:** SVGAnimationElement and SVGSVGElement timing methods control SMIL start/end, current time, pause and target information.
  - **Classification:** Engine.
  - **Why recommended:** Share control/inspection of renderer SVG animation timing; this can coexist with the translated F# animation engine.
- [x] **S08 [Missing] SVG gradients/patterns/stops:** gradient, pattern and stop APIs edit paint-server coordinates, transforms, colors and inheritance.
  - **Classification:** Engine.
  - **Why recommended:** Share paint-server editing and assertions using existing SVG rendering semantics.
- [x] **S09 [Missing] SVG filters:** SVGFilterElement and all filter primitive/lighting/component-transfer types expose filter parameters without reconstructing markup strings.
  - **Classification:** Engine.
  - **Why recommended:** Share filter parameter editing and introspection; exposing renderer operations avoids reimplementing filter algorithms.
- [x] **S10 [Missing] SVG clipping/masks/markers:** clipPath/mask/marker APIs expose geometry, units, orientation and reference behaviour.
  - **Classification:** Engine.
  - **Why recommended:** Share clip/mask/marker authoring and geometry-related inspection for vector controls.
- [x] **S11 [Missing] SVG resource/reuse/content types:** use/image/foreignObject/a/style/symbol/defs/switch/metadata/desc/title and URI-reference/test mixins expose remaining specialized SVG state.
  - **Classification:** Engine.
  - **Why recommended:** Complete typed access to modern SVG resource/reuse/content features so shared SVG logic does not fall back to ad-hoc attribute bindings.

- [x] **S13 [Partial] MathML typed element surface:** MathMLElement/MathMLMathElement and shared style/focus/data members expose native mathematical markup beyond generic attributes.
  - **Classification:** Engine.
  - **Why recommended:** Share typed mathematical-markup styling/focus/reflection where it is already rendered by the engine.

## Canvas, images, graphics and output

Source families: html canvas/WebGL, Modules/WebGPU, Modules/ShapeDetection; backend flags differ.

- [x] **R01 [Missing, Build] Canvas 2D context lifecycle:** HTMLCanvasElement.getContext and CanvasRenderingContext2D expose a renderer-backed immediate drawing surface.
  - **Classification:** Engine.
  - **Why recommended:** Share Canvas 2D drawing and lifecycle code directly against renderer contexts; source/build availability still needs verification.
- [x] **R02 [Missing, Build] Canvas paths and hit testing:** CanvasPath/Path2D and fill/stroke/clip/isPointInPath/isPointInStroke draw and test reusable paths.
  - **Classification:** Engine.
  - **Why recommended:** Share paths, clipping and geometric hit tests without replacing the renderer path implementation.
- [x] **R03 [Missing, Build] Canvas state/transforms/compositing:** save/restore/reset, transforms, blend modes, alpha, filters, clipping and layers control drawing state.
  - **Classification:** Engine.
  - **Why recommended:** Share drawing-state/transform/blend/filter logic with the same canvas operation contract.
- [x] **R04 [Missing, Build] Canvas paints:** gradients, patterns, line/fill/stroke/shadow/smoothing styles configure drawing appearance.
  - **Classification:** Engine.
  - **Why recommended:** Share canvas paint/style construction and rendering code.
- [x] **R05 [Missing, Build] Canvas text:** draw/measure text and TextMetrics expose widths, baselines and bounding boxes for custom controls/charts.
  - **Classification:** Engine.
  - **Why recommended:** Share custom-control/chart text drawing and metrics from the rendering backend.
- [x] **R06 [Missing, Build] Canvas image/pixel operations:** drawImage, ImageData, get/putImageData and color-space/pixel-format choices move raster data into/out of canvases.
  - **Classification:** Qualified.
  - **Why recommended:** Share typed raster buffers and image operations with an explicit common pixel format, color space, alpha and stride contract.
- [x] **R07 [Missing, Build] Canvas export/bitmap transport:** toBlob/toDataURL, ImageBitmap/createImageBitmap, bitmap-rendering contexts and close/transfer operations exchange image results.
  - **Classification:** Qualified.
  - **Why recommended:** Share bitmap ownership/export APIs with browser bitmap/blob and native buffer/encoder implementations; distinguish transfer, copy and disposal.
- [?] **R08 [Missing, Build] Offscreen drawing:** OffscreenCanvas/OffscreenCanvasRenderingContext2D support detached drawing surfaces; worker/ownership integration is a separate choice.
  - **Classification:** Qualified.
  - **Why support it:** Share offscreen rendering and move appropriate drawing work away from UI interaction.
  - **Why query/defer it:** The native engine has owning-thread restrictions, while browser transfer/worker contexts have their own ownership rules; wrapping a handle does not make it thread-safe.
  - **Recommended boundary:** Specify transferable vs thread-affine surfaces, copy/transfer costs and supported contexts; native drawing jobs may need a dedicated render owner instead of arbitrary Tasks.
- [x] **R09 [Partial] Image readiness and decoding:** HTMLImageElement.decode provides asynchronous decode completion beyond already exported image properties/load events.
  - **Classification:** Engine.
  - **Why recommended:** Share decode completion and errors so image-dependent layout/drawing does not race resource readiness.
- [?] **R10 [Missing, Build] WebGL 1/2:** context/resource/shader/texture/buffer/framebuffer/query APIs and extension families provide programmable 3D rendering; slim explicitly disables WebGL.
  - **Classification:** Qualified.
  - **Why support it:** Reuse browser WebGL renderers/charts and shader/resource logic on native through the same graphics contract.
  - **Why query/defer it:** The slim build disables WebGL and stubs ANGLE; this needs backend/build/driver/context-loss/resource-lifetime work rather than just IDL exports. Existing GPU presentation is not a WebGL implementation.
  - **Recommended boundary:** Use an optional graphics package/build, agree a supported WebGL version/extensions set and test identical shaders/context-loss behaviour. Do not enable it in every native package automatically.
- [?] **R11 [Missing, Build] WebGPU:** adapter/device/queue/buffer/texture/shader/render/compute APIs provide modern GPU drawing/compute; the shipped browser-API stub removes this family.
  - **Classification:** Qualified.
  - **Why support it:** Share modern GPU drawing/compute code through browser WebGPU and a native WebCore or dedicated GPU adapter.
  - **Why query/defer it:** The slim package stubs WebGPU; adapter/device feature negotiation, shader validation, asynchronous errors and device loss are substantial integration contracts. A generic native graphics library is not automatically WebGPU-compatible.
  - **Recommended boundary:** Use an optional backend with explicit limits/features and WGSL/resource semantics; validate the supported profile before promising portability.
- [x] **R12 [Partial, Host] Snapshot/export options:** extend existing host/element RGBA snapshots with desired scale, crop, color-space, encoding and asynchronous readback options.
  - **Classification:** Qualified.
  - **Why recommended:** Share snapshot/export requests over existing native snapshots and browser-capable capture paths, with explicit restrictions for arbitrary DOM capture in ordinary browser scripts.
- [?] **R13 [Partial, Host] Compositor/presentation integration:** expose required layer/frame synchronization and texture transport on chosen platforms; Windows D3D11 presentation exists, Linux/macOS GPU work remains paused.
  - **Classification:** Platform.
  - **Why support it:** Provide efficient native compositor/texture presentation needed by demanding native UIs.
  - **Why query/defer it:** A browser app cannot acquire the browser compositor native texture through ordinary DOM APIs, and Linux/macOS GPU transport is paused. This is a platform extension, not shared application API parity.
  - **Recommended boundary:** Expose a separate native presentation capability; any platform expansion requires a separate authorized implementation task, not merely this checkbox.
- [?] **R14 [Host] Printing/PDF/pagination:** expose paginated layout and output through an explicit native printer/PDF path rather than browser print UI.
  - **Classification:** Qualified.
  - **Why support it:** Share report/page descriptions and print/export intentions across native and browser targets.
  - **Why query/defer it:** Browsers do not offer silent PDF/printer control equivalent to native print APIs; pagination, user prompts, fonts and output fidelity differ.
  - **Recommended boundary:** Share page-style and print-request abstractions, qualify actual output capabilities, and distinguish preview/print dialog from deterministic PDF generation.
- [?] **R15 [Missing, Build, Boundary] Shape detection:** BarcodeDetector/FaceDetector/TextDetector return recognized regions/landmarks/text from images where the platform implements them.
  - **Classification:** Qualified.
  - **Why support it:** Share image-analysis requests/results for barcode/text/face-driven UI using browser APIs or native providers.
  - **Why query/defer it:** Availability and recognition outputs vary by OS/provider, and detection models may require optional dependencies; identical input cannot promise identical detections.
  - **Recommended boundary:** Use capability-tested detector interfaces with explicit formats/options and documented result variance; keep providers optional.
- [?] **R16 [Missing, Build, Boundary] Model elements:** HTMLModelElement/Camera embeds and manipulates supported 3D models, subject to platform support.
  - **Classification:** Qualified.
  - **Why support it:** Share model presentation/camera intent where browser and native model providers support it.
  - **Why query/defer it:** HTMLModelElement is not a uniformly available rendering contract; model formats, camera operations and platform backends need a portable subset or a different shared renderer.
  - **Recommended boundary:** Agree supported assets/camera interactions and an optional fallback renderer; do not imply cross-platform model support from one IDL declaration.

## Events, pointer input and accessibility

Source families: dom events, accessibility, Modules/gamepad; host event injection is distinct from payload access.

- [x] **I01 [Missing] Event cancellation signals:** AbortController/AbortSignal and listener signal options cancel groups of subscriptions and asynchronous UI work.
  - **Classification:** Facade.
  - **Why recommended:** Share AbortController/Signal-style cancellation over browser signals and native cancellation/ownership, preserving reason, one-shot state and listener cleanup.
- [x] **I02 [Partial] Standalone/custom event construction:** CustomEvent.detail, typed event constructors/init dictionaries and non-node EventTargets dispatch application-defined payloads without JS objects.
  - **Classification:** Qualified.
  - **Why recommended:** Share typed application events and synthetic UI events, preserving synchronous dispatch/cancellation and target semantics; native payloads need not be JS objects.
- [x] **I03 [Missing, Host] Touch event payloads and injection:** Touch/TouchList/TouchEvent expose coordinates, identity, force/radii and changed touches, with a matching native input delivery path.
  - **Classification:** Qualified.
  - **Why recommended:** Share touch interaction payloads and delivery across browser input and native host devices, with explicit capability reporting.
- [x] **I04 [Partial, Host] Pen/pointer fidelity:** audit/extend pointer type/pressure/tilt/twist/contact geometry, device ids and input injection beyond existing Mouse/PointerEvent getters and pointer capture.
  - **Classification:** Qualified.
  - **Why recommended:** Share pointer/pen interaction logic without losing pressure/tilt/type details; distinguish readable event fields from actual host device support.
- [?] **I05 [Missing, Host] Gesture and force input:** route native pinch/rotate/pan/force interactions into usable callbacks, including any platform-specific WebCore gesture events.
  - **Classification:** Qualified.
  - **Why support it:** Share high-level gesture interactions while allowing native recognizers and browser pointer/touch implementations.
  - **Why query/defer it:** Browser gesture events are inconsistent/nonstandard and OS recognizers differ in arbitration, units and cancellation.
  - **Recommended boundary:** Define portable gesture units/phases and pointer fallback rules; expose provider-specific gestures as extensions instead of copying vendor event names as universal API.
- [x] **I06 [Missing, Host] Relative pointer capture/lock:** pointer-lock requests/state and relative movement support drag-intensive editors or viewports under native window policy.
  - **Classification:** Qualified.
  - **Why recommended:** Share relative-pointer interactions using browser lock or native capture/relative-motion APIs, with explicit activation/permission and loss-of-lock handling.
- [x] **I07 [Partial] Drag/drop object model:** DataTransfer/Item/ItemList/FileList and DragEvent expose files, format lists, effects and drag images beyond existing string-oriented transport.
  - **Classification:** Qualified.
  - **Why recommended:** Share drag/drop formats, files and effects through browser DataTransfer and native data transport, without pretending privileged file access is universally granted.
- [x] **I08 [Partial] Event detail adapters:** typed ErrorEvent, ProgressEvent, animation/transition, content-visibility, form/toggle/command and other UI event records expose useful fields instead of only event names.
  - **Classification:** Engine.
  - **Why recommended:** Complete typed event payloads so the same handlers can inspect relevant engine events on either target.
- [?] **I09 [Missing, Host] Gamepad and haptics:** Gamepad/Button/HapticActuator expose controller state, connection changes and supported vibration effects for native UI/game interactions.
  - **Classification:** Qualified.
  - **Why support it:** Share controller-driven UI and supported haptic requests through browser gamepads or host input adapters.
  - **Why query/defer it:** Polling timing, device layouts, connection identity and vibration capabilities vary; identical button indices/actuators cannot be assumed.
  - **Recommended boundary:** Expose raw standard mappings plus explicit capabilities and optional action mapping; unavailable haptics must be reported.
- [?] **I10 [Partial, Host] Accessibility platform adapters:** build on existing AX tree/actions/events and Windows UIA with selected native platform accessibility integrations; Linux/macOS parity is not inferred from the AX tree alone.
  - **Classification:** Qualified.
  - **Why support it:** Keep application accessibility semantics shared while adapting to each platform accessibility bridge.
  - **Why query/defer it:** Windows UIA does not establish Linux/macOS integration, and accessibility trees/actions differ from browser DOM inspection. Native platform adapters and real assistive-technology tests are substantial.
  - **Recommended boundary:** Share semantic/action contracts, keep native bridge details private and qualify platform readiness; this recommendation does not resume paused platform work.
- [x] **I11 [Partial] Component accessibility internals:** ElementInternals and reflected ARIA element relationships expose component semantics without duplicating the existing AX tree and ARIA attribute setters.
  - **Classification:** Qualified.
  - **Why recommended:** Share component roles/states/relationships through engine internals or a component accessibility facade; form-associated ElementInternals eligibility still depends on the D08 decision.
- [x] **I12 [Host] Native focus/activation/input capability state:** expose host window activation, keyboard modality, touch-point capacity and capture changes consistently to UI logic.
  - **Classification:** Qualified.
  - **Why recommended:** Share focus/activation/modality/capture state using browser events and native window adapters, with explicit unavailable states.
- [?] **I13 [Missing, Boundary] DOM Observable facade:** Observable/Subscriber and EventTarget-associated observable streams expose native event pipelines; compare with Ranvier signals/effects to avoid duplicating its reactive system.
  - **Classification:** Facade.
  - **Why support it:** Allow event-observable-style code to remain shared by adapting browser observables or events to Ranvier subscriptions on native.
  - **Why query/defer it:** Observable operator/completion/error and scheduling semantics need definition; simply aliasing Signal to Observable would conflate current values with event streams and duplicate ownership.
  - **Recommended boundary:** Use a small owned event-stream facade or adapt an existing common implementation; expose full DOM Observable only if a consumer needs its exact operators/terminal semantics.

## Native scheduling, diagnostics and resources

These groups distinguish engine introspection from services .NET/the host already supplies.

- [x] **H01 [Missing, Host] Idle work scheduling:** requestIdleCallback/IdleDeadline/cancelIdleCallback run optional work when the host has spare frame budget.
  - **Classification:** Qualified.
  - **Why recommended:** Share cancellable idle work scheduling through browser idle callbacks and host frame-budget scheduling; a native estimate must not claim an exact future deadline.
- [x] **H02 [Partial, Host] Monotonic time and profiling marks:** Performance.now/timeOrigin/mark/measure expose a shared timing domain; .NET Stopwatch and existing frame timestamps may meet the requirement.
  - **Classification:** Facade.
  - **Why recommended:** Share monotonic timing and named profiling marks through browser Performance and native Stopwatch/trace providers, with a defined time origin and units.
- [?] **H03 [Missing, Build] Performance observation:** PerformanceObserver/EntryList plus paint/event/layout-related entries report renderer timing and interaction costs where implemented.
  - **Classification:** Qualified.
  - **Why support it:** Share performance entries and observers for diagnosing input/rendering costs across targets.
  - **Why query/defer it:** Entry types and measurement boundaries differ and some are unavailable from ordinary browser or native builds; inventing zero values would misrepresent support.
  - **Recommended boundary:** Expose supported entry types and common fields with native extensions; validate units/time origins and avoid claiming cross-engine timing equivalence.
- [?] **H04 [Missing, Boundary] Resource timing:** PerformanceResourceTiming/ServerTiming report load phases and sizes; application resource callbacks may be a better source without browser networking.
  - **Classification:** Qualified.
  - **Why support it:** Share resource-load diagnostics across browser Performance and native application loaders.
  - **Why query/defer it:** Browser privacy restrictions/caches and host loaders expose different phases and metadata; not every DNS/TLS/server field has a meaningful counterpart.
  - **Recommended boundary:** Expose optional phase fields and provenance, report unavailable values explicitly and keep request-control semantics separate from diagnostics.
- [x] **H05 [Host] Font/image/resource completion and failures:** expose owned load/decode/error completion through the existing host resource mechanism, with cancellation and cache control where needed.
  - **Classification:** Qualified.
  - **Why recommended:** Share resource completion/cancellation/error handling across browser loads and the native resource callback system.
- [x] **H06 [Host] Locale/language/time-zone services:** expose application locale and preferred languages for text, sorting and formatting; use .NET/ICU instead of adding a browser Navigator merely for these values.
  - **Classification:** Qualified.
  - **Why recommended:** Share locale/language/time-zone queries through browser Intl/environment and native ICU/.NET/host services; specify locale selection and known data-version differences.
- [x] **H07 [Host] Rendering capability discovery:** report supported ABI features, renderer settings, image/font formats and enabled platform facilities instead of treating an IDL declaration as runtime support.
  - **Classification:** Facade.
  - **Why recommended:** Give shared code a capability API that distinguishes implemented, enabled, available and permitted features; native/host backing is a reason to provide this facade, not to exclude it.
- [x] **H08 [Host] Native inspector hooks:** expose selected DOM/style/layout/accessibility/render-tree diagnostics without requiring a browser developer-tools frontend or JS evaluation.
  - **Classification:** Platform.
  - **Why recommended:** Expose native inspection tools as an extension, with a browser tooling adapter where available; ordinary browser scripts cannot promise access to every internal engine structure.
- [?] **H09 [Host] Paint/style/layout change tracing:** provide scoped diagnostic notifications and counters to explain invalidation, layout and rendering costs in native tools.
  - **Classification:** Platform.
  - **Why support it:** Trace native invalidation/layout/paint for actionable performance diagnosis.
  - **Why query/defer it:** Detailed phases/counters are engine-specific and instrumentation can affect timings; plain browser DOM lacks equivalent internal hooks.
  - **Recommended boundary:** Keep an opt-in diagnostic extension with overhead/provenance metadata, and share only the common trace envelope across providers.
- [?] **H10 [Boundary] Error/reporting records:** ErrorEvent, PromiseRejectionEvent and ReportingObserver/report bodies expose relevant errors/deprecations when there is a native consumer; script/security reports may be irrelevant.
  - **Classification:** Qualified.
  - **Why support it:** Share a typed error/reporting channel over browser records and native engine/task failures.
  - **Why query/defer it:** JS promise rejection/CSP/network report categories do not all exist natively, and exceptions must not be relabelled as equivalent browser failures.
  - **Recommended boundary:** Define common severity/source/cause fields with provider-specific payloads; support browser-only categories as optional extensions.
- [x] **H11 [Missing, Boundary] Binary/file value objects:** Blob/File/FileList/FileReader supply file/image/clipboard/media payloads; .NET buffers/streams/file capabilities can back a shared facade.
  - **Classification:** Qualified.
  - **Why recommended:** Share immutable binary/file values, slicing, metadata and asynchronous reads using browser Blob/File and native buffers/file capabilities; define lifetime, copy and access rules.
- [?] **H12 [Boundary] Text encoding/compression/streams:** TextEncoder/Decoder, CompressionStream and readable/writable/transform streams provide shared data-pipeline contracts with browser and native implementations.
  - **Classification:** Qualified.
  - **Why support it:** Share data pipelines, text encoding and compression code while using browser streams and native .NET implementations.
  - **Why query/defer it:** WHATWG streams have backpressure, locking, close/error/cancel and reader ownership semantics that System.IO.Stream does not implement by itself; encoding failure/BOM and compression format details also matter.
  - **Recommended boundary:** Specify the portable reader/writer/transform contract and exact formats; test closure/cancellation/backpressure rather than declare full browser stream parity from a wrapper.
- [x] **H13 [Boundary] URL parsing/pattern matching:** URL/URLSearchParams/URLPattern support resource identifiers and route matching without implementing browser navigation.
  - **Classification:** Qualified.
  - **Why recommended:** Share URL/query/pattern logic through a common tested contract; WHATWG URL normalization, relative resolution and URLPattern syntax must not silently become different System.Uri/regex semantics.
- [?] **H14 [Boundary] Message channels/structured copy:** MessagePort/Channel/BroadcastChannel, postMessage and structuredClone communicate between execution contexts through browser channels or a native messaging/copy facade.
  - **Classification:** Qualified.
  - **Why support it:** Share typed messaging between background and UI contexts with browser channels and native message queues.
  - **Why query/defer it:** Structured-clone object graphs, cycles, transfer lists and sender detachment differ from passing a CLR object reference; queue ordering and cancellation are observable.
  - **Recommended boundary:** Start with an explicit serializable message/value set and documented copy/transfer rules; add exact structured-clone features only when required.

## Larger adjacent capabilities: explicit scope decisions

These are inventoried so useful native extensions are not lost merely because they live in browser-oriented directories. They are not commitments and are not assumed present in the slim binary.

- [?] **B01 [Build, Boundary] Audio playback/processing:** HTML audio, Web Audio nodes/parameters/offline rendering and AudioSession expose sound through browser-engine or optional native audio providers.
  - **Classification:** Qualified.
  - **Why support it:** Share audio controls and possibly processing graphs across browser and native providers.
  - **Why query/defer it:** Web Audio graphs, timing/automation, device negotiation and real-time threading are larger contracts than sound playback, and native audio libraries are not drop-in graph equivalents.
  - **Recommended boundary:** Split basic playback from Web Audio processing; optional provider packages should expose a tested common graph/profile only where needed.
- [?] **B02 [Build, Boundary] Video/media elements:** HTMLMediaElement/VideoElement/SourceElement, tracks/cues/captions/time ranges and video-frame callbacks expose playback; slim disables video.
  - **Classification:** Qualified.
  - **Why support it:** Share video controls, timeline/caption state and frame callbacks using browser media elements or native playback.
  - **Why query/defer it:** The slim build disables video; codec availability, hardware decode, subtitles, frame scheduling and surfaces vary across backends.
  - **Recommended boundary:** Define a portable playback/caption/frame profile and optional native media package, with explicit supported formats and capabilities.
- [?] **B03 [Build, Boundary] Codecs and raw frames:** WebCodecs encode/decode and AudioData/VideoFrame exchange media with a native pipeline.
  - **Classification:** Qualified.
  - **Why support it:** Share media encoding/decoding workflows and typed frame exchange across browser codecs and native providers.
  - **Why query/defer it:** Buffer ownership, timestamps, codec profiles, flush/backpressure and asynchronous failures require careful compatibility; the native dependency footprint can be large.
  - **Recommended boundary:** Use explicit format/profile/capability negotiation and conformance fixtures; do not promise every browser codec on every native target.
- [?] **B04 [Build, Boundary] Capture and recording:** media devices, image capture, streams/tracks, canvas capture and MediaRecorder expose camera/microphone/screen/media capture.
  - **Classification:** Qualified.
  - **Why support it:** Share camera/microphone/capture requests and resulting tracks/recordings.
  - **Why query/defer it:** Device enumeration, permission prompts, screen selection, frame formats and lifecycle differ by browser/OS and need native integration.
  - **Recommended boundary:** Agree common track/permission/cancellation semantics and optional providers; retain platform extensions for capture modes with no browser counterpart.
- [?] **B05 [Build, Boundary] Media source buffering:** MediaSource/SourceBuffer and managed variants support application-fed playback streams.
  - **Classification:** Qualified.
  - **Why support it:** Share application-fed adaptive media pipelines where the application needs them.
  - **Why query/defer it:** MediaSource append/remove/buffering state has specific semantics that generic native streaming players may not expose, and a simple playback API cannot satisfy it.
  - **Recommended boundary:** Select only with a concrete streaming consumer; implement or qualify the supported source-buffer state machine and formats separately from B02 playback.
- [x] **B06 [Build, Boundary] Media capability queries:** MediaCapabilities tests supported playback/encoding formats and configurations.
  - **Classification:** Qualified.
  - **Why recommended:** Share media capability queries over browser APIs and actual native providers, distinguishing supported/unknown and avoiding optimistic claims about smoothness or efficiency.
- [?] **B07 [Build, Boundary] OS media session/remote playback:** MediaSession actions/metadata, AirPlay/RemotePlayback and Picture-in-Picture connect media to OS controls or external displays.
  - **Classification:** Qualified.
  - **Why support it:** Share OS media actions/metadata and optional picture-in-picture/remote-playback intent.
  - **Why query/defer it:** Browser permission/activation rules, PiP surfaces and remote-device discovery differ from native OS integrations; this broad group contains several independently costly providers.
  - **Recommended boundary:** Recommend common media-session actions/metadata; select PiP and remote playback individually with capabilities and explicit user interaction rules.
- [?] **B08 [Build, Boundary] Encrypted media:** MediaKeys/KeySession and related events support protected media where deliberately required.
  - **Classification:** Qualified.
  - **Why support it:** A shared protected-playback contract can support applications with an actual encrypted-media requirement.
  - **Why query/defer it:** Key-system/CDM availability, provisioning, protected surfaces and platform interfaces are a separate substantial dependency; ordinary crypto or codec wrappers cannot substitute for them.
  - **Recommended boundary:** Defer until there is a named content/key-system requirement, then use optional supported providers and explicit unsupported outcomes rather than emulate EME.
- [x] **B09 [Host, Boundary] Notifications/app badges:** native Notification and badge integration can serve application alerts without a browser permission model.
  - **Classification:** Qualified.
  - **Why recommended:** Share notification and badge intent through browser or native OS providers; permission, persistence, action delivery and availability must be explicit.
- [x] **B10 [Host, Boundary] Wake locks:** keep displays awake during presentation/playback through a native application policy.
  - **Classification:** Qualified.
  - **Why recommended:** Share owned display-wake requests through browser wake-lock or host services, including revocation/loss and cleanup.
- [?] **B11 [Build, Boundary] Speech:** recognition and synthesis can support dictation and spoken feedback using platform speech services.
  - **Classification:** Qualified.
  - **Why support it:** Share speech synthesis/recognition requests and result events through browser or native providers.
  - **Why query/defer it:** Voices/languages, interim recognition, service availability, privacy modes and cancellation vary, so byte-for-byte output or identical recognition cannot be promised.
  - **Recommended boundary:** Define a provider/capability model and common event ordering; keep speech optional and document result variance.
- [?] **B12 [Host, Boundary] Device motion/orientation:** sensor data can drive interaction on relevant hardware; availability and permission handling are host concerns.
  - **Classification:** Qualified.
  - **Why support it:** Share motion/orientation-driven interactions across browser sensors and host hardware.
  - **Why query/defer it:** Many native targets have no sensors; coordinates, sampling rate and browser permission/access limits differ.
  - **Recommended boundary:** Expose availability and coordinate/time conventions, with optional host providers and no fabricated data.
- [?] **B13 [Host, Boundary] Window/display modes:** fullscreen, window positioning/resizing, orientation lock and focus/close state belong to the native window manager, not necessarily the DOM ABI.
  - **Classification:** Qualified.
  - **Why support it:** Share window intents such as fullscreen/focus/close and let native apps opt into richer window control.
  - **Why query/defer it:** Browsers intentionally constrain move/resize/close/orientation operations, so a universal native window-manager API cannot be implemented by an ordinary browser adapter.
  - **Recommended boundary:** Separate portable user-initiated intents from capability-gated native positioning/multiwindow extensions; preserve browser denial outcomes.
- [?] **B14 [Host, Boundary] Contact/location/sharing integrations:** contact picker, geolocation and native share sheets are optional application services with no need for browser navigation.
  - **Classification:** Qualified.
  - **Why support it:** Share contact/location/share requests with browser and native picker/provider implementations.
  - **Why query/defer it:** Permission models, result fields, supported share payloads and user interaction rules differ, while each provider adds platform work.
  - **Recommended boundary:** Use typed results, capability checks and cancellation; select each service independently rather than require one all-platform bundle.
- [?] **B15 [Build, Boundary] XR:** WebXR sessions, input, tracking and rendering layers are a separate immersive-runtime decision; slim stubs this family.
  - **Classification:** Qualified.
  - **Why support it:** Share immersive scene/input logic if an XR application is a real target.
  - **Why query/defer it:** XR tracking, coordinate spaces, sessions, input and GPU layers require a dedicated runtime/backend integration, and the slim build stubs this family.
  - **Recommended boundary:** Treat as an optional XR provider/profile after an explicit use case; preserve reference-space/session semantics and platform capability negotiation.
- [?] **B16 [Build, Boundary] Paint/custom worklets:** CSS paint callbacks and Worklet/PaintWorklet scopes supply programmable rendering; current APIs assume JS execution, so a native callback alternative would require explicit design.
  - **Classification:** Qualified.
  - **Why support it:** Share procedural-paint intent through browser worklets and native F# callbacks.
  - **Why query/defer it:** Existing worklet registration/module/loading and isolation semantics are JS-specific; wrapping their names does not create a native execution environment.
  - **Recommended boundary:** Prefer a common pure paint callback/data contract with browser/native adapters; select actual worklet-module compatibility only if required by an integration.
- [?] **B17 [Build, Boundary] Background execution:** Worker/SharedWorker/ServiceWorker APIs and background fetch cover several execution models; browser workers and native owned execution contexts can support a shared compute/message profile.
  - **Classification:** Qualified.
  - **Why support it:** Share background computation and messaging through browser workers and native Tasks/owned execution contexts.
  - **Why query/defer it:** Dedicated workers, shared workers and service workers have different globals/lifetimes; Tasks do not supply script isolation, origin scope, fetch interception or background activation.
  - **Recommended boundary:** Recommend a portable compute/message facade; decide browser worker/service-worker compatibility separately and do not restore JS execution merely to match surface names.
- [?] **B18 [Build, Boundary] Network/protocol APIs:** fetch/XHR/EventSource/beacon/WebSocket/WebTransport/WebRTC provide communication; a shared facade can use native .NET networking while the slim engine network stack remains stubbed.
  - **Classification:** Qualified.
  - **Why support it:** Share request/response, cancellation, streaming, WebSocket and possibly real-time communication logic using browser networking and native .NET/host providers.
  - **Why query/defer it:** Browser CORS/credentials/cache/forbidden headers and native network privileges differ; WebRTC/WebTransport are distinct substantial protocols. Re-enabling WebCore networking is not required for a facade.
  - **Recommended boundary:** Recommend a bounded HTTP/header/body/cancel facade and selected socket APIs; make browser restrictions explicit and select real-time protocols separately. Do not claim HttpClient reproduces the full Fetch processing model.
- [?] **B19 [Build, Boundary] Browser storage/file-system APIs:** IndexedDB, caches, local/session storage, cookie store, WebDatabase and file handles provide persistence; shared facades need explicit transaction, scope and file-capability contracts.
  - **Classification:** Qualified.
  - **Why support it:** Share persistence/file-access logic through browser storage and native database/file providers.
  - **Why query/defer it:** Transactions, quotas, eviction, origin scoping, file capabilities and cookie semantics differ; a native dictionary is not an IndexedDB implementation.
  - **Recommended boundary:** Recommend portable scoped key/value and explicit transaction/file-capability contracts; choose exact IndexedDB/query compatibility only for a consumer, and leave browser cookie/service-worker cache emulation optional.
- [?] **B20 [Build, Boundary] Browser navigation/lifecycle:** Window/History/Location/Navigation/frame and lifecycle APIs combine browsing-context operations with navigation intent that a shared browser/native routing facade could provide.
  - **Classification:** Qualified.
  - **Why support it:** Share application routes, navigation commands, back/forward intent and lifecycle state through browser history or a native router.
  - **Why query/defer it:** Browser Window/frame/origin/navigation interception and page unload semantics cannot all map to native component transitions.
  - **Recommended boundary:** Recommend a route/navigation facade; keep browsing-context/frame/script globals deliberately outside it and document lifecycle differences rather than pretend to create a browser.
- [?] **B21 [Boundary] Browser identity/security/permissions:** credential management, WebAuthn, digital identity, payments/Apple Pay, permission queries, trusted types and CSP/cross-origin state are separate application/security decisions.
  - **Classification:** Qualified.
  - **Why support it:** Share credential/passkey/payment/permission intents through browser and native platform providers where a concrete app needs them.
  - **Why query/defer it:** This group combines unrelated security-sensitive services, browser origin identity/trusted types and platform-specific payment systems; a facade must not invent equivalent security guarantees.
  - **Recommended boundary:** Split identity, passkeys, payments and capability permissions into optional contracts with explicit trust/provider boundaries; do not emulate a browser origin or bypass browser restrictions.
- [x] **B22 [Boundary] Crypto:** Web Crypto random/digest/key operations can share a supported algorithm/key contract across browser and established native/.NET providers.
  - **Classification:** Qualified.
  - **Why recommended:** Share supported cryptographic/random operations through browser Web Crypto and established native providers; require an explicit algorithm/encoding/key-lifetime profile and surface unsupported operations rather than implement new cryptography.

- [?] **B24 [Boundary] Cross-context locks:** Web Locks coordinates named locks across contexts; native synchronization can back a shared facade when scope, ordering and lifetime guarantees are explicit.
  - **Classification:** Qualified.
  - **Why support it:** Share scoped named-lock coordination across supported execution contexts.
  - **Why query/defer it:** Browser Web Locks is origin/context scoped; an in-process SemaphoreSlim is not a cross-process equivalent. Fairness, cancellation, ownership and crash release need a contract.
  - **Recommended boundary:** Define session/application/process scope explicitly and use suitable native synchronization; expose broader browser-style lock semantics only if tested providers can honour them.
- [?] **B25 [Boundary] Browser platform reporting/push:** push subscriptions, service-worker notifications and security/network reports are browser backend infrastructure rather than renderer introspection.
  - **Classification:** Qualified.
  - **Why support it:** Share background notification/message intent where browser push and native OS/server providers can deliver it.
  - **Why query/defer it:** Push subscriptions, background fetch and service-worker activation have browser-specific lifetimes and infrastructure; native delivery needs its own provider and cannot be inferred from B09 notifications.
  - **Recommended boundary:** Split a portable background-message contract from browser service-worker plumbing; implement only with a concrete delivery/backend requirement, leaving browser-only reports as extensions.

## Precise gaps already evidenced

These anchor the broader inventory in concrete omissions; they remain evidence, independent of the recommendation marks.

- `page/DOMWindow+CSSOMView.idl` defines matchMedia, but the native headers/model do not export it.
- `css/DOMCSSNamespace.idl` defines CSS.supports/escape/highlights; namespaces are skipped by the current generator's IDL model rather than emitted as interfaces.
- `gen/dom-report.txt` explicitly omits Document.fonts, Document/ShadowRoot.adoptedStyleSheets, Element/Range.getClientRects, SVGGeometryElement.getTotalLength/getPointAtLength and SVGGraphicsElement.getCTM.
- `animation/Animatable.idl`, KeyframeEffect.idl and WebAnimation.idl define creation/effects/commit/lifecycle APIs; legacy rv_animation_* only covers a subset.
- ResizeObserver exists in the legacy ABI, but its callback is only `(target, width, height)`; IntersectionObserver uses a viewport root and returns only target/intersecting/ratio.
- Core listener support exists; hundreds of omitted `on...` convenience properties are **not** hundreds of missing event capabilities. The remaining work is specific event payloads/options/targets, not duplicating listeners under every spelling.
- The generated Views report explicitly lists SVG style/data/focus-related mixin members as unmapped; low-level StyledElement style get/set already works for SVG.

## Selection workflow

The current marks are captured in the linked task document; `[?]` and `[!]` are scope notation rather than completed-task checkboxes (some Markdown viewers render them as plain text). Split broad groups into named operations/providers where the recommended boundary requests it. Distinguish desired shared capability from exact browser-object compatibility, and record later scope changes explicitly rather than allowing implementation to expand this snapshot. For accepted groups, specify the portable F# contract, each browser/native provider, capability/deviation matrix and acceptance tests. This review does not install tools, change build flags, resume paused platform work or implement any capability.

## Verification limits

This is a source/export/configuration inventory. No candidate bridge was implemented or compiled, and no runtime probe was performed for each optional WebCore feature. Broad groups deliberately include related constructors, options, enums and collections so the review is complete at API-family level; selection is not an instruction to expose every browser-shaped member verbatim. Native facilities with no WebIDL are listed as host candidates, not falsely attributed to a standardized DOM API. Full cross-platform support remains unverified until a selected capability has concrete tests on each target.

## Sources for compatibility distinctions

- [DOM Standard: mutation observers](https://dom.spec.whatwg.org/#mutation-observers) — observer delivery concerns actual DOM mutations; this differs from Ranvier signal dependency tracking.
- [HTML Standard: custom elements and element internals](https://html.spec.whatwg.org/multipage/custom-elements.html#the-elementinternals-interface) — registration/reactions and form-associated element internals have eligibility and lifecycle rules beyond ordinary template construction.
- [Streams Standard](https://streams.spec.whatwg.org/) — reader ownership, backpressure and terminal states require a defined stream facade rather than an unchecked type alias.
- [Fetch Standard](https://fetch.spec.whatwg.org/) — requests/responses include browser processing and policy semantics beyond an HTTP transport; a portable facade must state its boundary.
- The pinned local source/export ledger remains the authority for this audit's API inventory; current web specifications clarify semantics and do not prove the pinned build implements newer features.
