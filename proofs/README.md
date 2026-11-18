# Ranvier.View Proofs

Pure Rocq 9.1 formalization and differential conformance suite for the view-owned state machines in
`Ranvier.View`, `Ranvier.View.Dom`, `Ranvier.View.WebCore`, and `Ranvier.View.WebCore.Sdl`.

## Scope

Reactive graph semantics, glitch freedom, cutoff soundness, `Owner` disposal order, and collection
diffing (`KeyedProjection`, `Positional.diff`) are verified in [`ranvier-org/Ranvier`](https://github.com/ranvier-org/Ranvier).
This directory verifies the stateful algorithms that `Ranvier.View` layers **above** the reactive core:

1. **`theories/Pure/Layout.v`** — DOM sibling range mutations (`move_before`), `Row.Layout`
   (prefix/suffix framing, pure insert, outer-pair swap, right-to-left placement outside `increasingRun`),
   `Departures` held-exit interleaving (`move_before_commutes_with_departures`, `place_rtl_commutes_with_departures`,
   `flush_released_fast_clear_sound`), and `Reflow` measurement gating (`reflow_latest_post_closes_once`).
2. **`theories/Pure/Hydration.v`** — SSR bracket marker balancing (`matching_end_well_bracketed`,
   `logical_next_skips_region`), hierarchical `HydrationCursor` LIFO rollback (`hydration_cursor_restore_exact_inverse`,
   `hydration_cursor_commit_clears_held`), and `BindingState` / `ViewBoundary` readiness & generation
   fencing (`binding_step_preserves_waiting_inv`, `boundary_stale_retry_fenced`).
3. **`theories/Pure/Lifetime.v`** — `Placement` instance-group hook ordering (`placement_fast_path_eq_stable_sort`,
   `pl_place_own_before_forwarded`, `pl_discard_drops_held_and_blocks_later`), `PlacementPass` `Inserted`
   tracking, `MountLifetime` attached-DOM cleanup ordering (`mount_lifetime_cleanups_precede_all_releases`),
   and `TeardownWindow` exact error partitioning (`teardown_window_exact_partition`).
4. **`theories/Pure/Scheduling.v`** — `Scheduling.Queue` slot deduplication, version cancellation,
   `FailFastCancel` `Dropped` callbacks, stale ticket invalidation, and per-scope error grouping
   (`q_enqueue_preserves_pending_inv`, `q_reenqueue_coalesces_in_place`, `q_stale_ticket_after_flush_noop`,
   `queue_report_groups_each_scope_once`), `OwnerWorkQueue` item conservation and no-lost-wakeup
   (`owq_operations_preserve_conservation`, `owq_no_lost_wakeup`), and `CompletionFence` GPU marker
   lifecycle (`cf_try_complete_preserves_inv`, `cf_reset_preserves_inv_and_rejects_pending`).

## Commands

- Build Rocq theories & extract OCaml model: `bash proofs/build.sh`
- Full audit (zero-axiom check, conformance regeneration, extraction check, 400 differential runs): `bash proofs/check.sh`
- Randomized differential check against compiled F# assemblies: `bash proofs/differential/run.sh [count] [seed]`

See `VERSIONS.md` for toolchain pins and `CORRESPONDENCE.md` for the symbol-by-symbol F# mapping.
