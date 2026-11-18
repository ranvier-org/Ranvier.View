From stdpp Require Import prelude.
From Stdlib Require Import Arith Lia List Bool.
Import ListNotations.

Local Arguments Nat.eqb : simpl never.
Local Arguments Nat.leb : simpl never.
Local Arguments Nat.ltb : simpl never.

(**md # Hierarchical `Placement` hook ordering, `MountLifetime` teardown, and `TeardownWindow` partitioning

Models `Ranvier.View.Dom.Placement` (`Placement`, `PlacementPass`) and `Ranvier.View.MountLifetime` / `TeardownWindow` (`Lifetime.fs`). *)

(** ** `Placement` container and `PlacementPass` *)

(** Stable insertion of [(g, h)] after all existing entries with group [<= g],
    matching [List.sortBy fst] on forward-accumulated [(group, hook)] pairs. *)
Fixpoint stable_insert_group (g h : nat) (xs : list (nat * nat)) : list (nat * nat) :=
  match xs with
  | [] => [(g, h)]
  | (g', h') :: rest =>
    if Nat.leb g' g then (g', h') :: stable_insert_group g h rest
    else (g, h) :: (g', h') :: rest
  end.

Fixpoint sort_groups_acc (acc rem : list (nat * nat)) : list (nat * nat) :=
  match rem with
  | [] => acc
  | (g, h) :: rs => sort_groups_acc (stable_insert_group g h acc) rs
  end.

Definition sort_groups (xs : list (nat * nat)) : list (nat * nat) :=
  sort_groups_acc [] xs.

Record placement_model := {
  pl_laid_out : bool;
  pl_discarded : bool;
  pl_parent_placed : bool;
  pl_ordered : bool;
  pl_last_group : nat;
  pl_group : nat;
  pl_entered : nat;
  pl_held : list (nat * nat);
  pl_forwarded : list nat;
  pl_executed : list nat;
  pl_to_parent : list nat
}.

Definition init_placement (parent_placed : bool) : placement_model :=
  {| pl_laid_out := false;
     pl_discarded := false;
     pl_parent_placed := parent_placed;
     pl_ordered := true;
     pl_last_group := 0;
     pl_group := 0;
     pl_entered := 0;
     pl_held := [];
     pl_forwarded := [];
     pl_executed := [];
     pl_to_parent := [] |}.

Definition pl_is_placed (p : placement_model) : bool :=
  pl_laid_out p && pl_parent_placed p.

Definition pl_enter (p : placement_model) : placement_model * nat :=
  let prev := pl_group p in
  let next_g := S (pl_entered p) in
  ({| pl_laid_out := pl_laid_out p;
      pl_discarded := pl_discarded p;
      pl_parent_placed := pl_parent_placed p;
      pl_ordered := pl_ordered p;
      pl_last_group := pl_last_group p;
      pl_group := next_g;
      pl_entered := next_g;
      pl_held := pl_held p;
      pl_forwarded := pl_forwarded p;
      pl_executed := pl_executed p;
      pl_to_parent := pl_to_parent p |},
   prev).

Definition pl_leave (prev : nat) (p : placement_model) : placement_model :=
  {| pl_laid_out := pl_laid_out p;
     pl_discarded := pl_discarded p;
     pl_parent_placed := pl_parent_placed p;
     pl_ordered := pl_ordered p;
     pl_last_group := pl_last_group p;
     pl_group := prev;
     pl_entered := pl_entered p;
     pl_held := pl_held p;
     pl_forwarded := pl_forwarded p;
     pl_executed := pl_executed p;
     pl_to_parent := pl_to_parent p |}.

Definition pl_enqueue (hook : nat) (released : bool) (p : placement_model) : placement_model :=
  if pl_discarded p then p
  else if negb (pl_laid_out p) then
    if released then
      {| pl_laid_out := pl_laid_out p;
         pl_discarded := pl_discarded p;
         pl_parent_placed := pl_parent_placed p;
         pl_ordered := pl_ordered p;
         pl_last_group := pl_last_group p;
         pl_group := pl_group p;
         pl_entered := pl_entered p;
         pl_held := pl_held p;
         pl_forwarded := pl_forwarded p ++ [hook];
         pl_executed := pl_executed p;
         pl_to_parent := pl_to_parent p |}
    else
      let ord' := if Nat.ltb (pl_group p) (pl_last_group p) then false else pl_ordered p in
      {| pl_laid_out := pl_laid_out p;
         pl_discarded := pl_discarded p;
         pl_parent_placed := pl_parent_placed p;
         pl_ordered := ord';
         pl_last_group := pl_group p;
         pl_group := pl_group p;
         pl_entered := pl_entered p;
         pl_held := pl_held p ++ [(pl_group p, hook)];
         pl_forwarded := pl_forwarded p;
         pl_executed := pl_executed p;
         pl_to_parent := pl_to_parent p |}
  else if pl_is_placed p then
    {| pl_laid_out := pl_laid_out p;
       pl_discarded := pl_discarded p;
       pl_parent_placed := pl_parent_placed p;
       pl_ordered := pl_ordered p;
       pl_last_group := pl_last_group p;
       pl_group := pl_group p;
       pl_entered := pl_entered p;
       pl_held := pl_held p;
       pl_forwarded := pl_forwarded p;
       pl_executed := pl_executed p ++ [hook];
       pl_to_parent := pl_to_parent p |}
  else
    {| pl_laid_out := pl_laid_out p;
       pl_discarded := pl_discarded p;
       pl_parent_placed := pl_parent_placed p;
       pl_ordered := pl_ordered p;
       pl_last_group := pl_last_group p;
       pl_group := pl_group p;
       pl_entered := pl_entered p;
       pl_held := pl_held p;
       pl_forwarded := pl_forwarded p;
       pl_executed := pl_executed p;
       pl_to_parent := pl_to_parent p ++ [hook] |}.

