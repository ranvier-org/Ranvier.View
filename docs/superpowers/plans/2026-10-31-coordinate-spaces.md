# G14 coordinate-space conversion (Views 0.1.38, native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that converts coordinates, such as an overlay placed over a transformed element, reads the service under an `Owner`,
  re-reads after the layout or scroll change it reacts to, and registers the service's `Dispose` with `onCleanup`.
- **View-owned state:** none. Each conversion reads the engine's current layout, scroll, page scale and device scale
  through `rv_node_convert_points` or `rv_node_box_quad`; the provider keeps no geometry, handle or cache between
  calls.

## Design

- Core `Ranvier.Dom/Coordinates.fs`: `CoordinateService` over a `CoordinateProvider`. Spaces are
  `Element element` (the element's border box before its own transform), `Page`, `Client`, `Host` (the visible
  surface: client times the page scale, less the visual viewport's offset) and `Device` (host times the device pixel
  ratio). Results are `Detached`, `WrongDocument`, `NotRendered`, `NonInvertible` or `Unsupported`. The service
  validates nulls and non-finite coordinates before the provider runs.
- Core `Fable.Ranvier.Dom/BrowserCoordinates.fs`: an element space is the affine product of the 2D parts of the computed
  transforms of the element and its ancestors, placed by `getBoundingClientRect`. Perspective and preserve-3d chains,
  the individual transform properties and CSS zoom answer `Unsupported`. `Page` adds the window's scroll, `Host`
  follows `visualViewport`, `Device` multiplies by `devicePixelRatio`. An element of another document, such as an
  iframe's, answers `WrongDocument`.
- `Ranvier.View.WebCore/Coordinates.fs`: `Coordinates.forHost` anchors `Page`, `Client`, `Host` and `Device` at the
  host's current document. Native `RV_ERR_DETACHED` maps to `Detached`, `RV_ERR_WRONG_HOST` to `WrongDocument`, and
  `RV_ERR_UNSUPPORTED` to `NotRendered` when an element space has no CSS box, else `NonInvertible`. An element of a
  disposed host reads `Detached`.

## Proof

`tests/NativeApi.Conformance/CoordinateChecks.fs` runs on native JIT, NativeAOT and Chromium (304 assertions each):
element-to-page maps of a rotated and scaled box, its rotated child, a rotated child of a scrolled scroller and a
flattened rotateY/rotateX box against maps composed from the fixture's CSS (within 1/64 px); round trips through every
ordered pair of the five spaces for six elements; client corners against `getBoundingClientRect`; `Host` equal to
`Client` at page scale 1 and `Device` equal to `Host` times the ratio; the four box quads of a bordered, padded box with
margins; `NonInvertible` into scale(0); `NotRendered` for display:none and an inline box; a perspective chain (native
converts, Chromium `Unsupported`); `WrongDocument` for another host's or an iframe's element; `Detached` for a removed
element; and argument validation and disposal. The native runner adds the lifetime checks: page scale 2 at origin
40, 60 doubling `Client` into `Host` and halving it back, `Device` at device scale 2, wrong thread, an element of a
disposed host and of a replaced document, and a service held past host disposal.
