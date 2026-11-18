From stdpp Require Import prelude.
From Stdlib Require Import Arith Lia List Bool.
Import ListNotations.

Local Arguments Nat.eqb : simpl never.

(**md # SSR marker balancing, nested `HydrationCursor` rollback, and `ViewBoundary` readiness

Models the server-marker scanner and nested rollback cursor in `Ranvier.View.Dom.TemplateDom` (`matchingEnd`, `logicalNext`, `HydrationCursor`), `Ranvier.View.Dom.Hydration` (`hydrateWith`), `Ranvier.View.Scope` (`BindingState`), and `Ranvier.View.Dom.Mounting` (`ViewBoundary`). *)

(** ** SSR comment marker bracket matching *)

Inductive ssr_token :=
  | TokStart (tag : nat)
  | TokEnd
  | TokNode (id : nat).

Fixpoint scan_matching_end (depth : nat) (xs : list ssr_token) : option (list ssr_token) :=
  match xs with
  | [] => None
  | TokEnd :: rest =>
    match depth with
    | 0 => Some (TokEnd :: rest)
    | S d => scan_matching_end d rest
    end
  | TokStart _ :: rest => scan_matching_end (S depth) rest
  | TokNode _ :: rest => scan_matching_end depth rest
  end.

Definition logical_next (xs : list ssr_token) : option (list ssr_token) :=
  match xs with
  | [] => None
  | TokStart _ :: rest => scan_matching_end 0 rest
  | _ :: rest => Some rest
  end.

(** Well-bracketed SSR trees produced by [Ranvier.View.Server]. *)
Inductive ssr_tree :=
  | SNode (id : nat)
  | SRegion (tag : nat) (children : list ssr_tree).

Fixpoint flatten_tree (t : ssr_tree) : list ssr_token :=
  let fix flatten_list (ts : list ssr_tree) : list ssr_token :=
    match ts with
    | [] => []
    | c :: cs => flatten_tree c ++ flatten_list cs
    end in
  match t with
  | SNode id => [TokNode id]
  | SRegion tag children => TokStart tag :: flatten_list children ++ [TokEnd]
  end.

Fixpoint flatten_trees (ts : list ssr_tree) : list ssr_token :=
  match ts with
  | [] => []
  | t :: rest => flatten_tree t ++ flatten_trees rest
  end.

Lemma flatten_tree_unfold tag children :
  flatten_tree (SRegion tag children) = TokStart tag :: flatten_trees children ++ [TokEnd].
Proof. reflexivity. Qed.

Fixpoint tree_size (t : ssr_tree) : nat :=
  let fix list_size (ts : list ssr_tree) : nat :=
    match ts with
    | [] => 0
    | c :: cs => S (tree_size c + list_size cs)
    end in
  match t with
  | SNode _ => 1
  | SRegion _ cs => S (list_size cs)
  end.

Fixpoint trees_size (ts : list ssr_tree) : nat :=
  match ts with
  | [] => 0
  | c :: cs => S (tree_size c + trees_size cs)
  end.

Lemma tree_region_size tag cs :
  tree_size (SRegion tag cs) = S (trees_size cs).
Proof. reflexivity. Qed.

Lemma scan_past_trees_sized bound :
  (forall t, tree_size t <= bound ->
     forall depth suf, scan_matching_end depth (flatten_tree t ++ suf) = scan_matching_end depth suf) /\
  (forall ts, trees_size ts <= bound ->
     forall depth suf, scan_matching_end depth (flatten_trees ts ++ suf) = scan_matching_end depth suf).
Proof.
  induction bound as [|b [IHt IHts]].
  - split.
    + intros [id | tag cs] Hsz; [simpl in Hsz; lia | rewrite tree_region_size in Hsz; lia].
    + intros [|c cs] Hsz; [reflexivity | simpl in Hsz; lia].
  - split.
    + intros [id | tag cs] Hsz depth suf.
      * reflexivity.
      * rewrite tree_region_size in Hsz.
        rewrite flatten_tree_unfold; simpl. rewrite <- app_assoc; simpl.
        rewrite IHts by lia. reflexivity.
    + intros [|c cs] Hsz depth suf; [reflexivity|].
      simpl in Hsz |- *. rewrite <- app_assoc.
      rewrite IHt by lia. apply IHts; lia.
Qed.

(**md
**Scanning from depth 0 past any well-bracketed forest lands on the enclosing closing marker.**

`scan_matching_end` (`TemplateDom.matchingEnd`) skips arbitrarily nested `TokStart` / `TokEnd` child regions inside `children` and returns `Some (TokEnd :: suf)` without overshooting into `suf`.

- [children:] the well-bracketed child SSR forest inside the region.
- [suf:] the trailing sibling tokens after the region's closing `TokEnd`.
*)
Theorem matching_end_well_bracketed children suf :
  scan_matching_end 0 (flatten_trees children ++ TokEnd :: suf) = Some (TokEnd :: suf).
Proof.
  destruct (scan_past_trees_sized (trees_size children)) as [_ Hts].
  rewrite (Hts children (Nat.le_refl _) 0 (TokEnd :: suf)).
  reflexivity.
Qed.

(**md
**`logicalNext` on an SSR start marker skips the entire bracketed region to its closing marker.**

Given a flattened `SRegion tag children` followed by `suf`, `logical_next` (`TemplateDom.logicalNext`) returns `Some (TokEnd :: suf)`.

- [tag:] the marker kind tag of the opening `<!--[...-->` comment.
- [children:] the nested SSR nodes and sub-regions enclosed by the region.
- [suf:] the sibling tokens following the region's closing `<!--]-->` comment.
*)
Theorem logical_next_skips_region tag children suf :
  logical_next (flatten_tree (SRegion tag children) ++ suf) = Some (TokEnd :: suf).
Proof.
  rewrite flatten_tree_unfold; simpl. rewrite <- app_assoc; simpl.
  apply matching_end_well_bracketed.
Qed.

(** ** Hierarchical `HydrationCursor` LIFO rollback and commit *)

Inductive cursor_entry (St : Type) :=
  | UndoStep (inv : St -> St)
  | NestedCursor (entries : list (cursor_entry St)).

Arguments UndoStep {St} _.
Arguments NestedCursor {St} _.

Fixpoint restore_entry {St : Type} (e : cursor_entry St) (st : St) : St :=
  let fix restore_list (es : list (cursor_entry St)) (acc : St) : St :=
    match es with
    | [] => acc
    | hd :: tl => restore_entry hd (restore_list tl acc)
    end in
  match e with
  | UndoStep inv => inv st
  | NestedCursor child => restore_list child st
  end.

Fixpoint restore_entries {St : Type} (es : list (cursor_entry St)) (st : St) : St :=
  match es with
  | [] => st
  | e :: rest => restore_entry e (restore_entries rest st)
  end.

Fixpoint held_entry {St : Type} (e : cursor_entry St) : nat :=
  let fix held_list (es : list (cursor_entry St)) : nat :=
    match es with
    | [] => 0
    | hd :: tl => held_entry hd + held_list tl
    end in
  match e with
  | UndoStep _ => 1
  | NestedCursor child => S (held_list child)
  end.

Fixpoint held_entries {St : Type} (es : list (cursor_entry St)) : nat :=
  match es with
  | [] => 0
  | e :: rest => held_entry e + held_entries rest
  end.

Definition commit_entries {St : Type} (_ : list (cursor_entry St)) : list (cursor_entry St) := [].

(** A reversible hydration transcript over server state [St] (DOM + DocumentHead). *)
Inductive hydration_op (St : Type) :=
  | HydEdit (fwd inv : St -> St)
  | HydScope (sub : list (hydration_op St)).

Arguments HydEdit {St} _ _.
Arguments HydScope {St} _.

