# H08 inspector diagnostics (Views 0.1.35, native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that shows diagnostics reads them under an `Owner` and registers the service's `Dispose` with `onCleanup`.
- **View-owned state:** none. Each read takes a native `RvDiagnostic` snapshot, copies its values into managed
  records and releases it before returning, so no native handle outlives a call. The opt-in is the host's native
  flag (`rv_host_set_diagnostics`), not view state.

## Design

- Core `Ranvier.Dom/Diagnostics.fs`: `DiagnosticsService` over a `DiagnosticsProvider`. The service starts disabled;
  every read returns `Disabled` until `Enable`, and `Dispose` disables it. Option validation (unknown flags, layer
  flags with an element) and null checks happen in the service, before the provider runs.
- Core `Fable.Ranvier.Dom/BrowserDiagnostics.fs`: the box model from `getBoundingClientRect` and the computed border,
  padding and margin widths (zoom applied). A transformed element or ancestor reads `Unsupported`. The render tree,
  matched rules and developer-tools frontend are `Unsupported`: browsers expose none of them to pages.
- `Ranvier.View.WebCore/Diagnostics.fs`: `Diagnostics.forHost` maps the service onto `rv_host_set_diagnostics`,
  `rv_host_inspect_render_tree`, `rv_host_inspect_matched_rules` and `rv_host_inspect_box_model`. The engine's
  `RV_ERR_UNSUPPORTED` for an element maps to `NotRendered`. The developer-tools frontend follows the host's
  `inspector-frontend` capability fact, which is 0 on every flavor.

## Proof

`tests/NativeApi.Conformance/DiagnosticsChecks.fs` runs on native JIT, NativeAOT and Chromium: features, the opt-in,
argument validation, the box model of a fractional, bordered and padded box against values derived from its CSS
(within 1/64 px), the box of a child before and after its scroller scrolls, `NotRendered` for display:none and inline
boxes, and disposal. On the native engine it also checks the render-tree invariants and the matched rules in cascade
order (@layer, @media, `:is()`, `:where()`, inline style). The native runner adds the lifetime checks: wrong thread,
a replaced document, host disposal and that every snapshot is released.
