(* OCaml driver for Ranvier.View differential conformance.
   Reads one S-expression program per line from stdin, runs the extracted Rocq pure model
   from [Engine], and prints [<index>\tok\t<canonical_observation>] per line. *)

open Engine

type sexp = Atom of string | Node of sexp Stdlib.List.t

let parse_sexp (text : string) : sexp =
  let n = Stdlib.String.length text in
  let i = ref 0 in
  let skip () =
    while !i < n && (text.[!i] = ' ' || text.[!i] = '\t' || text.[!i] = '\r') do
      incr i
    done
  in
  let rec one () =
    skip ();
    if text.[!i] = '(' then begin
      incr i;
      let items = ref [] in
      skip ();
      while text.[!i] <> ')' do
        items := one () :: !items;
        skip ()
      done;
      incr i;
      Node (Stdlib.List.rev !items)
    end else begin
      let start = !i in
      while !i < n && text.[!i] <> ' ' && text.[!i] <> '\t' && text.[!i] <> '\r'
            && text.[!i] <> ')' && text.[!i] <> '(' do
        incr i
      done;
      Atom (Stdlib.String.sub text start (!i - start))
    end
  in
  one ()

let rec nat_of_int (n : int) : nat = if n <= 0 then O else S (nat_of_int (n - 1))

let int_of_nat (n : nat) : int =
  let rec go acc = function O -> acc | S m -> go (acc + 1) m in
  go 0 n

let rec positive_of_int (n : int) : positive =
  if n = 1 then XH
  else if n land 1 = 1 then XI (positive_of_int (n lsr 1))
  else XO (positive_of_int (n lsr 1))

let rec int_of_positive (p : positive) : int =
  match p with
  | XH -> 1
  | XO q -> 2 * int_of_positive q
  | XI q -> (2 * int_of_positive q) + 1

let z_of_int (n : int) : z =
  if n = 0 then Z0 else if n > 0 then Zpos (positive_of_int n) else Zneg (positive_of_int (-n))

let int_of_z (v : z) : int =
  match v with
  | Z0 -> 0
  | Zpos p -> int_of_positive p
  | Zneg p -> -int_of_positive p

let bool_of_atom = function
  | Atom "1" | Atom "true" -> True
  | Atom "0" | Atom "false" -> False
  | _ -> failwith "expected bool"

let str_of_bool = function True -> "true" | False -> "false"

let atom_int = function
  | Atom a -> int_of_string a
  | Node _ -> failwith "expected int atom"

let rec coq_list_of_ocaml = function
  | [] -> Nil
  | x :: xs -> Cons (x, coq_list_of_ocaml xs)

let rec ocaml_list_of_coq = function
  | Nil -> []
  | Cons (x, xs) -> x :: ocaml_list_of_coq xs

let nat_list_of_sexp = function
  | Node xs -> coq_list_of_ocaml (Stdlib.List.map (fun x -> nat_of_int (atom_int x)) xs)
  | Atom _ -> failwith "expected list"

let fmt_nat_list (xs : nat list) : string =
  "[" ^ Stdlib.String.concat ";" (Stdlib.List.map (fun n -> string_of_int (int_of_nat n)) (ocaml_list_of_coq xs)) ^ "]"

(* 1. Layout *)
let read_layout_cmd = function
  | Node [ Atom "reg-exit"; n ] -> LCmdRegisterExit (nat_of_int (atom_int n))
  | Node [ Atom "reconcile"; rows ] -> LCmdReconcile (nat_list_of_sexp rows)
  | Node [ Atom "fin-exit"; n ] -> LCmdFinishExit (nat_of_int (atom_int n))
  | Node [ Atom "rf-before" ] -> LCmdReflowBefore
  | Node [ Atom "rf-post"; idx ] -> LCmdReflowPost (nat_of_int (atom_int idx))
  | Node [ Atom "rf-cancel"; idx ] -> LCmdReflowCancel (nat_of_int (atom_int idx))
  | _ -> failwith "bad layout_cmd"

