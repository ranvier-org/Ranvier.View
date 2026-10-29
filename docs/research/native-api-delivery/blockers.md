# Native API delivery blockers

No conditional task is deferred at initialization. A06-A09 and A11 remain current-scope implementation candidates.

Only a demonstrated requirement to reintroduce dependencies or require JavaScript can defer a conditional task under the user's active goal. Generator gaps, routine native bridging, unknown platform status, cost and slow builds are work to resolve, not permitted deferral reasons. Any allowed deferral must identify the exact dependency/JS requirement, attempted native route and evidence.

Required x tasks remain required. Unavailable runtime evidence is tracked as incomplete, never as an implemented capability.

## C07 browser constructor profile

Native WebCore honours CSSStyleSheetInit.baseURL: the shared fixture resolves relative image URLs against the supplied URL. The inspected Chromium runtime silently ignores that option and resolves against the document base instead. The facade exposes SupportsBaseUrl and rejects explicit options on that runtime. Chromium acceptance therefore verifies capability reporting/rejection and default base resolution; it does not prove explicit-base support. No required group is marked complete solely because the unsupported option is labelled. Published-platform/package acceptance and the final scope audit remain pending.

Chromium adoption also modifies its observable list incrementally before an invalid entry throws. The provider now restores the previous ordered snapshot on failure; the same atomicity assertion passes against Chromium and native WebCore. This defect is resolved in the facade, not deferred.
