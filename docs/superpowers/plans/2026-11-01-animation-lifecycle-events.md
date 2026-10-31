# A03 animation lifecycle and A10 typed animation events (native provider)

## Core and view state

- **Core primitives used:** none of the reactive core's. A view that waits for an animation calls
  `AnimationService.Ready` or `Finished` under the owner of the node it animates and registers the returned
  operation's `Dispose` with `onCleanup`, so an operation ends with its owner. A view that reacts to the
  outcome writes it into a core signal, and the core's scheduling then re-renders. Event subscriptions use the
  existing generic `addEventListener` on the animation or the document.
- **View-owned state:** none. The native provider holds the token of each pending native operation and a
  process-wide table from completion context to callback. Both are native resources with no core equivalent. It
  adds no kinds, because the animation handles are A01's.

## Design

- Core `Ranvier.Dom/Resources.fs` adds `ResourceFailure.Aborted of name`: the engine rejected the operation with the
  named DOMException. Every match on `ResourceFailure` in Core and Views handles the case explicitly.
- Core `Ranvier.Dom/Animations.fs`: `AnimationService.Ready` and `Finished` take an animation and a terminal callback,
  and return a `ResourceOperation<WebAnimation>`. They settle with the animation when the promise current at the call
  resolves. Otherwise they settle `Aborted "AbortError"` when the engine rejects the promise, `Cancelled` on
  cancel, dispose or service disposal, or `Invalidated` when the document is replaced or destroyed. An animation of
  another document raises `ArgumentException`.
- Core `Fable.Ranvier.Dom/BrowserAnimations.fs`: the browser implementation observes `animation.ready` and
  `animation.finished`. The realm check is `instanceof` the document's `Animation`.
- `Ranvier.View.WebCore/Animations.fs`: `rv_animation_ready` and `rv_animation_finished` are resource operations, and
  their completions arrive only from `rv_run_loop_cycle`. Outcome 5 (`RV_RESOURCE_ABORTED`) maps to `Aborted`, and
  `RV_ERR_WRONG_HOST` maps to `ArgumentException`. `Resources.fs` and `BlobUrls.fs` map outcome 5 explicitly.
- The A03 state members (play, pause, finish, cancel, reverse, updatePlaybackRate, persist, playState, pending,
  replaceState, startTime) and the A10 event getters (AnimationEvent, TransitionEvent, AnimationPlaybackEvent) are the
  generated shared interfaces. Their native routes come from A01's generation and I08's event objects.

## Proof

- `tests/NativeApi.Conformance/AnimationLifecycleChecks.fs` (29 assertions) runs on native JIT, NativeAOT and
  Chromium. It covers:
  - the state machine;
  - ready and finished settlement, and finish/cancel/remove playback events, per animation;
  - a cancel inside a finish listener, and its order after the dispatching finish;
  - a cancel inside a ready callback;
  - disposal before completion, and service disposal.
- The native runner adds 4 lifetime assertions: another thread, another host's animation, host disposal
  (`Invalidated`), and nothing delivered after that.
- `tests/NativeApi.Conformance/AnimationEventChecks.fs` (9 assertions) captures typed CSS animation and transition
  events at the document. It checks names, elapsed times, `::before` and `::after`, cancel events, the playback event
  times of `Element.animate`, and capture and bubble listeners with their removal.
- The values follow the Chromium 156 oracles of Ranvier.WebCore `tests/fixtures/animation-lifecycle-oracle.json` and
  `tests/fixtures/animation-events-oracle.json`.

## Qualifications

- Promise settlements and engine events are compared as separate orders. Native completions arrive on the next run
  loop cycle, while events dispatch inside the rendering update.
- Under the native host, the engine's JavaScript-visible AbortError rejection never runs, because microtasks are
  refused under DisallowVMEntry. Native patch 0051 reports the rejection when it is scheduled, and that native
  outcome is the contract.
- Cancel events carry a frame-dependent elapsedTime, so the fixture checks only that it is present.
