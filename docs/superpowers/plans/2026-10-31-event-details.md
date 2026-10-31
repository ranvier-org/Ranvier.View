# I08 event detail adapters

Route: Engine (ranvier-org/Ranvier.WebCore#105). Native producer: Native39/ABI47, stacked on G04, G03, B22 and T06
over D14 (Native34/ABI42). Managed: DOM alpha.42 (Core) and Views 0.1.32 / backend alpha.32.

## Core and view state

This group touches the shared DOM contract and the WebCore host adapter, not the view renderer.

- **Core primitives used:** none. An event detail record is DOM state the engine creates or dispatches; the service
  constructs it and keeps no reactive graph of its own.
- **View-owned state:** none beyond the native adapter: the service's disposed flag, and the dispatch from the native
  Event type tag to a wrapper class in `Factories.event`. The type tag is the engine's, read through
  `rv_dom_event_get_interface_name`, and the wrapper choice has no core equivalent.

## Contract (Ranvier.Dom)

- Generated interfaces (from Ranvier.WebCore `gen/Ranvier.Dom.fs`): `ErrorEvent` (message, filename, lineno, colno),
  `ProgressEvent`, `AnimationEvent` and `TransitionEvent` (name, elapsedTime, pseudoElement),
  `ContentVisibilityAutoStateChangeEvent`, `AnimationPlaybackEvent` (currentTime, timelineTime as
  `DoubleOrCSSNumericValue option`), `PageTransitionEvent`, `HashChangeEvent`, `MediaQueryListEvent`, `PopStateEvent`
  (hasUAVisualTransition), `BeforeUnloadEvent.returnValue`.
- `EventDetailService` (hand-written, `EventDetails.fs`): one `Create*` per constructible interface from `EventOptions`
  and per-interface fields. Strings reject null; IDL doubles reject NaN and infinities with ArgumentException.
- Browser provider: `BrowserEventDetails.forDocument` (Fable.Ranvier.Dom) over the realm's constructors. Native
  provider: `Ranvier.View.WebCore.EventDetails.forHost` over `rv_dom_*_event_create`.
- Existing coverage reused: I02 `EventConstruction` (Event, CustomEvent, UI, Mouse, Keyboard, Focus), D13/D14
  `SubmitEvent`/`FormDataEvent`, D16 `ToggleEventService`.

## Native surface (ABI47, additive)

- Generated `dom:<Interface>` and `dom-constructor:<Interface>` features, and `dom-type-query:Event`:
  `rv_dom_event_get_interface_name` returns the exposed name (AnimationEvent, not CSSAnimationEvent), CustomEvent
  included; an unscoped event reports its nearest generated base (DragEvent reads MouseEvent, TextEvent UIEvent).
- ErrorEvent and PopStateEvent constructors use the Document's global object and fail with InvalidStateError on a
  document without a browsing context.

## Views

- `Factories.event` calls the generated `Classes.Event`, which switches on the type query; the trial-getter chain is
  gone. The wrapper generator maps a query's unscoped extras to the root wrapper.
- `tests/NativeApi.Conformance/EventDetailChecks.fs`: constructed defaults and full inits for eleven interfaces, a
  constructed record through a listener, validation, and engine payloads (0 s `::before` animation, 50 ms
  transition). Native adds type, thread and teardown checks.

## Deferred

MessageEvent and PromiseRejectionEvent (script values), StorageEvent (Storage), ClipboardEvent and DragEvent
(DataTransfer), CloseEvent (Modules/), TextEvent, SecurityPolicyViolationEvent, and the `any` fields ErrorEvent.error
and PopStateEvent.state, each with its owning group in Ranvier.WebCore `tests/fixtures/event-details-cases.json`.
