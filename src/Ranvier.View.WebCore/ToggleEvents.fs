namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>ToggleEvents and CommandEvents of a native host.</summary>
module ToggleEvents =
    /// <summary>A toggle event service for <c>host</c>'s current document.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the ToggleEvent and CommandEvent constructors.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : ToggleEventService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "dom-constructor:ToggleEvent"
                && WebCoreHost.Has "dom-constructor:CommandEvent"
            )
        then
            raise (NotSupportedException "The native library has no ToggleEvent or CommandEvent constructors")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof ToggleEventService))

            host.EnsureActive ()

        let source (value: Element) =
            match box value with
            | null -> 0n
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle (box value)
            | _ -> invalidArg "source" "The source belongs to another provider"

        let flag value =
            if value then 1 else 0

        { new ToggleEventService with
            member _.CreateToggleEvent(eventType, options, fields) =
                check ()
                let eventType = Encoding.UTF8.GetBytes (ToggleEventContract.eventType eventType)
                use inputs = new InputScope ()
                let mutable dictionary = NativeDom.RvDomToggleEventInit ()
                dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomToggleEventInit>())
                dictionary.bubbles <- flag options.Bubbles
                dictionary.cancelable <- flag options.Cancelable
                dictionary.composed <- flag options.Composed
                dictionary.old_state <- inputs.TextInput (Option.ofObj (ToggleEventContract.text "OldState" fields.OldState))
                dictionary.new_state <- inputs.TextInput (Option.ofObj (ToggleEventContract.text "NewState" fields.NewState))
                dictionary.source <- source fields.Source
                let mutable result = 0n

                NativeDom.rv_dom_toggle_event_create (Interop.handle (box document), eventType, eventType.Length, [| dictionary |], &result)
                |> Interop.check "ToggleEvent constructor"

                unbox<ToggleEvent>(session.Event result)

            member _.CreateCommandEvent(eventType, options, fields) =
                check ()
                let eventType = Encoding.UTF8.GetBytes (ToggleEventContract.eventType eventType)
                use inputs = new InputScope ()
                let mutable dictionary = NativeDom.RvDomCommandEventInit ()
                dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomCommandEventInit>())
                dictionary.bubbles <- flag options.Bubbles
                dictionary.cancelable <- flag options.Cancelable
                dictionary.composed <- flag options.Composed
                dictionary.command <- inputs.TextInput (Option.ofObj (ToggleEventContract.text "Command" fields.Command))
                dictionary.source <- source fields.Source
                let mutable result = 0n

                NativeDom.rv_dom_command_event_create (Interop.handle (box document), eventType, eventType.Length, [| dictionary |], &result)
                |> Interop.check "CommandEvent constructor"

                unbox<CommandEvent>(session.Event result)

            member _.Dispose() =
                disposed.Value <- true
        }
