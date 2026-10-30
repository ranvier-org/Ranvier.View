# C13 typed style map conformance

## Selected scope

Provide actual inline, declared and computed typed style maps through the generated shared DOM interfaces and borrowed StyleMapService. Preserve get/getAll/has/size, set/append/delete/clear and ordered entry snapshots containing independently owned typed values, including nullable slots. No C15 arithmetic or C17 color expansion is included.

## Completed private implementation and proof

- [x] Import canonical 1739-export producer metadata, append map cache kind21, preserve all old wrapper bodies, and support explicit checked document context, undefined monostate and nullable-element sequences.
- [x] Resolve acquisition context before constructing finalizable wrappers; independently review ownership and entry cleanup. Ownerless declared acquisition prevalidates the explicit provider document without rebinding a rule.
- [x] Exercise all selected map operations, missing values, actual computed effects, typed value independence, list order/cardinality, priority replacement, error preservation, null/forged inputs and NUL/USV handling.
- [x] Verify all six declared-map mutations against identical cached sheets with complete surviving CSSOM and meaningful owning/survivor layout.
- [x] Verify inline/declared/computed entries, insertion order, Unicode names, independent snapshots, retained values, document/thread/session/provider lifetime and old-document browser navigation.
- [x] Require exact focused/full counts:127shared,33native lifetime,3browser navigation.
- [x] Run focused Windows JIT, Windows NativeAOT and Chromium; run the complete combined harness once per target with Tab and native teardown.
- [x] Persist log hashes and executed DLL hashes in the delivery evidence JSON.
- [ ] Complete final independent source review, broader repository gates, coordinated producer pins and source commits.
- [ ] Verify platform CI and released normal/traced cohort15 consumers before acceptance.

Actual private combined results: Windows JIT and NativeAOT1755shared+87lifetime, Chromium1768shared+13context,196609CSS.escape oracle comparisons and Tab. Focused C13 is127shared on all three targets,33native lifetime and3browser navigation. Exact commands/log hashes and qualifications are in ../../research/native-api-delivery/typed-style-maps-runtime-evidence.json.

## Qualifications

Malformed partial native snapshot conversion is source-reviewed, not fault-injected at runtime. Natural fixture values do not synthesize null slots; transport/decoder preserve them explicitly. No public15/platform/traced acceptance follows solely from these private checks.

The initial guessed alphabetical inline-entry assertion was corrected after source and both actual engines proved insertion order. The real browser null typed-reference failure was repaired in the canonical generator, retaining the shared regression.

A private computed-entry crash was traced to mismatched JavaScriptCore bytes retained by PreserveNewest, not an engine defect. All three DLLs in actual executed JIT/AOT directories were hash-verified after the final build/publish; timestamps alone are insufficient. The raw minimal reproduction passed and the unchanged managed fixture passed after replacing only the owned output JSC with its coherent candidate.

Sage session f9bcddfa compiles assert expressions away. Explicit if/failwith checks established context-pairing RED/GREEN and service/type shape; final executable assertions all use explicit failures. No daemon restart occurred.
