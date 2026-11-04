module Ranvier.View.WebCore.Tests.ExitLifetimeTests

open System
open System.Runtime.CompilerServices
open Expecto
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Sugar
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

type Rows = { Items: Signal<int array> }

let private finishes = ResizeArray<unit -> unit>()

/// <summary>A list whose row roots have exits that end when the test calls their <c>finish</c>.</summary>
let private exiting =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () {
            forEach
                id
                (fun (s: Rows) -> s.Items.Value)
                (li (
                    Hooks.exit (
                        System.Func<_, _, _, _>(fun (_: unit -> int) (_: HTMLLIElement) finish ->
                            finishes.Add finish
                            ignore)
                    )
                ) {
                    fun (row: unit -> int) -> string (row ())
                })
        })

let private collect (host: WebCoreHost) =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()
    host.Cycle ()

// The text node of each row, read in a frame that ends before the caller's collection.
[<MethodImpl(MethodImplOptions.NoInlining)>]
let private texts (mount: Mount) : WeakReference array =
    let list = mount.Container.querySelectorAll ("li": string)
    [| for i in 0 .. int list.length - 1 -> WeakReference ((list.item (uint32 i)).firstChild) |]

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private sameObjects (mount: Mount) (before: WeakReference array) =
    let list = mount.Container.querySelectorAll ("li": string)
    Expect.equal (int list.length) before.Length "every row is still in place"

    for i in 0 .. before.Length - 1 do
        Expect.isTrue (obj.ReferenceEquals ((list.item (uint32 i)).firstChild, before[i].Target)) $"row {i}'s text node object"

let private count = 20

[<Tests>]
let tests =
    testList
        "Exit lifetimes"
        [
            hostCase "a leaving row keeps its node objects through a collection, and releases them when its exit ends"
            <| fun host ->
                finishes.Clear ()
                use graph = new Graph ()
                let state = graph.Run (fun () -> { Items = Api.createSignal [| 1..count |] })
                use mounted = mount host graph exiting state
                let before = texts mounted
                mounted.Act (host, fun () -> state.Items.Value <- [||])
                Expect.equal finishes.Count count "every row's exit started"
                collect host
                sameObjects mounted before
                let released = host.Session.Released
                mounted.Act (host, fun () -> for finish in finishes.ToArray () do finish ())
                Expect.equal (int (mounted.Container.querySelectorAll ("li": string)).length) 0 "the exits removed their rows"
                collect host
                Expect.isGreaterThanOrEqual (host.Session.Released - released) count "the rows' text node objects are released"

            hostCase "disposing a mount releases the node objects of rows whose exits never started"
            <| fun host ->
                finishes.Clear ()
                use graph = new Graph ()
                let state = graph.Run (fun () -> { Items = Api.createSignal [| 1..count |] })
                let mounted = mount host graph exiting state
                collect host
                let released = host.Session.Released
                (mounted :> IDisposable).Dispose ()
                Expect.equal finishes.Count 0 "a mount disposal starts no exit"
                collect host
                Expect.isGreaterThanOrEqual (host.Session.Released - released) (2 * count) "the rows' element and text node objects are released"

            hostCase "disposing a mount releases the node objects of rows whose exits are running"
            <| fun host ->
                finishes.Clear ()
                use graph = new Graph ()
                let state = graph.Run (fun () -> { Items = Api.createSignal [| 1..count |] })
                let mounted = mount host graph exiting state
                mounted.Act (host, fun () -> state.Items.Value <- [||])
                collect host
                let released = host.Session.Released
                (mounted :> IDisposable).Dispose ()
                // Each finish captures its row's root.
                finishes.Clear ()
                collect host
                Expect.isGreaterThanOrEqual (host.Session.Released - released) (2 * count) "the rows' element and text node objects are released"
        ]
