# A09 custom animation effects (native provider)

## Core and view state

- **Core primitives used:** none of the reactive core's. A view that drives its own animation from a custom effect
  creates it with `CustomEffectService` on a document the view already owns, under that node's `Owner`. It disposes
  the registration with `onCleanup` when the owner ends. A tick that feeds view state writes a core signal, and the
  core's scheduling then re-renders.
- **View-owned state:** none. The native provider keeps one engine callback reference per registration
  (`RvCustomEffectCallback`, released when the registration is revoked) and a GC handle for its tick. Both are
  native resources with no core equivalent. Effects are session objects of the existing AnimationEffect kind;
  the generated `CustomEffectObject` class answers the runtime type `CustomEffect`. No new kind is used, so none of
  the placeholder kinds 930+ is taken.

## Design

- Core `Ranvier.Dom/CustomEffects.fs`: `CustomEffectService` with `CreateCustomEffect` (EffectTiming),
  `CreateCustomEffectFor` (a duration) and `Animate` (DocumentTimeline.animate with CustomAnimationOptions). These
  return a `CustomEffectRegistration`, or a `CustomAnimation` with the playing animation. Dispose revokes the tick,
  including queued ticks.
- Native `Ranvier.View.WebCore/CustomEffects.fs`: each registration owns a host `RvCustomEffectCallback`. Ticks
  arrive from `rv_run_loop_cycle` after the rendering update that computed them, and run as listeners: an exception
  joins `Session.ListenerErrors`. Disposing the service revokes every registration it made.
- Browser `Fable.Ranvier.Dom/BrowserCustomEffects.fs`: Chromium has no CustomEffect, so a facade provides the
  contract with a null-target KeyframeEffect and one requestAnimationFrame loop. Its capability provider reports
  Exported No, Implemented Yes, Enabled, Permitted and Available Unknown, with Mechanism "Ranvier facade emulation".
- `DocumentTimeline.animate` on the generated wrappers stays unmapped (NotSupported), as it is in Chromium. The
  service is the shared contract.

## Verification

- `CustomEffectChecks.fs` (23 assertions, shared by native and Chromium): seeked progress, animate id and play state,
  animation order in a frame, a DOM write from a tick, self-disposal inside a tick, exception routing, disposal,
  and the TypeError for invalid timing. Native lifetime adds 3: wrong thread, no tick after host disposal, and a
  disposed service.
- Qualified: a native DOM write in a tick renders one frame later. Tick counts are never asserted.
