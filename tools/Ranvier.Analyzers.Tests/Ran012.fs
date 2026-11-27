module Ranvier.Analyzers.Tests.Ran012

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\n"
        + body

    run Analyzers.ran012Cli (context (ranvier.Force ()) source)

let private codes body =
    findings body |> List.map _.Code

let private asyncMemo (inner: string) =
    "let load (userId: Signal<int>) (previous: Task<int>) =\n    createAsync (fun _ _ ->\n        task {\n"
    + inner
    + "        })\n"

[<Tests>]
let tests =
    testList
        "RAN012"
        [
            test "a read after do! Task.Yield, with its exact message" {
                Expect.equal
                    (findings (asyncMemo "            do! Task.Yield ()\n            return userId.Value\n")
                     |> List.map _.Message)
                    [
                        "`userId.Value` is read after the first `let!`/`do!` of this async memo; a read after an await that suspends is untracked, and a change to `userId` starts no flight. Read it before the first await and bind it to a local."
                    ]
                    "one message"
            }

            test "a read after let!" {
                Expect.equal (codes (asyncMemo "            let! _ = previous\n            return userId.Value\n")) [ "RAN012" ] "let!"
            }

            test "a read in an and! continuation" {
                Expect.equal
                    (codes (asyncMemo "            let! a = previous\n            and! b = previous\n            return a + b + userId.Value\n"))
                    [ "RAN012" ]
                    "and!"
            }

            test "a read after the await of createAsyncWithPolicy" {
                let body =
                    "let load (userId: Signal<int>) =\n    createAsyncWithPolicy FlightPolicy.KeepLatest (fun _ _ ->\n        task {\n            do! Task.Yield ()\n            return userId.Value\n        })\n"

                Expect.equal (codes body) [ "RAN012" ] "policy form"
            }

            test "a read before the first await" {
                Expect.isEmpty
                    (codes (asyncMemo "            let id = userId.Value\n            do! Task.Yield ()\n            return id\n"))
                    "tracked"
            }

            test "Peek after the await" {
                Expect.isEmpty (codes (asyncMemo "            do! Task.Yield ()\n            return userId.Peek\n")) "untracked by design"
            }

            test "an await outside an async memo" {
                Expect.isEmpty
                    (codes "let t (s: Signal<int>) =\n    task {\n        do! Task.Yield ()\n        return s.Value\n    }\n")
                    "no async memo"
            }

            test "a read in the body of an effect created after the await belongs to the effect" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            createEffect (fun () -> userId.Value |> ignore)
            return 1
"
                    ))
                    "the effect tracks its own reads"
            }

            test "a read under untrack after the await" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            return untrack (fun () -> userId.Value)
