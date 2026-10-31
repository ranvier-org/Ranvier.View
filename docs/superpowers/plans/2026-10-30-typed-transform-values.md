# C16 shared typed transform values

## Core and view state

Core primitives used: existing DOM service contracts, StyleValueException and generated Typed OM/geometry interfaces. The native provider reuses Session.Object identity, exported-reference ownership, retained acquisition documents and thread/session guards. No reactive ownership or scheduler implementation is duplicated.

View-owned state: a borrowed provider's disposal bit and existing native wrapper/document anchors. Transform lists, numeric values, component state, validation and matrix mathematics remain owned by the actual engine.

## Approved API

Add a separate TransformValueService and browser/native TransformValues module, leaving StyleValueService implementors unchanged. Factories return actual generated objects:

- CreateList(CSSTransformComponent array): CSSTransformValue.
- CreateTranslate(x:CSSNumericValue,y:CSSNumericValue) and the explicit three-argument overload with z:CSSNumericValue: CSSTranslate.
- CreateRotate(angle:CSSNumericValue) and CreateRotate(x/y/z:DoubleOrCSSNumericValue,angle:CSSNumericValue): CSSRotate.
- CreateScale(x/y:DoubleOrCSSNumericValue) and the explicit three-argument overload with z:DoubleOrCSSNumericValue: CSSScale.
- CreateSkew(ax/ay:CSSNumericValue), CreateSkewX(ax), CreateSkewY(ay).
- CreatePerspective(CSSNumericValueOrDOMStringOrCSSOMKeywordValue): CSSPerspective.
- CreateMatrixComponent(matrix:DOMMatrixReadOnly,?is2D:bool): CSSMatrixComponent.
- Get(list:CSSTransformValue,index:uint32): CSSTransformComponent option, normalizing native null/browser undefined for missing indexed values.

Reuse StyleValueException for engine exception names. Null containers/reference arms reject before union coercion. Two-argument construction differs from explicit z in component is2D. Actual Fable compilation erased Some null in the initial optional-z design, accepting explicit null as omission. The approved thirteen-method surface uses explicit two/three-argument overloads to preserve presence without a global compiler flag. The canonical CSSMatrixComponentOptions dictionary is constructor-only and is not emitted as a public class in the current generated contract; optional is2D preserves absence versus present false without a duplicate DTO.

Provider disposal invalidates calls without mutating previously returned objects. Native factories validate same-session arguments and document/thread lifetime; browser factories use the selected active document's realm and intrinsic brand checks. A retained component/list anchors its acquisition context so returned matrices can use setMatrixValue correctly. No global registry is added.

Native service calls and generated operations for these ten new interfaces retain their original managed arguments through a finally/GC.KeepAlive boundary. Component arrays retain their members; numeric/union/matrix objects remain alive after raw handles are extracted. No old wrapper bodies change.

## Source and isolation

Core worktree C:/Users/shaya/.codex/worktrees/native-api-transform-values/Ranvier starts from 88109eac; Views C:/wk/rv-transform-values starts from d11697c9. Both use codex/native-api-transform-values. The Native16 consumer trees stay clean for release. The app worktree tool created Core; it rejected Views sourceRoot outside the calling chat's roots, so the authorized git-worktree fallback created Views.

The initial producer snapshot in C:/wk/native-css-transforms selected 153 interfaces, 1926 functions, 24 roots and 21 dictionaries. Its hashes remain in ignored artifacts/c16-input/input-hashes.json as historical local input. The final pushed source and reserved cohort are recorded below. Root owns all native generator/engine repairs; the initial defective runtime was never used as acceptance.

## Consumer mechanics and actual boundary evidence

