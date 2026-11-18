From stdpp Require Import prelude.
From Stdlib Require Import Arith Lia List Bool.
Import ListNotations.

Local Arguments Nat.eqb : simpl never.
Local Arguments Nat.leb : simpl never.
Local Arguments Nat.ltb : simpl never.

(**md # Queued DOM commits, `OwnerWorkQueue` wakeups, and `CompletionFence` lifecycle

Models `Ranvier.View.Dom.Scheduling` (`Slot`, `Queue`), `Ranvier.View.WebCore.OwnerWorkQueue` (`OwnerWorkQueue`), and `Ranvier.View.WebCore.Sdl.CompletionFence` (`CompletionFence`). *)

(** ** `Scheduling.Queue` coalescing, cancellation, and error reporting *)

Inductive commit_failure :=
  | CollectAndContinue
  | FailFastCancel.

Record slot_model := {
  sl_version : nat;
  sl_listed : bool;
  sl_action : option nat;
  sl_has_dropped_cb : bool;
  sl_dropped_runs : nat;
  sl_scope : nat
}.

Record queue_model := {
  q_slots : list slot_model;
  q_pending : list nat;
  q_disposed : bool;
  q_scheduled : bool;
  q_generation : nat;
  q_tickets : list nat;
  q_committed : list nat;
  q_errors : list (nat * nat)
}.

Definition init_queue : queue_model :=
  {| q_slots := [];
     q_pending := [];
     q_disposed := false;
     q_scheduled := false;
     q_generation := 0;
     q_tickets := [];
     q_committed := [];
     q_errors := [] |}.

Fixpoint set_slot (idx : nat) (s : slot_model) (ss : list slot_model) : list slot_model :=
  match idx, ss with
  | _, [] => []
  | 0, _ :: rest => s :: rest
  | S i, x :: rest => x :: set_slot i s rest
  end.

Definition q_request (q : queue_model) : queue_model :=
  if negb (q_disposed q) && negb (q_scheduled q) &&
     match q_pending q with [] => false | _ => true end
  then
    {| q_slots := q_slots q;
       q_pending := q_pending q;
       q_disposed := q_disposed q;
       q_scheduled := true;
       q_generation := q_generation q;
       q_tickets := q_tickets q ++ [q_generation q];
       q_committed := q_committed q;
       q_errors := q_errors q |}
  else q.

Definition q_reserve (scope : nat) (has_dropped_cb : bool) (q : queue_model) : queue_model * nat :=
  let idx := length (q_slots q) in
  let sl := {| sl_version := 0;
               sl_listed := false;
               sl_action := None;
               sl_has_dropped_cb := has_dropped_cb;
               sl_dropped_runs := 0;
               sl_scope := scope |} in
  ({| q_slots := q_slots q ++ [sl];
      q_pending := q_pending q;
      q_disposed := q_disposed q;
      q_scheduled := q_scheduled q;
      q_generation := q_generation q;
      q_tickets := q_tickets q;
      q_committed := q_committed q;
      q_errors := q_errors q |},
   idx).

Definition q_cancel (idx : nat) (q : queue_model) : queue_model :=
  match nth_error (q_slots q) idx with
  | None => q
  | Some sl =>
    let sl' := {| sl_version := S (sl_version sl);
                  sl_listed := sl_listed sl;
                  sl_action := None;
                  sl_has_dropped_cb := sl_has_dropped_cb sl;
                  sl_dropped_runs := sl_dropped_runs sl;
                  sl_scope := sl_scope sl |} in
    {| q_slots := set_slot idx sl' (q_slots q);
       q_pending := q_pending q;
       q_disposed := q_disposed q;
       q_scheduled := q_scheduled q;
       q_generation := q_generation q;
       q_tickets := q_tickets q;
       q_committed := q_committed q;
       q_errors := q_errors q |}
  end.

Definition q_enqueue (idx action : nat) (q : queue_model) : queue_model :=
  if q_disposed q then q
  else
    match nth_error (q_slots q) idx with
    | None => q
    | Some sl =>
      let was_listed := sl_listed sl in
      let sl' := {| sl_version := S (sl_version sl);
                    sl_listed := true;
                    sl_action := Some action;
                    sl_has_dropped_cb := sl_has_dropped_cb sl;
                    sl_dropped_runs := sl_dropped_runs sl;
                    sl_scope := sl_scope sl |} in
      let q1 :=
        {| q_slots := set_slot idx sl' (q_slots q);
           q_pending := if was_listed then q_pending q else q_pending q ++ [idx];
           q_disposed := q_disposed q;
           q_scheduled := q_scheduled q;
           q_generation := q_generation q;
           q_tickets := q_tickets q;
           q_committed := q_committed q;
           q_errors := q_errors q |} in
      q_request q1
    end.

(** Snapshot & clear phase at the start of [Queue.Flush] ([Scheduling.fs:163-171]). *)
Fixpoint snapshot_and_clear (pend : list nat) (slots : list slot_model)
    : list (nat * nat * option nat) * list slot_model :=
  match pend with
  | [] => ([], slots)
  | idx :: rest =>
    match nth_error slots idx with
    | None => snapshot_and_clear rest slots
    | Some sl =>
      let item := (idx, sl_version sl, sl_action sl) in
      let sl_cleared := {| sl_version := sl_version sl;
                           sl_listed := false;
                           sl_action := None;
                           sl_has_dropped_cb := sl_has_dropped_cb sl;
                           sl_dropped_runs := sl_dropped_runs sl;
                           sl_scope := sl_scope sl |} in
      let slots1 := set_slot idx sl_cleared slots in
      let '(items, slots2) := snapshot_and_clear rest slots1 in
      (item :: items, slots2)
    end
  end.

