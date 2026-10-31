# G07 shared geometry values

## Scope and source

Actual DOMRect/DOMRectReadOnly, DOMPoint/DOMPointReadOnly and DOMQuad operations share a borrowed GeometryService. Core defines optional DOMPointInit/DOMRectInit/DOMQuadInit dictionaries; browser and native providers call their engines. Null dictionaries use empty-dictionary defaults. Doubles preserve NaN, infinities, signed zero and precision beyond float32. G08 matrix operations and toJSON are outside this checkpoint.

Canonical local Native source is 829078076a616dc3f757773b505448c3e2f05a4e (141 interfaces, 1775 exports, 22 roots). The private ABI23 runtime is frozen under artifacts/g07-runtime-stage; no producer pin/version/publication claim follows from these tests. Parent coordinates the combined G07/G08 Native16 consumer cohort.

## Implementation and regression

- [x] Core service, explicit optional dictionaries, browser document-realm provider, packaged standalone compile list and fresh FCS checks.
- [x] Native document/host provider, exact scalar dictionary defaults, pinned nested dictionary lifetimes and owned cached results.
- [x] Append Point22/Quad23 cache kinds; preserve all existing IDs/releases. Canonical runtime queries select mutable/read-only point and rectangle wrappers.
- [x] Reproduce generator failure for missing Point release kind, then generate with zero unmapped members.
- [x] Reproduce inherited-class mutable getter ambiguity FS0361 in Sage. Explicitly reimplement the base interface when a derived interface redeclares matching property signatures; verify both interface views agree. Real generated native build compiles all eight mutable property slots.
- [x] Independently review Core/browser/native provider and bounded emitter changes with parent.
- [x] Actual Windows JIT/AOT 177 shared +26 lifetime; Chromium 177 shared +3 document replacement. Strict focused/full count guards; native teardown is clean.
- [ ] Independent fixture review, bounded commits and combined G08 repository gates.
- [ ] Public dependency/package, non-Windows and immutable profile acceptance.

The generated report has 131 classes and 1786 members: 1773 mapped, 13 hand-written, zero unmapped. Old wrapper body changes are restricted to geometry: mutable rectangle slots, explicit inherited getter implementation, and DOMRect results using trusted runtime dispatch (list item, Range bounds, Element bounds). Other class bodies remain byte-identical after normalizing line endings.

## Verification limits

These are focused private checks, not full acceptance. The complete conformance/analyzer/platform suites are deliberately batched with G08. No public Native16 package was consumed. Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 AOT warnings remain visible. Browser disposal is observed as Exception:GeometryService; native disposal is ObjectDisposedException, without asserting typed exception parity. Native parent anchoring keeps a quad reachable while its retained point exists; no forced native owner destruction is claimed.

Exact commands, counts, binary hashes and logs are recorded in docs/research/native-api-delivery/geometry-values-runtime-evidence.json. Prior C13 checkpoints/profiles and the preserved FCS analyzer issue14 remain unchanged.
