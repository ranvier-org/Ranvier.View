module Ranvier.Analyzers.Tests.Ran010

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\n"
        + body

    run Analyzers.ran010Cli (context (ranvier.Force ()) source)

let private codes body =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN010"
        [
            test "an effect in createMemo's body, with its exact message" {
                Expect.equal
                    (findings "let m () = createMemo (fun _ -> createEffect ignore; 1)\n"
                     |> List.map _.Message)
                    [
                        "`createEffect` creates an owned node inside `createMemo`'s body, which fails every read of the memo. Use `createMemoWith` for a memo that owns the nodes its body creates, or create the node outside the memo."
                    ]
                    "one message"
            }

            test "a memo in createAsync's compute" {
                Expect.equal
                    (codes "let a () = createAsync (fun _ _ -> createMemo (fun _ -> 1) |> ignore; Task.FromResult 1)\n")
                    [ "RAN010" ]
                    "createAsync"
            }

            test "a creator in createEffectOn's compute" {
                Expect.equal (codes "let e () = createEffectOn (fun () -> onCleanup ignore; 1) ignore\n") [ "RAN010" ] "createEffectOn"
            }

            test "a creator in a projection's map" {
                Expect.equal
                    (codes "let p () = createProjection id (fun x -> createSignal 1 |> ignore; createEffect ignore; x) (fun () -> [ 1 ])\n")
                    [ "RAN010" ]
                    "createProjection map"
            }

            test "a creator in a projection row's reader" {
                Expect.equal
                    (codes "let p () = createProjectionWith id (fun item -> fun () -> createEffect ignore; item ()) (fun () -> [ 1 ])\n")
                    [ "RAN010" ]
                    "the reader"
            }

            test "a creator in a lookup's f and affected" {
                Expect.equal
                    (codes "let l () = createLookup (fun s k -> onCleanup ignore; s + k) (fun _ _ -> onCleanup ignore; Seq.empty) (fun () -> 1)\n")
                    [ "RAN010"; "RAN010" ]
                    "f and affected"
            }

            test "a creator in an editable's seed" {
                Expect.equal (codes "let e () = createEditable (fun _ -> onCleanup ignore; 1)\n") [ "RAN010" ] "seed"
            }

            test "a creator in a projection aggregate's folder" {
                Expect.equal
                    (codes "let f (p: Projection<int, int>) = p |> Projection.fold (fun s v -> onCleanup ignore; s + v) 0\n")
                    [ "RAN010" ]
                    "fold"
            }

            test "a creator under untrack" {
                Expect.equal
                    (codes "let m () = createMemo (fun _ -> untrack (fun () -> createEffect ignore); 1)\n")
                    [ "RAN010" ]
                    "untrack is the same run"
            }

            test "a creator inside try ... with" {
                Expect.equal (codes "let m () = createMemo (fun _ -> try createEffect ignore; 1 with _ -> 0)\n") [ "RAN010" ] "caught creator"
            }

            test "the Memo constructor without the owning argument" {
                Expect.equal
                    (codes "let m (g: Graph) = Memo (g, System.Func<int voption, int>(fun _ -> createEffect ignore; 1))\n")
                    [ "RAN010" ]
                    "Memo (graph, compute)"
            }

            test "a pipelined createMemo body is a pure context" {
                Expect.equal (codes "let m () = (fun _ -> createEffect ignore; 1) |> createMemo\n") [ "RAN010" ] "pipe"
            }

            test "a stored callback inside a projection factory" {
                Expect.isEmpty
                    (codes
                        "let p (handlers: ResizeArray<unit -> unit>) = createProjectionWith id (fun item -> handlers.Add (fun () -> createEffect ignore); fun () -> item ()) (fun () -> [ 1 ])
")
                    "the callback runs later, not as the row's reader"
            }

            test "the finding's range is the creator call" {
                let range =
                    (findings "let m () = createMemo (fun _ -> createEffect ignore; 1)\n").Head.Range

                Expect.equal (range.StartLine, range.StartColumn, range.EndColumn) (4, 32, 51) "createEffect ignore"
            }

            test "createMemoWith owns its nodes" {
                Expect.isEmpty (codes "let m () = createMemoWith (fun _ -> createEffect ignore; 1)\n") "owning memo"
            }

            test "a creator in the projection factory body" {
                Expect.isEmpty
                    (codes "let p () = createProjectionWith id (fun item -> createEffect ignore; fun () -> item ()) (fun () -> [ 1 ])\n")
                    "the factory runs once per key"
            }

            test "a creator in a stored callback" {
                Expect.isEmpty
                    (codes "let m (handlers: ResizeArray<unit -> unit>) = createMemo (fun _ -> handlers.Add (fun () -> createEffect ignore); 1)\n")
                    "the callback runs later"
            }

            test "createSignal in a memo" {
                Expect.isEmpty (codes "let m () = createMemo (fun _ -> (createSignal 1).Value)\n") "a signal is not owned"
            }

            test "a user function named createMemo is not a context" {
                Expect.isEmpty
                    (codes "let createMemo (f: unit -> int) = f ()\nlet m () = createMemo (fun () -> createEffect ignore; 1)\n")
                    "own createMemo"
            }

            test "a project without Ranvier is not analyzed" {
                let source = "module Fixture\nlet x = 1\n"
                Expect.isEmpty (run Analyzers.ran010Cli (context (plain.Force ()) source)) "no Ranvier reference"
            }

            test "the fixes rename the comparer, async and policy forms" {
                for before, after in
                    [
                        "createMemoWithComparer HashIdentity.Structural (fun _ -> createEffect ignore; 1)",
                        "createOwningMemoWithComparer HashIdentity.Structural (fun _"
                        "createAsync (fun _ _ -> createEffect ignore; Task.FromResult 1)", "createAsyncWith (fun _ _"
                        "createAsyncWithPolicy FlightPolicy.KeepLatest (fun _ _ -> createEffect ignore; Task.FromResult 1)",
                        "createOwningAsyncWithPolicy FlightPolicy.KeepLatest (fun _ _"
                    ] do
                    let source =
                        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\nlet m () = "
                        + before
                        + "\n"

                    let fix =
                        (run Analyzers.ran010Cli (context (ranvier.Force ()) source)).Head.Fixes
                        |> List.exactlyOne

                    let repaired = applyFix source fix
                    Expect.stringContains repaired after "renamed"
                    Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)) "no finding after the fix"
            }

            test "the fix renames createMemo to createMemoWith" {
                let source =
                    "module Fixture\nopen Ranvier\nlet m () = createMemo (fun _ -> createEffect ignore; 1)\n"

                let fix =
                    (run Analyzers.ran010Cli (context (ranvier.Force ()) source)).Head.Fixes
                    |> List.exactlyOne

                let repaired = applyFix source fix
                Expect.stringContains repaired "createMemoWith (fun _" "renamed"
                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)) "no finding after the fix"
            }

            test "createMemoOn read and compute report RAN010, and compute fixes to createMemoWithOn" {
                let source =
                    "module Fixture\nopen Ranvier\nlet s = createSignal 1\nlet m () = createMemoOn (fun () -> createEffect ignore; s.Value) (fun x -> onCleanup ignore; x)\n"

                let msgs = run Analyzers.ran010Cli (context (ranvier.Force ()) source)
                Expect.equal (msgs |> List.map _.Code) [ "RAN010"; "RAN010" ] "both read and compute"
                Expect.isEmpty msgs[0].Fixes "read has no owning fix"
                let computeFix = msgs[1].Fixes |> List.exactlyOne
                let repaired = applyFix source computeFix
                Expect.stringContains repaired "createMemoWithOn" "compute renamed"

                Expect.equal
                    (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)
                     |> List.map _.Code)
                    [ "RAN010" ]
                    "only read remains"
            }

            test "createMemoOnWithComparer compute fixes to createOwningMemoOnWithComparer, and owning variants allow creators in compute" {
                let source =
                    "module Fixture\nopen Ranvier\nlet s = createSignal 1\nlet m () = createMemoOnWithComparer HashIdentity.Structural (fun () -> s.Value) (fun x -> createEffect ignore; x)\n"

                let msgs = run Analyzers.ran010Cli (context (ranvier.Force ()) source)
                Expect.equal (msgs |> List.map _.Code) [ "RAN010" ] "compute only"
                let repaired = applyFix source (msgs.Head.Fixes |> List.exactlyOne)
                Expect.stringContains repaired "createOwningMemoOnWithComparer" "renamed"
                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)) "clean after fix"

                Expect.isEmpty
                    (codes "let ok () = createMemoWithOn (fun () -> 1) (fun x -> createEffect ignore; x)\n")
                    "createMemoWithOn compute is owning"

                Expect.equal
                    (codes
                        "let bad () = createOwningMemoOnWithComparer HashIdentity.Structural (fun () -> onCleanup ignore; 1) (fun x -> createEffect ignore; x)\n")
                    [ "RAN010" ]
                    "read stays pure on owning form"
            }

            test "createAsyncOn and createAsyncOnWithPolicy fix to owning variants, and createAsyncWithOn is owning in compute" {
                for before, after in
                    [
                        "createAsyncOn (fun () -> 1) (fun x _ _ -> createEffect ignore; Task.FromResult x)", "createAsyncWithOn (fun () -> 1)"
                        "createAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> 1) (fun x _ _ -> createEffect ignore; Task.FromResult x)",
                        "createOwningAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> 1)"
                    ] do
                    let source =
                        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\nlet a () = "
                        + before
                        + "\n"

                    let msgs = run Analyzers.ran010Cli (context (ranvier.Force ()) source)
                    Expect.equal (msgs |> List.map _.Code) [ "RAN010" ] "one finding"
                    let repaired = applyFix source (msgs.Head.Fixes |> List.exactlyOne)
                    Expect.stringContains repaired after "renamed"
                    Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)) "clean after fix"

                Expect.equal
                    (codes "let bad () = createAsyncWithOn (fun () -> onCleanup ignore; 1) (fun x _ _ -> createEffect ignore; Task.FromResult x)\n")
                    [ "RAN010" ]
                    "read is pure while compute is owning"

                Expect.isEmpty
                    (codes
                        "let ok () = createOwningAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> 1) (fun x _ _ -> onCleanup ignore; Task.FromResult x)\n")
                    "owning async with policy allows cleanup"
            }

            test "stream, combinator, timed, projection, and grouping creators and pure contexts" {
                let body1 =
                    "let s = createSignal 1
let st = createStreamOn (fun () -> onCleanup ignore; s.Value) (fun _ _ -> createEffect ignore; Unchecked.defaultof<StreamSource<int>>)
let stf = createStreamFoldOn 0 (+) (fun () -> onCleanup ignore; s.Value) (fun _ _ -> createEffect ignore; Unchecked.defaultof<StreamSource<int>>)
"

                Expect.equal (codes body1) [ "RAN010"; "RAN010"; "RAN010"; "RAN010" ] "createStreamOn and createStreamFoldOn"

                let body2 =
                    "let s = createSignal 1
let sm = s |> Signal.map (fun x -> createEffect ignore; x)
let sm2 = Signal.map2 (fun a b -> onCleanup ignore; a + b) s s
let sb = s |> Signal.bind (fun x -> createEffect ignore; createMemo (fun _ -> x))
let ab = createAsync (fun _ _ -> Task.FromResult 1) |> AsyncMemo.bind (fun x _ -> createEffect ignore; Task.FromResult x)
let abp = createAsync (fun _ _ -> Task.FromResult 1) |> AsyncMemo.bindWithPolicy FlightPolicy.KeepLatest (fun x _ -> createEffect ignore; Task.FromResult x)
let asb = (createAsyncSource<int> ()) |> AsyncSource.bind (fun x _ -> createEffect ignore; Task.FromResult x)
let smb = createStream (fun _ -> Unchecked.defaultof<StreamSource<int>>) |> StreamMemo.bind (fun _ _ -> createEffect ignore; Unchecked.defaultof<StreamSource<int>>)
let timed (g: Graph) = debounce System.TimeSpan.Zero (fun () -> onCleanup ignore; s.Value) g
"

                Expect.equal
                    (codes body2)
                    [ "RAN010"; "RAN010"; "RAN010"; "RAN010"; "RAN010"; "RAN010"; "RAN010" ]
                    "combinators and debounce (Signal.bind is owning)"

                let makeInPure expr =
                    $"let s = createSignal 1\nlet inPure (p: Projection<int, int>) (l: Lookup<int, int>) (g: Grouping<int, int, int>) =\n    createMemo (fun _ ->\n        {expr}\n        ())\n"

                Expect.equal (codes (makeInPure "createEffectNode ignore |> ignore")) [ "RAN010" ] "createEffectNode in pure memo"
                Expect.isEmpty (codes (makeInPure "Signal.update s id")) "Signal.update is not a creator"
                Expect.isEmpty (codes (makeInPure "Projection.at 1 p |> ignore")) "Projection.at is not a creator"
                Expect.isEmpty (codes (makeInPure "Projection.keys p |> ignore")) "Projection.keys is not a creator"
                Expect.isEmpty (codes (makeInPure "Lookup.tryAt 1 l |> ignore")) "Lookup.tryAt is not a creator"
                Expect.equal (codes (makeInPure "p |> Projection.filter (fun v -> v > 0) |> ignore")) [ "RAN010" ] "Projection.filter in pure memo"
                Expect.equal (codes (makeInPure "p |> Projection.sumBy id |> ignore")) [ "RAN010" ] "Projection.sumBy in pure memo"
                Expect.equal (codes (makeInPure "g |> Grouping.aggregate Projection.length |> ignore")) [ "RAN010" ] "Grouping.aggregate in pure memo"
                Expect.equal (codes (makeInPure "p |> Projection.length |> ignore")) [ "RAN010" ] "Projection.length in pure memo"
                Expect.equal (codes (makeInPure "s |> Signal.map id |> ignore")) [ "RAN010" ] "Signal.map in pure memo"
                Expect.equal (codes (makeInPure "createEditable (fun _ -> 1) |> ignore")) [ "RAN010" ] "createEditable in pure memo"
                Expect.equal (codes (makeInPure "createDraft (fun _ -> 1) |> ignore")) [ "RAN010" ] "createDraft in pure memo"
            }

            test "memo CE pure vs owning vs option vs MemoTail return!" {
                let ce (extra: string) =
                    "module Fixture\nopen System\nopen System.Threading\nopen System.Threading.Tasks\nopen Ranvier\nopen Ranvier.CE\n"
                    + extra

                let pureMemoSrc =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        let! x = s
        createEffect ignore
        return x
    }
