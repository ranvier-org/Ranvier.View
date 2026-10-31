# G01/G15 media queries (native provider and owned queries)

## Core and view state

- **Core primitives used:** `createSignal`, `batch` and `onCleanup` in `Ranvier.View.Dom.MediaQueries.matches`: the
  signal holds the query's matches, each change writes it in a batch, and the current `Owner`'s cleanup removes the
  subscription and releases the query.
- **View-owned state:** none. The native provider keeps no state: each call marshals `RvMediaPreferences` on the
  stack of the call, and the query lists are the session's event targets.

## Design

- `Ranvier.View.WebCore/MediaQueries.fs`: `provider` and `forHost` over `Viewport.provider`'s `matchMedia` and
  `rv_host_set_media_preferences` / `rv_host_media_preferences` (ABI placeholder, root renumbers at integration) when
  the native library has `media-preferences`. `unavailable` lists forced-colors, prefers-contrast less and custom,
  reduced transparency and reduced data.
- `Ranvier.View.Dom/MediaQueries.fs`: `matches service query` returns a signal read owned by the current scope.

## Proof

`tests/NativeApi.Conformance/MediaQueryChecks.fs` runs on native JIT, NativeAOT and Chromium (CDP device metrics,
emulated media features and touch emulation): matches for every step, one change per changed list with its new
matches, a subscription removed before a change, an owner-scoped query that stops after disposal, query and service
disposal, and natively the preference readback, validation and both-pointer devices. The native runner adds a
wrong-thread supply, a replaced document and a service held past host disposal.
