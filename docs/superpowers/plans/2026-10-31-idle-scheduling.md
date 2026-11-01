# H01 idle scheduling

Route: Facade (ranvier-org/Ranvier.WebCore#108). No native ABI change: the native host runs idle periods in its
managed loop, as it runs frames, timers and microtasks (A12). Managed: DOM alpha.32 (Core) and Views 0.1.22 /
backend alpha.22, stacked on the G05 managed heads (Core 135897c2, Views 4b5085f) and pinning Native preview.29.

## Core and view state

- **Core primitives used:**
  - `Owner`, `createRoot`, `runWithOwner`, `onCleanup`: `Ranvier.View.Dom.Frames.idle` gives each idle callback a
    child scope of the current owner, as `Frames.frame` does. Disposing the scope or any ancestor cancels a callback
    that has not started; the scope is disposed after the callback returns.
  - `batch`: an idle callback runs inside a batch, so its writes produce one effect run per effect.
  - `flush` and the graph's dispatcher: work that an idle callback defers stays on the graph's dispatcher; idle
    scheduling adds no commit queue.
- **View-owned state:**
  - The browser provider's `requestIdleCallback` ids, one per pending callback, held to cancel it and to cancel every
    pending callback on disposal (the A12 frame-id precedent).
  - The native host's idle queue in `WebCoreHost`, next to its timer and frame queues: pending idle callbacks in
    request order and their timeout timers. The host loop decides when an idle period starts and its deadline; the
    core has no host loop, so it cannot hold this queue.

## RED (measured 2026-10-31)

Receipt: `docs/research/native-api-delivery/idle-red-measurements.json`.

- Native: Native28 has no idle export (`dom-api.json`, the interop assembly and `RanvierWebCoreHost.dll` carry no
  idle symbol), `WebCoreHost` has no idle queue, and `Ranvier.Dom` has no idle member. WebKit's
  `IdleCallbackController` sits behind `RequestIdleCallbackEnabled` (status testable, default false) and is reachable
  only from script, which the native host does not run.
- Chromium 156: `requestIdleCallback`, `cancelIdleCallback` and `IdleDeadline` exist. `timeRemaining()` is at most
  49.9 ms and reaches 0 once the deadline passes. Callbacks run in request order; cancellation is idempotent and
  ignores unknown ids. A callback requested inside an idle callback runs in a later idle period. With
  `{ timeout = 20 }` on a busy thread the callback runs with `didTimeout = true` and `timeRemaining() = 0`. A pending
  animation frame runs before the idle callback.

## Contract

- `Ranvier.Dom.IdleDeadline`: `TimeRemaining: unit -> float` (milliseconds, from 0 to 50) and `DidTimeout: bool`.
- `Ranvier.Dom.IdleScheduler`, a separate `IDisposable` interface; `FrameScheduler` stays as A12 shipped it.
  `Idle: timeout: float option * callback: (IdleDeadline -> unit) -> IDisposable`. Callbacks run in request order;
  a callback requested inside an idle callback runs in a later idle period. With a timeout, the callback runs no
  later than the timeout elapsing, with `DidTimeout = true` and `TimeRemaining () = 0`. Cancellation is idempotent.
  Disposing the scheduler cancels pending idle callbacks and rejects later requests with `ObjectDisposedException`.
  Providers implement `FrameScheduler` and `IdleScheduler` on one object where the host has both; code that needs
  both asks for both.
- Capability: `PlatformFacility` key `idle-callbacks`, observed by each provider.
- Providers:
  - `BrowserIdle.forDocument`: `Some` scheduler over Window `requestIdleCallback`/`cancelIdleCallback`, `None`
    where the Window lacks them (WebKit with the preference off). `BrowserIdle.capabilities` reports `idle-callbacks`
    with `Implemented` and `Available` set to `No` there. No timer emulation.
  - `Ranvier.View.WebCore.Frames.idleForHost`, next to A12's `Frames.forHost`, over the host's run loop
    (`WebCoreHost.RequestIdleCallback` / `CancelIdleCallback`):
    `WebCoreHost.Cycle` starts an idle period after it runs posted work, due timers and microtasks, when no frame is
    requested. Deadline source: the host timing clock. Deadline = min(start + 50 ms, the earliest pending timer's due
    time), the 50 ms cap matching Chromium's measured 49.9 ms maximum. A period runs the callbacks requested before
    it started, in request order, until `TimeRemaining` reaches 0; the rest wait for the next period. Timeouts are host
    timers that run the callback with `DidTimeout = true`.
  - `ManualFrameScheduler` implements `IdleScheduler`: `RunIdle budget` runs the callbacks requested before the call
    with deadline min(now + budget, now + 50, the next timer's due time); `Advance` runs expired idle timeouts in
    deadline order with the timers.
- `Ranvier.View.Dom.Frames.idle (scheduler: IdleScheduler) timeout callback`: owner-bound, as `frame`.

## Verification

`tests/NativeApi.Conformance/IdleChecks.fs` asserts the Chromium-measured behaviour on the manual, native and browser
schedulers, on native JIT, NativeAOT and Chromium: request order, a nested request running in a later period, a
pending frame running first, a timeout giving `DidTimeout = true` with 0 remaining, idempotent cancellation, the
50 ms deadline bound, owner disposal and scheduler disposal. Native timing differences from Chromium are recorded as
qualifications in the evidence. Native lifetime cases: host disposal with pending idle callbacks and timeouts.

The full default runs carry 85 native JIT, 85 NativeAOT and 67 Chromium PASS lines on DOM alpha.32, Views 0.1.22,
backend alpha.22 and Native preview.29, each a superset of G05's 83/83/66
(`docs/research/native-api-delivery/idle-runtime-evidence.json`).

## Decisions (root, 2026-10-31)

- `IdleScheduler` is separate from `FrameScheduler`.
- A Window without `requestIdleCallback` reports an unsupported capability; a timer cannot honour idle periods or
  deadlines.
