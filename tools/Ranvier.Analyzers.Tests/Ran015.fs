module Ranvier.Analyzers.Tests.Ran015

open Expecto
open FSharp.Analyzers.SDK
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen System.Threading.Tasks\nopen Ranvier\n"
        + body

    run Analyzers.ran015Cli (context (ranvier.Force ()) source)

[<Tests>]
let tests =
    testList
        "RAN015"
        [
            test "a wildcard token, with its exact message and Hint severity" {
                let messages =
                    findings "let a () = createAsyncWithPolicy FlightPolicy.CancelPrevious (fun _ _ -> Task.FromResult 1)\n"

                Expect.equal
                    (messages
                     |> List.map (fun m -> m.Severity, m.Message))
                    [
                        Severity.Hint,
                        "This async memo's body ignores its CancellationToken, so `CancelPrevious` cancels nothing and acts as `KeepLatest`. Pass the token to the awaited work, or use `KeepLatest`."
                    ]
                    "one hint"
            }

            test "a named token that is never used" {
                Expect.equal
                    (findings "let a () = createAsyncWithPolicy FlightPolicy.CancelPrevious (fun _ token -> Task.FromResult 1)\n"
                     |> List.map _.Code)
                    [ "RAN015" ]
                    "unused"
            }

            test "the finding's range is the token parameter" {
                let source =
                    "let a () = createAsyncWithPolicy FlightPolicy.CancelPrevious (fun _ token -> Task.FromResult 1)\n"

                let range = (findings source).Head.Range

                Expect.equal
                    (textAt
                        ("module Fixture\nopen System.Threading.Tasks\nopen Ranvier\n"
                         + source)
                        range)
                    "token"
                    "the token"
            }

            test "the owning form" {
                Expect.equal
                    (findings "let a () = createOwningAsyncWithPolicy FlightPolicy.CancelPrevious (fun _ _ -> Task.FromResult 1)\n"
                     |> List.map _.Code)
                    [ "RAN015" ]
                    "owning"
            }

            test "a token passed to the work" {
                Expect.isEmpty
                    (findings
                        "let a () = createAsyncWithPolicy FlightPolicy.CancelPrevious (fun _ token -> (Task.Delay (10, token)).ContinueWith (fun (_: Task) -> 1))\n")
                    "used"
            }

            test "KeepLatest" {
                Expect.isEmpty
                    (findings "let a () = createAsyncWithPolicy FlightPolicy.KeepLatest (fun _ _ -> Task.FromResult 1)\n")
                    "no cancellation promised"
            }

            test "a policy parameter" {
                Expect.isEmpty
                    (findings "let a (policy: FlightPolicy) = createAsyncWithPolicy policy (fun _ _ -> Task.FromResult 1)\n")
                    "unknown policy"
            }

            test "createAsyncOnWithPolicy and createOwningAsyncOnWithPolicy" {
                let msgs =
                    findings
                        "let a1 () = createAsyncOnWithPolicy FlightPolicy.CancelPrevious (fun () -> 1) (fun _ _ _ -> Task.FromResult 1)
let a2 () = createAsyncOnWithPolicy FlightPolicy.CancelPrevious (fun () -> 1) (fun _ _ tok -> Task.FromResult 1)
let a3 () = createOwningAsyncOnWithPolicy FlightPolicy.CancelPrevious (fun () -> 1) (fun _ _ _ -> Task.FromResult 1)
let a4 () = createAsyncOnWithPolicy FlightPolicy.CancelPrevious (fun () -> 1) (fun _ _ token -> (Task.Delay (10, token)).ContinueWith (fun (_: Task) -> 1))
let a5 () = createAsyncOnWithPolicy FlightPolicy.KeepLatest (fun () -> 1) (fun _ _ _ -> Task.FromResult 1)
"

                Expect.equal (msgs |> List.map _.Code) [ "RAN015"; "RAN015"; "RAN015" ] "a1, a2, a3 only"

                let src =
                    "module Fixture\nopen System.Threading.Tasks\nopen Ranvier\nlet a () = createAsyncOnWithPolicy FlightPolicy.CancelPrevious (fun () -> 1) (fun _ _ tok -> Task.FromResult 1)\n"

                Expect.equal (textAt src (run Analyzers.ran015Cli (context (ranvier.Force ()) src)).Head.Range) "tok" "exact range"
            }

            test "asyncMemo CE with CancelPrevious and missing, wildcard, or unused cancellationToken" {
                let ce (extra: string) =
                    "module Fixture
open System.Threading.Tasks
open Ranvier
open Ranvier.CE
"
                    + extra

                let missingSrc =
                    ce
                        "let s = createSignal 1
let a () =
    asyncMemo {
        policy FlightPolicy.CancelPrevious
        let! x = s
        return! Task.FromResult x
    }
"

                let missingMsgs = run Analyzers.ran015Cli (context (ranvier.Force ()) missingSrc)
                Expect.equal (missingMsgs |> List.map _.Code) [ "RAN015" ] "never bound"
                Expect.equal (textAt missingSrc missingMsgs.Head.Range) "policy FlightPolicy.CancelPrevious" "range is policy clause"

                let wildcardSrc =
                    ce
                        "let s = createSignal 1
let a () =
    asyncMemo {
        policy FlightPolicy.CancelPrevious
        let! _ = asyncMemo.cancellationToken
        let local = 42
        return local + s.Value
    }
"

                let wildMsgs = run Analyzers.ran015Cli (context (ranvier.Force ()) wildcardSrc)
                Expect.equal (wildMsgs |> List.map _.Code) [ "RAN015" ] "wildcard bind"
                Expect.equal (textAt wildcardSrc wildMsgs.Head.Range) "_" "wildcard range"

                let unusedSrc =
                    ce
                        "let s = createSignal 1
let a () =
    asyncMemo {
        owning
        policy FlightPolicy.CancelPrevious
        let! ct = asyncMemo.cancellationToken
        let! x = s
        do! Task.CompletedTask
        return x
    }
"

                let unusedMsgs = run Analyzers.ran015Cli (context (ranvier.Force ()) unusedSrc)
                Expect.equal (unusedMsgs |> List.map _.Code) [ "RAN015" ] "unused ct bind"
                Expect.equal (textAt unusedSrc unusedMsgs.Head.Range) "ct" "ct range"

                let nestedOuterMissing =
                    ce
                        "let a () =
    asyncMemo {
        owning
        policy FlightPolicy.CancelPrevious
        let _inner =
            asyncMemo {
                policy FlightPolicy.CancelPrevious
                let! ct = asyncMemo.cancellationToken
                do! Task.Delay (10, ct)
                return 1
            }
        return! Task.FromResult 1
    }
"

                let nestedMsgs =
                    run Analyzers.ran015Cli (context (ranvier.Force ()) nestedOuterMissing)

                Expect.equal (nestedMsgs |> List.map _.Code) [ "RAN015" ] "inner token bind does not satisfy outer asyncMemo"

                let clean =
                    ce
                        "let s = createSignal 1
let used () =
    asyncMemo {
        policy FlightPolicy.CancelPrevious
        let! ct = asyncMemo.cancellationToken
        let! x = s
        do! Task.Delay (10, ct)
        return x
    }
let keepLatest () =
    asyncMemo {
        policy FlightPolicy.KeepLatest
        let! x = s
        return! Task.FromResult x
    }
let paramPolicy (p: FlightPolicy) =
    asyncMemo {
        policy p
        let! x = s
        return! Task.FromResult x
    }
"

                Expect.isEmpty
                    (run Analyzers.ran015Cli (context (ranvier.Force ()) clean))
                    "used token, KeepLatest, and parameterized policy are clean"
            }
        ]