Fixpoint run_hydration_op {St : Type} (op : hydration_op St) (st : St) : St * cursor_entry St :=
  let fix run_list (ops : list (hydration_op St)) (acc : St) : St * list (cursor_entry St) :=
    match ops with
    | [] => (acc, [])
    | hd :: tl =>
      let '(acc1, e_hd) := run_hydration_op hd acc in
      let '(acc2, es_tl) := run_list tl acc1 in
      (acc2, e_hd :: es_tl)
    end in
  match op with
  | HydEdit fwd inv => (fwd st, UndoStep inv)
  | HydScope sub =>
    let '(st1, child_log) := run_list sub st in
    (st1, NestedCursor child_log)
  end.

Fixpoint run_hydration_ops {St : Type} (ops : list (hydration_op St)) (st : St)
    : St * list (cursor_entry St) :=
  match ops with
  | [] => (st, [])
  | op :: rest =>
    let '(st1, e) := run_hydration_op op st in
    let '(st2, log_rest) := run_hydration_ops rest st1 in
    (st2, e :: log_rest)
  end.

Fixpoint op_reversible {St : Type} (op : hydration_op St) : Prop :=
  let fix list_rev (ops : list (hydration_op St)) : Prop :=
    match ops with
    | [] => True
    | hd :: tl => op_reversible hd /\ list_rev tl
    end in
  match op with
  | HydEdit fwd inv => forall s, inv (fwd s) = s
  | HydScope sub => list_rev sub
  end.

Fixpoint ops_reversible {St : Type} (ops : list (hydration_op St)) : Prop :=
  match ops with
  | [] => True
  | op :: rest => op_reversible op /\ ops_reversible rest
  end.

Fixpoint op_size {St : Type} (op : hydration_op St) : nat :=
  let fix list_sz (ops : list (hydration_op St)) : nat :=
    match ops with
    | [] => 0
    | hd :: tl => op_size hd + list_sz tl
    end in
  match op with
  | HydEdit _ _ => 1
  | HydScope sub => S (list_sz sub)
  end.

Fixpoint ops_size {St : Type} (ops : list (hydration_op St)) : nat :=
  match ops with
  | [] => 0
  | op :: rest => op_size op + ops_size rest
  end.

Lemma restore_entry_nested {St : Type} (child : list (cursor_entry St)) (st : St) :
  restore_entry (NestedCursor child) st = restore_entries child st.
Proof.
  simpl. revert st. induction child as [|e es IH]; simpl; [reflexivity|].
  intros st; rewrite IH; reflexivity.
Qed.

Lemma run_op_scope {St : Type} (sub : list (hydration_op St)) (st : St) :
  run_hydration_op (HydScope sub) st =
    let '(st1, child_log) := run_hydration_ops sub st in (st1, NestedCursor child_log).
Proof.
  simpl.
  assert (Hrun : (fix run_list (ops : list (hydration_op St)) (acc : St) {struct ops} : St * list (cursor_entry St) :=
                    match ops with
                    | [] => (acc, [])
                    | hd :: tl =>
                        let '(acc1, e_hd) := run_hydration_op hd acc in
                        let '(acc2, es_tl) := run_list tl acc1 in (acc2, e_hd :: es_tl)
                    end) sub st = run_hydration_ops sub st).
  { revert st. induction sub as [|h t IH]; simpl; [reflexivity|].
    intros st. destruct (run_hydration_op h st) as [st1 eh].
    rewrite IH. reflexivity. }
  rewrite Hrun. reflexivity.
Qed.

Lemma op_rev_scope {St : Type} (sub : list (hydration_op St)) :
  op_reversible (HydScope sub) -> ops_reversible sub.
Proof.
  simpl. induction sub as [|h t IH]; simpl; [trivial | intros [H1 H2]; split; [exact H1 | exact (IH H2)]].
Qed.

Lemma op_size_scope {St : Type} (sub : list (hydration_op St)) :
  op_size (HydScope sub) = S (ops_size sub).
Proof.
  simpl. apply eq_S. induction sub as [|h t IH]; simpl; [reflexivity | lia].
Qed.

(**md
**Restoring a nested `HydrationCursor` log exactly inverts all server DOM and head edits.**

When every recorded `HydEdit` carries its left inverse (`ops_reversible`), `restore_entries` (`HydrationCursor.Restore`) unwinds arbitrarily nested child cursors in LIFO order back to the initial state `s0`.

- [St:] the state type over which server DOM and `DocumentHead` edits operate.
- [ops:] the nested hydration edit transcript executed before failure.
- [s0:] the initial state before hydration began.
*)
Theorem hydration_cursor_restore_exact_inverse {St : Type} (ops : list (hydration_op St)) (s0 : St) :
  ops_reversible ops ->
  let '(s_end, log) := run_hydration_ops ops s0 in
  restore_entries log s_end = s0.
Proof.
  remember (ops_size ops) as sz eqn:Hsz.
  assert (Hle : ops_size ops <= sz) by lia; clear Hsz.
  revert ops s0 Hle.
  induction sz as [|n IH]; intros ops s0 Hle Hrev.
  - destruct ops as [|[fwd inv | sub] rest]; [reflexivity | simpl in Hle; lia | simpl in Hle; lia].
  - destruct ops as [|op rest]; [reflexivity|].
    destruct op as [fwd inv | sub].
    + simpl in Hle, Hrev |- *. destruct Hrev as [Hinv Hrev_rest].
      specialize (IH rest (fwd s0) ltac:(lia) Hrev_rest).
      destruct (run_hydration_ops rest (fwd s0)) as [s2 log_rest]; simpl.
      rewrite IH, Hinv. reflexivity.
    + assert (Hsz : S (ops_size sub) + ops_size rest <= S n)
        by (change (ops_size (HydScope sub :: rest)) with (op_size (HydScope sub) + ops_size rest) in Hle;
            rewrite op_size_scope in Hle; exact Hle).
      simpl in Hrev; destruct Hrev as [Hrev_sub Hrev_rest].
      apply op_rev_scope in Hrev_sub.
      change (run_hydration_ops (HydScope sub :: rest) s0) with
        (let '(st1, e) := run_hydration_op (HydScope sub) s0 in
         let '(st2, log_rest) := run_hydration_ops rest st1 in
         (st2, e :: log_rest)).
      rewrite run_op_scope.
      pose proof (IH sub s0 ltac:(lia) Hrev_sub) as IH_sub.
      destruct (run_hydration_ops sub s0) as [s1 child_log].
      pose proof (IH rest s1 ltac:(lia) Hrev_rest) as IH_rest.
      destruct (run_hydration_ops rest s1) as [s2 log_rest].
      change (restore_entries (NestedCursor child_log :: log_rest) s2) with
        (restore_entry (NestedCursor child_log) (restore_entries log_rest s2)).
      rewrite restore_entry_nested, IH_rest, IH_sub.
      reflexivity.
Qed.

(**md
**Committing a `HydrationCursor` drops every held undo closure and nested cursor.**

`commit_entries` (`HydrationCursor.Commit`) reduces `HydrationCounters.held` (`held_entries`) to `0`.

- [St:] the state type over which undo entries operate.
- [es:] the undo and nested-cursor entries accumulated during hydration.
*)
Theorem hydration_cursor_commit_clears_held {St : Type} (es : list (cursor_entry St)) :
  held_entries (commit_entries es) = 0.
Proof.
  reflexivity.
Qed.

(** ** `BindingState` and `ViewBoundary` readiness invariants *)

Record bstate := {
  bs_failed : bool;
  bs_waiting : bool
}.

Inductive read_outcome :=
  | OutValue
  | OutNotReady
  | OutError.

Record boundary_model := {
  bm_tracks : bool;
  bm_waiting : nat;
  bm_setting_up : bool;
  bm_revealed : bool;
  bm_failed : bool;
  bm_pending_sig : bool;
  bm_wakes : nat;
  bm_clear_calls : nat;
  bm_fail_calls : nat;
  bm_gen : nat
}.

