# I02 standalone and custom event construction

Route: Qualified (ranvier-org/Ranvier.WebCore#100). Native producer: Native24/ABI32 (ranvier-org/Ranvier.WebCore#136).

## Core and view state

This group touches the shared DOM contract and the WebCore host adapter, not the view renderer.

- **Core primitives used:** none. Events and event targets are DOM objects.
- **View-owned state:** native standalone EventTarget handles and their listener registrations. They are DOM state of
  native objects with no core equivalent. CustomEvent.detail stays on the engine's CustomEvent; the producer keeps it
  reachable, and the view keeps no detail table.

## Contract

`Ranvier.Dom.EventService` constructs untrusted events from typed records (`EventConstruction`: Event, Custom, UI,
Mouse, Keyboard, Focus), reads a CustomEvent detail as the H02 `TimingDetail` tree, and creates standalone targets.
Custom details contain finite numbers only, so the native JSON transport and the browser carry identical trees.
Dispatch uses `EventTarget.dispatchEvent`; listener signals reuse I01 `CancellationService.Listen`.

Measured provider qualifications:
- Both engines expose a constructed MouseEvent clientX/clientY as integers.
- Chromium 156 reports button 0 for a constructed mouseover; mousedown and click keep the init button.
- At the target, WebKit runs capture listeners before non-capture listeners (current DOM); Chromium 156 runs
  registration order. The shared fixture registers capture first so both agree; the native probe asserts WebKit's order.

## Verification

`tests/NativeApi.Conformance/CustomEvents.fs` runs on native JIT, NativeAOT and Chromium: base options, detail copy and
rejection, typed init fields, relatedTarget identity, standalone dispatch order with preventDefault and
stopImmediatePropagation, target/currentTarget/eventPhase after dispatch, capture/target/bubble propagation,
stopPropagation, once, removal and I01 signals. Native adds wrong-thread, collection, dispatch and host-disposal checks.