Fixpoint exec_work (mode : commit_failure) (will_fail : nat -> bool) (stopped : bool)
    (work : list (nat * nat * option nat))
    (slots : list slot_model) (committed : list nat) (errs : list (nat * nat))
    : list slot_model * list nat * list (nat * nat) :=
  match work with
  | [] => (slots, committed, errs)
  | (idx, ver, act_opt) :: rest =>
    match nth_error slots idx with
    | None => exec_work mode will_fail stopped rest slots committed errs
    | Some sl =>
      let has_commit := match act_opt with Some _ => true | None => false end in
      if stopped then
        let has_replacement := match sl_action sl with Some _ => true | None => false end in
        let dropped := (has_commit && Nat.eqb (sl_version sl) ver) || has_replacement in
        let sl' := {| sl_version := S (sl_version sl);
                      sl_listed := sl_listed sl;
                      sl_action := None;
                      sl_has_dropped_cb := sl_has_dropped_cb sl;
                      sl_dropped_runs := sl_dropped_runs sl +
                        (if dropped && sl_has_dropped_cb sl then 1 else 0);
                      sl_scope := sl_scope sl |} in
        exec_work mode will_fail true rest (set_slot idx sl' slots) committed errs
      else
        match act_opt with
        | Some act =>
          if Nat.eqb (sl_version sl) ver then
            if will_fail act then
              let stop_now := match mode with FailFastCancel => true | CollectAndContinue => false end in
              exec_work mode will_fail stop_now rest slots committed (errs ++ [(sl_scope sl, act)])
            else
              exec_work mode will_fail false rest slots (committed ++ [act]) errs
          else
            exec_work mode will_fail false rest slots committed errs
        | None =>
          exec_work mode will_fail false rest slots committed errs
        end
    end
  end.

Definition q_flush (mode : commit_failure) (will_fail : nat -> bool) (q : queue_model) : queue_model :=
  if q_disposed q then q
  else
    let '(work, slots1) := snapshot_and_clear (q_pending q) (q_slots q) in
    let '(slots2, committed', errs') :=
      exec_work mode will_fail false work slots1 (q_committed q) [] in
    q_request
      {| q_slots := slots2;
         q_pending := [];
         q_disposed := false;
         q_scheduled := false;
         q_generation := S (q_generation q);
         q_tickets := q_tickets q;
         q_committed := committed';
         q_errors := errs' |}.

(** In-flush re-enqueues triggered while a commit callback executes ([BindingChecks.failFastReplacements]). *)
Fixpoint apply_reenqueues (act : nat) (reenq : list (nat * nat * nat))
    (slots : list slot_model) (pend : list nat) : list slot_model * list nat :=
  match reenq with
  | [] => (slots, pend)
  | (trig, tgt, new_act) :: rest =>
    if Nat.eqb trig act then
      match nth_error slots tgt with
      | None => apply_reenqueues act rest slots pend
      | Some tsl =>
        let pend' := if sl_listed tsl then pend else pend ++ [tgt] in
        let tsl' := {| sl_version := S (sl_version tsl);
                       sl_listed := true;
                       sl_action := Some new_act;
                       sl_has_dropped_cb := sl_has_dropped_cb tsl;
                       sl_dropped_runs := sl_dropped_runs tsl;
                       sl_scope := sl_scope tsl |} in
        apply_reenqueues act rest (set_slot tgt tsl' slots) pend'
      end
    else
      apply_reenqueues act rest slots pend
  end.

Fixpoint exec_work_reenq (mode : commit_failure) (will_fail : nat -> bool)
    (reenq : list (nat * nat * nat)) (stopped : bool)
    (work : list (nat * nat * option nat))
    (slots : list slot_model) (pend : list nat)
    (committed : list nat) (errs : list (nat * nat))
    : list slot_model * list nat * list nat * list (nat * nat) :=
  match work with
  | [] => (slots, pend, committed, errs)
  | (idx, ver, act_opt) :: rest =>
    match nth_error slots idx with
    | None => exec_work_reenq mode will_fail reenq stopped rest slots pend committed errs
    | Some sl =>
      let has_commit := match act_opt with Some _ => true | None => false end in
      if stopped then
        let has_replacement := match sl_action sl with Some _ => true | None => false end in
        let dropped := (has_commit && Nat.eqb (sl_version sl) ver) || has_replacement in
        let sl' := {| sl_version := S (sl_version sl);
                      sl_listed := sl_listed sl;
                      sl_action := None;
                      sl_has_dropped_cb := sl_has_dropped_cb sl;
                      sl_dropped_runs := sl_dropped_runs sl +
                        (if dropped && sl_has_dropped_cb sl then 1 else 0);
                      sl_scope := sl_scope sl |} in
        exec_work_reenq mode will_fail reenq true rest (set_slot idx sl' slots) pend committed errs
      else
        match act_opt with
        | Some act =>
          if Nat.eqb (sl_version sl) ver then
            let '(slots_after, pend_after) := apply_reenqueues act reenq slots pend in
            if will_fail act then
              let stop_now := match mode with FailFastCancel => true | CollectAndContinue => false end in
              exec_work_reenq mode will_fail reenq stop_now rest slots_after pend_after committed
                (errs ++ [(sl_scope sl, act)])
            else
              exec_work_reenq mode will_fail reenq false rest slots_after pend_after
                (committed ++ [act]) errs
          else
            exec_work_reenq mode will_fail reenq false rest slots pend committed errs
        | None =>
          exec_work_reenq mode will_fail reenq false rest slots pend committed errs
        end
    end
  end.

Definition q_flush_reenq (mode : commit_failure) (will_fail : nat -> bool)
    (reenq : list (nat * nat * nat)) (q : queue_model) : queue_model :=
  if q_disposed q then q
  else
    let '(work, slots1) := snapshot_and_clear (q_pending q) (q_slots q) in
    let '(slots2, pend2, committed', errs') :=
      exec_work_reenq mode will_fail reenq false work slots1 [] (q_committed q) [] in
    q_request
      {| q_slots := slots2;
         q_pending := pend2;
         q_disposed := false;
         q_scheduled := false;
         q_generation := S (q_generation q);
         q_tickets := q_tickets q;
         q_committed := committed';
         q_errors := errs' |}.

Definition q_run_posted_ticket (ticket : nat) (mode : commit_failure) (will_fail : nat -> bool)
    (q : queue_model) : queue_model :=
  if negb (q_disposed q) && Nat.eqb ticket (q_generation q) then
    q_flush mode will_fail q
  else q.

Fixpoint cancel_all_pending (pend : list nat) (slots : list slot_model) : list slot_model :=
  match pend with
  | [] => slots
  | idx :: rest =>
    let slots1 :=
      match nth_error slots idx with
      | None => slots
      | Some sl =>
        set_slot idx
          {| sl_version := S (sl_version sl);
             sl_listed := false;
             sl_action := None;
             sl_has_dropped_cb := sl_has_dropped_cb sl;
             sl_dropped_runs := sl_dropped_runs sl;
             sl_scope := sl_scope sl |} slots
      end in
    cancel_all_pending rest slots1
  end.

Definition q_dispose (q : queue_model) : queue_model :=
  if q_disposed q then q
  else
    {| q_slots := cancel_all_pending (q_pending q) (q_slots q);
       q_pending := [];
       q_disposed := true;
       q_scheduled := false;
       q_generation := S (q_generation q);
       q_tickets := q_tickets q;
       q_committed := q_committed q;
       q_errors := q_errors q |}.

(** Per-scope error grouping ([Queue.Report], [Scheduling.fs:219-250]):
    scope 0 denotes a null scope (unreported); each non-zero scope receives all its
    errors in one call, ordered by the scope's first failing slot. *)
Fixpoint report_by_scope_aux (seen : list nat) (rem : list (nat * nat)) : list (nat * list nat) :=
  match rem with
  | [] => []
  | (sc, err) :: rest =>
    if Nat.eqb sc 0 || existsb (Nat.eqb sc) seen then
      report_by_scope_aux seen rest
    else
      let group := err :: map snd (List.filter (fun p => Nat.eqb (fst p) sc) rest) in
      (sc, group) :: report_by_scope_aux (sc :: seen) rest
  end.

Definition report_by_scope (failures : list (nat * nat)) : list (nat * list nat) :=
  report_by_scope_aux [] failures.

(** ** [Scheduling.Queue] theorems *)

Definition q_pending_inv (q : queue_model) : Prop :=
  NoDup (q_pending q) /\
  forall idx sl,
    nth_error (q_slots q) idx = Some sl ->
    (In idx (q_pending q) <-> sl_listed sl = true).

Lemma nth_error_set_slot idx s ss j :
  nth_error (set_slot idx s ss) j =
    if Nat.eqb j idx then
      match nth_error ss idx with Some _ => Some s | None => None end
    else nth_error ss j.
Proof.
  revert idx j. induction ss as [|x xs IH]; intros [|i] [|j]; simpl.
  - destruct (Nat.eqb 0 0); reflexivity.
  - destruct (Nat.eqb (S j) 0); reflexivity.
  - destruct (Nat.eqb 0 (S i)); reflexivity.
  - destruct (Nat.eqb (S j) (S i)); reflexivity.
  - destruct (Nat.eqb 0 0); reflexivity.
  - destruct (Nat.eqb (S j) 0) eqn:Heq; [apply Nat.eqb_eq in Heq; lia | reflexivity].
  - destruct (Nat.eqb 0 (S i)) eqn:Heq; [apply Nat.eqb_eq in Heq; lia | reflexivity].
  - rewrite IH.
    assert (Heq_iff : Nat.eqb (S j) (S i) = Nat.eqb j i).
    { destruct (Nat.eqb (S j) (S i)) eqn:E1; destruct (Nat.eqb j i) eqn:E2; auto.
      - apply Nat.eqb_eq in E1; apply Nat.eqb_neq in E2; lia.
      - apply Nat.eqb_neq in E1; apply Nat.eqb_eq in E2; lia. }
    rewrite Heq_iff; reflexivity.
Qed.

Lemma q_pending_inv_request r :
  q_pending_inv r -> q_pending_inv (q_request r).
Proof.
  unfold q_request, q_pending_inv.
  destruct (negb (q_disposed r) && negb (q_scheduled r) &&
            match q_pending r with [] => false | _ :: _ => true end); simpl; trivial.
Qed.

(**md
**Enqueuing a commit preserves `NoDup` on `q_pending` and exact `sl_listed` agreement.**

`q_enqueue` (`Queue.Enqueue`) appends `idx` to `q_pending` if and only if `sl_listed` was `false`, preserving `q_pending_inv`.

- [idx:] the slot index enqueued.
- [act:] the commit action id stored on the slot.
- [q:] the `Queue` state before `q_enqueue`.
*)
Theorem q_enqueue_preserves_pending_inv idx act q :
  q_pending_inv q ->
  q_pending_inv (q_enqueue idx act q).
Proof.
  intros [Hnd Hiff]. unfold q_enqueue.
  destruct (q_disposed q); [exact (conj Hnd Hiff)|].
  destruct (nth_error (q_slots q) idx) as [sl|] eqn:Hsl; [|exact (conj Hnd Hiff)].
  apply q_pending_inv_request. unfold q_pending_inv; simpl.
  assert (Hcore :
    NoDup (if sl_listed sl then q_pending q else q_pending q ++ [idx]) /\
    forall j sl_j,
      nth_error (set_slot idx {| sl_version := S (sl_version sl); sl_listed := true; sl_action := Some act;
                                 sl_has_dropped_cb := sl_has_dropped_cb sl; sl_dropped_runs := sl_dropped_runs sl;
                                 sl_scope := sl_scope sl |} (q_slots q)) j = Some sl_j ->
      (In j (if sl_listed sl then q_pending q else q_pending q ++ [idx]) <-> sl_listed sl_j = true)).
  { destruct (sl_listed sl) eqn:Hlist.
    - split; [exact Hnd|].
      intros j sl_j Hj. rewrite nth_error_set_slot in Hj.
      destruct (Nat.eqb j idx) eqn:Hji.
      + apply Nat.eqb_eq in Hji; subst j. rewrite Hsl in Hj; injection Hj as <-; simpl.
        pose proof (proj2 (Hiff idx sl Hsl) Hlist) as Hin. tauto.
      + exact (Hiff j sl_j Hj).
    - assert (Hnin : ~ In idx (q_pending q)).
      { intros Hin. apply (proj1 (Hiff idx sl Hsl)) in Hin. congruence. }
      split.
      + clear -Hnd Hnin. induction Hnd as [|x xs Hx Hnd' IH]; simpl.
        * constructor; [intros [] | constructor].
        * constructor.
          -- intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | [-> | []]];
             [exact (Hx Hin) | apply Hnin; left; reflexivity].
          -- apply IH; intros Hin; apply Hnin; right; exact Hin.
      + intros j sl_j Hj. rewrite nth_error_set_slot in Hj.
        destruct (Nat.eqb j idx) eqn:Hji.
        * apply Nat.eqb_eq in Hji; subst j. rewrite Hsl in Hj; injection Hj as <-; simpl.
          split; [trivial | intros _; apply in_or_app; right; left; reflexivity].
        * apply Nat.eqb_neq in Hji. rewrite in_app_iff; simpl.
          pose proof (Hiff j sl_j Hj) as Hj_iff.
          split; [intros [Hin | [-> | []]]; [exact (proj1 Hj_iff Hin) | contradiction]
                 | intros Hl; left; exact (proj2 Hj_iff Hl)]. }
  exact Hcore.
Qed.

Lemma q_disposed_request r : q_disposed (q_request r) = q_disposed r.
Proof. unfold q_request; destruct (_ && _ && _); reflexivity. Qed.

Lemma q_slots_request r : q_slots (q_request r) = q_slots r.
Proof. unfold q_request; destruct (_ && _ && _); reflexivity. Qed.

Lemma q_pending_request r : q_pending (q_request r) = q_pending r.
Proof. unfold q_request; destruct (_ && _ && _); reflexivity. Qed.

Lemma q_gen_request r : q_generation (q_request r) = q_generation r.
Proof. unfold q_request; destruct (_ && _ && _); reflexivity. Qed.

(**md
**Re-enqueuing an already-listed slot coalesces in place without growing `q_pending`.**

A second `q_enqueue` (`Queue.Enqueue`) on `idx` before `Flush` updates `sl_action` and `sl_version` while leaving `q_pending` identical.

- [idx:] the live slot index enqueued twice.
- [a1:] the first commit action id written to `idx`.
- [a2:] the superseding commit action id written to `idx`.
- [q:] the live `Queue` state before the two enqueues.
*)
Theorem q_reenqueue_coalesces_in_place idx a1 a2 q :
  q_disposed q = false ->
  nth_error (q_slots q) idx <> None ->
  let q1 := q_enqueue idx a1 q in
  let q2 := q_enqueue idx a2 q1 in
  q_pending q2 = q_pending q1.
Proof.
  intros Hdisp Hsome. cbv zeta. unfold q_enqueue.
  rewrite Hdisp.
  destruct (nth_error (q_slots q) idx) as [sl|] eqn:Hsl; [|contradiction].
  rewrite q_disposed_request, q_slots_request; simpl.
  rewrite nth_error_set_slot, Nat.eqb_refl, Hsl; simpl.
  rewrite !q_pending_request; simpl. reflexivity.
Qed.

(**md
**An explicit `Flush` invalidates every posted flush ticket from an earlier or equal generation.**

`q_flush` (`Queue.Flush`) advances `q_generation`, making `q_run_posted_ticket` a no-op for any `ticket <= q_generation q`.

- [ticket:] the generation captured by an earlier posted callback.
- [mode1:] the `CommitFailure` policy of the explicit flush.
- [wf1:] the failure predicate during the explicit flush.
- [mode2:] the `CommitFailure` policy active when the stale ticket fires.
- [wf2:] the failure predicate when the stale ticket fires.
- [q:] the live `Queue` state prior to `q_flush`.
*)
Theorem q_stale_ticket_after_flush_noop ticket mode1 wf1 mode2 wf2 q :
  q_disposed q = false ->
  ticket <= q_generation q ->
  let q_flushed := q_flush mode1 wf1 q in
  q_run_posted_ticket ticket mode2 wf2 q_flushed = q_flushed.
Proof.
  intros Hdisp Hle. cbv zeta. unfold q_flush; rewrite Hdisp.
  destruct (snapshot_and_clear (q_pending q) (q_slots q)) as [work slots1].
  destruct (exec_work mode1 wf1 false work slots1 (q_committed q) []) as [[slots2 comm] errs].
  unfold q_run_posted_ticket.
  rewrite q_disposed_request, q_gen_request; simpl.
  assert (Hneq : Nat.eqb ticket (S (q_generation q)) = false) by (apply Nat.eqb_neq; lia).
  rewrite Hneq; reflexivity.
Qed.

(**md
**Disposing a `Queue` makes every posted flush ticket a no-op.**

`q_dispose` (`Queue.Dispose`) sets `q_disposed = true`, so `q_run_posted_ticket` leaves the disposed state untouched.

- [ticket:] the generation captured by any posted flush callback.
- [mode:] the `CommitFailure` policy active when the callback runs.
- [wf:] the failure predicate when the callback runs.
- [q:] the `Queue` state before `q_dispose`.
*)
Theorem q_ticket_after_dispose_noop ticket mode wf q :
  q_run_posted_ticket ticket mode wf (q_dispose q) = q_dispose q.
Proof.
  unfold q_dispose, q_run_posted_ticket.
  destruct (q_disposed q) eqn:Hd; simpl; [rewrite Hd; reflexivity | reflexivity].
Qed.

(**md
**Once `FailFastCancel` stops a flush, no remaining queued slot commits or records an error.**

With `stopped = true`, `exec_work` (`Queue.Flush`) leaves `committed` and `errs` unchanged while invoking `Dropped` on still-current slots.

- [mode:] the `CommitFailure` mode of the flush.
- [wf:] the action failure predicate.
- [work:] the remaining `(slot, version, action)` entries in the flush snapshot.
- [slots:] the current slot array.
- [committed:] the actions already committed before the failure.
- [errs:] the `(scope, error)` pairs already recorded before stopping.
*)
Theorem exec_work_stopped_commits_nothing mode wf work slots committed errs :
  let '(_, committed', errs') := exec_work mode wf true work slots committed errs in
  committed' = committed /\ errs' = errs.
Proof.
  revert slots committed errs.
  induction work as [|[[idx ver] act_opt] rest IH]; simpl; intros slots committed errs; [split; reflexivity|].
  destruct (nth_error slots idx) as [sl|]; [apply IH | apply IH].
Qed.

(**md
**Checking only snapshot version equality misses `Slot.Dropped` when `FailFastCancel` cancels an in-flush replacement.**

Counterexample (Issue #175): when an earlier commit in the flush re-enqueued `sl` (`sl_version = S ver`, `sl_action = Some new_act`), the unfixed guard `has_commit && Nat.eqb (sl_version sl) ver` evaluates to `false` even though `Cancel` clears `sl_action`.

- [ver:] the version recorded for the slot when the flush snapshot was taken.
- [old_act:] the snapshot commit superseded during the flush.
- [new_act:] the replacement commit enqueued onto the slot before `FailFastCancel` stopped the flush.
*)
Theorem naive_failfast_reenqueue_skips_dropped_falsified (ver old_act new_act : nat) :
  let has_commit := match Some old_act with Some _ => true | None => false end in
  let sl_ver := S ver in
  let sl_act := Some new_act in
  sl_act <> None /\ (has_commit && Nat.eqb sl_ver ver) = false.
Proof.
  simpl. split; [discriminate | apply Nat.eqb_neq; lia].
Qed.

(**md
**A stopped `FailFastCancel` flush fires `Slot.Dropped` if and only if cancelling the slot drops an active commit.**

Whether the slot still holds its unreplaced snapshot commit (`act_opt = Some _` and `sl_version = ver`) or holds an in-flush replacement commit (`sl_action = Some _`), `exec_work` clears `sl_action` to `None` and increments `sl_dropped_runs` iff `sl_has_dropped_cb` is set; an already-cancelled slot (`sl_version <> ver` and `sl_action = None`) is not notified.

- [mode:] the `CommitFailure` policy of the flush.
- [wf:] the action failure predicate.
- [ver:] the snapshot version of slot `0`.
- [act_opt:] the snapshot action of slot `0`.
- [sl:] the current state of slot `0` when the stopped loop visits it.
*)
Theorem exec_work_stopped_drops_iff_active_commit mode wf ver act_opt sl :
  sl_has_dropped_cb sl = true ->
  let active :=
    (match act_opt with Some _ => true | None => false end && Nat.eqb (sl_version sl) ver) ||
    match sl_action sl with Some _ => true | None => false end in
  let '(slots', _, _) := exec_work mode wf true [(0, ver, act_opt)] [sl] [] [] in
  match slots' with
  | [sl'] =>
    sl_action sl' = None /\
    sl_dropped_runs sl' = sl_dropped_runs sl + (if active then 1 else 0)
  | _ => False
  end.
Proof.
  intros Hcb. simpl. rewrite Hcb, andb_true_r.
  split; reflexivity.
Qed.

Lemma report_by_scope_aux_nodup seen rem :
  (forall sc, In sc (map fst (report_by_scope_aux seen rem)) -> sc <> 0 /\ ~ In sc seen) /\
  NoDup (map fst (report_by_scope_aux seen rem)).
Proof.
  revert seen. induction rem as [|[sc err] rest IH]; simpl; intros seen.
  - split; [contradiction | constructor].
  - destruct (Nat.eqb sc 0) eqn:Hsc0; simpl.
    + exact (IH seen).
    + destruct (existsb (Nat.eqb sc) seen) eqn:Hseen; simpl.
      * exact (IH seen).
      * destruct (IH (sc :: seen)) as [Hmem Hnd].
        apply Nat.eqb_neq in Hsc0.
        assert (Hnot_seen : ~ In sc seen).
        { intros Hin.
          assert (Htrue : existsb (Nat.eqb sc) seen = true)
            by (apply existsb_exists; exists sc; split; [exact Hin | apply Nat.eqb_refl]).
          congruence. }
        split.
        -- intros x [<- | Hx]; [exact (conj Hsc0 Hnot_seen)|].
           destruct (Hmem x Hx) as [Hx0 Hx_nseen].
           split; [exact Hx0 | intros Hin; apply Hx_nseen; right; exact Hin].
        -- constructor; [| exact Hnd].
           intros Hin. destruct (Hmem sc Hin) as [_ Hsc_n]; apply Hsc_n; left; reflexivity.
Qed.

(**md
**`Queue.Report` groups flush failures so each non-null `ViewScope` is notified at most once.**

`report_by_scope` (`Queue.Report`) drops scope `0` (null scope) and produces duplicate-free scope batches (`NoDup`).

- [failures:] the `(scope_id, error_id)` pairs recorded during a flush.
*)
Theorem queue_report_groups_each_scope_once failures :
  NoDup (map fst (report_by_scope failures)) /\
  (forall sc, In sc (map fst (report_by_scope failures)) -> sc <> 0).
Proof.
  unfold report_by_scope.
  destruct (report_by_scope_aux_nodup [] failures) as [Hmem Hnd].
  split; [exact Hnd | intros sc Hsc; exact (proj1 (Hmem sc Hsc))].
Qed.

(** ** `OwnerWorkQueue` admission, accounting, and no-lost-wakeup *)

Inductive owq_admission :=
  | AdmAccepted
  | AdmFull
  | AdmClosed.

Record owq_model := {
  owq_items : list nat;
  owq_stopped : bool;
  owq_has_wake : bool;
  owq_wakes : nat;
  owq_accepted : nat;
  owq_dequeued : nat;
  owq_rejected : nat;
  owq_discarded : nat
}.

Definition init_owq : owq_model :=
  {| owq_items := [];
     owq_stopped := false;
     owq_has_wake := false;
     owq_wakes := 0;
     owq_accepted := 0;
     owq_dequeued := 0;
     owq_rejected := 0;
     owq_discarded := 0 |}.

Definition owq_try_enqueue (item capacity : nat) (q : owq_model) : owq_admission * owq_model :=
  if owq_stopped q then (AdmClosed, q)
  else if Nat.leb capacity (length (owq_items q)) then
    (AdmFull,
     {| owq_items := owq_items q;
        owq_stopped := false;
        owq_has_wake := owq_has_wake q;
        owq_wakes := owq_wakes q;
        owq_accepted := owq_accepted q;
        owq_dequeued := owq_dequeued q;
        owq_rejected := S (owq_rejected q);
        owq_discarded := owq_discarded q |})
  else
    let was_empty := match owq_items q with [] => true | _ => false end in
    let notify := if was_empty && owq_has_wake q then 1 else 0 in
    (AdmAccepted,
     {| owq_items := owq_items q ++ [item];
        owq_stopped := false;
        owq_has_wake := owq_has_wake q;
        owq_wakes := owq_wakes q + notify;
        owq_accepted := S (owq_accepted q);
        owq_dequeued := owq_dequeued q;
        owq_rejected := owq_rejected q;
        owq_discarded := owq_discarded q |}).

Definition owq_try_dequeue (q : owq_model) : option nat * owq_model :=
  match owq_items q with
  | [] => (None, q)
  | x :: rest =>
    (Some x,
     {| owq_items := rest;
        owq_stopped := owq_stopped q;
        owq_has_wake := owq_has_wake q;
        owq_wakes := owq_wakes q;
        owq_accepted := owq_accepted q;
        owq_dequeued := S (owq_dequeued q);
        owq_rejected := owq_rejected q;
        owq_discarded := owq_discarded q |})
  end.

Definition owq_set_wake (has_cb : bool) (q : owq_model) : owq_model :=
  if owq_stopped q then q
  else
    let non_empty := match owq_items q with [] => false | _ => true end in
    let notify := if non_empty && has_cb then 1 else 0 in
    {| owq_items := owq_items q;
       owq_stopped := false;
       owq_has_wake := has_cb;
       owq_wakes := owq_wakes q + notify;
       owq_accepted := owq_accepted q;
       owq_dequeued := owq_dequeued q;
       owq_rejected := owq_rejected q;
       owq_discarded := owq_discarded q |}.

Definition owq_stop (q : owq_model) : owq_model :=
  {| owq_items := [];
     owq_stopped := true;
     owq_has_wake := false;
     owq_wakes := owq_wakes q;
     owq_accepted := owq_accepted q;
     owq_dequeued := owq_dequeued q;
     owq_rejected := owq_rejected q;
     owq_discarded := owq_discarded q + length (owq_items q) |}.

Definition owq_conserved (q : owq_model) : Prop :=
  owq_accepted q = owq_dequeued q + owq_discarded q + length (owq_items q).

(**md
**Every `OwnerWorkQueue` operation conserves `accepted = dequeued + discarded + Count`.**

`owq_try_enqueue`, `owq_try_dequeue`, `owq_set_wake`, and `owq_stop` (`OwnerWorkQueue.fs`) neither lose nor fabricate accepted work items.

- [item:] the work item passed to `TryEnqueue`.
- [cap:] the queue capacity limit checked by `TryEnqueue`.
- [has_cb:] whether `SetWakeCallback` installs `Some` callback or `None`.
- [q:] the `OwnerWorkQueue` state before the operation.
*)
Theorem owq_operations_preserve_conservation item cap has_cb q :
  owq_conserved q ->
  owq_conserved (snd (owq_try_enqueue item cap q)) /\
  owq_conserved (snd (owq_try_dequeue q)) /\
  owq_conserved (owq_set_wake has_cb q) /\
  owq_conserved (owq_stop q).
Proof.
  destruct q as [items stp wk wks acc deq rej dsc].
  unfold owq_conserved, owq_try_enqueue, owq_try_dequeue, owq_set_wake, owq_stop; simpl.
  intros Hc. repeat split.
  - destruct stp; simpl; [exact Hc|].
    destruct (Nat.leb cap (length items)); simpl; [exact Hc|].
    rewrite List.length_app; simpl; lia.
  - destruct items as [|x rest]; simpl in *; lia.
  - destruct stp; simpl; exact Hc.
  - simpl; lia.
Qed.

(**md
**Either interleaving of `TryEnqueue` and `SetWakeCallback` on an open queue fires a wakeup.**

Whether `SetWakeCallback` registers before `TryEnqueue` or `TryEnqueue` enqueues before `SetWakeCallback` attaches, `owq_wakes` equals `1` (`OwnerWorkQueue.fs`).

- [item:] the work item enqueued onto `init_owq`.
- [cap:] the positive capacity bound (`cap >= 1`).
*)
Theorem owq_no_lost_wakeup item cap :
  cap >= 1 ->
  let q_wake_first := snd (owq_try_enqueue item cap (owq_set_wake true init_owq)) in
  let q_enq_first := owq_set_wake true (snd (owq_try_enqueue item cap init_owq)) in
  owq_wakes q_wake_first = 1 /\ owq_wakes q_enq_first = 1.
Proof.
  intros Hcap. unfold owq_try_enqueue, owq_set_wake, init_owq; simpl.
  assert (Hleb : Nat.leb cap 0 = false) by (apply Nat.leb_gt; lia).
  rewrite Hleb; simpl. split; reflexivity.
Qed.

(** ** `CompletionFence` GPU marker lifecycle *)

Inductive fence_poll :=
  | PollNotReady
  | PollReady
  | PollGpuFailure.

Inductive fence_outcome :=
  | FenceOk (done : bool)
  | FenceError.

Record fence_model := {
  cf_pending : option nat;
  cf_failed : bool;
  cf_next_marker : nat;
  cf_released : list nat
}.

Definition init_fence : fence_model :=
  {| cf_pending := None;
     cf_failed := false;
     cf_next_marker := 1;
     cf_released := [] |}.

Definition cf_try_complete (poll : fence_poll) (f : fence_model) : fence_outcome * fence_model :=
  if cf_failed f then (FenceError, f)
  else
    let '(marker, next_m) :=
      match cf_pending f with
      | Some m => (m, cf_next_marker f)
      | None => (cf_next_marker f, S (cf_next_marker f))
      end in
    match poll with
    | PollNotReady =>
      (FenceOk false,
       {| cf_pending := Some marker;
          cf_failed := false;
          cf_next_marker := next_m;
          cf_released := cf_released f |})
    | PollReady =>
      (FenceOk true,
       {| cf_pending := None;
          cf_failed := false;
          cf_next_marker := next_m;
          cf_released := cf_released f ++ [marker] |})
    | PollGpuFailure =>
      (FenceError,
       {| cf_pending := None;
          cf_failed := true;
          cf_next_marker := next_m;
          cf_released := cf_released f ++ [marker] |})
    end.

Definition cf_reset (f : fence_model) : fence_outcome * fence_model :=
  match cf_pending f with
  | Some _ => (FenceError, f)
  | None =>
    (FenceOk true,
     {| cf_pending := None;
        cf_failed := false;
        cf_next_marker := cf_next_marker f;
        cf_released := cf_released f |})
  end.

Definition fence_inv (f : fence_model) : Prop :=
  NoDup (cf_released f) /\
  Forall (fun r => r < cf_next_marker f) (cf_released f) /\
  (match cf_pending f with
   | None => True
   | Some m => m < cf_next_marker f /\ ~ In m (cf_released f) /\ cf_failed f = false
   end).

(**md
**`CompletionFence.TryComplete` releases each GPU marker at most once and clears `pending` on `GpuCompletionFailure`.**

`cf_try_complete` (`CompletionFence.TryComplete`) preserves `NoDup (cf_released)` across `PollNotReady`, `PollReady`, and `PollGpuFailure`, and latches `cf_failed = true` with `cf_pending = None` on `PollGpuFailure`.

- [poll:] the GPU completion poll result (`PollNotReady`, `PollReady`, or `PollGpuFailure`).
- [f:] the `CompletionFence` state before `TryComplete`.
*)
Theorem cf_try_complete_preserves_inv poll f :
  fence_inv f ->
  fence_inv (snd (cf_try_complete poll f)) /\
  (poll = PollGpuFailure -> cf_failed f = false ->
   cf_pending (snd (cf_try_complete poll f)) = None /\
   cf_failed (snd (cf_try_complete poll f)) = true).
Proof.
  destruct f as [pend fl nxt rel].
  unfold fence_inv, cf_try_complete; simpl.
  intros [Hnd [Hlt Hpend]].
  destruct fl.
  - split; [exact (conj Hnd (conj Hlt Hpend)) | intros _ Hfalse; discriminate].
  - assert (Halloc :
      let '(marker, next_m) :=
        match pend with
        | Some m => (m, nxt)
        | None => (nxt, S nxt)
        end in
      marker < next_m /\ ~ In marker rel /\
      Forall (fun r => r < next_m) rel).
    { destruct pend as [m|].
      - destruct Hpend as [Hm_lt [Hm_nin _]]. repeat split; assumption.
      - repeat split; [lia| |].
        + intros Hin. rewrite Forall_forall in Hlt. specialize (Hlt _ Hin); lia.
        + eapply Forall_impl; [| exact Hlt]; simpl; lia. }
    destruct (match pend with
              | Some m => (m, nxt)
              | None => (nxt, S nxt)
              end) as [marker next_m].
    destruct Halloc as [Hm_lt [Hm_nin Hlt_next]].
    assert (Hnd_snoc : NoDup (rel ++ [marker])).
    { clear -Hnd Hm_nin. induction Hnd as [|x xs Hx Hnd' IH]; simpl.
      - constructor; [intros [] | constructor].
      - constructor.
        + intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | [-> | []]];
          [exact (Hx Hin) | apply Hm_nin; left; reflexivity].
        + apply IH; intros Hin; apply Hm_nin; right; exact Hin. }
    assert (Hlt_snoc : Forall (fun r => r < next_m) (rel ++ [marker])).
    { apply Forall_app; split; [exact Hlt_next | constructor; [exact Hm_lt | constructor]]. }
    destruct poll; simpl.
    + split; [repeat split; try assumption; reflexivity | intros H; discriminate].
    + split; [repeat split; try assumption; trivial | intros H; discriminate].
    + split; [repeat split; try assumption; trivial | intros _ _; split; reflexivity].
Qed.

(**md
**`CompletionFence.Reset` rejects in-flight GPU work and preserves marker uniqueness.**

`cf_reset` (`CompletionFence.Reset`) returns `FenceError` whenever `cf_pending <> None` and clears `cf_failed` otherwise without touching released markers.

- [f:] the `CompletionFence` state before `Reset`.
*)
Theorem cf_reset_preserves_inv_and_rejects_pending f :
  fence_inv f ->
  fence_inv (snd (cf_reset f)) /\
  (cf_pending f <> None -> fst (cf_reset f) = FenceError).
Proof.
  destruct f as [pend fl nxt rel].
  unfold fence_inv, cf_reset; simpl.
  intros [Hnd [Hlt Hpend]].
  destruct pend as [m|]; simpl.
  - split; [exact (conj Hnd (conj Hlt Hpend)) | intros _; reflexivity].
  - split; [repeat split; try assumption; trivial | intros H; contradiction].
Qed.

(** ** Batch V4: Reusable `Queue.Flush` work buffer and zero-allocation hoisted slot commits *)

Definition work_entry := (nat * nat * option nat)%type.

Fixpoint overwrite_prefix (items : list work_entry) (buf : list (option work_entry))
    : list (option work_entry) :=
  match items, buf with
  | [], rest => rest
  | x :: xs, [] => Some x :: overwrite_prefix xs []
  | x :: xs, _ :: ys => Some x :: overwrite_prefix xs ys
  end.

Fixpoint exec_buf_prefix (count : nat) (mode : commit_failure) (will_fail : nat -> bool)
    (reenq : list (nat * nat * nat)) (stopped : bool)
    (buf : list (option work_entry))
    (slots : list slot_model) (pend : list nat)
    (committed : list nat) (errs : list (nat * nat))
    : list (option work_entry) * list slot_model * list nat * list nat * list (nat * nat) :=
  match count, buf with
  | 0, _ | _, [] => (buf, slots, pend, committed, errs)
  | S k, None :: rest_buf =>
    let '(rest_buf', slots', pend', committed', errs') :=
      exec_buf_prefix k mode will_fail reenq stopped rest_buf slots pend committed errs in
    (None :: rest_buf', slots', pend', committed', errs')
  | S k, Some (idx, ver, act_opt) :: rest_buf =>
    match nth_error slots idx with
    | None =>
      let '(rest_buf', slots', pend', committed', errs') :=
        exec_buf_prefix k mode will_fail reenq stopped rest_buf slots pend committed errs in
      (None :: rest_buf', slots', pend', committed', errs')
    | Some sl =>
      let has_commit := match act_opt with Some _ => true | None => false end in
      if stopped then
        let has_replacement := match sl_action sl with Some _ => true | None => false end in
        let dropped := (has_commit && Nat.eqb (sl_version sl) ver) || has_replacement in
        let sl' := {| sl_version := S (sl_version sl);
                      sl_listed := sl_listed sl;
                      sl_action := None;
                      sl_has_dropped_cb := sl_has_dropped_cb sl;
                      sl_dropped_runs := sl_dropped_runs sl +
                        (if dropped && sl_has_dropped_cb sl then 1 else 0);
                      sl_scope := sl_scope sl |} in
        let '(rest_buf', slots', pend', committed', errs') :=
          exec_buf_prefix k mode will_fail reenq true rest_buf (set_slot idx sl' slots) pend committed errs in
        (None :: rest_buf', slots', pend', committed', errs')
      else
        match act_opt with
        | Some act =>
          if Nat.eqb (sl_version sl) ver then
            let '(slots_after, pend_after) := apply_reenqueues act reenq slots pend in
            if will_fail act then
              let stop_now := match mode with FailFastCancel => true | CollectAndContinue => false end in
              let '(rest_buf', slots', pend', committed', errs') :=
                exec_buf_prefix k mode will_fail reenq stop_now rest_buf slots_after pend_after committed
                  (errs ++ [(sl_scope sl, act)]) in
              (None :: rest_buf', slots', pend', committed', errs')
            else
              let '(rest_buf', slots', pend', committed', errs') :=
                exec_buf_prefix k mode will_fail reenq false rest_buf slots_after pend_after
                  (committed ++ [act]) errs in
              (None :: rest_buf', slots', pend', committed', errs')
          else
            let '(rest_buf', slots', pend', committed', errs') :=
              exec_buf_prefix k mode will_fail reenq false rest_buf slots pend committed errs in
            (None :: rest_buf', slots', pend', committed', errs')
        | None =>
          let '(rest_buf', slots', pend', committed', errs') :=
            exec_buf_prefix k mode will_fail reenq false rest_buf slots pend committed errs in
          (None :: rest_buf', slots', pend', committed', errs')
        end
    end
  end.

Definition q_flush_reenq_reusable (mode : commit_failure) (will_fail : nat -> bool)
    (reenq : list (nat * nat * nat))
    (buf : list (option work_entry)) (q : queue_model)
    : list (option work_entry) * queue_model :=
  if q_disposed q then (buf, q)
  else
    let '(work, slots1) := snapshot_and_clear (q_pending q) (q_slots q) in
    let staged := overwrite_prefix work buf in
    let '(buf_after, slots2, pend2, committed', errs') :=
      exec_buf_prefix (length work) mode will_fail reenq false staged slots1 [] (q_committed q) [] in
    (buf_after,
     q_request
       {| q_slots := slots2;
          q_pending := pend2;
          q_disposed := false;
          q_scheduled := false;
          q_generation := S (q_generation q);
          q_tickets := q_tickets q;
          q_committed := committed';
          q_errors := errs' |}).

Lemma exec_buf_prefix_overwrite_equiv :
  forall (work : list work_entry) (buf : list (option work_entry))
         mode will_fail reenq stopped slots pend committed errs,
    let '(buf_after, slots', pend', committed', errs') :=
      exec_buf_prefix (length work) mode will_fail reenq stopped
        (overwrite_prefix work buf) slots pend committed errs in
    (slots', pend', committed', errs') =
      exec_work_reenq mode will_fail reenq stopped work slots pend committed errs /\
    firstn (length work) buf_after = repeat None (length work).
Proof.
  induction work as [|[[idx ver] act_opt] rest IH]; intros buf mode will_fail reenq stopped slots pend committed errs.
  - destruct buf; simpl; split; reflexivity.
  - unfold work_entry in *.
    assert (Hov : exists tail_buf, overwrite_prefix ((idx, ver, act_opt) :: rest) buf =
                                   Some (idx, ver, act_opt) :: overwrite_prefix rest tail_buf).
    { destruct buf as [|b bs]; [exists [] | exists bs]; reflexivity. }
    destruct Hov as [tail_buf Hov]; rewrite Hov.
    simpl length. simpl exec_buf_prefix. simpl exec_work_reenq.
    destruct (nth_error slots idx) as [sl|].
    + destruct stopped.
      * specialize (IH tail_buf mode will_fail reenq true
          (set_slot idx
             {| sl_version := S (sl_version sl);
                sl_listed := sl_listed sl;
                sl_action := None;
                sl_has_dropped_cb := sl_has_dropped_cb sl;
                sl_dropped_runs := sl_dropped_runs sl +
                  (if ((match act_opt with Some _ => true | None => false end &&
                        Nat.eqb (sl_version sl) ver) ||
                       match sl_action sl with Some _ => true | None => false end) &&
                      sl_has_dropped_cb sl then 1 else 0);
                sl_scope := sl_scope sl |} slots)
          pend committed errs).
        destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
          as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
        destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
      * destruct act_opt as [act|].
        -- destruct (Nat.eqb (sl_version sl) ver).
           ++ destruct (apply_reenqueues act reenq slots pend) as [slots_after pend_after].
              destruct (will_fail act).
              ** specialize (IH tail_buf mode will_fail reenq
                   (match mode with FailFastCancel => true | CollectAndContinue => false end)
                   slots_after pend_after committed (errs ++ [(sl_scope sl, act)])).
                 destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
                   as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
                 destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
              ** specialize (IH tail_buf mode will_fail reenq false
                   slots_after pend_after (committed ++ [act]) errs).
                 destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
                   as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
                 destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
           ++ specialize (IH tail_buf mode will_fail reenq false slots pend committed errs).
              destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
                as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
              destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
        -- specialize (IH tail_buf mode will_fail reenq false slots pend committed errs).
           destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
             as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
           destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
    + specialize (IH tail_buf mode will_fail reenq stopped slots pend committed errs).
      destruct (exec_buf_prefix (length rest) _ _ _ _ _ _ _ _ _)
        as [[[[buf_r slots_r] pend_r] comm_r] errs_r].
      destruct IH as [Heq Hclr]; split; [exact Heq | simpl; f_equal; exact Hclr].
Qed.

(**md
**Reusing a grow-only non-reentrant `work` buffer in `Scheduling.Queue.Flush` is observationally identical to allocating a fresh array per flush and clears every active slot reference.**

Because `Queue.Flush` prohibits re-entrancy (`if flushing then raise`), staging `pending[0 .. count - 1]` into a persistent `work` array regardless of stale trailing entries and clearing `work[i] <- default` upon reading each slot yields the exact `queue_model` state of `q_flush_reenq` while leaving all `count` staged positions `None`.

- [mode:] `CollectAndContinue` or `FailFastCancel`.
- [will_fail:] predicate identifying commit actions that raise.
- [reenq:] in-flush re-enqueues triggered during commit execution.
- [buf:] arbitrary residual `work` buffer left over from previous flushes.
- [q:] the `Scheduling.Queue` state before `Flush`.
*)
Theorem q_flush_reusable_work_buffer_equiv mode will_fail reenq buf q :
  let '(buf_after, q_after) := q_flush_reenq_reusable mode will_fail reenq buf q in
  q_after = q_flush_reenq mode will_fail reenq q /\
  (q_disposed q = false ->
   let count := length (fst (snapshot_and_clear (q_pending q) (q_slots q))) in
   firstn count buf_after = repeat None count).
Proof.
  unfold q_flush_reenq_reusable, q_flush_reenq, work_entry in *.
  destruct (q_disposed q).
  - split; [reflexivity | intros H; discriminate].
  - destruct (snapshot_and_clear (q_pending q) (q_slots q)) as [work slots1] eqn:Hsnap.
    pose proof (exec_buf_prefix_overwrite_equiv work buf mode will_fail reenq false
                  slots1 [] (q_committed q) []) as Hbuf.
    destruct (exec_buf_prefix (length work) mode will_fail reenq false
                (overwrite_prefix work buf) slots1 [] (q_committed q) [])
      as [[[[buf_after slots2] pend2] committed'] errs'] eqn:Hexec.
    unfold work_entry in Hbuf; rewrite Hexec in Hbuf.
    destruct Hbuf as [Heq Hclr].
    rewrite <- Heq.
    split; [reflexivity | intros _; simpl; exact Hclr].
Qed.

(** Hoisted `Binding.startQueuedIn` commit & cleanup state machine vs per-update closures. *)
Section QueuedBindingHoisting.
  Context {V : Type} (eqb_val : V -> V -> bool).

  Inductive binding_event :=
    | BEvUpdate (read_res : option V) (reenter_on_first : option (option V))
    | BEvFlush (reenter_on_flush : option (option V))
    | BEvFailFastDrop
    | BEvDispose.

  Record b_spec_state := {
    bs_first : bool;
    bs_version : nat;
    bs_live : bool;
    bs_committed : option V;
    bs_queued : option (nat * V);
    bs_writes : list V;
    bs_after_runs : nat
  }.

  Record b_hoisted_state := {
    bh_first : bool;
    bh_version : nat;
    bh_live : bool;
    bh_committed : option V;
    bh_commit_ver : nat;
    bh_pending_val : option V;
    bh_slot_active : bool;
    bh_writes : list V;
    bh_after_runs : nat
  }.

  Definition init_b_spec : b_spec_state :=
    {| bs_first := true;
       bs_version := 0;
       bs_live := true;
       bs_committed := None;
       bs_queued := None;
       bs_writes := [];
       bs_after_runs := 0 |}.

  Definition init_b_hoisted : b_hoisted_state :=
    {| bh_first := true;
       bh_version := 0;
       bh_live := true;
       bh_committed := None;
       bh_commit_ver := 0;
       bh_pending_val := None;
       bh_slot_active := false;
       bh_writes := [];
       bh_after_runs := 0 |}.

  (** Nested reactive trigger fired synchronously from inside [write value] (when [first] is already [false]). *)
  Definition b_spec_nested (re : option (option V)) (s : b_spec_state) : b_spec_state :=
    match re with
    | None => s
    | Some None =>
      if negb (bs_live s) then s
      else
        {| bs_first := false;
           bs_version := S (bs_version s);
           bs_live := bs_live s;
           bs_committed := bs_committed s;
           bs_queued := None;
           bs_writes := bs_writes s;
           bs_after_runs := bs_after_runs s |}
    | Some (Some v2) =>
      if negb (bs_live s) then s
      else
        let ver' := S (bs_version s) in
        {| bs_first := false;
           bs_version := ver';
           bs_live := bs_live s;
           bs_committed := bs_committed s;
           bs_queued := Some (ver', v2);
           bs_writes := bs_writes s;
           bs_after_runs := bs_after_runs s |}
    end.

  Definition b_spec_run_commit (current : nat) (val0 : V) (re : option (option V))
      (s : b_spec_state) : b_spec_state :=
    if bs_live s && Nat.eqb (bs_version s) current then
      let changed :=
        match bs_committed s with
        | None => true
        | Some prev => negb (eqb_val prev val0)
        end in
      let s_after_write :=
        if changed then
          let s_written :=
            {| bs_first := bs_first s;
               bs_version := bs_version s;
               bs_live := bs_live s;
               bs_committed := bs_committed s;
               bs_queued := bs_queued s;
               bs_writes := bs_writes s ++ [val0];
               bs_after_runs := bs_after_runs s |} in
          let s_re := b_spec_nested re s_written in
          if Nat.eqb (bs_version s_re) current then
            {| bs_first := bs_first s_re;
               bs_version := bs_version s_re;
               bs_live := bs_live s_re;
               bs_committed := Some val0;
               bs_queued := bs_queued s_re;
               bs_writes := bs_writes s_re;
               bs_after_runs := bs_after_runs s_re |}
          else s_re
        else s in
      {| bs_first := bs_first s_after_write;
         bs_version := bs_version s_after_write;
         bs_live := bs_live s_after_write;
         bs_committed := bs_committed s_after_write;
         bs_queued := bs_queued s_after_write;
         bs_writes := bs_writes s_after_write;
         bs_after_runs := S (bs_after_runs s_after_write) |}
    else s.

  Definition step_b_spec (ev : binding_event) (s : b_spec_state) : b_spec_state :=
    match ev with
    | BEvUpdate read_res re =>
      if negb (bs_live s) then s
      else
        let current := S (bs_version s) in
        let was_first := bs_first s in
        (* Spec unconditionally cancels slot at top of effect *)
        let s1 := {| bs_first := was_first;
                     bs_version := current;
                     bs_live := bs_live s;
                     bs_committed := bs_committed s;
                     bs_queued := None;
                     bs_writes := bs_writes s;
                     bs_after_runs := bs_after_runs s |} in
        match read_res with
        | None => s1
        | Some v =>
          if was_first then
            let s2 := {| bs_first := false;
                         bs_version := current;
                         bs_live := bs_live s1;
                         bs_committed := bs_committed s1;
                         bs_queued := None;
                         bs_writes := bs_writes s1;
                         bs_after_runs := bs_after_runs s1 |} in
            b_spec_run_commit current v re s2
          else
            {| bs_first := false;
               bs_version := current;
               bs_live := bs_live s1;
               bs_committed := bs_committed s1;
               bs_queued := Some (current, v);
               bs_writes := bs_writes s1;
               bs_after_runs := bs_after_runs s1 |}
        end
    | BEvFlush re =>
      match bs_queued s with
      | None => s
      | Some (cur, val0) =>
        let s_cleared := {| bs_first := bs_first s;
                            bs_version := bs_version s;
                            bs_live := bs_live s;
                            bs_committed := bs_committed s;
                            bs_queued := None;
                            bs_writes := bs_writes s;
                            bs_after_runs := bs_after_runs s |} in
        b_spec_run_commit cur val0 re s_cleared
      end
    | BEvFailFastDrop =>
      match bs_queued s with
      | None => s
      | Some _ =>
        {| bs_first := bs_first s;
           bs_version := bs_version s;
           bs_live := bs_live s;
           bs_committed := bs_committed s;
           bs_queued := None;
           bs_writes := bs_writes s;
           bs_after_runs := S (bs_after_runs s) |}
      end
    | BEvDispose =>
      {| bs_first := bs_first s;
         bs_version := bs_version s;
         bs_live := false;
         bs_committed := bs_committed s;
         bs_queued := None;
         bs_writes := bs_writes s;
         bs_after_runs := bs_after_runs s |}
    end.

  Definition bh_cancel_slot (h : b_hoisted_state) : b_hoisted_state :=
    {| bh_first := bh_first h;
       bh_version := bh_version h;
       bh_live := bh_live h;
       bh_committed := bh_committed h;
       bh_commit_ver := bh_commit_ver h;
       bh_pending_val := None;
       bh_slot_active := false;
       bh_writes := bh_writes h;
       bh_after_runs := bh_after_runs h |}.

  Definition b_hoisted_nested (re : option (option V)) (h : b_hoisted_state) : b_hoisted_state :=
    match re with
    | None => h
    | Some None =>
      if negb (bh_live h) then h
      else
        bh_cancel_slot
          {| bh_first := false;
             bh_version := S (bh_version h);
             bh_live := bh_live h;
             bh_committed := bh_committed h;
             bh_commit_ver := bh_commit_ver h;
             bh_pending_val := bh_pending_val h;
             bh_slot_active := bh_slot_active h;
             bh_writes := bh_writes h;
             bh_after_runs := bh_after_runs h |}
    | Some (Some v2) =>
      if negb (bh_live h) then h
      else
        let ver' := S (bh_version h) in
        {| bh_first := false;
           bh_version := ver';
           bh_live := bh_live h;
           bh_committed := bh_committed h;
           bh_commit_ver := ver';
           bh_pending_val := Some v2;
           bh_slot_active := true;
           bh_writes := bh_writes h;
           bh_after_runs := bh_after_runs h |}
    end.

  (** Single hoisted [commit] + [untrackedWrite] closure: snapshots [expected := bh_commit_ver]
      and [val_opt := bh_pending_val] onto stack locals before calling [equals] or [write]. *)
  Definition b_hoisted_run_commit (re : option (option V)) (h : b_hoisted_state) : b_hoisted_state :=
    let expected := bh_commit_ver h in
    if bh_live h && Nat.eqb (bh_version h) expected then
      match bh_pending_val h with
      | None => h
      | Some val0 =>
        let h_taken :=
          {| bh_first := bh_first h;
             bh_version := bh_version h;
             bh_live := bh_live h;
             bh_committed := bh_committed h;
             bh_commit_ver := bh_commit_ver h;
             bh_pending_val := None;
             bh_slot_active := bh_slot_active h;
             bh_writes := bh_writes h;
             bh_after_runs := bh_after_runs h |} in
        let changed :=
          match bh_committed h_taken with
          | None => true
          | Some prev => negb (eqb_val prev val0)
          end in
        let h_after_write :=
          if changed then
            let h_written :=
              {| bh_first := bh_first h_taken;
                 bh_version := bh_version h_taken;
                 bh_live := bh_live h_taken;
                 bh_committed := bh_committed h_taken;
                 bh_commit_ver := bh_commit_ver h_taken;
                 bh_pending_val := bh_pending_val h_taken;
                 bh_slot_active := bh_slot_active h_taken;
                 bh_writes := bh_writes h_taken ++ [val0];
                 bh_after_runs := bh_after_runs h_taken |} in
            let h_re := b_hoisted_nested re h_written in
            if Nat.eqb (bh_version h_re) expected then
              {| bh_first := bh_first h_re;
                 bh_version := bh_version h_re;
                 bh_live := bh_live h_re;
                 bh_committed := Some val0;
                 bh_commit_ver := bh_commit_ver h_re;
                 bh_pending_val := bh_pending_val h_re;
                 bh_slot_active := bh_slot_active h_re;
                 bh_writes := bh_writes h_re;
                 bh_after_runs := bh_after_runs h_re |}
            else h_re
          else h_taken in
        {| bh_first := bh_first h_after_write;
           bh_version := bh_version h_after_write;
           bh_live := bh_live h_after_write;
           bh_committed := bh_committed h_after_write;
           bh_commit_ver := bh_commit_ver h_after_write;
           bh_pending_val := bh_pending_val h_after_write;
           bh_slot_active := bh_slot_active h_after_write;
           bh_writes := bh_writes h_after_write;
           bh_after_runs := S (bh_after_runs h_after_write) |}
      end
    else if Nat.eqb (bh_version h) expected then
      {| bh_first := bh_first h;
         bh_version := bh_version h;
         bh_live := bh_live h;
         bh_committed := bh_committed h;
         bh_commit_ver := bh_commit_ver h;
         bh_pending_val := None;
         bh_slot_active := bh_slot_active h;
         bh_writes := bh_writes h;
         bh_after_runs := bh_after_runs h |}
    else h.

  Definition step_b_hoisted (ev : binding_event) (h : b_hoisted_state) : b_hoisted_state :=
    match ev with
    | BEvUpdate read_res re =>
      if negb (bh_live h) then h
      else
        let current := S (bh_version h) in
        let was_first := bh_first h in
        (* Hoisted skips redundant queue.Cancel on first run before any enqueue *)
        let h1 :=
          if was_first then
            {| bh_first := true;
               bh_version := current;
               bh_live := bh_live h;
               bh_committed := bh_committed h;
               bh_commit_ver := bh_commit_ver h;
               bh_pending_val := bh_pending_val h;
               bh_slot_active := bh_slot_active h;
               bh_writes := bh_writes h;
               bh_after_runs := bh_after_runs h |}
          else
            bh_cancel_slot
              {| bh_first := false;
                 bh_version := current;
                 bh_live := bh_live h;
                 bh_committed := bh_committed h;
                 bh_commit_ver := bh_commit_ver h;
                 bh_pending_val := bh_pending_val h;
                 bh_slot_active := bh_slot_active h;
                 bh_writes := bh_writes h;
                 bh_after_runs := bh_after_runs h |} in
        match read_res with
        | None => h1
        | Some v =>
          if was_first then
            let h2 := {| bh_first := false;
                         bh_version := current;
                         bh_live := bh_live h1;
                         bh_committed := bh_committed h1;
                         bh_commit_ver := current;
                         bh_pending_val := Some v;
                         bh_slot_active := false;
                         bh_writes := bh_writes h1;
                         bh_after_runs := bh_after_runs h1 |} in
            b_hoisted_run_commit re h2
          else
            {| bh_first := false;
               bh_version := current;
               bh_live := bh_live h1;
               bh_committed := bh_committed h1;
               bh_commit_ver := current;
               bh_pending_val := Some v;
               bh_slot_active := true;
               bh_writes := bh_writes h1;
               bh_after_runs := bh_after_runs h1 |}
        end
    | BEvFlush re =>
      if bh_slot_active h then
        let h_cleared :=
          {| bh_first := bh_first h;
             bh_version := bh_version h;
             bh_live := bh_live h;
             bh_committed := bh_committed h;
             bh_commit_ver := bh_commit_ver h;
             bh_pending_val := bh_pending_val h;
             bh_slot_active := false;
             bh_writes := bh_writes h;
             bh_after_runs := bh_after_runs h |} in
        b_hoisted_run_commit re h_cleared
      else h
    | BEvFailFastDrop =>
      if bh_slot_active h then
        let h_can := bh_cancel_slot h in
        {| bh_first := bh_first h_can;
           bh_version := bh_version h_can;
           bh_live := bh_live h_can;
           bh_committed := bh_committed h_can;
           bh_commit_ver := bh_commit_ver h_can;
           bh_pending_val := bh_pending_val h_can;
           bh_slot_active := false;
           bh_writes := bh_writes h_can;
           bh_after_runs := S (bh_after_runs h_can) |}
      else h
    | BEvDispose =>
      let h_can := bh_cancel_slot h in
      {| bh_first := bh_first h_can;
         bh_version := bh_version h_can;
         bh_live := false;
         bh_committed := bh_committed h_can;
         bh_commit_ver := bh_commit_ver h_can;
         bh_pending_val := bh_pending_val h_can;
         bh_slot_active := false;
         bh_writes := bh_writes h_can;
         bh_after_runs := bh_after_runs h_can |}
    end.

  Fixpoint run_b_spec (evs : list binding_event) (s : b_spec_state) : b_spec_state :=
    match evs with
    | [] => s
    | ev :: rest => run_b_spec rest (step_b_spec ev s)
    end.

  Fixpoint run_b_hoisted (evs : list binding_event) (h : b_hoisted_state) : b_hoisted_state :=
    match evs with
    | [] => h
    | ev :: rest => run_b_hoisted rest (step_b_hoisted ev h)
    end.

  Definition binding_sim_inv (s : b_spec_state) (h : b_hoisted_state) : Prop :=
    bh_first h = bs_first s /\
    bh_version h = bs_version s /\
    bh_live h = bs_live s /\
    bh_committed h = bs_committed s /\
    bh_writes h = bs_writes s /\
    bh_after_runs h = bs_after_runs s /\
    (bs_first s = true -> bs_queued s = None /\ bh_slot_active h = false /\ bh_pending_val h = None) /\
    match bs_queued s with
    | None => bh_slot_active h = false /\ bh_pending_val h = None
    | Some (cur, val0) =>
      bh_slot_active h = true /\
      cur = bs_version s /\
      bh_commit_ver h = cur /\
      bh_pending_val h = Some val0
    end.

  Lemma b_run_commit_preserves_inv cur val0 re s h :
    bh_first h = bs_first s ->
    bh_version h = bs_version s ->
    bh_live h = bs_live s ->
    bh_committed h = bs_committed s ->
    bh_writes h = bs_writes s ->
    bh_after_runs h = bs_after_runs s ->
    bs_first s = false ->
    bs_queued s = None ->
    bh_slot_active h = false ->
    cur = bs_version s ->
    bh_commit_ver h = cur ->
    bh_pending_val h = Some val0 ->
    binding_sim_inv (b_spec_run_commit cur val0 re s) (b_hoisted_run_commit re h).
  Proof.
    intros Hfirst Hver Hlive Hcomm Hwr Haft Hnf Hsq Hsa Hcur Hcver Hpval.
    unfold b_spec_run_commit, b_hoisted_run_commit.
    rewrite Hlive, Hver, Hcver, Hcur, Nat.eqb_refl, Hpval.
    destruct (bs_live s) eqn:Hl; simpl.
    - rewrite Hcomm.
      destruct (match bs_committed s with None => true | Some prev => negb (eqb_val prev val0) end) eqn:Hchg.
      + destruct re as [[v2|]|]; simpl; rewrite ?Hl, ?Hver, ?Hnf, ?Hsq, ?Hsa, ?Hwr, ?Hcomm, ?Haft; simpl.
        * assert (Hneq : Nat.eqb (S (bs_version s)) (bs_version s) = false)
            by (apply Nat.eqb_neq; lia).
          rewrite Hneq; unfold binding_sim_inv; simpl.
          repeat split; try reflexivity; try congruence.
        * assert (Hneq : Nat.eqb (S (bs_version s)) (bs_version s) = false)
            by (apply Nat.eqb_neq; lia).
          rewrite Hneq; unfold binding_sim_inv; simpl.
          repeat split; try reflexivity; try congruence.
        * rewrite Nat.eqb_refl; unfold binding_sim_inv; simpl.
          repeat split; try reflexivity; try congruence.
      + unfold binding_sim_inv; simpl.
        rewrite ?Hfirst, ?Hver, ?Hcomm, ?Hwr, ?Haft, ?Hsq, ?Hsa, ?Hl; simpl.
        repeat split; try reflexivity; try congruence.
    - unfold binding_sim_inv; simpl.
      rewrite ?Hfirst, ?Hver, ?Hcomm, ?Hwr, ?Haft, ?Hsq, ?Hsa, ?Hl.
      repeat split; try reflexivity; try congruence.
  Qed.

  Lemma step_b_preserves_inv ev s h :
    binding_sim_inv s h ->
    binding_sim_inv (step_b_spec ev s) (step_b_hoisted ev h).
  Proof.
    intros Hinv.
    pose proof Hinv as [Hfirst [Hver [Hlive [Hcomm [Hwr [Haft [Hinit Hq]]]]]]].
    destruct ev as [read_res re | re | | ].
    - simpl step_b_spec; simpl step_b_hoisted.
      rewrite Hlive.
      destruct (bs_live s) eqn:Hl; simpl; [| exact Hinv].
      destruct read_res as [v|].
      + rewrite Hfirst.
        destruct (bs_first s) eqn:Hbf; simpl.
        * apply b_run_commit_preserves_inv; simpl;
            try assumption; try congruence.
        * unfold binding_sim_inv; simpl.
          rewrite ?Hver, ?Hl, ?Hcomm, ?Hwr, ?Haft.
          repeat split; try reflexivity; try congruence.
      + rewrite Hfirst.
        destruct (bs_first s) eqn:Hbf.
        * destruct (Hinit eq_refl) as [Hsq [Hsa Hpval]].
          unfold binding_sim_inv; simpl.
          rewrite ?Hver, ?Hl, ?Hcomm, ?Hwr, ?Haft, ?Hsa, ?Hpval.
          repeat split; try reflexivity.
        * unfold binding_sim_inv; simpl.
          rewrite ?Hver, ?Hl, ?Hcomm, ?Hwr, ?Haft.
          repeat split; try reflexivity; try congruence.
    - simpl step_b_spec; simpl step_b_hoisted.
      destruct (bs_queued s) as [[cur val0]|] eqn:Hsq.
      + destruct Hq as [Hsa [Hcur [Hcver Hpval]]].
        rewrite Hsa.
        assert (Hnf : bs_first s = false).
        { destruct (bs_first s) eqn:Hbf; [| reflexivity].
          destruct (Hinit eq_refl) as [Hnone _]. discriminate Hnone. }
        apply b_run_commit_preserves_inv; simpl;
          try assumption; try congruence.
      + destruct Hq as [Hsa _]; rewrite Hsa; exact Hinv.
    - simpl step_b_spec; simpl step_b_hoisted.
      destruct (bs_queued s) as [[cur val0]|] eqn:Hsq.
      + destruct Hq as [Hsa _].
        rewrite Hsa.
        assert (Hnf : bs_first s = false).
        { destruct (bs_first s) eqn:Hbf; [| reflexivity].
          destruct (Hinit eq_refl) as [Hnone _]. discriminate Hnone. }
        unfold binding_sim_inv; simpl.
        rewrite ?Hfirst, ?Hver, ?Hlive, ?Hcomm, ?Hwr, ?Haft, ?Hnf.
        repeat split; try reflexivity; try congruence.
      + destruct Hq as [Hsa _]; rewrite Hsa; exact Hinv.
    - simpl step_b_spec; simpl step_b_hoisted.
      unfold binding_sim_inv; simpl.
      rewrite ?Hfirst, ?Hver, ?Hcomm, ?Hwr, ?Haft.
      repeat split; try reflexivity.
  Qed.

  Lemma run_b_preserves_inv evs s h :
    binding_sim_inv s h ->
    binding_sim_inv (run_b_spec evs s) (run_b_hoisted evs h).
  Proof.
    revert s h; induction evs as [|ev rest IH]; intros s h Hinv; simpl.
    - exact Hinv.
    - apply IH, step_b_preserves_inv, Hinv.
  Qed.
End QueuedBindingHoisting.

(**md
**Hoisting `cancelSlot`, `commit`, and `untrackedWrite` in `Binding.startQueuedIn` produces the exact DOM writes, `committed` state, and `afterCommit` calls of per-update closure allocation.**

Skipping the redundant initial `queue.Cancel` before the first enqueue, sharing a single hoisted `cancelSlot` and `commit` closure per queued binding, and snapshotting `(expected := commitVersion, value := pendingValue)` onto stack locals before `equals` / `write` matches closure-per-update execution across coalesced bursts, `NotReady` suspensions, `FailFastCancel` drops, owner disposal, and synchronous re-entrant `write` callbacks.

- [V:] the bound value type.
- [eqb_val:] equality predicate passed to `Binding.startQueuedIn`.
- [evs:] arbitrary sequence of updates, flushes, fail-fast drops, and disposal events.
*)
Theorem binding_hoisted_commit_and_cancel_equiv {V : Type} (eqb_val : V -> V -> bool)
    (evs : list (@binding_event V)) :
  let s_end := run_b_spec eqb_val evs init_b_spec in
  let h_end := run_b_hoisted eqb_val evs init_b_hoisted in
  bh_writes h_end = bs_writes s_end /\
  bh_committed h_end = bs_committed s_end /\
  bh_after_runs h_end = bs_after_runs s_end /\
  (bs_queued s_end = None -> bh_slot_active h_end = false /\ bh_pending_val h_end = None).
Proof.
  assert (Hinit : @binding_sim_inv V (@init_b_spec V) (@init_b_hoisted V)).
  { unfold binding_sim_inv, init_b_spec, init_b_hoisted; simpl.
    repeat split; reflexivity. }
  pose proof (run_b_preserves_inv eqb_val evs (@init_b_spec V) (@init_b_hoisted V) Hinit)
    as [_ [_ [_ [Hcomm [Hwr [Haft [_ Hq]]]]]]].
  split; [exact Hwr | split; [exact Hcomm | split; [exact Haft |]]].
  intros Hnone; rewrite Hnone in Hq; exact Hq.
Qed.

(** Hoisted `TemplateDom.startIsolated` commit closure with stack-copied `pending` slot. *)
Section TemplateIsolatedHoisting.
  Context {V : Type} (eqb_val : V -> V -> bool).

  Inductive iso_step :=
    | IsoRead (val_opt : option V) (reenter : option V).

  Record iso_state := {
    iso_prev : option V;
    iso_pending : option V;
    iso_writes : list V
  }.

  Definition commit_val (v_opt : option V) (prev : option V) (writes : list V)
      : option V * list V :=
    match v_opt with
    | None => (prev, writes)
    | Some v =>
      let changed :=
        match prev with
        | None => true
        | Some p => negb (eqb_val p v)
        end in
      if changed then (Some v, writes ++ [v]) else (prev, writes)
    end.

  (** Spec: fresh closure [fun () -> commit ... value] allocated on every isolated effect run. *)
  Definition step_iso_spec (st : iso_step) (prev : option V) (writes : list V)
      : option V * list V :=
    let '(IsoRead val_opt re) := st in
    match val_opt with
    | None => (prev, writes)
    | Some v =>
      let changed :=
        match prev with
        | None => true
        | Some p => negb (eqb_val p v)
        end in
      if changed then
        let writes1 := writes ++ [v] in
        match re with
        | None => (Some v, writes1)
        | Some v2 =>
          (* Synchronous re-entry inside property setter writes v2, then outer frame stores v *)
          let '(_, writes2) := commit_val (Some v2) prev writes1 in
          (Some v, writes2)
        end
      else (prev, writes)
    end.

  (** Hoisted: single [commitIsolated] closure reads [let v = pending; pending <- None] onto stack before [write]. *)
  Definition step_iso_hoisted (st : iso_step) (s : iso_state) : iso_state :=
    let '(IsoRead val_opt re) := st in
    let s1 := {| iso_prev := iso_prev s; iso_pending := val_opt; iso_writes := iso_writes s |} in
    (* commitIsolated takes pending onto stack and clears iso_pending := None immediately *)
    let taken := iso_pending s1 in
    match taken with
    | None => {| iso_prev := iso_prev s1; iso_pending := None; iso_writes := iso_writes s1 |}
    | Some v =>
      let changed :=
        match iso_prev s1 with
        | None => true
        | Some p => negb (eqb_val p v)
        end in
      if changed then
        let writes1 := iso_writes s1 ++ [v] in
        match re with
        | None => {| iso_prev := Some v; iso_pending := None; iso_writes := writes1 |}
        | Some v2 =>
          (* Nested run sets iso_pending := Some v2 and commitIsolated clears it back to None *)
          let '(_, writes2) := commit_val (Some v2) (iso_prev s1) writes1 in
          {| iso_prev := Some v; iso_pending := None; iso_writes := writes2 |}
        end
      else {| iso_prev := iso_prev s1; iso_pending := None; iso_writes := iso_writes s1 |}
    end.

  Fixpoint run_iso_spec (sts : list iso_step) (prev : option V) (writes : list V)
      : option V * list V :=
    match sts with
    | [] => (prev, writes)
    | st :: rest =>
      let '(prev', writes') := step_iso_spec st prev writes in
      run_iso_spec rest prev' writes'
    end.

  Fixpoint run_iso_hoisted (sts : list iso_step) (s : iso_state) : iso_state :=
    match sts with
    | [] => s
    | st :: rest => run_iso_hoisted rest (step_iso_hoisted st s)
    end.
End TemplateIsolatedHoisting.

(**md
**Hoisting `commitIsolated` in `TemplateDom.startIsolated` produces the exact DOM writes and `previous` value of per-update `Api.untrack` closures while clearing `pending` after every commit.**

Snapshotting `let value = pending` onto the stack and resetting `pending <- unset` before invoking `commit` preserves exact equivalence across normal, suspended, unchanged, and synchronously re-entrant isolated slot writes, with zero per-update closure allocations and zero retained `pending` references.

- [V:] the slot value type.
- [eqb_val:] `slot.Same` equality predicate.
- [sts:] sequence of isolated slot update steps.
*)
Theorem template_hoisted_isolated_commit_equiv {V : Type} (eqb_val : V -> V -> bool)
    (sts : list (@iso_step V)) :
  let '(prev_s, writes_s) := run_iso_spec eqb_val sts None [] in
  let h_end := run_iso_hoisted eqb_val sts {| iso_prev := None; iso_pending := None; iso_writes := [] |} in
  iso_prev h_end = prev_s /\
  iso_writes h_end = writes_s /\
  iso_pending h_end = None.
Proof.
  assert (Hgen : forall (prev : option V) (writes : list V),
    let '(ps, ws) := run_iso_spec eqb_val sts prev writes in
    let he := run_iso_hoisted eqb_val sts {| iso_prev := prev; iso_pending := None; iso_writes := writes |} in
    iso_prev he = ps /\ iso_writes he = ws /\ iso_pending he = None).
  { induction sts as [|[val_opt re] rest IH]; intros prev writes; simpl.
    - repeat split; reflexivity.
    - destruct val_opt as [v|]; simpl.
      + destruct (match prev with None => true | Some p => negb (eqb_val p v) end); simpl.
        * destruct re as [v2|]; simpl.
          -- destruct (match prev with None => true | Some p => negb (eqb_val p v2) end); simpl; apply IH.
          -- apply IH.
        * apply IH.
      + apply IH. }
  apply (Hgen None []).
Qed.