"

                let msgs = run Analyzers.ran010Cli (context (ranvier.Force ()) pureMemoSrc)
                Expect.equal (msgs |> List.map _.Code) [ "RAN010" ] "pure memo CE"
                let repaired = applyFix pureMemoSrc (msgs.Head.Fixes |> List.exactlyOne)
                Expect.stringContains repaired "owning" "inserts owning"
                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) repaired)) "clean after fix"

                let commentBraceSrc =
                    ce
                        "let s = createSignal 1
let m () =
    memo { // keeps body aligned
        let! x = s
        createEffect ignore
        return x
    }
"

                let commentMsgs =
                    run Analyzers.ran010Cli (context (ranvier.Force ()) commentBraceSrc)

                Expect.equal (commentMsgs |> List.map _.Code) [ "RAN010" ] "memo with trailing brace comment"

                let commentRepaired =
                    applyFix commentBraceSrc (commentMsgs.Head.Fixes |> List.exactlyOne)

                Expect.stringContains commentRepaired "memo { // keeps body aligned\n        owning\n" "inserts owning on next line aligned with body"

                Expect.isEmpty
                    (run Analyzers.ran010Cli (context (ranvier.Force ()) commentRepaired))
                    "repaired comment brace memo compiles and is clean"

                let singleLineSrc =
                    ce "let s = createSignal 1\nlet m () = memo { createEffect ignore; return s.Value }\n"

                let singleMsgs = run Analyzers.ran010Cli (context (ranvier.Force ()) singleLineSrc)
                Expect.equal (singleMsgs |> List.map _.Code) [ "RAN010" ] "single-line pure memo"

                let singleRepaired =
                    applyFix singleLineSrc (singleMsgs.Head.Fixes |> List.exactlyOne)

                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) singleRepaired)) "single-line fix compiles and is clean"

                let comparerMemoSrc =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        comparer HashIdentity.Structural
        let! x = s
        createEffect ignore
        return x
    }
