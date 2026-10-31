# G11 intersection observation (native provider)

## Core and view state

- **Core primitives used:** the I01 `CancellationService` signal: `IntersectionObservationService.Create` takes an
  optional `AbortSignal` and registers `OnAbort` to dispose the observer. A view that reacts to visibility reads the
  service under an `Owner`, writes each delivery into a signal or memo of the core, and registers the observer's
  `Dispose` with `onCleanup`; the core's batching and scheduling then decide what re-renders.
- **View-owned state:** none. The native provider keeps the engine observer handle of each observer and its callback
  delegate, which the ABI requires to stay reachable while the engine can call it; both are native resources with no
  core equivalent. Entries are copied values; the provider keeps no entry, rectangle or target cache between
  deliveries.

## Design

- Core `Ranvier.Dom/IntersectionObservation.fs`: `IntersectionObservationService` over an
  `IntersectionObservationProvider`. `IntersectionObserverInit` holds the root (`Implicit`, `Document` or
  `Element`), `RootMargin`, `ScrollMargin` and `Threshold`; `IntersectionObserverEntry` copies the target, time, root
  bounds, bounding client and intersection rectangles, `IsIntersecting` and the ratio. `TakeRecords` returns the
  queued entries, and `Now` is the clock of entry times. An invalid margin or threshold raises
  `IntersectionObserverException` with the engine's name (SyntaxError, RangeError). The service validates nulls,
  ignores deliveries after disposal and disposes its observers.
- Core `Fable.Ranvier.Dom/BrowserIntersectionObservation.fs`: the window's `IntersectionObserver` and
  `performance.now()`.
- `Ranvier.View.WebCore/IntersectionObservation.fs`: `IntersectionObservation.forHost` over
  `rv_host_observe_intersection_entries`, `rv_observer_observe`, `rv_observer_unobserve`, `rv_observer_disconnect`,
  `rv_observer_take_records`, `rv_host_now` and `rv_observer_release` (ABI feature "intersection-entries"; the Native
  and ABI numbers are assigned at batch integration). A root or target of another host raises ArgumentException. A
  callback exception is added to `WebCoreHost.Errors` and leaves other observers delivering. Host disposal releases
  every observer before `rv_host_destroy`.
- Engine differences, recorded in Ranvier.WebCore's `tests/fixtures/intersection-observation-qualified.json`: the
  order between observers; WebKit's root bounds include the scroll margin; with an explicit root WebKit clips by the
  scroll containers between target and root without the scroll margin.

## Proof

`tests/NativeApi.Conformance/IntersectionObservationChecks.fs` runs on native JIT and NativeAOT and in Chromium in a
320x240 viewport: implicit, document, element and detached roots; pixel, percentage and negative root margins; a
scroll margin on a nested scroller; threshold crossings with their rectangles and ratios; entry times inside their
rendering update, one time per update, and the observation clock agreeing with the H02 `TimingService.Now` of the same
host; takeRecords before and after an update, and inside the first delivering callback, where it takes the other
observer's entries; unobserve, re-observation and disconnect; target and root removal; a throwing callback reported
once while the next observer delivers; an entry held past later deliveries; abort signals; option errors; argument
validation and service disposal. Every step that changes layout runs one rendering update before the next assertion
(Chromium: two animation frames and a task), and assertions compare per-observer ordered logs and final values. The
native runner adds lifetime checks: wrong thread, a target and a root of another host, a replaced document's observer
and the kept time origin, an observer and service held past host disposal, an entry held past host disposal, and no
live native objects after disposal.
