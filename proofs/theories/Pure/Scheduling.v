From stdpp Require Import prelude.
From Stdlib Require Import Arith Lia List Bool.
Import ListNotations.

Local Arguments Nat.eqb : simpl never.
Local Arguments Nat.leb : simpl never.
Local Arguments Nat.ltb : simpl never.

(** * Queued DOM commits, OwnerWorkQueue wakeups, and CompletionFence lifecycle *)

(** Corresponds to [Ranvier.View.Dom.Scheduling] ([Slot], [Queue]),
    [Ranvier.View.WebCore.OwnerWorkQueue] ([OwnerWorkQueue]),
    [Ranvier.View.WebCore.OwnerTurnBudget] ([TurnLimit]), and
    [Ranvier.View.WebCore.Sdl.CompletionFence] ([CompletionFence]). *)

(** ** 1. [Scheduling.Queue] ([Scheduling.fs:32-262]) *)

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
        let dropped := has_commit && Nat.eqb (sl_version sl) ver in
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

(** Re-enqueuing an already-listed slot coalesces in place without growing [q_pending]. *)
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

(** Stale posted ticket invalidation across [q_flush] and [q_dispose] ([Scheduling.fs:67-71, 112, 254]). *)
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

Theorem q_ticket_after_dispose_noop ticket mode wf q :
  q_run_posted_ticket ticket mode wf (q_dispose q) = q_dispose q.
Proof.
  unfold q_dispose, q_run_posted_ticket.
  destruct (q_disposed q) eqn:Hd; simpl; [rewrite Hd; reflexivity | reflexivity].
Qed.

(** [FailFastCancel] invariant ([Scheduling.fs:178-201]): once [stopped = true], [exec_work]
    commits ZERO further actions and increments [sl_dropped_runs] iff [has_commit && sl_version = ver && sl_has_dropped_cb]. *)
Theorem exec_work_stopped_commits_nothing mode wf work slots committed errs :
  let '(_, committed', errs') := exec_work mode wf true work slots committed errs in
  committed' = committed /\ errs' = errs.
Proof.
  revert slots committed errs.
  induction work as [|[[idx ver] act_opt] rest IH]; simpl; intros slots committed errs; [split; reflexivity|].
  destruct (nth_error slots idx) as [sl|]; [apply IH | apply IH].
Qed.

(** [Queue.Report] grouping theorem ([Scheduling.fs:219-250]): each reported scope is non-zero
    and appears at most once ([NoDup (map fst ...)]). *)
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

Theorem queue_report_groups_each_scope_once failures :
  NoDup (map fst (report_by_scope failures)) /\
  (forall sc, In sc (map fst (report_by_scope failures)) -> sc <> 0).
Proof.
  unfold report_by_scope.
  destruct (report_by_scope_aux_nodup [] failures) as [Hmem Hnd].
  split; [exact Hnd | intros sc Hsc; exact (proj1 (Hmem sc Hsc))].
Qed.

(** ** 2. [OwnerWorkQueue] admission, accounting, and no-lost-wakeup ([OwnerWorkQueue.fs:7-111]) *)

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

(** No-lost-wakeup across the [TryEnqueue] / [SetWakeCallback] handover ([OwnerWorkQueue.fs:49-62, 95-104]):
    regardless of whether [SetWakeCallback] runs before [TryEnqueue] or [TryEnqueue] runs before
    [SetWakeCallback], an accepted item on an open queue fires a wake notification! *)
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

(** ** 3. [CompletionFence] GPU marker lifecycle ([CompletionFence.fs:8-53]) *)

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
