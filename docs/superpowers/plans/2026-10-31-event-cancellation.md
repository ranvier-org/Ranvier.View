# I01 event cancellation signals

Route: Facade (ranvier-org/Ranvier.WebCore#99). No native ABI or canonical model change.

## Core and view state

This group touches the shared DOM contract and the WebCore host adapter, not the view renderer.

- **Core primitives used:** none. Signals are DOM objects, not reactive graph state.
- **View-owned state:** the native listener registrations that a signal removes, and the managed signal's
  abort-callback list on native hosts. Both are event-target state of native objects with no core equivalent.

Possible follow-up, not in this group: abort a view subscription's signal when its owner is cleaned up, through
the core's `onCleanup` / `Owner`.

## Contract

`Ranvier.Dom.CancellationService` creates controllers, already-aborted signals (`Abort`), timeouts and `Any`
composites, adds listeners with a signal (`Listen`) and maps signals to and from `CancellationToken`.

- A signal aborts at most once. Its reason is `Abort` (AbortError), `Timeout` (TimeoutError) or `Custom` of the
  H02 portable `TimingDetail`, copied on abort; cycles are rejected with DataCloneError.
- Callbacks run once, after internal cleanup, in registration order; a composite's callbacks run after its
  source's. A signal that has already aborted never runs a new callback.
- `Listen` removes the registration matching type, listener and capture when the signal aborts. This matches
  the measured Chromium behaviour, where a listener removed and re-added before the abort is also removed.
- A token cancellation aborts its signal on a later host turn, never synchronously.

## Providers

- Native (`Ranvier.View.WebCore.Cancellation.forHost`): `Ranvier.Dom.Cancellation.create` over the host's
  `SetTimeout`/`ClearTimeout`, `PostToOwner`/`Cycle`, error list and native listener removal. Owner thread only;
  host disposal disposes the domain and clears its timers.
- Browser (`Ranvier.Dom.BrowserCancellation.forDocument`): the document realm's real `AbortController` and
  `AbortSignal` statics and the real `signal` listener option. A static the realm lacks raises
  NotSupportedException and its capability fact stays Unknown; it is never emulated.
- Capability providers report `abort-signal-abort|timeout|any` platform facilities: Implemented on native (the
  managed facade), Exported when the browser realm defines the static.

## Verification

`tests/NativeApi.Conformance/Cancellation.fs` runs on native JIT, NativeAOT and Chromium. Native time advances
only through `Cycle`; no check waits on wall-clock time. Native adds thread, error-report, timer and teardown
checks; the browser adds raw reason identity, capability and windowless-document checks.
