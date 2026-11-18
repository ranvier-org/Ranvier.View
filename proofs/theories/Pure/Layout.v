From stdpp Require Import prelude.
From Stdlib Require Import Arith Lia List Bool ZArith.
Import ListNotations.

(**md # DOM sibling layout, LIS row placement, and `Departures` interleaving

Models the sibling-list reconciliation and exit-animation state machines in `Ranvier.View.Dom.Mounting` (`increasingRun`, `Row.Layout`, `Row.ClearBetween`, `RegionSite.FlushReleased`) and `Ranvier.View.Departures` (`nextLive`, `holdsBetween`, `retire`, `before`). *)

(** ** DOM sibling-list primitives *)

Fixpoint remove_node (x : nat) (xs : list nat) : list nat :=
  match xs with
  | [] => []
  | y :: ys => if Nat.eqb y x then remove_node x ys else y :: remove_node x ys
  end.

Fixpoint insert_at_anchor (x anchor : nat) (xs : list nat) : list nat :=
  match xs with
  | [] => [x]
  | y :: ys =>
    if Nat.eqb y anchor then x :: y :: ys
    else y :: insert_at_anchor x anchor ys
  end.

(** Models [parent.insertBefore(x, anchor)]: detaches [x] from its current sibling
    position and inserts it immediately before [anchor]. *)
Definition move_before (x anchor : nat) (dom : list nat) : list nat :=
  if Nat.eqb x anchor then dom
  else insert_at_anchor x anchor (remove_node x dom).

Fixpoint move_many_before (rows : list nat) (anchor : nat) (dom : list nat) : list nat :=
  match rows with
  | [] => dom
  | r :: rs => move_many_before rs anchor (move_before r anchor dom)
  end.

(** ** Right-to-left placement outside a kept run ([Mounting.fs:1533-1544]) *)

(** Processes [(row, keep)] pairs from right to left starting at [anchor], returning
    the new [before] anchor and updated sibling list, matching the backward loop:
    [offset = count - 1 .. 0] in [Row.Layout]. *)
Fixpoint place_rtl_step (pairs : list (nat * bool)) (anchor : nat) (dom : list nat) : nat * list nat :=
  match pairs with
  | [] => (anchor, dom)
  | (r, k) :: rest =>
    let '(before, dom') := place_rtl_step rest anchor dom in
    let dom'' := if k then dom' else move_before r before dom' in
    (r, dom'')
  end.

Definition place_rtl (rows : list nat) (keeps : list bool) (anchor : nat) (dom : list nat) : list nat :=
  snd (place_rtl_step (combine rows keeps) anchor dom).

(** ** Longest increasing run of non-negative source positions ([Mounting.fs:626-665]) *)

(** Binary search for the first tail index [low] in [0 .. length tails] where
    [nth (nth low tails 0) sources (-1) >= val_x]. *)
Fixpoint lower_bound_tails (sources : list Z) (tails : list nat) (val_x : Z) : nat :=
  match tails with
  | [] => 0
  | t :: ts =>
    if Z.ltb (nth t sources (-1)%Z) val_x
    then S (lower_bound_tails sources ts val_x)
    else 0
  end.

Fixpoint list_set {A : Type} (idx : nat) (v : A) (xs : list A) : list A :=
  match idx, xs with
  | _, [] => []
  | 0, _ :: ys => v :: ys
  | S k, y :: ys => y :: list_set k v ys
  end.

Definition update_tails (low idx : nat) (tails : list nat) : list nat :=
  if Nat.eqb low (length tails) then tails ++ [idx]
  else list_set low idx tails.

Fixpoint lis_scan (sources : list Z) (idx : nat) (rem : list Z)
    (tails : list nat) (prev : list (option nat)) : list nat * list (option nat) :=
  match rem with
  | [] => (tails, prev)
  | v :: vs =>
    if Z.geb v 0%Z then
      let low := lower_bound_tails sources tails v in
      let pred := match low with 0 => None | S k => nth_error tails k end in
      let prev' := list_set idx pred prev in
      let tails' := update_tails low idx tails in
      lis_scan sources (S idx) vs tails' prev'
    else
      lis_scan sources (S idx) vs tails prev
  end.

Fixpoint mark_chain (fuel : nat) (cursor : option nat) (prev : list (option nat)) (keep : list bool) : list bool :=
  match fuel, cursor with
  | 0, _ | _, None => keep
  | S f, Some c =>
    let next_c := match nth_error prev c with Some p => p | None => None end in
    mark_chain f next_c prev (list_set c true keep)
  end.

Definition increasing_run (sources : list Z) : list bool :=
  let n := length sources in
  let init_prev := repeat (@None nat) n in
  let init_keep := repeat false n in
  let '(tails, prev) := lis_scan sources 0 sources [] init_prev in
  let start := match rev tails with [] => None | last_t :: _ => Some last_t end in
  mark_chain n start prev init_keep.

(** ** Window framing and [Row.Layout] ([Mounting.fs:1450-1568]) *)

Fixpoint common_prefix_len (a b : list nat) : nat :=
  match a, b with
  | x :: xs, y :: ys => if Nat.eqb x y then S (common_prefix_len xs ys) else 0
  | _, _ => 0
  end.

Definition common_suffix_len (a b : list nat) : nat :=
  common_prefix_len (rev a) (rev b).

Definition slice {A : Type} (start len : nat) (xs : list A) : list A :=
  firstn len (skipn start xs).

Fixpoint index_of (x : nat) (base : nat) (xs : list nat) : Z :=
  match xs with
  | [] => (-1)%Z
  | y :: ys => if Nat.eqb x y then Z.of_nat base else index_of x (S base) ys
  end.

Definition is_outer_swap (a_win b_win : list nat) : bool :=
  let count := length b_win in
  Nat.leb 2 count &&
  Nat.eqb (length a_win) count &&
  Nat.eqb (nth 0 a_win 0) (nth (count - 1) b_win 0) &&
  Nat.eqb (nth (count - 1) a_win 0) (nth 0 b_win 0) &&
  forallb (fun off => Nat.eqb (nth off a_win 0) (nth off b_win 0)) (seq 1 (count - 2)).

Definition row_layout (previous next : list nat) (region_end : nat) (dom : list nat) : list nat :=
  let lead := common_prefix_len previous next in
  let prev_rem := skipn lead previous in
  let next_rem := skipn lead next in
  let trail := common_suffix_len prev_rem next_rem in
  let a_len := length prev_rem - trail in
  let b_len := length next_rem - trail in
  if Nat.eqb b_len 0 then dom
  else
    let a_win := firstn a_len prev_rem in
    let b_win := firstn b_len next_rem in
    let anchor :=
      match skipn (lead + b_len) next with
      | r :: _ => r
      | [] => region_end
      end in
    if Nat.eqb a_len 0 then
      move_many_before b_win anchor dom
    else if is_outer_swap a_win b_win then
      let dom1 := move_before (nth 0 b_win 0) (nth 0 a_win 0) dom in
      if Nat.ltb 2 b_len then move_before (nth (b_len - 1) b_win 0) anchor dom1
      else dom1
    else
      let sources := map (fun r => index_of r lead a_win) b_win in
      let keeps := increasing_run sources in
      place_rtl b_win keeps anchor dom.

(** ** Departures state machine ([Departures.fs:285-562]) & [FlushReleased] ([Mounting.fs:1880-1915]) *)

Record dep_state := {
  dep_exits : list nat;
  dep_leaving : list nat;
  dep_pending : list nat
}.

Definition mem_nat (x : nat) (xs : list nat) : bool :=
  existsb (Nat.eqb x) xs.

