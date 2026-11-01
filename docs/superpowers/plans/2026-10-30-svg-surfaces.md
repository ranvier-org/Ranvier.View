# Shared SVG shapes, text, views and SMIL

**Goal:** Deliver S03, S05, S06 and S07 through the actual shared browser/native DOM, and prove the remaining real Boolean/Integer value routes supporting S04. Incidental filter members do not accept S09.

**Architecture:** Import the Native19 canonical contracts and append the one new SVGAnimatedInteger cache/release kind. Reuse the existing native wrapper generator and session ownership rules. Use Element attribute transport for authored path data; use each engine's own parser, geometry, rendering and SMIL implementation.

**Starting points:** Core `a2562e814b028a94a6390235e777d3bbea987b4d` and Views `dd96497c2568636d6af752cdee77bb4a2e175325`, isolated on `codex/native-api-svg-surfaces`. These contain H06/Core20/Views0.1.10 over the frozen SVG geometry/value source. Forward-merge final H06 metadata before opening PRs. Preserve current Core19/Views0.1.9 trees and evidence.

**Reservation:** Core DOM/Fable DOM alpha21, Views0.1.11 and backend/SDL alpha11 now require Native20/ABI28 so the current item includes actual same-document named-view/svgView selection. Root's fresh absence record is `C:/wk/rv-next20-22-allocation-check.json`. H07 follows with pair22/Views0.1.12 using the same runtime20. Native19 stays frozen; its staged ABI27 proof is provisional supporting evidence, never final consumer21 coherence.

## Canonical scope and import

- Source `C:/wk/native-svg-surfaces/gen/Ranvier.Dom.fs` SHA256 `419e740286d121e8e3b25edcfd7b59cec5e22dd8eb768a4c59f7a2f3220a9c93` (CRLF).
- Model SHA256 `59f3bda27bda810476e199c84be83992c1affefa08b06da32aa62f8b93cb4d81`; config SHA256 `e5686acb7fa441604ee50c6d870feb3a99ec5b4f973f0a525070843666232a8e`.
- Producer scope: 205 interfaces, 2234 functions, 50 handle roots, 22 dictionaries, zero stubs. All 2182 previous function records are preserved; 52 are added.
- Canonical and all24 shipping ABI27 payloads were handed off and hash-verified. They support provisional shape/text/SMIL/Boolean/Integer checks only; final consumer21 execution waits for Native20/ABI28 selector interop and payload hashes. Never use diagnostic-only exports or mix runtime cohorts.

## Implementation and proof

- [x] Capture clean baseline FCS/API and impact before public changes; import only hash-verified canonical source and retain the complete API delta.
- [x] Allocate the reserved consumer metadata separately from body changes. Prepare private exact interop/runtime snapshots after producer handoff.
- [x] Append SVGAnimatedInteger's release/cache kind without renumbering existing kinds. Regenerate wrappers, require zero unmapped members and compare all previous non-SVG blocks and exports.
- [x] Expose nullable authored path data over actual SVGPathElement get/set/remove attribute operations. Verify edits, removal, invalid data and resulting native/browser geometry, without implementing a parser or segment API.
- [x] Prove circle/ellipse/line/rect animated lengths and polygon/polyline live/read-only points through resulting bounds and paint; include detached values and ownership boundaries.
- [x] Prove text/tspan/textPath with pinned Ahem font readiness: all character metrics, substring/selection/errors, textLength/lengthAdjust, positioning/rotation lists and path offsets. Keep measured vertical-text and rotated-boundary provider qualifications explicit.
- [x] Add genuine SVG-document host support and a browser SVG MIME context through existing loading boundaries. Run FCS impact before any host API addition. Preserve scripts-disabled behavior, XML parser-error documents, document transitions and retained values.
- [x] Model currentView/useCurrentView exposure explicitly as available, NotExposed, Unsupported or Unknown. Never box undefined as a nonnullable view object or infer applied view state from requested attributes. Test actual CTM, BCR, intersection/enclosure, named-view and pan/zoom effects in inline and standalone contexts.
- [x] Retain deprecated redraw methods as actual no-ops, without claiming suspension. Record standalone page-zoom versus SVG-zoom differences and the measured inline currentTranslate difference.
- [x] Prove SMIL target identity, finite offsets, pause/seek/begin/end/resume, delayed starts and real running begin/repeat/end events against rendered geometry. Keep paused-seek event differences explicit; A03/A12 remain separate dependencies.
- [x] Reach real SVGFEConvolveMatrixElement.preserveAlpha and integer fields, verify shared Boolean/Integer wrapper identity/mutation/lifetime and close only S04's demonstrated remaining route after acceptance gates.
- [x] Run focused shared JIT, real NativeAOT and packaged Fable/Chromium with exact executed payload checks; preserve actual initial failures. Batch final combined and configured repository gates after source stabilizes.
- [ ] Freeze source/package/runtime/log manifests, obtain independent review, and open ordered Core21/Views0.1.11 PRs against frozen H06 parents. Official packages, remaining platforms/profiles and public acceptance remain required.

## Lifetime contract

Native wrappers retain their first acquisition receiver. Cache hits preserve that anchor; later list ownership is not a promise to retain every later owner. Retained lists keep their owners/documents; engine owner destruction may detach a retained item. Preserve actual wrapper identity, strong bidirectional mutations and SameObject property checks; introduce no identity registry or geometry copies.
