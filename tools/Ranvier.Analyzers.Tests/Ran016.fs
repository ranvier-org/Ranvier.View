module Ranvier.Analyzers.Tests.Ran016

open Expecto
open FSharp.Analyzers.SDK
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private preamble =
    "module Fixture
open System
open System.Threading
open System.Threading.Tasks
open Ranvier
open Ranvier.CE
"

let private findings (body: string) =
    run Analyzers.ran016Cli (context (ranvier.Force ()) (preamble + body))

let private codes (body: string) =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN016"
        [
            test "reactive read in memo Phase 2, with its exact message, severity, and range" {
                let body =
                    "let s = createSignal 1
let other = createSignal 2
let m () =
    memo {
        let! x = s
        return x + other.Value
    }
"

                let msgs = findings body

                Expect.equal
                    (msgs |> List.map (fun m -> m.Severity, m.Message))
                    [
                        Severity.Warning,
                        "`other.Value` runs inside an untracked compute phase, so changes to `other` never wake this computation and a pending read fails it. Bind `other` with `let!`/`and!` in the tracked phase, or use `other.Peek` / `untrack` for an intentional snapshot."
                    ]
                    "exact warning message"

                Expect.equal (textAt (preamble + body) msgs.Head.Range) "other.Value" "range is the reactive read"
            }

            test "other reactive read members: TryValue, Status, Get, TryGet, Keys, StreamMemo, Timed" {
                let body =
                    "let s = createSignal 1
let opt = createMemo (fun _ -> 2)
let st = createStream (fun _ -> Unchecked.defaultof<StreamSource<int>>)
let tm (g: Graph) = debounce TimeSpan.Zero (fun () -> s.Value) g
let proj = createProjection fst id (fun () -> [ (1, 2) ])
let lkp = createLookup (fun v k -> v + k) (fun _ _ -> Seq.empty) (fun () -> s.Value)
let private readVal (r: Reading<int>) =
    match r with
    | Reading.Ready v -> v
    | _ -> 0
let reads (g: Graph) =
    let tmNode = tm g
    let m1 =
        memo {
            let! x = s
            return x + readVal opt.TryValue
        }
    let m2 =
        memo {
            let! x = s
            return string opt.Status + string x
        }
    let m3 =
        memo {
            let! x = s
            return x + fst (proj.Get 1) + (Option.defaultValue 0 (lkp.TryGet 1))
        }
    let m4 =
        memo {
            let! x = s
            return x + Seq.length proj.Keys
        }
    let m5 =
        memo {
            let! x = s
            return x + st.Value + tmNode.Value
        }
    m1, m2, m3, m4, m5
"

                Expect.equal
                    (codes body)
                    [ "RAN016"; "RAN016"; "RAN016"; "RAN016"; "RAN016"; "RAN016"; "RAN016" ]
                    "TryValue, Status, Get, TryGet, Keys, StreamMemo.Value, Timed.Value"
            }

            test "closed Phase-2 reads across all reactive CEs" {
                let body =
                    "let s = createSignal 1
let other = createSignal 2
let proj =
    createProjectionWith fst (fun item ->
        row {
            let! v = item
            return snd v + other.Value
        }) (fun () -> [ (1, 2) ])
let allCes () =
    let e =
        effect {
            let! x = s
            ignore (x + other.Value)
        }
    let ed =
        editable {
            let! x = s
            return x + other.Value
        }
    let dr =
        draft {
            let! x = s
            return x + other.Value
        }
    let bn =
        boundary {
            pending (fun _ -> 0)
            let! x = s
            return x + other.Value
        }
    let am =
        asyncMemo {
            let! k = s
            let snap = other.Value
            do! Task.CompletedTask
            return k + snap
        }
    let st =
        stream {
            let! k = s
            return Unchecked.defaultof<StreamSource<int>> |> fun ss -> ignore (k + other.Value); ss
        }
    let stCtx =
        stream {
            let! k = s
            let! _ct = stream.cancellationToken
            let _ = k + other.Value
            return Unchecked.defaultof<StreamSource<int>>
        }
    let mo =
        memo {
            owning
            let! x = s
            return x + other.Value
        }
    let mOpt =
        memo {
            option
            let! x = s
            return Some (x + other.Value)
        }
    let mBranch (flag: Signal<bool>) =
        memo {
            let! f = flag
            if f then
                let! y = s
                return y
            else
                return other.Value
        }
    let mPrev =
        memo {
            let! x = s
            let! p = memo.previous
            return x + other.Value + (ValueOption.defaultValue 0 p)
        }
    let mTail () =
        memo {
            let! x = s
            let y = other.Value
            return! createMemo (fun _ -> x + y)
        }
    e, ed, dr, bn, am, st, stCtx, mo, mOpt, mBranch, mPrev, mTail
"

                Expect.equal (codes body |> List.length) 13 "row plus 12 CE cases in allCes"
            }

            test "non-CE closed Phase-2 callbacks: *On and combinator map/map2/bind*" {
                let body =
                    "let s = createSignal 1
let other = createSignal 2
let mSrc = createMemo (fun _ -> s.Value)
let aSrc = createAsync (fun _ _ -> Task.FromResult 1)
let aRaw = createAsyncSource<int> ()
let stSrc = createStream (fun _ -> Unchecked.defaultof<StreamSource<int>>)

let fns () =
    let m1 = createMemoOn (fun () -> s.Value) (fun x -> x + other.Value)
    let m2 = createMemoOnWithComparer HashIdentity.Structural (fun () -> s.Value) (fun x -> x + other.Value)
    let m3 = createMemoWithOn (fun () -> s.Value) (fun x -> x + other.Value)
    let m4 = createOwningMemoOnWithComparer HashIdentity.Structural (fun () -> s.Value) (fun x -> x + other.Value)
    let e1 = createEffectOn (fun () -> s.Value) (fun x -> ignore (x + other.Value))
    let e2 = createEffectOnWithComparer HashIdentity.Structural (fun () -> s.Value) (fun x -> ignore (x + other.Value))
    let a1 =
        createAsyncOn (fun () -> s.Value) (fun k _ _ ->
            task {
                let v = other.Value
                do! Task.Yield ()
                return k + v
            })
    let a2 = createAsyncWithOn (fun () -> s.Value) (fun k _ _ -> let v = other.Value in Task.FromResult (k + v))
    let a3 = createAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> s.Value) (fun k _ _ -> let v = other.Value in Task.FromResult (k + v))
    let a4 = createOwningAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> s.Value) (fun k _ _ -> let v = other.Value in Task.FromResult (k + v))
    let st1 = createStreamOn (fun () -> s.Value) (fun k _ -> ignore (k + other.Value); Unchecked.defaultof<StreamSource<int>>)
    let st2 = createStreamFoldOn 0 (+) (fun () -> s.Value) (fun k _ -> ignore (k + other.Value); Unchecked.defaultof<StreamSource<int>>)
    let cm1 = s |> Signal.map (fun x -> x + other.Value)
    let cm2 = Signal.map2 (fun x y -> x + y + other.Value) s s
    let cm3 = s |> Signal.bind (fun x -> let v = other.Value in createMemo (fun _ -> x + v))
    let mm1 = mSrc |> Memo.map (fun x -> x + other.Value)
    let mm2 = Memo.map2 (fun x y -> x + y + other.Value) mSrc mSrc
    let mm3 = mSrc |> Memo.bind (fun x -> let v = other.Value in createMemo (fun _ -> x + v))
    let am1 = aSrc |> AsyncMemo.map (fun x -> x + other.Value)
    let am2 = AsyncMemo.map2 (fun x y -> x + y + other.Value) aSrc aSrc
    let am3 = aSrc |> AsyncMemo.bind (fun x _ -> let v = other.Value in Task.FromResult (x + v))
    let am4 = aSrc |> AsyncMemo.bindWithPolicy FlightPolicy.KeepLatest (fun x _ -> let v = other.Value in Task.FromResult (x + v))
    let as1 = aRaw |> AsyncSource.map (fun x -> x + other.Value)
    let as2 = AsyncSource.map2 (fun x y -> x + y + other.Value) aRaw aRaw
    let as3 = aRaw |> AsyncSource.bind (fun x _ -> let v = other.Value in Task.FromResult (x + v))
    let sm1 = stSrc |> StreamMemo.map (fun x -> x + other.Value)
    let sm2 = StreamMemo.map2 (fun x y -> x + y + other.Value) stSrc stSrc
    let sm3 = stSrc |> StreamMemo.bind (fun x _ -> ignore (x + other.Value); Unchecked.defaultof<StreamSource<int>>)
    let sm4 = stSrc |> StreamMemo.bindFold 0 (+) (fun x _ -> ignore (x + other.Value); Unchecked.defaultof<StreamSource<int>>)
    m1, m2, m3, m4, e1, e2, a1, a2, a3, a4, st1, st2, cm1, cm2, cm3, mm1, mm2, mm3, am1, am2, am3, am4, as1, as2, as3, sm1, sm2, sm3, sm4
