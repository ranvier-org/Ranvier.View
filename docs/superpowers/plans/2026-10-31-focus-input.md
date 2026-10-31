# I12 native focus, activation and input capability state

Route: Qualified (ranvier-org/Ranvier.WebCore#107). Native producer: Native26/ABI34, stacked on Native25/ABI33
(ranvier-org/Ranvier.WebCore#137). Managed: DOM alpha.27 (Core, stacked on #277) and Views 0.1.17 (stacked on #33).

## Core and view state

This group touches the shared DOM contract and the WebCore host adapter, not the view renderer.

- **Core primitives used:** none. Activation, modality and pointer capture are DOM state read from DOM events;
  the service publishes them to subscribers and keeps no reactive graph of its own.
- **View-owned state:** the native window EventTarget handle and the service's listener registrations on it. They
  are DOM state of a native object with no core equivalent. The last observed modality and focus value are DOM
  state of the document the service observes.

## Measured behaviour (Native25 candidate-3 and Chromium 156)

- Window activation: `rv_host_set_focused` changes `document.hasFocus()`, but no window event is observable
  natively, because the ABI has no window EventTarget. A host starts unfocused; Chromium starts focused.
  Chromium deactivates only in new headless (`channel: 'chromium'`, `bringToFront` on a second page); the
  headless shell keeps `hasFocus()` true.
- Keyboard modality: WebKit `:focus-visible` follows `Document::m_latestFocusTrigger` (a click clears it, a
  non-modifier keydown restores it). Both engines: click on a `tabindex` element gives focus without
  `:focus-visible`; Tab gives `:focus-visible`; a script focus after keyboard input is focus-visible. A WebKit
  button is not mouse-focusable, so the fixture uses `tabindex` elements.
- Touch capacity: WebKit returns `navigator.maxTouchPoints` 0 outside iOS touch builds; it has no touch delivery
  path (I03). Chromium returns 0, or 1 in a `hasTouch` context. The native ABI has no Navigator.
- Pointer capture: both engines fire `gotpointercapture` at the next pointer event after `setPointerCapture(1)`
  during a press, and `lostpointercapture` on release, with pointerId 1 and pointerType "mouse". An unknown id
  raises NotFoundError (native: RV_ERR_DOM_EXCEPTION); an inactive pointer is a no-op.

## Native producer (ABI34, feature "input-state")

- `RvEventTarget* rv_host_window_event_target(RvHost*)`: a new reference to the current document's window, for the
  `rv_event_target_*` functions, receiving every window event type; NULL without a window. The host keeps one window
  for its lifetime (measured: `rv_host_load_html` and `rv_host_load_svg` keep the window and its listeners). A
  reference and its listeners stay valid after `rv_host_destroy`, and `rv_listener_remove` on them returns RV_OK.
- `rv_host_set_focused` focuses the main frame before the first activation; before the fix the first activation
  fired two window focus events (`FocusController::setFocusedFrame` and `setActivityState`).
- `int32_t rv_host_max_touch_points(RvHost*, int32_t* value)`: `Navigator.maxTouchPoints` of the current window.

## Contract (Ranvier.Dom)

`InputStateService` over a window target and a document:
- `Current ()`: `{ Focused; Modality; FocusVisible; MaxTouchPoints }`, read live (`document.hasFocus()`, the active
  element matching `:focus-visible`, the provider's touch capacity).
- `Captured pointerId`: the element holding capture after the last `gotpointercapture`, cleared on
  `lostpointercapture`.
- `Subscribe`: `InputChange` values `Activation`, `Modality`, `CaptureGained`, `CaptureLost`, each delivered once per
  change; disposal of the subscription or service is idempotent and stops delivery.
- Modality starts `Unknown`; trusted `keydown` (not modifier-only, as WebKit) sets `Keyboard`; trusted
  `pointerdown`/`mousedown` set `Pointer`. Listeners are capture-phase on the window.
- `InputState.capabilities`: a H07 provider with PlatformFacility keys `input.window-activation`,
  `input.keyboard-modality`, `input.touch-points` and `input.pointer-capture`. Touch points report Available No at
  capacity 0; the native provider reports Implemented No for touch delivery. A runtime without "input-state" reports
  the Abi capability `input-state` as Exported No through the H07 catalogue, and `InputState.forHost` raises
  NotSupportedException.

Browser provider: `BrowserInput.forDocument` (Fable.Ranvier.Dom). Native provider:
`Ranvier.View.WebCore.InputState.forHost`.

## Verification

- RED: `tests/abi/focus_input.py` on Native25 (ABI 33, no feature, no exports).
- GREEN: the same probe on the private Native26 candidate: window focus/blur on `rv_host_set_focused`, window
  identity and currentTarget, capture-phase input reaching the window, touch points, pointer capture events and
  teardown (document replacement, release after destroy, release before destroy).
- `tests/NativeApi.Conformance/InputState.fs` on native JIT, NativeAOT and Chromium (new headless): activation
  change, modality sequence with focus-visible, capture gain/loss, NotFoundError, capability reports and disposal.
