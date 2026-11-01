# G03 visual viewport (Views 0.1.30, native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that shows visual viewport values subscribes under an `Owner` and registers the subscription's `Dispose` with
  `onCleanup`.
- **View-owned state:** `VisualViewportObject`, the session's wrapper of the engine's `VisualViewport` event target
  (`Kind.VisualViewport`, released with `rv_event_target_release`). The wrapper is a native handle: the core has no
  representation of one.

## Design

- `Ranvier.View.WebCore/VisualViewport.fs`:
  - `VisualViewport.provider` reads `Window.visualViewport` through `rv_dom_window_get_visual_viewport` and wraps
    it in the session's object table, anchored to the document.
  - Reads use the seven `rv_dom_visual_viewport_*` getters. `RV_ERR_DETACHED`, or a handle cleared by host
    disposal, reads `Detached`.
  - `SetPageScale` and `SetObscuredInsets` call `rv_host_set_page_scale` and `rv_host_set_obscured_insets`. The
    engine's `RV_ERR_UNSUPPORTED` maps to `Unsupported`.
  - `VisualViewport.forHost` returns the Core `VisualViewports.create` service over that provider.
- The wrapper generator treats an interface with its own release kind as a root class. `VisualViewportObject`
  therefore inherits `NativeObject` with `Kind.VisualViewport` (Kind 74) and implements `EventTarget` through
  `Events`. Without that rule it would inherit `EventTargetObject`, a node kind.

## Proof

`tests/NativeApi.Conformance/VisualViewportChecks.fs` replays the Ranvier.WebCore oracle steps on native JIT,
NativeAOT and Chromium (66 shared checks): resizes, device scales, page scales 2 and 1, a window scroll and a wheel pan
at scale 2, argument validation, obscured insets, subscription removal and disposal.

- Native drives the page scale and the insets through the service. Chromium drives the page scale with
  `Emulation.setPageScaleFactor` and reports both inputs `Unsupported`.
- The WebKit-qualified steps expect offsets equal to the page position while page-scaled, and a visual viewport
  `scroll` for a window scroll at scale 2.
- The native runner adds the lifetime checks: wrong thread, a page replacement that keeps the window's visual
  viewport, `Detached` after host disposal, and no live objects.