"

                Expect.equal (codes body |> List.length) 29 "all 29 non-CE closed Phase-2 calls"
            }

            test "post-await reads report RAN012 only, with no duplicate RAN016" {
                let body =
                    "let s = createSignal 1
let other = createSignal 2
let a1 () =
    asyncMemo {
        let! k = s
        do! Task.CompletedTask
        return k + other.Value
    }
let a2 () =
    createAsyncOn (fun () -> s.Value) (fun k _ _ ->
        task {
            do! Task.Yield ()
            return k + other.Value
        })
let a3 (am: AsyncMemo<int>) =
    am |> AsyncMemo.bind (fun k _ ->
        task {
            do! Task.Yield ()
            return k + other.Value
        })
"

                let ctx = context (ranvier.Force ()) (preamble + body)
                Expect.equal (run Analyzers.ran012Cli ctx |> List.map _.Code) [ "RAN012"; "RAN012"; "RAN012" ] "RAN012 fires"
                Expect.isEmpty (run Analyzers.ran016Cli ctx) "RAN016 does not duplicate RAN012"
            }

            test "batch inside untracked phase stays flagged while clean forms emit zero findings" {
                let batchBad =
                    "let s = createSignal 1
let other = createSignal 2
let b () = memo { let! x = s in return batch (fun () -> x + other.Value) }
"

                Expect.equal (codes batchBad) [ "RAN016" ] "batch stays in closed untracked phase"

                let clean =
                    "let s = createSignal 1
let other = createSignal 2
let okAnd =
    memo {
        let! a = s
        and! b = other
        return a + b
    }
let okPeek = s |> Signal.map (fun x -> x + other.Peek)
let okUntrack = s |> Signal.map (fun x -> x + untrack (fun () -> other.Value))
let okUntrackCe =
    memo {
        let! x = s
        return x + untrack (fun () -> other.Value)
    }
let okNestedEffect =
    memo {
        owning
        let! x = s
        createEffect (fun () -> ignore other.Value)
        return x
    }
let okNestedMemo =
    memo {
        owning
        let! x = s
        let inner =
            memo {
                let! y = other
                return x + y
            }
        return inner
    }
let okNestedBindEffect =
    Signal.bind (fun x -> createEffect (fun () -> ignore other.Value); createMemo (fun _ -> x)) s
let okCreateAsyncNoAwait =
    createAsync (fun _ _ ->
        task {
            let v = other.Value
            do! Task.Yield ()
            return v
        })
let okReadPhase =
    createMemoOn (fun () -> s.Value + other.Value) id
let okStoredCallback (handlers: ResizeArray<unit -> unit>) =
    memo {
        let! x = s
        handlers.Add (fun () -> ignore other.Value)
        return x
    }
let okZeroBind =
    memo {
        return s.Value + other.Value
    }
let okPrevBeforeBind =
    memo {
        let! prev = memo.previous
        let x = other.Value
        let! a = s
        return (ValueOption.defaultValue 0 prev) + x + a
    }
"

                Expect.isEmpty
                    (codes clean)
                    "and!, Peek, untrack, nested tracked bodies, Phase-1 read, stored callback, 0-bind, and pre-bind context token are clean"

                let v024Bad =
                    "let s = createSignal 1
let other = createSignal 2
let ed = createDraft (fun _ -> 10)
let b1 () = memo { let! x = s in return batched { return x + other.Value } }
let b2 () = memo { debounce System.TimeSpan.Zero; let! x = s in return x + other.Value }
let b3 () = memo { let! x = s in return (if ed.IsEdited then ed.Upstream else x) }
"

                Expect.equal
                    (codes v024Bad)
                    [ "RAN016"; "RAN016"; "RAN016"; "RAN016" ]
                    "0.2.4 batched in closed phase, timed memo Phase 2, and Editable.IsEdited/Upstream in closed phase"

                let v024Clean =
                    "let s = createSignal 1
let other = createSignal 2
let ed = createDraft (fun _ -> 10)
let c1 () = memo { let! x = s in return x + untracked { return other.Value } }
let c2 () =
    memo {
        let! x = s
        and! ie = Editable.isEdited ed
        and! up = Editable.upstream ed
        return if ie then up else x
    }
"

                Expect.isEmpty (codes v024Clean) "0.2.4 untracked CE and Editable.isEdited/upstream in and! stay clean"
            }
        ]
