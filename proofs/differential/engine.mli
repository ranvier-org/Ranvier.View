
type bool =
| True
| False

val negb : bool -> bool

type nat =
| O
| S of nat

type 'a option =
| Some of 'a
| None

type ('a, 'b) prod =
| Pair of 'a * 'b

val fst : ('a1, 'a2) prod -> 'a1

val snd : ('a1, 'a2) prod -> 'a2

type 'a list =
| Nil
| Cons of 'a * 'a list

val length : 'a1 list -> nat

val app : 'a1 list -> 'a1 list -> 'a1 list

type comparison =
| Eq
| Lt
| Gt

val compOpp : comparison -> comparison

val add : nat -> nat -> nat

val sub : nat -> nat -> nat

module Nat :
 sig
  val eqb : nat -> nat -> bool

  val leb : nat -> nat -> bool

  val ltb : nat -> nat -> bool
 end

val map : ('a1 -> 'a2) -> 'a1 list -> 'a2 list

val seq : nat -> nat -> nat list

val repeat : 'a1 -> nat -> 'a1 list

val nth : nat -> 'a1 list -> 'a1 -> 'a1

val firstn : nat -> 'a1 list -> 'a1 list

val skipn : nat -> 'a1 list -> 'a1 list

val nth_error : 'a1 list -> nat -> 'a1 option

val rev : 'a1 list -> 'a1 list

