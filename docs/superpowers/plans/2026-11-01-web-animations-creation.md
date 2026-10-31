# A01 Web Animations creation (native provider)

## Core and view state

- **Core primitives used:** none of the reactive core's. A view that animates an element calls `Element.animate`
  or the `AnimationService` constructors on a node the view already owns, under that node's `Owner`. When an
  animation should end with its owner, the view registers `cancel` with `onCleanup`. Values a view reads from an
  animation (`currentTime`, `playState`) go into a core signal or memo, which the core's scheduling then
  re-renders.
- **View-owned state:** none. The native provider holds the engine handles of the animations, effects and
  timelines it returns, as native objects of the session (kinds WebAnimation, AnimationEffect and AnimationTimeline,
  with placeholder numbers that root assigns at integration). Those handles are native resources with no core
  equivalent. Keyframes and options are copied into the call and are not cached.

## Design

- Core `Ranvier.Dom/Animations.fs`: `AnimationService` with `CreateKeyframeEffect` and `CreateKeyframeEffectFor`
  (options or a duration), `CopyKeyframeEffect`, `CreateAnimation` (by default on the document timeline),
  `CreateAnimationWithoutTimeline` and `CreateDocumentTimeline`. Engine errors raise `AnimationException` with the
  WebIDL name. `Element.animate`, `getAnimations`, `Document.timeline` and the members of the created objects are
  the generated shared interfaces (`Keyframe`, `KeyframeEffectOptions`, `KeyframeAnimationOptions`, `WebAnimation`,
  `KeyframeEffect`, `DocumentTimeline`).
- Core `Fable.Ranvier.Dom/BrowserAnimations.fs`: the realm's `KeyframeEffect`, `Animation` and `DocumentTimeline`
  constructors. A `Keyframe` sequence becomes the browser's keyframe objects; the generated `Element.animate`
  adapter does the same in the browser.
- `Ranvier.View.WebCore/Animations.fs`: `Animations.forHost` over `rv_dom_keyframe_effect_create_3` and `_create_1`,
  `rv_dom_web_animation_create` (`timeline_present` distinguishes no timeline from the document timeline) and
  `rv_dom_document_timeline_create`.
- The generator (`tools/Ranvier.View.WebCore.Generate`) handles:
  - sequences of dictionaries, as arrays of pointers;
  - optional nullable handle fields (`Rv<Root>* const*`);
  - nullable string fields, where a null string is absent;
  - retains of event-target objects.

## Proof

`tests/NativeApi.Conformance/AnimationChecks.fs` runs the same 110 assertions on native JIT and NativeAOT and in
Chromium. Each animation is paused and seeked to its start, midpoint and end, and the fixture compares the play state,
the current time and the computed opacity or transform. The cases cover fill, delay, reverse and alternate
direction, composite add, keyframe composite, offsets and easing, number options, the timeline option, the
constructors and the copy constructor, and an animation without an effect or without a timeline. They also cover a
new DocumentTimeline and the TypeError and SyntaxError cases.

The native run adds 3 lifetime checks: the owning thread, an animation held across host disposal, and construction
after disposal. Native ABI and pixel evidence is in Ranvier.WebCore `docs/web-animations-evidence/`.

## Qualifications

- Animation identity: each `Element.animate` result and each `getAnimations` item is a new wrapper of the same engine
  handle. A05 adds identity, with CSSAnimation and CSSTransition.
- An omitted timeline and `Some null` are both the document timeline (the user's option rule).
  `CreateAnimationWithoutTimeline` is the explicit no-timeline constructor.
- Keyframes use the keyframe-sequence form.