let run_layout = function
  | init_rows :: cmds ->
    let v = observe_layout (nat_list_of_sexp init_rows)
              (coq_list_of_ocaml (Stdlib.List.map read_layout_cmd cmds)) in
    Printf.sprintf "dom=%s;live=%s;open=%s;before=%d;after=%d"
      (fmt_nat_list v.lv_dom)
      (fmt_nat_list v.lv_live)
      (str_of_bool v.lv_open)
      (int_of_nat v.lv_before)
      (int_of_nat v.lv_after)
  | _ -> failwith "bad layout prog"

(* 2. SSR markers *)
let rec read_ssr_tree = function
  | Node [ Atom "node"; id ] -> SNode (nat_of_int (atom_int id))
  | Node (Atom "reg" :: tag :: children) ->
    SRegion (nat_of_int (atom_int tag), coq_list_of_ocaml (Stdlib.List.map read_ssr_tree children))
  | _ -> failwith "bad ssr_tree"

let fmt_tok = function
  | TokStart t -> "S" ^ string_of_int (int_of_nat t)
  | TokEnd -> "E"
  | TokNode id -> "N" ^ string_of_int (int_of_nat id)

let run_ssr = function
  | [ tree; suf ] ->
    let t = read_ssr_tree tree in
    let s =
      match suf with
      | Node xs -> coq_list_of_ocaml (Stdlib.List.map (fun x -> TokNode (nat_of_int (atom_int x))) xs)
      | _ -> failwith "bad ssr suf"
    in
    begin match logical_next (app (flatten_tree t) s) with
    | None -> "none"
    | Some rem ->
      "[" ^ Stdlib.String.concat ";" (Stdlib.List.map fmt_tok (ocaml_list_of_coq rem)) ^ "]"
    end
  | _ -> failwith "bad ssr prog"

(* 3. HydrationCursor *)
let rec read_hyd_op = function
  | Node [ Atom "add"; d ] -> HAdd (z_of_int (atom_int d))
  | Node (Atom "scope" :: sub) -> HScope (coq_list_of_ocaml (Stdlib.List.map read_hyd_op sub))
  | _ -> failwith "bad hyd_op"

let run_cursor = function
  | s0 :: do_commit :: ops ->
    let Pair (Pair (s_mid, s_after), held) =
      observe_cursor_rollback (z_of_int (atom_int s0))
        (coq_list_of_ocaml (Stdlib.List.map read_hyd_op ops))
        (bool_of_atom do_commit)
    in
    Printf.sprintf "mid=%d;after=%d;held=%d"
      (int_of_z s_mid) (int_of_z s_after) (int_of_nat held)
  | _ -> failwith "bad cursor prog"

(* 4. Boundary + BindingState *)
let read_outcome_of_sexp = function
  | Atom "val" -> OutValue
  | Atom "pending" -> OutNotReady
  | Atom "err" -> OutError
  | _ -> failwith "bad read_outcome"

let read_bnd_cmd = function
  | Node [ Atom "expect" ] -> BCmdExpect
  | Node [ Atom "step"; idx; out ] -> BCmdStep (nat_of_int (atom_int idx), read_outcome_of_sexp out)
  | Node [ Atom "dispose"; idx ] -> BCmdDispose (nat_of_int (atom_int idx))
  | Node [ Atom "end-setup" ] -> BCmdEndSetup
  | Node [ Atom "reveal" ] -> BCmdReveal
  | Node [ Atom "retry"; g ] -> BCmdRetry (nat_of_int (atom_int g))
  | _ -> failwith "bad bnd_cmd"

let run_boundary = function
  | tracks :: cmds ->
    let bm = observe_boundary (bool_of_atom tracks)
               (coq_list_of_ocaml (Stdlib.List.map read_bnd_cmd cmds)) in
    Printf.sprintf "wait=%d;setup=%s;rev=%s;fail=%s;pend=%s;wakes=%d;clear=%d;fails=%d;gen=%d"
      (int_of_nat bm.bm_waiting)
      (str_of_bool bm.bm_setting_up)
      (str_of_bool bm.bm_revealed)
      (str_of_bool bm.bm_failed)
      (str_of_bool bm.bm_pending_sig)
      (int_of_nat bm.bm_wakes)
      (int_of_nat bm.bm_clear_calls)
      (int_of_nat bm.bm_fail_calls)
      (int_of_nat bm.bm_gen)
  | _ -> failwith "bad boundary prog"