val existsb : ('a1 -> bool) -> 'a1 list -> bool

val forallb : ('a1 -> bool) -> 'a1 list -> bool

val filter : ('a1 -> bool) -> 'a1 list -> 'a1 list

val combine : 'a1 list -> 'a2 list -> ('a1, 'a2) prod list

type positive =
| XI of positive
| XO of positive
| XH

type z =
| Z0
| Zpos of positive
| Zneg of positive

module Pos :
 sig
  val succ : positive -> positive

  val add : positive -> positive -> positive

  val add_carry : positive -> positive -> positive

  val pred_double : positive -> positive

  val compare_cont : comparison -> positive -> positive -> comparison

  val compare : positive -> positive -> comparison

  val eqb : positive -> positive -> bool

  val of_succ_nat : nat -> positive
 end

module Z :
 sig
  val double : z -> z

  val succ_double : z -> z

  val pred_double : z -> z

  val pos_sub : positive -> positive -> z

  val add : z -> z -> z

  val opp : z -> z

  val sub : z -> z -> z

  val compare : z -> z -> comparison

  val ltb : z -> z -> bool

  val eqb : z -> z -> bool

  val of_nat : nat -> z

  val geb : z -> z -> bool
 end

val remove_node : nat -> nat list -> nat list

val insert_at_anchor : nat -> nat -> nat list -> nat list

val move_before : nat -> nat -> nat list -> nat list

val move_many_before : nat list -> nat -> nat list -> nat list

val place_rtl_step :
  (nat, bool) prod list -> nat -> nat list -> (nat, nat list) prod

val place_rtl : nat list -> bool list -> nat -> nat list -> nat list

val lower_bound_tails : z list -> nat list -> z -> nat

val list_set : nat -> 'a1 -> 'a1 list -> 'a1 list

val update_tails : nat -> nat -> nat list -> nat list

val lis_scan :
  z list -> nat -> z list -> nat list -> nat option list -> (nat list, nat
  option list) prod

val common_prefix_len : nat list -> nat list -> nat

val common_suffix_len : nat list -> nat list -> nat

val is_outer_swap : nat list -> nat list -> bool

type dep_state = { dep_exits : nat list; dep_leaving : nat list;
                   dep_pending : nat list }

val mem_nat : nat -> nat list -> bool

val filter_live : nat list -> nat list -> nat list

val between_Exclusive : nat -> nat -> nat list -> nat list

val holds_between : dep_state -> nat -> nat -> nat list -> bool

val retire_or_remove :
  nat -> dep_state -> nat list -> (dep_state, nat list) prod

val finish_exit : nat -> dep_state -> nat list -> (dep_state, nat list) prod

val clear_between : nat -> nat -> nat list -> nat list

val release_rows :
  nat list -> dep_state -> nat list -> (dep_state, nat list) prod

val flush_released :
  bool -> nat -> nat -> nat list -> dep_state -> nat list -> (dep_state, nat
  list) prod

type reflow_state = { rf_open : bool; rf_posts : nat; rf_before_runs : 
                      nat; rf_after_runs : nat }

val reflow_before : reflow_state -> (reflow_state, nat) prod

val reflow_run_post : nat -> reflow_state -> reflow_state

val reflow_abort_post : nat -> reflow_state -> reflow_state

val stamp_layout_indices : nat -> nat list -> nat -> z

val mark_chain_in_place :
  nat -> nat option -> nat option list -> z list -> z list

val increasing_run_in_place : z list -> z list

val strictly_increasing_from : z -> z list -> bool

val strictly_increasing_nonneg : z list -> bool

val row_layout_fast : nat list -> nat list -> nat -> nat list -> nat list

type ssr_token =
| TokStart of nat
| TokEnd
| TokNode of nat

val scan_matching_end : nat -> ssr_token list -> ssr_token list option

val logical_next : ssr_token list -> ssr_token list option

type ssr_tree =
| SNode of nat
| SRegion of nat * ssr_tree list

val flatten_tree : ssr_tree -> ssr_token list

type bstate = { bs_failed : bool; bs_waiting : bool }

type read_outcome =
| OutValue
| OutNotReady
| OutError

type boundary_model = { bm_tracks : bool; bm_waiting : nat;
                        bm_setting_up : bool; bm_revealed : bool;
                        bm_failed : bool; bm_pending_sig : bool;
                        bm_wakes : nat; bm_clear_calls : nat;
                        bm_fail_calls : nat; bm_gen : nat }

val bs_expect : boundary_model -> (boundary_model, bstate) prod

val bm_wait : boundary_model -> boundary_model

val bm_ready : boundary_model -> boundary_model

val bm_fail : boundary_model -> boundary_model

val binding_step :
  read_outcome -> boundary_model -> bstate -> (boundary_model, bstate) prod

val binding_dispose :
  boundary_model -> bstate -> (boundary_model, bstate) prod

val bm_retry : nat -> boundary_model -> boundary_model

val update_binding : nat -> bstate -> bstate list -> bstate list

type 'st cursor_tag_entry =
| TagCustom of ('st -> 'st)
| TagInsertBefore of ('st -> 'st)
| TagRemove of ('st -> 'st)
| TagReplace of ('st -> 'st)
| TagNest of 'st cursor_tag_entry list

val restore_tag_entry : 'a1 cursor_tag_entry -> 'a1 -> 'a1

val restore_tag_entries : 'a1 cursor_tag_entry list -> 'a1 -> 'a1

val held_tag_entry : 'a1 cursor_tag_entry -> nat

val held_tag_entries : 'a1 cursor_tag_entry list -> nat

val commit_tag_entries :
  'a1 cursor_tag_entry list -> 'a1 cursor_tag_entry list

type placement_model = { pl_laid_out : bool; pl_discarded : bool;
                         pl_parent_placed : bool; pl_ordered : bool;
                         pl_last_group : nat; pl_group : nat;
                         pl_entered : nat; pl_held : (nat, nat) prod list;
                         pl_forwarded : nat list; pl_executed : nat list;
                         pl_to_parent : nat list }

val init_placement : bool -> placement_model

val pl_is_placed : placement_model -> bool

val pl_enter : placement_model -> (placement_model, nat) prod

val pl_leave : nat -> placement_model -> placement_model

val pl_enqueue : nat -> bool -> placement_model -> placement_model

val pl_add : nat -> placement_model -> placement_model

val pl_discard : placement_model -> placement_model

val pl_held_count : placement_model -> nat

type lifetime_tree =
| LTNode of nat * nat list * lifetime_tree list

type lt_event =
| EvOwnerCleanup of nat * nat
| EvDomRelease of nat

val collect_teardown_sub :
  lifetime_tree -> (lt_event list, lt_event list) prod

val dispose_lifetime_tree : lifetime_tree -> lt_event list

val in_interval : nat -> (nat, nat) prod -> bool

val in_any_nested : nat -> (nat, nat) prod list -> bool

val teardown_window_indices : nat -> nat -> (nat, nat) prod list -> nat list

val insert_from_right_rev :
  nat -> nat -> (nat, nat) prod list -> (nat, nat) prod list

val insert_from_right :
  nat -> nat -> (nat, nat) prod list -> (nat, nat) prod list

val in_place_insertion_sort_acc :
  (nat, nat) prod list -> (nat, nat) prod list -> (nat, nat) prod list

val in_place_insertion_sort : (nat, nat) prod list -> (nat, nat) prod list

val pl_place_fast : placement_model -> placement_model

type commit_failure =
| CollectAndContinue
| FailFastCancel

type slot_model = { sl_version : nat; sl_listed : bool;
                    sl_action : nat option; sl_has_dropped_cb : bool;
                    sl_dropped_runs : nat; sl_scope : nat }

type queue_model = { q_slots : slot_model list; q_pending : nat list;
                     q_disposed : bool; q_scheduled : bool;
                     q_generation : nat; q_tickets : nat list;
                     q_committed : nat list; q_errors : (nat, nat) prod list }

val init_queue : queue_model

val set_slot : nat -> slot_model -> slot_model list -> slot_model list

val q_request : queue_model -> queue_model

val q_reserve : nat -> bool -> queue_model -> (queue_model, nat) prod

val q_cancel : nat -> queue_model -> queue_model

val q_enqueue : nat -> nat -> queue_model -> queue_model

val snapshot_and_clear :
  nat list -> slot_model list -> (((nat, nat) prod, nat option) prod list,
  slot_model list) prod

val apply_reenqueues :
  nat -> ((nat, nat) prod, nat) prod list -> slot_model list -> nat list ->
  (slot_model list, nat list) prod

val cancel_all_pending : nat list -> slot_model list -> slot_model list

val q_dispose : queue_model -> queue_model

val report_by_scope_aux :
  nat list -> (nat, nat) prod list -> (nat, nat list) prod list

val report_by_scope : (nat, nat) prod list -> (nat, nat list) prod list

type owq_admission =
| AdmAccepted
| AdmFull
| AdmClosed

type owq_model = { owq_items : nat list; owq_stopped : bool;
                   owq_has_wake : bool; owq_wakes : nat; owq_accepted : 
                   nat; owq_dequeued : nat; owq_rejected : nat;
                   owq_discarded : nat }

val init_owq : owq_model

val owq_try_enqueue :
  nat -> nat -> owq_model -> (owq_admission, owq_model) prod

val owq_try_dequeue : owq_model -> (nat option, owq_model) prod

val owq_set_wake : bool -> owq_model -> owq_model

val owq_stop : owq_model -> owq_model

type fence_poll =
| PollNotReady
| PollReady
| PollGpuFailure

type fence_outcome =
| FenceOk of bool
| FenceError

type fence_model = { cf_pending : nat option; cf_failed : bool;
                     cf_next_marker : nat; cf_released : nat list }

val init_fence : fence_model

val cf_try_complete :
  fence_poll -> fence_model -> (fence_outcome, fence_model) prod

val cf_reset : fence_model -> (fence_outcome, fence_model) prod

type work_entry = ((nat, nat) prod, nat option) prod

val overwrite_prefix :
  work_entry list -> work_entry option list -> work_entry option list

val exec_buf_prefix :
  nat -> commit_failure -> (nat -> bool) -> ((nat, nat) prod, nat) prod list
  -> bool -> work_entry option list -> slot_model list -> nat list -> nat
  list -> (nat, nat) prod list -> ((((work_entry option list, slot_model
  list) prod, nat list) prod, nat list) prod, (nat, nat) prod list) prod

val q_flush_reenq_reusable :
  commit_failure -> (nat -> bool) -> ((nat, nat) prod, nat) prod list ->
  work_entry option list -> queue_model -> (work_entry option list,
  queue_model) prod

type layout_cmd =
| LCmdRegisterExit of nat
| LCmdReconcile of nat list
| LCmdFinishExit of nat
| LCmdReflowBefore
| LCmdReflowPost of nat
| LCmdReflowCancel of nat

type layout_sim = { ls_start : nat; ls_end : nat; ls_dom : nat list;
                    ls_laid_out : nat list; ls_dep : dep_state;
                    ls_reflow : reflow_state; ls_tickets : nat list }

val init_layout_sim : nat list -> layout_sim

val step_layout_cmd : layout_cmd -> layout_sim -> layout_sim

val run_layout_cmds : layout_cmd list -> layout_sim -> layout_sim

type layout_view = { lv_dom : nat list; lv_live : nat list; lv_open : 
                     bool; lv_before : nat; lv_after : nat }

val observe_layout : nat list -> layout_cmd list -> layout_view

type hyd_int_op =
| HAdd of z
| HScope of hyd_int_op list

val run_hyd_tag_op : hyd_int_op -> z -> (z, z cursor_tag_entry) prod

val run_hyd_tag_ops :
  hyd_int_op list -> z -> (z, z cursor_tag_entry list) prod

val observe_cursor_rollback :
  z -> hyd_int_op list -> bool -> ((z, z) prod, nat) prod

type bnd_cmd =
| BCmdExpect
| BCmdStep of nat * read_outcome
| BCmdDispose of nat
| BCmdEndSetup
| BCmdReveal
| BCmdRetry of nat

type bnd_sim = { bsim_bm : boundary_model; bsim_slots : bstate list }

val init_bnd_sim : bool -> bnd_sim

val step_bnd_cmd : bnd_cmd -> bnd_sim -> bnd_sim

val run_bnd_cmds : bnd_cmd list -> bnd_sim -> bnd_sim

val observe_boundary : bool -> bnd_cmd list -> boundary_model

type pl_cmd =
| PCmdEnter
| PCmdLeave
| PCmdAdd of nat
| PCmdReleaseChild of nat
| PCmdPlace
| PCmdDiscard

val run_pl_cmds :
  pl_cmd list -> nat list -> placement_model -> placement_model

type pl_view = { pv_held : nat; pv_executed : nat list;
                 pv_to_parent : nat list }

val observe_placement : bool -> pl_cmd list -> pl_view

type q_cmd =
| QCmdReserve of nat * bool
| QCmdEnqueue of nat * nat
| QCmdCancel of nat
| QCmdFlush of commit_failure * nat list
| QCmdFlushReenq of commit_failure * nat list
   * ((nat, nat) prod, nat) prod list
| QCmdRunTicket of nat * commit_failure * nat list
| QCmdDispose

val step_q_cmd :
  q_cmd -> work_entry option list -> queue_model -> (work_entry option list,
  queue_model) prod

val run_q_cmds :
  q_cmd list -> work_entry option list -> queue_model -> queue_model

type q_view = { qv_committed : nat list; qv_dropped : nat list;
                qv_reported : (nat, nat list) prod list;
                qv_pending_len : nat; qv_disposed : bool }

val observe_queue : q_cmd list -> q_view

type owq_cmd =
| OWQEnqueue of nat * nat
| OWQDequeue
| OWQSetWake of bool
| OWQStop

val run_owq_cmds :
  owq_cmd list -> nat list -> owq_model -> (nat list, owq_model) prod

type owq_view = { ov_dequeued_items : nat list; ov_count : nat;
                  ov_wakes : nat; ov_rejected : nat; ov_discarded : nat;
                  ov_stopped : bool }

val observe_owq : owq_cmd list -> owq_view

type cf_cmd =
| CFTryComplete of fence_poll
| CFReset

val run_cf_cmds :
  cf_cmd list -> fence_outcome list -> fence_model -> (fence_outcome list,
  fence_model) prod

val observe_fence :
  cf_cmd list -> ((fence_outcome list, nat list) prod, bool) prod
