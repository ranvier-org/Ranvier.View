module App

open System
open Fable.Core
open Ranvier.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Dsl
open Ranvier.View.Dom

type private DemoState =
    {
        Count: Signal<int>
        Name: Signal<string>
    }

type private Demo =
    {
        State: DemoState
        Doubled: Memo<int>
    }

    static member Create(_: Setup, state: DemoState) =
        {
            State = state
            Doubled = createMemo (fun _ -> state.Count.Value * 2)
        }

    static member View: TemplateView<Demo> =
        section (
            section.className "demo-grid",
            section.data ("testid", "demo-root"),
            section.data ("parity", fun demo -> Some (if demo.State.Count.Value % 2 = 0 then "even" else "odd"))
        ) {
            article (article.className "panel counter-panel") {
                div (div.className "panel-heading") {
                    span (span.className "eyebrow") { "01 / Counter" }
                    span (span.className "tag") { "signal → text" }
                }

                output (output.className "count", output.data ("testid", "count"), output.aria ("live", "polite")) {
                    fun demo -> string demo.State.Count.Value
                }

                div (div.className "derived") {
                    span () { "Doubled" }
                    output (output.data ("testid", "doubled")) { fun demo -> string demo.Doubled.Value }
                }

                div (div.className "actions") {
                    button (
                        button.className "primary",
                        button.data ("testid", "increment"),
                        button.``type`` "button",
                        button.onClick (fun demo _ -> demo.State.Count.Value <- demo.State.Count.Value + 1)
                    ) {
                        "Increment +1"
                    }

                    button (
                        button.className "secondary",
                        button.data ("testid", "reset"),
                        button.``type`` "button",
                        button.props.disabled (fun demo -> demo.State.Count.Value = 0),
                        button.onClick (fun demo _ -> demo.State.Count.Value <- 0)
                    ) {
                        "Reset"
                    }

                    button (
                        button.className "secondary",
                        button.data ("testid", "burst"),
                        button.``type`` "button",
                        button.onClick (fun demo _ ->
                            batch (fun () ->
                                for _ in 1..100 do
                                    demo.State.Count.Value <- demo.State.Count.Value + 1))
                    ) {
                        "Burst +100"
                    }
                }

                p (p.className "note") { "The output changes. The DOM nodes stay." }
            }

            article (article.className "panel name-panel") {
                div (div.className "panel-heading") {
                    span (span.className "eyebrow") { "02 / Input" }
                    span (span.className "tag") { "event → signal" }
                }

                label (label.attribute ("for", "name-field")) { "Your name" }

                input (
                    input.id "name-field",
                    input.``type`` "text",
                    input.attribute ("autocomplete", "off"),
                    input.data ("testid", "name"),
                    input.props.value _.State.Name.Value,
                    input.onInput (fun demo event -> demo.State.Name.Value <- (unbox<HTMLInputElement> event.currentTarget).value)
                )

                p (p.className "greeting", p.data ("testid", "greeting"), p.aria ("live", "polite")) { fun demo -> $"Hello, {demo.State.Name.Value}" }

                p (p.className "note") { "Type anywhere in the field. Focus and selection are preserved." }
            }
        }

let private demoComponent =
    Component.template (fun setup props -> Demo.Create (setup, props)) Demo.View