(* 5. Placement *)
let read_pl_cmd = function
  | Node [ Atom "enter" ] -> PCmdEnter
  | Node [ Atom "leave" ] -> PCmdLeave
  | Node [ Atom "add"; h ] -> PCmdAdd (nat_of_int (atom_int h))
  | Node [ Atom "rel-child"; h ] -> PCmdReleaseChild (nat_of_int (atom_int h))
  | Node [ Atom "place" ] -> PCmdPlace
  | Node [ Atom "discard" ] -> PCmdDiscard
  | _ -> failwith "bad pl_cmd"

let run_placement = function
  | parent_placed :: cmds ->
    let v = observe_placement (bool_of_atom parent_placed)
              (coq_list_of_ocaml (Stdlib.List.map read_pl_cmd cmds)) in
    Printf.sprintf "held=%d;exec=%s;parent=%s"
      (int_of_nat v.pv_held)
      (fmt_nat_list v.pv_executed)
      (fmt_nat_list v.pv_to_parent)
  | _ -> failwith "bad placement prog"

(* 6. MountLifetime tree *)
let rec read_lt_tree = function
  | Node (Atom "lt" :: id :: cleanups :: children) ->
    LTNode (nat_of_int (atom_int id),
            nat_list_of_sexp cleanups,
            coq_list_of_ocaml (Stdlib.List.map read_lt_tree children))
  | _ -> failwith "bad lt_tree"

let fmt_lt_ev = function
  | EvOwnerCleanup (id, c) -> Printf.sprintf "C(%d,%d)" (int_of_nat id) (int_of_nat c)
  | EvDomRelease id -> Printf.sprintf "R(%d)" (int_of_nat id)

let run_lifetime = function
  | [ tree ] ->
    let evs = dispose_lifetime_tree (read_lt_tree tree) in
    "[" ^ Stdlib.String.concat ";" (Stdlib.List.map fmt_lt_ev (ocaml_list_of_coq evs)) ^ "]"
  | _ -> failwith "bad lifetime prog"

(* 7. TeardownWindow *)
let read_pair = function
  | Node [ a; b ] -> Pair (nat_of_int (atom_int a), nat_of_int (atom_int b))
  | _ -> failwith "bad pair"

let run_window = function
  | start :: len :: ivs ->
    let out = teardown_window_indices (nat_of_int (atom_int start))
                (nat_of_int (atom_int len))
                (coq_list_of_ocaml (Stdlib.List.map read_pair ivs)) in
    fmt_nat_list out
  | _ -> failwith "bad window prog"

(* 8. Scheduling.Queue *)
let read_mode = function
  | Atom "collect" -> CollectAndContinue
  | Atom "failfast" -> FailFastCancel
  | _ -> failwith "bad mode"

let read_reenq_triple = function
  | Node [ tr; tg; na ] ->
    Pair (Pair (nat_of_int (atom_int tr), nat_of_int (atom_int tg)), nat_of_int (atom_int na))
  | _ -> failwith "bad reenq triple"

let read_q_cmd = function
  | Node [ Atom "reserve"; sc; hd ] -> QCmdReserve (nat_of_int (atom_int sc), bool_of_atom hd)
  | Node [ Atom "enqueue"; idx; act ] -> QCmdEnqueue (nat_of_int (atom_int idx), nat_of_int (atom_int act))
  | Node [ Atom "cancel"; idx ] -> QCmdCancel (nat_of_int (atom_int idx))
  | Node [ Atom "flush"; mode; failing ] -> QCmdFlush (read_mode mode, nat_list_of_sexp failing)
  | Node [ Atom "flush-reenq"; mode; failing; Node triples ] ->
    QCmdFlushReenq (read_mode mode, nat_list_of_sexp failing, coq_list_of_ocaml (Stdlib.List.map read_reenq_triple triples))
  | Node [ Atom "ticket"; tidx; mode; failing ] ->
    QCmdRunTicket (nat_of_int (atom_int tidx), read_mode mode, nat_list_of_sexp failing)
  | Node [ Atom "dispose" ] -> QCmdDispose
  | _ -> failwith "bad q_cmd"

