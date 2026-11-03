module Ranvier.View.WebCore.AotSmoke.Program

open System
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore

#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type Counter =
    {
        Count: Signal<int>
    }
#if !FABLE_COMPILER
    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        elif isNull (box this.Count) then
            "{ Count = null }"
        else
            "{ Count = <Ranvier.Signal<int>> }"
#endif

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

let private nativeDisplayChecks () =
    let display =
        {
            DisplayScreen.Width = 1920
            Height = 1080
            UsableLeft = -2
            UsableTop = 3
            UsableWidth = 1918
            UsableHeight = 1040
            ColorDepth = 24
            Orientation = ScreenOrientationType.LandscapePrimary
        }

    let displayText = display.ToString ()

    check
        "native display diagnostics retain screen fields"
        (displayText.Contains "UsableLeft = -2"
         && displayText.Contains "Orientation = LandscapePrimary")
        displayText

    let invalidDisplay =
        { display with
            Orientation = enum<ScreenOrientationType> 7
        }

    let invalidText = invalidDisplay.ToString ()
    check "native display diagnostics retain unknown orientation" (invalidText.Contains "Orientation = 7") invalidText

    let originalCulture = System.Globalization.CultureInfo.CurrentCulture

    try
        System.Globalization.CultureInfo.CurrentCulture <- System.Globalization.CultureInfo.GetCultureInfo "ar-SA"

        let negativeDisplay =
            { display with
                Orientation = enum<ScreenOrientationType> -1
            }

        let negativeText = negativeDisplay.ToString ()
        let expected = (-1).ToString(System.Globalization.CultureInfo.CurrentCulture)
        check "unknown orientation uses current culture" (negativeText.Contains ("Orientation = " + expected)) negativeText
    finally
        System.Globalization.CultureInfo.CurrentCulture <- originalCulture

    let nullModel = { Count = Unchecked.defaultof<_> }
    let modelText = nullModel.ToString ()
    check "native smoke model diagnostics retain null" (modelText = "{ Count = null }") modelText

    let capability =
        {
            CapabilityKey.Category = CapabilityCategory.Abi
            Name = "abi"
        }

    let capabilityText = capability.ToString ()
    check "capability diagnostics retain fields" (capabilityText = "{ Category = Abi\n  Name = \"abi\" }") capabilityText

    let nodes =
        TemplateNode.FragmentNode [| TemplateNode.TextNode "a"; TemplateNode.TextNode "λ" |]

    let nodeText = nodes.ToString ()
    check "template diagnostics retain typed arrays" (nodeText = "FragmentNode [|TextNode \"a\"; TextNode \"λ\"|]") nodeText

    let window =
        {
            IdentityWindow.Parent = Unchecked.defaultof<_>
            Kind = 1
            A = 2
            B = 3
            Path = Unchecked.defaultof<_>
            Issued = 4
            Window = 5
        }

    let windowText = window.ToString ()

    check
        "private record diagnostics retain fields without recursion"
        (windowText.Contains "Issued = 4"
         && windowText.Contains "Window = 5")
        windowText

    let collectText = Scheduling.CollectAndContinue.ToString ()
    let cancelText = Scheduling.FailFastCancel.ToString ()
    check "commit diagnostics retain collect policy" (collectText = "CollectAndContinue") collectText
    check "commit diagnostics retain cancel policy" (cancelText = "FailFastCancel") cancelText
    let mutable modifierCalls = 0

    let modifier: Typed.Modifier<HTMLButtonElement> =
        Typed.listener "click" (fun _ -> modifierCalls <- modifierCalls + 1)

    let modifierText = modifier.ToString ()

    let modifierMatches =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            modifierText.StartsWith "Modifier "
            && modifierText.Contains "<fun:"
        else
            modifierText = "Modifier <function>"

    check "modifier diagnostics retain the case and opaque function" modifierMatches modifierText
    check "modifier diagnostics do not invoke the listener" (modifierCalls = 0) (string modifierCalls)

    if not System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
        let mutable unexpectedCalls = 0

        let unexpected () =
            unexpectedCalls <- unexpectedCalls + 1
            failwith "a diagnostic executed user code"

        let slot =
            {
                Slot.Target = SlotTarget.TextTarget
                Mode = SlotMode.DirectMode
                Read = Func<obj, obj>(fun _ -> unexpected ())
                Same = Func<obj, obj, bool>(fun _ _ -> unexpected ())
            }

        let slotText = slot.ToString ()

        check
            "native diagnostics retain fields beside delegates"
            (slotText.Contains "Target = TextTarget"
             && slotText.Contains "<System.Func<System.Object,System.Object>>")
            slotText

        let nullSlot = { slot with Read = null; Same = null }
        let nullText = nullSlot.ToString ()

        check
            "native diagnostics retain null delegates"
            (nullText.Contains "Read = null"
             && nullText.Contains "Same = null")
            nullText

        let status =
            {
                BoundaryStatus.IsPending = fun () -> unexpected ()
            }

        let statusText = status.ToString ()
        check "native diagnostics do not invoke functions" (statusText = "{ IsPending = <function> }") statusText

        let opaque =
            { new obj() with
                override _.ToString() =
                    unexpected ()
            }

        let part: Part<obj, obj> =
            Part.StaticProperty (Unchecked.defaultof<PortableProperty>, opaque)

        let partText = part.ToString ()

        check
            "native diagnostics retain cases beside opaque payloads"
            (partText.Contains "StaticProperty"
             && partText.Contains "<System.Object>")
            partText

        let error =
            { new InvalidOperationException("native diagnostic detail") with
                override _.ToString() =
                    unexpected ()
            }

        let failure =
            {
                BoundaryFailure.Error = error
                Retry = fun () -> unexpected ()
            }

        let failureText = failure.ToString ()

        check
            "native exception diagnostics retain known details"
            (failureText.Contains "native diagnostic detail"
             && failureText.Contains "Type ="
             && failureText.Contains "HResult ="
             && failureText.Contains "InnerException =")
            failureText

        check "native diagnostics never execute opaque user code" (unexpectedCalls = 0) (string unexpectedCalls)

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

    if
        WebCoreHost.Has "host-tracing"
        && WebCoreHost.Has "host-errors"
    then
        host.SetRenderingTrace true
        host.SetErrorRecording true
        paint host |> ignore
        let events, dropped = host.DrainRenderingTrace ()
        check "native diagnostics are available" (events.Length > 0 && dropped = 0UL) "trace missing or overflowed"
        check "successful painting has no error" (host.TakeHostError () |> ValueOption.isNone) "unexpected host error"
        host.SetRenderingTrace false
        host.SetErrorRecording false

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
    nativeDisplayChecks ()
    failures
