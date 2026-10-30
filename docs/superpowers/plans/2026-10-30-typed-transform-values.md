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

Core worktree C:/Users/shaya/.codex/worktrees/native-api-transform-values/Ranvier starts from88109eac; Views C:/wk/rv-transform-values starts fromd11697c9. Both use codex/native-api-transform-values. The Native16 consumer trees stay clean for release. The app worktree tool created Core; it rejected Views sourceRoot outside the calling chat's roots, so the authorized git-worktree fallback created Views.

Producer C:/wk/native-css-transforms currently selects153 interfaces/1926 functions/24 roots/21 dictionaries. Snapshot hashes live in ignored artifacts/c16-input/input-hashes.json. This is local uncommitted input, not a passing source pin. Existing versions and published pin remain unchanged. Root owns all native generator/engine repairs; the initial defective runtime is never used as acceptance.

## Consumer mechanics and actual boundary evidence

- Append component cache/release kind25; preserve all earlier kinds. Add trusted CSSTransformComponent subtype dispatch and CSSTransformValue CSSStyleValue dispatch.
- Preserve owned references, same-object numeric getter/list component identities and independent toMatrix snapshots. Propagate acquisition-document anchors to matrix results before wrapper construction.
- Add bounded same-session validation for new component/matrix reference inputs. Existing Interop.valueHandle only accepts StyleValue/VariableReferenceValue; leave those paths unchanged.
- Scope finite CSSNumberish checks to restricted numeric union arms; unrestricted geometry doubles remain unrestricted.
- Shared Get maps native null and browser undefined to None; raw indexed behavior remains separately qualified.
- Actual Chromium156.0.8078.4 accepts list[uint32.MaxValue] as an ordinary own property, leaves length/serialization unchanged, then returns that property on read. Index2 with length1 raises RangeError. A canonical selected indexed-operation guard is required; generated output is never patched manually.
- Actual new CSSScale(null,3) and false coerce to numeric zero. Numeric reference arms must reject null/forged references before coercion. CSSNumericValue.prototype.type.call verifies real numeric values and rejects fake/keyword objects without exposing C15 APIs.

Owned direct-browser evidence is artifacts/c16-browser-boundaries.mjs/json. Actual generated-Fable RED/GREEN probes show canonical intrinsic branding rejects null/forged reference arms and the MaxValue guard raises RangeError without creating an expando. Canonical LF source SHA256 97e4239839bbe0c97d219f0773244775f505ba0722099b2282015addb09905bf equals compiled Core source d662c67919b6a270122f88175f1d4dbc0b8ce3965be55c4e5349748702514611 after UTF-8 CRLF normalization, proved in artifacts/c16-fable-input-provenance.json.

The original optional-z presence RED remains in artifacts/c16-provider-presence-probe.json. After explicit overloads, artifacts/c16-provider-explicit-arity-green.json records both two-argument defaults as 2D, explicit null numeric/DU/reference rejection, forged-reference rejection, and optional is2D=false. A preliminary package operation left Fickl's project-reference cracking pointed at missing fable_modules source paths; a fresh source-project probe consumed the exact eighteen current Core/Fable compile inputs and passed. Sage hard-reset/rebuild and explicit reflection checks verified all thirteen persisted methods. No native C16 runtime acceptance follows from these browser/type checks.

The first shared browser run exposed a Chromium flag discrepancy: a component with is2D=false and a genuinely 2D stored matrix returned an independent matrix with is2D=true. Direct engine evidence remains in artifacts/c16-matrix-flag-probe.mjs/json. The shared assertion was preserved. The canonical selected toMatrix adapter now copies all sixteen unchanged numbers through the returned matrix's own constructor only for false-owner/true-result flags. Canonical generated source SHA256 6daf97e8bf7f378f660fd094a365e57693a3379b439858e1b5fb367f00d342bc passes the original flag assertion plus all coefficient/copy/identity checks.

Exploratory private ABI24 Windows JIT proof passes 430 shared assertions plus 21 typed-map/render effects and 47 native lifetime checks; Chromium156.0.8078.4 passes the same 430+21 shared cases. This is not final ABI25 acceptance. The native runtime stage and every executed file were SHA256-checked after builds. The bounded generator maps all1963 members with zero unmapped; all251 pre-existing emitted class blocks remain byte-identical after newline normalization, with ten new classes and new runtime dispatch only (artifacts/c16-wrapper-diff.json).

Cross-document lifecycle tests retain a matrix acquired in a second document when assigning it to a component from the first, preserve public same-object identity on repeated getters, and exercise setMatrixValue after both secondary providers are disposed. The exact first-document field is verified statically through the cache-hit path; it is not directly observable while both documents remain live. No reflection, test-only production API or additional AOT metadata was added. Native nested hosts are disposed in reverse installation order.

The final reserved cohort is Nativepreview17/ABI25, Core DOMalpha17, Views0.1.7/backend+SDLalpha7. The release owner independently verified all sixteen consumer package versions and matching tags/PR reservations absent (C:/wk/rv-consumer17-allocation-check.json). Version allocation will be a separate checkpoint; the final producer pin awaits root's passing pushed source. Frozen Native16 consumer trees remain unchanged.

## Ordered work

- [x] Read selected producer scope/raw fixture/current rules; isolate both consumer trees; obtain source-backed API approval and fresh clean Core FCS.
- [x] Probe actual missing-index/max-index/null numeric coercion before selecting canonical guards.
- [x] Prove service overloads, optional union shapes and validation helpers in Sage; persist documented Core contract and browser provider.
- [x] Probe generated Fable reference arms; coordinate canonical null/brand/maximum-index guards with root.
- [x] Implement native factories, append component ownership kind, regenerate wrappers and compare old bodies after independent review.
- [ ] Wait for root's passing pushed producer source/runtime, then select a separate private package/runtime cache without changing release16 trees.
- [ ] Shared component constructors/setters/serialization/is2D defaults, invalid-preservation, list ordering/indexing, toMatrix independence and typed-map/render effects.
- [ ] Native thread/session/document/host/provider lifecycle and browser navigation; exact JIT/AOT/Chromium guards and executed binary/log hashes.
- [ ] Independent source/evidence review, required combined repository gates and bounded source checkpoints.
- [ ] Official producer/consumer/platform/profile acceptance; C15/C17 and Anime remain outside this scope.
