namespace Ranvier.View.WebCore

open System
open System.Threading
open System.Threading.Tasks
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Passive capability observations from a live native host and its loaded producer.</summary>
[<RequireQualifiedAccess>]
module Capabilities =
    let private categories =
        [|
            CapabilityCategory.Abi
            CapabilityCategory.RendererSetting
            CapabilityCategory.ImageFormat
            CapabilityCategory.FontFormat
            CapabilityCategory.PlatformFacility
        |]

    let private category value =
        Array.findIndex ((=) value) categories

    let private read (host: WebCoreHost) (key: CapabilityKey) =
        host.EnsureActive ()
        let kind = category key.Category

        let reason =
            match key.Category with
            | CapabilityCategory.Abi -> "Loaded ABI entry surface; behavior is observed separately."
            | CapabilityCategory.RendererSetting -> "Current Page settings and host fields."
            | CapabilityCategory.ImageFormat -> "MIMETypeRegistry image/video/SVG support query; arbitrary decode/render is unverified."
            | CapabilityCategory.FontFormat -> "Current native document CSS font-format support query; actual font loading is unverified."
            | CapabilityCategory.PlatformFacility ->
                "Loaded compiled feature/stub flags or current callback wiring; external readiness and policy are unverified."

        let fact dimension =
            let mutable value = -1

            Native.rv_host_capability_fact (host.Handle, kind, key.Name, dimension, &value)
            |> Interop.check "rv_host_capability_fact"

            {
                State =
                    if value = 1 then CapabilityState.Yes
                    elif value = 0 then CapabilityState.No
                    else CapabilityState.Unknown
                Reason = reason
            }

        let mutable valueKind = 0

        Native.rv_host_capability_value_kind (host.Handle, kind, key.Name, &valueKind)
        |> Interop.check "rv_host_capability_value_kind"

        let value =
            match valueKind with
            | 0 -> None
            | 1
            | 2 ->
                let mutable number = 0.

                Native.rv_host_capability_value_number (host.Handle, kind, key.Name, &number)
                |> Interop.check "rv_host_capability_value_number"

                Some (
                    if valueKind = 1 then
                        CapabilityValue.Boolean (number = 1.)
                    else
                        CapabilityValue.Number number
                )
            | 3 ->
                Some (
                    CapabilityValue.Text (
                        Interop.text "rv_host_capability_value_text" (fun buffer capacity length ->
                            Native.rv_host_capability_value_text (host.Handle, kind, key.Name, buffer, capacity, length))
                    )
                )
            | _ -> invalidOp "Unrecognized native capability value kind."

        {
            Key = key
            ProviderId = "native-host"
            Mechanism = reason
            Scope =
                "current host / loaded ABI "
                + string (Native.rv_abi_version ())
            Exported = fact 0
            Implemented = fact 1
            Enabled = fact 2
            Permitted = fact 3
            Available = fact 4
            Value = value
        }

    /// <summary>Creates a borrowed provider; asynchronous extensions publish through the host's Cycle queue.</summary>
    /// <remarks>Call reads and registration on the creating thread and keep its loop pumping for incomplete tasks.
    /// Disposal invalidates pending results immediately and may run on any thread. Queries acquire no GPU/device resources.</remarks>
    let forHost (host: WebCoreHost) : CapabilityService =
        if isNull (box host) then
            nullArg "host"

        host.EnsureActive ()

        if Native.rv_abi_has "capabilities" <> 1 then
            invalidOp "The loaded native producer lacks H07 capability introspection."

        let ownerThread = Environment.CurrentManagedThreadId
        let service = Ranvier.Dom.Capabilities.create ()
        let stopped = new CancellationTokenSource ()

        let keys =
            categories
            |> Array.mapi (fun index domain ->
                let count = Native.rv_capability_count index

                if count < 0 then
                    Interop.check "rv_capability_count" count

                Array.init count (fun item ->
                    let name =
                        Interop.text "rv_capability_name_at" (fun buffer capacity length ->
                            Native.rv_capability_name_at (index, item, buffer, capacity, length))

                    Ranvier.Dom.Capabilities.key domain name))
            |> Array.concat

        let builtin =
            service.Register
                {
                    Id = "native-host"
                    KnownKeys = keys
                    Observe =
                        fun key token ->
                            token.ThrowIfCancellationRequested ()
                            Task.FromResult (Some (read host key))
                }

        let invalidate () =
            if not stopped.IsCancellationRequested then
                stopped.Cancel ()
                service.Dispose ()

        let hostDisposal = host.Disposed.Subscribe (fun () -> invalidate ())

        let active () =
            host.EnsureActive ()

            if stopped.IsCancellationRequested then
                raise (CapabilityContextException "CapabilityService is disposed.")

        { new CapabilityService with
            member _.KnownKeys() =
                active ()
                service.KnownKeys ()

            member _.Register provider =
                active ()
                service.Register provider

            member _.Observe(key, token) =
                let key = Ranvier.Dom.Capabilities.key key.Category key.Name

                if token.IsCancellationRequested then
                    Task.FromCanceled<CapabilityReport>(token)
                else
                    active ()
                    let pending = service.Observe (key, token)

                    let result =
                        TaskCompletionSource<CapabilityReport>(TaskCreationOptions.RunContinuationsAsynchronously)

                    let cancel = token.Register (fun () -> result.TrySetCanceled (token) |> ignore)

                    let disposal =
                        stopped.Token.Register (fun () ->
                            result.TrySetException (CapabilityContextException "CapabilityService is disposed.")
                            |> ignore)

                    let publish () =
                        try
                            active ()
                            token.ThrowIfCancellationRequested ()

                            result.TrySetResult (pending.GetAwaiter().GetResult())
                            |> ignore
                        with
                        | :? OperationCanceledException -> result.TrySetCanceled (token) |> ignore
                        | error -> result.TrySetException error |> ignore

                    pending.ContinueWith (
                        (fun (_: Task<CapabilityReport>) ->
                            if not result.Task.IsCompleted then
                                if Environment.CurrentManagedThreadId = ownerThread then
                                    publish ()
                                else
                                    host.PostToOwner (fun () ->
                                        if not result.Task.IsCompleted then
                                            publish ())),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    )
                    |> ignore

                    result.Task.ContinueWith (
                        (fun (_: Task<CapabilityReport>) ->
                            cancel.Dispose ()
                            disposal.Dispose ()),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    )
                    |> ignore

                    result.Task

            member _.Dispose() =
                invalidate ()
                builtin.Dispose ()
                hostDisposal.Dispose ()
        }