Definition bs_expect (bm : boundary_model) : boundary_model * bstate :=
  if bm_tracks bm && negb (bm_revealed bm || bm_failed bm) then
    ({| bm_tracks := bm_tracks bm;
        bm_waiting := S (bm_waiting bm);
        bm_setting_up := bm_setting_up bm;
        bm_revealed := bm_revealed bm;
        bm_failed := bm_failed bm;
        bm_pending_sig := bm_pending_sig bm;
        bm_wakes := bm_wakes bm;
        bm_clear_calls := bm_clear_calls bm;
        bm_fail_calls := bm_fail_calls bm;
        bm_gen := bm_gen bm |},
     {| bs_failed := false; bs_waiting := true |})
  else
    (bm, {| bs_failed := false; bs_waiting := false |}).

Definition bm_wait (bm : boundary_model) : boundary_model :=
  {| bm_tracks := bm_tracks bm;
     bm_waiting := S (bm_waiting bm);
     bm_setting_up := bm_setting_up bm;
     bm_revealed := bm_revealed bm;
     bm_failed := bm_failed bm;
     bm_pending_sig := if bm_revealed bm then true else bm_pending_sig bm;
     bm_wakes := bm_wakes bm;
     bm_clear_calls := bm_clear_calls bm;
     bm_fail_calls := bm_fail_calls bm;
     bm_gen := bm_gen bm |}.

Definition bm_ready (bm : boundary_model) : boundary_model :=
  match bm_waiting bm with
  | 0 => bm
  | S rem =>
    {| bm_tracks := bm_tracks bm;
       bm_waiting := rem;
       bm_setting_up := bm_setting_up bm;
       bm_revealed := bm_revealed bm;
       bm_failed := bm_failed bm;
       bm_pending_sig := if Nat.eqb rem 0 && bm_revealed bm then false else bm_pending_sig bm;
       bm_wakes := if Nat.eqb rem 0 && negb (bm_revealed bm) && negb (bm_setting_up bm)
                   then S (bm_wakes bm) else bm_wakes bm;
       bm_clear_calls := bm_clear_calls bm;
       bm_fail_calls := bm_fail_calls bm;
       bm_gen := bm_gen bm |}
  end.

Definition bm_fail (bm : boundary_model) : boundary_model :=
  {| bm_tracks := bm_tracks bm;
     bm_waiting := bm_waiting bm;
     bm_setting_up := bm_setting_up bm;
     bm_revealed := bm_revealed bm;
     bm_failed := true;
     bm_pending_sig := bm_pending_sig bm;
     bm_wakes := bm_wakes bm;
     bm_clear_calls := bm_clear_calls bm;
     bm_fail_calls := S (bm_fail_calls bm);
     bm_gen := S (bm_gen bm) |}.

(** Models one binding read transition ([Scope.fs:45-80]):
    - [OutNotReady]: sets [Waiting] & calls [Wait] if not already [Waiting] and [bm_tracks]; never calls [Fail].
    - [OutError]: sets [ReadFailed] & calls [Fail], keeping [Waiting] untouched.
    - [OutValue]: clears [ReadFailed] (incrementing [clear_calls] iff it was set), then clears [Waiting] and
      calls [Ready] iff [Waiting] was set. *)
Definition binding_step (out : read_outcome) (bm : boundary_model) (bs : bstate)
    : boundary_model * bstate :=
  match out with
  | OutNotReady =>
    if negb (bs_waiting bs) && bm_tracks bm then
      (bm_wait bm, {| bs_failed := bs_failed bs; bs_waiting := true |})
    else
      (bm, bs)
  | OutError =>
    (bm_fail bm, {| bs_failed := true; bs_waiting := bs_waiting bs |})
  | OutValue =>
    let bm1 :=
      if bs_failed bs then
        {| bm_tracks := bm_tracks bm;
           bm_waiting := bm_waiting bm;
           bm_setting_up := bm_setting_up bm;
           bm_revealed := bm_revealed bm;
           bm_failed := bm_failed bm;
           bm_pending_sig := bm_pending_sig bm;
           bm_wakes := bm_wakes bm;
           bm_clear_calls := S (bm_clear_calls bm);
           bm_fail_calls := bm_fail_calls bm;
           bm_gen := bm_gen bm |}
      else bm in
    let bm2 := if bs_waiting bs then bm_ready bm1 else bm1 in
    (bm2, {| bs_failed := false; bs_waiting := false |})
  end.

(** Disposing a binding releases its [Waiting] registration once ([Mounting.fs:1925, 1932-1936]). *)
Definition binding_dispose (bm : boundary_model) (bs : bstate) : boundary_model * bstate :=
  let bm' := if bs_waiting bs then bm_ready bm else bm in
  (bm', {| bs_failed := bs_failed bs; bs_waiting := false |}).

Definition bm_retry (attempt_gen : nat) (bm : boundary_model) : boundary_model :=
  if bm_failed bm && Nat.eqb attempt_gen (bm_gen bm) then
    {| bm_tracks := bm_tracks bm;
       bm_waiting := 0;
       bm_setting_up := false;
       bm_revealed := false;
       bm_failed := false;
       bm_pending_sig := false;
       bm_wakes := bm_wakes bm;
       bm_clear_calls := bm_clear_calls bm;
       bm_fail_calls := bm_fail_calls bm;
       bm_gen := bm_gen bm |}
  else bm.

Fixpoint count_waiting (bss : list bstate) : nat :=
  match bss with
  | [] => 0
  | b :: rest => (if bs_waiting b then 1 else 0) + count_waiting rest
  end.

Fixpoint update_binding (idx : nat) (b_new : bstate) (bss : list bstate) : list bstate :=
  match idx, bss with
  | _, [] => []
  | 0, _ :: rest => b_new :: rest
  | S i, b :: rest => b :: update_binding i b_new rest
  end.

Lemma count_waiting_nth_update idx b_old b_new bss :
  nth_error bss idx = Some b_old ->
  (if bs_waiting b_old then 1 else 0) + count_waiting (update_binding idx b_new bss) =
  (if bs_waiting b_new then 1 else 0) + count_waiting bss.
Proof.
  revert idx. induction bss as [|x xs IH]; intros [|i] Hnth; simpl in *; try discriminate.
  - injection Hnth as ->. lia.
  - specialize (IH i Hnth). lia.
Qed.

(**md
**Registering a new binding preserves exact agreement between `bm_waiting` and binding `Waiting` bits.**

`bs_expect` (`BindingState.started` / `ViewScope.Expect`) increments `bm_waiting` if and only if the new binding starts with `bs_waiting = true`.

- [bm:] the `ViewBoundary` state before registering the binding.
- [bss:] the states of the bindings already registered with `bm`.
*)
Theorem binding_expect_preserves_waiting_inv bm bss :
  bm_waiting bm = count_waiting bss ->
  let '(bm', b_new) := bs_expect bm in
  bm_waiting bm' = count_waiting (b_new :: bss).
Proof.
  intros Hinv. unfold bs_expect.
  destruct (bm_tracks bm && negb (bm_revealed bm || bm_failed bm)); simpl; lia.
Qed.

(**md
**Every binding read outcome (`OutValue`, `OutNotReady`, `OutError`) preserves the boundary waiting-count invariant.**

Across `binding_step` (`BindingState.rejected`, `report`, `recovered`, `released`), `bm_waiting` always equals the count of bindings holding `Waiting`.

- [out:] the outcome of evaluating the binding's reader.
- [bm:] the `ViewBoundary` state before the step.
- [bss:] the list of binding states registered under `bm`.
- [idx:] the index in `bss` of the binding that ran.
- [b_old:] the state of binding `idx` before the step.
*)
Theorem binding_step_preserves_waiting_inv out bm bss idx b_old :
  bm_waiting bm = count_waiting bss ->
  nth_error bss idx = Some b_old ->
  let '(bm', b_new) := binding_step out bm b_old in
  bm_waiting bm' = count_waiting (update_binding idx b_new bss).
