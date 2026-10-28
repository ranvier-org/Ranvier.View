module Ranvier.View.WebCore.Tests.ObjectTests

open System
open System.Runtime.CompilerServices
open Expecto
open Ranvier.Dom
open Ranvier
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private createElements (document: Document) (count: int) =
    for _ in 1..count do
        document.createElement "span" |> ignore

let private collect (host: WebCoreHost) =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    host.Cycle ()

[<Tests>]
let tests =
    testList
        "Objects"
        [
            hostCase "one native node has one managed object"
            <| fun host ->
                let body = host.Document.body
                Expect.isTrue (obj.ReferenceEquals (body, host.Document.body)) "body read twice"
                let child = host.Document.createElement "p"
                body.appendChild child |> ignore
                Expect.isTrue (obj.ReferenceEquals (child, body.lastChild)) "the appended child read back"
                Expect.isTrue (obj.ReferenceEquals (body, child.parentNode)) "the parent read back"

            hostCase "an element gets the class of its tag"
            <| fun host ->
                let document = host.Document
                Expect.isTrue (box (document.createElement "input") :? HTMLInputElement) "input"
                Expect.isTrue (box (document.createElement "template") :? HTMLTemplateElement) "template"
                Expect.isTrue (box document.head :? HTMLHeadElement) "head"
                Expect.isTrue (box (document.createElement "x-custom") :? HTMLElement) "an unknown HTML tag"
                Expect.isTrue (box (document.createTextNode "t") :? Text) "text"
                Expect.isTrue (box (document.createDocumentFragment ()) :? DocumentFragment) "fragment"

            hostCase "a collected object releases its reference on the next cycle"
            <| fun host ->
                collect host
                let released = host.Session.Released
                createElements host.Document 100
                collect host
                Expect.isGreaterThanOrEqual (host.Session.Released - released) 100 "the 100 unclaimed elements"

            hostCase "an owner keeps the objects it claimed until its disposal"
            <| fun host ->
                collect host
                use graph = new Graph ()

                let owner =
                    graph.Run (fun () ->
                        createRoot (fun owner ->
                            createElements host.Document 50
                            owner))

                let released = host.Session.Released
                collect host
                Expect.equal (host.Session.Released - released) 0 "claimed objects survive a collection"
                graph.Run (fun () -> owner.Dispose ())
                collect host
                Expect.isGreaterThanOrEqual (host.Session.Released - released) 50 "released after the owner's disposal"

            testCase "host disposal releases the references of live objects"
            <| fun () ->
                onHostThread (fun () ->
                    let host = WebCoreHost.Create (100, 100)
                    let element = host.Document.createElement "div"
                    Expect.notEqual (box element :?> NativeObject).Handle 0n "a live handle"
                    (host :> IDisposable).Dispose()
                    Expect.equal (box element :?> NativeObject).Handle 0n "released at disposal")

            hostCase "an unmapped member raises NotSupportedException naming it"
            <| fun host ->
                let element = host.Document.createElement "area" :?> HTMLAreaElement

                Expect.throwsC (fun () -> element.href |> ignore) (fun e ->
                    Expect.isTrue (e :? NotSupportedException) "the exception type"
                    Expect.stringContains e.Message "HTMLHyperlinkElementUtils.href is not available in the native backend" "the message")

            hostCase "a DOM exception carries its name"
            <| fun host ->
                let parent = host.Document.createElement "div"
                let stranger = host.Document.createElement "span"

                Expect.throwsC (fun () -> parent.removeChild stranger |> ignore) (fun e ->
                    match e with
                    | WebCoreDomException (name, _) -> Expect.equal name "NotFoundError" "the DOM name"
                    | other -> failtestf "expected WebCoreDomException, got %O" other)

            hostCase "union variadics preserve text, node identity and sequence values"
            <| fun host ->
                let document = host.Document
                let parent = document.createElement "div"
                let child = document.createElement "b"
                child.textContent <- "node"
                parent.id <- "variadic"
                parent.append [| NodeOrDOMString.DOMString "λ"; NodeOrDOMString.Node child |]
                Expect.equal parent.textContent "λnode" "both union cases were appended"
                Expect.isTrue (obj.ReferenceEquals (parent.lastChild, child)) "the node was moved, not copied"
                parent.prepend [| NodeOrDOMString.DOMString "start:" |]
                let replacement = document.createElement "i"
                replacement.textContent <- "replacement"

                (child :> ChildNode).replaceWith
                    [|
                        NodeOrDOMString.DOMString "替"
                        NodeOrDOMString.Node replacement
                        NodeOrDOMString.DOMString ":end"
                    |]

                Expect.equal parent.textContent "start:λ替replacement:end" "mixed replacement arguments retain their order"
                Expect.isNull child.parentNode "the replaced child was detached"
                parent.classList.add [| "alpha"; "βeta" |]
                Expect.isTrue (parent.classList.contains "βeta") "UTF-8 sequence input"
                Expect.sequenceEqual (parent.getAttributeNames ()) [| "id"; "class" |] "sequence output preserves attribute order"
                parent.classList.remove [| "alpha"; "βeta" |]
                parent.classList.add [||]
                Expect.equal parent.classList.length 0u "empty variadics do not add tokens"
                parent.replaceChildren [| NodeOrDOMString.DOMString "only:"; NodeOrDOMString.Node replacement |]
                Expect.equal parent.textContent "only:replacement" "replaceChildren accepts the same union sequence"
                Expect.isTrue (obj.ReferenceEquals (parent.lastChild, replacement)) "a kept argument retains its identity"

            hostCase "secondary documents clone and import nested template contents independently"
            <| fun host ->
                let document = host.Document
                let template = document.createElement "template" :?> HTMLTemplateElement
                template.innerHTML <- "<section data-kind=\"outer\"><template><b>nested</b></template></section>"
                document.body.appendChild template |> ignore

                let nested (value: HTMLTemplateElement) =
                    value.content.querySelector "template" :?> HTMLTemplateElement

                let clone = template.cloneNode true :?> HTMLTemplateElement
                Expect.equal clone.innerHTML template.innerHTML "deep clone retains the template markup"
                Expect.equal (nested clone).content.textContent "nested" "deep clone retains nested content"
                Expect.isFalse (obj.ReferenceEquals (clone.content, template.content)) "clones have independent content fragments"
                let shallow = template.cloneNode false :?> HTMLTemplateElement
                Expect.equal shallow.content.childNodes.length 0u "shallow clones have no content children"
                let secondary = document.implementation.createHTMLDocument "secondary"
                Expect.equal secondary.title "secondary" "the detached document has its own title"
                let prefixed = secondary.createElementNS ("urn:probe", "p:entry")

                Expect.equal
                    (prefixed.namespaceURI, prefixed.prefix, prefixed.localName)
                    ("urn:probe", "p", "entry")
                    "namespace and qualified name survive creation"

                let imported = secondary.importNode (template, true) :?> HTMLTemplateElement
                secondary.body.appendChild imported |> ignore
                Expect.isTrue (obj.ReferenceEquals (imported.ownerDocument, secondary)) "import assigns the destination document"
                Expect.equal imported.innerHTML template.innerHTML "import retains the outer and nested template content"
                ((nested imported).content.querySelector "b").textContent <- "changed"
                Expect.equal (nested template).content.textContent "nested" "editing the import does not modify its source"
                let whole = document.cloneNode true :?> Document
                let wholeTemplate = whole.querySelector "template" :?> HTMLTemplateElement
                Expect.equal (nested wholeTemplate).content.textContent "nested" "whole-document cloning also preserves nested template contents"

            hostCase "popover dictionaries carry optional source handles and force flags"
            <| fun host ->
                let popover = host.Document.createElement "div" :?> HTMLElement
                let source = host.Document.createElement "button" :?> HTMLElement
                popover.popover <- "manual"
                host.Document.body.appendChild source |> ignore
                host.Document.body.appendChild popover |> ignore

                let isOpen () =
                    popover.matches ":popover-open"

                popover.showPopover ()
                Expect.isTrue (isOpen ()) "omitting the options opens the popover"
                popover.hidePopover ()
                Expect.isFalse (isOpen ()) "hide closes it"
                popover.showPopover (ShowPopoverOptions ())
                Expect.isTrue (isOpen ()) "a dictionary with an omitted source opens it"
                popover.hidePopover ()
                popover.showPopover (ShowPopoverOptions (source = Some null))
                Expect.isTrue (isOpen ()) "an explicit null source remains a null native handle"
                popover.hidePopover ()
                popover.showPopover (ShowPopoverOptions (source = Some source))
                Expect.isTrue (isOpen ()) "a nonnull source carries its node handle"
                popover.hidePopover ()
                Expect.isTrue (popover.togglePopover (TogglePopoverOptions (source = Some source, force = Some true))) "dictionary force opens it"
                Expect.isTrue (isOpen ()) "dictionary force changed the native popover state"
                Expect.isTrue (popover.togglePopover true) "forcing open again keeps it open"

                Expect.isFalse
                    (popover.togglePopover (TogglePopoverOptions (source = Some null, force = Some false)))
                    "dictionary force closes it with a null source"

                Expect.isFalse (isOpen ()) "dictionary force false changed the native state"
                Expect.isTrue (popover.togglePopover (TogglePopoverOptions (force = Some true))) "force works when the source is omitted"
                Expect.isFalse (popover.togglePopover false) "the boolean overload closes it"
                Expect.isFalse (isOpen ()) "all paths leave the popover closed"

            hostCase "generated members read and write the page"
            <| fun host ->
                let input = host.Document.createElement "input" :?> HTMLInputElement
                input.value <- "typed"
                Expect.equal input.value "typed" "HTMLInputElement.value"
                input.className <- "a b"
                Expect.isTrue (input.classList.contains "b") "Element.classList"
                let style = (input :> ElementCSSInlineStyle).style
                style.setProperty ("color", "red")
                Expect.equal (style.getPropertyValue "color") "red" "the CSS property read back"
                Expect.equal (input.getAttribute "style") "color: red;" "a CSS property through setProperty"
                let template = host.Document.createElement "template" :?> HTMLTemplateElement
                template.innerHTML <- "<li>one</li><li>two</li>"
                Expect.equal template.content.childNodes.length 2u "template content"
                Expect.equal template.innerHTML "<li>one</li><li>two</li>" "template innerHTML"
                let attributes = (input :> Element).attributes
                Expect.equal attributes.length 2u "attribute count"
                Expect.equal (attributes.item 0u).name "class" "first attribute"
        ]