type private Foundation =
    {
        Rows: Signal<(int * string) array>
        Busy: unit -> bool
        Status: unit -> string
        Result: unit -> string
        Error: unit -> string
        Pending: unit -> unit
        Fail: unit -> unit
        Recover: unit -> unit
    }

    static member Create(_: Setup, ()) =
        let graph = Graph.Current
        let source = createSignal (createAsyncSource<string>())

        let result =
            createBoundary (fun _ -> "Loading…") (fun _ _ -> "Could not load the result") (fun () -> source.Value.Value)

        source.Value.Settle "Loaded result"

        {
            Rows = createSignal [| 1, "Alpha"; 2, "Beta" |]
            Busy =
                fun () ->
                    result.Value |> ignore
                    result.IsWaiting
            Status =
                fun () ->
                    result.Value |> ignore

                    if result.IsWaiting then "Pending"
                    elif not (isNull result.Caught) then "Failed"
                    else "Ready"
            Result = fun () -> result.Value
            Error =
                fun () ->
                    result.Value |> ignore
                    if isNull result.Caught then "" else result.Caught.Message
            Pending = fun () -> graph.Run (fun () -> source.Value <- createAsyncSource<string>())
            Fail = fun () -> source.Value.Fail (Exception "Connection unavailable")
            Recover = fun () -> source.Value.Settle "Recovered result"
        }

    static member View: TemplateView<Foundation> =
        section () {
            section (
                section.id "foundation-demo",
                section.className "panel",
                section.data ("row-count", fun state -> Some (string state.Rows.Value.Length))
            ) {
                "Foundation implementation: keyed rows"

                button (
                    button.id "foundation-add",
                    button.``type`` "button",
                    button.onClick (fun state _ ->
                        let next =
                            state.Rows.Value
                            |> Array.map fst
                            |> Array.max
                            |> (+) 1

                        state.Rows.Value <- Array.append state.Rows.Value [| next, "Item " + string next |])
                ) {
                    "Add row"
                }

                button (
                    button.id "foundation-reverse",
                    button.``type`` "button",
                    button.onClick (fun state _ -> state.Rows.Value <- Array.rev state.Rows.Value)
                ) {
                    "Reverse rows"
                }

                forEach
                    fst
                    _.Rows.Value
                    (div (div.id (fun current -> "foundation-row-" + string (fst (current ())))) { fun current -> snd (current ()) })
            }

            renderOf ComponentsExample.``component`` ()

            div (
                div.id "foundation-load",
                div.role "region",
                div.aria ("label", "Load result"),
                div.aria ("busy", fun state -> Some (if state.Busy () then "true" else "false"))
            ) {
                "Load result"
                div (div.id "foundation-load-status") { fun state -> state.Status () }
                div (div.id "foundation-load-result") { fun state -> state.Result () }
                div (div.id "foundation-load-error") { fun state -> state.Error () }

                button (button.id "foundation-pending", button.``type`` "button", button.onClick (fun state _ -> state.Pending ())) {
                    "Start loading"
                }

                button (button.id "foundation-fail", button.``type`` "button", button.onClick (fun state _ -> state.Fail ())) { "Simulate failure" }

                button (button.id "foundation-recover", button.``type`` "button", button.onClick (fun state _ -> state.Recover ())) { "Recover" }
            }
        }

let private foundationComponent =
    Component.template (fun setup props -> Foundation.Create (setup, props)) Foundation.View

