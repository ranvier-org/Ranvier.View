
type bool =
| True
| False

(** val negb : bool -> bool **)

let negb = function
| True -> False
| False -> True

type nat =
| O
| S of nat

type 'a option =
| Some of 'a
| None

type ('a, 'b) prod =
| Pair of 'a * 'b

(** val fst : ('a1, 'a2) prod -> 'a1 **)

let fst = function
| Pair (x, _) -> x

(** val snd : ('a1, 'a2) prod -> 'a2 **)

let snd = function
| Pair (_, y) -> y

type 'a list =
| Nil
| Cons of 'a * 'a list

(** val length : 'a1 list -> nat **)

let rec length = function
| Nil -> O
| Cons (_, l') -> S (length l')

(** val app : 'a1 list -> 'a1 list -> 'a1 list **)

let rec app l m =
  match l with
  | Nil -> m
  | Cons (a, l1) -> Cons (a, (app l1 m))

type comparison =
| Eq
| Lt
| Gt

(** val compOpp : comparison -> comparison **)

let compOpp = function
| Eq -> Eq
| Lt -> Gt
| Gt -> Lt

(** val add : nat -> nat -> nat **)

let rec add n m =
  match n with
  | O -> m
  | S p -> S (add p m)

(** val sub : nat -> nat -> nat **)

let rec sub n m =
  match n with
  | O -> n
  | S k -> (match m with
            | O -> n
            | S l -> sub k l)

module Nat =
 struct
  (** val eqb : nat -> nat -> bool **)

  let rec eqb n m =
    match n with
    | O -> (match m with
            | O -> True
            | S _ -> False)
    | S n' -> (match m with
               | O -> False
               | S m' -> eqb n' m')

  (** val leb : nat -> nat -> bool **)

  let rec leb n m =
    match n with
    | O -> True
    | S n' -> (match m with
               | O -> False
               | S m' -> leb n' m')

  (** val ltb : nat -> nat -> bool **)

  let ltb n m =
    leb (S n) m
 end

(** val map : ('a1 -> 'a2) -> 'a1 list -> 'a2 list **)

let rec map f = function
| Nil -> Nil
| Cons (a, l0) -> Cons ((f a), (map f l0))

(** val seq : nat -> nat -> nat list **)

let rec seq start = function
| O -> Nil
| S len0 -> Cons (start, (seq (S start) len0))

(** val repeat : 'a1 -> nat -> 'a1 list **)

let rec repeat x = function
| O -> Nil
| S k -> Cons (x, (repeat x k))

(** val nth : nat -> 'a1 list -> 'a1 -> 'a1 **)

let rec nth n l default =
  match n with
  | O -> (match l with
          | Nil -> default
          | Cons (x, _) -> x)
  | S m -> (match l with
            | Nil -> default
            | Cons (_, l') -> nth m l' default)

(** val firstn : nat -> 'a1 list -> 'a1 list **)

let rec firstn n l =
  match n with
  | O -> Nil
  | S n0 ->
    (match l with
     | Nil -> Nil
     | Cons (a, l0) -> Cons (a, (firstn n0 l0)))

(** val skipn : nat -> 'a1 list -> 'a1 list **)

let rec skipn n l =
  match n with
  | O -> l
  | S n0 -> (match l with
             | Nil -> Nil
             | Cons (_, l0) -> skipn n0 l0)

(** val nth_error : 'a1 list -> nat -> 'a1 option **)

let rec nth_error l = function
| O -> (match l with
        | Nil -> None
        | Cons (x, _) -> Some x)
| S n0 -> (match l with
           | Nil -> None
           | Cons (_, l') -> nth_error l' n0)

(** val rev : 'a1 list -> 'a1 list **)

let rec rev = function
| Nil -> Nil
| Cons (x, l') -> app (rev l') (Cons (x, Nil))

(** val existsb : ('a1 -> bool) -> 'a1 list -> bool **)

let rec existsb f = function
| Nil -> False
| Cons (a, l0) -> (match f a with
                   | True -> True
                   | False -> existsb f l0)

(** val forallb : ('a1 -> bool) -> 'a1 list -> bool **)

let rec forallb f = function
| Nil -> True
| Cons (a, l0) -> (match f a with
                   | True -> forallb f l0
                   | False -> False)

(** val filter : ('a1 -> bool) -> 'a1 list -> 'a1 list **)

let rec filter f = function
| Nil -> Nil
| Cons (x, l0) ->
  (match f x with
   | True -> Cons (x, (filter f l0))
   | False -> filter f l0)

(** val combine : 'a1 list -> 'a2 list -> ('a1, 'a2) prod list **)

let rec combine l l' =
  match l with
  | Nil -> Nil
  | Cons (x, tl) ->
    (match l' with
     | Nil -> Nil
     | Cons (y, tl') -> Cons ((Pair (x, y)), (combine tl tl')))

type positive =
| XI of positive
| XO of positive
| XH

type z =
| Z0
| Zpos of positive
| Zneg of positive

module Pos =
 struct
  (** val succ : positive -> positive **)

  let rec succ = function
  | XI p -> XO (succ p)
  | XO p -> XI p
  | XH -> XO XH

  (** val add : positive -> positive -> positive **)

  let rec add x y =
    match x with
    | XI p ->
      (match y with
       | XI q -> XO (add_carry p q)
       | XO q -> XI (add p q)
       | XH -> XO (succ p))
    | XO p ->
      (match y with
       | XI q -> XI (add p q)
       | XO q -> XO (add p q)
       | XH -> XI p)
    | XH -> (match y with
             | XI q -> XO (succ q)
             | XO q -> XI q
             | XH -> XO XH)

  (** val add_carry : positive -> positive -> positive **)

  and add_carry x y =
    match x with
    | XI p ->
      (match y with
       | XI q -> XI (add_carry p q)
       | XO q -> XO (add_carry p q)
       | XH -> XI (succ p))
    | XO p ->
      (match y with
       | XI q -> XO (add_carry p q)
       | XO q -> XI (add p q)
       | XH -> XO (succ p))
    | XH ->
      (match y with
       | XI q -> XI (succ q)
       | XO q -> XO (succ q)
       | XH -> XI XH)

  (** val pred_double : positive -> positive **)

  let rec pred_double = function
  | XI p -> XI (XO p)
  | XO p -> XI (pred_double p)
  | XH -> XH

  (** val compare_cont : comparison -> positive -> positive -> comparison **)

  let rec compare_cont r x y =
    match x with
    | XI p ->
      (match y with
       | XI q -> compare_cont r p q
       | XO q -> compare_cont Gt p q
       | XH -> Gt)
    | XO p ->
      (match y with
       | XI q -> compare_cont Lt p q
       | XO q -> compare_cont r p q
       | XH -> Gt)
    | XH -> (match y with
             | XH -> r
             | _ -> Lt)

  (** val compare : positive -> positive -> comparison **)

  let compare =
    compare_cont Eq

  (** val of_succ_nat : nat -> positive **)

  let rec of_succ_nat = function
  | O -> XH
  | S x -> succ (of_succ_nat x)
 end

module Z =
 struct
  (** val double : z -> z **)

  let double = function
  | Z0 -> Z0
  | Zpos p -> Zpos (XO p)
  | Zneg p -> Zneg (XO p)

  (** val succ_double : z -> z **)

  let succ_double = function
  | Z0 -> Zpos XH
  | Zpos p -> Zpos (XI p)
  | Zneg p -> Zneg (Pos.pred_double p)

  (** val pred_double : z -> z **)

  let pred_double = function
  | Z0 -> Zneg XH
  | Zpos p -> Zpos (Pos.pred_double p)
  | Zneg p -> Zneg (XI p)

  (** val pos_sub : positive -> positive -> z **)

  let rec pos_sub x y =
    match x with
    | XI p ->
      (match y with
       | XI q -> double (pos_sub p q)
       | XO q -> succ_double (pos_sub p q)
       | XH -> Zpos (XO p))
    | XO p ->
      (match y with
       | XI q -> pred_double (pos_sub p q)
       | XO q -> double (pos_sub p q)
       | XH -> Zpos (Pos.pred_double p))
    | XH ->
      (match y with
       | XI q -> Zneg (XO q)
       | XO q -> Zneg (Pos.pred_double q)
       | XH -> Z0)

  (** val add : z -> z -> z **)

  let add x y =
    match x with
    | Z0 -> y
    | Zpos x' ->
      (match y with
       | Z0 -> x
       | Zpos y' -> Zpos (Pos.add x' y')
       | Zneg y' -> pos_sub x' y')
    | Zneg x' ->
      (match y with
       | Z0 -> x
       | Zpos y' -> pos_sub y' x'
       | Zneg y' -> Zneg (Pos.add x' y'))

  (** val opp : z -> z **)

  let opp = function
  | Z0 -> Z0
  | Zpos x0 -> Zneg x0
  | Zneg x0 -> Zpos x0

  (** val sub : z -> z -> z **)

  let sub m n =
    add m (opp n)

  (** val compare : z -> z -> comparison **)

  let compare x y =
    match x with
    | Z0 -> (match y with
             | Z0 -> Eq
             | Zpos _ -> Lt
             | Zneg _ -> Gt)
    | Zpos x' -> (match y with
                  | Zpos y' -> Pos.compare x' y'
                  | _ -> Gt)
    | Zneg x' ->
      (match y with
       | Zneg y' -> compOpp (Pos.compare x' y')
       | _ -> Lt)

  (** val ltb : z -> z -> bool **)

  let ltb x y =
    match compare x y with
    | Lt -> True
    | _ -> False

  (** val of_nat : nat -> z **)

  let of_nat = function
  | O -> Z0
  | S n0 -> Zpos (Pos.of_succ_nat n0)

  (** val geb : z -> z -> bool **)

  let geb x y =
    match compare x y with
    | Lt -> False
    | _ -> True
 end

(** val remove_node : nat -> nat list -> nat list **)

let rec remove_node x = function
| Nil -> Nil
| Cons (y, ys) ->
  (match Nat.eqb y x with
   | True -> remove_node x ys
   | False -> Cons (y, (remove_node x ys)))

(** val insert_at_anchor : nat -> nat -> nat list -> nat list **)

let rec insert_at_anchor x anchor = function
| Nil -> Cons (x, Nil)
| Cons (y, ys) ->
  (match Nat.eqb y anchor with
   | True -> Cons (x, (Cons (y, ys)))
   | False -> Cons (y, (insert_at_anchor x anchor ys)))

(** val move_before : nat -> nat -> nat list -> nat list **)

let move_before x anchor dom =
  match Nat.eqb x anchor with
  | True -> dom
  | False -> insert_at_anchor x anchor (remove_node x dom)

(** val move_many_before : nat list -> nat -> nat list -> nat list **)

let rec move_many_before rows anchor dom =
  match rows with
  | Nil -> dom
  | Cons (r, rs) -> move_many_before rs anchor (move_before r anchor dom)

(** val place_rtl_step :
    (nat, bool) prod list -> nat -> nat list -> (nat, nat list) prod **)

let rec place_rtl_step pairs anchor dom =
  match pairs with
  | Nil -> Pair (anchor, dom)
  | Cons (p, rest) ->
    let Pair (r, k) = p in
    let Pair (before, dom') = place_rtl_step rest anchor dom in
    let dom'' = match k with
                | True -> dom'
                | False -> move_before r before dom'
    in
    Pair (r, dom'')

(** val place_rtl : nat list -> bool list -> nat -> nat list -> nat list **)

let place_rtl rows keeps anchor dom =
  snd (place_rtl_step (combine rows keeps) anchor dom)

(** val lower_bound_tails : z list -> nat list -> z -> nat **)

let rec lower_bound_tails sources tails val_x =
  match tails with
  | Nil -> O
  | Cons (t, ts) ->
    (match Z.ltb (nth t sources (Zneg XH)) val_x with
     | True -> S (lower_bound_tails sources ts val_x)
     | False -> O)

(** val list_set : nat -> 'a1 -> 'a1 list -> 'a1 list **)

let rec list_set idx v xs =
  match idx with
  | O -> (match xs with
          | Nil -> Nil
          | Cons (_, ys) -> Cons (v, ys))
  | S k ->
    (match xs with
     | Nil -> Nil
     | Cons (y, ys) -> Cons (y, (list_set k v ys)))

(** val update_tails : nat -> nat -> nat list -> nat list **)

let update_tails low idx tails =
  match Nat.eqb low (length tails) with
  | True -> app tails (Cons (idx, Nil))
  | False -> list_set low idx tails

(** val lis_scan :
    z list -> nat -> z list -> nat list -> nat option list -> (nat list, nat
    option list) prod **)

let rec lis_scan sources idx rem tails prev =
  match rem with
  | Nil -> Pair (tails, prev)
  | Cons (v, vs) ->
    (match Z.geb v Z0 with
     | True ->
       let low = lower_bound_tails sources tails v in
       let pred = match low with
                  | O -> None
                  | S k -> nth_error tails k in
       let prev' = list_set idx pred prev in
       let tails' = update_tails low idx tails in
       lis_scan sources (S idx) vs tails' prev'
     | False -> lis_scan sources (S idx) vs tails prev)

(** val mark_chain :
    nat -> nat option -> nat option list -> bool list -> bool list **)

let rec mark_chain fuel cursor prev keep =
  match fuel with
  | O -> keep
  | S f ->
    (match cursor with
     | Some c ->
       let next_c = match nth_error prev c with
                    | Some p -> p
                    | None -> None in
       mark_chain f next_c prev (list_set c True keep)
     | None -> keep)

(** val increasing_run : z list -> bool list **)

let increasing_run sources =
  let n = length sources in
  let init_prev = repeat None n in
  let init_keep = repeat False n in
  let Pair (tails, prev) = lis_scan sources O sources Nil init_prev in
  let start =
    match rev tails with
    | Nil -> None
    | Cons (last_t, _) -> Some last_t
  in
  mark_chain n start prev init_keep

(** val common_prefix_len : nat list -> nat list -> nat **)

let rec common_prefix_len a b =
  match a with
  | Nil -> O
  | Cons (x, xs) ->
    (match b with
     | Nil -> O
     | Cons (y, ys) ->
       (match Nat.eqb x y with
        | True -> S (common_prefix_len xs ys)
        | False -> O))

(** val common_suffix_len : nat list -> nat list -> nat **)

let common_suffix_len a b =
  common_prefix_len (rev a) (rev b)

(** val index_of : nat -> nat -> nat list -> z **)

let rec index_of x base = function
| Nil -> Zneg XH
| Cons (y, ys) ->
  (match Nat.eqb x y with
   | True -> Z.of_nat base
   | False -> index_of x (S base) ys)

(** val is_outer_swap : nat list -> nat list -> bool **)

let is_outer_swap a_win b_win =
  let count = length b_win in
  (match match match match Nat.leb (S (S O)) count with
                     | True -> Nat.eqb (length a_win) count
                     | False -> False with
               | True ->
                 Nat.eqb (nth O a_win O) (nth (sub count (S O)) b_win O)
               | False -> False with
         | True -> Nat.eqb (nth (sub count (S O)) a_win O) (nth O b_win O)
         | False -> False with
   | True ->
     forallb (fun off -> Nat.eqb (nth off a_win O) (nth off b_win O))
       (seq (S O) (sub count (S (S O))))
   | False -> False)

(** val row_layout : nat list -> nat list -> nat -> nat list -> nat list **)

let row_layout previous next region_end dom =
  let lead = common_prefix_len previous next in
  let prev_rem = skipn lead previous in
  let next_rem = skipn lead next in
  let trail = common_suffix_len prev_rem next_rem in
  let a_len = sub (length prev_rem) trail in
  let b_len = sub (length next_rem) trail in
  (match Nat.eqb b_len O with
   | True -> dom
   | False ->
     let a_win = firstn a_len prev_rem in
     let b_win = firstn b_len next_rem in
     let anchor =
       match skipn (add lead b_len) next with
       | Nil -> region_end
       | Cons (r, _) -> r
     in
     (match Nat.eqb a_len O with
      | True -> move_many_before b_win anchor dom
      | False ->
        (match is_outer_swap a_win b_win with
         | True ->
           let dom1 = move_before (nth O b_win O) (nth O a_win O) dom in
           (match Nat.ltb (S (S O)) b_len with
            | True -> move_before (nth (sub b_len (S O)) b_win O) anchor dom1
            | False -> dom1)
         | False ->
           let sources = map (fun r -> index_of r lead a_win) b_win in
           let keeps = increasing_run sources in
           place_rtl b_win keeps anchor dom)))

type dep_state = { dep_exits : nat list; dep_leaving : nat list;
                   dep_pending : nat list }

(** val mem_nat : nat -> nat list -> bool **)

let mem_nat x xs =
  existsb (Nat.eqb x) xs

(** val filter_live : nat list -> nat list -> nat list **)

let rec filter_live leaving = function
| Nil -> Nil
| Cons (x, xs) ->
  (match mem_nat x leaving with
   | True -> filter_live leaving xs
   | False -> Cons (x, (filter_live leaving xs)))

(** val between_Exclusive : nat -> nat -> nat list -> nat list **)

let rec between_Exclusive start finish = function
| Nil -> Nil
| Cons (x, xs) ->
  (match Nat.eqb x start with
   | True ->
     let rec take_until = function
     | Nil -> Nil
     | Cons (y, zs) ->
       (match Nat.eqb y finish with
        | True -> Nil
        | False -> Cons (y, (take_until zs)))
     in take_until xs
   | False -> between_Exclusive start finish xs)

(** val holds_between : dep_state -> nat -> nat -> nat list -> bool **)

let holds_between d start finish dom =
  existsb (fun n ->
    match mem_nat n d.dep_leaving with
    | True -> True
    | False -> mem_nat n d.dep_exits) (between_Exclusive start finish dom)

(** val retire_or_remove :
    nat -> dep_state -> nat list -> (dep_state, nat list) prod **)

let retire_or_remove n d dom =
  match mem_nat n d.dep_exits with
  | True ->
    Pair ({ dep_exits = (remove_node n d.dep_exits); dep_leaving =
      (match mem_nat n d.dep_leaving with
       | True -> d.dep_leaving
       | False -> Cons (n, d.dep_leaving));
      dep_pending = (remove_node n d.dep_pending) }, dom)
  | False ->
    Pair ({ dep_exits = d.dep_exits; dep_leaving = d.dep_leaving;
      dep_pending = (remove_node n d.dep_pending) }, (remove_node n dom))

(** val finish_exit :
    nat -> dep_state -> nat list -> (dep_state, nat list) prod **)

let finish_exit n d dom =
  Pair ({ dep_exits = d.dep_exits; dep_leaving =
    (remove_node n d.dep_leaving); dep_pending = d.dep_pending },
    (remove_node n dom))

(** val clear_between : nat -> nat -> nat list -> nat list **)

let rec clear_between start finish = function
| Nil -> Nil
| Cons (x, xs) ->
  (match Nat.eqb x start with
   | True ->
     let skip_to_end =
       let rec skip_to_end = function
       | Nil -> Nil
       | Cons (y, zs) ->
         (match Nat.eqb y finish with
          | True -> Cons (y, zs)
          | False -> skip_to_end zs)
       in skip_to_end
     in
     Cons (x, (skip_to_end xs))
   | False -> Cons (x, (clear_between start finish xs)))

(** val release_rows :
    nat list -> dep_state -> nat list -> (dep_state, nat list) prod **)

let rec release_rows rows d dom =
  match rows with
  | Nil -> Pair (d, dom)
  | Cons (r, rs) ->
    let Pair (d', dom') = retire_or_remove r d dom in release_rows rs d' dom'

(** val flush_released :
    bool -> nat -> nat -> nat list -> dep_state -> nat list -> (dep_state,
    nat list) prod **)

let flush_released cleared start finish rows d dom =
  match match cleared with
        | True -> negb (holds_between d start finish dom)
        | False -> False with
  | True -> Pair (d, (clear_between start finish dom))
  | False -> release_rows rows d dom

type reflow_state = { rf_open : bool; rf_posts : nat; rf_before_runs : 
                      nat; rf_after_runs : nat }

(** val reflow_before : reflow_state -> (reflow_state, nat) prod **)

let reflow_before s =
  let s1 =
    match s.rf_open with
    | True -> s
    | False ->
      { rf_open = True; rf_posts = s.rf_posts; rf_before_runs = (S
        s.rf_before_runs); rf_after_runs = s.rf_after_runs }
  in
  let ticket = S s1.rf_posts in
  Pair ({ rf_open = s1.rf_open; rf_posts = ticket; rf_before_runs =
  s1.rf_before_runs; rf_after_runs = s1.rf_after_runs }, ticket)

(** val reflow_run_post : nat -> reflow_state -> reflow_state **)

let reflow_run_post ticket s =
  match match s.rf_open with
        | True -> Nat.eqb s.rf_posts ticket
        | False -> False with
  | True ->
    { rf_open = False; rf_posts = s.rf_posts; rf_before_runs =
      s.rf_before_runs; rf_after_runs = (S s.rf_after_runs) }
  | False -> s

type ssr_token =
| TokStart of nat
| TokEnd
| TokNode of nat

(** val scan_matching_end : nat -> ssr_token list -> ssr_token list option **)

let rec scan_matching_end depth = function
| Nil -> None
| Cons (s, rest) ->
  (match s with
   | TokStart _ -> scan_matching_end (S depth) rest
   | TokEnd ->
     (match depth with
      | O -> Some (Cons (TokEnd, rest))
      | S d -> scan_matching_end d rest)
   | TokNode _ -> scan_matching_end depth rest)

(** val logical_next : ssr_token list -> ssr_token list option **)

let logical_next = function
| Nil -> None
| Cons (s, rest) ->
  (match s with
   | TokStart _ -> scan_matching_end O rest
   | _ -> Some rest)

type ssr_tree =
| SNode of nat
| SRegion of nat * ssr_tree list

(** val flatten_tree : ssr_tree -> ssr_token list **)

let rec flatten_tree t =
  let flatten_list =
    let rec flatten_list = function
    | Nil -> Nil
    | Cons (c, cs) -> app (flatten_tree c) (flatten_list cs)
    in flatten_list
  in
  (match t with
   | SNode id -> Cons ((TokNode id), Nil)
   | SRegion (tag, children) ->
     Cons ((TokStart tag), (app (flatten_list children) (Cons (TokEnd, Nil)))))

type 'st cursor_entry =
| UndoStep of ('st -> 'st)
| NestedCursor of 'st cursor_entry list

(** val restore_entry : 'a1 cursor_entry -> 'a1 -> 'a1 **)

let rec restore_entry e st =
  let restore_list =
    let rec restore_list es acc =
      match es with
      | Nil -> acc
      | Cons (hd, tl) -> restore_entry hd (restore_list tl acc)
    in restore_list
  in
  (match e with
   | UndoStep inv -> inv st
   | NestedCursor child -> restore_list child st)

(** val restore_entries : 'a1 cursor_entry list -> 'a1 -> 'a1 **)

let rec restore_entries es st =
  match es with
  | Nil -> st
  | Cons (e, rest) -> restore_entry e (restore_entries rest st)

(** val held_entry : 'a1 cursor_entry -> nat **)

let rec held_entry e =
  let held_list =
    let rec held_list = function
    | Nil -> O
    | Cons (hd, tl) -> add (held_entry hd) (held_list tl)
    in held_list
  in
  (match e with
   | UndoStep _ -> S O
   | NestedCursor child -> S (held_list child))

(** val held_entries : 'a1 cursor_entry list -> nat **)

let rec held_entries = function
| Nil -> O
| Cons (e, rest) -> add (held_entry e) (held_entries rest)

(** val commit_entries : 'a1 cursor_entry list -> 'a1 cursor_entry list **)

let commit_entries _ =
  Nil

type 'st hydration_op =
| HydEdit of ('st -> 'st) * ('st -> 'st)
| HydScope of 'st hydration_op list

(** val run_hydration_op :
    'a1 hydration_op -> 'a1 -> ('a1, 'a1 cursor_entry) prod **)

let rec run_hydration_op op st =
  let run_list =
    let rec run_list ops acc =
      match ops with
      | Nil -> Pair (acc, Nil)
      | Cons (hd, tl) ->
        let Pair (acc1, e_hd) = run_hydration_op hd acc in
        let Pair (acc2, es_tl) = run_list tl acc1 in
        Pair (acc2, (Cons (e_hd, es_tl)))
    in run_list
  in
  (match op with
   | HydEdit (fwd, inv) -> Pair ((fwd st), (UndoStep inv))
   | HydScope sub0 ->
     let Pair (st1, child_log) = run_list sub0 st in
     Pair (st1, (NestedCursor child_log)))

(** val run_hydration_ops :
    'a1 hydration_op list -> 'a1 -> ('a1, 'a1 cursor_entry list) prod **)

let rec run_hydration_ops ops st =
  match ops with
  | Nil -> Pair (st, Nil)
  | Cons (op, rest) ->
    let Pair (st1, e) = run_hydration_op op st in
    let Pair (st2, log_rest) = run_hydration_ops rest st1 in
    Pair (st2, (Cons (e, log_rest)))

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

(** val bs_expect : boundary_model -> (boundary_model, bstate) prod **)

let bs_expect bm =
  match match bm.bm_tracks with
        | True ->
          negb
            (match bm.bm_revealed with
             | True -> True
             | False -> bm.bm_failed)
        | False -> False with
  | True ->
    Pair ({ bm_tracks = bm.bm_tracks; bm_waiting = (S bm.bm_waiting);
      bm_setting_up = bm.bm_setting_up; bm_revealed = bm.bm_revealed;
      bm_failed = bm.bm_failed; bm_pending_sig = bm.bm_pending_sig;
      bm_wakes = bm.bm_wakes; bm_clear_calls = bm.bm_clear_calls;
      bm_fail_calls = bm.bm_fail_calls; bm_gen = bm.bm_gen }, { bs_failed =
      False; bs_waiting = True })
  | False -> Pair (bm, { bs_failed = False; bs_waiting = False })

(** val bm_wait : boundary_model -> boundary_model **)

let bm_wait bm =
  { bm_tracks = bm.bm_tracks; bm_waiting = (S bm.bm_waiting); bm_setting_up =
    bm.bm_setting_up; bm_revealed = bm.bm_revealed; bm_failed = bm.bm_failed;
    bm_pending_sig =
    (match bm.bm_revealed with
     | True -> True
     | False -> bm.bm_pending_sig);
    bm_wakes = bm.bm_wakes; bm_clear_calls = bm.bm_clear_calls;
    bm_fail_calls = bm.bm_fail_calls; bm_gen = bm.bm_gen }

(** val bm_ready : boundary_model -> boundary_model **)

let bm_ready bm =
  match bm.bm_waiting with
  | O -> bm
  | S rem ->
    { bm_tracks = bm.bm_tracks; bm_waiting = rem; bm_setting_up =
      bm.bm_setting_up; bm_revealed = bm.bm_revealed; bm_failed =
      bm.bm_failed; bm_pending_sig =
      (match match Nat.eqb rem O with
             | True -> bm.bm_revealed
             | False -> False with
       | True -> False
       | False -> bm.bm_pending_sig);
      bm_wakes =
      (match match match Nat.eqb rem O with
                   | True -> negb bm.bm_revealed
                   | False -> False with
             | True -> negb bm.bm_setting_up
             | False -> False with
       | True -> S bm.bm_wakes
       | False -> bm.bm_wakes);
      bm_clear_calls = bm.bm_clear_calls; bm_fail_calls = bm.bm_fail_calls;
      bm_gen = bm.bm_gen }

(** val bm_fail : boundary_model -> boundary_model **)

let bm_fail bm =
  { bm_tracks = bm.bm_tracks; bm_waiting = bm.bm_waiting; bm_setting_up =
    bm.bm_setting_up; bm_revealed = bm.bm_revealed; bm_failed = True;
    bm_pending_sig = bm.bm_pending_sig; bm_wakes = bm.bm_wakes;
    bm_clear_calls = bm.bm_clear_calls; bm_fail_calls = (S bm.bm_fail_calls);
    bm_gen = (S bm.bm_gen) }

(** val binding_step :
    read_outcome -> boundary_model -> bstate -> (boundary_model, bstate) prod **)

let binding_step out bm bs =
  match out with
  | OutValue ->
    let bm1 =
      match bs.bs_failed with
      | True ->
        { bm_tracks = bm.bm_tracks; bm_waiting = bm.bm_waiting;
          bm_setting_up = bm.bm_setting_up; bm_revealed = bm.bm_revealed;
          bm_failed = bm.bm_failed; bm_pending_sig = bm.bm_pending_sig;
          bm_wakes = bm.bm_wakes; bm_clear_calls = (S bm.bm_clear_calls);
          bm_fail_calls = bm.bm_fail_calls; bm_gen = bm.bm_gen }
      | False -> bm
    in
    let bm2 = match bs.bs_waiting with
              | True -> bm_ready bm1
              | False -> bm1 in
    Pair (bm2, { bs_failed = False; bs_waiting = False })
  | OutNotReady ->
    (match match negb bs.bs_waiting with
           | True -> bm.bm_tracks
           | False -> False with
     | True ->
       Pair ((bm_wait bm), { bs_failed = bs.bs_failed; bs_waiting = True })
     | False -> Pair (bm, bs))
  | OutError ->
    Pair ((bm_fail bm), { bs_failed = True; bs_waiting = bs.bs_waiting })

(** val binding_dispose :
    boundary_model -> bstate -> (boundary_model, bstate) prod **)

let binding_dispose bm bs =
  let bm' = match bs.bs_waiting with
            | True -> bm_ready bm
            | False -> bm in
  Pair (bm', { bs_failed = bs.bs_failed; bs_waiting = False })

(** val bm_retry : nat -> boundary_model -> boundary_model **)

let bm_retry attempt_gen bm =
  match match bm.bm_failed with
        | True -> Nat.eqb attempt_gen bm.bm_gen
        | False -> False with
  | True ->
    { bm_tracks = bm.bm_tracks; bm_waiting = O; bm_setting_up = False;
      bm_revealed = False; bm_failed = False; bm_pending_sig = False;
      bm_wakes = bm.bm_wakes; bm_clear_calls = bm.bm_clear_calls;
      bm_fail_calls = bm.bm_fail_calls; bm_gen = bm.bm_gen }
  | False -> bm

(** val update_binding : nat -> bstate -> bstate list -> bstate list **)

let rec update_binding idx b_new bss =
  match idx with
  | O -> (match bss with
          | Nil -> Nil
          | Cons (_, rest) -> Cons (b_new, rest))
  | S i ->
    (match bss with
     | Nil -> Nil
     | Cons (b, rest) -> Cons (b, (update_binding i b_new rest)))

(** val stable_insert_group :
    nat -> nat -> (nat, nat) prod list -> (nat, nat) prod list **)

let rec stable_insert_group g h = function
| Nil -> Cons ((Pair (g, h)), Nil)
| Cons (p, rest) ->
  let Pair (g', h') = p in
  (match Nat.leb g' g with
   | True -> Cons ((Pair (g', h')), (stable_insert_group g h rest))
   | False -> Cons ((Pair (g, h)), (Cons ((Pair (g', h')), rest))))

(** val sort_groups_acc :
    (nat, nat) prod list -> (nat, nat) prod list -> (nat, nat) prod list **)

let rec sort_groups_acc acc = function
| Nil -> acc
| Cons (p, rs) ->
  let Pair (g, h) = p in sort_groups_acc (stable_insert_group g h acc) rs

(** val sort_groups : (nat, nat) prod list -> (nat, nat) prod list **)

let sort_groups xs =
  sort_groups_acc Nil xs

type placement_model = { pl_laid_out : bool; pl_discarded : bool;
                         pl_parent_placed : bool; pl_ordered : bool;
                         pl_last_group : nat; pl_group : nat;
                         pl_entered : nat; pl_held : (nat, nat) prod list;
                         pl_forwarded : nat list; pl_executed : nat list;
                         pl_to_parent : nat list }

(** val init_placement : bool -> placement_model **)

let init_placement parent_placed =
  { pl_laid_out = False; pl_discarded = False; pl_parent_placed =
    parent_placed; pl_ordered = True; pl_last_group = O; pl_group = O;
    pl_entered = O; pl_held = Nil; pl_forwarded = Nil; pl_executed = Nil;
    pl_to_parent = Nil }

(** val pl_is_placed : placement_model -> bool **)

let pl_is_placed p =
  match p.pl_laid_out with
  | True -> p.pl_parent_placed
  | False -> False

(** val pl_enter : placement_model -> (placement_model, nat) prod **)

let pl_enter p =
  let prev = p.pl_group in
  let next_g = S p.pl_entered in
  Pair ({ pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
  pl_parent_placed = p.pl_parent_placed; pl_ordered = p.pl_ordered;
  pl_last_group = p.pl_last_group; pl_group = next_g; pl_entered = next_g;
  pl_held = p.pl_held; pl_forwarded = p.pl_forwarded; pl_executed =
  p.pl_executed; pl_to_parent = p.pl_to_parent }, prev)

(** val pl_leave : nat -> placement_model -> placement_model **)

let pl_leave prev p =
  { pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
    pl_parent_placed = p.pl_parent_placed; pl_ordered = p.pl_ordered;
    pl_last_group = p.pl_last_group; pl_group = prev; pl_entered =
    p.pl_entered; pl_held = p.pl_held; pl_forwarded = p.pl_forwarded;
    pl_executed = p.pl_executed; pl_to_parent = p.pl_to_parent }

(** val pl_enqueue : nat -> bool -> placement_model -> placement_model **)

let pl_enqueue hook released p =
  match p.pl_discarded with
  | True -> p
  | False ->
    (match negb p.pl_laid_out with
     | True ->
       (match released with
        | True ->
          { pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
            pl_parent_placed = p.pl_parent_placed; pl_ordered = p.pl_ordered;
            pl_last_group = p.pl_last_group; pl_group = p.pl_group;
            pl_entered = p.pl_entered; pl_held = p.pl_held; pl_forwarded =
            (app p.pl_forwarded (Cons (hook, Nil))); pl_executed =
            p.pl_executed; pl_to_parent = p.pl_to_parent }
        | False ->
          let ord' =
            match Nat.ltb p.pl_group p.pl_last_group with
            | True -> False
            | False -> p.pl_ordered
          in
          { pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
          pl_parent_placed = p.pl_parent_placed; pl_ordered = ord';
          pl_last_group = p.pl_group; pl_group = p.pl_group; pl_entered =
          p.pl_entered; pl_held =
          (app p.pl_held (Cons ((Pair (p.pl_group, hook)), Nil)));
          pl_forwarded = p.pl_forwarded; pl_executed = p.pl_executed;
          pl_to_parent = p.pl_to_parent })
     | False ->
       (match pl_is_placed p with
        | True ->
          { pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
            pl_parent_placed = p.pl_parent_placed; pl_ordered = p.pl_ordered;
            pl_last_group = p.pl_last_group; pl_group = p.pl_group;
            pl_entered = p.pl_entered; pl_held = p.pl_held; pl_forwarded =
            p.pl_forwarded; pl_executed =
            (app p.pl_executed (Cons (hook, Nil))); pl_to_parent =
            p.pl_to_parent }
        | False ->
          { pl_laid_out = p.pl_laid_out; pl_discarded = p.pl_discarded;
            pl_parent_placed = p.pl_parent_placed; pl_ordered = p.pl_ordered;
            pl_last_group = p.pl_last_group; pl_group = p.pl_group;
            pl_entered = p.pl_entered; pl_held = p.pl_held; pl_forwarded =
            p.pl_forwarded; pl_executed = p.pl_executed; pl_to_parent =
            (app p.pl_to_parent (Cons (hook, Nil))) }))

(** val pl_add : nat -> placement_model -> placement_model **)

let pl_add hook p =
  pl_enqueue hook False p

(** val pl_place : placement_model -> placement_model **)

let pl_place p =
  match match negb p.pl_laid_out with
        | True -> negb p.pl_discarded
        | False -> False with
  | True ->
    let own_hooks =
      match p.pl_ordered with
      | True -> map snd p.pl_held
      | False -> map snd (sort_groups p.pl_held)
    in
    let all_hooks = app own_hooks p.pl_forwarded in
    { pl_laid_out = True; pl_discarded = False; pl_parent_placed =
    p.pl_parent_placed; pl_ordered = p.pl_ordered; pl_last_group =
    p.pl_last_group; pl_group = p.pl_group; pl_entered = p.pl_entered;
    pl_held = Nil; pl_forwarded = Nil; pl_executed =
    (match p.pl_parent_placed with
     | True -> app p.pl_executed all_hooks
     | False -> p.pl_executed);
    pl_to_parent =
    (match p.pl_parent_placed with
     | True -> p.pl_to_parent
     | False -> app p.pl_to_parent all_hooks) }
  | False -> p

(** val pl_discard : placement_model -> placement_model **)

let pl_discard p =
  { pl_laid_out = p.pl_laid_out; pl_discarded = True; pl_parent_placed =
    p.pl_parent_placed; pl_ordered = p.pl_ordered; pl_last_group =
    p.pl_last_group; pl_group = p.pl_group; pl_entered = p.pl_entered;
    pl_held = Nil; pl_forwarded = Nil; pl_executed = p.pl_executed;
    pl_to_parent = p.pl_to_parent }

(** val pl_held_count : placement_model -> nat **)

let pl_held_count p =
  add (length p.pl_held) (length p.pl_forwarded)

type lifetime_tree =
| LTNode of nat * nat list * lifetime_tree list

type lt_event =
| EvOwnerCleanup of nat * nat
| EvDomRelease of nat

(** val collect_teardown_sub :
    lifetime_tree -> (lt_event list, lt_event list) prod **)

let rec collect_teardown_sub t =
  let collect_children_rev =
    let rec collect_children_rev = function
    | Nil -> Pair (Nil, Nil)
    | Cons (c, cs) ->
      let Pair (cl_c, rel_c) = collect_teardown_sub c in
      let Pair (cl_cs, rel_cs) = collect_children_rev cs in
      Pair ((app cl_cs cl_c), (app rel_cs rel_c))
    in collect_children_rev
  in
  let LTNode (id, cleanups, children) = t in
  let Pair (child_cleanups, queued_releases) = collect_children_rev children
  in
  let own_cleanups = map (fun x -> EvOwnerCleanup (id, x)) (rev cleanups) in
  Pair ((app child_cleanups own_cleanups),
  (app queued_releases (Cons ((EvDomRelease id), Nil))))

(** val dispose_lifetime_tree : lifetime_tree -> lt_event list **)

let dispose_lifetime_tree t =
  let Pair (cleanups, releases) = collect_teardown_sub t in
  app cleanups releases

(** val in_interval : nat -> (nat, nat) prod -> bool **)

let in_interval idx iv =
  match Nat.leb (fst iv) idx with
  | True -> Nat.ltb idx (snd iv)
  | False -> False

(** val in_any_nested : nat -> (nat, nat) prod list -> bool **)

let in_any_nested idx nested =
  existsb (in_interval idx) nested

(** val teardown_window_indices :
    nat -> nat -> (nat, nat) prod list -> nat list **)

let teardown_window_indices start len nested =
  filter (fun idx -> negb (in_any_nested idx nested)) (seq start len)

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

(** val init_queue : queue_model **)

let init_queue =
  { q_slots = Nil; q_pending = Nil; q_disposed = False; q_scheduled = False;
    q_generation = O; q_tickets = Nil; q_committed = Nil; q_errors = Nil }

(** val set_slot : nat -> slot_model -> slot_model list -> slot_model list **)

let rec set_slot idx s ss =
  match idx with
  | O -> (match ss with
          | Nil -> Nil
          | Cons (_, rest) -> Cons (s, rest))
  | S i ->
    (match ss with
     | Nil -> Nil
     | Cons (x, rest) -> Cons (x, (set_slot i s rest)))

(** val q_request : queue_model -> queue_model **)

let q_request q =
  match match negb q.q_disposed with
        | True -> negb q.q_scheduled
        | False -> False with
  | True ->
    (match q.q_pending with
     | Nil -> q
     | Cons (_, _) ->
       { q_slots = q.q_slots; q_pending = q.q_pending; q_disposed =
         q.q_disposed; q_scheduled = True; q_generation = q.q_generation;
         q_tickets = (app q.q_tickets (Cons (q.q_generation, Nil)));
         q_committed = q.q_committed; q_errors = q.q_errors })
  | False -> q

(** val q_reserve : nat -> bool -> queue_model -> (queue_model, nat) prod **)

let q_reserve scope has_dropped_cb q =
  let idx = length q.q_slots in
  let sl = { sl_version = O; sl_listed = False; sl_action = None;
    sl_has_dropped_cb = has_dropped_cb; sl_dropped_runs = O; sl_scope =
    scope }
  in
  Pair ({ q_slots = (app q.q_slots (Cons (sl, Nil))); q_pending =
  q.q_pending; q_disposed = q.q_disposed; q_scheduled = q.q_scheduled;
  q_generation = q.q_generation; q_tickets = q.q_tickets; q_committed =
  q.q_committed; q_errors = q.q_errors }, idx)

(** val q_cancel : nat -> queue_model -> queue_model **)

let q_cancel idx q =
  match nth_error q.q_slots idx with
  | Some sl ->
    let sl' = { sl_version = (S sl.sl_version); sl_listed = sl.sl_listed;
      sl_action = None; sl_has_dropped_cb = sl.sl_has_dropped_cb;
      sl_dropped_runs = sl.sl_dropped_runs; sl_scope = sl.sl_scope }
    in
    { q_slots = (set_slot idx sl' q.q_slots); q_pending = q.q_pending;
    q_disposed = q.q_disposed; q_scheduled = q.q_scheduled; q_generation =
    q.q_generation; q_tickets = q.q_tickets; q_committed = q.q_committed;
    q_errors = q.q_errors }
  | None -> q

(** val q_enqueue : nat -> nat -> queue_model -> queue_model **)

let q_enqueue idx action q =
  match q.q_disposed with
  | True -> q
  | False ->
    (match nth_error q.q_slots idx with
     | Some sl ->
       let was_listed = sl.sl_listed in
       let sl' = { sl_version = (S sl.sl_version); sl_listed = True;
         sl_action = (Some action); sl_has_dropped_cb = sl.sl_has_dropped_cb;
         sl_dropped_runs = sl.sl_dropped_runs; sl_scope = sl.sl_scope }
       in
       let q1 = { q_slots = (set_slot idx sl' q.q_slots); q_pending =
         (match was_listed with
          | True -> q.q_pending
          | False -> app q.q_pending (Cons (idx, Nil)));
         q_disposed = q.q_disposed; q_scheduled = q.q_scheduled;
         q_generation = q.q_generation; q_tickets = q.q_tickets;
         q_committed = q.q_committed; q_errors = q.q_errors }
       in
       q_request q1
     | None -> q)

(** val snapshot_and_clear :
    nat list -> slot_model list -> (((nat, nat) prod, nat option) prod list,
    slot_model list) prod **)

let rec snapshot_and_clear pend slots =
  match pend with
  | Nil -> Pair (Nil, slots)
  | Cons (idx, rest) ->
    (match nth_error slots idx with
     | Some sl ->
       let item = Pair ((Pair (idx, sl.sl_version)), sl.sl_action) in
       let sl_cleared = { sl_version = sl.sl_version; sl_listed = False;
         sl_action = None; sl_has_dropped_cb = sl.sl_has_dropped_cb;
         sl_dropped_runs = sl.sl_dropped_runs; sl_scope = sl.sl_scope }
       in
       let slots1 = set_slot idx sl_cleared slots in
       let Pair (items, slots2) = snapshot_and_clear rest slots1 in
       Pair ((Cons (item, items)), slots2)
     | None -> snapshot_and_clear rest slots)

(** val exec_work :
    commit_failure -> (nat -> bool) -> bool -> ((nat, nat) prod, nat option)
    prod list -> slot_model list -> nat list -> (nat, nat) prod list ->
    ((slot_model list, nat list) prod, (nat, nat) prod list) prod **)

let rec exec_work mode will_fail stopped work slots committed errs =
  match work with
  | Nil -> Pair ((Pair (slots, committed)), errs)
  | Cons (p, rest) ->
    let Pair (p0, act_opt) = p in
    let Pair (idx, ver) = p0 in
    (match nth_error slots idx with
     | Some sl ->
       let has_commit = match act_opt with
                        | Some _ -> True
                        | None -> False in
       (match stopped with
        | True ->
          let dropped =
            match has_commit with
            | True -> Nat.eqb sl.sl_version ver
            | False -> False
          in
          let sl' = { sl_version = (S sl.sl_version); sl_listed =
            sl.sl_listed; sl_action = None; sl_has_dropped_cb =
            sl.sl_has_dropped_cb; sl_dropped_runs =
            (add sl.sl_dropped_runs
              (match match dropped with
                     | True -> sl.sl_has_dropped_cb
                     | False -> False with
               | True -> S O
               | False -> O));
            sl_scope = sl.sl_scope }
          in
          exec_work mode will_fail True rest (set_slot idx sl' slots)
            committed errs
        | False ->
          (match act_opt with
           | Some act ->
             (match Nat.eqb sl.sl_version ver with
              | True ->
                (match will_fail act with
                 | True ->
                   let stop_now =
                     match mode with
                     | CollectAndContinue -> False
                     | FailFastCancel -> True
                   in
                   exec_work mode will_fail stop_now rest slots committed
                     (app errs (Cons ((Pair (sl.sl_scope, act)), Nil)))
                 | False ->
                   exec_work mode will_fail False rest slots
                     (app committed (Cons (act, Nil))) errs)
              | False ->
                exec_work mode will_fail False rest slots committed errs)
           | None -> exec_work mode will_fail False rest slots committed errs))
     | None -> exec_work mode will_fail stopped rest slots committed errs)

(** val q_flush :
    commit_failure -> (nat -> bool) -> queue_model -> queue_model **)

let q_flush mode will_fail q =
  match q.q_disposed with
  | True -> q
  | False ->
    let Pair (work, slots1) = snapshot_and_clear q.q_pending q.q_slots in
    let Pair (p, errs') =
      exec_work mode will_fail False work slots1 q.q_committed Nil
    in
    let Pair (slots2, committed') = p in
    q_request { q_slots = slots2; q_pending = Nil; q_disposed = False;
      q_scheduled = False; q_generation = (S q.q_generation); q_tickets =
      q.q_tickets; q_committed = committed'; q_errors = errs' }

(** val q_run_posted_ticket :
    nat -> commit_failure -> (nat -> bool) -> queue_model -> queue_model **)

let q_run_posted_ticket ticket mode will_fail q =
  match match negb q.q_disposed with
        | True -> Nat.eqb ticket q.q_generation
        | False -> False with
  | True -> q_flush mode will_fail q
  | False -> q

(** val cancel_all_pending :
    nat list -> slot_model list -> slot_model list **)

let rec cancel_all_pending pend slots =
  match pend with
  | Nil -> slots
  | Cons (idx, rest) ->
    let slots1 =
      match nth_error slots idx with
      | Some sl ->
        set_slot idx { sl_version = (S sl.sl_version); sl_listed = False;
          sl_action = None; sl_has_dropped_cb = sl.sl_has_dropped_cb;
          sl_dropped_runs = sl.sl_dropped_runs; sl_scope = sl.sl_scope } slots
      | None -> slots
    in
    cancel_all_pending rest slots1

(** val q_dispose : queue_model -> queue_model **)

let q_dispose q =
  match q.q_disposed with
  | True -> q
  | False ->
    { q_slots = (cancel_all_pending q.q_pending q.q_slots); q_pending = Nil;
      q_disposed = True; q_scheduled = False; q_generation = (S
      q.q_generation); q_tickets = q.q_tickets; q_committed = q.q_committed;
      q_errors = q.q_errors }

(** val report_by_scope_aux :
    nat list -> (nat, nat) prod list -> (nat, nat list) prod list **)

let rec report_by_scope_aux seen = function
| Nil -> Nil
| Cons (p, rest) ->
  let Pair (sc, err) = p in
  (match match Nat.eqb sc O with
         | True -> True
         | False -> existsb (Nat.eqb sc) seen with
   | True -> report_by_scope_aux seen rest
   | False ->
     let group = Cons (err,
       (map snd (filter (fun p0 -> Nat.eqb (fst p0) sc) rest)))
     in
     Cons ((Pair (sc, group)), (report_by_scope_aux (Cons (sc, seen)) rest)))

(** val report_by_scope :
    (nat, nat) prod list -> (nat, nat list) prod list **)

let report_by_scope failures =
  report_by_scope_aux Nil failures

type owq_admission =
| AdmAccepted
| AdmFull
| AdmClosed

type owq_model = { owq_items : nat list; owq_stopped : bool;
                   owq_has_wake : bool; owq_wakes : nat; owq_accepted : 
                   nat; owq_dequeued : nat; owq_rejected : nat;
                   owq_discarded : nat }

(** val init_owq : owq_model **)

let init_owq =
  { owq_items = Nil; owq_stopped = False; owq_has_wake = False; owq_wakes =
    O; owq_accepted = O; owq_dequeued = O; owq_rejected = O; owq_discarded =
    O }

(** val owq_try_enqueue :
    nat -> nat -> owq_model -> (owq_admission, owq_model) prod **)

let owq_try_enqueue item capacity q =
  match q.owq_stopped with
  | True -> Pair (AdmClosed, q)
  | False ->
    (match Nat.leb capacity (length q.owq_items) with
     | True ->
       Pair (AdmFull, { owq_items = q.owq_items; owq_stopped = False;
         owq_has_wake = q.owq_has_wake; owq_wakes = q.owq_wakes;
         owq_accepted = q.owq_accepted; owq_dequeued = q.owq_dequeued;
         owq_rejected = (S q.owq_rejected); owq_discarded = q.owq_discarded })
     | False ->
       let was_empty =
         match q.owq_items with
         | Nil -> True
         | Cons (_, _) -> False
       in
       let notify =
         match match was_empty with
               | True -> q.owq_has_wake
               | False -> False with
         | True -> S O
         | False -> O
       in
       Pair (AdmAccepted, { owq_items = (app q.owq_items (Cons (item, Nil)));
       owq_stopped = False; owq_has_wake = q.owq_has_wake; owq_wakes =
       (add q.owq_wakes notify); owq_accepted = (S q.owq_accepted);
       owq_dequeued = q.owq_dequeued; owq_rejected = q.owq_rejected;
       owq_discarded = q.owq_discarded }))

(** val owq_try_dequeue : owq_model -> (nat option, owq_model) prod **)

let owq_try_dequeue q =
  match q.owq_items with
  | Nil -> Pair (None, q)
  | Cons (x, rest) ->
    Pair ((Some x), { owq_items = rest; owq_stopped = q.owq_stopped;
      owq_has_wake = q.owq_has_wake; owq_wakes = q.owq_wakes; owq_accepted =
      q.owq_accepted; owq_dequeued = (S q.owq_dequeued); owq_rejected =
      q.owq_rejected; owq_discarded = q.owq_discarded })

(** val owq_set_wake : bool -> owq_model -> owq_model **)

let owq_set_wake has_cb q =
  match q.owq_stopped with
  | True -> q
  | False ->
    let non_empty = match q.owq_items with
                    | Nil -> False
                    | Cons (_, _) -> True
    in
    let notify =
      match match non_empty with
            | True -> has_cb
            | False -> False with
      | True -> S O
      | False -> O
    in
    { owq_items = q.owq_items; owq_stopped = False; owq_has_wake = has_cb;
    owq_wakes = (add q.owq_wakes notify); owq_accepted = q.owq_accepted;
    owq_dequeued = q.owq_dequeued; owq_rejected = q.owq_rejected;
    owq_discarded = q.owq_discarded }

(** val owq_stop : owq_model -> owq_model **)

let owq_stop q =
  { owq_items = Nil; owq_stopped = True; owq_has_wake = False; owq_wakes =
    q.owq_wakes; owq_accepted = q.owq_accepted; owq_dequeued =
    q.owq_dequeued; owq_rejected = q.owq_rejected; owq_discarded =
    (add q.owq_discarded (length q.owq_items)) }

type fence_poll =
| PollNotReady
| PollReady
| PollGpuFailure

type fence_outcome =
| FenceOk of bool
| FenceError

type fence_model = { cf_pending : nat option; cf_failed : bool;
                     cf_next_marker : nat; cf_released : nat list }

(** val init_fence : fence_model **)

let init_fence =
  { cf_pending = None; cf_failed = False; cf_next_marker = (S O);
    cf_released = Nil }

(** val cf_try_complete :
    fence_poll -> fence_model -> (fence_outcome, fence_model) prod **)

let cf_try_complete poll f =
  match f.cf_failed with
  | True -> Pair (FenceError, f)
  | False ->
    (match f.cf_pending with
     | Some m ->
       let next_m = f.cf_next_marker in
       (match poll with
        | PollNotReady ->
          Pair ((FenceOk False), { cf_pending = (Some m); cf_failed = False;
            cf_next_marker = next_m; cf_released = f.cf_released })
        | PollReady ->
          Pair ((FenceOk True), { cf_pending = None; cf_failed = False;
            cf_next_marker = next_m; cf_released =
            (app f.cf_released (Cons (m, Nil))) })
        | PollGpuFailure ->
          Pair (FenceError, { cf_pending = None; cf_failed = True;
            cf_next_marker = next_m; cf_released =
            (app f.cf_released (Cons (m, Nil))) }))
     | None ->
       let marker = f.cf_next_marker in
       let next_m = S f.cf_next_marker in
       (match poll with
        | PollNotReady ->
          Pair ((FenceOk False), { cf_pending = (Some marker); cf_failed =
            False; cf_next_marker = next_m; cf_released = f.cf_released })
        | PollReady ->
          Pair ((FenceOk True), { cf_pending = None; cf_failed = False;
            cf_next_marker = next_m; cf_released =
            (app f.cf_released (Cons (marker, Nil))) })
        | PollGpuFailure ->
          Pair (FenceError, { cf_pending = None; cf_failed = True;
            cf_next_marker = next_m; cf_released =
            (app f.cf_released (Cons (marker, Nil))) })))

(** val cf_reset : fence_model -> (fence_outcome, fence_model) prod **)

let cf_reset f =
  match f.cf_pending with
  | Some _ -> Pair (FenceError, f)
  | None ->
    Pair ((FenceOk True), { cf_pending = None; cf_failed = False;
      cf_next_marker = f.cf_next_marker; cf_released = f.cf_released })

type layout_cmd =
| LCmdRegisterExit of nat
| LCmdReconcile of nat list
| LCmdFinishExit of nat
| LCmdReflowBefore
| LCmdReflowPost of nat

type layout_sim = { ls_start : nat; ls_end : nat; ls_dom : nat list;
                    ls_laid_out : nat list; ls_dep : dep_state;
                    ls_reflow : reflow_state; ls_tickets : nat list }

(** val init_layout_sim : nat list -> layout_sim **)

let init_layout_sim init_rows =
  { ls_start = (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    O))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))));
    ls_end = (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S
    O))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))));
    ls_dom = (Cons ((S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
    O)))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))),
    (app init_rows (Cons ((S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      (S (S (S (S (S (S (S (S (S (S (S (S (S (S (S
      O)))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))),
      Nil)))));
    ls_laid_out = init_rows; ls_dep = { dep_exits = Nil; dep_leaving = Nil;
    dep_pending = Nil }; ls_reflow = { rf_open = False; rf_posts = O;
    rf_before_runs = O; rf_after_runs = O }; ls_tickets = Nil }

(** val step_layout_cmd : layout_cmd -> layout_sim -> layout_sim **)

let step_layout_cmd c s =
  match c with
  | LCmdRegisterExit n ->
    let ex' =
      match mem_nat n s.ls_dep.dep_exits with
      | True -> s.ls_dep.dep_exits
      | False -> Cons (n, s.ls_dep.dep_exits)
    in
    { ls_start = s.ls_start; ls_end = s.ls_end; ls_dom = s.ls_dom;
    ls_laid_out = s.ls_laid_out; ls_dep = { dep_exits = ex'; dep_leaving =
    s.ls_dep.dep_leaving; dep_pending =
    (remove_node n s.ls_dep.dep_pending) }; ls_reflow = s.ls_reflow;
    ls_tickets = s.ls_tickets }
  | LCmdReconcile next_rows ->
    let removed = filter (fun r -> negb (mem_nat r next_rows)) s.ls_laid_out
    in
    let cleared = match next_rows with
                  | Nil -> True
                  | Cons (_, _) -> False in
    let Pair (dep1, dom1) =
      flush_released cleared s.ls_start s.ls_end removed s.ls_dep s.ls_dom
    in
    let dom2 = row_layout s.ls_laid_out next_rows s.ls_end dom1 in
    { ls_start = s.ls_start; ls_end = s.ls_end; ls_dom = dom2; ls_laid_out =
    next_rows; ls_dep = dep1; ls_reflow = s.ls_reflow; ls_tickets =
    s.ls_tickets }
  | LCmdFinishExit n ->
    let Pair (dep1, dom1) = finish_exit n s.ls_dep s.ls_dom in
    { ls_start = s.ls_start; ls_end = s.ls_end; ls_dom = dom1; ls_laid_out =
    s.ls_laid_out; ls_dep = dep1; ls_reflow = s.ls_reflow; ls_tickets =
    s.ls_tickets }
  | LCmdReflowBefore ->
    let Pair (rf1, t) = reflow_before s.ls_reflow in
    { ls_start = s.ls_start; ls_end = s.ls_end; ls_dom = s.ls_dom;
    ls_laid_out = s.ls_laid_out; ls_dep = s.ls_dep; ls_reflow = rf1;
    ls_tickets = (app s.ls_tickets (Cons (t, Nil))) }
  | LCmdReflowPost idx ->
    let ticket = nth idx s.ls_tickets O in
    { ls_start = s.ls_start; ls_end = s.ls_end; ls_dom = s.ls_dom;
    ls_laid_out = s.ls_laid_out; ls_dep = s.ls_dep; ls_reflow =
    (reflow_run_post ticket s.ls_reflow); ls_tickets = s.ls_tickets }

(** val run_layout_cmds : layout_cmd list -> layout_sim -> layout_sim **)

let rec run_layout_cmds cs s =
  match cs with
  | Nil -> s
  | Cons (c, rest) -> run_layout_cmds rest (step_layout_cmd c s)

type layout_view = { lv_dom : nat list; lv_live : nat list; lv_open : 
                     bool; lv_before : nat; lv_after : nat }

(** val observe_layout : nat list -> layout_cmd list -> layout_view **)

let observe_layout init_rows cs =
  let s = run_layout_cmds cs (init_layout_sim init_rows) in
  { lv_dom = (between_Exclusive s.ls_start s.ls_end s.ls_dom); lv_live =
  (filter_live s.ls_dep.dep_leaving
    (between_Exclusive s.ls_start s.ls_end s.ls_dom));
  lv_open = s.ls_reflow.rf_open; lv_before = s.ls_reflow.rf_before_runs;
  lv_after = s.ls_reflow.rf_after_runs }

type hyd_int_op =
| HAdd of z
| HScope of hyd_int_op list

(** val to_hydration_op : hyd_int_op -> z hydration_op **)

let rec to_hydration_op op =
  let conv_list =
    let rec conv_list = function
    | Nil -> Nil
    | Cons (h, t) -> Cons ((to_hydration_op h), (conv_list t))
    in conv_list
  in
  (match op with
   | HAdd d -> HydEdit ((fun x -> Z.add x d), (fun x -> Z.sub x d))
   | HScope sub0 -> HydScope (conv_list sub0))

(** val observe_cursor_rollback :
    z -> hyd_int_op list -> bool -> ((z, z) prod, nat) prod **)

let observe_cursor_rollback s0 ops do_commit =
  let hops = map to_hydration_op ops in
  let Pair (s_mid, log) = run_hydration_ops hops s0 in
  let log' = match do_commit with
             | True -> commit_entries log
             | False -> log in
  let held = held_entries log' in
  let s_after =
    match do_commit with
    | True -> s_mid
    | False -> restore_entries log' s_mid
  in
  Pair ((Pair (s_mid, s_after)), held)

type bnd_cmd =
| BCmdExpect
| BCmdStep of nat * read_outcome
| BCmdDispose of nat
| BCmdEndSetup
| BCmdReveal
| BCmdRetry of nat

type bnd_sim = { bsim_bm : boundary_model; bsim_slots : bstate list }

(** val init_bnd_sim : bool -> bnd_sim **)

let init_bnd_sim tracks =
  { bsim_bm = { bm_tracks = tracks; bm_waiting = O; bm_setting_up = True;
    bm_revealed = False; bm_failed = False; bm_pending_sig = False;
    bm_wakes = O; bm_clear_calls = O; bm_fail_calls = O; bm_gen = O };
    bsim_slots = Nil }

(** val step_bnd_cmd : bnd_cmd -> bnd_sim -> bnd_sim **)

let step_bnd_cmd c s =
  let bm = s.bsim_bm in
  (match c with
   | BCmdExpect ->
     let Pair (bm', b) = bs_expect bm in
     { bsim_bm = bm'; bsim_slots = (app s.bsim_slots (Cons (b, Nil))) }
   | BCmdStep (idx, out) ->
     (match nth_error s.bsim_slots idx with
      | Some b ->
        let Pair (bm', b') = binding_step out bm b in
        { bsim_bm = bm'; bsim_slots = (update_binding idx b' s.bsim_slots) }
      | None -> s)
   | BCmdDispose idx ->
     (match nth_error s.bsim_slots idx with
      | Some b ->
        let Pair (bm', b') = binding_dispose bm b in
        { bsim_bm = bm'; bsim_slots = (update_binding idx b' s.bsim_slots) }
      | None -> s)
   | BCmdEndSetup ->
     { bsim_bm = { bm_tracks = bm.bm_tracks; bm_waiting = bm.bm_waiting;
       bm_setting_up = False; bm_revealed = bm.bm_revealed; bm_failed =
       bm.bm_failed; bm_pending_sig = bm.bm_pending_sig; bm_wakes =
       bm.bm_wakes; bm_clear_calls = bm.bm_clear_calls; bm_fail_calls =
       bm.bm_fail_calls; bm_gen = bm.bm_gen }; bsim_slots = s.bsim_slots }
   | BCmdReveal ->
     (match match Nat.eqb bm.bm_waiting O with
            | True -> negb bm.bm_failed
            | False -> False with
      | True ->
        { bsim_bm = { bm_tracks = bm.bm_tracks; bm_waiting = O;
          bm_setting_up = False; bm_revealed = True; bm_failed = False;
          bm_pending_sig = False; bm_wakes = bm.bm_wakes; bm_clear_calls =
          bm.bm_clear_calls; bm_fail_calls = bm.bm_fail_calls; bm_gen =
          bm.bm_gen }; bsim_slots = s.bsim_slots }
      | False -> s)
   | BCmdRetry gen ->
     let bm' = bm_retry gen bm in
     (match match negb bm'.bm_failed with
            | True -> bm.bm_failed
            | False -> False with
      | True -> { bsim_bm = bm'; bsim_slots = Nil }
      | False -> { bsim_bm = bm'; bsim_slots = s.bsim_slots }))

(** val run_bnd_cmds : bnd_cmd list -> bnd_sim -> bnd_sim **)

let rec run_bnd_cmds cs s =
  match cs with
  | Nil -> s
  | Cons (c, rest) -> run_bnd_cmds rest (step_bnd_cmd c s)

(** val observe_boundary : bool -> bnd_cmd list -> boundary_model **)

let observe_boundary tracks cs =
  (run_bnd_cmds cs (init_bnd_sim tracks)).bsim_bm

type pl_cmd =
| PCmdEnter
| PCmdLeave
| PCmdAdd of nat
| PCmdReleaseChild of nat
| PCmdPlace
| PCmdDiscard

(** val run_pl_cmds :
    pl_cmd list -> nat list -> placement_model -> placement_model **)

let rec run_pl_cmds cs stack p =
  match cs with
  | Nil -> p
  | Cons (c, rest) ->
    (match c with
     | PCmdEnter ->
       let Pair (p', prev) = pl_enter p in
       run_pl_cmds rest (Cons (prev, stack)) p'
     | PCmdLeave ->
       (match stack with
        | Nil -> run_pl_cmds rest Nil p
        | Cons (prev, st') -> run_pl_cmds rest st' (pl_leave prev p))
     | PCmdAdd h -> run_pl_cmds rest stack (pl_add h p)
     | PCmdReleaseChild h -> run_pl_cmds rest stack (pl_enqueue h True p)
     | PCmdPlace -> run_pl_cmds rest stack (pl_place p)
     | PCmdDiscard -> run_pl_cmds rest stack (pl_discard p))

type pl_view = { pv_held : nat; pv_executed : nat list;
                 pv_to_parent : nat list }

(** val observe_placement : bool -> pl_cmd list -> pl_view **)

let observe_placement parent_placed cs =
  let p = run_pl_cmds cs Nil (init_placement parent_placed) in
  { pv_held = (pl_held_count p); pv_executed = p.pl_executed; pv_to_parent =
  p.pl_to_parent }

type q_cmd =
| QCmdReserve of nat * bool
| QCmdEnqueue of nat * nat
| QCmdCancel of nat
| QCmdFlush of commit_failure * nat list
| QCmdRunTicket of nat * commit_failure * nat list
| QCmdDispose

(** val step_q_cmd : q_cmd -> queue_model -> queue_model **)

let step_q_cmd c q =
  match c with
  | QCmdReserve (sc, hd) ->
    (match q.q_disposed with
     | True -> q
     | False -> fst (q_reserve sc hd q))
  | QCmdEnqueue (idx, act) -> q_enqueue idx act q
  | QCmdCancel idx -> q_cancel idx q
  | QCmdFlush (mode, failing) ->
    q_flush mode (fun a -> existsb (Nat.eqb a) failing) q
  | QCmdRunTicket (tidx, mode, failing) ->
    (match nth_error q.q_tickets tidx with
     | Some t ->
       q_run_posted_ticket t mode (fun a -> existsb (Nat.eqb a) failing) q
     | None -> q)
  | QCmdDispose -> q_dispose q

(** val run_q_cmds : q_cmd list -> queue_model -> queue_model **)

let rec run_q_cmds cs q =
  match cs with
  | Nil -> q
  | Cons (c, rest) -> run_q_cmds rest (step_q_cmd c q)

type q_view = { qv_committed : nat list; qv_dropped : nat list;
                qv_reported : (nat, nat list) prod list;
                qv_pending_len : nat; qv_disposed : bool }

(** val observe_queue : q_cmd list -> q_view **)

let observe_queue cs =
  let q = run_q_cmds cs init_queue in
  { qv_committed = q.q_committed; qv_dropped =
  (map (fun s -> s.sl_dropped_runs) q.q_slots); qv_reported =
  (report_by_scope q.q_errors); qv_pending_len = (length q.q_pending);
  qv_disposed = q.q_disposed }

type owq_cmd =
| OWQEnqueue of nat * nat
| OWQDequeue
| OWQSetWake of bool
| OWQStop

(** val run_owq_cmds :
    owq_cmd list -> nat list -> owq_model -> (nat list, owq_model) prod **)

let rec run_owq_cmds cs deq_out q =
  match cs with
  | Nil -> Pair (deq_out, q)
  | Cons (c, rest) ->
    (match c with
     | OWQEnqueue (item, cap) ->
       run_owq_cmds rest deq_out (snd (owq_try_enqueue item cap q))
     | OWQDequeue ->
       let Pair (res, q') = owq_try_dequeue q in
       let out' =
         match res with
         | Some x -> app deq_out (Cons (x, Nil))
         | None -> deq_out
       in
       run_owq_cmds rest out' q'
     | OWQSetWake cb -> run_owq_cmds rest deq_out (owq_set_wake cb q)
     | OWQStop -> run_owq_cmds rest deq_out (owq_stop q))

type owq_view = { ov_dequeued_items : nat list; ov_count : nat;
                  ov_wakes : nat; ov_rejected : nat; ov_discarded : nat;
                  ov_stopped : bool }

(** val observe_owq : owq_cmd list -> owq_view **)

let observe_owq cs =
  let Pair (out, q) = run_owq_cmds cs Nil init_owq in
  { ov_dequeued_items = out; ov_count = (length q.owq_items); ov_wakes =
  q.owq_wakes; ov_rejected = q.owq_rejected; ov_discarded = q.owq_discarded;
  ov_stopped = q.owq_stopped }

type cf_cmd =
| CFTryComplete of fence_poll
| CFReset

(** val run_cf_cmds :
    cf_cmd list -> fence_outcome list -> fence_model -> (fence_outcome list,
    fence_model) prod **)

let rec run_cf_cmds cs outs f =
  match cs with
  | Nil -> Pair (outs, f)
  | Cons (c, rest) ->
    let Pair (o, f') =
      match c with
      | CFTryComplete poll -> cf_try_complete poll f
      | CFReset -> cf_reset f
    in
    run_cf_cmds rest (app outs (Cons (o, Nil))) f'

(** val observe_fence :
    cf_cmd list -> ((fence_outcome list, nat list) prod, bool) prod **)

let observe_fence cs =
  let Pair (outs, f) = run_cf_cmds cs Nil init_fence in
  Pair ((Pair (outs, f.cf_released)), f.cf_failed)
