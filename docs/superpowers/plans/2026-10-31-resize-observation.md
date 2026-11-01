# G10 resize observation (Views 0.1.41, native provider)

## Core and view state

- **Core primitives used:** the I01 `CancellationService` signal: `ResizeObservationService.Create` takes an optional
  `AbortSignal` and registers `OnAbort` to dispose the observer. A view that sizes itself from an element reads the
  service under an `Owner`, writes each delivery into a signal or memo of the core, and registers the observer's
  `Dispose` with `onCleanup`; the core's batching and scheduling then decide what re-renders.
- **View-owned state:** none. The native provider keeps the engine observer handle of each observer and its callback
  delegate, which the ABI requires to stay reachable while the engine can call it; both are native resources with no
  core equivalent. Entries are copied values; the provider keeps no entry, size or target cache between deliveries.

## Design

- Core `Ranvier.Dom/ResizeObservation.fs`: `ResizeObservationService` over a `ResizeObservationProvider`.
  `ResizeObserverBox` is `ContentBox`, `BorderBox` or `DevicePixelContentBox`; `ResizeObserverEntry` copies the target,
  `contentRect`, and the content, border and device-pixel box sizes in the writing mode. The service validates nulls,
  rejects unsupported boxes with NotSupportedException, ignores deliveries after disposal and disposes its observers.
- Core `Fable.Ranvier.Dom/BrowserResizeObservation.fs`: the window's `ResizeObserver`; device-pixel support follows
  `ResizeObserverEntry.prototype.devicePixelContentBoxSize`.
- `Ranvier.View.WebCore/ResizeObservation.fs`: `ResizeObservation.forHost` over `rv_host_observe_resize_entries`,
  `rv_observer_observe_box`, `rv_observer_unobserve`, `rv_observer_disconnect` and `rv_observer_release` (Native48,
  ABI56). The device-pixel content box is unsupported natively (webkit.org/b/219005, Ranvier.WebCore#146). A callback
  exception is added to `WebCoreHost.Errors` and leaves other observers delivering. Host disposal releases every
  observer before `rv_host_destroy`.

## Proof

`tests/NativeApi.Conformance/ResizeObservationChecks.fs` runs on native JIT and NativeAOT (35 assertions) and Chromium
(34): content and border boxes of bordered, padded, vertical, zoomed, fractional, border-box, inline and hidden
elements; the device-pixel box (Chromium reports it, native rejects it); a padding change reaching the border-box
observer alone; logical sizes across a writing-mode change; re-observation with the same and another box; delivery in
first-observe order, and the qualified construction-order difference; depth ordering of sizes changed in a callback;
unobserve, disconnect and re-observation; a detached, attached, removed and hidden target; a throwing callback
reported once while the next observer delivers; an entry held past later deliveries; abort signals; argument
validation and service disposal. Every step that changes layout runs one rendering update before the next assertion,
and assertions compare ordered delivery logs and final values. The native runner adds 11 lifetime checks: wrong
thread, an element of another host, a replaced document's observer, an observer and service held past host disposal,
an entry held past host disposal, and no live native objects after disposal.
