# Task 8 execution — webcore-performance-plan.md

## Core and view state

Core retains reactive ownership, batching and publication. View's new state is the capacity and lifetime of detached snapshot encoding jobs, with terminal delivery through the existing ResourceArbiter and host queue. Native capture/DOM work remains on owner; worker-local image buffers contain only copied pixels.

## Preflight

- User authorizes implementation, tests, merge and new releases after CI passes. Existing worktrees: View 1b3cd1f, native cc8dc84b. Tracked trees are clean; upstream main remains dab7ab6/9f4f71ce. Preserve unrelated untracked directories and other worktrees.
- Tasks 0–7 are complete as recorded in previous ledgers. Task 8 consumes host wake/post, snapshot resource lifetime and GPU fallback contracts. Preserve legacy capture semantics and GPU congestion handling.
- SageFs get_daemon_status returns Transport closed. Use isolated CLI checks without restarting the user's daemon. Native Serena supports Python only; use direct source inspection for C++.
- Release sequencing: native ABI/package first, then View consumer dependency and release. Inspect actual repository release conventions and published versions before choosing version numbers. Merge only after relevant CI passes.
- Ruling: reuse existing owner-thread RGBA snapshot capture and detach its bytes before worker encoding. New native encoding accepts raw pixels and creates all image objects locally on the caller worker; it receives no live host/DOM/GPU handles. Cost: raw capture and one bitmap conversion still run/copy separately.
- Ruling: one running encode and one replaceable preview per host, shared across services; export requests reject when busy. Cancellation is cooperative at delivery and does not free an active worker slot early. Cost: cancelled encoding can occupy capacity until its codec finishes.
- CPU staging arrays/textures already persist in View. Measure native scratch allocation and upload separately before deciding which additional reuse pays for its complexity.

## Status

Task 8 in progress: native encoder and managed admission/lifetime implementation tested locally.

- Native 9072523e adds ABI 70 detached worker encoding and independent buffer/raster/encode/copy trace phases. Windows probes pass: snapshot options 579 assertions, partial repaint 26, trace 337; detached PNG/JPEG/WebP encoding runs after host destruction. GPU source CI 42852924311 and Slim source CI 42853884528 are running with 60-second gh watches.
- Warm 960x540 raw capture: about 2.5 ms (allocation 0.010 ms, raster 1.05 ms, copying 1.37 ms); synchronous PNG about 10.1 ms, with encoding about 9 ms.
- Ruling: do not add a native scratch-buffer cache for a measured 0.010 ms allocation. Existing View staging arrays and textures already persist; instrument their allocation and upload and verify invalidation instead. Cost if wrong: additional allocation savings remain unrealized.
- Ruling: queued previews capture when admitted, while export Capture still paints during the call. This avoids holding another detached bitmap or live native handles in the pending slot. Cost: a preview observes the newer document state at admission; the public API documents this explicitly.
- Managed RED: new BackgroundSnapshots API missing. GREEN: five lifetime/admission cases plus full Windows suite, 948 passed / 18 platform exclusions, no failures, against privately built ABI 70. Tests cover shared per-host capacity, preview replacement, active cancellation, disposal before owner delivery, document replacement, host destruction and worker-dispatch failure.
- NuGet already contains native 0.1.1-rc.5 and SDL 0.1.0-rc.6, newer than some checked-in version fields. Verify package source commits and choose monotonic versions before publication.
- Package provenance verified: native rc.5 c8e69dd and SDL rc.6 49a348b are both ancestors of these branches. Native next version is 0.1.1-rc.6; View WebCore will advance to rc.4 and SDL to rc.7 after native publication.
- CPU staging trace/pixel check RED (missing phases) → GREEN. Measured software-renderer 200x150 allocation 0.0407 ms once, uploads 0.0093/0.0082 ms, no reallocation for the second paint; removed content equals a fresh full repaint. Full suite before review: 949 passed / 18 exclusions. First incremental F# build retained an old reference assembly; a scoped WebCore rebuild fixed compilation, followed by a fresh successful test build.
- Final independent review by task8_release_review found one Important issue: encoder bounds applied after capture could permit oversized owner allocations. Native f4f26552 adds opt-in rv_host_snapshot_bounded, checking effective cropped/scaled backend dimensions before ImageBuffer allocation. Legacy entry point is unchanged. RED missing bounded export → GREEN bounded_capture, plus 579 legacy snapshot assertions and detached encoding. Managed service consumes bounded capture; rejected capture returns a failure rather than a spurious cancellation. Seven managed lifetime tests pass, including oversized capture and cancellation before the first owner cycle. Whole-suite gate follows this commit.
- Final: Ruling: physical IME/input-to-photon evidence remains a hardware acceptance item from prior tasks, not a claim of this release. Synthetic CI proves contracts only; cost if wrong is unmeasured physical-device latency.
- Final: Ruling: publication readiness remains gated on final-head cross-platform CI, package hashes and dependency versions. No merge or publication based solely on the review verdict.
- Reviewed native CI started: Linux GPU 42857841372; Windows/Linux Slim 42857863724. Earlier Windows GPU/macOS jobs are allowed to finish to retain build caches, then those cells will be rerun for the bounded-capture fix.
