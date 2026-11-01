namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Window activation, input modality, touch capacity and pointer capture of a native host.</summary>
module InputState =

    let private require (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "input-state") then
            raise (NotSupportedException "The native library has no window event target")

    /// <summary>The host's window as an EventTarget; the host keeps one window across document replacement.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the "input-state" functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let window (host: WebCoreHost) : EventTarget =
        require host
        let handle = Native.rv_host_window_event_target host.Session.Host

        if handle = 0n then
            raise (InvalidOperationException "rv_host_window_event_target returned NULL")

        unbox<EventTarget>(host.Session.Target handle)

    /// <summary>An input state service for <c>host</c>'s current document.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the "input-state" functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : InputStateService =
        let target = window host
        let session = host.Session

        let touchPoints () =
            host.EnsureActive ()
            let mutable value = 0

            Native.rv_host_max_touch_points (session.Host, &value)
            |> Interop.check "navigator.maxTouchPoints"

            value

        Ranvier.Dom.InputState.create target host.Document touchPoints

    /// <summary>The capability provider "webcore-input" over <c>service</c>.</summary>
    /// <remarks>Touch points report Implemented No: the native engine delivers no touch input.</remarks>
    let capabilities (service: InputStateService) : CapabilityProvider =
        Ranvier.Dom.InputState.capabilities
            "webcore-input"
            "Host window focus, blur, input and pointer capture events, Document.hasFocus, :focus-visible and rv_host_max_touch_points."
            {
                State = CapabilityState.No
                Reason = "The native engine is built without touch events and the host has no touch input path."
            }
            service