Fixpoint filter_live (leaving : list nat) (dom : list nat) : list nat :=
  match dom with
  | [] => []
  | x :: xs =>
    if mem_nat x leaving then filter_live leaving xs
    else x :: filter_live leaving xs
  end.

Fixpoint next_live_after (leaving : list nat) (target : nat) (dom : list nat) : option nat :=
  match dom with
  | [] => None
  | x :: xs =>
    if Nat.eqb x target then
      head (filter_live leaving xs)
    else next_live_after leaving target xs
  end.

Fixpoint between_Exclusive (start finish : nat) (dom : list nat) : list nat :=
  match dom with
  | [] => []
  | x :: xs =>
    if Nat.eqb x start then
      let fix take_until (ys : list nat) : list nat :=
        match ys with
        | [] => []
        | y :: zs => if Nat.eqb y finish then [] else y :: take_until zs
        end
      in take_until xs
    else between_Exclusive start finish xs
  end.

Definition holds_between (d : dep_state) (start finish : nat) (dom : list nat) : bool :=
  existsb (fun n => mem_nat n (dep_leaving d) || mem_nat n (dep_exits d))
    (between_Exclusive start finish dom).

(** Retires a removed row root: moves it from [exits]/[pending] into [leaving] while keeping
    it physically in [dom]; if it has no exit, detaches it from [dom] immediately. *)
Definition retire_or_remove (n : nat) (d : dep_state) (dom : list nat) : dep_state * list nat :=
  if mem_nat n (dep_exits d) then
    ({| dep_exits := remove_node n (dep_exits d);
        dep_leaving := if mem_nat n (dep_leaving d) then dep_leaving d else n :: dep_leaving d;
        dep_pending := remove_node n (dep_pending d) |},
     dom)
  else
    ({| dep_exits := dep_exits d;
        dep_leaving := dep_leaving d;
        dep_pending := remove_node n (dep_pending d) |},
     remove_node n dom).

(** Finishes a held exit: removes [n] from [leaving] and detaches [n] from [dom]. *)
Definition finish_exit (n : nat) (d : dep_state) (dom : list nat) : dep_state * list nat :=
  ({| dep_exits := dep_exits d;
      dep_leaving := remove_node n (dep_leaving d);
      dep_pending := dep_pending d |},
   remove_node n dom).

Fixpoint clear_between (start finish : nat) (dom : list nat) : list nat :=
  match dom with
  | [] => []
  | x :: xs =>
    if Nat.eqb x start then
      let fix skip_to_end (ys : list nat) : list nat :=
        match ys with
        | [] => []
        | y :: zs => if Nat.eqb y finish then y :: zs else skip_to_end zs
        end
      in x :: skip_to_end xs
    else x :: clear_between start finish xs
  end.

Fixpoint release_rows (rows : list nat) (d : dep_state) (dom : list nat) : dep_state * list nat :=
  match rows with
  | [] => (d, dom)
  | r :: rs =>
    let '(d', dom') := retire_or_remove r d dom in
    release_rows rs d' dom'
  end.

Definition flush_released (cleared : bool) (start finish : nat)
    (rows : list nat) (d : dep_state) (dom : list nat) : dep_state * list nat :=
  if cleared && negb (holds_between d start finish dom) then
    (d, clear_between start finish dom)
  else
    release_rows rows d dom.

(** Reflow measurement gate ([Departures.fs:276-283, 469-497]). *)
Record reflow_state := {
  rf_open : bool;
  rf_posts : nat;
  rf_before_runs : nat;
  rf_after_runs : nat
}.

Definition reflow_before (s : reflow_state) : reflow_state * nat :=
  let s1 :=
    if rf_open s then s
    else {| rf_open := true;
            rf_posts := rf_posts s;
            rf_before_runs := S (rf_before_runs s);
            rf_after_runs := rf_after_runs s |} in
  let ticket := S (rf_posts s1) in
  ({| rf_open := rf_open s1;
      rf_posts := ticket;
      rf_before_runs := rf_before_runs s1;
      rf_after_runs := rf_after_runs s1 |},
   ticket).

Definition reflow_run_post (ticket : nat) (s : reflow_state) : reflow_state :=
  if rf_open s && Nat.eqb (rf_posts s) ticket then
    {| rf_open := false;
       rf_posts := rf_posts s;
       rf_before_runs := rf_before_runs s;
       rf_after_runs := S (rf_after_runs s) |}
  else s.

(** ** Metatheory and invariants *)

Lemma remove_node_not_in x xs :
  ~ In x xs -> remove_node x xs = xs.
Proof.
  induction xs as [|y ys IH]; simpl; [done|].
  intros Hnin.
  destruct (Nat.eqb y x) eqn:Heq.
  - apply Nat.eqb_eq in Heq; subst. exfalso; apply Hnin; left; reflexivity.
  - f_equal. apply IH. intros Hin; apply Hnin; right; exact Hin.
Qed.

Lemma remove_node_app x a b :
  remove_node x (a ++ b) = remove_node x a ++ remove_node x b.
Proof.
  induction a as [|y ys IH]; simpl; [done|].
  destruct (Nat.eqb y x); simpl; [exact IH | f_equal; exact IH].
Qed.

Lemma remove_node_in_iff x y xs :
  In y (remove_node x xs) <-> In y xs /\ y <> x.
Proof.
  induction xs as [|z zs IH]; simpl.
  - split; [contradiction | intros [[] _]].
  - destruct (Nat.eqb z x) eqn:Heq.
    + apply Nat.eqb_eq in Heq; subst z.
      rewrite IH. split.
      * intros [Hin Hne]; split; [right; exact Hin | exact Hne].
      * intros [[-> | Hin] Hne]; [contradiction | split; assumption].
    + apply Nat.eqb_neq in Heq. simpl. rewrite IH. split.
      * intros [-> | [Hin Hne]]; [split; [left; reflexivity | exact Heq] | split; [right; exact Hin | exact Hne]].
      * intros [[-> | Hin] Hne]; [left; reflexivity | right; split; assumption].
Qed.

Lemma insert_at_anchor_split x anchor pre suf :
  ~ In anchor pre ->
  insert_at_anchor x anchor (pre ++ anchor :: suf) = pre ++ x :: anchor :: suf.
Proof.
  induction pre as [|y ys IH]; simpl; intros Hnin.
  - rewrite Nat.eqb_refl. reflexivity.
  - destruct (Nat.eqb y anchor) eqn:Heq.
    + apply Nat.eqb_eq in Heq; subst. exfalso; apply Hnin; left; reflexivity.
    + f_equal. apply IH. intros Hin; apply Hnin; right; exact Hin.
Qed.

(** Key commutation theorem: [move_before] on live nodes commutes with [filter_live].
    Held exit-animating nodes in [leaving] never perturb live sibling placement! *)
Lemma filter_live_remove_comm leaving x xs :
  remove_node x (filter_live leaving xs) = filter_live leaving (remove_node x xs).
Proof.
  induction xs as [|y ys IH]; simpl; [done|].
  destruct (Nat.eqb y x) eqn:Heq; destruct (mem_nat y leaving) eqn:Hmem; simpl.
  - exact IH.
  - rewrite Heq. exact IH.
  - rewrite Hmem. exact IH.
  - rewrite Heq, Hmem. f_equal; exact IH.
Qed.

Lemma filter_live_insert_comm leaving x anchor xs :
  mem_nat x leaving = false ->
  mem_nat anchor leaving = false ->
  insert_at_anchor x anchor (filter_live leaving xs) =
  filter_live leaving (insert_at_anchor x anchor xs).