Proof.
  intros Hinv Hnth.
  pose proof (count_waiting_nth_update idx b_old) as Hupd.
  assert (Hge : bs_waiting b_old = true -> count_waiting bss >= 1).
  { intros Hw. clear -Hnth Hw. revert idx Hnth.
    induction bss as [|x xs IH]; intros [|i] Hnth; simpl in *; try discriminate.
    - injection Hnth as ->; rewrite Hw; lia.
    - specialize (IH i Hnth); lia. }
  unfold binding_step, bm_ready, bm_wait, bm_fail. destruct out.
  - (* OutValue *)
    destruct (bs_failed b_old); destruct (bs_waiting b_old) eqn:Hw; simpl in *.
    + specialize (Hge eq_refl).
      destruct (bm_waiting bm) as [|rem] eqn:Hbmw; [lia|].
      simpl. specialize (Hupd {| bs_failed := false; bs_waiting := false |} bss Hnth).
      simpl in Hupd. lia.
    + specialize (Hupd {| bs_failed := false; bs_waiting := false |} bss Hnth).
      simpl in Hupd. lia.
    + specialize (Hge eq_refl).
      destruct (bm_waiting bm) as [|rem] eqn:Hbmw; [lia|].
      simpl. specialize (Hupd {| bs_failed := false; bs_waiting := false |} bss Hnth).
      simpl in Hupd. lia.
    + specialize (Hupd {| bs_failed := false; bs_waiting := false |} bss Hnth).
      simpl in Hupd. lia.
  - (* OutNotReady *)
    destruct (bs_waiting b_old) eqn:Hw; simpl.
    + specialize (Hupd b_old bss Hnth). rewrite ?Hw in Hupd; simpl in Hupd. lia.
    + destruct (bm_tracks bm); simpl.
      * specialize (Hupd {| bs_failed := bs_failed b_old; bs_waiting := true |} bss Hnth).
        rewrite ?Hw in Hupd; simpl in Hupd. lia.
      * specialize (Hupd b_old bss Hnth). rewrite ?Hw in Hupd; simpl in Hupd. lia.
  - (* OutError *)
    simpl. specialize (Hupd {| bs_failed := true; bs_waiting := bs_waiting b_old |} bss Hnth).
    simpl in Hupd. lia.
Qed.

(**md
**Disposing a binding releases its `Waiting` claim at most once and preserves `bm_waiting = count_waiting`.**

When a waiting binding tears down before resolving (`Mounting.fs` cleanup calling `BindingState.ready`), `binding_dispose` decrements `bm_waiting` once and clears `bs_waiting`.

- [bm:] the `ViewBoundary` state before disposal.
- [bss:] the list of binding states registered under `bm`.
- [idx:] the index in `bss` of the disposed binding.
- [b_old:] the state of binding `idx` before disposal.
*)
Theorem binding_dispose_preserves_waiting_inv bm bss idx b_old :
  bm_waiting bm = count_waiting bss ->
  nth_error bss idx = Some b_old ->
  let '(bm', b_new) := binding_dispose bm b_old in
  bm_waiting bm' = count_waiting (update_binding idx b_new bss).
Proof.
  intros Hinv Hnth.
  pose proof (count_waiting_nth_update idx b_old {| bs_failed := bs_failed b_old; bs_waiting := false |} bss Hnth) as Hupd.
  unfold binding_dispose, bm_ready.
  destruct (bs_waiting b_old) eqn:Hw; simpl in *.
  - assert (Hpos : count_waiting bss >= 1).
    { clear -Hnth Hw. revert idx Hnth.
      induction bss as [|x xs IH]; intros [|i] Hnth; simpl in *; try discriminate;
      [injection Hnth as ->; rewrite Hw; lia | specialize (IH i Hnth); lia]. }
    destruct (bm_waiting bm) as [|rem]; [lia | simpl; lia].
  - lia.
Qed.

(**md
**`NotReadyException` never reports an error, and consecutive `NotReadyException`s on the same binding are idempotent.**

`BindingState.rejected` leaves `bm_fail_calls` unchanged and makes a second `OutNotReady` read a state identity.

- [bm:] the `ViewBoundary` state before the `NotReadyException` reads.
- [bs:] the binding's 2-bit state before the `NotReadyException` reads.
*)
Theorem binding_not_ready_idempotent_and_silent bm bs :
  let '(bm1, bs1) := binding_step OutNotReady bm bs in
  let '(bm2, bs2) := binding_step OutNotReady bm1 bs1 in
  bm_fail_calls bm1 = bm_fail_calls bm /\
  bm2 = bm1 /\ bs2 = bs1.
Proof.
  unfold binding_step.
  destruct (bs_waiting bs) eqn:Hw; destruct (bm_tracks bm) eqn:Htr;
    simpl; rewrite ?Hw, ?Htr; simpl; repeat split; reflexivity.
Qed.

(**md
**A successful read clears `ReadFailed` and invokes `clearReported` if and only if the binding had previously failed.**

`BindingState.recovered` increments `bm_clear_calls` by `1` when `bs_failed` was `true` and by `0` otherwise.

- [bm:] the `ViewBoundary` state before the recovery read.
- [bs:] the binding's 2-bit state before the recovery read.
*)
Theorem binding_recovered_clears_iff_failed bm bs :
  let '(bm', bs') := binding_step OutValue bm bs in
  bs_failed bs' = false /\
  bm_clear_calls bm' = bm_clear_calls bm + (if bs_failed bs then 1 else 0).
Proof.
  unfold binding_step.
  destruct (bs_failed bs); destruct (bs_waiting bs); unfold bm_ready;
    destruct (bm_waiting bm); simpl; (split; [reflexivity | lia]).
Qed.

(**md
**Advancing the boundary generation on failure fences out every retry handle from an earlier failure.**

`bm_fail` increments `bm_gen`, so `bm_retry` invoked with any `attempt_gen <= bm_gen bm` is a no-op (`ViewBoundary` retry generation check in `Mounting.fs`).

- [attempt_gen:] the generation captured when the stale retry callback was created.
- [bm:] the `ViewBoundary` state prior to the newer failure.
*)
Theorem boundary_stale_retry_fenced attempt_gen bm :
  attempt_gen <= bm_gen bm ->
  bm_retry attempt_gen (bm_fail bm) = bm_fail bm.
Proof.
  intros Hle. unfold bm_fail, bm_retry; simpl.
  assert (Hneq : Nat.eqb attempt_gen (S (bm_gen bm)) = false) by (apply Nat.eqb_neq; lia).
  rewrite Hneq; reflexivity.
Qed.

(** ** Proof-backed `HydrationCursor` defunctionalization and lazy `copyServed` *)

(** Defunctionalized `UndoEntry` tags (`TemplateDom.HydrationCursor`), eliminating per-row
    and per-slot closure allocations on separator removal, placeholder replacement,
    RCDATA text insertion, `MountRow` marker removal, and nested cursor registration. *)
Inductive cursor_tag_entry (St : Type) :=
  | TagCustom (inv : St -> St)
  | TagInsertBefore (reinsert : St -> St)
  | TagRemove (detach : St -> St)
  | TagReplace (swap : St -> St)
  | TagNest (child : list (cursor_tag_entry St)).

Arguments TagCustom {St} _.
Arguments TagInsertBefore {St} _.
Arguments TagRemove {St} _.
Arguments TagReplace {St} _.
Arguments TagNest {St} _.

Fixpoint denote_tag_entry {St : Type} (e : cursor_tag_entry St) : cursor_entry St :=
  let fix denote_list (es : list (cursor_tag_entry St)) : list (cursor_entry St) :=
    match es with
    | [] => []
    | hd :: tl => denote_tag_entry hd :: denote_list tl
    end in
  match e with
  | TagCustom inv => UndoStep inv
  | TagInsertBefore reinsert => UndoStep reinsert
  | TagRemove detach => UndoStep detach
  | TagReplace swap => UndoStep swap
  | TagNest child => NestedCursor (denote_list child)
  end.

Fixpoint restore_tag_entry {St : Type} (e : cursor_tag_entry St) (st : St) : St :=
  let fix restore_list (es : list (cursor_tag_entry St)) (acc : St) : St :=
    match es with
    | [] => acc
    | hd :: tl => restore_tag_entry hd (restore_list tl acc)
    end in
  match e with
  | TagCustom inv => inv st
  | TagInsertBefore reinsert => reinsert st
  | TagRemove detach => detach st
  | TagReplace swap => swap st
  | TagNest child => restore_list child st
  end.

