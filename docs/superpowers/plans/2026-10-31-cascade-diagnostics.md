# C25 cascade diagnostics (Views 0.1.39, native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that shows a cascade reads it under an `Owner` and registers the service's `Dispose` with `onCleanup`, as for the
  H08 reads.
- **View-owned state:** none. Each cascade read takes a native `RvDiagnostic` snapshot, copies its entries into
  `CascadeDeclaration` records and releases it before returning. The opt-in is the host's native flag
  (`rv_host_set_diagnostics`), shared with the H08 reads.

## Design

- Core `Ranvier.Dom/Diagnostics.fs`: `DiagnosticsService.Cascade (element, property)` returns a `PropertyCascade`:
  the declarations of a longhand or custom property in cascade input order, each with origin (`PresentationalHint`
  added), selector, specificity, importance, layer, conditions, position, shorthand, `var()` flag, depth and the
  engine's winner. `PropertyCascade.Winner` is the winner of the greatest depth. `DiagnosticsFeatures.Cascade` reports
  availability; an unknown or shorthand property reads `UnsupportedProperty`.
- Core `Fable.Ranvier.Dom/BrowserDiagnostics.fs`: `Cascade` is `Unsupported`. Browsers expose no cascade provenance;
  the provider does not derive one from `getComputedStyle`.
- `Ranvier.View.WebCore/Diagnostics.fs`: `Cascade` maps onto `rv_host_inspect_cascade` (ABI54) when the native library
  has `cascade-diagnostics`. An `RV_ERR_UNSUPPORTED` for an element the engine reads for `display` maps to
  `UnsupportedProperty`; otherwise to `NotRendered`.

## Declared limits

A winning `revert` or `revert-layer` is reported as that keyword; `:visited` declarations, `@starting-style` rules and
animations are absent; `Value` is the declared value, and `var()` resolution is the computed value's.

## Proof

`tests/NativeApi.Conformance/CascadeChecks.fs` runs on native JIT, NativeAOT and Chromium. On every engine it checks the
computed value of 22 cases (specificity, source order, `:where()`, importance across origins and layers, named,
anonymous and nested layers, inline style, user-agent origin, a presentational hint, a shorthand longhand, a logical
pair under vertical writing mode, registered and unregistered custom properties, inherit and unset chains, `var()`,
`@media` and `@supports`), the opt-in and argument validation. On the native engine it checks every declaration of each
case and its winner; in Chromium every cascade read is `Unsupported`. The native runner adds wrong-thread, replaced
document, host disposal and snapshot release checks.
