From stdpp Require Import prelude.
From Stdlib Require Extraction.
From Stdlib Require Import Arith List Bool ZArith.
From RanvierView.Pure Require Import Layout Hydration Lifetime Scheduling.
Import ListNotations.

Local Arguments Nat.eqb : simpl never.

(** * Executable runners for conformance and differential testing *)

(** ** 1. Layout + Departures runner *)

Inductive layout_cmd :=
  | LCmdRegisterExit (node : nat)
  | LCmdReconcile (next_rows : list nat)
  | LCmdFinishExit (node : nat)
  | LCmdReflowBefore
  | LCmdReflowPost (ticket : nat)
  | LCmdReflowCancel (ticket : nat).

Record layout_sim := {
  ls_start : nat;
  ls_end : nat;
  ls_dom : list nat;
  ls_laid_out : list nat;
  ls_dep : dep_state;
  ls_reflow : reflow_state;
  ls_tickets : list nat
}.

Definition init_layout_sim (init_rows : list nat) : layout_sim :=
  {| ls_start := 1000;
     ls_end := 2000;
     ls_dom := 1000 :: init_rows ++ [2000];
     ls_laid_out := init_rows;
     ls_dep := {| dep_exits := []; dep_leaving := []; dep_pending := [] |};
     ls_reflow := {| rf_open := false; rf_posts := 0; rf_before_runs := 0; rf_after_runs := 0 |};
     ls_tickets := [] |}.

Definition step_layout_cmd (c : layout_cmd) (s : layout_sim) : layout_sim :=
  match c with
  | LCmdRegisterExit n =>
    let ex' := if mem_nat n (dep_exits (ls_dep s)) then dep_exits (ls_dep s) else n :: dep_exits (ls_dep s) in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := ls_dom s;
       ls_laid_out := ls_laid_out s;
       ls_dep := {| dep_exits := ex';
                    dep_leaving := dep_leaving (ls_dep s);
                    dep_pending := remove_node n (dep_pending (ls_dep s)) |};
       ls_reflow := ls_reflow s;
       ls_tickets := ls_tickets s |}
  | LCmdReconcile next_rows =>
    let removed := List.filter (fun r => negb (mem_nat r next_rows)) (ls_laid_out s) in
    let cleared := match next_rows with [] => true | _ => false end in
    let '(dep1, dom1) := flush_released cleared (ls_start s) (ls_end s) removed (ls_dep s) (ls_dom s) in
    let dom2 := row_layout_fast (ls_laid_out s) next_rows (ls_end s) dom1 in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := dom2;
       ls_laid_out := next_rows;
       ls_dep := dep1;
       ls_reflow := ls_reflow s;
       ls_tickets := ls_tickets s |}
  | LCmdFinishExit n =>
    let '(dep1, dom1) := finish_exit n (ls_dep s) (ls_dom s) in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := dom1;
       ls_laid_out := ls_laid_out s;
       ls_dep := dep1;
       ls_reflow := ls_reflow s;
       ls_tickets := ls_tickets s |}
  | LCmdReflowBefore =>
    let '(rf1, t) := reflow_before (ls_reflow s) in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := ls_dom s;
       ls_laid_out := ls_laid_out s;
       ls_dep := ls_dep s;
       ls_reflow := rf1;
       ls_tickets := ls_tickets s ++ [t] |}
  | LCmdReflowPost idx =>
    let ticket := nth idx (ls_tickets s) 0 in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := ls_dom s;
       ls_laid_out := ls_laid_out s;
       ls_dep := ls_dep s;
       ls_reflow := reflow_run_post ticket (ls_reflow s);
       ls_tickets := ls_tickets s |}
  | LCmdReflowCancel idx =>
    let ticket := nth idx (ls_tickets s) 0 in
    {| ls_start := ls_start s;
       ls_end := ls_end s;
       ls_dom := ls_dom s;
       ls_laid_out := ls_laid_out s;
       ls_dep := ls_dep s;
       ls_reflow := reflow_abort_post ticket (ls_reflow s);
       ls_tickets := ls_tickets s |}
  end.

