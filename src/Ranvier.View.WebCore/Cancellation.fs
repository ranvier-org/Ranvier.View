namespace Ranvier.View.WebCore

open System
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Cancellation signals over the host's timers, owner queue and native listener registrations.</summary>
module Cancellation =
    /// <summary>Creates a cancellation domain on <c>host</c>; host disposal stops its timeouts and token mappings.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : CancellationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        let service =
            Ranvier.Dom.Cancellation.create
                { new CancellationHost with
                    member _.Check() =
                        host.EnsureActive ()

                    member _.SetTimeout(action, milliseconds) =
                        let id = host.SetTimeout (action, milliseconds)
                        fun () -> host.ClearTimeout id

                    member _.Post action =
                        host.PostToOwner action

                    member _.Report error =
                        host.Session.ListenerErrors.Add error

                    member _.Listen(target, eventType, listener, options) =
                        match box target with
                        | :? NativeObject as native ->
                            let capture = not (isNull (box options)) && options.capture

                            Events.AddRegistered (native, eventType, listener, options)
                            |> Option.map (fun _ -> fun () -> Events.RemoveCore (native, eventType, box listener, capture))
                        | _ -> raise (ArgumentException "The event target is not a native WebCore object")
                }

        host.Disposed.Add (fun () -> service.Dispose ())
        service

    /// <summary>Capability facts for the managed AbortSignal statics on native hosts.</summary>
    let capabilityProvider: CapabilityProvider =
        let names = [| "abort"; "timeout"; "any" |]

        let keys =
            names
            |> Array.map (fun name -> Capabilities.key CapabilityCategory.PlatformFacility ("abort-signal-" + name))

        {
            Id = "webcore-cancellation"
            KnownKeys = keys
            Observe =
                fun key _ ->
                    if Array.contains key keys then
                        let observation =
                            Capabilities.unknown key "webcore-cancellation" "Managed facade" "host" "Unobserved"

                        Task.FromResult (
                            Some
                                { observation with
                                    Implemented =
                                        {
                                            State = CapabilityState.Yes
                                            Reason = "Managed signal over host timers, the owner queue and native listener removal."
                                        }
                                }
                        )
                    else
                        Task.FromResult None
        }
