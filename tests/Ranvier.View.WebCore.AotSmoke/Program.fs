module Ranvier.View.WebCore.AotSmoke.Program

open System
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore

type Counter = { Count: Signal<int> }

let private counter =
    Component.define (fun _ (start: int) -> { Count = Api.createSignal start }) (fun t ->
        t.button
            [|
                t.Attrs.attribute ("style", "font-size:48px;width:200px;height:80px")
                t.Events.on ("click", fun state _ -> state.Count.Value <- state.Count.Value + 1)
            |]
            [| t.text (fun state -> string state.Count.Value) |])

let mutable private failures = 0

let private check (name: string) (ok: bool) (detail: string) =
    if ok then
        Console.WriteLine ("ok   " + name)
    else
        failures <- failures + 1
        Console.WriteLine ("FAIL " + name + ": " + detail)

let private paint (host: WebCoreHost) =
    let struct (width, height) = host.PaintSize
    let pixels = Array.zeroCreate<byte>(width * height * 4)
    host.Paint pixels
    pixels

let private sharedDomChecks (host: WebCoreHost) =
    let document = host.Document
    let parent = document.createElement "div"
    let child = document.createElement "b"
    child.textContent <- "node"
    parent.append [| NodeOrDOMString.DOMString "λ"; NodeOrDOMString.Node child |]
    parent.classList.add [| "first"; "βeta" |]

    check
        "union and sequence inputs"
        (parent.textContent = "λnode"
         && parent.classList.contains "βeta")
        parent.outerHTML

    check "sequence output" (parent.getAttributeNames () = [| "class" |]) parent.outerHTML
    let template = document.createElement "template" :?> HTMLTemplateElement
    template.innerHTML <- "<section><template><b>nested</b></template></section>"
    let secondary = document.implementation.createHTMLDocument "second"
    let imported = secondary.importNode (template, true) :?> HTMLTemplateElement
    let nested = imported.content.querySelector "template" :?> HTMLTemplateElement

    check
        "cross-document nested template import"
        (nested.content.textContent = "nested"
         && obj.ReferenceEquals (imported.ownerDocument, secondary))
        imported.innerHTML

    let cloned = imported.cloneNode true :?> HTMLTemplateElement
    let clonedNested = cloned.content.querySelector "template" :?> HTMLTemplateElement

    check
        "nested template clone"
        (clonedNested.content.textContent = "nested"
         && not (obj.ReferenceEquals (cloned.content, imported.content)))
        cloned.innerHTML

    let mutable calls = 0

    let listener (event: Event) =
        calls <- calls + 1
        event.preventDefault ()

    parent.addEventListener ("probe", listener, AddEventListenerOptions (once = true, passive = Some true))

    let fire () =
        let event = document.createEvent "Event"
        event.initEvent ("probe", true, true)
        parent.dispatchEvent event, event.defaultPrevented

    let first = fire ()
    let second = fire ()

    check
        "passive once listener options"
        (first = (true, false)
         && second = (true, false)
         && calls = 1)
        (string calls)

let private hydrationChecks (host: WebCoreHost) =
    use graph = new Graph ()
    let container = host.Document.createElement "div"

    container.innerHTML <-
        Server.render
            { Server.Options.defaults with
                Policy = Omit
            }
            counter
            7

    host.Document.body.appendChild container |> ignore
    let before = container.querySelector "button" :?> HTMLElement

    let mounted =
        Hydration.hydrate graph None (fun errors -> raise (AggregateException errors)) Hydration.HydrationOptions.defaults container counter 7

    check "hydration retains its server node" (obj.ReferenceEquals (before, container.querySelector "button")) container.innerHTML
    before.click ()
    host.Cycle ()
    check "hydrated event updates its state" (container.textContent = "8") container.textContent
    graph.Run (fun () -> mounted.Dispose ())

let private styleAndPopoverChecks (host: WebCoreHost) =
    let source = host.Document.createElement "button" :?> HTMLElement
    let style = (source :> ElementCSSInlineStyle).style
    style.setProperty ("color", "red")
    check "omitted CSS priority uses the IDL default" (style.getPropertyValue "color" = "red") source.outerHTML
    let popover = host.Document.createElement "div" :?> HTMLElement
    popover.popover <- "manual"
    host.Document.body.appendChild source |> ignore
    host.Document.body.appendChild popover |> ignore
    popover.showPopover (ShowPopoverOptions (source = Some null))
    let openedWithNull = popover.matches ":popover-open"
    popover.hidePopover ()
    popover.showPopover (ShowPopoverOptions (source = Some source))
    let openedWithSource = popover.matches ":popover-open"

    let closed =
        not (popover.togglePopover (TogglePopoverOptions (source = Some source, force = Some false)))

    check
        "popover dictionary handles and force"
        (openedWithNull
         && openedWithSource
         && closed
         && not (popover.matches ":popover-open"))
        popover.outerHTML

[<EntryPoint>]
let main _ =
    use host = WebCoreHost.Create (320, 200)
    let blank = paint host
    use graph = new Graph ()
    let container = host.Document.createElement "div"
    host.Document.body.appendChild container |> ignore

    let mounted =
        Mounting.mount graph None (fun errors -> raise (AggregateException (errors))) container None (fun context ->
            Program.run (Component.render counter 1) context.Backend)

    check "the mount rendered" (container.textContent = "1") container.textContent
    let first = paint host
    check "the mount painted" (first <> blank) "the page is blank"

    let button = container.querySelector "button" :?> HTMLElement
    let mutable x = 0.0
    let mutable y = 0.0
    let mutable width = 0.0
    let mutable height = 0.0

    RanvierWebCoreAbi.Native.rv_element_rect ((box button :?> NativeObject).Handle, &x, &y, &width, &height)
    |> ignore

    host.Click (int (x + width / 2.0), int (y + height / 2.0))
    |> ignore

    host.Cycle ()
    check "a native click updated the text" (container.textContent = "2") container.textContent
    host.Frame ()
    let second = paint host
    check "the update repainted" (second <> first) "the pixels are unchanged"
    check "no listener errors" (host.Errors.Count = 0) (string host.Errors.Count)
    graph.Run (fun () -> mounted.Dispose ())
    sharedDomChecks host
    hydrationChecks host
    styleAndPopoverChecks host
    check "no ABI21 listener errors" (host.Errors.Count = 0) (string host.Errors.Count)
    failures