"

                let cmpMsgs = run Analyzers.ran010Cli (context (ranvier.Force ()) comparerMemoSrc)
                Expect.equal (cmpMsgs |> List.map _.Code) [ "RAN010" ] "comparer pure memo"
                let cmpRepaired = applyFix comparerMemoSrc (cmpMsgs.Head.Fixes |> List.exactlyOne)
                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) cmpRepaired)) "comparer memo owning fix compiles cleanly"

                let owningClean =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        owning
        let! x = s
        createEffect ignore
        return x
    }
"

                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) owningClean)) "owning memo Phase 2 is clean"

                let owningPhase1Bad =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        owning
        let! x = (createEffect ignore; s)
        createEffect ignore
        return x
    }
"

                let phase1Msgs =
                    run Analyzers.ran010Cli (context (ranvier.Force ()) owningPhase1Bad)

                Expect.equal (phase1Msgs |> List.map _.Code) [ "RAN010" ] "Phase 1 stays pure even with owning"
                Expect.isEmpty phase1Msgs.Head.Fixes "Phase 1 violation offers no owning fix"

                let phase1WithComparerBad =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        owning
        let! x = (createEffect ignore; s) |> withComparer HashIdentity.Structural
        return x
    }
"

                let cmpPhase1Msgs =
                    run Analyzers.ran010Cli (context (ranvier.Force ()) phase1WithComparerBad)

                Expect.equal (cmpPhase1Msgs |> List.map _.Code) [ "RAN010" ] "Phase 1 through withComparer stays pure"
                Expect.isEmpty cmpPhase1Msgs.Head.Fixes "Phase 1 through withComparer offers no owning fix"

                let optionBad =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        option
        let! x = s
        createEffect ignore
        return Some x
    }