Proof.
  intros Hx Ha.
  induction xs as [|y ys IH]; simpl.
  - rewrite Hx. reflexivity.
  - destruct (Nat.eqb y anchor) eqn:Heq.
    + apply Nat.eqb_eq in Heq; subst y.
      rewrite Ha; simpl. rewrite Nat.eqb_refl, Hx, Ha. reflexivity.
    + simpl. destruct (mem_nat y leaving) eqn:Hy; simpl.
      * exact IH.
      * rewrite Heq. f_equal; exact IH.
Qed.

(** ** Live sibling moves commute with held departures *)

(**md
**Moving a live sibling before a live anchor commutes with filtering out leaving nodes.**

Held exit-animating nodes in `leaving` never perturb the relative sibling order produced by `move_before` (`Mounting.insert`, `Row.Place`).

- [leaving:] the node ids currently held in `Departures` while their exit animations run.
- [x:] the live row node moved.
- [anchor:] the live sibling anchor before which `x` is placed.
- [dom:] the parent's full DOM sibling list before the move.
*)
Theorem move_before_commutes_with_departures leaving x anchor dom :
  mem_nat x leaving = false ->
  mem_nat anchor leaving = false ->
  filter_live leaving (move_before x anchor dom) =
  move_before x anchor (filter_live leaving dom).
Proof.
  intros Hx Ha. unfold move_before.
  destruct (Nat.eqb x anchor); [reflexivity|].
  rewrite <- filter_live_insert_comm by assumption.
  rewrite filter_live_remove_comm. reflexivity.
Qed.