"
                    ))
                    "untracked by intent"
            }

            test "a read in createRoot's body after the await" {
                Expect.equal
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            return createRoot (fun _ -> userId.Value)
"
                    ))
                    [ "RAN012" ]
                    "a root tracks nothing"
            }

            test "a read in createEffectOn's act after the await" {
                Expect.equal
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            createEffectOn (fun () -> 1) (fun _ -> userId.Value |> ignore)
            return 1
"
                    ))
                    [ "RAN012" ]
                    "act is untracked"
            }

            test "a read in an onCleanup callback after the await" {
                Expect.equal
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            onCleanup (fun () -> userId.Value |> ignore)
            return 1
"
                    ))
                    [ "RAN012" ]
                    "a cleanup tracks nothing"
            }

            test "a read after an await inside if" {
                let body =
                    "let load (userId: Signal<int>) (flag: bool) =
    createAsync (fun _ _ ->
        task {
            if flag then
                do! Task.Yield ()

            return userId.Value
        })
"

                Expect.equal (codes body) [ "RAN012" ] "the branch awaits"
            }

            test "a read after an await inside for" {
                let body =
                    "let load (userId: Signal<int>) =
    createAsync (fun _ _ ->
        task {
            for _ in 1..2 do
                do! Task.Yield ()

            return userId.Value
        })
"

                Expect.equal (codes body) [ "RAN012" ] "the loop awaits"
            }

            test "a read after a branch without an await" {
                let body =
                    "let load (userId: Signal<int>) (flag: bool) (log: ResizeArray<int>) =
    createAsync (fun _ _ ->
        task {
            if flag then
                log.Add 1

            return userId.Value
        })
"

                Expect.isEmpty (codes body) "no await before the read"
            }

            test "an unawaited inner task in a branch is no await" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            if userId.Peek > 0 then\n                (task { do! Task.Yield () }) |> ignore\n            return userId.Value\n"
                    ))
                    "the inner task is not awaited"
            }

            test "a read before the await in a loop body runs after the previous iteration's await" {
                Expect.equal
                    (codes (
                        asyncMemo
                            "            for _ in 1..3 do\n                let n = userId.Value\n                do! Task.Delay n\n            return 0\n"
                    ))
                    [ "RAN012" ]
                    "loop body"
            }

            test "a read in a loop body without an await" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            let mutable total = 0\n            for _ in 1..3 do\n                total <- total + userId.Value\n            do! Task.Yield ()\n            return total\n"
                    ))
                    "before the first await"
            }

            test "the finding's range is the read" {
                let range =
                    (findings (asyncMemo "            do! Task.Yield ()\n            return userId.Value\n")).Head.Range

                Expect.equal (range.StartLine, range.StartColumn, range.EndColumn) (8, 19, 31) "userId.Value"
            }

            test "a user Value property after an await is not a reactive read" {
                let body =
                    "type Box() =\n    member _.Value = 1\nlet load (box: Box) =\n    createAsync (fun _ _ ->\n        task {\n            do! Task.Yield ()\n            return box.Value\n        })\n"

                Expect.isEmpty (codes body) "user type"
            }

            test "StreamMemo and Timed Value after await are reactive reads" {
                let body =
                    "let load (st: StreamMemo<int>) (tm: Timed<int>) =
    createAsync (fun _ _ ->
        task {
            do! Task.Yield ()
            return st.Value + tm.Value
        })
"

                Expect.equal (codes body) [ "RAN012"; "RAN012" ] "StreamMemo and Timed"
            }

            test "createAsyncOn* and AsyncMemo/AsyncSource.bind reads after await" {
                let body =
                    "let load (k: Signal<int>) (s: Signal<int>) (am: AsyncMemo<int>) (src: AsyncSource<int>) =
    let a1 = createAsyncOn (fun () -> k.Value) (fun _ _ _ -> task {
        do! Task.Yield ()
        return s.Value
    })
    let a2 = createAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> k.Value) (fun _ _ _ -> task {
        do! Task.Yield ()
        return s.Value
    })
    let a3 = createAsyncWithOn (fun () -> k.Value) (fun _ _ _ -> task {
        do! Task.Yield ()
        return s.Value
    })
    let a4 = am |> AsyncMemo.bind (fun _ _ -> task {
        do! Task.Yield ()
        return s.Value
    })
    let a5 = src |> AsyncSource.bind (fun _ _ -> task {
        do! Task.Yield ()
        return s.Value
    })
    a1, a2, a3, a4, a5
