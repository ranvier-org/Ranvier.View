module Ranvier.View.WebCore.Tests.HostTests

open System
open Expecto
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Twin
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private name (node: obj) =
    match node with
    | null -> "null"
    | :? Node as node -> node.nodeName
    | other -> other.GetType().Name

/// <summary>
/// Drives the DOM members the view calls on nodes, and every IHost member, and records what each returns or leaves in
/// the tree.
/// </summary>
let private script (host: Native.IHost) : string list =
    let log = ResizeArray<string>()
    let document = host.Document
    let root = document.createElement "div" :> Node
    let span = document.createElement "span" :> Node
    let bold = document.createElement "b" :> Node
    let text = document.createTextNode "x" :> Node
    let comment = document.createComment "m" :> Node

    let children (parent: Node) =
        let rec go (node: Node) acc =
            if isNull node then
                List.rev acc
            else
                go node.nextSibling (name node :: acc)

        go parent.firstChild [] |> String.concat ","

    root.appendChild span |> ignore
    root.appendChild text |> ignore
    root.insertBefore (comment, span) |> ignore
    root.insertBefore (bold, null) |> ignore
    log.Add $"children {children root}"

    log.Add $"last {name root.lastChild}"

    log.Add $"parent {name span.parentNode} / {not (isNull root.parentNode)}"

    log.Add $"data {(text :?> CharacterData).data}"
    (text :?> CharacterData).data <- "y"
    log.Add $"data {(text :?> CharacterData).data} {(comment :?> CharacterData).data}"

    let element = span :?> Element
    element.setAttribute ("id", "v")
    log.Add $"""attribute {element.getAttribute "id"} {element.getAttribute "missing"}"""
    element.removeAttribute "id"
    log.Add $"""removed {element.getAttribute "id"}"""

    let deep = root.cloneNode true
    let shallow = root.cloneNode false
    log.Add $"clones {children deep} / {children shallow} / {obj.ReferenceEquals (deep, root)}"

    host.DeleteBetween (comment, bold)
    log.Add $"deleted {children root}"
    (comment :?> ChildNode).remove()
    root.removeChild bold |> ignore
    log.Add $"emptied [{children root}]"

    let mutable queued = 0
    host.QueueMicrotask (fun () -> queued <- queued + 1)
    log.Add $"queued {queued}"

    let button = document.createElement "button" :?> HTMLElement
    document.body.appendChild button |> ignore
    let mutable count = 0
    let mutable seen = ""

    let remove =
        host.Listen (
            button,
            "click",
            fun event ->
                count <- count + 1
                host.OverrideCurrentTarget (event, fun () -> box root)

                seen <- (unbox<Event> event).currentTarget |> box |> name
        )

    button.click ()
    remove ()
    button.click ()
    log.Add $"listener {count} {seen}"
    (button :> ChildNode).remove()
    List.ofSeq log

[<Tests>]
let tests =
    testList
        "IHost"
        [
            // tests/abi/abi_layouts.py in Ranvier.WebCore checks the C layout of RvWindowMetrics: 32 bytes.
            testCase "WindowMetrics mirrors RvWindowMetrics"
            <| fun () ->
                let offset name =
                    int (Runtime.InteropServices.Marshal.OffsetOf<WindowMetrics> name)

                Expect.equal (Runtime.InteropServices.Marshal.SizeOf<WindowMetrics>()) 32 "size"

                Expect.equal
                    ([
                        "Size"
                        "InnerWidth"
                        "InnerHeight"
                        "OuterWidth"
                        "OuterHeight"
                        "DevicePixelRatio"
                     ]
                     |> List.map offset)
                    [ 0; 4; 8; 12; 16; 24 ]
                    "field order and offsets"
            testCase "each member matches the DOM twin"
            <| fun () ->
                onHostThread (fun () ->
                    let twin = TwinHost.Create ()

                    let expected =
                        try
                            script twin
                        finally
                            (twin.Document :> IDisposable).Dispose()

                    use host = WebCoreHost.Create (100, 100)
                    let actual = script host
                    Expect.sequenceEqual actual expected "the native trace equals the twin's"
                    Expect.equal expected.Length 12 "every step recorded")
        ]
