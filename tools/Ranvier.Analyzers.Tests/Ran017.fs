module Ranvier.Analyzers.Tests.Ran017

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
    run Analyzers.ran017Cli (context (ranvier.Force ()) (preamble + body))

let private codes (body: string) =
    findings body |> List.map _.Code

let private applyAllSequential (source: string) =
    let rec step (current: string) =
        match run Analyzers.ran017Cli (context (ranvier.Force ()) current) with
        | [] -> current
        | msg :: _ ->
            let fix = msg.Fixes |> List.exactlyOne
            step (applyFix current fix)

    step source

[<Tests>]
let tests =
    testList
        "RAN017"
        [
            test "independent sequential let! reports Hint, exact message, range on second let!, and applying fix leaves zero findings" {
                let body =
                    "let price = createSignal 10
let quantity = createSignal 3
let total () =
    memo {
        let! p = price
        let! q = quantity
        return p * q
    }
"

                let full = preamble + body
                let msgs = findings body

                Expect.equal
                    (msgs |> List.map (fun m -> m.Severity, m.Message))
                    [
                        Severity.Hint,
                        "`quantity` does not depend on `p`; sequential `let!` forces dynamic tracking and skips static `map2`/`*On` key cutoff. Write `and!` to bind both sources statically."
                    ]
                    "exact hint message"

                Expect.equal (textAt full msgs.Head.Range) "let!" "range is second let!"

                let fix = msgs.Head.Fixes |> List.exactlyOne
                let repaired = applyFix full fix
                Expect.stringContains repaired "and! q = quantity" "replaces let! with and!"
                Expect.isEmpty (run Analyzers.ran017Cli (context (ranvier.Force ()) repaired)) "clean after fix"
            }

            test "all 7 supported builders trigger and applyFix compiles cleanly with zero findings" {
                let samples =
                    [
                        "memo",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    memo {
        let! a = s1
        let! b = s2
        return a + b
    }
"
                        "effect",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    effect {
        let! a = s1
        let! b = s2
        ignore (a + b)
    }
"
                        "asyncMemo",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    asyncMemo {
        let! a = s1
        let! b = s2
        do! Task.CompletedTask
        return a + b
    }
"
                        "stream",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    stream {
        let! a = s1
        let! b = s2
        ignore (a + b)
        return Unchecked.defaultof<StreamSource<int>>
    }
"
                        "editable",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    editable {
        let! a = s1
        let! b = s2
        return a + b
    }
"
                        "draft",
                        "let s1 = createSignal 1
let s2 = createSignal 2
let r () =
    draft {
        let! a = s1
        let! b = s2
        return a + b
    }
"
                        "row with item",
                        "let s2 = createSignal 2
let r () =
    createProjectionWith fst (fun item ->
        row {
            let! a = item
            let! b = s2
            return snd a + b
        }) (fun () -> [ (1, 10) ])
"
                    ]

                for label, body in samples do
                    let full = preamble + body
                    let msgs = findings body
                    Expect.equal (msgs |> List.map _.Code) [ "RAN017" ] $"{label} triggers"
                    let repaired = applyFix full (msgs.Head.Fixes |> List.exactlyOne)
                    Expect.isEmpty (run Analyzers.ran017Cli (context (ranvier.Force ()) repaired)) $"{label} clean after fix"
            }

            test "Review Focus #4: tuple, nested, struct, record, and DU destructuring preserve dependencies while independent binds trigger" {
                let depBody =
                    "type PairRec = { Left: int; Right: int }
type BoxId = BoxId of int
let pairSig = createSignal (1, 2)
let nestedSig = createSignal ((1, 2), 3)
let structSig = createSignal (struct (1, 2))
let recSig = createSignal { Left = 1; Right = 2 }
let boxSig = createSignal (BoxId 5)
let pick (_: int) = createSignal 10

let dTupleLeft () =
    memo {
        let! (x, y) = pairSig
        let! z = pick x
        return z + y
    }
let dTupleRight () =
    memo {
        let! (x, y) = pairSig
        let! z = pick y
        return z + x
    }
let dTupleAs () =
    memo {
        let! (x, _ as whole) = pairSig
        let! z = pick (snd whole)
        return x + z
    }
let dNested () =
    memo {
        let! ((a, b), c) = nestedSig
        let! z = pick a
        return b + c + z
    }
let dStruct () =
    memo {
        let! struct (x, y) = structSig
        let! z = pick y
        return x + z
    }
let dRec () =
    memo {
        let! { Left = l; Right = r } = recSig
        let! z = pick l
        return r + z
    }
let dBox () =
    memo {
        let! (BoxId v) = boxSig
        let! z = pick v
        return z
    }
"

                Expect.isEmpty (codes depBody) "every destructured dependency must stay clean"

                let indepTuple =
                    "let pairSig = createSignal (1, 2)
let otherSig = createSignal 3
let indep () =
    memo {
        let! (x, y) = pairSig
        let! z = otherSig
        return x + y + z
    }
"

                let msgs = findings indepTuple
                Expect.equal (msgs |> List.map _.Code) [ "RAN017" ] "independent bind after tuple destructuring triggers"
                Expect.stringContains msgs.Head.Message "does not depend on `x, y`" "names destructured locals"
                let full = preamble + indepTuple
                let repaired = applyFix full (msgs.Head.Fixes |> List.exactlyOne)
                Expect.stringContains repaired "and! z = otherSig" "replaces let! with and!"
                Expect.isEmpty (run Analyzers.ran017Cli (context (ranvier.Force ()) repaired)) "tuple fix compiles and leaves 0 findings"
            }

            test "chains, existing and!, wildcards, comments, and multi-line patterns" {
                let chain3 =
                    "let s1 = createSignal 1
let s2 = createSignal 2
let s3 = createSignal 3
let c3 () =
    memo {
        let! a = s1
        let! b = s2
        let! c = s3
        return a + b + c
    }
"

                Expect.equal (codes chain3) [ "RAN017"; "RAN017" ] "two pairs in a 3-bind chain"
                let repaired3 = applyAllSequential (preamble + chain3)
                Expect.stringContains repaired3 "and! b = s2" "second becomes and!"
                Expect.stringContains repaired3 "and! c = s3" "third becomes and!"

                let existingAndIndep =
                    "let s1 = createSignal 1
let s2 = createSignal 2
let s3 = createSignal 3
let pick (_: int) = s3
let okWhenTrailingAndDepends () =
    memo {
        let! a = s1
        let! b = s2
        and! c = pick a
        return a + b + c
    }
let badWhenTrailingAndIndep () =
    memo {
        let! a = s1
        let! b = s2
        and! c = s3
        return a + b + c
    }
let badAfterAnd () =
    memo {
        let! a = s1
        and! b = s2
        let! c = s3
        return a + b + c
    }
let badWildcardAndComment () =
    memo {
        let! _ = s1 // fake = let!
        let! (* inline *) b = s2
        return b
    }
"

                let msgs = findings existingAndIndep

                Expect.equal
                    (msgs |> List.map _.Code)
                    [ "RAN017"; "RAN017"; "RAN017" ]
                    "trailing dependent and! stays clean while independent cases trigger"

                Expect.stringContains msgs[1].Message "does not depend on `a, b`" "merged first bind lists both locals"
                Expect.stringContains msgs[2].Message "does not depend on `_`" "wildcard bind uses _"
                let repairedAll = applyAllSequential (preamble + existingAndIndep)
                Expect.isEmpty (run Analyzers.ran017Cli (context (ranvier.Force ()) repairedAll)) "all fixes compile cleanly"
            }

            test "exclusions: boundary, intervening user let/statements/control-flow, context tokens, and Task/ValueTask" {
                let clean =
                    "let s1 = createSignal 1
let s2 = createSignal 2
let opt = createSignal (Some 1)
let pick (_: int) = s2

let boundaryExcluded () =
    boundary {
        pending (fun _ -> 0)
        let! a = s1
        let! b = s2
        return a + b
    }
let userLetBetween () =
    memo {
        let! a = s1
        let copy = a
        let! b = s2
        return copy + b
    }
let statementBetween (log: ResizeArray<int>) =
    memo {
        let! a = s1
        log.Add a
        let! b = s2
        return a + b
    }
let ifBetween (flag: bool) =
    memo {
        let! a = s1
        if flag then
            let! b = s2
            return a + b
        else
            return a
    }
let matchBangAfter () =
    memo {
        let! a = s1
        match! opt with
        | Some b -> return a + b
        | None -> return a
    }
let dependentAndThenIndep () =
    memo {
        let! a = s1
        let! b = pick a
        return a + b
    }
let memoPrevTokens () =
    let m1 =
        memo {
            let! p = memo.previous
            let! a = s1
            return a + ValueOption.defaultValue 0 p
        }
    let m2 =
        memo {
            let! a = s1
            let! p = memo.previousOr 0
            return a + p
        }
    m1, m2
let editDraftPrevTokens () =
    let e1 =
        editable {
            let! p = editable.previous
            let! a = s1
            return a + ValueOption.defaultValue 0 p
        }
    let e2 =
        editable {
            let! a = s1
            let! p = editable.previousOr 0
            return a + p
        }
    let d1 =
        draft {
            let! p = draft.previous
            let! a = s1
            return a + ValueOption.defaultValue 0 p
        }
    let d2 =
        draft {
            let! a = s1
            let! p = draft.previousOr 0
            return a + p
        }
    e1, e2, d1, d2
let asyncTokensAndTasks () =
    let a1 =
        asyncMemo {
            let! _p = asyncMemo.previous
            let! a = s1
            do! Task.CompletedTask
            return a
        }
    let a2 =
        asyncMemo {
            let! _ct = asyncMemo.cancellationToken
            let! a = s1
            do! Task.CompletedTask
            return a
        }
    let a3 =
        asyncMemo {
            let! a = s1
            let! t = Task.FromResult 1
            return a + t
        }
    let a4 =
        asyncMemo {
            let! a = s1
            let! t = ValueTask.FromResult 1
            return a + t
        }
    a1, a2, a3, a4
let streamToken () =
    stream {
        let! _ct = stream.cancellationToken
        let! a = s1
        ignore a
        return Unchecked.defaultof<StreamSource<int>>
    }
"

                Expect.isEmpty
                    (codes clean)
                    "boundary, user let, Combine, IfThenElse, match!, dependent bind, context tokens, and Task/ValueTask stay clean"
            }
        ]
