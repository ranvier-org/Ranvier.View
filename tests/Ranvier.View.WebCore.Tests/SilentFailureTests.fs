module Ranvier.View.WebCore.Tests.SilentFailureTests

open System
open System.IO
open Expecto
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

let private label =
    Component.define (fun _ (count: Signal<int>) -> count) (fun t -> t.span [||] [| t.text (fun (count: Signal<int>) -> string count.Value) |])

// A stand-in for a chart scene: Ranvier.D5's `Scene.create` rejects duplicate mark keys with an ArgumentException.
let private scene (keys: string list) : string =
    match
        keys
        |> List.countBy id
        |> List.tryFind (fun (_, count) -> count > 1)
    with
    | Some (key, _) -> raise (ArgumentException ("marks: duplicate key '" + key + "'"))
    | None -> String.concat "," keys

let private chart =
    Component.define (fun _ (keys: Signal<string list>) -> keys) (fun t ->
        t.span [||] [| t.text (fun (keys: Signal<string list>) -> scene keys.Value) |])

// The dev host's mount: binding failures go to `host.Report`. An exception raised inside a reporter is held by the
// failed binding's effect.
let private mountReporting (host: WebCoreHost) (graph: Graph) (definition: Component<'props>) (props: 'props) : Mount =
    let container = host.Document.createElement "div" :?> Ranvier.Dom.HTMLElement
    host.Document.body.appendChild container |> ignore

    let mounted =
        Mounting.mount graph None (List.iter host.Report) container None (fun context ->
            Program.run (Component.render definition props) context.Backend)

    {
        Graph = graph
        Mounted = mounted
        Container = container
    }

let private hiddenWindow (body: Window -> unit) =
    onHostThread (fun () ->
        use window =
            Window.Create ("Ranvier test", 200, 150, hidden = true, renderer = "software")

        body window)

// The text written to the process's standard error while `body` runs.
let private standardError (body: unit -> unit) : string =
    let previous = Console.Error
    use captured = new StringWriter ()
    Console.SetError captured

    try
        body ()
    finally
        Console.SetError previous

    captured.ToString ()

let private occurrences (needle: string) (text: string) =
    (text.Length - text.Replace(needle, "").Length)
    / needle.Length

[<Tests>]
let tests =
    testList
        "Silent failures"
        [
            testCase "a frame callback's signal write updates a view on the window's graph"
            <| fun () ->
                hiddenWindow (fun window ->
                    let graph = window.Graph
                    let count = graph.Run (fun () -> Api.createSignal 0)
                    use mounted = mount window.Host graph label count

                    window.Host.RequestAnimationFrame (fun _ -> count.Value <- 5)
                    |> ignore

                    window.Present ()
                    Expect.equal mounted.Text "5" "after the frame"
                    Expect.isEmpty (List.ofSeq window.Host.Errors) "host errors")

            // The dev host's arrangement: the view mounts on a graph of its own, beside the window's.
            testCase "a frame callback's signal write updates a view on a graph beside the window's"
            <| fun () ->
                hiddenWindow (fun window ->
                    use graph = new Graph ()
                    let count = graph.Run (fun () -> Api.createSignal 0)
                    use mounted = mount window.Host graph label count

                    window.Host.RequestAnimationFrame (fun _ -> count.Value <- 5)
                    |> ignore

                    window.Present ()
                    Expect.equal mounted.Text "5" "after the frame"
                    Expect.isEmpty (List.ofSeq window.Host.Errors) "host errors")

            testCase "a frame callback that needs the ambient graph reports its absence"
            <| fun () ->
                hiddenWindow (fun window ->
                    use graph = new Graph ()
                    let count = graph.Run (fun () -> Api.createSignal 0)
                    use mounted = mount window.Host graph label count

                    let printed =
                        standardError (fun () ->
                            window.Host.RequestAnimationFrame (fun _ -> Api.batch (fun () -> count.Value <- 5))
                            |> ignore

                            window.Present ())

                    Expect.equal mounted.Text "0" "the write did not run"
                    Expect.stringContains printed "No ambient graph" "the reason is printed"
                    window.Host.Errors.Clear ())

            testCase "an exception in a frame callback is printed once with its stack"
            <| fun () ->
                hiddenWindow (fun window ->
                    let printed =
                        standardError (fun () ->
                            window.Host.RequestAnimationFrame (fun _ -> invalidOp "frame callback failure")
                            |> ignore

                            window.Present ()
                            window.Present ())

                    Expect.equal (occurrences "frame callback failure" printed) 1 "printed once"
                    Expect.stringContains printed "SilentFailureTests" "with the stack of the callback"
                    Expect.equal window.Host.Errors.Count 1 "and kept on the host"
                    window.Host.Errors.Clear ())

            testCase "an exception in a binding is printed once with its stack"
            <| fun () ->
                hiddenWindow (fun window ->
                    use graph = new Graph ()
                    let keys = graph.Run (fun () -> Api.createSignal [ "x"; "y" ])
                    use mounted = mountReporting window.Host graph chart keys
                    Expect.equal mounted.Text "x,y" "first render"

                    let printed =
                        standardError (fun () ->
                            window.Host.RequestAnimationFrame (fun _ -> graph.Run (fun () -> keys.Value <- [ "xl"; "y"; "xl" ]))
                            |> ignore

                            window.Present ()
                            window.Present ())

                    Expect.equal mounted.Text "x,y" "the previous chart stays"

                    Expect.isTrue
                        (window.Host.Errors
                         |> Seq.exists (fun error -> error.ToString().Contains "duplicate key 'xl'"))
                        "the host holds the binding's exception"

                    Expect.equal (occurrences "duplicate key 'xl'" printed) 1 "printed once"
                    Expect.stringContains printed "SilentFailureTests.scene" "with the stack of the function that raised"
                    window.Host.Errors.Clear ())

            testCase "a scene with a duplicate key built in a binding reports the error from a click"
            <| fun () ->
                hiddenWindow (fun window ->
                    use graph = new Graph ()
                    let keys = graph.Run (fun () -> Api.createSignal [ "x"; "y" ])
                    use mounted = mountReporting window.Host graph chart keys
                    let span = mounted.Container.querySelector "span" :?> Ranvier.Dom.HTMLElement

                    span.addEventListener ("click", (fun (_: Ranvier.Dom.Event) -> keys.Value <- [ "xl"; "y"; "xl" ]))

                    let printed =
                        standardError (fun () ->
                            span.click ()
                            window.Host.Cycle ())

                    Expect.isTrue
                        (window.Host.Errors
                         |> Seq.exists (fun error -> error.ToString().Contains "duplicate key 'xl'"))
                        "the host holds the binding's exception"

                    Expect.equal (occurrences "duplicate key 'xl'" printed) 1 "printed once"
                    window.Host.Errors.Clear ())
        ]
