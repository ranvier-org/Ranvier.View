# Extreme lines and snapshot owner work

## Core and view state

Use the existing Graph, signals, batch and keyed row projection. New state describes the DOM projection of a logical text line (column origin, extent and cached eligibility); it does not replace graph ownership or scheduling. Document snapshots remain the source of full text.

## Goal and constraints

Reduce WebCore shaping work for extreme code lines and eliminate avoidable owner-thread snapshot raster/copy work. Keep contextual shaping, bidi, composition and retained snapshot results correct. Reuse existing worktrees, preserve unrelated tooling, benchmark each batch, review edge cases, merge green PRs and release native-first when an ABI extension is required. Existing authorization includes pushes, merges and publication without further permission questions.

Baselines: View a40fd16c57212b7f9527f6836ca28cd5181e5334; native f61977ca19ebda64070cdb74750774a3f9bc05e6. Both remotes unchanged at start.

## Batch A: bounded extreme-line projection

- Add an opt-in horizontal window for unwrapped fixed-pitch printable ASCII, with bounded overscan. Disable kerning/ligatures consistently in the optimized and reference rendering. Preserve full text and logical UTF-16 columns outside the DOM projection.
- Cache eligibility by line revision. Wrapped text, non-ASCII/contextual text, uncertain font metrics and active composition use whole-line rendering. Never split a surrogate or claim a bidi line is safe from a visible substring alone.
- Test projection bounds, far horizontal scroll, caret/column mapping, edits, font/layout changes, composition and complex-script fallback. Compare pixels with whole-line rendering.
- Benchmark fixed-operation extreme-line edits/scrolls with equal work and identical runtime; retain raw samples. Gate bounded projected length and no-op DOM work deterministically; compare latency/allocation across five fresh-process cohorts.

## Batch B: native snapshot ownership and paint reduction

- Profile existing capture raster and pixel-copy phases. Inspect WebCore's safe detached image/display-list facilities before selecting the transfer mechanism.
- Introduce explicit native ownership for worker-consumable immutable capture data; no worker may touch mutable DOM, layout, or owner-thread-only graphics resources. Avoid the native-to-managed-to-native RGBA round trip for encoded captures.
- Reduce repeated raster work with a validated reusable capture or safely detached recording path. Capture identity must cover content, layout, scale, crop, color space and document replacement. Do not hide raster work by excluding a queue drain from timing.
- Test cancel/close at each phase, wrong-thread calls, stale generations, concurrent ownership, resize, alpha/color correctness and retained results. Preserve old APIs and capability-gate additions.
- Benchmark owner latency, end-to-end completion, allocation and phase timings at 800x600 and 1600x1200. Publish remaining unavoidable owner work explicitly.

## Release gates

Focused regressions then full suites for each batch; independent final review; Windows/Linux/macOS native/runtime qualification and matched performance cohorts. Follow native packaging/generation and ShipIt-managed versions. Verify public-package consumers and packaged documentation; clean merged feature branches while retaining existing worktrees with unrelated files.

## Execution ledger

- SageFs get_daemon_status returned `Transport closed`; compiled runner is used without restarting the user's daemon.
- Horizontal projection regression failed as expected: a 100,000-character line still projected 100,000 characters instead of at most 2,048. The first filtered command selected zero tests; corrected to the full `Layout phases.horizontal projection` prefix and verified one actual failure.
- Native project Serena exposes only Python, so its C++ symbols cannot be read there; native C++ uses targeted filesystem reads.
- Ruling: the reference editor uses explicit platform monospace fonts, disabled kerning/ligatures and integer CSS-pixel cells in both full and projected modes. Generic monospace resolved to a proportional font on Windows; fractional glyph advances accumulated visible drift at distant offsets. Integer cells passed byte-exact full/projection comparisons through 700,000px. This is an editor cell policy, not arbitrary HTML/CSS text equivalence.
- Bounded projection is opt-in (`horizontalCulling = true`, fixture `--horizontal-culling`). It retains complete source text and exposes logical/local column conversion. Native DOM selection across projected text is not integrated; the fixture's editable input remains the separate textarea. Contextual lines, wrapping and composition render complete rows.
- Full suite before review: 970 passed, 18 platform-inapplicable ignored. Review found stale logical-width measurement after an offscreen synthetic edit and missing mutation annotations. Both failures were reproduced with focused tests before fixing; the complete Layout phases group then passed.
- Benchmark protocol: five fresh processes per mode/scenario, 100 operations each, one 100,000-character row, identical runtime/fonts/cell policy. Cross-platform qualification now gates median p95 at baseline * 1.25 + 0.5ms and allocation at baseline + 8KiB/op; deterministic tests cap projected text at 2,048 characters.
- Ruling: pinned WebKit DisplayList.h explicitly disallows multi-thread use because retained Font objects are not thread-safe. Batch B will use immutable native pixels and conservatively invalidated paint reuse instead of moving its display list onto a worker.