(**md
**Right-to-left LIS placement commutes with held departure siblings.**

Running `place_rtl_step` (`Row.Layout`'s backward placement loop) over a parent sibling list containing held `leaving` nodes yields the exact same live sibling projection and anchor as running it over the live-filtered list.

- [leaving:] the node ids currently held in `Departures`.
- [pairs:] the target `(row, keep)` pairs of the active window.
- [anchor:] the live sibling anchor following the window.
- [dom:] the parent's full DOM sibling list before placement.
*)
Theorem place_rtl_commutes_with_departures leaving pairs anchor dom :
  Forall (fun p => mem_nat (fst p) leaving = false) pairs ->
  mem_nat anchor leaving = false ->
  let '(b1, d1) := place_rtl_step pairs anchor dom in
  let '(b2, d2) := place_rtl_step pairs anchor (filter_live leaving dom) in
  b1 = b2 /\ mem_nat b1 leaving = false /\ filter_live leaving d1 = d2.
Proof.
  induction pairs as [|[r k] rest IH]; simpl; intros Hall Ha.
  - repeat split; assumption || reflexivity.
  - inversion Hall as [|? ? Hr Hrest]; subst; simpl in Hr.
    specialize (IH Hrest Ha).
    destruct (place_rtl_step rest anchor dom) as [b1 d1].
    destruct (place_rtl_step rest anchor (filter_live leaving dom)) as [b2 d2].
    destruct IH as [-> [Hb2 Hd]].
    repeat split; [exact Hr|].
    destruct k; [exact Hd|].
    rewrite move_before_commutes_with_departures by assumption.
    rewrite Hd. reflexivity.
Qed.

(** Ordered subsequence witness for kept rows inside a window. *)
Inductive subseq {A : Type} : list A -> list A -> Prop :=
  | subseq_nil ys : subseq [] ys
  | subseq_cons x xs ys : subseq xs ys -> subseq (x :: xs) (x :: ys)
  | subseq_skip xs y ys : subseq xs ys -> subseq xs (y :: ys).

Fixpoint kept_rows (pairs : list (nat * bool)) : list nat :=
  match pairs with
  | [] => []
  | (r, true) :: rest => r :: kept_rows rest
  | (_, false) :: rest => kept_rows rest
  end.

Lemma remove_node_notin_subseq x ks mid :
  ~ In x ks -> subseq ks mid -> subseq ks (remove_node x mid).
Proof.
  intros Hnin Hsub. revert Hnin. induction Hsub; simpl; intros Hnin.
  - apply subseq_nil.
  - destruct (Nat.eqb x0 x) eqn:Heq.
    + apply Nat.eqb_eq in Heq; subst. exfalso; apply Hnin; left; reflexivity.
    + apply subseq_cons, IHHsub. intros Hin; apply Hnin; right; exact Hin.
  - destruct (Nat.eqb y x); [exact (IHHsub Hnin) | apply subseq_skip, (IHHsub Hnin)].
Qed.

Lemma subseq_split (r : nat) (ks mid : list nat) :
  subseq (r :: ks) mid ->
  ~ In r ks ->
  NoDup mid ->
  exists m1 m2, mid = m1 ++ r :: m2 /\ ~ In r m1 /\ subseq ks m2.
Proof.
  intros Hsub Hnin Hnd.
  remember (r :: ks) as rks eqn:Heq.
  revert r ks Heq Hnin Hnd.
  induction Hsub; intros r0 ks0 Heq Hnin Hnd; [discriminate| |].
  - injection Heq as -> ->.
    inversion Hnd as [|? ? Hx Hnd_ys]; subst.
    exists [], ys. repeat split; [intros [] | exact Hsub].
  - inversion Hnd as [|y0 ys0 Hy Hnd_ys]; subst y0 ys0.
    destruct (IHHsub r0 ks0 Heq Hnin Hnd_ys) as (m1 & m2 & -> & Hnot1 & Hsub2).
    exists (y :: m1), m2. repeat split; [|exact Hsub2].
    intros [-> | Hin]; [apply Hy; rewrite in_app_iff; right; left; reflexivity | exact (Hnot1 Hin)].
Qed.

Lemma remove_node_nodup x xs :
  NoDup xs -> NoDup (remove_node x xs).
Proof.
  induction 1 as [|y ys Hy Hnd IH]; simpl; [constructor|].
  destruct (Nat.eqb y x); [exact IH|].
  constructor; [|exact IH].
  rewrite remove_node_in_iff. intros [Hin _]; exact (Hy Hin).
Qed.

Lemma NoDup_app_inv {A : Type} (l1 l2 : list A) :
  NoDup (l1 ++ l2) -> NoDup l1 /\ NoDup l2 /\ (forall x, In x l1 -> ~ In x l2).
Proof.
  induction l1 as [|a l1 IH]; simpl; intros Hnd.
  - repeat split; [constructor | exact Hnd | contradiction].
  - inversion Hnd as [|? ? Ha Hnd']; subst.
    destruct (IH Hnd') as (H1 & H2 & Hdisj).
    repeat split.
    + constructor; [intros Hin; apply Ha, in_or_app; left; exact Hin | exact H1].
    + exact H2.
    + intros x [-> | Hin] Hin2; [apply Ha, in_or_app; right; exact Hin2 | exact (Hdisj x Hin Hin2)].
Qed.

Lemma NoDup_app_intro {A : Type} (l1 l2 : list A) :
  NoDup l1 -> NoDup l2 -> (forall x, In x l1 -> ~ In x l2) -> NoDup (l1 ++ l2).
Proof.
  induction l1 as [|a l1 IH]; simpl; intros H1 H2 Hdisj; [exact H2|].
  inversion H1 as [|? ? Ha H1']; subst.
  constructor.
  - intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | Hin];
    [exact (Ha Hin) | exact (Hdisj a (or_introl eq_refl) Hin)].
  - apply IH; [exact H1' | exact H2 | intros x Hx; exact (Hdisj x (or_intror Hx))].
Qed.

Lemma subseq_app_r {A : Type} (xs m1 m2 : list A) :
  subseq xs m2 -> subseq xs (m1 ++ m2).
Proof.
  induction m1 as [|y m1 IH]; simpl; [ trivial | intros H; apply subseq_skip, IH, H ].
Qed.

Lemma subseq_app_both {A : Type} (a1 a2 b1 b2 : list A) :
  subseq a1 b1 -> subseq a2 b2 -> subseq (a1 ++ a2) (b1 ++ b2).
Proof.
  intros H1 H2. induction H1; simpl.
  - apply subseq_app_r, H2.
  - apply subseq_cons, IHsubseq.
  - apply subseq_skip, IHsubseq.
Qed.

Lemma subseq_split_middle {A : Type} (ks1 : list A) (x : A) (ks2 mid : list A) :
  subseq (ks1 ++ x :: ks2) mid ->
  ~ In x ks1 ->
  NoDup mid ->
  exists m1 m2, mid = m1 ++ x :: m2 /\ ~ In x m1 /\ subseq ks1 m1 /\ subseq ks2 m2.
Proof.
  revert ks1. induction mid as [|y ys IHmid]; intros ks1 Hsub Hnin Hnd.
  - destruct ks1; simpl in Hsub; inversion Hsub.
  - inversion Hnd as [|? ? Hy Hnd_ys]; subst.
    destruct ks1 as [|k ks1']; simpl in Hsub.
    + inversion Hsub as [| ? ? ? Htail | ? ? ? Hskip]; subst.
      * exists [], ys. repeat split; [intros [] | apply subseq_nil | exact Htail].
      * destruct (IHmid [] Hskip Hnin Hnd_ys) as (m1 & m2 & -> & Hnot1 & _ & Hsub2).
        exists (y :: m1), m2. repeat split; [| apply subseq_nil | exact Hsub2].
        intros [-> | Hin]; [apply Hy, in_or_app; right; left; reflexivity | exact (Hnot1 Hin)].
    + inversion Hsub as [| ? ? ? Htail | ? ? ? Hskip]; subst.
      * assert (~ In x ks1') as Hnin' by (intros Hin; apply Hnin; right; exact Hin).
        assert (x <> y) as Hx_ne_y by (intros ->; apply Hnin; left; reflexivity).
        destruct (IHmid ks1' Htail Hnin' Hnd_ys) as (m1 & m2 & -> & Hnot1 & Hsub1 & Hsub2).
        exists (y :: m1), m2. repeat split; [| apply subseq_cons, Hsub1 | exact Hsub2].
        intros [-> | Hin]; [exact (Hx_ne_y eq_refl) | exact (Hnot1 Hin)].
      * destruct (IHmid (k :: ks1') Hskip Hnin Hnd_ys) as (m1 & m2 & -> & Hnot1 & Hsub1 & Hsub2).
        exists (y :: m1), m2. repeat split; [| apply subseq_skip, Hsub1 | exact Hsub2].
        intros [-> | Hin]; [apply Hy, in_or_app; right; left; reflexivity | exact (Hnot1 Hin)].
Qed.

Lemma subseq_length {A : Type} (xs ys : list A) :
  subseq xs ys -> length xs <= length ys.
Proof.
  induction 1; simpl; lia.
Qed.

Lemma nodup_incl_length (xs ys : list nat) :
  NoDup xs ->
  (forall x, In x xs -> In x ys) ->
  length xs <= length ys.
Proof.
  revert ys. induction xs as [|x xs IH]; simpl; intros ys Hnd Hincl; [lia|].
  inversion Hnd as [|? ? Hx Hnd']; subst.
  assert (Hin : In x ys) by (apply Hincl; left; reflexivity).
  apply in_split in Hin. destruct Hin as (y1 & y2 & ->).
  rewrite List.length_app; simpl.
  assert (Hincl' : forall z, In z xs -> In z (y1 ++ y2)).
  { intros z Hz. assert (z <> x) by (intros ->; exact (Hx Hz)).
    specialize (Hincl z (or_intror Hz)).
    apply in_app_or in Hincl. apply in_or_app.
    destruct Hincl as [H1 | [-> | H2]]; [left; exact H1 | contradiction | right; exact H2]. }
  specialize (IH (y1 ++ y2) Hnd' Hincl').
  rewrite List.length_app in IH. lia.
Qed.

Lemma subseq_equal_length {A : Type} (xs ys : list A) :
  subseq xs ys -> length xs = length ys -> xs = ys.
Proof.
  intros Hsub. induction Hsub; simpl; intros Hlen.
  - destruct ys; [reflexivity | discriminate].
  - f_equal. apply IHHsub. lia.
  - apply subseq_length in Hsub. lia.
Qed.

Lemma place_rtl_step_invariant pairs pre mid anchor suf :
  NoDup (map fst pairs) ->
  NoDup mid ->
  ~ In anchor pre ->
  ~ In anchor mid ->
  (forall r, In r (map fst pairs) -> ~ In r pre /\ r <> anchor /\ ~ In r suf) ->
  forall ks_before,
    NoDup ks_before ->
    (forall k, In k ks_before -> ~ In k (map fst pairs)) ->
    subseq (ks_before ++ kept_rows pairs) mid ->
    exists mid',
      place_rtl_step pairs anchor (pre ++ mid ++ anchor :: suf) =
        (match pairs with [] => anchor | (r, _) :: _ => r end,
         pre ++ mid' ++ anchor :: suf) /\
      NoDup mid' /\
      ~ In anchor mid' /\
      (forall z, In z mid' -> In z mid \/ In z (map fst pairs)) /\
      subseq (ks_before ++ map fst pairs) mid'.
Proof.
  revert mid.
  induction pairs as [|[r k] rest IH]; intros mid Hnd_p Hnd_mid Hanc_pre Hanc_mid Hdisj ks_before Hnd_ks Hks_disj Hsub; simpl in *.
  - exists mid. rewrite app_nil_r in Hsub |- *.
    repeat split; try assumption; try reflexivity.
    intros z Hz; left; exact Hz.
  - inversion Hnd_p as [|? ? Hr_not_rest Hnd_rest]; subst.
    destruct (Hdisj r (or_introl eq_refl)) as (Hr_pre & Hr_anc & Hr_suf).
    assert (Hdisj_rest : forall x, In x (map fst rest) -> ~ In x pre /\ x <> anchor /\ ~ In x suf).
    { intros x Hx; apply Hdisj; right; exact Hx. }
    destruct k.
    + (* Kept row [r] *)
      assert (Hnd_ks' : NoDup (ks_before ++ [r])).
      { apply NoDup_app_intro; [exact Hnd_ks | constructor; [intros [] | constructor] |].
        intros x Hx [-> | []]. exact (Hks_disj x Hx (or_introl eq_refl)). }
      assert (Hks_disj' : forall x, In x (ks_before ++ [r]) -> ~ In x (map fst rest)).
      { intros x Hx Hx_rest. apply in_app_or in Hx.
        destruct Hx as [Hx | [-> | []]]; [exact (Hks_disj x Hx (or_intror Hx_rest)) | exact (Hr_not_rest Hx_rest)]. }
      assert (Hsub' : subseq ((ks_before ++ [r]) ++ kept_rows rest) mid).
      { rewrite <- app_assoc. exact Hsub. }
      destruct (IH mid Hnd_rest Hnd_mid Hanc_pre Hanc_mid Hdisj_rest (ks_before ++ [r]) Hnd_ks' Hks_disj' Hsub')
        as (mid' & Hstep & Hnd_mid' & Hanc_mid' & Hmem' & Hsub_out).
      rewrite Hstep. exists mid'. rewrite <- app_assoc in Hsub_out.
      repeat split; try assumption; try reflexivity.
      intros z Hz. destruct (Hmem' z Hz) as [Hzm | Hzr]; [left; exact Hzm | right; right; exact Hzr].
    + (* Unkept row [r]: moved before [before] *)
      assert (Hks_disj' : forall x, In x ks_before -> ~ In x (map fst rest)).
      { intros x Hx Hx_rest; exact (Hks_disj x Hx (or_intror Hx_rest)). }
      destruct (IH mid Hnd_rest Hnd_mid Hanc_pre Hanc_mid Hdisj_rest ks_before Hnd_ks Hks_disj' Hsub)
        as (mid' & Hstep & Hnd_mid' & Hanc_mid' & Hmem' & Hsub_out).
      rewrite Hstep; clear Hstep.
      set (b := match rest with [] => anchor | (r2, _) :: _ => r2 end).
      assert (Hr_ne_b : r <> b).
      { unfold b. destruct rest as [|[r2 k2] ?]; [exact Hr_anc|].
        intros ->. apply Hr_not_rest; simpl; left; reflexivity. }
      assert (Hrem_pre : remove_node r pre = pre) by (apply remove_node_not_in, Hr_pre).
      assert (Hrem_suf : remove_node r suf = suf) by (apply remove_node_not_in, Hr_suf).
      assert (Hr_not_ks : ~ In r ks_before) by (intros Hin; exact (Hks_disj r Hin (or_introl eq_refl))).
      assert (Hr_not_sub : ~ In r (ks_before ++ map fst rest)).
      { intros Hin; apply in_app_or in Hin; destruct Hin; [contradiction|contradiction]. }
      assert (Hsub_rem : subseq (ks_before ++ map fst rest) (remove_node r mid'))
        by (apply remove_node_notin_subseq; assumption).
      assert (Hnd_rem : NoDup (remove_node r mid')) by (apply remove_node_nodup, Hnd_mid').
      assert (Hanc_rem : ~ In anchor (remove_node r mid')).
      { rewrite remove_node_in_iff. intros [Hin _]; exact (Hanc_mid' Hin). }
      unfold move_before.
      destruct (Nat.eqb r b) eqn:Hrb; [apply Nat.eqb_eq in Hrb; contradiction|].
      rewrite !remove_node_app; simpl.
      destruct (Nat.eqb anchor r) eqn:Har; [apply Nat.eqb_eq in Har; symmetry in Har; contradiction|].
      rewrite Hrem_pre, Hrem_suf.
      destruct rest as [|[r2 k2] rest']; simpl in *.
      * (* [b = anchor] *)
        unfold b.
        assert (Hanc_all : ~ In anchor (pre ++ remove_node r mid')).
        { intros Hin; apply in_app_or in Hin; destruct Hin; contradiction. }
        rewrite app_assoc, insert_at_anchor_split by exact Hanc_all.
        rewrite <- app_assoc.
        exists (remove_node r mid' ++ [r]).
        rewrite app_nil_r in Hsub_rem.
        repeat split.
        -- rewrite <- app_assoc; reflexivity.
        -- apply NoDup_app_intro; [exact Hnd_rem | constructor; [intros [] | constructor] |].
           intros x Hx [-> | []]. rewrite remove_node_in_iff in Hx; destruct Hx as [_ Hne]; exact (Hne eq_refl).
        -- intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | [-> | []]]; [exact (Hanc_rem Hin) | exact (Hr_anc eq_refl)].
        -- intros z Hz; apply in_app_or in Hz; destruct Hz as [Hz | [<- | []]].
           ++ rewrite remove_node_in_iff in Hz; destruct Hz as [Hz _].
              destruct (Hmem' z Hz) as [Hzm | []]; left; exact Hzm.
           ++ right; left; reflexivity.
        -- apply subseq_app_both; [exact Hsub_rem | apply subseq_cons, subseq_nil].
      * (* [b = r2] *)
        unfold b.
        assert (Hr2_not_ks : ~ In r2 ks_before) by (intros Hin; exact (Hks_disj' r2 Hin (or_introl eq_refl))).
        destruct (subseq_split_middle ks_before r2 (map fst rest') (remove_node r mid') Hsub_rem Hr2_not_ks Hnd_rem)
          as (u1 & u2 & Heq_rem & Hr2_u1 & Hsub_u1 & Hsub_u2).
        rewrite Heq_rem.
        assert (Hr2_pre : ~ In r2 pre) by (exact (proj1 (Hdisj_rest r2 (or_introl eq_refl)))).
        assert (Hr2_all : ~ In r2 (pre ++ u1)).
        { intros Hin; apply in_app_or in Hin; destruct Hin; contradiction. }
        assert (pre ++ (u1 ++ r2 :: u2) ++ anchor :: suf = (pre ++ u1) ++ r2 :: (u2 ++ anchor :: suf)) as Hassoc
          by (rewrite <- !app_assoc; simpl; reflexivity).
        rewrite Hassoc, insert_at_anchor_split by exact Hr2_all.
        exists (u1 ++ r :: r2 :: u2).
        repeat split.
        -- f_equal. rewrite <- !app_assoc; simpl; reflexivity.
        -- assert (Hnd_u : NoDup (u1 ++ r2 :: u2)) by (rewrite <- Heq_rem; exact Hnd_rem).
           destruct (NoDup_app_inv u1 (r2 :: u2) Hnd_u) as (Hnd_u1 & Hnd_r2u2 & Hu1_r2u2).
           assert (~ In r (u1 ++ r2 :: u2)) as Hr_u12.
           { rewrite <- Heq_rem, remove_node_in_iff. intros [_ Hne]; exact (Hne eq_refl). }
           apply NoDup_app_intro; [exact Hnd_u1| |].
           ++ constructor; [intros Hin; apply Hr_u12, in_or_app; right; exact Hin | exact Hnd_r2u2].
           ++ intros x Hx [-> | Hin]; [apply Hr_u12, in_or_app; left; exact Hx | exact (Hu1_r2u2 x Hx Hin)].
        -- intros Hin; apply Hanc_rem; rewrite Heq_rem.
           apply in_app_or in Hin; apply in_or_app.
           destruct Hin as [H1 | [<- | H2]]; [left; exact H1 | exfalso; exact (Hr_anc eq_refl) | right; exact H2].
        -- intros z Hz.
           apply in_app_or in Hz. destruct Hz as [Hz | [<- | Hz]].
           ++ assert (Hin_rem : In z (remove_node r mid')) by (rewrite Heq_rem; apply in_or_app; left; exact Hz).
              rewrite remove_node_in_iff in Hin_rem. destruct (Hmem' z (proj1 Hin_rem)) as [Hzm | Hzr];
              [left; exact Hzm | right; right; exact Hzr].
           ++ right; left; reflexivity.
           ++ assert (Hin_rem : In z (remove_node r mid')) by (rewrite Heq_rem; apply in_or_app; right; exact Hz).
              rewrite remove_node_in_iff in Hin_rem. destruct (Hmem' z (proj1 Hin_rem)) as [Hzm | Hzr];
              [left; exact Hzm | right; right; exact Hzr].
        -- apply subseq_app_both; [exact Hsub_u1 | apply subseq_cons, subseq_cons, Hsub_u2].
Qed.

(** ** `Row.Layout` window placement and fast paths *)

(**md
**Right-to-left placement produces the exact target sibling sequence whenever kept rows form an ordered subsequence.**

When every row marked `keep = true` already appears in `mid` in target relative order, `place_rtl` (`Row.Layout`, `Mounting.fs`) leaves `pre` and `anchor :: suf` untouched and transforms the window into `rows`.

- [rows:] the target row ids of the window in final order.
- [keeps:] the boolean keep mask aligned with `rows`.
- [pre:] the unchanged sibling prefix before the window.
- [mid:] the existing live rows inside the window before placement.
- [anchor:] the sibling node immediately after the window.
- [suf:] the unchanged sibling suffix after `anchor`.
*)
Theorem place_rtl_places_exact_order rows keeps pre mid anchor suf :
  length rows = length keeps ->
  NoDup rows ->
  NoDup mid ->
  ~ In anchor pre ->
  ~ In anchor mid ->
  (forall z, In z mid -> In z rows) ->
  (forall r, In r rows -> ~ In r pre /\ r <> anchor /\ ~ In r suf) ->
  subseq (kept_rows (combine rows keeps)) mid ->
  place_rtl rows keeps anchor (pre ++ mid ++ anchor :: suf) = pre ++ rows ++ anchor :: suf.
Proof.
  intros Hlen Hnd_r Hnd_m Hanc_pre Hanc_m Hmid_sub Hdisj Hsub.
  unfold place_rtl.
  assert (Hfst : map fst (combine rows keeps) = rows).
  { clear -Hlen. revert keeps Hlen. induction rows as [|x xs IH]; destruct keeps as [|b bs]; simpl; intros Hlen;
    try discriminate; [reflexivity | f_equal; apply IH; lia]. }
  assert (Hnd_comb : NoDup (map fst (combine rows keeps))) by (rewrite Hfst; exact Hnd_r).
  assert (Hdisj_comb : forall r, In r (map fst (combine rows keeps)) -> ~ In r pre /\ r <> anchor /\ ~ In r suf)
    by (rewrite Hfst; exact Hdisj).
  destruct (place_rtl_step_invariant (combine rows keeps) pre mid anchor suf
              Hnd_comb Hnd_m Hanc_pre Hanc_m Hdisj_comb [] (NoDup_nil _) (fun _ H => match H with end) Hsub)
    as (mid' & Hstep & Hnd_mid' & _ & Hmem' & Hsub').
  rewrite Hstep; simpl. rewrite Hfst in Hmem', Hsub'.
  assert (mid' = rows) as ->; [|reflexivity].
  simpl in Hsub'. symmetry.
  apply subseq_equal_length; [exact Hsub'|].
  apply Nat.le_antisymm; [apply subseq_length, Hsub'|].
  apply nodup_incl_length; [exact Hnd_mid'|].
  intros z Hz. destruct (Hmem' z Hz) as [Hzm | Hzr]; [exact (Hmid_sub z Hzm) | exact Hzr].
Qed.

(**md
**The pure-insertion fast path splices the new window immediately before the anchor.**

When the old window is empty (`aStart = aEnd` in `Row.Layout`), `move_many_before` inserts `b_win` in exact order between `pre` and `anchor :: suf`.

- [b_win:] the newly mounted row ids to insert.
- [pre:] the sibling prefix before `anchor`.
- [anchor:] the sibling node before which `b_win` is inserted.
- [suf:] the sibling suffix after `anchor`.
*)
Theorem move_many_before_inserts_exact b_win pre anchor suf :
  NoDup b_win ->
  ~ In anchor pre ->
  (forall r, In r b_win -> ~ In r pre /\ r <> anchor /\ ~ In r suf) ->
  move_many_before b_win anchor (pre ++ anchor :: suf) = pre ++ b_win ++ anchor :: suf.
Proof.
  revert pre. induction b_win as [|r rs IH]; simpl; intros pre Hnd Hanc Hdisj; [reflexivity|].
  inversion Hnd as [|? ? Hr_rs Hnd_rs]; subst.
  destruct (Hdisj r (or_introl eq_refl)) as (Hr_pre & Hr_anc & Hr_suf).
  unfold move_before.
  destruct (Nat.eqb r anchor) eqn:Hra; [apply Nat.eqb_eq in Hra; contradiction|].
  rewrite remove_node_app; simpl.
  destruct (Nat.eqb anchor r) eqn:Har; [apply Nat.eqb_eq in Har; symmetry in Har; contradiction|].
  rewrite (remove_node_not_in r pre Hr_pre), (remove_node_not_in r suf Hr_suf).
  rewrite insert_at_anchor_split by exact Hanc.
  assert (pre ++ r :: anchor :: suf = (pre ++ [r]) ++ anchor :: suf) as -> by (rewrite <- app_assoc; reflexivity).
  rewrite IH.
  - rewrite <- app_assoc; reflexivity.
  - exact Hnd_rs.
  - intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | [-> | []]]; [exact (Hanc Hin) | exact (Hr_anc eq_refl)].
  - intros x Hx. destruct (Hdisj x (or_intror Hx)) as (Hx_pre & Hx_anc & Hx_suf).
    repeat split; [| exact Hx_anc | exact Hx_suf].
    intros Hin; apply in_app_or in Hin; destruct Hin as [Hin | [<- | []]]; [exact (Hx_pre Hin) | exact (Hr_rs Hx)].
Qed.

(**md
**The outer-pair swap fast path exchanges the endpoints of a window in at most two DOM moves.**

When `first_r` and `last_r` swap places around an unchanged interior `mid` (`swapped` in `Row.Layout`), one move (when `mid` is empty) or two moves (when `mid` is non-empty) produces `last_r :: mid ++ [first_r]`.

- [first_r:] the row originally at the front of the window.
- [mid:] the unchanged interior rows between `first_r` and `last_r`.
- [last_r:] the row originally at the end of the window.
- [pre:] the sibling prefix before the window.
- [anchor:] the sibling anchor immediately after the window.
- [suf:] the sibling suffix after `anchor`.
*)
Theorem outer_swap_places_exact first_r mid last_r pre anchor suf :
  first_r <> last_r ->
  ~ In first_r mid ->
  ~ In last_r mid ->
  ~ In first_r pre -> ~ In first_r suf -> first_r <> anchor ->
  ~ In last_r pre -> ~ In last_r suf -> last_r <> anchor ->
  ~ In anchor pre -> ~ In anchor mid ->
  (let dom1 := move_before last_r first_r (pre ++ (first_r :: mid ++ [last_r]) ++ anchor :: suf) in
   match mid with
   | [] => dom1
   | _ => move_before first_r anchor dom1
   end) = pre ++ (last_r :: mid ++ [first_r]) ++ anchor :: suf.
Proof.
  intros Hne Hf_mid Hl_mid Hf_pre Hf_suf Hf_anc Hl_pre Hl_suf Hl_anc Hanc_pre Hanc_mid.
  assert (Hdom1 :
    move_before last_r first_r (pre ++ (first_r :: mid ++ [last_r]) ++ anchor :: suf) =
    pre ++ last_r :: first_r :: mid ++ anchor :: suf).
  { unfold move_before.
    destruct (Nat.eqb last_r first_r) eqn:Hlf; [apply Nat.eqb_eq in Hlf; symmetry in Hlf; contradiction|].
    rewrite !remove_node_app; simpl.
    destruct (Nat.eqb first_r last_r) eqn:Hfl; [apply Nat.eqb_eq in Hfl; contradiction|].
    rewrite remove_node_app; simpl. rewrite Nat.eqb_refl.
    destruct (Nat.eqb anchor last_r) eqn:Hal; [apply Nat.eqb_eq in Hal; symmetry in Hal; contradiction|].
    rewrite (remove_node_not_in last_r pre Hl_pre),
            (remove_node_not_in last_r mid Hl_mid),
            (remove_node_not_in last_r suf Hl_suf),
            app_nil_r.
    apply insert_at_anchor_split, Hf_pre. }
  cbv zeta. rewrite Hdom1; clear Hdom1.
  destruct mid as [|m ms]; simpl; [reflexivity|].
  unfold move_before.
  destruct (Nat.eqb first_r anchor) eqn:Hfa; [apply Nat.eqb_eq in Hfa; contradiction|].
  rewrite !remove_node_app; simpl.
  destruct (Nat.eqb last_r first_r) eqn:Hlf; [apply Nat.eqb_eq in Hlf; symmetry in Hlf; contradiction|].
  rewrite Nat.eqb_refl.
  destruct (Nat.eqb m first_r) eqn:Hmf.
  { apply Nat.eqb_eq in Hmf; subst m. exfalso; apply Hf_mid; left; reflexivity. }
  rewrite remove_node_app; simpl.
  assert (Hf_ms : ~ In first_r ms) by (intros Hin; apply Hf_mid; right; exact Hin).
  destruct (Nat.eqb anchor first_r) eqn:Haf; [apply Nat.eqb_eq in Haf; symmetry in Haf; contradiction|].
  rewrite (remove_node_not_in first_r pre Hf_pre),
          (remove_node_not_in first_r ms Hf_ms),
          (remove_node_not_in first_r suf Hf_suf).
  assert (Hanc_all : ~ In anchor (pre ++ last_r :: m :: ms)).
  { intros Hin; apply in_app_or in Hin.
    destruct Hin as [Hin | [<- | Hin]]; [exact (Hanc_pre Hin) | exact (Hl_anc eq_refl) | exact (Hanc_mid Hin)]. }
  assert (pre ++ last_r :: m :: ms ++ anchor :: suf = (pre ++ last_r :: m :: ms) ++ anchor :: suf) as ->
    by (rewrite <- app_assoc; reflexivity).
  rewrite insert_at_anchor_split by exact Hanc_all.
  rewrite <- !app_assoc; simpl. reflexivity.
Qed.

(** ** LIS ([increasing_run]) length and non-negativity invariants *)

Lemma list_set_length {A : Type} idx (v : A) xs :
  length (list_set idx v xs) = length xs.
Proof.
  revert idx. induction xs as [|x xs IH]; destruct idx; simpl; [reflexivity | reflexivity | reflexivity | f_equal; apply IH].
Qed.

Lemma mark_chain_length fuel cursor prev keep :
  length (mark_chain fuel cursor prev keep) = length keep.
Proof.
  revert cursor keep. induction fuel as [|f IH]; intros [c|] keep; simpl; try reflexivity.
  rewrite IH, list_set_length. reflexivity.
Qed.

(** ** LIS (`increasingRun`), `Departures`, and `Reflow` gating *)

(**md
**The patience-sorting LIS mask matches the input window length.**

`increasing_run` (`Mounting.increasingRun`) returns one boolean keep flag per entry in `sources`.

- [sources:] the old-window source indices (-1 for fresh rows) of the target window.
*)
Theorem increasing_run_length sources :
  length (increasing_run sources) = length sources.
Proof.
  unfold increasing_run.
  destruct (lis_scan sources 0 sources [] (repeat None (length sources))) as [tails prev].
  rewrite mark_chain_length, repeat_length. reflexivity.
Qed.

(**md
**Retiring a row removes it from the live sibling projection whether or not an exit animation holds its DOM node.**

Whether `retire_or_remove` (`Departures.retire`) detaches `n` immediately or holds `n` in `dep_leaving` for an exit animation, `filter_live` sees `n` removed at once.

- [n:] the retired row node id.
- [d:] the `Departures` state before retirement.
- [dom:] the parent's full DOM sibling list before retirement.
*)
Theorem departures_retire_preserves_live_siblings n d dom :
  ~ In n (dep_leaving d) ->
  let '(d', dom') := retire_or_remove n d dom in
  filter_live (dep_leaving d') dom' = remove_node n (filter_live (dep_leaving d) dom).
Proof.
  intros Hnot. unfold retire_or_remove.
  destruct (mem_nat n (dep_exits d)) eqn:Hex.
  - destruct (mem_nat n (dep_leaving d)) eqn:Hmem.
    + unfold mem_nat in Hmem. apply existsb_exists in Hmem.
      destruct Hmem as (y & Hy & Heq). apply Nat.eqb_eq in Heq; subst y. contradiction.
    + simpl. clear Hex Hnot Hmem.
      induction dom as [|y ys IH]; simpl; [reflexivity|].
      unfold mem_nat at 1; simpl.
      destruct (Nat.eqb y n) eqn:Hyn; simpl.
      * apply Nat.eqb_eq in Hyn; subst y.
        destruct (mem_nat n (dep_leaving d)); simpl; [exact IH | rewrite Nat.eqb_refl; exact IH].
      * fold (mem_nat y (dep_leaving d)).
        destruct (mem_nat y (dep_leaving d)); simpl; [exact IH | rewrite Hyn; f_equal; exact IH].
  - simpl. symmetry. apply filter_live_remove_comm.
Qed.

Lemma clear_between_split pre start inner finish suf :
  ~ In start pre ->
  ~ In finish inner ->
  clear_between start finish (pre ++ start :: inner ++ finish :: suf) =
  pre ++ start :: finish :: suf.
Proof.
  intros Hstart Hfin.
  induction pre as [|x pre IH]; simpl in *.
  - rewrite Nat.eqb_refl. f_equal.
    induction inner as [|y inner IHi]; simpl in *.
    + rewrite Nat.eqb_refl. reflexivity.
    + destruct (Nat.eqb y finish) eqn:Hyf.
      * apply Nat.eqb_eq in Hyf; subst y. exfalso; apply Hfin; left; reflexivity.
      * apply IHi. intros Hin; apply Hfin; right; exact Hin.
  - destruct (Nat.eqb x start) eqn:Hxs.
    + apply Nat.eqb_eq in Hxs; subst x. exfalso; apply Hstart; left; reflexivity.
    + f_equal. apply IH. intros Hin; apply Hstart; right; exact Hin.
Qed.

Lemma release_rows_no_exits rows d pre start inner finish suf :
  NoDup (pre ++ start :: inner ++ finish :: suf) ->
  (forall x, In x inner <-> In x rows) ->
  (forall r, In r rows -> mem_nat r (dep_exits d) = false /\ mem_nat r (dep_pending d) = false) ->
  release_rows rows d (pre ++ start :: inner ++ finish :: suf) =
    (d, pre ++ start :: finish :: suf).
Proof.
  intros Hnd Hiff Hno_exit.
  assert (Hdisj : forall r, In r rows -> ~ In r pre /\ r <> start /\ r <> finish /\ ~ In r suf).
  { intros r Hr. apply Hiff in Hr.
    destruct (NoDup_app_inv pre (start :: inner ++ finish :: suf) Hnd) as (_ & Hnd1 & Hpre_r).
    inversion Hnd1 as [|? ? Hst Hnd2]; subst.
    destruct (NoDup_app_inv inner (finish :: suf) Hnd2) as (_ & Hnd3 & Hin_fs).
    repeat split.
    - intros Hpre; apply (Hpre_r r Hpre); right; apply in_or_app; left; exact Hr.
    - intros ->; apply Hst, in_or_app; left; exact Hr.
    - intros ->; apply (Hin_fs finish Hr); left; reflexivity.
    - intros Hsuf; apply (Hin_fs r Hr); right; exact Hsuf. }
  clear Hnd.
  assert (Hgen : forall mid,
    (forall x, In x mid -> In x rows) ->
    snd (release_rows rows d (pre ++ start :: mid ++ finish :: suf)) = pre ++ start :: finish :: suf /\
    fst (release_rows rows d (pre ++ start :: mid ++ finish :: suf)) = d).
  { clear Hiff inner. intros mid Hmid.
    revert mid Hmid. induction rows as [|r rs IH]; simpl; intros mid Hmid.
    - assert (mid = []) as -> by (destruct mid as [|z ?]; [reflexivity | exfalso; exact (Hmid z (or_introl eq_refl))]).
      split; reflexivity.
    - destruct (Hno_exit r (or_introl eq_refl)) as [Hex Hpen].
      destruct (Hdisj r (or_introl eq_refl)) as (Hr_pre & Hr_st & Hr_fin & Hr_suf).
      unfold retire_or_remove. rewrite Hex.
      assert (Hpen_id : remove_node r (dep_pending d) = dep_pending d).
      { apply remove_node_not_in. intros Hin.
        assert (Hm : mem_nat r (dep_pending d) = true)
          by (unfold mem_nat; apply existsb_exists; exists r; split; [exact Hin | apply Nat.eqb_refl]).
         congruence. }
      destruct d as [ex lv pn]; simpl in *. rewrite Hpen_id.
      rewrite !remove_node_app; simpl.
      destruct (Nat.eqb start r) eqn:Hsr; [apply Nat.eqb_eq in Hsr; symmetry in Hsr; contradiction|].
      rewrite remove_node_app; simpl.
      destruct (Nat.eqb finish r) eqn:Hfr; [apply Nat.eqb_eq in Hfr; symmetry in Hfr; contradiction|].
      rewrite (remove_node_not_in r pre Hr_pre), (remove_node_not_in r suf Hr_suf).
      apply IH.
      + intros x Hx; exact (Hno_exit x (or_intror Hx)).
      + intros x Hx; exact (Hdisj x (or_intror Hx)).
      + intros x Hx. rewrite remove_node_in_iff in Hx. destruct Hx as [Hx Hne].
        destruct (Hmid x Hx) as [-> | Hrs]; [contradiction | exact Hrs]. }
  destruct (Hgen inner (fun x H => proj1 (Hiff x) H)) as [Hsnd Hfst].
  destruct (release_rows rows d (pre ++ start :: inner ++ finish :: suf)) as [d1 dom1]; simpl in *; subst; reflexivity.
Qed.

(**md
**Clearing between region markers equals row-by-row release whenever no departure holds a node in the span.**

When `holds_between` (`Departures.Holds`) is false and no pending claim remains on `rows`, `RegionSite.FlushReleased`'s bulk `clear_between` fast path produces the exact `(Departures, DOM)` pair of `release_rows`.

- [rows:] the rows owned by the cleared region.
- [d:] the `Departures` state before flushing.
- [pre:] the sibling prefix before the region's start marker.
- [start:] the region's opening marker node.
- [inner:] the sibling nodes currently between `start` and `finish`.
- [finish:] the region's closing marker node.
- [suf:] the sibling suffix after `finish`.
*)
Theorem flush_released_fast_clear_sound rows d pre start inner finish suf :
  NoDup (pre ++ start :: inner ++ finish :: suf) ->
  (forall x, In x inner <-> In x rows) ->
  (forall r, In r rows -> mem_nat r (dep_pending d) = false) ->
  holds_between d start finish (pre ++ start :: inner ++ finish :: suf) = false ->
  flush_released true start finish rows d (pre ++ start :: inner ++ finish :: suf) =
  release_rows rows d (pre ++ start :: inner ++ finish :: suf).
Proof.
  intros Hnd Hiff Hpen Hholds.
  unfold flush_released. rewrite Hholds; simpl.
  assert (Hstart_pre : ~ In start pre).
  { destruct (NoDup_app_inv pre (start :: inner ++ finish :: suf) Hnd) as (_ & _ & Hd).
    intros Hin; exact (Hd start Hin (or_introl eq_refl)). }
  assert (Hfin_in : ~ In finish inner).
  { destruct (NoDup_app_inv pre (start :: inner ++ finish :: suf) Hnd) as (_ & Hnd1 & _).
    inversion Hnd1 as [|? ? _ Hnd2]; subst.
    destruct (NoDup_app_inv inner (finish :: suf) Hnd2) as (_ & _ & Hd).
    intros Hin; exact (Hd finish Hin (or_introl eq_refl)). }
  rewrite clear_between_split by assumption.
  assert (Hbetween : between_Exclusive start finish (pre ++ start :: inner ++ finish :: suf) = inner).
  { clear -Hstart_pre Hfin_in.
    induction pre as [|x pre IH]; simpl in *.
    - rewrite Nat.eqb_refl.
      induction inner as [|y inner IHi]; simpl in *.
      + rewrite Nat.eqb_refl; reflexivity.
      + destruct (Nat.eqb y finish) eqn:Hyf;
        [apply Nat.eqb_eq in Hyf; subst; exfalso; apply Hfin_in; left; reflexivity |].
        f_equal; apply IHi; intros Hin; apply Hfin_in; right; exact Hin.
    - destruct (Nat.eqb x start) eqn:Hxs;
      [apply Nat.eqb_eq in Hxs; subst; exfalso; apply Hstart_pre; left; reflexivity |].
      apply IH; intros Hin; apply Hstart_pre; right; exact Hin. }
  unfold holds_between in Hholds. rewrite Hbetween in Hholds.
  symmetry. apply release_rows_no_exits; [exact Hnd | exact Hiff |].
  intros r Hr. split; [| exact (Hpen r Hr)].
  apply Hiff in Hr.
  destruct (mem_nat r (dep_exits d)) eqn:Hex; [|reflexivity].
  exfalso.
  assert (Htrue : existsb (fun n => mem_nat n (dep_leaving d) || mem_nat n (dep_exits d)) inner = true).
  { apply existsb_exists. exists r. split; [exact Hr | rewrite Hex, orb_true_r; reflexivity]. }
  congruence.
Qed.

Local Arguments Nat.eqb : simpl never.

(**md
**Repeated reflow requests in one open measurement window run `Before` once and close only on the latest post.**

Two `reflow_before` (`Departures.before`) calls while a measurement is open invoke `Before` once, ignore the superseded earlier post ticket, and close the window with a single `After` run when the latest ticket fires.

- [s0:] the closed `ReflowEntry` state before the measurement window opens.
*)
Theorem reflow_latest_post_closes_once (s0 : reflow_state) :
  rf_open s0 = false ->
  let '(s1, t1) := reflow_before s0 in
  let '(s2, t2) := reflow_before s1 in
  let s3 := reflow_run_post t1 s2 in
  let s4 := reflow_run_post t2 s3 in
  rf_before_runs s4 = S (rf_before_runs s0) /\
  rf_after_runs s3 = rf_after_runs s0 /\
  rf_open s3 = true /\
  rf_after_runs s4 = S (rf_after_runs s0) /\
  rf_open s4 = false.
Proof.
  destruct s0 as [op p br ar]; simpl; intros ->.
  unfold reflow_before, reflow_run_post; simpl.
  assert (Hneq : Nat.eqb (S (S p)) (S p) = false) by (apply Nat.eqb_neq; lia).
  rewrite Hneq; simpl. rewrite Nat.eqb_refl; simpl.
  repeat split; reflexivity.
Qed.