"

                let optionMsgs = run Analyzers.ran010Cli (context (ranvier.Force ()) optionBad)
                Expect.equal (optionMsgs |> List.map _.Code) [ "RAN010" ] "option memo Phase 2 is pure"
                Expect.isEmpty optionMsgs.Head.Fixes "option cannot combine with owning"

                let tailClean =
                    ce
                        "let s = createSignal 1
let s2 = createSignal 2
let m () =
    memo {
        let! x = s
        return! memo {
            let! y = s2
            return x + y
        }
    }
"

                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) tailClean)) "MemoTail SplitBind binder owns inner memo"

                let tailInnerBad =
                    ce
                        "let s = createSignal 1
let s2 = createSignal 2
let m () =
    memo {
        let! x = s
        return! memo {
            let! y = s2
            createEffect ignore
            return x + y
        }
    }
"

                let tailInnerMsgs =
                    run Analyzers.ran010Cli (context (ranvier.Force ()) tailInnerBad)

                Expect.equal (tailInnerMsgs |> List.map _.Code) [ "RAN010" ] "inner non-owning memo in return! still reports RAN010"
            }

            test "effect, asyncMemo, editable, draft, stream, and row CEs" {
                let ce (extra: string) =
                    "module Fixture\nopen System\nopen System.Threading\nopen System.Threading.Tasks\nopen Ranvier\nopen Ranvier.CE\n"
                    + extra

                let asyncPureSrc =
                    ce
                        "let s = createSignal 1
let a () =
    asyncMemo {
        let! x = s
        createEffect ignore
        do! Task.CompletedTask
        return x
    }
"

                let asyncMsgs = run Analyzers.ran010Cli (context (ranvier.Force ()) asyncPureSrc)
                Expect.equal (asyncMsgs |> List.map _.Code) [ "RAN010" ] "non-owning asyncMemo"
                let asyncRepaired = applyFix asyncPureSrc (asyncMsgs.Head.Fixes |> List.exactlyOne)
                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) asyncRepaired)) "asyncMemo owning fix cleans pre-await"

                let mixed =
                    ce
                        "let s = createSignal 1