Fixpoint run_layout_cmds (cs : list layout_cmd) (s : layout_sim) : layout_sim :=
  match cs with
  | [] => s
  | c :: rest => run_layout_cmds rest (step_layout_cmd c s)
  end.

Record layout_view := {
  lv_dom : list nat;
  lv_live : list nat;
  lv_open : bool;
  lv_before : nat;
  lv_after : nat
}.

Definition observe_layout (init_rows : list nat) (cs : list layout_cmd) : layout_view :=
  let s := run_layout_cmds cs (init_layout_sim init_rows) in
  {| lv_dom := between_Exclusive (ls_start s) (ls_end s) (ls_dom s);
     lv_live := filter_live (dep_leaving (ls_dep s))
                  (between_Exclusive (ls_start s) (ls_end s) (ls_dom s));
     lv_open := rf_open (ls_reflow s);
     lv_before := rf_before_runs (ls_reflow s);
     lv_after := rf_after_runs (ls_reflow s) |}.

(** ** 2. Hydration + Boundary runner *)

Inductive hyd_int_op :=
  | HAdd (delta : Z)
  | HScope (sub : list hyd_int_op).

Fixpoint to_hydration_op (op : hyd_int_op) : hydration_op Z :=
  let fix conv_list (ops : list hyd_int_op) : list (hydration_op Z) :=
    match ops with
    | [] => []
    | h :: t => to_hydration_op h :: conv_list t
    end in
  match op with
  | HAdd d => HydEdit (fun x => (x + d)%Z) (fun x => (x - d)%Z)
  | HScope sub => HydScope (conv_list sub)
  end.

Definition observe_cursor_rollback (s0 : Z) (ops : list hyd_int_op) (do_commit : bool)
    : Z * Z * nat :=
  let hops := map to_hydration_op ops in
  let '(s_mid, log) := run_hydration_ops hops s0 in
  let log' := if do_commit then commit_entries log else log in
  let held := held_entries log' in
  let s_after := if do_commit then s_mid else restore_entries log' s_mid in
  (s_mid, s_after, held).

Inductive bnd_cmd :=
  | BCmdExpect
  | BCmdStep (idx : nat) (out : read_outcome)
  | BCmdDispose (idx : nat)
  | BCmdEndSetup
  | BCmdReveal
  | BCmdRetry (gen : nat).

Record bnd_sim := {
  bsim_bm : boundary_model;
  bsim_slots : list bstate
}.

Definition init_bnd_sim (tracks : bool) : bnd_sim :=
  {| bsim_bm := {| bm_tracks := tracks;
                   bm_waiting := 0;
                   bm_setting_up := true;
                   bm_revealed := false;
                   bm_failed := false;
                   bm_pending_sig := false;
                   bm_wakes := 0;
                   bm_clear_calls := 0;
                   bm_fail_calls := 0;
                   bm_gen := 0 |};
     bsim_slots := [] |}.