Fixpoint restore_tag_entries {St : Type} (es : list (cursor_tag_entry St)) (st : St) : St :=
  match es with
  | [] => st
  | e :: rest => restore_tag_entry e (restore_tag_entries rest st)
  end.

Fixpoint held_tag_entry {St : Type} (e : cursor_tag_entry St) : nat :=
  let fix held_list (es : list (cursor_tag_entry St)) : nat :=
    match es with
    | [] => 0
    | hd :: tl => held_tag_entry hd + held_list tl
    end in
  match e with
  | TagCustom _ | TagInsertBefore _ | TagRemove _ | TagReplace _ => 1
  | TagNest child => S (held_list child)
  end.

Fixpoint held_tag_entries {St : Type} (es : list (cursor_tag_entry St)) : nat :=
  match es with
  | [] => 0
  | e :: rest => held_tag_entry e + held_tag_entries rest
  end.

Definition commit_tag_entries {St : Type} (_ : list (cursor_tag_entry St)) : list (cursor_tag_entry St) := [].

Fixpoint tag_entry_size {St : Type} (e : cursor_tag_entry St) : nat :=
  let fix list_sz (es : list (cursor_tag_entry St)) : nat :=
    match es with
    | [] => 0
    | hd :: tl => S (tag_entry_size hd + list_sz tl)
    end in
  match e with
  | TagCustom _ | TagInsertBefore _ | TagRemove _ | TagReplace _ => 1
  | TagNest child => S (list_sz child)
  end.

Fixpoint tag_entries_size {St : Type} (es : list (cursor_tag_entry St)) : nat :=
  match es with
  | [] => 0
  | e :: rest => S (tag_entry_size e + tag_entries_size rest)
  end.

Lemma tag_entry_size_nest {St : Type} (child : list (cursor_tag_entry St)) :
  tag_entry_size (TagNest child) = S (tag_entries_size child).
Proof.
  simpl. apply eq_S. induction child as [|h t IH]; simpl; [reflexivity | lia].
Qed.

Lemma denote_tag_nest_unfold {St : Type} (child : list (cursor_tag_entry St)) :
  denote_tag_entry (TagNest child) = NestedCursor (map denote_tag_entry child).
Proof.
  simpl. apply f_equal. induction child as [|h t IH]; simpl; [reflexivity | apply f_equal2; [reflexivity | exact IH]].
Qed.

Lemma restore_tag_nest_unfold {St : Type} (child : list (cursor_tag_entry St)) (st : St) :
  restore_tag_entry (TagNest child) st = restore_tag_entries child st.
Proof.
  simpl. revert st. induction child as [|h t IH]; simpl; [reflexivity | intros st; rewrite IH; reflexivity].
Qed.

Lemma held_tag_nest_unfold {St : Type} (child : list (cursor_tag_entry St)) :
  held_tag_entry (TagNest child) = S (held_tag_entries child).
Proof.
  simpl. apply eq_S. induction child as [|h t IH]; simpl; [reflexivity | lia].
Qed.

Lemma held_entry_nested_unfold {St : Type} (child : list (cursor_entry St)) :
  held_entry (NestedCursor child) = S (held_entries child).
Proof.
  simpl. apply eq_S. induction child as [|h t IH]; simpl; [reflexivity | lia].
Qed.

Lemma defunctionalized_cursor_sized {St : Type} bound :
  (forall (e : cursor_tag_entry St), tag_entry_size e <= bound ->
     (forall st, restore_tag_entry e st = restore_entry (denote_tag_entry e) st) /\
     held_tag_entry e = held_entry (denote_tag_entry e)) /\
  (forall (es : list (cursor_tag_entry St)), tag_entries_size es <= bound ->
     (forall st, restore_tag_entries es st = restore_entries (map denote_tag_entry es) st) /\
     held_tag_entries es = held_entries (map denote_tag_entry es)).
Proof.
  induction bound as [|b [IHe IHes]].
  - split.
    + intros [? | ? | ? | ? | child] Hsz; simpl in Hsz; lia.
    + intros [|e rest] Hsz; [split; reflexivity|].
      destruct e; simpl in Hsz; lia.
  - split.
    + intros [inv | reinsert | detach | swap | child] Hsz.
      * split; reflexivity.
      * split; reflexivity.
      * split; reflexivity.
      * split; reflexivity.
      * rewrite tag_entry_size_nest in Hsz.
        destruct (IHes child ltac:(lia)) as [Hrest Hheld].
        rewrite denote_tag_nest_unfold, held_tag_nest_unfold, held_entry_nested_unfold.
        split.
        -- intros st. rewrite restore_tag_nest_unfold, restore_entry_nested. apply Hrest.
        -- lia.
    + intros [|e rest] Hsz; [split; reflexivity|].
      assert (He_pos : tag_entry_size e >= 1) by (destruct e; simpl; lia).
      simpl in Hsz |- *.
      destruct (IHe e ltac:(lia)) as [He_r He_h].
      destruct (IHes rest ltac:(lia)) as [Hrest_r Hrest_h].
      split.
      * intros st. rewrite Hrest_r, He_r. reflexivity.
      * lia.
Qed.

(**md
**Restoring defunctionalized `[<Struct>]` `HydrationCursor` entries produces the exact state of closure-based `restore_entries`.**

Dispatching tagged `UndoEntry` values (`TagCustom`, `TagInsertBefore`, `TagRemove`, `TagReplace`, `TagNest`) in LIFO order is extensionally identical to running closure-wrapped `cursor_entry` values.

- [St:] the server DOM and `DocumentHead` state type.
- [es:] the recorded tagged undo entries.
- [st:] the post-attempt state to unwind.
*)
Theorem defunctionalized_cursor_restore_equiv {St : Type}
    (es : list (cursor_tag_entry St)) (st : St) :
  restore_tag_entries es st = restore_entries (map denote_tag_entry es) st.
Proof.
  destruct (@defunctionalized_cursor_sized St (tag_entries_size es)) as [_ Hes].
  destruct (Hes es (Nat.le_refl _)) as [Hr _].
  apply Hr.
Qed.

(**md
**Defunctionalized `HydrationCursor` entries preserve exact `HydrationCounters.held` accounting and zero on `Commit`.**

Tagged undo entries report the identical `held` count as closure entries and drop to `0` upon `commit_tag_entries`.

- [St:] the server DOM and `DocumentHead` state type.
- [es:] the recorded tagged undo entries.
*)
Theorem defunctionalized_cursor_held_equiv {St : Type}
    (es : list (cursor_tag_entry St)) :
  held_tag_entries es = held_entries (map denote_tag_entry es) /\
  held_tag_entries (commit_tag_entries es) = 0.
Proof.
  destruct (@defunctionalized_cursor_sized St (tag_entries_size es)) as [_ Hes].
  destruct (Hes es (Nat.le_refl _)) as [_ Hh].
  split; [exact Hh | reflexivity].
Qed.

(** Copy-on-write `copyServed` model for `[c` `ViewBoundary.HydrateContent`. *)
Inductive boundary_hyd_step (St : Type) :=
  | BHMatch
  | BHMutate (f : St -> St).

Arguments BHMatch {St}.
Arguments BHMutate {St} _.

Fixpoint run_hyd_with_lazy_copy {St : Type}
    (steps : list (boundary_hyd_step St)) (cur : St) (snap : option St) : St * option St :=
  match steps with
  | [] => (cur, snap)
  | BHMatch :: rest => run_hyd_with_lazy_copy rest cur snap
  | BHMutate f :: rest =>
    let snap1 := match snap with Some s => Some s | None => Some cur end in
    run_hyd_with_lazy_copy rest (f cur) snap1
  end.

Definition finalize_lazy_copy {St : Type} (waiting : nat) (cur : St) (snap : option St) : option St :=
  if Nat.eqb waiting 0 then None
  else match snap with Some s => Some s | None => Some cur end.

