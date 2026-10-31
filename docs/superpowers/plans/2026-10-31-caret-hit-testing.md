# G09 caret hit testing

Route: Engine (ranvier-org/Ranvier.WebCore#54). Native: `Document.caretPositionFromPoint`, `CaretPosition`
(`offsetNode`, `offset`, `getClientRect`) and `CaretPositionFromPointOptions.shadowRoots`, generated into ABI40 /
Native preview.32, stacked on G05 dc0951a, D16 2fc30ea and H11. Managed: DOM alpha.35 (Core) and Views 0.1.25 /
backend alpha.25, stacked on the H01 heads (Core 60b0fa69, Views bf1f59d) and the D16 and H11 managed heads.
`Document.caretRangeFromPoint`, already generated, is measured beside it.

## Core and view state

- **Core primitives used:** none. A hit test is a synchronous query of the current layout: it holds no value over
  time, has no owner and schedules nothing. A caller that hit-tests inside an effect or event handler uses that
  scope's own primitives.
- **View-owned state:**
  - The native `CaretPosition` handle inside the generated wrapper, released as every generated handle wrapper
    releases its handle. It is a native object reference with no core equivalent.
  - No other state. The browser target returns Chromium's own `CaretPosition`.

## RED (measured 2026-10-31)

Fixture: `tests/fixtures/caret-hit.html` in Ranvier.WebCore, pinned Ahem 20px squares, 320x240 viewport. Oracle:
`tests/fixtures/caret-hit-oracle.json`, Chromium 156.0.8078.4 new headless, device scales 1, 1.5 and 2, unscrolled,
scrolled to (50, 590), (0.5, 20.5) and (0, 20), 41 points each: wrapped text, inline element boundaries, an RTL `bdo`
run and an RTL block, open and closed shadow roots, a text control, an empty box, outside-viewport points, and
non-finite coordinates.

- Native29 exports no `caretPositionFromPoint` or `CaretPosition` member
  (`docs/caret-hit-evidence/red-measure.json`). The model's `CaretPosition` was an empty interface.
- Chromium: `caretPositionFromPoint` throws `TypeError` for NaN and infinite coordinates; returns null outside the
  viewport; retargets a hit inside an unlisted shadow tree to the host's parent at the host's index (CSSOM View,
  `caretPositionFromPoint()`); returns the text control element for a hit in an input.
- Native29 `caretRangeFromPoint` (WebIDL `long`, truncated) agrees with Chromium on 38 of 41 points unscrolled. It
  retargets shadow and text-control hits to the host at offset 0.
- After `scrollTo(0.5, 20.5)` the native engine sits at (0, 20) and Chromium at (1, 21): the G05 truncation. Each
  runtime is checked against the oracle case whose actual scroll offset equals its own.

## Contract

- Generated model (`Ranvier.Dom`, from `gen/Ranvier.Dom.fs`):
  - `Document.caretPositionFromPoint: x: float * y: float * ?options: CaretPositionFromPointOptions -> CaretPosition`;
    `null` where no position exists (outside the viewport, no render tree).
  - `CaretPosition.offsetNode: Node`, `CaretPosition.offset: uint32`, `CaretPosition.getClientRect: unit -> DOMRect`.
  - `CaretPositionFromPointOptions.shadowRoots: ShadowRoot array`, default empty.
- Native ABI40: `rv_dom_document_caret_position_from_point` checks `CaretPositionFromPointEnabled` (on by default)
  and returns `RV_ERR_DOM_EXCEPTION` with `TypeError` for non-finite coordinates. `rv_dom_caret_position_*` members
  rely on the factory's setting check (`creationGatedInterfaces`). Capability: `dom:CaretPosition`.
- Views: the generated `Document.caretPositionFromPoint` wrapper and a `CaretPosition` wrapper, from
  `tools/Ranvier.View.WebCore.Generate`.

## Verification

- Native: `tests/abi/caret_hit.py` replays all 12 oracle cases (1476 comparisons) on the runtime and requires the
  differences from Chromium to equal `tests/fixtures/caret-hit-qualified.json`; checks errors, threads and options
  struct sizes; holds a `CaretPosition` past a text change, node removal and `rv_host_destroy`. Its own build.yml
  slim step runs it on every slim runtime.
- Managed: `tests/NativeApi.Conformance/CaretChecks.fs` replays the same oracle through `Ranvier.Dom` on native JIT,
  NativeAOT and Chromium (1537 assertions each), with the qualified differences per backend, and a host-disposal
  lifetime case.

## Qualifications

Measured on Native32 (Source 0000-0035) against Chromium 156.0.8078.4; the native probe requires exactly these
(`tests/fixtures/caret-hit-qualified.json`, 90 of 1476 comparisons) and the managed fixture asserts the same 30
outcomes at each of the three scales:

- Legacy `caretRangeFromPoint` retargets a hit in a shadow tree or text control to the host at offset 0; Chromium
  gives the host's parent at the host's index (21 comparisons). `caretPositionFromPoint` agrees with Chromium.
- `CaretPosition.getClientRect` is 0x0 at (0, 0) at a line end before a collapsed space (wrap offsets 4 and 9, RTL
  offset 4); Chromium gives the 0x20 caret box (48).
- `getClientRect` in a text control (`INPUT`, offset 1) is 0x0 at (0, 0); Chromium gives the caret box (18).
- A held caret whose text shrank below its offset gives the 0x20 box at x 40; Chromium gives 0x0 (3).
- After `scrollTo(0.5, 20.5)` the engine sits at (0, 20) and Chromium at (1, 21) (G05); each backend is checked at
  its own offset, so this adds no hit difference.

Patch 0035 fixes the one engine defect found: with a non-empty `shadowRoots`, a hit in a text control outside any
shadow tree returned a node of the control's user-agent shadow root.

Evidence: Ranvier.WebCore `docs/caret-hit-evidence/final-evidence.json`; Views
`docs/research/native-api-delivery/caret-runtime-evidence.json` (JIT 91, NativeAOT 91, Chromium 70 PASS lines,
a superset of H11's 89/89/69).
