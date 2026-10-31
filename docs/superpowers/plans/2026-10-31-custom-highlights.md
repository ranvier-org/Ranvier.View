# T03 custom highlights (Views 0.1.33, native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that highlights search matches, diagnostics or collaborative cursors computes the match ranges in a memo
  (`createMemoWithComparer` over the query and the text), applies the registry change in an effect
  (`createEffectOnWithComparer`), and removes its names with `onCleanup` under the owner that registered them.
- **View-owned state:** `HighlightObject` and `HighlightRegistryObject`, the session's wrappers of the engine's
  `Highlight` and `HighlightRegistry` handles (Kinds 86 and 87), and the session object table entry `Session.Remember`
  adds for a range that enters a highlight, so `Ranges` returns the caller's range wrapper. Both are native handle
  state: the core has no representation of a native handle or of its wrapper identity.

## Design

- `Ranvier.View.WebCore/Highlights.fs`:
  - `Highlights.forHost` implements the Core `HighlightService` for the document the host shows at creation; a call
    after the host shows another document raises `InvalidOperationException`.
  - `Create` passes the ranges to `rv_dom_highlight_create`; the setlike and maplike operations call the generated
    `rv_dom_highlight_*` and `rv_dom_highlight_registry_*` functions; `Ranges` and `Entries` read the owned snapshot
    sequences and release them.
  - Highlights and the registry come back through the session object table, one wrapper per handle. A range whose
    wrapper was collected comes back as a new `Range`, or a `StaticRange` when the engine rejects a Range getter.
  - `HighlightsFromPoint` answers `HighlightHitTest.Unsupported`: the pinned WebKit has no `highlightsFromPoint`.
- `Ranvier.Dom` `BrowserHighlights.forDocument` calls the realm's `Highlight` constructor and `CSS.highlights`.

## Proof

`tests/NativeApi.Conformance/HighlightChecks.fs` runs 35 shared checks on native JIT, NativeAOT and Chromium: registry
and highlight iteration order, snapshots under mutation, priority and type, object identity, unchanged text, null
arguments, and `highlightsFromPoint` per engine. The native runner adds the lifetime checks: the wrong thread, document
replacement (a new empty registry, emptied registered highlights, a held highlight still usable), host disposal and no
live objects. Paint is checked by the Ranvier.WebCore probe `tests/abi/custom_highlights.py` against the Chromium
oracle.
