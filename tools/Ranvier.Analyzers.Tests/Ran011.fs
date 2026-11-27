module Ranvier.Analyzers.Tests.Ran011

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\n"
        + body

    run Analyzers.ran011Cli (context (ranvier.Force ()) source)

let private codes body =
    findings body |> List.map _.Code

let private fetch = "let fetch () = createAsync (fun _ _ -> Task.FromResult 1)\n"

[<Tests>]
let tests =
    testList
        "RAN011"
        [
            test "created and read in a suspense body, with its exact message" {
                let body =
                    fetch
                    + "let view () =\n    createSuspense (fun _ -> 0) (fun () ->\n        let report = createAsync (fun _ _ -> Task.FromResult 1)\n        report.Value)\n"

                Expect.equal
                    (findings body |> List.map _.Message)
                    [
                        "`report` is created inside `createSuspense`'s body and read there; each run of the body disposes `report` and starts a new flight. Create `report` outside `createSuspense` and read it inside."
                    ]
                    "one message"
            }

            test "the finding's range is the binding identifier" {
                let range =
                    (findings
                        "let view () =
    createSuspense (fun _ -> 0) (fun () ->
        let report = createAsync (fun _ _ -> Task.FromResult 1)
        report.Value)\n")
                        .Head.Range

                Expect.equal (range.StartLine, range.StartColumn, range.EndColumn) (6, 12, 18) "report"
            }

            test "an owning memo" {
                Expect.equal
                    (codes "let m () =\n    createMemoWith (fun _ ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        a.Value)\n")
                    [ "RAN011" ]
                    "createMemoWith"
            }

            test "an effect" {
                Expect.equal
                    (codes
                        "let e () =\n    createEffect (fun () ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        ignore a.Status)\n")
                    [ "RAN011" ]
                    "createEffect"
            }

            test "an error boundary and a boundary" {
                let body =
                    "let b () =\n    createErrorBoundary (fun _ _ -> 0) (fun () ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        a.Value)\n"
                    + "let c () =\n    createBoundary (fun _ -> 0) (fun _ _ -> 0) (fun () ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        a.Value)\n"

                Expect.equal (codes body) [ "RAN011"; "RAN011" ] "both boundaries"
            }

            test "createAsyncWith" {
                Expect.equal
                    (codes
                        "let w () =\n    createAsyncWith (fun _ _ ->\n        let inner = createAsync (fun _ _ -> Task.FromResult 1)\n        Task.FromResult inner.Value)\n")
                    [ "RAN011" ]
                    "owning async"
            }

            test "created outside and read inside" {
                let body =
                    fetch
                    + "let view () =\n    let report = fetch ()\n    createSuspense (fun _ -> 0) (fun () -> report.Value)\n"

                Expect.isEmpty (codes body) "the right shape"
            }

            test "created inside and never read there" {
                let body =
                    "let view () =\n    createSuspense (fun _ -> 0) (fun () ->\n        let report = createAsync (fun _ _ -> Task.FromResult 1)\n        ignore report\n        0)\n"

                Expect.isEmpty (codes body) "not read in the body"
            }

            test "created inside a pure createMemo is RAN010's" {
                Expect.isEmpty
                    (codes "let m () =\n    createMemo (fun _ ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        a.Value)\n")
                    "pure memo"
            }

            test "read inside a nested stored callback" {
                let body =
                    "let view (handlers: ResizeArray<unit -> int>) =\n    createSuspense (fun _ -> 0) (fun () ->\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        handlers.Add (fun () -> a.Value)\n        0)\n"

                Expect.isEmpty (codes body) "the read is not in the body's run"
            }

            test
                "createMemoWithOn, createOwningMemoOnWithComparer, createEffectNode, createAsyncWithOn, createOwningAsyncOnWithPolicy, Signal.bind, Memo.bind" {
                let body =
                    "let s = createSignal 1
let m1 = createMemoWithOn (fun () -> s.Value) (fun _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in a.Value)
let m2 = createOwningMemoOnWithComparer HashIdentity.Structural (fun () -> s.Value) (fun _ -> let a = createStream (fun _ -> Unchecked.defaultof<StreamSource<int>>) in a.Value)
let e1 = createEffectNode (fun () -> let a = createAsync (fun _ _ -> Task.FromResult 1) in ignore a.Status)
let a1 = createAsyncWithOn (fun () -> s.Value) (fun _ _ _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in Task.FromResult a.Value)
let a2 = createOwningAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> s.Value) (fun _ _ _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in Task.FromResult a.Value)
let b1 = s |> Signal.bind (fun _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in let v = a.Value in createMemo (fun _ -> v))
let b2 = (createMemo (fun _ -> 1)) |> Memo.bind (fun _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in let v = a.Value in createMemo (fun _ -> v))
"

                Expect.equal (codes body) [ "RAN011"; "RAN011"; "RAN011"; "RAN011"; "RAN011"; "RAN011"; "RAN011" ] "all PR #389 owner bodies"
            }

            test "Signal.bind returning a memo that reads the async value inside the child memo is clean" {
                let body =
                    "let s = createSignal 1\nlet b = s |> Signal.bind (fun _ -> let a = createAsync (fun _ _ -> Task.FromResult 1) in createMemo (fun _ -> a.Value))\n"

                Expect.isEmpty (codes body) "read inside child memo, not binder"
            }

            test "CE owners (memo owning, MemoTail, effect, asyncMemo owning pre-await, boundary) with .Value and let! reads" {
                let runCe (extra: string) =
                    let src =
                        "module Fixture\nopen Ranvier\nopen Ranvier.CE\nopen System.Threading.Tasks\nlet s = createSignal 1\n"
                        + extra

                    run Analyzers.ran011Cli (context (ranvier.Force ()) src)
                    |> List.map _.Code

                Expect.equal
                    (runCe
                        "let mLet () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! v = a\n        return v\n    }\n")
                    [ "RAN011" ]
                    "memo owning + let!"

                Expect.equal
                    (runCe
                        "let mAnd () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! x = s\n        and! v = a\n        return x + v\n    }\n")
                    [ "RAN011" ]
                    "memo owning + and!"

                Expect.equal
                    (runCe
                        "let mCmp () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! v = a |> withComparer HashIdentity.Structural\n        return v\n    }\n")
                    [ "RAN011" ]
                    "memo owning + withComparer let!"

                Expect.equal
                    (runCe
                        "let mLater () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! _ = s\n        return a.Value\n    }\n")
                    [ "RAN011" ]
                    "memo owning + bind before later read"

                Expect.equal
                    (runCe
                        "let mVal () =\n    memo {\n        owning\n        let! _ = s\n        let a = stream { return Unchecked.defaultof<StreamSource<int>> }\n        return a.Value\n    }\n")
                    [ "RAN011" ]
                    "memo owning + stream.Value"

                Expect.equal
                    (runCe
                        "let mTail () =\n    memo {\n        let! k = s\n        let a = AsyncMemo.bind (fun n _ -> Task.FromResult n) (asyncMemo { return! Task.FromResult k })\n        let! v = a\n        return! memo { return v }\n    }\n")
                    [ "RAN011" ]
                    "MemoTail owner"

                Expect.equal
                    (runCe
                        "let eLet () =\n    effect {\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        let! v = a\n        ignore v\n    }\n")
                    [ "RAN011" ]
                    "effect + let!"

                Expect.equal
                    (runCe
                        "let aPre () =\n    asyncMemo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! v = a\n        do! Task.CompletedTask\n        return v\n    }\n")
                    [ "RAN011" ]
                    "asyncMemo owning pre-await + let!"

                Expect.equal
                    (runCe
                        "let bn () =\n    boundary {\n        pending (fun _ -> 0)\n        let a = createAsync (fun _ _ -> Task.FromResult 1)\n        let! v = a\n        return v\n    }\n")
                    [ "RAN011" ]
                    "boundary + let!"

                Expect.equal
                    (runCe
                        "let mTryRead () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! r = tryRead a\n        return r\n    }\n")
                    [ "RAN011" ]
                    "memo owning + tryRead let!"

                Expect.equal
                    (runCe
                        "let mDebounced () =\n    memo {\n        owning\n        let a = asyncMemo { return! Task.FromResult 1 }\n        let! v = a |> debounced System.TimeSpan.Zero\n        return v\n    }\n")
                    [ "RAN011" ]
                    "memo owning + debounced pipe let!"
            }

            test "post-await creation in owning asyncMemo and child-memo reads are clean in RAN011" {
                let ceBody =
                    "module Fixture
open Ranvier
open Ranvier.CE
open System.Threading.Tasks

let aPostAwait () =
    asyncMemo {
        owning
        do! Task.CompletedTask
        let a = asyncMemo { return! Task.FromResult 1 }
        return a.Value
    }
let mChild () =
    memo {
        owning
        let a = asyncMemo { return! Task.FromResult 1 }
        return memo {
            let! v = a
            return v
        }
    }
"

                Expect.isEmpty (run Analyzers.ran011Cli (context (ranvier.Force ()) ceBody)) "post-await and nested memo are not RAN011"
            }
        ]
