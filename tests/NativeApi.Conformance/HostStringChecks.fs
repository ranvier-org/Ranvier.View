module internal NativeApiConformance.HostStringChecks

open Ranvier.Dom

/// <summary>
/// Checks that event types holding U+0000 reach the engine whole through listener registration and event
/// construction, on element and standalone targets; the result is the assertion count. The expected values are
/// Chromium 156's (Ranvier.WebCore tests/fixtures/host-string-nul-oracle.json, nodeListenerType, constructEventType and
/// targetListenerType).
/// </summary>
let run (document: Document) (create: unit -> EventService) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"U+0000 event type {label}: expected %A{expected}, got %A{actual}"

    use service = create ()
    let kind = "a\u0000b"

    let fired (target: EventTarget) =
        let seen = ResizeArray<string>()
        target.addEventListener (kind, fun _ -> seen.Add "whole")
        target.addEventListener ("a", fun _ -> seen.Add "prefix")
        let event = service.Create (kind, EventConstruction.Event EventOptions.Default)
        equal "constructed type" kind event.``type``
        target.dispatchEvent event |> ignore
        [ seen.Contains "whole"; seen.Contains "prefix" ]

    let element = document.createElement "div"
    document.body.appendChild element |> ignore
    equal "element listener" [ true; false ] (fired (element :> EventTarget))
    equal "standalone target listener" [ true; false ] (fired (service.CreateTarget () :> EventTarget))
    let trailing = service.Create ("t\u0000", EventConstruction.Event EventOptions.Default)
    equal "trailing U+0000 type" "t\u0000" trailing.``type``
    checks
