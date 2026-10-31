# A12 native frame and timer ownership

Route: Facade (ranvier-org/Ranvier.WebCore#71). No native ABI change: the native host schedules microtasks, timers
and animation frames in managed code. Managed: DOM alpha.30 (Core) and Views 0.1.20 / backend alpha.20, pinning
Native28; stacked on the G02/G12 heads (Core 780b9f0f, Views bd4ce10).

## Core and view state

- **Core primitives used:**
  - `Owner`, `createRoot`, `runWithOwner`, `onCleanup`: each scheduled callback gets a child scope of the current
    owner. The scope's cleanup cancels the host callback, so disposing the scope or any ancestor cancels a callback
    that has not started. The callback runs under the scope, which is disposed after it returns and unlinks itself
    from its parent.
  - `batch`: a callback runs inside a batch, so its writes produce one effect run per effect.
  - `TimedClock` / `TimedTimer`: `Frames.clock` presents the scheduler's clock and timeouts to timed reactive values,
    so timed values and frame callbacks read one timeline.
- **View-owned state:** the host callback handles: a browser `requestAnimationFrame`/`setTimeout` id or a native host
  id, one per pending callback, held by its provider to cancel it and to cancel every pending callback when the
  provider is disposed. The core holds no host scheduling primitive, so it cannot hold these ids. Ownership,
  cancellation on disposal and the clock stay in the core; the view keeps no timer registry or ownership tree.

## Contract

- `Ranvier.Dom.FrameScheduler`: `Now`, `Frame`, `Timeout`, `Microtask`, each returning an idempotent cancellation;
  disposal cancels pending callbacks and rejects later scheduling. A callback scheduled from a frame callback runs at
  the next frame.
- Providers: `BrowserFrames.forDocument` (Window rAF, timers, `queueMicrotask`, `performance.now()`),
  `Ranvier.View.WebCore.Frames.forHost` (host queues and timing clock), and `ManualFrameScheduler` (clock and frames
  advance only on `Advance` and `RunFrame`).
- `Ranvier.View.Dom.Frames`: `frame`, `timeout`, `microtask` (owner-bound) and `clock`.

## Verification

`tests/NativeApi.Conformance/FrameChecks.fs` runs identically on the manual scheduler and on the host scheduler, on
native JIT, NativeAOT and Chromium: microtask checkpoint, timer delay and cancellation, one-shot and rescheduled
frames with frame times on the scheduler clock, owner-bound callbacks in a disposed child scope, owner and handle
cancellation, the timed clock (re-arm, disarm, dispose) and scheduler disposal.

Frame times are positive, at most the scheduler clock read inside the callback, and nondecreasing across frames.
The full default runs carry 81 native JIT, 81 NativeAOT and 65 Chromium PASS lines on DOM alpha.30, Views 0.1.20,
backend alpha.20 and Native preview.28 (`docs/research/native-api-delivery/frame-ownership-runtime-evidence.json`).
