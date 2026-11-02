namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore

/// <summary>The optional Linux provider's physical window coordinates and CSS-to-device scale.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal AtspiGeometry =
    val mutable Size: uint32
    val mutable Known: int
    val mutable X: int
    val mutable Y: int
    val mutable Scale: double

module private AtspiNative =
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Create = delegate of nativeint * nativeint * nativeint * int * byref<AtspiGeometry> * byref<uint64> -> int

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SetGeometry = delegate of uint64 * byref<AtspiGeometry> -> int

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Release = delegate of uint64 -> int

    type Api =
        {
            Create: Create
            SetGeometry: SetGeometry
            Release: Release
        }

    let api =
        lazy
            if not (OperatingSystem.IsLinux ()) then
                None
            else
                let mutable library = 0n

                if not (NativeLibrary.TryLoad ("libRanvierWebCoreAtspi.so", typeof<WebCoreHost>.Assembly, Nullable (), &library)) then
                    None
                else
                    try
                        Some
                            {
                                Create = Marshal.GetDelegateForFunctionPointer<Create>(NativeLibrary.GetExport (library, "rv_atspi_bridge_create"))
                                SetGeometry =
                                    Marshal.GetDelegateForFunctionPointer<SetGeometry>(
                                        NativeLibrary.GetExport (library, "rv_atspi_bridge_set_geometry")
                                    )
                                Release = Marshal.GetDelegateForFunctionPointer<Release>(NativeLibrary.GetExport (library, "rv_atspi_bridge_release"))
                            }
                    with _ ->
                        NativeLibrary.Free library
                        reraise ()

    let geometry (window: nativeptr<SDL_Window>) =
        let mutable geometry = AtspiGeometry ()
        geometry.Size <- uint32 (Marshal.SizeOf<AtspiGeometry>())
        geometry.Scale <- max 1.0e-6 (float (SDL3.SDL_GetWindowDisplayScale window))

        if SDL3.SDL_GetCurrentVideoDriver () = "x11" then
            let mutable x, y = 0, 0

            if SDLBool.op_Implicit (SDL3.SDL_GetWindowPosition (window, &&x, &&y)) then
                geometry.Known <- 1
                geometry.X <- x
                geometry.Y <- y

        geometry

/// <summary>An owner-thread AT-SPI lease registered in the host's cleanup installations.</summary>
[<Sealed>]
type internal LinuxAccessibility private (api: AtspiNative.Api, initialToken: uint64, host: WebCoreHost, window: nativeptr<SDL_Window>) =
    let mutable token = initialToken

    /// <summary>Whether the optional native provider still owns its accessibility callback.</summary>
    member _.Attached = token <> 0UL

    /// <summary>Updates physical position and scale after SDL window or display changes.</summary>
    member _.Update() =
        if token <> 0UL then
            host.EnsureActive ()
            let mutable geometry = AtspiNative.geometry window
            let status = api.SetGeometry.Invoke (token, &geometry)

            if status <> 0 then
                invalidOp (
                    "AT-SPI geometry update failed: "
                    + status.ToString ()
                )

    /// <summary>Attaches an installed provider, returning none when the module or desktop service is unavailable.</summary>
    static member TryCreate(host: WebCoreHost, window: nativeptr<SDL_Window>) =
        host.EnsureActive ()

        match AtspiNative.api.Value with
        | None -> None
        | Some api ->
            let mutable token = 0UL
            let mutable geometry = AtspiNative.geometry window
            let name = Encoding.UTF8.GetBytes (SDL3.SDL_GetWindowTitle window)
            use bytes = fixed name

            let status =
                api.Create.Invoke (host.Handle, 0n, NativePtr.toNativeInt bytes, name.Length, &geometry, &token)

            if status = -9 then
                None
            elif status <> 0 then
                invalidOp ("AT-SPI attachment failed: " + status.ToString ())
            else
                let bridge = new LinuxAccessibility (api, token, host, window)
                host.AddCleanupInstallation bridge
                Some bridge

    interface IDisposable with
        member _.Dispose() =
            if token <> 0UL then
                if not host.Session.IsOwnerThread then
                    invalidOp "AT-SPI is released on the host owner thread"

                let current = token
                let status = api.Release.Invoke current

                if status <> 0 then
                    invalidOp ("AT-SPI detachment failed: " + status.ToString ())

                token <- 0UL