Lemma run_hyd_with_some_snap {St : Type} (steps : list (boundary_hyd_step St)) (cur s_orig : St) :
  snd (run_hyd_with_lazy_copy steps cur (Some s_orig)) = Some s_orig.
Proof.
  revert cur.
  induction steps as [|[|f] rest IH]; intros cur; simpl; [reflexivity | apply IH | apply IH].
Qed.

Lemma run_hyd_preserves_origin {St : Type} (steps : list (boundary_hyd_step St)) (s0 : St) :
  let '(s_end, snap) := run_hyd_with_lazy_copy steps s0 None in
  match snap with
  | Some s_captured => s_captured = s0
  | None => s_end = s0
  end.
Proof.
  revert s0.
  induction steps as [|[|f] rest IH]; intros s0; simpl.
  - reflexivity.
  - apply IH.
  - pose proof (run_hyd_with_some_snap rest (f s0) s0) as Hsnd.
    destruct (run_hyd_with_lazy_copy rest (f s0) (Some s0)) as [s_end snap]; simpl in Hsnd.
    subst snap; reflexivity.
Qed.

(**md
**Lazy copy-on-write `copyServed` captures the exact initial server DOM whenever a `[c` boundary pends on the client.**

Whether `HydrateContent` mutates server nodes during setup (`BeforeEdit` snapshots before the first edit) or performs zero edits (`SetUpEnded` snapshots at `waiting > 0`), `finalize_lazy_copy` always yields `Some s0`.

- [St:] the server DOM state type between the boundary markers.
- [steps:] the sequence of matching and mutating hydration steps inside `SetUpContent`.
- [s0:] the initial server DOM state before `HydrateContent` began.
- [waiting:] the positive count of pending bindings at `SetUpEnded`.
*)
Theorem lazy_copy_served_snapshot_equiv {St : Type}
    (steps : list (boundary_hyd_step St)) (s0 : St) (waiting : nat) :
  0 < waiting ->
  let '(s_end, snap) := run_hyd_with_lazy_copy steps s0 None in
  finalize_lazy_copy waiting s_end snap = Some s0.
Proof.
  intros Hw.
  pose proof (run_hyd_preserves_origin steps s0) as Horig.
  destruct (run_hyd_with_lazy_copy steps s0 None) as [s_end [s_cap|]].
  - subst s_cap. unfold finalize_lazy_copy.
    assert (Hne : Nat.eqb waiting 0 = false) by (apply Nat.eqb_neq; lia).
    rewrite Hne; reflexivity.
  - subst s_end. unfold finalize_lazy_copy.
    assert (Hne : Nat.eqb waiting 0 = false) by (apply Nat.eqb_neq; lia).
    rewrite Hne; reflexivity.
Qed.

(**md
**Settled SSR `[c` boundaries whose hydration matches server output perform zero `copyServed` DOM clones.**

When every step is `BHMatch` and `waiting = 0` at `SetUpEnded`, `snap` stays `None` throughout setup and reveal — eliminating `node.cloneNode(true)` completely.

- [St:] the server DOM state type between the boundary markers.
- [steps:] a hydration transcript containing only non-mutating `BHMatch` steps.
- [s0:] the initial server DOM state.
*)
Theorem lazy_copy_served_skips_clone_when_settled {St : Type}
    (steps : list (boundary_hyd_step St)) (s0 : St) :
  Forall (fun step => step = BHMatch) steps ->
  let '(s_end, snap) := run_hyd_with_lazy_copy steps s0 None in
  snap = None /\ finalize_lazy_copy 0 s_end snap = None /\ s_end = s0.
Proof.
  induction 1 as [|step rest -> _ IH]; simpl.
  - unfold finalize_lazy_copy; rewrite Nat.eqb_refl; repeat split; reflexivity.
  - exact IH.
Qed.

(** ** Proof-backed SSR leaf-plan fast path, re-entrant `RequestScope` staging, and `IdentityScope` windows *)

(** Server-plan operations (`Template.ServerOp` in `Template.fs`). Leaf operations depend only on the
    root instance, whereas `SsrRegionOp` and `SsrEnterOp` mark `ServerPlan.Positioned = true`. *)
Inductive ssr_plan_op :=
  | SsrLeafEmit (emit : nat -> list nat)
  | SsrRegionOp (emit_region : nat -> nat -> list nat)
  | SsrEnterOp (child_slot : nat) (emit_child : nat -> nat -> list nat).

Definition ssr_op_positioned (op : ssr_plan_op) : bool :=
  match op with
  | SsrLeafEmit _ => false
  | SsrRegionOp _ | SsrEnterOp _ _ => true
  end.

Definition ssr_plan_positioned (ops : list ssr_plan_op) : bool :=
  existsb ssr_op_positioned ops.

Record ssr_exec_state := {
  ssr_markup : list nat;
  ssr_adopted_owners : list nat;
  ssr_next_oid : nat;
  ssr_completed_fences : list bool
}.

Fixpoint run_ssr_ops_full (scope_id : nat) (parts : list nat) (ops : list ssr_plan_op) : list nat :=
  let inst := match parts with [] => 0 | x :: _ => x end in
  match ops with
  | [] => []
  | SsrLeafEmit emit :: rest => emit inst ++ run_ssr_ops_full scope_id parts rest
  | SsrRegionOp emit_r :: rest => emit_r scope_id inst ++ run_ssr_ops_full scope_id parts rest
  | SsrEnterOp slot emit_c :: rest =>
    let c_inst := nth slot parts 0 in
    emit_c scope_id c_inst ++ run_ssr_ops_full scope_id parts rest
  end.

Fixpoint run_ssr_ops_leaf (inst : nat) (ops : list ssr_plan_op) : list nat :=
  match ops with
  | [] => []
  | SsrLeafEmit emit :: rest => emit inst ++ run_ssr_ops_leaf inst rest
  | _ :: rest => run_ssr_ops_leaf inst rest
  end.

Definition ssr_instance_full (setup_fn : nat -> nat) (child_count : nat)
    (ops : list ssr_plan_op) (st : ssr_exec_state) : ssr_exec_state :=
  let oid := ssr_next_oid st in
  let inst := setup_fn oid in
  let parts := inst :: repeat 0 child_count in
  let out := run_ssr_ops_full oid parts ops in
  {| ssr_markup := ssr_markup st ++ out;
     ssr_adopted_owners := ssr_adopted_owners st ++ [oid];
     ssr_next_oid := S oid;
     ssr_completed_fences := ssr_completed_fences st ++ [true] |}.

Definition ssr_instance_leaf (setup_fn : nat -> nat)
    (ops : list ssr_plan_op) (st : ssr_exec_state) : ssr_exec_state :=
  let oid := ssr_next_oid st in
  let inst := setup_fn oid in
  let out := run_ssr_ops_leaf inst ops in
  {| ssr_markup := ssr_markup st ++ out;
     ssr_adopted_owners := ssr_adopted_owners st ++ [oid];
     ssr_next_oid := S oid;
     ssr_completed_fences := ssr_completed_fences st ++ [true] |}.

Definition leaf_fence_allows_cleanup (active_owner : option nat) (disposed : bool) : bool :=
  match active_owner with
  | None => false
  | Some _ => negb disposed
  end.

Definition full_fence_allows_cleanup (completed : bool) (disposed : bool) : bool :=
  negb completed && negb disposed.

Lemma run_ssr_ops_leaf_eq_full scope_id inst tail ops :
  ssr_plan_positioned ops = false ->
  run_ssr_ops_leaf inst ops = run_ssr_ops_full scope_id (inst :: tail) ops.
Proof.
  induction ops as [|op rest IH]; intros Hpos; [reflexivity|].
  destruct op as [emit | ? | ? ?]; simpl in Hpos; try discriminate.
  simpl. rewrite (IH Hpos). reflexivity.
Qed.