let e () =
    effect {
        let! x = (onCleanup ignore; s)
        createEffect ignore
        ignore x
    }
let aOwning () =
    asyncMemo {
        owning
        let! x = (createEffect ignore; s)
        createEffect ignore
        do! Task.CompletedTask
        return x
    }
let ed () = editable { let! x = s in createEffect ignore; return x }
let dr () = draft { let! x = s in createEffect ignore; return x }
let st () = stream { let! x = s in createEffect ignore; return Unchecked.defaultof<StreamSource<int>> }
let proj () =
    createProjectionWith id (fun item ->
        row {
            let! v = item
            createEffect ignore
            return v + 1
        }) (fun () -> [ 1 ])
let projOk () =
    createProjectionWith id (fun item ->
        row {
            let! v = item
            return v + 1
        }) (fun () -> [ 1 ])
let callbackOk (handlers: ResizeArray<unit -> unit>) =
    memo {
        handlers.Add (fun () -> createEffect ignore)
        return 1
    }
"

                let mixedCodes =
                    run Analyzers.ran010Cli (context (ranvier.Force ()) mixed)
                    |> List.map _.Code

                Expect.equal
                    mixedCodes
                    [ "RAN010"; "RAN010"; "RAN010"; "RAN010"; "RAN010"; "RAN010" ]
                    "effect Phase1, asyncMemo owning Phase1, editable, draft, stream, row (projOk and callbackOk clean)"

                let v024 =
                    ce
                        "let s = createSignal 1