Definition pl_add (hook : nat) (p : placement_model) : placement_model :=
  pl_enqueue hook false p.

Definition pl_place (p : placement_model) : placement_model :=
  if negb (pl_laid_out p) && negb (pl_discarded p) then
    let own_hooks :=
      if pl_ordered p then map snd (pl_held p)
      else map snd (sort_groups (pl_held p)) in
    let all_hooks := own_hooks ++ pl_forwarded p in
    {| pl_laid_out := true;
       pl_discarded := false;
       pl_parent_placed := pl_parent_placed p;
       pl_ordered := pl_ordered p;
       pl_last_group := pl_last_group p;
       pl_group := pl_group p;
       pl_entered := pl_entered p;
       pl_held := [];
       pl_forwarded := [];
       pl_executed := if pl_parent_placed p then pl_executed p ++ all_hooks else pl_executed p;
       pl_to_parent := if pl_parent_placed p then pl_to_parent p else pl_to_parent p ++ all_hooks |}
  else p.

Definition pl_discard (p : placement_model) : placement_model :=
  {| pl_laid_out := pl_laid_out p;
     pl_discarded := true;
     pl_parent_placed := pl_parent_placed p;
     pl_ordered := pl_ordered p;
     pl_last_group := pl_last_group p;
     pl_group := pl_group p;
     pl_entered := pl_entered p;
     pl_held := [];
     pl_forwarded := [];
     pl_executed := pl_executed p;
     pl_to_parent := pl_to_parent p |}.

Definition pl_held_count (p : placement_model) : nat :=
  length (pl_held p) + length (pl_forwarded p).

(** [PlacementPass] ([Placement.fs:180-220]) *)
Record pass_model := {
  ps_inserted : bool;
  ps_begun : bool;
  ps_placed : bool
}.

Definition init_pass : pass_model :=
  {| ps_inserted := false; ps_begun := false; ps_placed := false |}.

Definition ps_begin (ps : pass_model) : pass_model :=
  {| ps_inserted := ps_begun ps; ps_begun := true; ps_placed := false |}.

Definition ps_place (ps : pass_model) : pass_model :=
  {| ps_inserted := ps_inserted ps; ps_begun := ps_begun ps; ps_placed := true |}.

(** ** [Placement] theorems *)

Fixpoint bounded_nondec (low : nat) (xs : list (nat * nat)) : Prop :=
  match xs with
  | [] => True
  | (g, _) :: rest => low <= g /\ bounded_nondec g rest
  end.

Definition pl_order_inv (p : placement_model) : Prop :=
  pl_ordered p = true ->
  bounded_nondec 0 (pl_held p) /\
  Forall (fun gh => fst gh <= pl_last_group p) (pl_held p).

Lemma stable_insert_at_end g h xs :
  Forall (fun gh => fst gh <= g) xs ->
  stable_insert_group g h xs = xs ++ [(g, h)].
