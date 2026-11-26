module Ranvier.Analyzers.Tests.Ran018

open Expecto
open FSharp.Analyzers.SDK
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private preamble =
    "module Fixture
open System.Threading.Tasks
open Ranvier
open Ranvier.CE
"

let private findings (body: string) =
    run Analyzers.ran018Cli (context (ranvier.Force ()) (preamble + body))

let private codes (body: string) =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN018"
        [
            test "direct reactive read in 0-bind memo reports Hint, exact message, and exact range" {
                let body =
                    "let count = createSignal 1
let m () =
    memo {
        return count.Value + 1
    }
"

                let msgs = findings body

                Expect.equal
                    (msgs |> List.map (fun m -> m.Severity, m.Message))
                    [
                        Severity.Hint,
                        "`count.Value` is read directly inside `memo { ... }` with no `let!` bindings, which skips static source fusion, `.Graph` resolution, and closed-phase tracking. Bind `count` with `let!`."
                    ]
                    "exact hint message"

                Expect.equal (textAt (preamble + body) msgs.Head.Range) "count.Value" "range is the read"
            }

            test "all 6 included builders with 0 reactive let! bindings report RAN018" {
                let body =
                    "let s = createSignal 1
let p = createProjection fst id (fun () -> [ (1, 2) ])
let all () =
    let m = memo { return s.Value + 1 }
    let mo =
        memo {
            owning
            return fst (p.Get 1)
        }
    let ed = editable { return s.Value }
    let dr = draft { return s.Value }
    let am = asyncMemo { return s.Value }
    let amPre =
        asyncMemo {
            let x = s.Value
            do! Task.CompletedTask
            return x
        }
    let st =
        stream {
            let x = s.Value
            ignore x
            return Unchecked.defaultof<StreamSource<int>>
        }
    let mCtx =
        memo {
            let! prev = memo.previousOr 0
            return prev + s.Value
        }
    let stCtx =
        stream {
            let! _ct = stream.cancellationToken
            ignore s.Value
            return Unchecked.defaultof<StreamSource<int>>
        }
    m, mo, ed, dr, am, amPre, st, mCtx, stCtx
"

                Expect.equal (codes body |> List.length) 9 "all 0-reactive-bind CE variants trigger"
            }

            test "row CE item () and external reactive reads across projection factories" {
                let body =
                    "type ItemRow = { Key: int; Title: string }
let s = createSignal 1
let items = createSignal [ { Key = 1; Title = \"a\" } ]
let p1 = createProjectionWith (fun x -> x.Key) (fun item -> row { return (item ()).Key + 1 }) (fun () -> items.Value)
let p2 = createProjectionWithOptions ProjectionOptions.CreateFirst (fun x -> x.Key) (fun item -> row { let v = item () in return v.Key }) (fun () -> items.Value)
let p3 = createIndexProjectionWith (fun item -> row { return (item ()).Key }) (fun () -> items.Value)
let p4 = p1 |> Projection.mapWith (fun _ item -> row { return item () + 1 })
let pExt = p1 |> Projection.mapWith (fun _ _item -> row { return s.Value })
"

                let msgs = findings body

                Expect.equal
                    (msgs |> List.map _.Code)
                    [ "RAN018"; "RAN018"; "RAN018"; "RAN018"; "RAN018" ]
                    "all 4 projection item () reads plus external s.Value in row"

                Expect.stringContains msgs.Head.Message "inside `row { ... }`" "names row builder"
                Expect.stringContains msgs.Head.Message "Bind `item` with `let!`" "names item parameter"
            }

            test "outer 0-bind memo reading an inner bound memo still triggers on outer read" {
                let body =
                    "let s = createSignal 1
let outer () =
    memo {
        let inner =
            memo {
                let! x = s
                return x + 1
            }
        return inner.Value
    }
"

                let msgs = findings body
                Expect.equal (msgs |> List.map _.Code) [ "RAN018" ] "outer triggers on inner.Value"
                Expect.stringContains msgs.Head.Message "`inner.Value`" "flags inner.Value, not inner's bound s"
            }

            test "exclusions: effect, boundary, pure return, Peek, untrack, nested tracked, nested closures, and bound CEs" {
                let clean =
                    "let s = createSignal 1
let cond = createSignal true
let asyncVal = createAsync (fun _ _ -> Task.FromResult 1)
let items = createSignal [ (1, \"a\") ]

let effectZero (act: unit -> unit) =
    effect {
        if cond.Value then
            act ()
    }
let boundaryZero () =
    boundary {
        pending (fun _ -> 0)
        return asyncVal.Value
    }
let pureReturn () = memo { return 42 }
let peekRead () = memo { return s.Peek }
let untrackRead () = memo { return untrack (fun () -> s.Value) }
let nestedEffect () =
    memo {
        owning
        createEffect (fun () -> ignore s.Value)
        return 42
    }
let nestedMemo () =
    memo {
        owning
        let _child = createMemo (fun _ -> s.Value)
        return 42
    }
let boundMemo () =
    memo {
        let! x = s
        return x + 1
    }
let boundRow =
    createProjectionWith fst (fun item ->
        row {
            let! x = item
            return snd x
        }) (fun () -> items.Value)
let untrackRow =
    createProjectionWith fst (fun item ->
        row {
            return snd (untrack (fun () -> item ()))
        }) (fun () -> items.Value)
let closureInRow =
    createProjectionWith fst (fun item ->
        row {
            let getter () = snd (item ())
            ignore getter
            return 1
        }) (fun () -> items.Value)
"

                Expect.isEmpty
                    (codes clean)
                    "effect, boundary, constant, Peek, untrack, nested tracked computations, closure in row, and bound CEs stay clean"
            }
        ]
