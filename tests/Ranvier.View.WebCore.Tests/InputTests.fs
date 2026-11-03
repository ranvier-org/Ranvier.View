module Ranvier.View.WebCore.Tests.InputTests

open System
open Expecto
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support
open Ranvier.View.WebCore.Tests.MountTests

type Check = { Prevent: bool; Clicks: Signal<int> }

let private checkbox =
    Component.define (fun _ (state: Check) -> state) (fun t ->
        t.input
            [|
                t.Attrs.attribute ("type", "checkbox")
                t.Attrs.attribute ("style", "width:40px;height:40px")
                t.Events.on (
                    "click",
                    fun state (event: Event) ->
                        state.Clicks.Value <- state.Clicks.Value + 1

                        if state.Prevent then
                            event.preventDefault ()
                )
            |])

let private probeEvent (host: WebCoreHost) =
    let event = host.Document.createEvent "Event"
    event.initEvent ("options-probe", true, true)
    event

[<Tests>]
let tests =
    testList
        "Input"
        [
            testCase "every input rejects a foreign thread and a disposed host"
            <| fun () ->
                onHostThread (fun () ->
                    use host = WebCoreHost.Create (320, 240)
                    let pointer = PointerSample.Create (0, 1, 0, 10.0, 10.0)

                    let operations =
                        [|
                            "mouse", fun () -> host.Mouse (0, 10, 10, -1, 0, 0) |> ignore
                            "click", fun () -> host.Click (10, 10) |> ignore
                            "wheel", fun () -> host.Wheel (10, 10, 0.0f, 1.0f, 0) |> ignore
                            "key",
                            fun () ->
                                host.Key (0, "a", "KeyA", null, 0, false)
                                |> ignore
                            "type", fun () -> host.Type ("a", "KeyA", "a")
                            "pointer", fun () -> host.Pointer [| pointer |] |> ignore
                        |]

                    let failures = ResizeArray<exn>()
                    let mutable checkedInputs = 0

                    let worker =
                        Threading.Thread (
                            Threading.ThreadStart (fun () ->
                                for name, operation in operations do
                                    try
                                        Expect.throwsT<InvalidOperationException> operation (name + " requires the owner thread")
                                        checkedInputs <- checkedInputs + 1
                                    with error ->
                                        failures.Add error)
                        )

                    worker.IsBackground <- true
                    worker.Start ()
                    Expect.isTrue (worker.Join 5000) "foreign-thread admission checks terminate"

                    for failure in failures do
                        raise failure

                    Expect.equal checkedInputs operations.Length "every input admission was checked"
                    host.Click (10, 10) |> ignore
                    (host :> IDisposable).Dispose()

                    for name, operation in operations do
                        Expect.throwsT<ObjectDisposedException> operation (name + " rejects the disposed host"))

            hostCase "editor undo and redo route through native transaction history"
            <| fun host ->
                if not (WebCoreHost.Has "editing-undo") then
                    Expect.throwsT<NotSupportedException> (fun () -> host.Undo () |> ignore) "old native library reports unavailable undo"
                    Expect.throwsT<NotSupportedException> (fun () -> host.Redo () |> ignore) "old native library reports unavailable redo"
                else
                    Expect.isFalse (host.Undo ()) "empty editor history is not handled"
                    let element = host.Document.createElement "input"
                    let input = element :?> HTMLInputElement
                    input.value <- "original"
                    host.Document.body.appendChild element |> ignore
                    host.SetFocused true
                    input.focus ()
                    input.select ()
                    host.CommitComposition "edited"
                    Expect.equal input.value "edited" "native user edit"

                    let cancel (event: Event) =
                        event.preventDefault ()

                    input.addEventListener ("beforeinput", cancel)
                    Expect.isTrue (host.Undo ()) "canceled undo still consumes the command"
                    Expect.equal input.value "edited" "beforeinput cancellation preserves text"
                    input.removeEventListener ("beforeinput", cancel)
                    Expect.isTrue (host.Undo ()) "retry retains the editor transaction"
                    Expect.equal input.value "original" "undo restored the selection"
                    Expect.isTrue (host.Redo ()) "redo is handled"
                    Expect.equal input.value "edited" "redo restored the edit"

            testCase "Windows picker decodes single and multiple file selections"
            <| fun () ->
                let buffer =
                    Runtime.InteropServices.Marshal.StringToHGlobalUni ("C:\\files\000one.txt\000two.txt\000\000")

                try
                    let paths = WindowsFileDialog.paths buffer 64

                    Expect.sequenceEqual
                        paths
                        [|
                            IO.Path.Combine ("C:\\files", "one.txt")
                            IO.Path.Combine ("C:\\files", "two.txt")
                        |]
                        "multiple selection paths"
                finally
                    Runtime.InteropServices.Marshal.FreeHGlobal buffer

            testCase "Windows picker cancellation before dialog creation is retained"
            <| fun () ->
                let state = WindowsFileDialog.State ()
                state.Cancel ()
                Expect.isTrue (state.Finish ()) "early invalidation survives dialog creation"

            hostCase "file picker provider delivers and invalidates the original request"
            <| fun host ->
                if WebCoreHost.Has "file-picker" then
                    let events = ResizeArray<FilePickerEvent>()
                    use provider = FilePickers.install host events.Add
                    Expect.throws (fun () -> FilePickers.install host ignore |> ignore) "one provider owns a host"
                    let input = host.Document.createElement "input"
                    input.setAttribute ("type", "file")
                    input.setAttribute ("accept", ".txt,image/png")
                    input.setAttribute ("multiple", "")
                    host.Document.body.appendChild input |> ignore
                    host.Cycle ()
                    clickOn host input
                    Expect.isEmpty (List.ofSeq events) "provider is not called inside native input"
                    host.Cycle ()

                    let first =
                        match events[0] with
                        | Show request -> request
                        | _ -> failtest "expected a picker request"

                    Expect.isTrue first.Multiple "multiple selection is reported"
                    Expect.sequenceEqual first.MimeTypes [| "image/png" |] "MIME filter"
                    Expect.sequenceEqual first.Extensions [| ".txt" |] "extension filter"

                    Expect.throws
                        (fun () ->
                            first.Complete (Selected [| "relative.txt" |])
                            |> ignore)
                        "relative paths are rejected"

                    let path = IO.Path.GetTempFileName ()

                    try
                        Expect.isTrue (first.Complete (Selected [| path |])) "selection completes the live request"
                        Expect.equal (input :?> HTMLInputElement).files.length 1u "selected file reaches the original input"
                        Expect.isFalse (first.Complete Cancelled) "a request completes only once"
                    finally
                        IO.File.Delete path

                    clickOn host input
                    host.Cycle ()

                    let second =
                        match events[events.Count - 1] with
                        | Show request -> request
                        | _ -> failtest "expected a second picker request"

                    Expect.isTrue (second.Complete Cancelled) "cancellation completes a live request"
                    Expect.equal (input :?> HTMLInputElement).files.length 1u "cancellation retains the selected file"
                    clickOn host input
                    host.Cycle ()

                    let removed =
                        match events[events.Count - 1] with
                        | Show request -> request
                        | _ -> failtest "expected a request before removal"

                    input.remove ()
                    host.Cycle ()

                    Expect.isTrue
                        (events
                         |> Seq.exists (function
                             | Invalidate id -> id = removed.Id
                             | _ -> false))
                        "removing the input invalidates the provider"

                    Expect.isFalse (removed.Complete Cancelled) "invalidated requests reject completion"
                    host.Document.body.appendChild input |> ignore
                    host.Cycle ()
                    clickOn host input
                    host.Cycle ()

                    let third =
                        match events[events.Count - 1] with
                        | Show request -> request
                        | _ -> failtest "expected a third picker request"

                    provider.Dispose ()

                    Expect.isTrue
                        (events
                         |> Seq.exists (function
                             | Invalidate id -> id = third.Id
                             | _ -> false))
                        "disposing the provider dismisses its dialog"

                    use replacement = FilePickers.install host ignore
                    ()

            testCase "disposing the host invalidates an open file picker"
            <| fun () ->
                onHostThread (fun () ->
                    use host = WebCoreHost.Create (320, 240)

                    if WebCoreHost.Has "file-picker" then
                        let events = ResizeArray<FilePickerEvent>()
                        use provider = FilePickers.install host events.Add
                        let input = host.Document.createElement "input"
                        input.setAttribute ("type", "file")
                        host.Document.body.appendChild input |> ignore
                        host.Cycle ()
                        clickOn host input
                        host.Cycle ()

                        let request =
                            match events[0] with
                            | Show request -> request
                            | _ -> failtest "expected a picker request"

                        (host :> IDisposable).Dispose()

                        Expect.isTrue
                            (events
                             |> Seq.exists (function
                                 | Invalidate id -> id = request.Id
                                 | _ -> false))
                            "host teardown dismisses the dialog")

            hostCase "disposing a picker provider during selection does not invalidate the completed request"
            <| fun host ->
                if WebCoreHost.Has "file-picker" then
                    let events = ResizeArray<FilePickerEvent>()
                    use provider = FilePickers.install host events.Add
                    let input = host.Document.createElement "input"
                    input.setAttribute ("type", "file")
                    host.Document.body.appendChild input |> ignore

                    let changed (_: Event) =
                        provider.Dispose ()

                    input.addEventListener ("change", changed, AddEventListenerOptions ())
                    host.Cycle ()
                    clickOn host input
                    host.Cycle ()

                    let request =
                        match events[0] with
                        | Show value -> value
                        | _ -> failtest "expected a picker request"

                    let path = IO.Path.GetTempFileName ()

                    try
                        Expect.isTrue (request.Complete (Selected [| path |])) "selection completes"

                        Expect.isFalse
                            (events
                             |> Seq.exists (function
                                 | Invalidate id -> id = request.Id
                                 | _ -> false))
                            "a completed selection is not invalidated"
                    finally
                        IO.File.Delete path

            hostCase "listener dictionaries preserve capture once passive and removal"
            <| fun host ->
                let parent = host.Document.createElement "div"
                let target = host.Document.createElement "button"
                parent.appendChild target |> ignore
                host.Document.body.appendChild parent |> ignore
                let observed = ResizeArray<uint16 * bool * bool>()

                let passive (event: Event) =
                    event.preventDefault ()
                    observed.Add (event.eventPhase, event.defaultPrevented, obj.ReferenceEquals (event.currentTarget, parent))

                parent.addEventListener ("options-probe", passive, AddEventListenerOptions (capture = true, once = true, passive = Some true))
                let first = probeEvent host
                Expect.isTrue (target.dispatchEvent first) "a passive listener cannot cancel a cancelable event"
                Expect.isFalse first.defaultPrevented "preventDefault was ignored inside the passive callback"
                Expect.sequenceEqual observed [| (1us, false, true) |] "capture ran on the parent with its currentTarget"
                Expect.isTrue (target.dispatchEvent (probeEvent host)) "dispatch after the once listener was removed"
                Expect.equal observed.Count 1 "once applies to a passive capture listener"

                let active (event: Event) =
                    event.preventDefault ()

                target.addEventListener ("options-probe", active, AddEventListenerOptions (passive = Some false))
                let canceled = probeEvent host
                Expect.isFalse (target.dispatchEvent canceled) "an explicitly active listener can cancel"
                Expect.isTrue canceled.defaultPrevented "the cancellation reached the native event"
                target.removeEventListener ("options-probe", active, EventListenerOptions (capture = false))
                Expect.isTrue (target.dispatchEvent (probeEvent host)) "dictionary removal matches the original callback and capture flag"

            hostCase "a native click reaches a component's handler"
            <| fun host ->
                use graph = new Graph ()
                use mounted = mount host graph counter 1
                clickOn host (mounted.Container.querySelector "button")
                Expect.equal mounted.Text "2" "the handler ran once"
                clickOn host (mounted.Container.querySelector "button")
                Expect.equal mounted.Text "3" "and again"

            hostCase "the event objects of handled clicks are released after collection"
            <| fun host ->
                use graph = new Graph ()
                use mounted = mount host graph counter 0
                let button = mounted.Container.querySelector "button"

                for _ in 1..20 do
                    clickOn host button

                Expect.equal mounted.Text "20" "every click was handled"
                let released = host.Session.Released
                GC.Collect ()
                GC.WaitForPendingFinalizers ()
                host.Cycle ()
                Expect.isGreaterThanOrEqual (host.Session.Released - released) 20 "the events, at least one per click"

            hostCase "preventDefault in a handler cancels the native default action"
            <| fun host ->
                use graph = new Graph ()

                let prevented =
                    graph.Run (fun () ->
                        {
                            Prevent = true
                            Clicks = Api.createSignal 0
                        })

                use first = mount host graph checkbox prevented
                let box = first.Container.querySelector "input" :?> HTMLInputElement
                clickOn host box
                Expect.equal prevented.Clicks.Value 1 "the handler ran"
                Expect.isFalse box.``checked`` "the checkbox stays unchecked"

                let allowed =
                    graph.Run (fun () ->
                        {
                            Prevent = false
                            Clicks = Api.createSignal 0
                        })

                use second = mount host graph checkbox allowed
                let other = second.Container.querySelector "input" :?> HTMLInputElement
                clickOn host other
                Expect.equal allowed.Clicks.Value 1 "the handler ran"
                Expect.isTrue other.``checked`` "the default action checked it"
        ]
