namespace Ranvier.View.WebCore

open System
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Touch geometry, pointer lock and pointer-input capabilities of a native host.</summary>
/// <remarks>
/// The app delivers touch, pen and mouse samples with <c>WebCoreHost.Pointer</c>. A page's pointer-lock request waits in
/// <c>PointerLockState.Pending</c> until the app answers it with <c>resolveLock</c>; the app hides its cursor and sends
/// relative movement while the state is <c>Locked</c>, and calls <c>loseLock</c> when its window loses the pointer.
/// </remarks>
module PointerInput =

    let private require (host: WebCoreHost) =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "pointer-input") then
            raise (NotSupportedException "The native library has no pointer-input functions")

    let private handleOf (name: string) (value: obj) =
        match value with
        | :? NativeObject as item -> item.Handle
        | _ -> invalidArg name "The object must be an object of the native backend"

    let private lockState (host: WebCoreHost) () =
        host.EnsureActive ()
        let mutable state = 0
        let element = [| 0n |]

        Native.rv_host_pointer_lock_state (host.Session.Host, &state, element)
        |> Interop.check "rv_host_pointer_lock_state"

        let target = box (host.Session.Node element[0])

        match state, target with
        | 1, (:? Element as item) -> PointerLockState.Pending item
        | 2, (:? Element as item) -> PointerLockState.Locked item
        | _ -> PointerLockState.Unlocked

    /// <summary>The provider of <c>host</c>'s current document.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let provider (host: WebCoreHost) : PointerInputProvider =
        require host

        {
            TouchGeometry =
                fun touch ->
                    host.EnsureActive ()
                    let geometry = Array.zeroCreate<byte> 32
                    // RvTouchGeometry: int32 size, float rotationAngle, double radiusX, double radiusY, float force.
                    BitConverter.TryWriteBytes (Span<byte>(geometry, 0, 4), 32)
                    |> ignore

                    let pinned =
                        Runtime.InteropServices.GCHandle.Alloc (geometry, Runtime.InteropServices.GCHandleType.Pinned)

                    try
                        Native.rv_touch_geometry (handleOf "touch" (box touch), pinned.AddrOfPinnedObject ())
                        |> Interop.check "rv_touch_geometry"
                    finally
                        pinned.Free ()

                    {
                        RotationAngle = float (BitConverter.ToSingle (geometry, 4))
                        RadiusX = BitConverter.ToDouble (geometry, 8)
                        RadiusY = BitConverter.ToDouble (geometry, 16)
                        Force = float (BitConverter.ToSingle (geometry, 24))
                    }
            RequestLock =
                fun (element, unadjustedMovement) ->
                    host.EnsureActive ()

                    match Native.rv_element_request_pointer_lock (handleOf "element" (box element), (if unadjustedMovement then 1 else 0)) with
                    | 0 -> Ok ()
                    | status ->
                        match Interop.failure "rv_element_request_pointer_lock" status with
                        | WebCoreDomException (name, _) -> Error (PointerInput.refusal name)
                        | error -> raise error
            ExitLock =
                fun () ->
                    host.EnsureActive ()

                    Native.rv_document_exit_pointer_lock (Interop.handle host.Document)
                    |> Interop.check "rv_document_exit_pointer_lock"
            LockState = lockState host
        }

    /// <summary>A pointer lock service over <c>host</c>'s current document.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let lockForHost (host: WebCoreHost) : PointerLockService =
        PointerInput.lock (provider host) host.Document

    /// <summary>Grants or denies the page's pending pointer-lock request. Returns false when none is pending.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    /// <param name="granted">Whether the app gives the page the pointer.</param>
    let resolveLock (host: WebCoreHost) (granted: bool) : bool =
        require host

        match Native.rv_host_pointer_lock_resolve (host.Session.Host, (if granted then 1 else 0)) with
        | 0 -> true
        | -9 -> false
        | status -> raise (Interop.failure "rv_host_pointer_lock_resolve" status)

    /// <summary>Ends a held lock, as the platform does on Escape or when the window loses the pointer.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let loseLock (host: WebCoreHost) =
        require host

        Native.rv_host_pointer_lock_lose host.Session.Host
        |> Interop.check "rv_host_pointer_lock_lose"

    /// <summary>The capability provider "webcore-pointer-input".</summary>
    let capabilities () : CapabilityProvider =
        let fact feature yes no =
            if WebCoreHost.Has feature then
                {
                    State = CapabilityState.Yes
                    Reason = yes
                }
            else
                {
                    State = CapabilityState.No
                    Reason = no
                }

        PointerInput.capabilities
            "webcore-pointer-input"
            "rv_host_pointer touch frames and pen samples, PointerEvent pen fields and rv_element_request_pointer_lock."
            (fact "touch-input" "The app delivers touch frames through rv_host_pointer." "The native engine is built without touch events.")
            (fact
                "pointer-input"
                "The app delivers pen pressure, tilt, twist and contact size through rv_host_pointer."
                "The native library has no pointer-input functions.")
            (fact
                "pointer-lock"
                "The app grants pointer-lock requests through rv_host_pointer_lock_resolve."
                "The native engine is built without pointer lock.")