type private Shell =
    {
        Mounted: Signal<bool>
        Queued: Signal<bool>
        Toggle: unit -> unit
        ToggleScheduling: unit -> unit
        Flush: unit -> unit
        AttachDemo: HTMLDivElement -> (unit -> unit)
    }

    static member Create(_: Setup, ()) =
        let graph = Graph.Current

        let state =
            {
                Count = createSignal 0
                Name = createSignal "world"
            }

        let mounted = createSignal true
        let queued = createSignal false
        let mutable host: HTMLElement = Unchecked.defaultof<_>
        let mutable demo: Mounting.Mounted option = None

        let disposeDemo () =
            demo
            |> Option.iter (fun current -> current.Dispose ())

            demo <- None

        let mountDemo () =
            let mode =
                if queued.Value then
                    Microtask.mode Scheduling.CollectAndContinue
                else
                    Scheduling.SynchronousCommits

            let report (errors: exn list) =
                for error in errors do
                    JS.console.error ("Ranvier mount failure", error)

            demo <-
                Some (
                    Mounting.mountWith mode graph None report host None (fun context ->
                        Program.run (Ranvier.View.Template.Component.render demoComponent state) context.Backend)
                )

        {
            Mounted = mounted
            Queued = queued
            Toggle =
                fun () ->
                    match demo with
                    | Some _ ->
                        disposeDemo ()
                        mounted.Value <- false
                    | None ->
                        mountDemo ()
                        mounted.Value <- true
            ToggleScheduling =
                fun () ->
                    queued.Value <- not queued.Value

                    if demo.IsSome then
                        disposeDemo ()
                        mountDemo ()
            Flush =
                fun () ->
                    demo
                    |> Option.iter (fun current -> current.Flush ())
            AttachDemo =
                fun element ->
                    host <- element

                    if mounted.Value then
                        mountDemo ()

                    fun () ->
                        disposeDemo ()
                        host <- Unchecked.defaultof<_>
        }

    static member View: TemplateView<Shell> =
        div (div.className "playground") {
            header (header.className "page-heading") {
                div (div.className "brand") {
                    span (span.className "brand-mark") { "r" }
                    span () { "Fable.Ranvier" }
                }

                span (span.className "preview") { "DOM PoC" }
            }

            section (section.className "intro") {
                p (p.className "eyebrow") { "F# · Fable · Browser DOM" }

                h1 () {
                    "Create once."
                    br ()
                    span () { "Update what changes." }
                }

                p (p.className "lead") { "A small playground for reactive text, properties and events. No component tree rebuild on each edit." }
            }

            div (div.className "mount-bar") {
                span (span.data ("testid", "scheduling-status")) {
                    fun shell ->
                        if shell.Queued.Value then
                            "Microtask DOM commits"
                        else
                            "Synchronous DOM commits"
                }

                button (
                    button.className "secondary",
                    button.data ("testid", "scheduling-toggle"),
                    button.``type`` "button",
                    button.onClick (fun shell _ -> shell.ToggleScheduling ())
                ) {
                    fun shell ->
                        if shell.Queued.Value then
                            "Use synchronous"
                        else
                            "Use microtasks"
                }

                button (
                    button.className "secondary",
                    button.data ("testid", "flush-dom"),
                    button.``type`` "button",
                    button.onClick (fun shell _ -> shell.Flush ())
                ) {
                    "Flush DOM"
                }
            }

            div (div.className "mount-bar") {
                div (div.className "mount-state") {
                    span (span.className "status-dot", span.data ("active", fun shell -> if shell.Mounted.Value then Some "true" else None)) { }

                    span (span.data ("testid", "mount-status")) {
                        fun shell ->
                            if shell.Mounted.Value then
                                "Demo mounted"
                            else
                                "Demo unmounted"
                    }
                }

                button (
                    button.className "secondary toggle",
                    button.data ("testid", "mount-toggle"),
                    button.``type`` "button",
                    button.onClick (fun shell _ -> shell.Toggle ())
                ) {
                    fun shell ->
                        if shell.Mounted.Value then
                            "Unmount demo"
                        else
                            "Remount demo"
                }
            }
            // This widget owns the mount whose commit scheduler and lifetime the controls demonstrate.
            div (div.widget (fun shell element -> shell.AttachDemo element)) { }
            renderOf foundationComponent ()

            section (section.className "empty-state", section.hidden _.Mounted.Value) {
                h2 () { "The mount is disposed." }
                p () { "Its nodes, bindings and event listeners have been removed. Remount to continue with the same state." }
            }

            footer () { "Built with Xantham DOM bindings and Ranvier scopes. Experimental API; DOM scheduling is opt-in." }
        }

/// <summary>The shared playground shell, keyed rows, load status and reusable component examples.</summary>
let ``component`` =
    Component.template (fun setup props -> Shell.Create (setup, props)) Shell.View

[<Emit("globalThis.playground = { dispose: $0 }")>]
let private publish (dispose: unit -> unit) : unit = jsNative

/// <summary>Publishes the generated page's disposal handle for browser lifecycle tests.</summary>
let expose (page: PageClient.Started) =
    publish page.Dispose