(**md
**Omitting child `RequestScope`, discarded intermediate `Setup`, and `[| null |]` `parts` on leaf SSR plans is observationally exact.**

Whenever `ssr_plan_positioned ops = false` (`not plan.Positioned` in `ServerWriter.Instance`), `ssr_instance_leaf` (`RequestScope.NestLeaf`) emits identical HTML, adopts the exact same per-instance `Owner` IDs, and enforces the exact `OnCleanup` completion and disposal fences as `ssr_instance_full`.

- [setup_fn:] the component's `Setup` function parameterized by its adopted child `Owner`.
- [ops:] a compiled `ServerPlan` operation list with `ssr_plan_positioned ops = false`.
- [st:] the SSR state prior to rendering the leaf instance.
- [disposed:] whether the child `Owner` has been disposed when `OnCleanup` is invoked.
*)
Theorem ssr_leaf_plan_omits_child_scope_equiv
    (setup_fn : nat -> nat) (ops : list ssr_plan_op) (st : ssr_exec_state) (disposed : bool) :
  ssr_plan_positioned ops = false ->
  ssr_instance_leaf setup_fn ops st = ssr_instance_full setup_fn 0 ops st /\
  leaf_fence_allows_cleanup (Some (ssr_next_oid st)) disposed = full_fence_allows_cleanup false disposed /\
  leaf_fence_allows_cleanup None disposed = full_fence_allows_cleanup true disposed.
Proof.
  intros Hpos.
  split.
  - unfold ssr_instance_leaf, ssr_instance_full.
    rewrite (run_ssr_ops_leaf_eq_full (ssr_next_oid st) (setup_fn (ssr_next_oid st)) [] ops Hpos).
    reflexivity.
  - split; destruct disposed; reflexivity.
Qed.

(** Re-entrant staging model for hoisted `RequestScope` `runWithOwner` / `createRoot` callbacks. *)
Record ssr_staging := {
  stg_def : nat;
  stg_props : nat;
  stg_kind : nat;
  stg_a : nat;
  stg_b : nat
}.

Inductive ssr_reentrant_call :=
  | SsrCall (def_id props kind a b : nat) (nested : list ssr_reentrant_call).

Fixpoint eval_closure_call (c : ssr_reentrant_call) : list ssr_staging :=
  let fix eval_list (cs : list ssr_reentrant_call) : list ssr_staging :=
    match cs with
    | [] => []
    | hd :: tl => eval_closure_call hd ++ eval_list tl
    end in
  match c with
  | SsrCall d p k a b nested =>
    let frame := {| stg_def := d; stg_props := p; stg_kind := k; stg_a := a; stg_b := b |} in
    frame :: eval_list nested ++ [frame]
  end.

Fixpoint eval_closure_calls (cs : list ssr_reentrant_call) : list ssr_staging :=
  match cs with
  | [] => []
  | hd :: tl => eval_closure_call hd ++ eval_closure_calls tl
  end.

Fixpoint eval_staged_call (c : ssr_reentrant_call) (reg : ssr_staging) : list ssr_staging * ssr_staging :=
  let fix eval_list (cs : list ssr_reentrant_call) (r : ssr_staging) : list ssr_staging * ssr_staging :=
    match cs with
    | [] => ([], r)
    | hd :: tl =>
      let '(tr1, r1) := eval_staged_call hd r in
      let '(tr2, r2) := eval_list tl r1 in
      (tr1 ++ tr2, r2)
    end in
  match c with
  | SsrCall d p k a b nested =>
    let saved := reg in
    let reg_in := {| stg_def := d; stg_props := p; stg_kind := k; stg_a := a; stg_b := b |} in
    let local_frame := reg_in in
    let '(nested_tr, _) := eval_list nested reg_in in
    (local_frame :: nested_tr ++ [local_frame], saved)
  end.

Fixpoint eval_staged_calls (cs : list ssr_reentrant_call) (reg : ssr_staging) : list ssr_staging * ssr_staging :=
  match cs with
  | [] => ([], reg)
  | hd :: tl =>
    let '(tr1, r1) := eval_staged_call hd reg in
    let '(tr2, r2) := eval_staged_calls tl r1 in
    (tr1 ++ tr2, r2)
  end.

Fixpoint ssr_call_size (c : ssr_reentrant_call) : nat :=
  let fix list_sz (cs : list ssr_reentrant_call) : nat :=
    match cs with
    | [] => 0
    | hd :: tl => S (ssr_call_size hd + list_sz tl)
    end in
  match c with
  | SsrCall _ _ _ _ _ nested => S (list_sz nested)
  end.

Fixpoint ssr_calls_size (cs : list ssr_reentrant_call) : nat :=
  match cs with
  | [] => 0
  | hd :: tl => S (ssr_call_size hd + ssr_calls_size tl)
  end.

Lemma ssr_call_size_unfold d p k a b nested :
  ssr_call_size (SsrCall d p k a b nested) = S (ssr_calls_size nested).
Proof.
  simpl. apply eq_S. induction nested as [|h t IH]; simpl; [reflexivity | lia].
Qed.

Lemma eval_closure_call_unfold d p k a b nested :
  let frame := {| stg_def := d; stg_props := p; stg_kind := k; stg_a := a; stg_b := b |} in
  eval_closure_call (SsrCall d p k a b nested) = frame :: eval_closure_calls nested ++ [frame].
Proof.
  simpl. apply f_equal. apply f_equal2; [|reflexivity].
  induction nested as [|h t IH]; simpl; [reflexivity | rewrite IH; reflexivity].
Qed.

