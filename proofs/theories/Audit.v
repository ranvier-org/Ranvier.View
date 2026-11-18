From RanvierView.Pure Require Import Layout Hydration Lifetime Scheduling.
From RanvierView.Conformance Require Check.
From RanvierView Require Extract.

(** One [Print Assumptions] per theorem; [check.sh] verifies every theorem is
    [Closed under the global context] with zero unapproved axioms. *)

(* Layout & Departures *)
Print Assumptions move_before_commutes_with_departures.
Print Assumptions place_rtl_commutes_with_departures.
Print Assumptions place_rtl_places_exact_order.
Print Assumptions move_many_before_inserts_exact.
Print Assumptions outer_swap_places_exact.
Print Assumptions increasing_run_length.
Print Assumptions departures_retire_preserves_live_siblings.
Print Assumptions flush_released_fast_clear_sound.
Print Assumptions reflow_latest_post_closes_once.

(* Hydration & ViewBoundary *)
Print Assumptions matching_end_well_bracketed.
Print Assumptions logical_next_skips_region.
Print Assumptions hydration_cursor_restore_exact_inverse.
Print Assumptions hydration_cursor_commit_clears_held.
Print Assumptions binding_expect_preserves_waiting_inv.
Print Assumptions binding_step_preserves_waiting_inv.
Print Assumptions binding_dispose_preserves_waiting_inv.
Print Assumptions binding_not_ready_idempotent_and_silent.
Print Assumptions binding_recovered_clears_iff_failed.
Print Assumptions boundary_stale_retry_fenced.

(* Placement, MountLifetime, & TeardownWindow *)
Print Assumptions placement_fast_path_eq_stable_sort.
Print Assumptions pl_add_preserves_order_inv.
Print Assumptions pl_place_own_before_forwarded.
Print Assumptions pl_discard_drops_held_and_blocks_later.
Print Assumptions pass_inserted_flag_exact.
Print Assumptions mount_lifetime_cleanups_precede_all_releases.
Print Assumptions teardown_window_exact_partition.

(* Scheduling, OwnerWorkQueue, & CompletionFence *)
Print Assumptions q_enqueue_preserves_pending_inv.
Print Assumptions q_reenqueue_coalesces_in_place.
Print Assumptions q_stale_ticket_after_flush_noop.
Print Assumptions q_ticket_after_dispose_noop.
Print Assumptions exec_work_stopped_commits_nothing.
Print Assumptions queue_report_groups_each_scope_once.
Print Assumptions owq_operations_preserve_conservation.
Print Assumptions owq_no_lost_wakeup.
Print Assumptions cf_try_complete_preserves_inv.
Print Assumptions cf_reset_preserves_inv_and_rejects_pending.
