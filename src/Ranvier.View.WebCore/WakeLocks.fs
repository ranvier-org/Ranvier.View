namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open Ranvier.Dom
open RanvierWebCoreAbi

type internal WakeLockPolicyCallback = delegate of context: nativeint * host: nativeint * kind: int -> int

type internal SleepDisablerCallback = delegate of context: nativeint * id: uint64 * display: int * reason: nativeint * held: int -> unit

/// <summary>Keeps the display awake on Windows while WebCore holds a display sleep disabler.</summary>
module internal DisplayWake =
    [<Literal>]
    let private Continuous = 0x80000000u

    [<Literal>]
    let private DisplayRequired = 0x00000002u

    [<DllImport("kernel32.dll")>]
    extern uint32 private SetThreadExecutionState(uint32 flags)

    let private held = HashSet<uint64>()
    let mutable private installed = false

    let private callback =
        SleepDisablerCallback (fun _ id display _ isHeld ->
            if display = 1 then
                let before = held.Count

                if isHeld = 1 then
                    held.Add id |> ignore
                else
                    held.Remove id |> ignore

                if before = 0 && held.Count > 0 then
                    SetThreadExecutionState (Continuous ||| DisplayRequired)
                    |> ignore
                elif before > 0 && held.Count = 0 then
                    SetThreadExecutionState Continuous |> ignore)

    let private pointer =
        Marshal.GetFunctionPointerForDelegate<SleepDisablerCallback> callback

    /// <summary>Whether this process can keep the display awake.</summary>
    let supported = OperatingSystem.IsWindows ()

    /// <summary>Installs the process's sleep disabler callback on Windows, on the host thread; idempotent.</summary>
    let install () =
        if supported && not installed then
            Native.rv_set_sleep_disabler_callback (pointer, 0n)
            |> Interop.check "rv_set_sleep_disabler_callback"

            installed <- true

    let isHeld () =
        held.Count > 0

/// <summary>A native WakeLockSentinel and its release callbacks.</summary>
type internal NativeWakeLock(host: WebCoreHost, handle: nativeint) as this =
    let callbacks = List<int * (unit -> unit)>()
    let mutable next = 0
    let mutable closed = false

    let run =
        Native.EventCallback (fun _ _ ->
            for _, callback in callbacks.ToArray () do
                try
                    callback ()
                with error ->
                    host.Errors.Add error

            0)

    let listener =
        Native.rv_event_target_add_listener (handle, "release", 0, Marshal.GetFunctionPointerForDelegate<Native.EventCallback> run, 0n)

    do
        if listener = 0n then
            Native.rv_event_target_release handle
            raise (InvalidOperationException "rv_event_target_add_listener returned no listener")

        ignore this

    /// <summary>Removes the listener and drops the sentinel reference; idempotent.</summary>
    member _.Close() =
        if not closed then
            closed <- true
            Native.rv_listener_remove listener |> ignore
            Native.rv_event_target_release handle
            callbacks.Clear ()

    interface WakeLockSentinel with
        member _.Released =
            host.EnsureActive ()
            let mutable released = 0
            let mutable kind = 0

            Native.rv_wake_lock_state (handle, &released, &kind)
            |> Interop.check "rv_wake_lock_state"

            released = 1

        member _.Type = WakeLockType.Screen

        member _.Release() =
            host.EnsureActive ()

            Native.rv_wake_lock_release handle
            |> Interop.check "rv_wake_lock_release"

            Task.FromResult ()

        member _.OnRelease callback =
            host.EnsureActive ()

            if isNull (box callback) then
                nullArg "callback"

            let id = next
            next <- next + 1
            callbacks.Add ((id, callback))

            { new IDisposable with
                member _.Dispose() =
                    callbacks.RemoveAll (fun (candidate, _) -> candidate = id)
                    |> ignore
            }

