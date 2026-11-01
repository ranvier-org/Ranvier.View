# Shared SVG geometry and values implementation plan

> Agent execution: continue inline with the existing implementation lane; independent acceptance review remains separately owned.

**Goal:** Deliver selected S01 bounds/transforms/viewBox, S02 path metrics/hit testing and S04 SVG values/lists through the shared browser/native surface.

**Architecture:** Import the producer's canonical contracts and generate native wrappers over the coherent engine ABI. Preserve actual SVG value types, session ownership and engine list semantics. Browser execution uses its realm's actual SVG DOM objects.

**Spec:** Producer `docs/SVG-GEOMETRY-PLAN.md` and `docs/svg-geometry-scope.json`; shared selected task scope and stable issues WebCore#78/#79/#81.

## Constraints and source ownership

- Core worktree `native-api-svg-values/Ranvier` starts at final C16 `1cfb3e06`; Views `C:/wk/rv-svg-values` starts at `fdc56dc28`. C16 PR267/Views18 trees remain separate.
- Producer candidate has 189 interfaces, 2182 functions, 49 handle roots, 22 dictionaries and zero stubs. Import only hash-verified canonical inputs supplied by the producer owner.
- SVGPoint/SVGRect fields use float precision; SVGMatrix fields use double precision. Never reinterpret native pointers as DOM geometry handles.
- Preserve all previous ABI function records and cache kinds. New reference inputs reject cross-session/thread/disposed objects and remain rooted through native calls.
- S03 and incidental SVG animation/view APIs remain independently required. No group closes from generated surface or private runtime proof.
- No new package/ABI allocation until the release owner verifies version availability. No shared WebKit edits in this lane.

## Tasks and verification

- [x] Import hash-verified canonical `gen/Ranvier.Dom.fs` into Core `src/Ranvier.Dom/Generated/Dom.fs`; capture public API additions and fresh FCS verdict. Coordinate formatting back to producer before freezing.
- [x] Compile private canonical Core/interop inputs and run the existing wrapper generator. Record actual unmapped/root-kind diagnostics before extending `Program.fs`, `Emit.fs`, `Objects.fs` and `Host.fs` narrowly.
- [x] Append SVG release/cache kinds. Preserve SameObject properties, argument lifetimes and same-session validation. Record measured backend list wrapper identity and strong shared mutation aliasing. Regenerate outputs and audit unchanged old class bodies.
- [x] Add `tests/NativeApi.Conformance/SvgGeometry.fs` and `SvgValues.fs` shared cases; wire both runners. Cover local/nested bounds, CTM/screenCTM/viewBox/aspect, line/curve length and points, fill/stroke coordinate space, factory defaults, matrix/point operations and units.
- [x] Cover number/length/point/transform/string list insert/replace/remove/clear/initialize, clone versus identity behavior, read-only animated values, rejected-edit preservation and detached owners. Add native lifecycle/thread/session/disposal/collection and browser context checks.
- [x] Run focused JIT/NativeAOT/packaged Fable Chromium checks against coherent hash-matched private runtime. Preserve initial failures and compare actual browser differences before choosing a facade.
- [x] Batch combined runtime and required repository analyzer/build/test gates once after source stabilizes. Record source, package, executed binaries and logs in durable evidence JSON; independent review before PR/release.
- [ ] Open ordered producer/Core/Views stacks after version reservation. Final official packages, platform/profile acceptance and stable issue evidence remain required.

## Review focus

Check nullable CTM and point-hit dictionaries, float precision versus double matrix fields, list identity after move/clone/remove, animated read-only state while attached versus detached, and retained argument/document lifetime. Every returned native SVG object must retain the correct release function and acquisition context.