"

                Expect.equal (codes body) [ "RAN012"; "RAN012"; "RAN012"; "RAN012"; "RAN012" ] "all PR #389 async compute forms"
            }

            test "asyncMemo CE reads after Task or ValueTask await report RAN012, while pre-await or no-Task binds do not" {
                let ce (extra: string) =
                    "module Fixture
open Ranvier
open Ranvier.CE
open System.Threading.Tasks
"
                    + extra

                let triggers =
                    ce
                        "let load (userId: Signal<int>) (flag: bool) =
    let a1 =
        asyncMemo {
            do! Task.CompletedTask
            return userId.Value
        }
    let a2 =
        asyncMemo {
            let! _ = Task.FromResult 1
            return userId.Value
        }
    let a3 =
        asyncMemo {
            do! ValueTask.CompletedTask
            return userId.Value
        }
    let a4 =
        asyncMemo {
            let! _ = ValueTask.FromResult 1
            return userId.Value
        }
    let a5 =
        asyncMemo {
            if flag then
                do! Task.CompletedTask
            return userId.Value
        }
    let a6 =
        asyncMemo {
            for _ in 1..2 do
                let n = userId.Value
                do! Task.Delay n
            return 0
        }
    a1, a2, a3, a4, a5, a6
"

                Expect.equal
                    (run Analyzers.ran012Cli (context (ranvier.Force ()) triggers)
                     |> List.map _.Code)
                    [ "RAN012"; "RAN012"; "RAN012"; "RAN012"; "RAN012"; "RAN012" ]
                    "asyncMemo CE post-await reads"

                let clean =
                    ce
                        "let load (s: Signal<int>) (other: Signal<int>) =
    let preAwait =
        asyncMemo {
            let x = other.Value
            do! Task.CompletedTask
            return x
        }
    let phase2NoAwait =
        asyncMemo {
            let! x = s
            return x + other.Value
        }
    let withHeader =
        asyncMemo {
            policy FlightPolicy.CancelPrevious
            let x = other.Value
            do! Task.CompletedTask
            return x
        }
    let nestedEffectAfterAwait =
        asyncMemo {
            do! Task.CompletedTask
            effect { ignore other.Value }
            return 1
        }
    let peekAfterAwait =
        asyncMemo {
            do! Task.CompletedTask
            return other.Peek
        }
    preAwait, phase2NoAwait, withHeader, nestedEffectAfterAwait, peekAfterAwait
"

                Expect.isEmpty
                    (run Analyzers.ran012Cli (context (ranvier.Force ()) clean))
                    "pre-await, no-await Phase2 (RAN016's), headers, nested effects, and Peek stay clean in RAN012"

                let v024Triggers =
                    ce
                        "let load (userId: Signal<int>) (ed: Editable<int>) (o: Owner) =
    let aCleanup =
        asyncMemo {
            owning
            do! Task.CompletedTask
            cleanup { ignore userId.Value }
            return 0
        }
    let aRoot =
        asyncMemo {
            do! Task.CompletedTask
            let r = root { let! _own = root.owner in return userId.Value }
            return r
        }
    let aOwner =
        asyncMemo {
            do! Task.CompletedTask
            o { ignore userId.Value }
            return 0
        }
    let aTimed =
        asyncMemo {
            debounce System.TimeSpan.Zero
            let! k = userId
            do! Task.CompletedTask
            return if ed.IsEdited then ed.Upstream + k else k
        }
    aCleanup, aRoot, aOwner, aTimed
"

                Expect.equal
                    (run Analyzers.ran012Cli (context (ranvier.Force ()) v024Triggers)
                     |> List.map _.Code)
                    [ "RAN012"; "RAN012"; "RAN012"; "RAN012"; "RAN012" ]
                    "0.2.4 post-await cleanup, root (with root.owner), owner CE, and timed asyncMemo Editable reads"

                let v024Clean =
                    ce
                        "let load (s: Signal<int>) (other: Signal<int>) =
    let aUntracked =
        asyncMemo {
            do! Task.CompletedTask
            return untracked { return other.Value }
        }
    let aTimedPre =
        asyncMemo {
            debounce System.TimeSpan.Zero
            let! x = s
            do! Task.CompletedTask
            return x
        }
    aUntracked, aTimedPre
"

                Expect.isEmpty
                    (run Analyzers.ran012Cli (context (ranvier.Force ()) v024Clean))
                    "0.2.4 untracked CE after await and timed asyncMemo pre-await bind stay clean"
            }
        ]
