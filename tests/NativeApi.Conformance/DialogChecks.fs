module internal NativeApiConformance.DialogChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>The engine a run measures, for the qualified differences of Ranvier.WebCore #24.</summary>
type Backend =
    | Native
    | Chromium

/// <summary>Host input and task delivery the fixture drives on an 800x600 CSS viewport.</summary>
type Driver =
    {
        /// <summary>Lets rendering updates and queued tasks run, so queued toggle and close events arrive.</summary>
        Settle: unit -> Async<unit>
        /// <summary>A trusted key press (down, up) of a KeyboardEvent key such as <c>"Escape"</c>.</summary>
        Press: string -> Async<unit>
        /// <summary>A trusted primary-button click at CSS viewport coordinates.</summary>
        Click: int -> int -> Async<unit>
        Backend: Backend
    }

// The D16 producer fixture markup (Ranvier.WebCore tests/fixtures/dialog-popover-cases.json), placed in a container.
let private markup =
    "<style>#d16 dialog{box-sizing:border-box;width:200px;height:100px;padding:0;border:0;margin:0;position:absolute;left:300px;top:200px}#d16 [popover]{box-sizing:border-box;width:100px;height:50px;padding:0;border:0;margin:0;position:fixed;inset:auto;left:600px;top:400px}#d16 #p2{left:600px;top:500px}#d16 #pm{left:10px;top:500px}#d16 #ph{left:150px;top:500px}#d16 #ph2{left:260px;top:500px}#d16 button{position:fixed;width:40px;height:20px;padding:0;border:0;margin:0}#d16-under{position:fixed;left:0;top:0;width:800px;height:600px}</style><div id=\"d16-under\"></div><button id=\"before\" style=\"left:0;top:0\">before</button><dialog id=\"d\"><form method=\"dialog\"><button id=\"dsubmit\" value=\"ok\" style=\"left:0;top:0\">ok</button></form></dialog><dialog id=\"m\"><input id=\"mfirst\"><input id=\"mauto\" autofocus></dialog><dialog id=\"cb\" closedby=\"any\">any</dialog><dialog id=\"cn\" closedby=\"none\">none</dialog><div id=\"p\" popover>auto</div><div id=\"p2\" popover>auto two</div><div id=\"pm\" popover=\"manual\">manual</div><div id=\"ph\" popover=\"hint\">hint</div><div id=\"ph2\" popover=\"hint\">hint two</div><div id=\"plain\">plain</div><button id=\"tog\" popovertarget=\"p\" style=\"left:0;top:40px\">t</button><button id=\"show\" popovertarget=\"p\" popovertargetaction=\"show\" style=\"left:50px;top:40px\">s</button><button id=\"hide\" popovertarget=\"p\" popovertargetaction=\"hide\" style=\"left:100px;top:40px\">h</button><button id=\"cmodal\" commandfor=\"m\" command=\"show-modal\" style=\"left:0;top:80px\">o</button><button id=\"cclose\" commandfor=\"m\" command=\"close\" value=\"bycmd\" style=\"left:50px;top:80px\">c</button><button id=\"creq\" commandfor=\"m\" command=\"request-close\" value=\"req\" style=\"left:100px;top:80px\">r</button><button id=\"ctoggle\" commandfor=\"p\" command=\"toggle-popover\" style=\"left:150px;top:80px\">tp</button><button id=\"ccustom\" commandfor=\"plain\" command=\"--spin\" style=\"left:0;top:120px\">x</button><button id=\"cbad\" commandfor=\"plain\" command=\"bogus\" style=\"left:50px;top:120px\">b</button>"

let private eventTypes = [ "beforetoggle"; "toggle"; "cancel"; "close"; "command" ]

// The settle rounds a marker toggle may take before the fixture reports its tasks as undelivered.
[<Literal>]
let private SettleLimit = 10000

