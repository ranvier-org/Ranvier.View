module Ranvier.Analyzers.Tests.IndexTests

open Expecto
open FSharp.Compiler.Symbols.FSharpExprPatterns
open Ranvier.Analyzers
open Ranvier.Analyzers.Index
open Ranvier.Analyzers.Tests.Options

let private index (source: string) =
    let context = context (ranvier.Force ()) source
    context.TypedTree.Value |> Index.ofFile

let private callsOf (name: string) (file: FileIndex) =
    file.Calls
    |> Array.filter (fun site -> site.Callee.LogicalName = name)

/// Shapes the rules were not written for; every rule must return without raising.
let unusual =
    """
module Fixture

open System
open Ranvier

[<Measure>]
type ms

type Point = { X: int; Y: int }

type Shape =
    | Circle of float
    | Square of float

type Alias = int list

let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd
let inline twice (x: ^T) = x + x

let disposable () =
    { new IDisposable with
        member _.Dispose() = () }

let quoted = <@ fun (x: int) -> x + 1 @>
let delay = 5<ms>
let numbers = seq { for i in 1..3 -> i * 2 }
let work = async { return 1 }

let running () =
    task {
        let! x = Threading.Tasks.Task.FromResult 1
        return x
    }

let classify n =
    match n with
    | Even -> "even"
    | Odd -> "odd"

let area shape =
    match shape with
    | Circle r -> r * r
    | Square s -> s * s

let memo () = createMemo (fun _ -> twice 2)
"""

[<Tests>]
let tests =
    testList
        "Index"
        [
            test "a curried member call keeps its literal arguments" {
                let file =
                    index
                        """
module Fixture
open Ranvier.View
open Ranvier.View.HtmlTemplate
let view = Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.text "x" |])
"""

                let div = callsOf "div" file |> Array.exactlyOne
                Expect.equal div.Arguments.Length 2 "parts and children"

                match div.Arguments[1] with
                | NewArray (_, [ _ ]) -> ()
                | other -> failtestf "the children were %A" other
            }

            test "a pipe is indexed as the call it applies" {
                let file =
                    index
                        """
module Fixture
open Ranvier
let memo () = (fun _ -> 1) |> createMemo
let back () = createMemo <| fun _ -> 2
"""

                let memos = callsOf "createMemo" file
                Expect.equal memos.Length 2 "both pipes"

                for site in memos do
                    match site.Arguments with
                    | [ Lambda _ ] -> ()
                    | other -> failtestf "the arguments were %A" other
            }

            test "a call's path names each enclosing argument and lambda" {
                let file =
                    index
                        """
module Fixture
open Ranvier
let memo () = createMemo (fun _ -> (untrack (fun () -> createSignal 1)).Value)
"""

                let signal = callsOf "createSignal" file |> Array.exactlyOne

                match signal.Path with
                | [ Step.Lambda (_, false); Step.Argument (untrack, 0, 1); Step.Receiver value; Step.Lambda (_, false); Step.Argument (memo, 0, 1) ] ->
                    Expect.equal untrack.LogicalName "untrack" "untrack's lambda"
                    Expect.equal value.LogicalName "get_Value" "the Value receiver"
                    Expect.equal memo.LogicalName "createMemo" "createMemo's compute"
                | other -> failtestf "the path was %A" other
            }

            test "arguments skips transparent lambdas and counts the others" {
                let file =
                    index
                        """
module Fixture
open Ranvier
let memo () = createMemo (fun _ -> (untrack (fun () -> createSignal 1)).Value)
"""

                let signal = callsOf "createSignal" file |> Array.exactlyOne

                let transparent (m: FSharp.Compiler.Symbols.FSharpMemberOrFunctionOrValue) =
                    m.LogicalName = "untrack"

                let names =
                    Index.arguments transparent signal.Path
                    |> List.map (fun (callee, index, _, lambdas) -> callee.LogicalName, index, lambdas)

                Expect.equal names [ "createMemo", 0, 1 ] "untrack is skipped; a receiver is not an argument"
            }

            test "a let body shares the let's path by reference" {
                let file =
                    index
                        """
module Fixture
open Ranvier
let memo () =
    createMemo (fun _ ->
        let s = createSignal 1
        s.Value)
"""

                let binding =
                    file.Lets
                    |> Array.find (fun site -> site.Value.DisplayName = "s")

                let read = callsOf "get_Value" file |> Array.exactlyOne

                Expect.equal
                    (Index.within binding.Path read.Path
                     |> Option.map List.length)
                    (Some 0)
                    "the body's own path"
            }

            test "a class's let function is indexed" {
                let file =
                    index
                        """
module Fixture
open Ranvier
type Holder() =
    let make () = createSignal 1
    member _.Make() = make ()
"""

                Expect.equal (callsOf "createSignal" file).Length 1 "the let function's body"
            }

            test "a lambda the compiler hoists before a partial application is walked at its argument" {
                let file =
                    index
                        """
module Fixture
open Ranvier
let f (p: Projection<int, int>) = p |> Projection.fold (fun s v -> s + v) 0
"""

                let addition = callsOf "op_Addition" file |> Array.exactlyOne

                Expect.isTrue
                    (addition.Path
                     |> List.exists (fun step ->
                         match step with
                         | Step.Argument (callee, 0, 3) -> callee.LogicalName = "fold"
                         | _ -> false))
                    "the folder is fold's argument 0"
            }

            test "the index is built once per file" {
                let tree = (context (ranvier.Force ()) "module Fixture\nlet x = 1").TypedTree.Value

                Expect.isTrue (obj.ReferenceEquals (Index.ofFile tree, Index.ofFile tree)) "the cached index"
            }

            test "ordinary native integer conversions remain typed calls" {
                let file = index "module Fixture\nlet convert (value: int64) = nativeint value\n"
                let call = file.Calls |> Array.exactlyOne
                Expect.equal call.Callee.CompiledName "ToIntPtr" "the numeric conversion still names its operator"
                match call.Arguments with
                | [ Value argument ] -> Expect.equal argument.DisplayName "value" "the original typed argument"
                | _ -> failtest "the numeric conversion lost its typed argument"
            }

            test "fixed buffers preserve calls and diagnostics across their whole scope" {
                let source =
                    """module Fixture
#nowarn "9"
open Ranvier
open Microsoft.FSharp.NativeInterop
let pin (bytes: byte[]) =
    let before = createSignal 1
    let address =
        use pointer = fixed (let seed = createSignal 2 in bytes)
        let inside = createSignal 3
        let owned = createMemo (fun _ -> createEffect ignore; 1)
        NativePtr.toNativeInt pointer
    let after = createSignal 4
    address
"""

                let context = context (ranvier.Force ()) source
                let file = Index.ofFile context.TypedTree.Value
                let signals = callsOf "createSignal" file

                Expect.sequenceEqual
                    (signals |> Array.map (fun site -> site.Range.StartLine) |> Array.sort)
                    [ 6; 8; 9; 12 ]
                    "calls before, in the initializer, in the fixed body and after it"

                let findings = run Analyzers.ran010Cli context
                Expect.equal (findings |> List.map _.Code) [ "RAN010" ] "the owned node inside the fixed body"
                Expect.equal findings.Head.Range.StartLine 10 "the diagnostic stays at the inner creator"
            }

            test "the index and every rule survive unusual shapes" {
                let context = context (ranvier.Force ()) unusual
                Index.ofFile context.TypedTree.Value |> ignore

                for rule in Analyzers.rules do
                    Analyzers.runCli rule context |> ignore
            }
        ]