- Append component cache/release kind 25; preserve all earlier kinds. Add trusted CSSTransformComponent subtype dispatch and CSSTransformValue CSSStyleValue dispatch.
- Preserve owned references, same-object numeric getter/list component identities and independent toMatrix snapshots. Propagate acquisition-document anchors to matrix results before wrapper construction.
- Add bounded same-session validation for new component/matrix reference inputs. Existing Interop.valueHandle only accepts StyleValue/VariableReferenceValue; leave those paths unchanged.
- Scope finite CSSNumberish checks to restricted numeric union arms; unrestricted geometry doubles remain unrestricted.
- Shared Get maps native null and browser undefined to None; raw indexed behavior remains separately qualified.
- Actual Chromium 156.0.8078.4 accepts list[uint32.MaxValue] as an ordinary own property, leaves length/serialization unchanged, then returns that property on read. Index 2 with length 1 raises RangeError. The canonical selected indexed-operation guard closes this facade escape; generated output is never patched manually.
- Actual new CSSScale(null,3) and false coerce to numeric zero. Numeric reference arms must reject null/forged references before coercion. CSSNumericValue.prototype.type.call verifies real numeric values and rejects fake/keyword objects without exposing C15 APIs.

Owned direct-browser evidence is artifacts/c16-browser-boundaries.mjs/json. Actual generated-Fable RED/GREEN probes show canonical intrinsic branding rejects null/forged reference arms and the MaxValue guard raises RangeError without creating an expando. Canonical LF source SHA256 97e4239839bbe0c97d219f0773244775f505ba0722099b2282015addb09905bf equals compiled Core source d662c67919b6a270122f88175f1d4dbc0b8ce3965be55c4e5349748702514611 after UTF-8 CRLF normalization, proved in artifacts/c16-fable-input-provenance.json.

The original optional-z presence RED remains in artifacts/c16-provider-presence-probe.json. After explicit overloads, artifacts/c16-provider-explicit-arity-green.json records both two-argument defaults as 2D, explicit null numeric/DU/reference rejection, forged-reference rejection, and optional is2D=false. A preliminary package operation left Fickl's project-reference cracking pointed at missing fable_modules source paths; a fresh source-project probe consumed the exact eighteen current Core/Fable compile inputs and passed. Sage hard-reset/rebuild and explicit reflection checks verified all thirteen persisted methods. No native C16 runtime acceptance follows from these browser/type checks.

The first shared browser run exposed a Chromium flag discrepancy: a component with is2D=false and a genuinely 2D stored matrix returned an independent matrix with is2D=true. Direct engine evidence remains in artifacts/c16-matrix-flag-probe.mjs/json. The shared assertion was preserved. The canonical selected toMatrix adapter now copies all sixteen unchanged numbers through the returned matrix's own constructor only for false-owner/true-result flags. Canonical generated source SHA256 6daf97e8bf7f378f660fd094a365e57693a3379b439858e1b5fb367f00d342bc passes the original flag assertion plus all coefficient/copy/identity checks.

Historical private ABI24 Windows JIT proof passed 430 shared assertions plus 21 typed-map/render effects and 47 native lifetime checks; Chromium 156.0.8078.4 passed the same 430+21 shared cases. Final ABI25 results below supersede this checkpoint. The native runtime stage and every executed file were SHA256-checked after builds. The bounded generator maps all 1963 members with zero unmapped; all 251 pre-existing emitted class blocks remain byte-identical after newline normalization, with ten new classes and new runtime dispatch only (artifacts/c16-wrapper-diff.json).

Cross-document lifecycle tests retain a matrix acquired in a second document when assigning it to a component from the first, preserve public same-object identity on repeated getters, and exercise setMatrixValue after both secondary providers are disposed. The exact first-document field is verified statically through the cache-hit path; it is not directly observable while both documents remain live. No reflection, test-only production API or additional AOT metadata was added. Native nested hosts are disposed in reverse installation order.

The reserved cohort is Native preview.17/ABI25, Core DOM alpha.17, Views 0.1.7 and backend/SDL alpha.7. The release owner independently verified all sixteen consumer package versions and matching tags/PR reservations absent (C:/wk/rv-consumer17-allocation-check.json). Separate allocation checkpoints are Core 39243bdb and Views 624c14f5; Core pin checkpoint 06162703 selects the passing pushed producer. Frozen Native16 consumer trees remain unchanged.