Definition step_bnd_cmd (c : bnd_cmd) (s : bnd_sim) : bnd_sim :=
  let bm := bsim_bm s in
  match c with
  | BCmdExpect =>
    let '(bm', b) := bs_expect bm in
    {| bsim_bm := bm'; bsim_slots := bsim_slots s ++ [b] |}
  | BCmdStep idx out =>
    match nth_error (bsim_slots s) idx with
    | None => s
    | Some b =>
      let '(bm', b') := binding_step out bm b in
      {| bsim_bm := bm'; bsim_slots := update_binding idx b' (bsim_slots s) |}
    end
  | BCmdDispose idx =>
    match nth_error (bsim_slots s) idx with
    | None => s
    | Some b =>
      let '(bm', b') := binding_dispose bm b in
      {| bsim_bm := bm'; bsim_slots := update_binding idx b' (bsim_slots s) |}
    end
  | BCmdEndSetup =>
    {| bsim_bm := {| bm_tracks := bm_tracks bm;
                     bm_waiting := bm_waiting bm;
                     bm_setting_up := false;
                     bm_revealed := bm_revealed bm;
                     bm_failed := bm_failed bm;
                     bm_pending_sig := bm_pending_sig bm;
                     bm_wakes := bm_wakes bm;
                     bm_clear_calls := bm_clear_calls bm;
                     bm_fail_calls := bm_fail_calls bm;
                     bm_gen := bm_gen bm |};
       bsim_slots := bsim_slots s |}
  | BCmdReveal =>
    if Nat.eqb (bm_waiting bm) 0 && negb (bm_failed bm) then
      {| bsim_bm := {| bm_tracks := bm_tracks bm;
                       bm_waiting := 0;
                       bm_setting_up := false;
                       bm_revealed := true;
                       bm_failed := false;
                       bm_pending_sig := false;
                       bm_wakes := bm_wakes bm;
                       bm_clear_calls := bm_clear_calls bm;
                       bm_fail_calls := bm_fail_calls bm;
                       bm_gen := bm_gen bm |};
         bsim_slots := bsim_slots s |}
    else s
  | BCmdRetry gen =>
    let bm' := bm_retry gen bm in
    if negb (bm_failed bm') && bm_failed bm then
      {| bsim_bm := bm'; bsim_slots := [] |}
    else
      {| bsim_bm := bm'; bsim_slots := bsim_slots s |}
  end.

Fixpoint run_bnd_cmds (cs : list bnd_cmd) (s : bnd_sim) : bnd_sim :=
  match cs with
  | [] => s
  | c :: rest => run_bnd_cmds rest (step_bnd_cmd c s)
  end.

Definition observe_boundary (tracks : bool) (cs : list bnd_cmd) : boundary_model :=
  bsim_bm (run_bnd_cmds cs (init_bnd_sim tracks)).

(** ** 3. Placement + MountLifetime runner *)

Inductive pl_cmd :=
  | PCmdEnter
  | PCmdLeave
  | PCmdAdd (hook : nat)
  | PCmdReleaseChild (hook : nat)
  | PCmdPlace
  | PCmdDiscard.

Fixpoint run_pl_cmds (cs : list pl_cmd) (stack : list nat) (p : placement_model) : placement_model :=
  match cs with
  | [] => p
  | c :: rest =>
    match c with
    | PCmdEnter =>
      let '(p', prev) := pl_enter p in
      run_pl_cmds rest (prev :: stack) p'
    | PCmdLeave =>
      match stack with
      | [] => run_pl_cmds rest [] p
      | prev :: st' => run_pl_cmds rest st' (pl_leave prev p)
      end
    | PCmdAdd h => run_pl_cmds rest stack (pl_add h p)
    | PCmdReleaseChild h => run_pl_cmds rest stack (pl_enqueue h true p)
    | PCmdPlace => run_pl_cmds rest stack (pl_place_fast p)
    | PCmdDiscard => run_pl_cmds rest stack (pl_discard p)
    end
  end.

Record pl_view := {
  pv_held : nat;
  pv_executed : list nat;
  pv_to_parent : list nat
}.

Definition observe_placement (parent_placed : bool) (cs : list pl_cmd) : pl_view :=
  let p := run_pl_cmds cs [] (init_placement parent_placed) in
  {| pv_held := pl_held_count p;
     pv_executed := pl_executed p;
     pv_to_parent := pl_to_parent p |}.

(** ** 4. Scheduling.Queue + OwnerWorkQueue + CompletionFence runners *)

Inductive q_cmd :=
  | QCmdReserve (scope : nat) (has_dropped : bool)
  | QCmdEnqueue (slot_idx action : nat)
  | QCmdCancel (slot_idx : nat)
  | QCmdFlush (mode : commit_failure) (failing : list nat)
  | QCmdFlushReenq (mode : commit_failure) (failing : list nat) (reenq : list (nat * nat * nat))
  | QCmdRunTicket (ticket_idx : nat) (mode : commit_failure) (failing : list nat)
  | QCmdDispose.

Definition step_q_cmd (c : q_cmd) (q : queue_model) : queue_model :=
  match c with
  | QCmdReserve sc hd => if q_disposed q then q else fst (q_reserve sc hd q)
  | QCmdEnqueue idx act => q_enqueue idx act q
  | QCmdCancel idx => q_cancel idx q
  | QCmdFlush mode failing =>
    q_flush mode (fun a => existsb (Nat.eqb a) failing) q
  | QCmdFlushReenq mode failing reenq =>
    q_flush_reenq mode (fun a => existsb (Nat.eqb a) failing) reenq q
  | QCmdRunTicket tidx mode failing =>
    match nth_error (q_tickets q) tidx with
    | None => q
    | Some t => q_run_posted_ticket t mode (fun a => existsb (Nat.eqb a) failing) q
    end
  | QCmdDispose => q_dispose q
  end.

Fixpoint run_q_cmds (cs : list q_cmd) (q : queue_model) : queue_model :=
  match cs with
  | [] => q
  | c :: rest => run_q_cmds rest (step_q_cmd c q)
  end.

Record q_view := {
  qv_committed : list nat;
  qv_dropped : list nat;
  qv_reported : list (nat * list nat);
  qv_pending_len : nat;
  qv_disposed : bool
}.

Definition observe_queue (cs : list q_cmd) : q_view :=
  let q := run_q_cmds cs init_queue in
  {| qv_committed := q_committed q;
     qv_dropped := map sl_dropped_runs (q_slots q);
     qv_reported := report_by_scope (q_errors q);
     qv_pending_len := length (q_pending q);
     qv_disposed := q_disposed q |}.

Inductive owq_cmd :=
  | OWQEnqueue (item cap : nat)
  | OWQDequeue
  | OWQSetWake (has_cb : bool)
  | OWQStop.

Fixpoint run_owq_cmds (cs : list owq_cmd) (deq_out : list nat) (q : owq_model) : list nat * owq_model :=
  match cs with
  | [] => (deq_out, q)
  | c :: rest =>
    match c with
    | OWQEnqueue item cap => run_owq_cmds rest deq_out (snd (owq_try_enqueue item cap q))
    | OWQDequeue =>
      let '(res, q') := owq_try_dequeue q in
      let out' := match res with Some x => deq_out ++ [x] | None => deq_out end in
      run_owq_cmds rest out' q'
    | OWQSetWake cb => run_owq_cmds rest deq_out (owq_set_wake cb q)
    | OWQStop => run_owq_cmds rest deq_out (owq_stop q)
    end
  end.

Record owq_view := {
  ov_dequeued_items : list nat;
  ov_count : nat;
  ov_wakes : nat;
  ov_rejected : nat;
  ov_discarded : nat;
  ov_stopped : bool
}.

Definition observe_owq (cs : list owq_cmd) : owq_view :=
  let '(out, q) := run_owq_cmds cs [] init_owq in
  {| ov_dequeued_items := out;
     ov_count := length (owq_items q);
     ov_wakes := owq_wakes q;
     ov_rejected := owq_rejected q;
     ov_discarded := owq_discarded q;
     ov_stopped := owq_stopped q |}.

Inductive cf_cmd :=
  | CFTryComplete (poll : fence_poll)
  | CFReset.

Fixpoint run_cf_cmds (cs : list cf_cmd) (outs : list fence_outcome) (f : fence_model)
    : list fence_outcome * fence_model :=
  match cs with
  | [] => (outs, f)
  | c :: rest =>
    let '(o, f') :=
      match c with
      | CFTryComplete poll => cf_try_complete poll f
      | CFReset => cf_reset f
      end in
    run_cf_cmds rest (outs ++ [o]) f'
  end.

Definition observe_fence (cs : list cf_cmd) : list fence_outcome * list nat * bool :=
  let '(outs, f) := run_cf_cmds cs [] init_fence in
  (outs, cf_released f, cf_failed f).

(** ** OCaml Extraction *)

Extraction Language OCaml.
Set Extraction Output Directory ".".

Extraction "engine.ml"
  logical_next
  flatten_tree
  observe_layout
  observe_cursor_rollback
  observe_boundary
  observe_placement
  dispose_lifetime_tree
  teardown_window_indices
  observe_queue
  observe_owq
  observe_fence.
