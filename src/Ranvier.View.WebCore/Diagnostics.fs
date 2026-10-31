namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>The native diagnostics of a host's document.</summary>
module Diagnostics =
    [<Literal>]
    let private Unsupported = -9

    [<Literal>]
    let private BufferTooSmall = -6

    let private copy (name: string) (call: byte array -> int -> int array -> int) =
        let length = [| 0 |]
        let status = call null 0 length

        if status <> 0 && status <> BufferTooSmall then
            Interop.check name status

        let buffer = Array.zeroCreate<byte> length[0]

        call buffer buffer.Length length
        |> Interop.check name

        Text.Encoding.UTF8.GetString (buffer, 0, length[0])

    // Copies the snapshot's values and releases it, so no snapshot outlives the call.
    let private snapshot (name: string) (take: nativeint array -> int) (read: nativeint -> 'T) =
        let output = [| 0n |]

        match take output with
        | 0 ->
            try
                Ok (read output[0])
            finally
                Native.rv_diagnostic_release output[0]
        | Unsupported -> Error DiagnosticsUnavailable.NotRendered
        | status ->
            Interop.check name status
            Error DiagnosticsUnavailable.NotRendered

    let private rules (diagnostic: nativeint) =
        Array.init (Native.rv_diagnostic_count diagnostic) (fun index ->
            let number field =
                let mutable value = 0.0

                Native.rv_diagnostic_entry_number (diagnostic, index, field, &value)
                |> Interop.check "rv_diagnostic_entry_number"

                int value

            let text field =
                copy "rv_diagnostic_entry_string" (fun buffer capacity length ->
                    Native.rv_diagnostic_entry_string (diagnostic, index, field, buffer, capacity, length))

            {
                Origin = enum<CascadeOrigin>(number 0)
                SelectorText = text 0
                MatchedSelector = text 1
                Specificity =
                    {
                        A = number 1
                        B = number 2
                        C = number 3
                    }
                SelectorCount = number 4
                MatchedSelectorCount = number 5
                Declarations = text 2
            })

    let private declarations (diagnostic: nativeint) =
        Array.init (Native.rv_diagnostic_count diagnostic) (fun index ->
            let number field =
                let mutable value = 0.0

                Native.rv_diagnostic_entry_number (diagnostic, index, field, &value)
                |> Interop.check "rv_diagnostic_entry_number"

                int value

            let text field =
                copy "rv_diagnostic_entry_string" (fun buffer capacity length ->
                    Native.rv_diagnostic_entry_string (diagnostic, index, field, buffer, capacity, length))

            let layerOrder = number 5
            let sheet = number 8
            let shorthand = text 4
            let conditions = text 6

            {
                Origin = enum<CascadeOrigin>(number 0)
                SelectorText = text 0
                MatchedSelector = text 1
                Specificity =
                    {
                        A = number 1
                        B = number 2
                        C = number 3
                    }
                Important = number 4 = 1
                Layer =
                    if layerOrder < 0 then
                        None
                    else
                        Some { Name = text 5; Order = layerOrder }
                Conditions =
                    if conditions.Length = 0 then
                        [||]
                    else
                        conditions.Split '\n'
                Property = text 2
                Value = text 3
                Shorthand = if shorthand.Length = 0 then None else Some shorthand
                HasVariableReference = number 11 = 1
                Depth = number 7
                Position = if sheet < 0 then None else Some { Sheet = sheet; Rule = number 9 }
                DeclarationIndex = number 10
                IsWinner = number 6 = 1
            })

    let private cascade (diagnostic: nativeint) =
        {
            Property =
                copy "rv_diagnostic_text" (fun buffer capacity length ->
                    Native.rv_diagnostic_text (diagnostic, buffer, capacity, length))
            Declarations = declarations diagnostic
        }

    let private rect (values: float array) offset : BoxRect =
        {
            X = values[offset]
            Y = values[offset + 1]
            Width = values[offset + 2]
            Height = values[offset + 3]
        }

    /// <summary>A diagnostics service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>
    /// Enabling the service sets the host's native opt-in (<c>rv_host_set_diagnostics</c>), and disabling or disposing
    /// it clears the opt-in; a second service of the same host shares the opt-in. Each read takes a native snapshot,
    /// copies it and releases it before returning. An element with no box the read can describe, or of another
    /// document, reads <c>NotRendered</c>. The developer-tools frontend follows the host's <c>inspector-frontend</c>
    /// capability. A cascade read of an unknown or shorthand property of an element of the document reads
    /// <c>UnsupportedProperty</c>, and the cascade is <c>Unsupported</c> when the native library lacks
    /// <c>cascade-diagnostics</c>. Raises NotSupportedException when the native library lacks the diagnostics functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : DiagnosticsService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "diagnostics") then
            raise (NotSupportedException "The native library has no diagnostics functions")

        let mutable frontend = 0
        let cascadeSupported = WebCoreHost.Has "cascade-diagnostics"

        let noCascade =
            "The native library has no cascade diagnostics."

        Native.rv_host_capability_fact (host.Handle, 4, "inspector-frontend", 1, &frontend)
        |> Interop.check "rv_host_capability_fact"

        let target (element: Element) =
            match box element with
            | :? NativeObject as value when obj.ReferenceEquals (value.Session, host.Session) -> Interop.handle value
            | _ -> invalidArg "element" "The element must belong to the host's document"

        Ranvier.Dom.Diagnostics.create
            {
                Features =
                    {
                        RenderTree = DiagnosticsAvailability.Supported
                        MatchedRules = DiagnosticsAvailability.Supported
                        BoxModel = DiagnosticsAvailability.Supported
                        DeveloperToolsFrontend =
                            if frontend = 1 then
                                DiagnosticsAvailability.Supported
                            else
                                Ranvier.Dom.Diagnostics.noFrontend
                        Cascade =
                            if cascadeSupported then
                                DiagnosticsAvailability.Supported
                            else
                                DiagnosticsAvailability.Unsupported noCascade
                    }
                SetEnabled =
                    fun enabled ->
                        if enabled then
                            host.EnsureActive ()

                            Native.rv_host_set_diagnostics (host.Handle, 1)
                            |> Interop.check "rv_host_set_diagnostics"
                        else
                            // A destroyed host has no opt-in left to clear.
                            let live =
                                try
                                    host.EnsureActive ()
                                    true
                                with :? ObjectDisposedException ->
                                    false

                            if live then
                                Native.rv_host_set_diagnostics (host.Handle, 0)
                                |> Interop.check "rv_host_set_diagnostics"
                RenderTree =
                    fun (element, options) ->
                        host.EnsureActive ()

                        let handle =
                            element
                            |> Option.map target
                            |> Option.defaultValue 0n

                        snapshot
                            "rv_host_inspect_render_tree"
                            (fun output -> Native.rv_host_inspect_render_tree (host.Handle, handle, int options, output))
                            (fun diagnostic ->
                                copy "rv_diagnostic_text" (fun buffer capacity length ->
                                    Native.rv_diagnostic_text (diagnostic, buffer, capacity, length)))
                MatchedRules =
                    fun element ->
                        host.EnsureActive ()
                        let handle = target element

                        snapshot
                            "rv_host_inspect_matched_rules"
                            (fun output -> Native.rv_host_inspect_matched_rules (host.Handle, handle, output))
                            rules
                Cascade =
                    fun (element, property) ->
                        host.EnsureActive ()
                        let handle = target element

                        if not cascadeSupported then
                            Error (DiagnosticsUnavailable.Unsupported noCascade)
                        else
                            let name = Text.Encoding.UTF8.GetBytes property
                            let output = [| 0n |]

                            match Native.rv_host_inspect_cascade (host.Handle, handle, name, name.Length, output) with
                            | 0 ->
                                try
                                    Ok (cascade output[0])
                                finally
                                    Native.rv_diagnostic_release output[0]
                            | Unsupported ->
                                // The engine reads an element of the document for a longhand it knows.
                                let probe = Text.Encoding.UTF8.GetBytes "display"
                                let known = [| 0n |]

                                match Native.rv_host_inspect_cascade (host.Handle, handle, probe, probe.Length, known) with
                                | 0 ->
                                    Native.rv_diagnostic_release known[0]
                                    Error DiagnosticsUnavailable.UnsupportedProperty
                                | _ -> Error DiagnosticsUnavailable.NotRendered
                            | -4 -> invalidArg "element" "The target is not an element"
                            | status ->
                                Interop.check "rv_host_inspect_cascade" status
                                Error DiagnosticsUnavailable.NotRendered
                BoxModel =
                    fun element ->
                        host.EnsureActive ()
                        let handle = target element
                        let values = Array.zeroCreate<float> 16

                        match Native.rv_host_inspect_box_model (host.Handle, handle, &values[0]) with
                        | 0 ->
                            Ok
                                {
                                    Content = rect values 0
                                    Padding = rect values 4
                                    Border = rect values 8
                                    Margin = rect values 12
                                }
                        | Unsupported -> Error DiagnosticsUnavailable.NotRendered
                        | -4 -> invalidArg "element" "The target is not an element"
                        | status ->
                            Interop.check "rv_host_inspect_box_model" status
                            Error DiagnosticsUnavailable.NotRendered
            }