## Ordered work

- [x] Read selected producer scope/raw fixture/current rules; isolate both consumer trees; obtain source-backed API approval and fresh clean Core FCS.
- [x] Probe actual missing-index/max-index/null numeric coercion before selecting canonical guards.
- [x] Prove service overloads, optional union shapes and validation helpers in Sage; persist documented Core contract and browser provider.
- [x] Probe generated Fable reference arms; coordinate canonical null/brand/maximum-index guards with root.
- [x] Implement native factories, append component ownership kind, regenerate wrappers and compare old bodies after independent review.
- [x] Wait for root's passing pushed producer source/runtime, then select a separate private package/runtime cache without changing release16 trees.
- [x] Shared component constructors/setters/serialization/is2D defaults, invalid-preservation, list ordering/indexing, toMatrix independence and typed-map/render effects.
- [x] Native thread/session/document/host/provider lifecycle and browser navigation; exact JIT/AOT/Chromium guards and executed binary/log hashes.
- [x] Independent source review, required combined runtime/repository gates and bounded source checkpoints.
- [ ] Independent final durable-evidence audit and official producer/consumer/platform/profile acceptance; C15/C17 and Anime remain outside this scope.

## Final private ABI25 runtime checkpoint

The pushed producer is 4091b07c5bff99d7acbe91c717165e7f94f40f81 ([Native PR127](https://github.com/ranvier-org/Ranvier.WebCore/pull/127)). Canonical generated source 07a00eac135210b9c80cfe947612e9f7d3c9e697de8afda8ca517c4a6dd0b04b passes Core formatting and matches the packaged/compiled browser source exactly. The final scoped adapter tests include perspective-none and an appended 3D transform list, asserting false matrix flags, all sixteen ordered coefficients and independent copies. Empty lists reject with the exact TypeError.

Focused Windows JIT and NativeAOT pass 471 shared assertions (450 component/matrix cases plus 21 typed-map/render effects), 47 native lifetime assertions and clean teardown. Packaged Fable Chromium 156.0.8078.4 passes 471 shared plus 3 actual document-replacement checks. The single combined batch passes 3113 shared/183 lifetime on both native modes and 3126 shared/22 context in Chromium, alongside the existing 196609 CSS oracle and Tab traversal. Every one of 24 executed runtime files was SHA256-verified before each native run; no runtime source or binary changed between the focused and combined gates.

The six exact logs, four executed-runtime manifests, actual source/package/compiled artifact digests, historical RED qualifications and private/public boundaries are in docs/research/native-api-delivery/typed-transform-values-runtime-evidence.json. Cohort17 regeneration with its own managed interop produces both committed outputs byte-for-byte. Core tests also pass 1568 normal, 95 C#, 290 wire and 1833 traced with 13 existing skips. Core's library analyzer pipeline passes both normal and traced checks; Views library/view/examples analyzer groups and the ordinary SDL/backend build also pass. The Core repository's docs/research/native-api-delivery/typed-transform-values-repository-gates.json records all ten successful logs and both final clean FCS checks, including preserved restore/bootstrap diagnostics and child-only cache configuration. Official Native17/DOM17/Views 0.1.7 and non-Windows/platform/profile acceptance remain open; no group is marked complete from these private checks.

## Reload breakpoint

Stop after these documentation checkpoints at the user's requested agent-limit reload. No new PR, build, publication or platform work starts here. All owned CLI processes have completed; Sage session0a6d690f is stopped and no owned leases remain live. Shared Serena was left untouched. Resume with independent evidence inspection, then coordinate the Core17/Views0.1.7 PR stack and exact released Native17 pin/package/platform/profile gates with the parent and release owner. Preserve historical runtime snapshots and do not repeat the full private suite for identical source or documentation-only integration.
