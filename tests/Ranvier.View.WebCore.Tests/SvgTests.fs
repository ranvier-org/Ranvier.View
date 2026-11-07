module Ranvier.View.WebCore.Tests.SvgTests

open Expecto
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Dom.Scheduling
open Ranvier.View.Sugar
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private svgNamespace = "http://www.w3.org/2000/svg"

type Chart =
    {
        On: Signal<bool>
        Points: Signal<int array>
        Paths: ResizeArray<SVGPathElement>
    }

let private chart () =
    {
        On = Api.createSignal true
        Points = Api.createSignal [| 1; 2; 3 |]
        Paths = ResizeArray ()
    }

let private shown =
    Component.template
        (fun _ (state: Chart) -> state)
        (svg () {
            show
                (fun s -> s.On.Value)
                (path (
                    Attrs.attribute ("d", "M0 0L1 1"),
                    Hooks.mounted (fun (s: Chart) (node: SVGPathElement) ->
                        s.Paths.Add node
                        ignore)
                ) {
                    ()
                })
        })

let private listed =
    Component.template
        (fun _ (state: Chart) -> state)
        (svg () {
            g () {
                forEach
                    id
                    (fun (s: Chart) -> s.Points.Value)
                    (circle (Attrs.attribute ("r", fun (point: unit -> int) -> Some (string (point ())))) { () })
            }
        })

// The namespace and local name of each element matching the selector, in document order.
let private names (mount: Mount) (selector: string) =
    let found = mount.Container.querySelectorAll selector

    [
        for i in 0 .. int found.length - 1 ->
            let element = found.item (uint32 i) :?> Element
            element.namespaceURI, element.localName
    ]

// A mount under queued commits, whose posted flushes run at the end of each Act.
let private mountQueued (host: WebCoreHost) (graph: Graph) (definition: Component<'props>) (props: 'props) =
    let posted = ResizeArray<unit -> unit>()
    let container = host.Document.createElement "div" :?> HTMLElement
    host.Document.body.appendChild container |> ignore

    let mounted =
        Mounting.mountWith
            (QueuedCommits (CollectAndContinue, posted.Add))
            graph
            None
            (fun errors -> failwithf "The mount reported %A" errors)
            container
            None
            (fun context -> Program.run (Component.render definition props) context.Backend)

    let mount =
        {
            Graph = graph
            Mounted = mounted
            Container = container
        }

    let drain () =
        while posted.Count > 0 do
            let flush = posted[0]
            posted.RemoveAt 0
            graph.Run flush

    drain ()
    mount, drain

[<Tests>]
let tests =
    testList
        "Svg"
        [
            hostCase "a show inside svg creates its elements in the SVG namespace"
            <| fun host ->
                use graph = new Graph ()
                let state = graph.Run chart
                use mounted = mount host graph shown state
                Expect.equal (names mounted "path") [ svgNamespace, "path" ] "the first arm"
                Expect.equal state.Paths.Count 1 "the mounted hook received an SVGPathElement"
                mounted.Act (host, fun () -> state.On.Value <- false)
                Expect.isEmpty (names mounted "path") "the hidden arm"
                mounted.Act (host, fun () -> state.On.Value <- true)
                Expect.equal (names mounted "path") [ svgNamespace, "path" ] "the arm shown again"
                Expect.equal state.Paths.Count 2 "the remounted arm's hook"

            hostCase "keyed rows inside svg are SVG elements"
            <| fun host ->
                use graph = new Graph ()
                let state = graph.Run chart
                use mounted = mount host graph listed state
                Expect.equal (names mounted "circle") (List.replicate 3 (svgNamespace, "circle")) "the first rows"
                mounted.Act (host, fun () -> state.Points.Value <- [| 3; 4; 1; 5 |])
                Expect.equal (names mounted "circle") (List.replicate 4 (svgNamespace, "circle")) "after a move and inserts"

            hostCase "a show inside svg under queued commits creates SVG elements"
            <| fun host ->
                use graph = new Graph ()
                let state = graph.Run chart
                let mounted, drain = mountQueued host graph shown state
                use _ = mounted
                Expect.equal (names mounted "svg") [ svgNamespace, "svg" ] "the svg element"
                Expect.equal (names mounted "path") [ svgNamespace, "path" ] "the first arm"
                Expect.equal state.Paths.Count 1 "the mounted hook received an SVGPathElement"
                graph.Run (fun () -> state.On.Value <- false)
                drain ()
                host.Cycle ()
                Expect.isEmpty (names mounted "path") "the hidden arm"
                graph.Run (fun () -> state.On.Value <- true)
                drain ()
                host.Cycle ()
                Expect.equal (names mounted "path") [ svgNamespace, "path" ] "the arm shown again"
                Expect.equal state.Paths.Count 2 "the remounted arm's hook"

            hostCase "keyed rows inside svg under queued commits are SVG elements"
            <| fun host ->
                use graph = new Graph ()
                let state = graph.Run chart
                let mounted, drain = mountQueued host graph listed state
                use _ = mounted
                Expect.equal (names mounted "circle") (List.replicate 3 (svgNamespace, "circle")) "the first rows"
                graph.Run (fun () -> state.Points.Value <- [| 3; 4; 1; 5 |])
                drain ()
                host.Cycle ()
                Expect.equal (names mounted "circle") (List.replicate 4 (svgNamespace, "circle")) "after a move and inserts"
        ]