let scopeViolations (g: Graph) (o: Owner) =
    let m1 = memo { cleanup { () }; return 1 }
    let m2 = memo { root { () } |> ignore; return 1 }
    let m3 = memo { o { () }; return 1 }
    let m4 = memo { runWithOwner o (fun () -> ()); return 1 }
    let m5 = memo { s |> debounced TimeSpan.Zero |> ignore; return 1 }
    let m6 = memo { s |> throttled TimeSpan.Zero |> ignore; return 1 }
    let m7 = memo { batched { cleanup { () } }; return 1 }
    let m8 = memo { untracked { cleanup { () } }; return 1 }
    let m9 = memo { g { cleanup { () } }; return 1 }
    let m10 = memo { let _ = signal { cleanup { () }; return 1 } in return 1 }
    let sigClean =
        memo {
            let s1 = signal { return 1 }
            let s2 = signal { comparer HashIdentity.Structural; return 2 }
            return s1, s2
        }
    m1, m2, m3, m4, m5, m6, m7, m8, m9, m10, sigClean
"

                Expect.equal
                    (run Analyzers.ran010Cli (context (ranvier.Force ()) v024)
                     |> List.map _.Code)
                    (List.replicate 10 "RAN010")
                    "0.2.4 scope/pipe creators and transparent wrappers (signal clean)"

                let timedMemoFixSrc =
                    ce
                        "let s = createSignal 1
let m () =
    memo {
        debounce TimeSpan.Zero
        let! x = s
        cleanup { () }
        return x
    }
"

                let timedMsgs = run Analyzers.ran010Cli (context (ranvier.Force ()) timedMemoFixSrc)
                Expect.equal (timedMsgs |> List.map _.Code) [ "RAN010" ] "timed pure memo reports RAN010"

                let timedRepaired =
                    applyFix timedMemoFixSrc (timedMsgs.Head.Fixes |> List.exactlyOne)

                Expect.isEmpty (run Analyzers.ran010Cli (context (ranvier.Force ()) timedRepaired)) "timed memo owning fix is clean"
            }
        ]