Proof.
  induction xs as [|[g' h'] rest IH]; simpl; [reflexivity|].
  intros Hall. inversion Hall as [|? ? Hle Hrest]; subst; simpl in Hle.
  assert (Hleb : Nat.leb g' g = true) by (apply Nat.leb_le; lia).
  rewrite Hleb. f_equal. apply IH, Hrest.
Qed.

Lemma sort_groups_acc_already_sorted acc rem :
  (forall g h, In (g, h) rem -> Forall (fun gh => fst gh <= g) acc) ->
  bounded_nondec 0 rem ->
  (match rem with [] => True | (g0, _) :: _ => Forall (fun gh => fst gh <= g0) acc end) ->
  sort_groups_acc acc rem = acc ++ rem.
Proof.
  revert acc. induction rem as [|[g h] rs IH]; simpl; intros acc _ Hbnd Hhead.
  - rewrite app_nil_r; reflexivity.
  - destruct Hbnd as [_ Hrs_bnd].
    rewrite (stable_insert_at_end g h acc Hhead).
    assert (Hbnd_rs : forall g1 h1, In (g1, h1) rs -> g <= g1).
    { clear -Hrs_bnd. revert g Hrs_bnd.
      induction rs as [|[ga ha] tl IHt]; simpl; [contradiction|].
      intros g [Hle Htl] g1 h1 [Heq | Hin].
      - injection Heq as <- _; lia.
      - specialize (IHt ga Htl g1 h1 Hin); lia. }
    rewrite IH.
    + rewrite <- app_assoc; reflexivity.
    + intros g1 h1 Hin.
      pose proof (Hbnd_rs g1 h1 Hin) as Hgg1.
      apply Forall_app; split.
      * eapply Forall_impl; [| exact Hhead]; simpl; lia.
      * constructor; [simpl; lia | constructor].
    + destruct rs as [|[ga ha] tl]; simpl in Hrs_bnd |- *; [trivial|].
      destruct Hrs_bnd as [Hle Htl]; split; [lia | exact Htl].
    + destruct rs as [|[g2 h2] tl]; [trivial|].
      pose proof (Hbnd_rs g2 h2 (or_introl eq_refl)) as Hgg2.
      apply Forall_app; split.
      * eapply Forall_impl; [| exact Hhead]; simpl; lia.
      * constructor; [simpl; lia | constructor].
Qed.

(**md
**The `Placement` non-decreasing fast path yields the exact sequence of stable group sorting.**

Whenever `bounded_nondec 0 xs` holds (guaranteed when `pl_ordered = true` in `Placement.fs`), iterating `xs` directly produces the identical `(group, hook)` sequence to `sort_groups xs` (`List.sortBy fst`).

- [xs:] the held `(group, hook)` entries accumulated on the `Placement` container.
*)
Theorem placement_fast_path_eq_stable_sort xs :
  bounded_nondec 0 xs ->
  sort_groups xs = xs.
Proof.
  intros Hbnd. unfold sort_groups.
  rewrite sort_groups_acc_already_sorted; [reflexivity | intros; constructor | exact Hbnd |].
  destruct xs as [|[g h] ?]; constructor.
Qed.

Lemma bounded_nondec_snoc low xs g h :
  bounded_nondec low xs ->
  Forall (fun gh => fst gh <= g) xs ->
  low <= g ->
  bounded_nondec low (xs ++ [(g, h)]).
Proof.
  revert low. induction xs as [|[g1 h1] rest IH]; simpl; intros low Hbnd Hall Hlow.
  - split; [exact Hlow | trivial].
  - destruct Hbnd as [Hl1 Hrest].
    inversion Hall as [|? ? Hg1 Hrest_all]; subst; simpl in Hg1.
    split; [exact Hl1 | apply IH; assumption].
Qed.

(**md
**Adding a hook preserves the `pl_ordered` invariant.**

Whenever `pl_ordered` remains `true` after `pl_add` (`Placement.Add`), `pl_held` remains non-decreasing by group and bounded by `pl_last_group`.

- [hook:] the mounted hook id added at the current instance group.
- [p:] the `Placement` container state before `pl_add`.
*)
Theorem pl_add_preserves_order_inv hook p :
  pl_order_inv p ->
  pl_order_inv (pl_add hook p).
Proof.
  unfold pl_order_inv, pl_add, pl_enqueue.
  intros Hinv Hord'.
  destruct (pl_discarded p); [exact (Hinv Hord')|].
  destruct (negb (pl_laid_out p)); [| destruct (pl_is_placed p); simpl in Hord' |- *; exact (Hinv Hord')].
  simpl in Hord' |- *.
  destruct (Nat.ltb (pl_group p) (pl_last_group p)) eqn:Hlt; [discriminate|].
  apply Nat.ltb_ge in Hlt.
  destruct (Hinv Hord') as [Hbnd Hall].
  assert (Hall_g : Forall (fun gh => fst gh <= pl_group p) (pl_held p)).
  { eapply Forall_impl; [| exact Hall]; simpl; lia. }
  split.
  - apply bounded_nondec_snoc; [exact Hbnd | exact Hall_g | lia].
  - apply Forall_app; split; [exact Hall_g | constructor; [simpl; lia | constructor]].
Qed.

(**md
**Placing a container drains its own ordered hooks strictly before forwarded child hooks.**

`pl_place` (`Placement.Place`) empties `pl_held` and either executes `ordered_own ++ pl_forwarded` immediately (when `pl_parent_placed` is `true`) or forwards that exact sequence to the enclosing parent `Placement`.

- [p:] the unplaced, non-discarded `Placement` state before `pl_place`.
*)
Theorem pl_place_own_before_forwarded p :
  pl_laid_out p = false ->
  pl_discarded p = false ->
  let p' := pl_place p in
  let ordered_own :=
    if pl_ordered p then map snd (pl_held p) else map snd (sort_groups (pl_held p)) in
  pl_held_count p' = 0 /\
  (if pl_parent_placed p then
     pl_executed p' = pl_executed p ++ ordered_own ++ pl_forwarded p /\
     pl_to_parent p' = pl_to_parent p
   else
     pl_to_parent p' = pl_to_parent p ++ ordered_own ++ pl_forwarded p /\
     pl_executed p' = pl_executed p).
Proof.
  intros Hlo Hdisc. unfold pl_place, pl_held_count.
  rewrite Hlo, Hdisc; simpl.
  split; [reflexivity|].
  destruct (pl_parent_placed p); split; rewrite ?app_assoc; reflexivity.
Qed.

(**md
**Discarding a `Placement` drops held hooks and silences all subsequent `Add` and `Place` calls.**

`pl_discard` (`Placement.Discard`) clears held hooks without running or forwarding them, and any later `pl_add` followed by `pl_place` leaves `pl_executed` and `pl_to_parent` unchanged.

- [hook:] any hook id added after `pl_discard`.
- [p:] the `Placement` container state before `pl_discard`.
*)
Theorem pl_discard_drops_held_and_blocks_later hook p :
  let p1 := pl_discard p in
  let p2 := pl_place (pl_add hook p1) in
  pl_held_count p1 = 0 /\
  pl_held_count p2 = 0 /\
  pl_executed p2 = pl_executed p /\
  pl_to_parent p2 = pl_to_parent p.
Proof.
  unfold pl_discard, pl_add, pl_enqueue, pl_place, pl_held_count; simpl.
  rewrite andb_false_r; simpl.
  repeat split; reflexivity.
Qed.

(**md
**`PlacementPass.Inserted` is `false` during the initial pass and `true` on every subsequent pass.**

`ps_begin` (`PlacementPass.Begin`) sets `Inserted = false` before the first `Place` and `Inserted = true` once the initial pass has been placed.
*)
Theorem pass_inserted_flag_exact :
  let p1 := ps_begin init_pass in
  let p2 := ps_begin (ps_place p1) in
  ps_inserted p1 = false /\ ps_inserted p2 = true.
Proof.
  simpl; split; reflexivity.
Qed.

(** ** `MountLifetime` attached-cleanup order and `TeardownWindow` partitioning *)

Inductive lifetime_tree :=
  | LTNode (lt_id : nat) (lt_cleanups : list nat) (lt_children : list lifetime_tree).

Inductive lt_event :=
  | EvOwnerCleanup (lt_id cleanup_id : nat)
  | EvDomRelease (lt_id : nat).

(** Models [MountLifetime.Dispose] on a root lifetime ([Lifetime.fs:356-406]):
    1. Core [Owner.DisposeScope] tears down children and cleanups in reverse acquisition
       order ([rev children], then [rev cleanups]), and runs [Finish] last because
       [MountLifetime.Create] registered [Finish] at acquisition 0.
    2. Each descendant's [Finish] sees an enclosing tearing-down [outer] and enqueues its
       [RunRelease] onto [outer.queued] instead of detaching its DOM nodes immediately.
    3. [outer.Finish -> Complete] runs all [outer.queued] child releases, then [outer.RunRelease]. *)
Fixpoint collect_teardown_sub (t : lifetime_tree) : list lt_event * list lt_event :=
  let fix collect_children_rev (ts : list lifetime_tree) : list lt_event * list lt_event :=
    match ts with
    | [] => ([], [])
    | c :: cs =>
      let '(cl_c, rel_c) := collect_teardown_sub c in
      let '(cl_cs, rel_cs) := collect_children_rev cs in
      (cl_cs ++ cl_c, rel_cs ++ rel_c)
    end in
  match t with
  | LTNode id cleanups children =>
    let '(child_cleanups, queued_releases) := collect_children_rev children in
    let own_cleanups := map (EvOwnerCleanup id) (rev cleanups) in
    (child_cleanups ++ own_cleanups, queued_releases ++ [EvDomRelease id])
  end.

Definition dispose_lifetime_tree (t : lifetime_tree) : list lt_event :=
  let '(cleanups, releases) := collect_teardown_sub t in
  cleanups ++ releases.

Definition is_cleanup_ev (e : lt_event) : bool :=
  match e with EvOwnerCleanup _ _ => true | EvDomRelease _ => false end.

Definition is_release_ev (e : lt_event) : bool :=
  match e with EvOwnerCleanup _ _ => false | EvDomRelease _ => true end.

Fixpoint lt_size (t : lifetime_tree) : nat :=
  let fix list_sz (ts : list lifetime_tree) : nat :=
    match ts with
    | [] => 0
    | c :: cs => S (lt_size c + list_sz cs)
    end in
  match t with
  | LTNode _ _ cs => S (list_sz cs)
  end.

Fixpoint lts_size (ts : list lifetime_tree) : nat :=
  match ts with
  | [] => 0
  | c :: cs => S (lt_size c + lts_size cs)
  end.

Lemma lt_size_unfold id cleanups cs :
  lt_size (LTNode id cleanups cs) = S (lts_size cs).
Proof.
  simpl. apply eq_S. induction cs as [|c cs' IH]; simpl; [reflexivity | lia].
Qed.

Lemma collect_teardown_phases_pure bound :
  (forall t, lt_size t <= bound ->
     let '(cl, rel) := collect_teardown_sub t in
     Forall (fun e => is_cleanup_ev e = true) cl /\
     Forall (fun e => is_release_ev e = true) rel /\
     match rev rel with
     | EvDomRelease last_id :: _ =>
       last_id = match t with LTNode id _ _ => id end
     | _ => False
     end) /\
  (forall ts, lts_size ts <= bound ->
     let fix collect_children_rev (xs : list lifetime_tree) : list lt_event * list lt_event :=
       match xs with
       | [] => ([], [])
       | c :: cs =>
         let '(cl_c, rel_c) := collect_teardown_sub c in
         let '(cl_cs, rel_cs) := collect_children_rev cs in
         (cl_cs ++ cl_c, rel_cs ++ rel_c)
       end in
     let '(cl, rel) := collect_children_rev ts in
     Forall (fun e => is_cleanup_ev e = true) cl /\
     Forall (fun e => is_release_ev e = true) rel).
Proof.
  induction bound as [|b [IHt IHts]].
  - split.
    + intros [id cln cs] Hsz; rewrite lt_size_unfold in Hsz; lia.
    + intros [|c cs] Hsz; [split; constructor | simpl in Hsz; assert (lt_size c >= 1) by (destruct c; simpl; lia); lia].
  - split.
    + intros [id cln cs] Hsz. rewrite lt_size_unfold in Hsz.
      pose proof (IHts cs ltac:(lia)) as Hcs.
      simpl in Hcs |- *.
      destruct ((fix collect_children_rev (xs : list lifetime_tree) : list lt_event * list lt_event :=
                   match xs with
                   | [] => ([], [])
                   | c :: cs0 =>
                     let '(cl_c, rel_c) := collect_teardown_sub c in
                     let '(cl_cs, rel_cs) := collect_children_rev cs0 in
                     (cl_cs ++ cl_c, rel_cs ++ rel_c)
                   end) cs) as [c_cl c_rel].
      destruct Hcs as [Hc_cl Hc_rel].
      repeat split.
      * apply Forall_app; split; [exact Hc_cl|].
        clear; induction (rev cln) as [|x xs IH]; simpl; constructor; [reflexivity | exact IH].
      * apply Forall_app; split; [exact Hc_rel | constructor; [reflexivity | constructor]].
      * rewrite rev_app_distr; simpl; reflexivity.
    + intros [|c cs] Hsz; [split; constructor|].
      simpl in Hsz |- *.
      assert (Hc_ge : lt_size c >= 1) by (destruct c; simpl; lia).
      pose proof (IHt c ltac:(lia)) as Hc.
      pose proof (IHts cs ltac:(lia)) as Hcs.
      simpl in Hc, Hcs.
      destruct (collect_teardown_sub c) as [cl1 rel1].
      destruct ((fix collect_children_rev (xs : list lifetime_tree) : list lt_event * list lt_event :=
                   match xs with
                   | [] => ([], [])
                   | c0 :: cs0 =>
                     let '(cl_c, rel_c) := collect_teardown_sub c0 in
                     let '(cl_cs, rel_cs) := collect_children_rev cs0 in
                     (cl_cs ++ cl_c, rel_cs ++ rel_c)
                   end) cs) as [cl2 rel2].
      destruct Hc as [Hcl1 [Hrel1 _]].
      destruct Hcs as [Hcl2 Hrel2].
      split; apply Forall_app; split; assumption.
Qed.

(**md
**Disposing a `MountLifetime` tree runs every `Owner` cleanup strictly before every DOM release, with the root release last.**

Because `Finish` is registered at acquisition `0` and descendant `Finish` calls defer `RunRelease` onto `outer.queued`, `dispose_lifetime_tree` (`MountLifetime.Dispose`) partitions cleanly into `cleanups ++ releases` so every cleanup runs while its DOM subtree is still attached.

- [t:] the mounted `MountLifetime` hierarchy disposed at its root.
*)
Theorem mount_lifetime_cleanups_precede_all_releases (t : lifetime_tree) :
  exists cleanups releases,
    dispose_lifetime_tree t = cleanups ++ releases /\
    Forall (fun e => is_cleanup_ev e = true) cleanups /\
    Forall (fun e => is_release_ev e = true) releases /\
    match rev releases with
    | EvDomRelease last_id :: _ => last_id = match t with LTNode id _ _ => id end
    | _ => False
    end.
Proof.
  destruct (collect_teardown_phases_pure (lt_size t)) as [Ht _].
  specialize (Ht t (Nat.le_refl _)).
  unfold dispose_lifetime_tree.
  destruct (collect_teardown_sub t) as [cl rel].
  exists cl, rel. split; [reflexivity | exact Ht].
Qed.

(** ** `TeardownWindow.Exclude` error partitioning *)

Definition in_interval (idx : nat) (iv : nat * nat) : bool :=
  Nat.leb (fst iv) idx && Nat.ltb idx (snd iv).

Definition in_any_nested (idx : nat) (nested : list (nat * nat)) : bool :=
  existsb (in_interval idx) nested.

Definition teardown_window_indices (start len : nat) (nested : list (nat * nat)) : list nat :=
  List.filter (fun idx => negb (in_any_nested idx nested)) (seq start len).

Fixpoint disjoint_intervals (ivs : list (nat * nat)) : Prop :=
  match ivs with
  | [] => True
  | iv1 :: rest =>
    (forall idx, in_interval idx iv1 = true -> in_any_nested idx rest = false) /\
    disjoint_intervals rest
  end.

Fixpoint matching_intervals (idx : nat) (ivs : list (nat * nat)) : nat :=
  match ivs with
  | [] => 0
  | iv :: rest => (if in_interval idx iv then 1 else 0) + matching_intervals idx rest
  end.

Lemma disjoint_at_most_one_window idx ivs :
  disjoint_intervals ivs ->
  (in_any_nested idx ivs = true -> matching_intervals idx ivs = 1) /\
  (in_any_nested idx ivs = false -> matching_intervals idx ivs = 0).
Proof.
  induction ivs as [|iv rest IH]; simpl; intros Hdisj.
  - split; [discriminate | reflexivity].
  - destruct Hdisj as [Hhead Hrest].
    destruct (IH Hrest) as [IH_t IH_f].
    destruct (in_interval idx iv) eqn:Hiv; simpl.
    + specialize (Hhead idx Hiv).
      rewrite (IH_f Hhead). split; [reflexivity | discriminate].
    + exact (conj IH_t IH_f).
Qed.

(**md
**Disjoint nested `TeardownWindow` exclusions partition root errors without loss or duplication.**

Every root error index `idx` recorded in `[start, start + len)` belongs either to `teardown_window_indices` (`TeardownWindow.Errors`) or to exactly one excluded nested window.

- [start:] the starting `Root.ErrorCount` of the outer `TeardownWindow`.
- [len:] the number of root errors recorded while the outer window is open.
- [nested:] the disjoint `[first, finish)` intervals excluded by nested `MountLifetime.Capture` windows.
- [idx:] any root error index in `[start, start + len)`.
*)
Theorem teardown_window_exact_partition start len nested idx :
  disjoint_intervals nested ->
  start <= idx < start + len ->
  (if existsb (Nat.eqb idx) (teardown_window_indices start len nested) then 1 else 0) +
  matching_intervals idx nested = 1.
Proof.
  intros Hdisj Hrange.
  destruct (disjoint_at_most_one_window idx nested Hdisj) as [Ht Hf].
  assert (Hin_seq : In idx (seq start len)) by (apply in_seq; exact Hrange).
  unfold teardown_window_indices.
  destruct (existsb (Nat.eqb idx) (List.filter (fun i => negb (in_any_nested i nested)) (seq start len))) eqn:Hex.
  - apply existsb_exists in Hex. destruct Hex as (y & Hy & Heq).
    apply Nat.eqb_eq in Heq; subst y.
    apply filter_In in Hy. destruct Hy as [_ Hnot].
    apply negb_true_iff in Hnot.
    rewrite (Hf Hnot); lia.
  - destruct (in_any_nested idx nested) eqn:Hin.
    + rewrite (Ht eq_refl); lia.
    + exfalso.
      assert (Htrue : existsb (Nat.eqb idx) (List.filter (fun i => negb (in_any_nested i nested)) (seq start len)) = true).
      { apply existsb_exists; exists idx; split.
        - apply filter_In; split; [exact Hin_seq | rewrite Hin; reflexivity].
        - apply Nat.eqb_refl. }
      congruence.
Qed.

(** ** Proof-backed `Placement` and `TeardownWindow` fast paths *)

(** Right-to-left shift step of in-place insertion sort on `Placement.held` (`own[j].group > key`). *)
Fixpoint insert_from_right_rev (g h : nat) (rev_prefix : list (nat * nat)) : list (nat * nat) :=
  match rev_prefix with
  | [] => [(g, h)]
  | (g', h') :: rest =>
    if Nat.ltb g g' then (g', h') :: insert_from_right_rev g h rest
    else (g, h) :: (g', h') :: rest
  end.

Definition insert_from_right (g h : nat) (prefix : list (nat * nat)) : list (nat * nat) :=
  rev (insert_from_right_rev g h (rev prefix)).

Fixpoint in_place_insertion_sort_acc (sorted_prefix rem : list (nat * nat)) : list (nat * nat) :=
  match rem with
  | [] => sorted_prefix
  | (g, h) :: rs => in_place_insertion_sort_acc (insert_from_right g h sorted_prefix) rs
  end.

Definition in_place_insertion_sort (xs : list (nat * nat)) : list (nat * nat) :=
  in_place_insertion_sort_acc [] xs.

Lemma stable_insert_preserves_bounded_nondec low g h xs :
  low <= g ->
  bounded_nondec low xs ->
  bounded_nondec low (stable_insert_group g h xs).
Proof.
  revert low.
  induction xs as [|[g1 h1] rest IH]; simpl; intros low Hlow Hbnd.
  - split; [exact Hlow | trivial].
  - destruct Hbnd as [Hl1 Hrest].
    destruct (Nat.leb g1 g) eqn:Hleb; simpl.
    + apply Nat.leb_le in Hleb.
      split; [exact Hl1 | apply IH; [exact Hleb | exact Hrest]].
    + apply Nat.leb_gt in Hleb.
      split; [exact Hlow | split; [lia | exact Hrest]].
Qed.

Lemma bounded_nondec_weaken low1 low2 xs :
  low1 <= low2 ->
  bounded_nondec low2 xs ->
  bounded_nondec low1 xs.
Proof.
  destruct xs as [|[g h] rest]; simpl; [trivial|].
  intros Hle [H1 H2]; split; [lia | exact H2].
Qed.

Lemma bounded_nondec_app_last low init gl hl :
  bounded_nondec low (init ++ [(gl, hl)]) ->
  bounded_nondec low init /\ Forall (fun gh => fst gh <= gl) init.
Proof.
  revert low.
  induction init as [|[g1 h1] rest IH]; simpl; intros low Hbnd.
  - split; [trivial | constructor].
  - destruct Hbnd as [Hl1 Hrest].
    destruct (IH g1 Hrest) as [Hb_rest Hall_rest].
    split.
    + split; [exact Hl1 | exact Hb_rest].
    + constructor; [|exact Hall_rest].
      destruct rest as [|[g2 h2] tl]; simpl in Hrest |- *.
      * lia.
      * inversion Hall_rest as [|? ? Hg2 _]; subst; simpl in *; lia.
Qed.

Lemma stable_insert_snoc_gt g h init gl hl :
  g < gl ->
  stable_insert_group g h (init ++ [(gl, hl)]) = stable_insert_group g h init ++ [(gl, hl)].
Proof.
  intros Hlt.
  induction init as [|[g1 h1] rest IH]; simpl.
  - assert (Hleb : Nat.leb gl g = false) by (apply Nat.leb_gt; exact Hlt).
    rewrite Hleb; reflexivity.
  - destruct (Nat.leb g1 g); simpl; [rewrite IH; reflexivity | reflexivity].
Qed.

Lemma insert_from_right_eq_stable_insert g h xs :
  bounded_nondec 0 xs ->
  insert_from_right g h xs = stable_insert_group g h xs.
Proof.
  induction xs as [|[gl hl] init IH] using rev_ind; intros Hbnd.
  - reflexivity.
  - destruct (bounded_nondec_app_last 0 init gl hl Hbnd) as [Hb_init Hall_init].
    unfold insert_from_right.
    rewrite rev_app_distr; simpl.
    destruct (Nat.ltb g gl) eqn:Hlt; simpl.
    + apply Nat.ltb_lt in Hlt.
      fold (insert_from_right g h init).
      rewrite (IH Hb_init), (stable_insert_snoc_gt g h init gl hl Hlt).
      reflexivity.
    + apply Nat.ltb_ge in Hlt.
      rewrite rev_involutive.
      symmetry. apply stable_insert_at_end.
      apply Forall_app; split.
      * eapply Forall_impl; [|exact Hall_init]; simpl; lia.
      * constructor; [simpl; exact Hlt | constructor].
Qed.

Lemma in_place_insertion_sort_acc_eq acc rem :
  bounded_nondec 0 acc ->
  in_place_insertion_sort_acc acc rem = sort_groups_acc acc rem.
Proof.
  revert acc.
  induction rem as [|[g h] rs IH]; intros acc Hbnd; simpl; [reflexivity|].
  rewrite (insert_from_right_eq_stable_insert g h acc Hbnd).
  apply IH.
  apply stable_insert_preserves_bounded_nondec; [lia | exact Hbnd].
Qed.

(**md
**In-place right-shifting insertion sort on `Placement.held` equals `sort_groups` (`List.sortBy fst`).**

Sorting `Placement`'s held hooks in place by shifting entries with `group > key` one slot right produces the exact stable group order of `sort_groups` with zero list or closure allocations.

- [xs:] the held `(group, hook)` entries of an unordered `Placement` container.
*)
Theorem in_place_insertion_sort_eq_sort_groups (xs : list (nat * nat)) :
  in_place_insertion_sort xs = sort_groups xs.
Proof.
  unfold in_place_insertion_sort, sort_groups.
  apply in_place_insertion_sort_acc_eq; simpl; trivial.
Qed.

(** Bulk forwarding of ordered hooks from a placed child container into an unplaced ancestor. *)
Definition pl_bulk_forward (hooks : list nat) (parent : placement_model) : placement_model :=
  if pl_discarded parent then parent
  else if negb (pl_laid_out parent) then
    {| pl_laid_out := pl_laid_out parent;
       pl_discarded := pl_discarded parent;
       pl_parent_placed := pl_parent_placed parent;
       pl_ordered := pl_ordered parent;
       pl_last_group := pl_last_group parent;
       pl_group := pl_group parent;
       pl_entered := pl_entered parent;
       pl_held := pl_held parent;
       pl_forwarded := pl_forwarded parent ++ hooks;
       pl_executed := pl_executed parent;
       pl_to_parent := pl_to_parent parent |}
  else if pl_is_placed parent then
    {| pl_laid_out := pl_laid_out parent;
       pl_discarded := pl_discarded parent;
       pl_parent_placed := pl_parent_placed parent;
       pl_ordered := pl_ordered parent;
       pl_last_group := pl_last_group parent;
       pl_group := pl_group parent;
       pl_entered := pl_entered parent;
       pl_held := pl_held parent;
       pl_forwarded := pl_forwarded parent;
       pl_executed := pl_executed parent ++ hooks;
       pl_to_parent := pl_to_parent parent |}
  else
    {| pl_laid_out := pl_laid_out parent;
       pl_discarded := pl_discarded parent;
       pl_parent_placed := pl_parent_placed parent;
       pl_ordered := pl_ordered parent;
       pl_last_group := pl_last_group parent;
       pl_group := pl_group parent;
       pl_entered := pl_entered parent;
       pl_held := pl_held parent;
       pl_forwarded := pl_forwarded parent;
       pl_executed := pl_executed parent;
       pl_to_parent := pl_to_parent parent ++ hooks |}.

(**md
**Bulk-forwarding a placed container's hooks into its parent equals item-by-item `Enqueue(..., true)`.**

Appending `hooks` directly into the unplaced parent's `forwarded` queue (with a single `IsPlaced` check) produces the exact `Placement` state of folding `pl_enqueue h true` across `hooks`.

- [hooks:] the ordered hook sequence released by a child `Placement.Place`.
- [parent:] the enclosing `Placement` state receiving the released hooks.
*)
Theorem pl_bulk_forward_eq_fold_enqueue (hooks : list nat) (parent : placement_model) :
  pl_bulk_forward hooks parent = fold_left (fun p h => pl_enqueue h true p) hooks parent.
Proof.
  revert parent.
  induction hooks as [|h hs IH]; intros parent; simpl.
  - unfold pl_bulk_forward.
    destruct parent as [lo dc pp ord lg g ent hld fwd ex tp]; simpl.
    destruct dc; [reflexivity|].
    destruct (negb lo); [rewrite app_nil_r; reflexivity|].
    unfold pl_is_placed; simpl.
    destruct (lo && pp); rewrite app_nil_r; reflexivity.
  - rewrite <- IH.
    destruct parent as [lo dc pp ord lg g ent hld fwd ex tp].
    destruct lo, dc, pp; unfold pl_bulk_forward, pl_enqueue, pl_is_placed; simpl;
      rewrite <- ?app_assoc; reflexivity.
Qed.

Definition pl_place_fast (p : placement_model) : placement_model :=
  if negb (pl_laid_out p) && negb (pl_discarded p) then
    let own_hooks :=
      if pl_ordered p then map snd (pl_held p)
      else map snd (in_place_insertion_sort (pl_held p)) in
    let all_hooks := own_hooks ++ pl_forwarded p in
    {| pl_laid_out := true;
       pl_discarded := false;
       pl_parent_placed := pl_parent_placed p;
       pl_ordered := pl_ordered p;
       pl_last_group := pl_last_group p;
       pl_group := pl_group p;
       pl_entered := pl_entered p;
       pl_held := [];
       pl_forwarded := [];
       pl_executed := if pl_parent_placed p then pl_executed p ++ all_hooks else pl_executed p;
       pl_to_parent := if pl_parent_placed p then pl_to_parent p else pl_to_parent p ++ all_hooks |}
  else p.

(**md
**Fast-path `Placement.Place` (using in-place insertion sort) is observationally identical to `pl_place`.**

For every `Placement` state `p`, `pl_place_fast p` produces the exact container state of `pl_place p`.

- [p:] any `Placement` container state.
*)
Theorem pl_place_fast_eq_pl_place (p : placement_model) :
  pl_place_fast p = pl_place p.
Proof.
  unfold pl_place_fast, pl_place.
  rewrite in_place_insertion_sort_eq_sort_groups.
  reflexivity.
Qed.

(**md
**An error-free `TeardownWindow` (`len = 0`) returns the empty index list regardless of nested exclusions.**

When `root.ErrorCount = start` (`len = 0`) and no routed errors were added, `TeardownWindow.Errors` can immediately return `[]` with zero allocations.

- [start:] the starting `Root.ErrorCount` of the window.
- [nested:] any nested `[first, finish)` exclusion intervals.
*)
Theorem teardown_window_empty_fast_path (start : nat) (nested : list (nat * nat)) :
  teardown_window_indices start 0 nested = [].
Proof.
  reflexivity.
Qed.

Lemma filter_all_true {A : Type} (xs : list A) :
  List.filter (fun _ => true) xs = xs.
Proof.
  induction xs as [|x xs IH]; simpl; [reflexivity | f_equal; exact IH].
Qed.

(**md
**A `TeardownWindow` with no nested windows (`nested = []`) retains every root error in `[start, start + len)`.**

When `nested` is empty (`isNull nested`), `TeardownWindow.Errors` can copy `recorded` directly without scanning exclusion pairs.

- [start:] the starting `Root.ErrorCount` of the window.
- [len:] the number of root errors recorded while the window was open.
*)
Theorem teardown_window_no_nested_fast_path (start len : nat) :
  teardown_window_indices start len [] = seq start len.
Proof.
  unfold teardown_window_indices, in_any_nested; simpl.
  apply filter_all_true.
Qed.