let fmt_reported (xs : (nat, nat list) prod list) : string =
  let items =
    Stdlib.List.map
      (fun (Pair (sc, errs)) -> Printf.sprintf "%d:%s" (int_of_nat sc) (fmt_nat_list errs))
      (ocaml_list_of_coq xs)
  in
  "[" ^ Stdlib.String.concat "," items ^ "]"

let run_queue cmds =
  let v = observe_queue (coq_list_of_ocaml (Stdlib.List.map read_q_cmd cmds)) in
  Printf.sprintf "comm=%s;drop=%s;rep=%s;pend=%d;disp=%s"
    (fmt_nat_list v.qv_committed)
    (fmt_nat_list v.qv_dropped)
    (fmt_reported v.qv_reported)
    (int_of_nat v.qv_pending_len)
    (str_of_bool v.qv_disposed)

(* 9. OwnerWorkQueue *)
let read_owq_cmd = function
  | Node [ Atom "enq"; item; cap ] -> OWQEnqueue (nat_of_int (atom_int item), nat_of_int (atom_int cap))
  | Node [ Atom "deq" ] -> OWQDequeue
  | Node [ Atom "wake"; cb ] -> OWQSetWake (bool_of_atom cb)
  | Node [ Atom "stop" ] -> OWQStop
  | _ -> failwith "bad owq_cmd"

let run_owq cmds =
  let v = observe_owq (coq_list_of_ocaml (Stdlib.List.map read_owq_cmd cmds)) in
  Printf.sprintf "deq=%s;cnt=%d;wakes=%d;rej=%d;disc=%d;stop=%s"
    (fmt_nat_list v.ov_dequeued_items)
    (int_of_nat v.ov_count)
    (int_of_nat v.ov_wakes)
    (int_of_nat v.ov_rejected)
    (int_of_nat v.ov_discarded)
    (str_of_bool v.ov_stopped)

(* 10. CompletionFence *)
let read_poll = function
  | Atom "not-ready" -> PollNotReady
  | Atom "ready" -> PollReady
  | Atom "gpu-fail" -> PollGpuFailure
  | _ -> failwith "bad poll"

let read_cf_cmd = function
  | Node [ Atom "try"; p ] -> CFTryComplete (read_poll p)
  | Node [ Atom "reset" ] -> CFReset
  | _ -> failwith "bad cf_cmd"

let fmt_cf_out = function
  | FenceOk True -> "ok1"
  | FenceOk False -> "ok0"
  | FenceError -> "err"

let run_fence cmds =
  let Pair (Pair (outs, rel), failed) =
    observe_fence (coq_list_of_ocaml (Stdlib.List.map read_cf_cmd cmds))
  in
  Printf.sprintf "outs=[%s];rel=%s;fail=%s"
    (Stdlib.String.concat ";" (Stdlib.List.map fmt_cf_out (ocaml_list_of_coq outs)))
    (fmt_nat_list rel)
    (str_of_bool failed)

let eval_program = function
  | Node (Atom "layout" :: rest) -> run_layout rest
  | Node (Atom "ssr" :: rest) -> run_ssr rest
  | Node (Atom "cursor" :: rest) -> run_cursor rest
  | Node (Atom "boundary" :: rest) -> run_boundary rest
  | Node (Atom "placement" :: rest) -> run_placement rest
  | Node (Atom "lifetime" :: rest) -> run_lifetime rest
  | Node (Atom "window" :: rest) -> run_window rest
  | Node (Atom "queue" :: rest) -> run_queue rest
  | Node (Atom "owq" :: rest) -> run_owq rest
  | Node (Atom "fence" :: rest) -> run_fence rest
  | _ -> failwith "unknown program kind"

let () =
  let idx = ref 0 in
  try
    while true do
      let line = input_line stdin in
      if Stdlib.String.trim line <> "" then begin
        let obs = eval_program (parse_sexp line) in
        Printf.printf "%d\tok\t%s\n" !idx obs;
        incr idx
      end
    done
  with End_of_file -> ()