Lemma eval_staged_call_unfold d p k a b nested reg :
  let saved := reg in
  let frame := {| stg_def := d; stg_props := p; stg_kind := k; stg_a := a; stg_b := b |} in
  eval_staged_call (SsrCall d p k a b nested) reg =
  (let '(nested_tr, _) := eval_staged_calls nested frame in (frame :: nested_tr ++ [frame], saved)).
Proof.
  simpl.
  assert (Hlist : forall cs r,
    (fix eval_list (l : list ssr_reentrant_call) (r0 : ssr_staging) {struct l} : list ssr_staging * ssr_staging :=
       match l with
       | [] => ([], r0)
       | hd :: tl =>
         let '(tr1, r1) := eval_staged_call hd r0 in
         let '(tr2, r2) := eval_list tl r1 in (tr1 ++ tr2, r2)
       end) cs r = eval_staged_calls cs r).
  { induction cs as [|h t IH]; intros r; simpl; [reflexivity|].
    destruct (eval_staged_call h r) as [tr1 r1].
    rewrite IH. reflexivity. }
  rewrite Hlist. reflexivity.
Qed.

Lemma ssr_reentrant_staging_sized bound :
  (forall c reg, ssr_call_size c <= bound -> eval_staged_call c reg = (eval_closure_call c, reg)) /\
  (forall cs reg, ssr_calls_size cs <= bound -> eval_staged_calls cs reg = (eval_closure_calls cs, reg)).
Proof.
  induction bound as [|b [IHc IHcs]].
  - split.
    + intros [? ? ? ? ? ?] reg Hsz; simpl in Hsz; lia.
    + intros [|c rest] reg Hsz; [reflexivity | destruct c; simpl in Hsz; lia].
  - split.
    + intros [d p k a b0 nested] reg Hsz.
      rewrite ssr_call_size_unfold in Hsz.
      rewrite eval_staged_call_unfold, eval_closure_call_unfold.
      simpl.
      pose proof (IHcs nested {| stg_def := d; stg_props := p; stg_kind := k; stg_a := a; stg_b := b0 |} ltac:(lia)) as Hn.
      rewrite Hn. reflexivity.
    + intros [|c rest] reg Hsz; [reflexivity|].
      assert (Hc_pos : ssr_call_size c >= 1) by (destruct c; simpl; lia).
      simpl in Hsz |- *.
      rewrite (IHc c reg ltac:(lia)).
      rewrite (IHcs rest reg ltac:(lia)).
      reflexivity.
Qed.

(**md
**Hoisted `RequestScope` staging fields with stack save/restore are re-entrancy-exact to per-call closure allocation.**

Saving outer staging fields on the stack, reading arguments into locals upon entering the hoisted `createRoot` delegate, and restoring the outer fields in `finally` produces the exact execution trace of closure-per-call `Nest`/`NestLeaf` at arbitrary nesting depth and leaves `reg` unchanged.

- [c:] a tree of nested/re-entrant `RequestScope` invocations.
- [reg:] the ambient staging state on `RequestScope` prior to the call.
*)
Theorem ssr_reentrant_staging_save_restore_equiv
    (c : ssr_reentrant_call) (reg : ssr_staging) :
  eval_staged_call c reg = (eval_closure_call c, reg).
Proof.
  destruct (ssr_reentrant_staging_sized (ssr_call_size c)) as [Hc _].
  apply Hc, Nat.le_refl.
Qed.

(** `IdentityScope` unboxed `[<Struct>]` window stack and direct single-pass ID formatting (`Identity.fs`). *)
Inductive id_step_kind :=
  | IdRoot
  | IdChild
  | IdRow
  | IdAuthored.

Definition compute_instance_path (parent : list nat) (kind : id_step_kind) (a b : nat) : list nat :=
  match kind with
  | IdRoot => [0]
  | IdChild => parent ++ [1; a]
  | IdRow => parent ++ [2; a; b]
  | IdAuthored => parent ++ [3; a]
  end.

Record id_window_struct := {
  iw_parent : list nat;
  iw_kind : id_step_kind;
  iw_a : nat;
  iw_b : nat;
  iw_path : option (list nat);
  iw_issued : nat;
  iw_window : nat
}.

Record id_scope_state := {
  ids_opened : bool;
  ids_parent : list nat;
  ids_kind : id_step_kind;
  ids_a : nat;
  ids_b : nat;
  ids_path : option (list nat);
  ids_issued : nat;
  ids_window : nat;
  ids_window_count : nat;
  ids_enclosing : list id_window_struct
}.

Definition id_path_inv (st : id_scope_state) : Prop :=
  ids_window st <= ids_window_count st /\
  match ids_path st with
  | Some p => p = compute_instance_path (ids_parent st) (ids_kind st) (ids_a st) (ids_b st)
  | None => True
  end.

Definition id_enter (parent : list nat) (kind : id_step_kind) (a b : nat) (st : id_scope_state) : id_scope_state :=
  let enc' :=
    if ids_opened st then
      {| iw_parent := ids_parent st;
         iw_kind := ids_kind st;
         iw_a := ids_a st;
         iw_b := ids_b st;
         iw_path := ids_path st;
         iw_issued := ids_issued st;
         iw_window := ids_window st |} :: ids_enclosing st
    else ids_enclosing st in
  let next_w := S (ids_window_count st) in
  {| ids_opened := true;
     ids_parent := parent;
     ids_kind := kind;
     ids_a := a;
     ids_b := b;
     ids_path := None;
     ids_issued := 0;
     ids_window := next_w;
     ids_window_count := next_w;
     ids_enclosing := enc' |}.

Definition id_leave (st : id_scope_state) : id_scope_state :=
  match ids_enclosing st with
  | outer :: rest =>
    {| ids_opened := true;
       ids_parent := iw_parent outer;
       ids_kind := iw_kind outer;
       ids_a := iw_a outer;
       ids_b := iw_b outer;
       ids_path := iw_path outer;
       ids_issued := iw_issued outer;
       ids_window := iw_window outer;
       ids_window_count := ids_window_count st;
       ids_enclosing := rest |}
  | [] =>
    {| ids_opened := false;
       ids_parent := [];
       ids_kind := ids_kind st;
       ids_a := ids_a st;
       ids_b := ids_b st;
       ids_path := None;
       ids_issued := ids_issued st;
       ids_window := 0;
       ids_window_count := ids_window_count st;
       ids_enclosing := [] |}
  end.

Definition id_create_eager (st : id_scope_state) : option ((list nat * nat) * id_scope_state) :=
  if negb (ids_opened st) then None
  else
    let p :=
      match ids_path st with
      | Some cached => cached
      | None => compute_instance_path (ids_parent st) (ids_kind st) (ids_a st) (ids_b st)
      end in
    let n := ids_issued st in
    Some ((p, n),
      {| ids_opened := true;
         ids_parent := ids_parent st;
         ids_kind := ids_kind st;
         ids_a := ids_a st;
         ids_b := ids_b st;
         ids_path := Some p;
         ids_issued := S n;
         ids_window := ids_window st;
         ids_window_count := ids_window_count st;
         ids_enclosing := ids_enclosing st |}).

Definition id_create_direct (st : id_scope_state) : option ((list nat * nat) * id_scope_state) :=
  if negb (ids_opened st) then None
  else
    let n := ids_issued st in
    match ids_path st with
    | Some cached =>
      Some ((cached, n),
        {| ids_opened := true;
           ids_parent := ids_parent st;
           ids_kind := ids_kind st;
           ids_a := ids_a st;
           ids_b := ids_b st;
           ids_path := Some cached;
           ids_issued := S n;
           ids_window := ids_window st;
           ids_window_count := ids_window_count st;
           ids_enclosing := ids_enclosing st |})
    | None =>
      let p := compute_instance_path (ids_parent st) (ids_kind st) (ids_a st) (ids_b st) in
      let next_cached := if Nat.eqb n 0 then None else Some p in
      Some ((p, n),
        {| ids_opened := true;
           ids_parent := ids_parent st;
           ids_kind := ids_kind st;
           ids_a := ids_a st;
           ids_b := ids_b st;
           ids_path := next_cached;
           ids_issued := S n;
           ids_window := ids_window st;
           ids_window_count := ids_window_count st;
           ids_enclosing := ids_enclosing st |})
    end.

Definition id_create_in_direct (owner : nat) (st : id_scope_state) : option ((list nat * nat) * id_scope_state) :=
  if Nat.eqb owner 0 || negb (Nat.eqb owner (ids_window st)) then None
  else id_create_direct st.

(**md
**Unboxed `[<Struct>]` `IdentityWindow` stack frames and direct leaf `CreateId` formatting preserve exact IDs and window fencing.**

Direct `CreateId` without allocating `InstancePath` on `issued = 0` emits the identical `(path, issued)` token as `this.Path().Text`, and `Enter` followed by `CreateId` calls and `Leave` restores the outer window while fencing out `CreateIdIn` on the closed child window.

- [st:] an open enclosing `IdentityScope` state satisfying `id_path_inv`.
- [parent:] the parent path passed to a nested `Enter`.
- [kind:] the `InstanceStep` kind of the nested window.
- [a:] first positional index of the nested window.
- [b:] second positional index of the nested window.
*)
Theorem identity_window_stack_lifo_equiv
    (st : id_scope_state) (parent : list nat) (kind : id_step_kind) (a b : nat) :
  ids_opened st = true ->
  id_path_inv st ->
  option_map fst (id_create_direct st) = option_map fst (id_create_eager st) /\
  let st_child := id_enter parent kind a b st in
  let child_win := ids_window st_child in
  let st_after :=
    match id_create_direct st_child with
    | Some (_, s1) => id_leave s1
    | None => id_leave st_child
    end in
  ids_window st_after = ids_window st /\
  ids_issued st_after = ids_issued st /\
  ids_parent st_after = ids_parent st /\
  id_create_in_direct child_win st_after = None.
Proof.
  intros Hopen [Hwin Hpath].
  split.
  - unfold id_create_direct, id_create_eager. rewrite Hopen; simpl.
    destruct (ids_path st) as [cached|]; [reflexivity|].
    destruct (Nat.eqb (ids_issued st) 0); reflexivity.
  - cbv zeta.
    unfold id_enter, id_create_direct, id_leave, id_create_in_direct; simpl.
    rewrite Hopen; simpl.
    assert (Hz0 : Nat.eqb 0 0 = true) by reflexivity.
    rewrite ?Hz0; simpl.
    assert (Hnz : Nat.eqb (S (ids_window_count st)) 0 = false) by reflexivity.
    assert (Hneq : Nat.eqb (S (ids_window_count st)) (ids_window st) = false)
      by (apply Nat.eqb_neq; lia).
    rewrite ?Hnz, ?Hneq; simpl.
    repeat split; reflexivity.
Qed.
