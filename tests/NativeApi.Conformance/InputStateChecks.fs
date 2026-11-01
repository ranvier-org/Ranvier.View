module internal NativeApiConformance.InputStateChecks

open System
open System.Collections.Generic
open Ranvier.Dom

/// <summary>Host input delivered as the user's: activation of the window, pointer and keyboard input.</summary>
type Driver =
    {
        SetActive: bool -> Async<unit>
        Move: int -> int -> Async<unit>
        Press: int -> int -> Async<unit>
        Release: int -> int -> Async<unit>
        Tab: unit -> Async<unit>
    }

/// <summary>Checks window activation, modality, focus visibility, pointer capture, capabilities and disposal.</summary>
let run
    (document: Document)
    (create: unit -> InputStateService)
    (capabilities: InputStateService -> CapabilityProvider)
    (touchImplemented: CapabilityState)
    (driver: Driver)
    =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let raises label (action: unit -> unit) =
            checks <- checks + 1

            let raised =
                try
                    action ()
                    false
                with _ ->
                    true

            if not raised then
                failwith $"{label}: expected an exception"

        let place (id: string) (top: int) (focusable: bool) (height: int) =
            let element = document.createElement "div"
            element.setAttribute ("id", id)

            element.setAttribute ("style", $"position:absolute;left:0px;top:{top}px;width:100px;height:{height}px")

            if focusable then
                element.setAttribute ("tabindex", "0")

            document.body.appendChild element |> ignore
            element

        document.body.setAttribute ("style", "margin:0")
        let a = place "input-a" 0 true 40
        let b = place "input-b" 40 true 40
        let c = place "input-c" 80 false 100

        let same (expected: Element) (actual: Element) =
            obj.ReferenceEquals (expected, actual)

        let service = create ()
        let changes = List<InputChange>()
        let subscription = service.Subscribe changes.Add

        let drain () =
            let items = changes.ToArray ()
            changes.Clear ()
            items

        equal "modality before input" InputModality.Unknown (service.Current().Modality)
        equal "touch capacity" 0 (service.Current().MaxTouchPoints)
        equal "no capture" None (service.Captured 1)

        // Activation.
        do! driver.SetActive true
        drain () |> ignore
        equal "active" true (service.Current().Focused)
        do! driver.SetActive false
        equal "deactivation change" [| InputChange.Activation false |] (drain ())
        equal "inactive" false (service.Current().Focused)
        do! driver.SetActive true
        equal "activation change" [| InputChange.Activation true |] (drain ())
        equal "active again" true (service.Current().Focused)

        // Modality and focus visibility.
        do! driver.Move 50 20
        do! driver.Press 50 20
        do! driver.Release 50 20
        equal "pointer modality" [| InputChange.Modality InputModality.Pointer |] (drain ())
        equal "click focuses a" true (same a document.activeElement)
        equal "click focus is not focus-visible" false (service.Current().FocusVisible)
        do! driver.Tab ()
        equal "keyboard modality" [| InputChange.Modality InputModality.Keyboard |] (drain ())
        equal "Tab focuses b" true (same b document.activeElement)
        equal "Tab focus is focus-visible" true (service.Current().FocusVisible)
        (a :?> HTMLElement).focus()
        equal "script focus after keyboard is focus-visible" true (service.Current().FocusVisible)
        equal "script focus changes no modality" 0 (drain ()).Length

        // Pointer capture.
        do! driver.Move 50 130
        do! driver.Press 50 130
        equal "press after keyboard" [| InputChange.Modality InputModality.Pointer |] (drain ())
        c.setPointerCapture 1
        equal "capture set" true (c.hasPointerCapture 1)
        equal "capture gain waits for the next pointer event" None (service.Captured 1)
        do! driver.Move 300 230

        match drain () with
        | [| InputChange.CaptureGained (1, "mouse", target) |] -> equal "gained target" true (same c target)
        | other -> failwith $"capture gain: {other.Length} changes"

        equal "captured" true (service.Captured 1 |> Option.exists (same c))
        do! driver.Release 300 230

        match drain () with
        | [| InputChange.CaptureLost (1, "mouse", target) |] -> equal "lost target" true (same c target)
        | other -> failwith $"capture loss: {other.Length} changes"

        equal "released" None (service.Captured 1)
        raises "unknown pointer id" (fun () -> c.setPointerCapture 7)

        // Capabilities.
        let provider = capabilities service
        equal "known keys" 4 provider.KnownKeys.Length

        let observe name =
            async {
                let! value =
                    provider.Observe (Capabilities.key CapabilityCategory.PlatformFacility name) Threading.CancellationToken.None
                    |> Async.AwaitTask

                return Option.get value
            }

        let! activation = observe "input.window-activation"
        equal "activation available" CapabilityState.Yes activation.Available.State
        equal "activation value" (Some (CapabilityValue.Boolean true)) activation.Value
        let! modality = observe "input.keyboard-modality"
        equal "modality value" (Some (CapabilityValue.Text "pointer")) modality.Value
        let! touch = observe "input.touch-points"
        equal "touch implemented" touchImplemented touch.Implemented.State
        equal "touch unavailable at capacity 0" CapabilityState.No touch.Available.State
        equal "touch value" (Some (CapabilityValue.Number 0.0)) touch.Value
        let! capture = observe "input.pointer-capture"
        equal "capture available" CapabilityState.Yes capture.Available.State

        let! unrelated =
            provider.Observe (Capabilities.key CapabilityCategory.PlatformFacility "input.unrelated") Threading.CancellationToken.None
            |> Async.AwaitTask

        equal "unrelated key" None unrelated

        // Disposal.
        subscription.Dispose ()
        subscription.Dispose ()
        do! driver.Tab ()
        equal "removed subscriber receives nothing" 0 (drain ()).Length
        equal "service still observes" InputModality.Keyboard (service.Current().Modality)
        service.Subscribe changes.Add |> ignore
        service.Dispose ()
        service.Dispose ()
        do! driver.Move 50 20
        do! driver.Press 50 20
        do! driver.Release 50 20
        equal "disposed service delivers nothing" 0 (drain ()).Length
        raises "Current after disposal" (fun () -> service.Current () |> ignore)
        raises "Subscribe after disposal" (fun () -> service.Subscribe ignore |> ignore)
        let! disposed = observe "input.window-activation"
        equal "disposed service reports unknown" CapabilityState.Unknown disposed.Available.State

        for element in [ a; b; c ] do
            element.remove ()

        return checks
    }
