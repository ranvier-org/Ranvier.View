# G08 shared matrix values

## Approved source and API

Append matrix factories and the generated DOMMatrix/DOMMatrixReadOnly operations to the G07 geometry service. Use actual browser/WebCore mathematics, default handling and errors. Canonical Native input is fe51afb53d7c372788cdcebf9d41a9c9d2dd1e0e (PR126), 143 interfaces/1875 exports/23 roots, private Native16/ABI24 runtime. Public release/platform acceptance remains pending.

GeometryService adds CreateMatrix and CreateReadOnlyMatrix overloads for omission, string and double array, plus MatrixFrom and ReadOnlyMatrixFrom taking DOMMatrixInit. Null numeric arrays are rejected consistently. Omission differs from present empty sequence; string null follows WebIDL conversion. DOMMatrixInit retains optional aliases and is2D so the engine validates equality and flags. Native wrappers retain their creation document for setMatrixValue's checked context.

The first canonical import produced fresh FCS FS0249: its DOMPointInit class conflicted with G07's unpublished manual record. Parent approved using canonical DOMPointInit and DOMMatrixInit classes, removing the manual point record, and adapting providers/fixtures to lowercase fields/defaults. G07's prior source/API/runtime snapshots remain historical and unchanged. DOMRectInit and DOMQuadInit remain facade DTOs. No generated output is patched manually.

## Ordered work

- [x] Commit independently reviewed G07 focus checkpoint: Core a3fffc5, Views824002b; preserve 177/26/3 results and frozen ABI23 hashes.
- [x] Inspect canonical source/model; obtain internal review of the eight factory overloads and dictionary collision resolution; fresh Core FCS clean after removal.
- [x] Prove overload/default/null shapes in Sage; persist Core service and browser provider.
- [x] Extend backend optional-double pointer inputs, dictionary defaults, Matrix24 release/cache kind and document context; regenerate with zero unmapped members and compare old outputs.
- [x] Implement native factories using owned trusted-subtype results and scoped dictionary/union inputs; independently review.
- [x] Shared actual matrix constructors, all elements/aliases, ordered math/self families, singular/nonfinite behavior, exact engine errors, string parsing/replacement, preservation and homogeneous transforms.
- [x] Native ownership/thread/document/session/host plus browser navigation; strict focused/full count guards, JIT/AOT/Chromium evidence and executed DLL hashes.
- [x] Combined repository/analyzer/conformance gates; source checkpoints follow the reviewed final evidence.
- [ ] Official Native16/managed dependencies, all platforms and immutable profile acceptance.

No toJSON or named Float32Array/Float64Array adapters are required by selected G07/G08 scope. Matrix doubles remain lossless. No math implementation or JavaScript dependency is introduced into native execution.

## Private verification

The final shared fixture passes 710 assertions on Windows JIT/NativeAOT and Chromium156.0.8078.4. Native lifetime checks pass23; browser actual document replacement passes3. G07 remains177 shared/26 native lifetime/3 browser context using canonical PointInit. One final combined batch passes2642 shared+136 lifetime on Windows JIT/AOT and2655 shared+19 context on Chromium, plus196609 CSS oracle checks and Tab behavior. All six executed native DLL comparisons match the coherent ABI24 stage.

Independent review found a real shared scope gap in the initial675 case: successful literal matrix3d parsing and finite3D serialization were only proven by the raw native fixture. The final710 adds all16 values and flags on parse and roundtrip plus explicit present-false is2D. Earlier675 logs are preserved, and the changed fixture justifies the final rerun. Both provider source reviews are clear; full fixture/evidence review is separate.

Direct JavaScript independently reproduces Chromium's inverse coefficient precision: scale(2,3) inverts d to0.3333333432674408 and f to-6.6666669845581055, yielding y0.9999999105930328 for the roundtrip. Only that four-component inverse assertion uses measured1e-6 tolerance. Other operations keep1e-12, and constructor/setter storage remains exact. Neither provider rounds results. The direct probe and hashes are recorded in matrix-values-runtime-evidence.json.

Null numeric arrays reject on both providers; observable non-DOM exception names differ (native ArgumentNullException/browser Error) and are explicitly qualified. WebIDL engine errors preserve TypeError/SyntaxError/InvalidStateError. Existing FSharp.Core and SameValueZero AOT warnings remain visible. The generated report has133 classes/1922 members/zero unmapped;247 prior wrapper class bodies remain identical, and only the two existing point classes gain matrixTransform. Matrix classes and trusted query dispatch are additive.

Release allocation is separate: Coreee8092d9 and Views35978f8 reserve alpha16/0.1.6/alpha6 only after the release lane checked all16 exact consumer package/version pairs, tags and competing PRs. No official Native16 package or final merged release SHA is claimed. Frozen0.1.2/0.1.3 profiles and historical G07 evidence remain unchanged.

Repository gates pass: exact143/1875/23 canonical source check; Core1568, CSharp95, Wire290 and traced1833 tests (13 existing skips); Core normal/traced library analyzers; Views library/view/examples analyzers; ordinary SDL backend build0warnings0errors. Core matrix-values-repository-gates.json records command/log hashes and the redundant second Core analyzer invocation. The separately documented additional SDL analyzer/FCS issue14 remains open and was not relabeled passing.