/// <summary>
/// Checks dialogs, popovers, popover and command invokers, trusted close requests and light dismiss, top-layer hit
/// testing and modal focus, and constructed ToggleEvents and CommandEvents on one provider; the result is the
/// assertion count.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>InvalidStateError</c>.</param>
let run (document: Document) (create: unit -> ToggleEventService) (driver: Driver) (errorName: exn -> string) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let fails label (expected: string) (action: unit -> unit) =
            checks <- checks + 1

            let name =
                try
                    action ()
                    None
                with error ->
                    Some (errorName error)

            if name <> Some expected then
                failwith $"{label}: expected {expected}, got %A{name}"

        let container = document.createElement "div"
        container.setAttribute ("id", "d16")
        document.body.appendChild container |> ignore
        container.innerHTML <- markup

        let element (id: string) =
            document.getElementById id

        let html (id: string) =
            unbox<HTMLElement>(element id)

        let dialog (id: string) =
            unbox<HTMLDialogElement>(element id)

        let button (id: string) =
            unbox<HTMLButtonElement>(element id)

        let label (value: Element) =
            if isNull value then "-" else value.id

        let isOpen (id: string) =
            (element id).matches ":popover-open"

        // A manual popover outside the viewport. Its toggle task runs after every task queued before it.
        let marker = unbox<HTMLElement>(document.createElement "div")
        marker.setAttribute ("id", "d16-marker")
        marker.setAttribute ("style", "position:fixed;inset:auto;left:-100px;top:-100px;width:1px;height:1px")
        marker.popover <- "manual"
        container.appendChild marker |> ignore
        let mutable markerToggles = 0

        let log = List<string>()
        let prevent = HashSet<string>()

        let describe (event: Event) =
            let target = label (unbox<Element> event.target)
            let flag = if event.cancelable then "c" else ""

            match event.``type`` with
            | "beforetoggle"
            | "toggle" ->
                let toggle = unbox<ToggleEvent> event
                $"{event.``type``}{flag}:{target}[{toggle.oldState}>{toggle.newState} {label toggle.source}]"
            | "command" ->
                let command = unbox<CommandEvent> event
                $"command{flag}:{target}[{command.command} {label command.source}]"
            | kind -> $"{kind}{flag}:{target}"

        // The state each element's last toggle reported; every toggle's oldState must continue it.
        let states = Dictionary<string, string>()
        let broken = List<string>()

        let record (event: Event) =
            let line = describe event

            if event.``type`` = "toggle" then
                let toggle = unbox<ToggleEvent> event
                let target = label (unbox<Element> event.target)

                let current =
                    match states.TryGetValue target with
                    | true, value -> value
                    | _ -> "closed"

                if toggle.oldState <> current then
                    broken.Add line

                states[target] <- toggle.newState

            if prevent.Remove event.``type`` then
                event.preventDefault ()
                log.Add (line + " X")
            else
                log.Add line

        let listener: EventListener =
            fun event ->
                if label (unbox<Element> event.target) <> "d16-marker" then
                    record event
                elif event.``type`` = "toggle" then
                    markerToggles <- markerToggles + 1

        for kind in eventTypes do
            document.addEventListener (kind, listener, true)

        let drain () =
            let items = List.ofSeq log
            log.Clear ()
            items

        // Settles until every task queued before the call has run.
        let settle () =
            async {
                let target = markerToggles + 1

                if marker.matches ":popover-open" then
                    marker.hidePopover ()
                else
                    marker.showPopover ()

                let mutable rounds = 0

                while markerToggles < target do
                    if rounds = SettleLimit then
                        failwith $"Queued tasks undelivered after {SettleLimit} settle rounds"

                    rounds <- rounds + 1
                    do! driver.Settle ()
            }

        // Events dispatched during a call, in order; queued toggle and close events, as a set after Settle.

        let isQueued (line: string) =
            line.StartsWith "toggle"
            || line.StartsWith "close"

        let dispatchedNow label (expected: string list) =
            let items = List.ofSeq log

            log.RemoveAll (fun line -> not (isQueued line))
            |> ignore

            equal label expected (items |> List.filter (isQueued >> not))

        // A queued line's target and, for a toggle, its old and new states.
        let parse (line: string) =
            let colon = line.IndexOf ':'
            let bracket = line.IndexOf '['

            if bracket < 0 then
                line.Substring (colon + 1), None
            else
                let pair = line.Substring(bracket + 1, line.IndexOf ' ' - bracket - 1).Split '>'
                line.Substring (colon + 1, bracket - colon - 1), Some (pair[0], pair[1])

        // Per element: the close count and the last toggle's newState. A pending toggle task absorbs the next toggle,
        // so the toggle count is engine timing and is not compared.
        let summary (lines: string list) =
            lines
            |> List.map parse
            |> List.groupBy fst
            |> List.map (fun (target, items) ->
                let closes =
                    items
                    |> List.filter (snd >> Option.isNone)
                    |> List.length

                let last =
                    items
                    |> List.choose snd
                    |> List.tryLast
                    |> Option.map snd

                let last = last |> Option.defaultValue "-"
                $"{target} closes={closes} last={last}")
            |> List.sort

        let queued label (expected: string list) =
            async {
                do! settle ()
                let actual = drain () |> List.filter isQueued
                equal label (summary expected) (summary actual)
            }

        do! settle ()

        // Dialog show, close and returnValue.
        let d = dialog "d"
        equal "dialog initially closed" false d.``open``
        d.show ()
        equal "show opens" true d.``open``
        equal "show reflects open" true (d.hasAttribute "open")
        equal "show is not modal" false (d.matches ":modal")
        dispatchedNow "show beforetoggle" [ "beforetogglec:d[closed>open -]" ]
        do! queued "show toggle" [ "toggle:d[closed>open -]" ]
        d.show ()
        fails "showModal on an open dialog" "InvalidStateError" (fun () -> d.showModal ())
        d.close "first"
        equal "close sets returnValue" "first" d.returnValue
        dispatchedNow "close beforetoggle" [ "beforetoggle:d[open>closed -]" ]
        do! queued "close toggle and close" [ "toggle:d[open>closed -]"; "close:d" ]
        d.close "second"
        equal "close of a closed dialog keeps returnValue" "first" d.returnValue
        d.returnValue <- "manual"
        equal "returnValue setter" "manual" d.returnValue
        do! queued "close of a closed dialog fires nothing" []

        // Modal dialog: top layer, inertness, autofocus and focus restore.
        let m = dialog "m"
        (html "before").focus()
        equal "focus before" "before" (label document.activeElement)
        m.showModal ()
        equal "showModal is modal" true (m.matches ":modal")
        equal "modal autofocus" "mauto" (label document.activeElement)
        dispatchedNow "showModal beforetoggle" [ "beforetogglec:m[closed>open -]" ]
        do! queued "showModal toggle" [ "toggle:m[closed>open -]" ]
        equal "modal hit inside" "m" (label (document.elementFromPoint (400.0, 250.0)))
        equal "modal hit outside is the dialog" "m" (label (document.elementFromPoint (20.0, 10.0)))
        (html "before").focus()
        equal "inert content takes no focus" "mauto" (label document.activeElement)
        fails "show on an open modal" "InvalidStateError" (fun () -> m.show ())
        m.close ()
        equal "close leaves the top layer" false (m.matches ":modal")
        equal "focus restores" "before" (label document.activeElement)
        dispatchedNow "modal close beforetoggle" [ "beforetoggle:m[open>closed -]" ]
        do! queued "modal close events" [ "toggle:m[open>closed -]"; "close:m" ]
        do! settle ()
        equal "hit after close" "before" (label (document.elementFromPoint (20.0, 10.0)))

        // requestClose and a cancelled cancel.
        m.showModal ()
        do! queued "requestClose setup" [ "toggle:m[closed>open -]" ]
        drain () |> ignore
        m.requestClose "r1"
        equal "requestClose closes" false m.``open``
        equal "requestClose returnValue" "r1" m.returnValue
        dispatchedNow "requestClose events" [ "cancelc:m"; "beforetoggle:m[open>closed -]" ]
        do! queued "requestClose close" [ "toggle:m[open>closed -]"; "close:m" ]
        m.showModal ()
        do! queued "second showModal" [ "toggle:m[closed>open -]" ]
        prevent.Add "cancel" |> ignore
        m.requestClose "r2"
        equal "a cancelled requestClose keeps the dialog open" true m.``open``
        equal "a cancelled requestClose keeps returnValue" "r1" m.returnValue
        dispatchedNow "cancelled requestClose" [ "cancelc:m X" ]
        m.close ()
        do! queued "close after cancel" [ "toggle:m[open>closed -]"; "close:m" ]
        drain () |> ignore

        // closedBy reflection and trusted close requests after equal user activation.
        equal "closedBy of a closed dialog" "none" d.closedBy
        equal "closedBy any" "any" (dialog "cb").closedBy
        equal "closedBy none" "none" (dialog "cn").closedBy
        do! driver.Click 780 590
        m.showModal ()
        equal "closedBy of a modal" "closerequest" m.closedBy
        do! queued "activation showModal" [ "toggle:m[closed>open -]" ]
        drain () |> ignore
        do! driver.Press "Escape"
        equal "Escape closes the modal" false m.``open``
        dispatchedNow "Escape after activation" [ "cancelc:m"; "beforetoggle:m[open>closed -]" ]
        do! queued "Escape close" [ "toggle:m[open>closed -]"; "close:m" ]
        let none = dialog "cn"
        none.showModal ()
        do! queued "closedby none open" [ "toggle:cn[closed>open -]" ]
        drain () |> ignore
        do! driver.Press "Escape"
        equal "closedby none ignores Escape" true none.``open``
        dispatchedNow "closedby none fires nothing" []
        none.close ()
        do! queued "closedby none close" [ "toggle:cn[open>closed -]"; "close:cn" ]
        drain () |> ignore
        let any = dialog "cb"
        any.show ()
        do! queued "closedby any open" [ "toggle:cb[closed>open -]" ]
        drain () |> ignore
        do! driver.Click 700 100
        equal "light dismiss closes closedby any" false any.``open``
        dispatchedNow "light dismiss of a dialog" [ "cancelc:cb"; "beforetoggle:cb[open>closed -]" ]
        do! queued "light dismiss close" [ "toggle:cb[open>closed -]"; "close:cb" ]
        m.closedBy <- "any"
        equal "closedBy setter" "any" m.closedBy
        m.closedBy <- "bogus"
        equal "invalid closedBy" "none" m.closedBy

        // form method=dialog.
        d.show ()
        do! queued "form dialog open" [ "toggle:d[closed>open -]" ]
        drain () |> ignore
        (html "dsubmit").click()
        equal "form method dialog closes" false d.``open``
        equal "form method dialog returnValue" "ok" d.returnValue
        dispatchedNow "form dialog beforetoggle" [ "beforetoggle:d[open>closed -]" ]
        do! queued "form dialog close" [ "toggle:d[open>closed -]"; "close:d" ]

        // Popover attribute and methods.
        let p = html "p"
        equal "popover auto" "auto" p.popover
        equal "popover manual" "manual" (html "pm").popover
        equal "popover hint" "hint" (html "ph").popover
        equal "no popover" null (html "plain").popover
        p.showPopover ()
        equal "showPopover opens" true (isOpen "p")
        dispatchedNow "showPopover beforetoggle" [ "beforetogglec:p[closed>open -]" ]
        do! queued "showPopover toggle" [ "toggle:p[closed>open -]" ]
        p.showPopover ()
        equal "togglePopover closes" false (p.togglePopover ())
        equal "togglePopover force opens" true (p.togglePopover true)
        equal "togglePopover force keeps open" true (p.togglePopover true)
        p.hidePopover ()
        p.hidePopover ()
        equal "hidePopover closes" false (isOpen "p")
        do! settle ()
        drain () |> ignore
        fails "showPopover without popover" "NotSupportedError" (fun () -> (html "plain").showPopover())
        fails "hidePopover without popover" "NotSupportedError" (fun () -> (html "plain").hidePopover())
        fails "togglePopover without popover" "NotSupportedError" (fun () -> (html "plain").togglePopover() |> ignore)
        p.showPopover (ShowPopoverOptions (source = Some (html "tog")))
        dispatchedNow "showPopover source" [ "beforetogglec:p[closed>open tog]" ]
        do! queued "showPopover source toggle" [ "toggle:p[closed>open tog]" ]
        p.hidePopover ()
        do! settle ()
        drain () |> ignore
        let plain = html "plain"
        plain.popover <- "manual"
        equal "popover setter" "manual" plain.popover
        plain.popover <- "bogus"
        equal "invalid popover is manual" "manual" plain.popover
        plain.removeAttribute "popover"
        prevent.Add "beforetoggle" |> ignore
        p.showPopover ()
        equal "a cancelled beforetoggle keeps the popover closed" false (isOpen "p")
        dispatchedNow "cancelled beforetoggle" [ "beforetogglec:p[closed>open -] X" ]

        // Auto, manual and hint stacking, light dismiss and Escape.
        p.showPopover ()
        (html "p2").showPopover()
        equal "a second auto popover closes the first" false (isOpen "p")
        (html "pm").showPopover()
        do! settle ()
        drain () |> ignore
        equal "popover top layer hit" "p2" (label (document.elementFromPoint (650.0, 520.0)))
        do! driver.Click 400 300
        equal "light dismiss closes the auto popover" false (isOpen "p2")
        equal "light dismiss keeps the manual popover" true (isOpen "pm")
        p.showPopover ()
        do! settle ()
        drain () |> ignore
        do! driver.Press "Escape"
        equal "Escape closes the auto popover" false (isOpen "p")
        equal "Escape keeps the manual popover" true (isOpen "pm")
        (html "pm").hidePopover()
        p.showPopover ()
        (html "ph").showPopover()
        equal "a hint keeps the auto popover" true (isOpen "p")
        (html "ph2").showPopover()
        equal "a hint closes another hint" false (isOpen "ph")
        (html "p2").showPopover()
        equal "an auto popover closes the hint" false (isOpen "ph2")
        equal "an auto popover closes its sibling" false (isOpen "p")
        (html "ph").showPopover()
        do! settle ()
        drain () |> ignore
        do! driver.Click 400 300
        equal "light dismiss closes the hint" false (isOpen "ph")
        equal "light dismiss closes the auto popover alongside" false (isOpen "p2")

        let dismissOrder =
            match driver.Backend with
            | Native -> [ "beforetoggle:p2[open>closed -]"; "beforetoggle:ph[open>closed -]" ]
            | Chromium -> [ "beforetoggle:ph[open>closed -]"; "beforetoggle:p2[open>closed -]" ]

        dispatchedNow "light dismiss order (qualified hint-light-dismiss-order)" dismissOrder
        p.showPopover ()
        (html "ph").showPopover()
        do! settle ()
        drain () |> ignore
        do! driver.Press "Escape"
        equal "Escape closes the hint first" (false, true) (isOpen "ph", isOpen "p")
        do! driver.Press "Escape"
        equal "a second Escape closes the auto popover" false (isOpen "p")
        do! settle ()
        drain () |> ignore

        // popovertarget invokers.
        let toggle = button "tog"
        equal "popoverTargetElement" "p" (label toggle.popoverTargetElement)
        equal "popoverTargetAction default" "toggle" toggle.popoverTargetAction
        equal "popoverTargetAction show" "show" (button "show").popoverTargetAction
        toggle.click ()
        equal "popovertarget opens" true (isOpen "p")
        dispatchedNow "popovertarget source" [ "beforetogglec:p[closed>open tog]" ]
        do! queued "popovertarget toggle" [ "toggle:p[closed>open tog]" ]
        toggle.click ()
        equal "popovertarget toggles closed" false (isOpen "p")
        (button "show").click()
        (button "show").click()
        equal "popovertargetaction show" true (isOpen "p")
        (button "hide").click()
        equal "popovertargetaction hide" false (isOpen "p")
        do! settle ()
        drain () |> ignore
        do! driver.Click 20 50
        equal "a trusted click on an invoker opens" true (isOpen "p")
        dispatchedNow "trusted invoker" [ "beforetogglec:p[closed>open tog]" ]
        toggle.popoverTargetAction <- "bogus"
        equal "invalid popoverTargetAction" "toggle" toggle.popoverTargetAction
        toggle.popoverTargetElement <- element "pm"
        equal "popoverTargetElement setter" "pm" (label toggle.popoverTargetElement)
        toggle.popoverTargetElement <- null
        equal "popoverTargetElement cleared" "-" (label toggle.popoverTargetElement)
        p.hidePopover ()
        do! settle ()
        drain () |> ignore

        // Command invokers.
        let modal = button "cmodal"
        equal "command" "show-modal" modal.command
        equal "commandForElement" "m" (label modal.commandForElement)
        equal "unknown command reflects empty" "" (button "cbad").command
        equal "custom command" "--spin" (button "ccustom").command
        modal.click ()
        equal "show-modal command" true (m.matches ":modal")
        dispatchedNow "show-modal command events" [ "commandc:m[show-modal cmodal]"; "beforetogglec:m[closed>open cmodal]" ]
        do! queued "show-modal command toggle" [ "toggle:m[closed>open cmodal]" ]
        (button "cclose").click()
        equal "close command" false m.``open``
        equal "close command returnValue" "bycmd" m.returnValue
        dispatchedNow "close command events" [ "commandc:m[close cclose]"; "beforetoggle:m[open>closed cclose]" ]
        do! queued "close command queued" [ "toggle:m[open>closed cclose]"; "close:m" ]
        modal.click ()
        do! settle ()
        drain () |> ignore
        (button "creq").click()
        equal "request-close command" false m.``open``
        equal "request-close returnValue" "req" m.returnValue

        dispatchedNow
            "request-close events"
            [
                "commandc:m[request-close creq]"
                "cancelc:m"
                "beforetoggle:m[open>closed creq]"
            ]

        do! queued "request-close queued" [ "toggle:m[open>closed creq]"; "close:m" ]
        (button "ctoggle").click()
        equal "toggle-popover command" true (isOpen "p")
        dispatchedNow "toggle-popover events" [ "commandc:p[toggle-popover ctoggle]"; "beforetogglec:p[closed>open ctoggle]" ]
        p.hidePopover ()
        do! settle ()
        drain () |> ignore
        (button "ccustom").click()
        dispatchedNow "custom command" [ "commandc:plain[--spin ccustom]" ]
        (button "cbad").click()
        dispatchedNow "unknown command fires nothing" []
        prevent.Add "command" |> ignore
        modal.click ()
        equal "a cancelled command runs nothing" false m.``open``
        dispatchedNow "cancelled command" [ "commandc:m[show-modal cmodal] X" ]
        (button "ccustom").command <- "close"
        equal "command setter" "close" (button "ccustom").command
        (button "ccustom").commandForElement <- element "d"
        equal "commandForElement setter" "d" (label (button "ccustom").commandForElement)

        // Constructed ToggleEvents and CommandEvents.
        use service = create ()

        let built =
            service.CreateToggleEvent ("toggle", EventOptions.Default, ToggleFields.Default)

        equal
            "default ToggleEvent"
            ("toggle", "", "", "-", false, false)
            (built.``type``, built.oldState, built.newState, label built.source, built.cancelable, built.isTrusted)

        let fields =
            {
                OldState = "closed"
                NewState = "open"
                Source = element "tog"
            }

        let before =
            service.CreateToggleEvent (
                "beforetoggle",
                { EventOptions.Default with
                    Cancelable = true
                },
                fields
            )

        equal "ToggleEvent fields" ("closed", "open", "tog", true) (before.oldState, before.newState, label before.source, before.cancelable)

        let command =
            service.CreateCommandEvent ("command", EventOptions.Default, CommandFields.Default)

        equal "default CommandEvent" ("command", "", "-", false) (command.``type``, command.command, label command.source, command.isTrusted)

        let spin =
            service.CreateCommandEvent (
                "command",
                {
                    Bubbles = true
                    Cancelable = true
                    Composed = false
                },
                {
                    Command = "show-modal"
                    Source = element "cmodal"
                }
            )

        equal "CommandEvent fields" ("show-modal", "cmodal", true, true) (spin.command, label spin.source, spin.bubbles, spin.cancelable)
        equal "dispatching a CommandEvent" true ((element "m").dispatchEvent spin)
        equal "a dispatched CommandEvent runs no command" false m.``open``
        equal "dispatching a ToggleEvent" true (p.dispatchEvent before)
        equal "a dispatched ToggleEvent opens nothing" false (isOpen "p")
        dispatchedNow "constructed events are untrusted" [ "commandc:m[show-modal cmodal]"; "beforetogglec:p[closed>open tog]" ]
        (service :> IDisposable).Dispose()

        let disposed =
            try
                service.CreateToggleEvent ("toggle", EventOptions.Default, ToggleFields.Default)
                |> ignore

                false
            with _ ->
                true

        equal "a disposed service raises" true disposed

        equal "every element's toggles chain oldState to the previous newState" [] (List.ofSeq broken)

        for kind in eventTypes do
            document.removeEventListener (kind, listener, true)

        container.remove ()
        return checks
    }