/// <summary>Screen wake locks on native hosts, granted by an application policy.</summary>
module WakeLock =
    /// <summary>Whether a display sleep disabler is held, which on Windows keeps the display awake.</summary>
    let displayHeld () =
        DisplayWake.isHeld ()

    /// <summary>A wake lock service for <c>host</c>'s document; <c>policy</c> grants each request, and None denies all.</summary>
    /// <remarks>
    /// A host has one policy: the latest service's, until that service is disposed, which leaves the host denying.
    /// Results are delivered on a later host turn. A policy that throws denies, and the exception is reported to the
    /// host's errors. On Windows the display stays awake while any lock is held; other systems have no display wake
    /// service, which the capability provider reports.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    /// <param name="policy">The application's grant decision for each request type.</param>
    let forHost (host: WebCoreHost) (policy: (WakeLockType -> bool) option) : WakeLockService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        DisplayWake.install ()
        let locks = List<NativeWakeLock>()
        let mutable disposed = false
        let mutable hostGone = false

        let decide =
            WakeLockPolicyCallback (fun _ _ kind ->
                match policy with
                | Some grant when not (isNull (box grant)) ->
                    try
                        if grant (enum<WakeLockType> kind) then 1 else 0
                    with error ->
                        host.Errors.Add error
                        0
                | _ -> 0)

        let decidePointer =
            Marshal.GetFunctionPointerForDelegate<WakeLockPolicyCallback> decide

        Native.rv_host_set_wake_lock_policy (host.Handle, decidePointer, 0n)
        |> Interop.check "rv_host_set_wake_lock_policy"

        let close () =
            if not disposed then
                disposed <- true
                GC.KeepAlive decide

                for item in locks.ToArray () do
                    item.Close ()

                locks.Clear ()

        let service =
            { new WakeLockService with
                member _.Request(kind, signal) =
                    host.EnsureActive ()

                    if disposed then
                        raise (ObjectDisposedException (nameof WakeLockService))

                    let signal = WakeLockContract.signal signal
                    let completion = TaskCompletionSource<WakeLockSentinel>()

                    let outcome: Result<WakeLockSentinel, exn> =
                        match signal with
                        | Some aborted when aborted.Aborted -> Error (AbortedException aborted.Reason.Value)
                        | _ ->
                            let sentinel = [| 0n |]

                            match Native.rv_wake_lock_request (host.Handle, int kind, sentinel) with
                            | 0 ->
                                let item = new NativeWakeLock (host, sentinel[0])
                                locks.Add item
                                let held = item :> WakeLockSentinel
                                WakeLockContract.bind signal held |> ignore
                                Ok held
                            | status ->
                                match Interop.failure "rv_wake_lock_request" status with
                                | WebCoreDomException (name, detail) -> Error (WakeLockException (name, detail))
                                | other -> Error other

                    host.PostToOwner (fun () ->
                        match outcome with
                        | Ok held -> completion.SetResult held
                        | Error error -> completion.SetException error)

                    completion.Task

                member _.Dispose() =
                    if not disposed && not hostGone then
                        Native.rv_host_set_wake_lock_policy (host.Handle, 0n, 0n)
                        |> ignore

                    close ()
            }

        host.Disposed.Add (fun () ->
            hostGone <- true
            close ())

        service

    /// <summary>Capability facts for display wake on native hosts.</summary>
    let capabilityProvider: CapabilityProvider =
        let key = Capabilities.key CapabilityCategory.PlatformFacility "display-wake"

        {
            Id = "webcore-wake-locks"
            KnownKeys = [| key |]
            Observe =
                fun requested _ ->
                    if requested = key then
                        let observation =
                            Capabilities.unknown key "webcore-wake-locks" "SetThreadExecutionState" "process" "Unobserved"

                        let available =
                            if DisplayWake.supported then
                                {
                                    State = CapabilityState.Yes
                                    Reason = "SetThreadExecutionState keeps the display on while a screen wake lock is held."
                                }
                            else
                                {
                                    State = CapabilityState.No
                                    Reason = "The slim embed has no display wake service on this operating system."
                                }

                        Task.FromResult (
                            Some
                                { observation with
                                    Implemented = available
                                    Available = available
                                }
                        )
                    else
                        Task.FromResult None
        }
