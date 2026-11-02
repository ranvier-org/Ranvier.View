module Ranvier.View.WebCore.Tests.CleanupFaultChecks

#nowarn "9"

open System
open System.Diagnostics
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open RanvierWebCoreAbi

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private failCleanup (error: exn) =
    raise error

let private check condition message =
    if not condition then
        failwith message

let private failure action =
    try
        action ()
        null
    with error ->
        error

let runHost () =
    let host = WebCoreHost.Create (80, 60)
    let first = InvalidOperationException "first installation"
    let second = InvalidOperationException "second installation"
    let mutable destroys = 0
    let mutable later = 0

    host.SetNativeDestroy (fun handle ->
        Native.rv_host_destroy handle
        destroys <- destroys + 1)

    host.AddCleanupInstallation
        { new IDisposable with
            member _.Dispose() =
                later <- later + 1
                failCleanup second
        }

    host.AddCleanupInstallation
        { new IDisposable with
            member _.Dispose() =
                failCleanup first
        }

    let mutable rejected: exn = null

    let requester =
        Thread (ThreadStart (fun () -> rejected <- failure (fun () -> (host :> IDisposable).Dispose())))

    requester.Start ()
    check (requester.Join 5000) "Wrong-thread host disposal must terminate"
    check (rejected :? InvalidOperationException) "Wrong-thread host disposal must reject"
    check (destroys = 0 && not host.Session.IsDisposed) "Rejection must preserve host ownership"

    let actual = failure (fun () -> (host :> IDisposable).Dispose())
    check (obj.ReferenceEquals (actual, first)) "Host must preserve its first installation exception"
    check (actual.StackTrace.Contains "failCleanup") "Host must preserve the original throwing stack"

    check
        (later = 1
         && host.Session.IsDisposed
         && destroys = 1)
        "Later cleanup and actual native destroy must finish once"

    (host :> IDisposable).Dispose()
    check (destroys = 1) "Repeated host disposal must not call native destroy again"

    Console.WriteLine (
        "CLEANUP_HOST nativeDestroy="
        + destroys.ToString ()
        + " laterInstallation="
        + later.ToString ()
    )

    7

let runWindow () =
    let phases = [| "accessibility"; "display"; "graph"; "presenter"; "host" |]

    for failed in phases do
        let window =
            Window.Create ("Cleanup phase control", 80, 60, hidden = true, renderer = "software")

        let host = window.Host
        let id = SDL3.SDL_GetWindowID (NativePtr.ofNativeInt window.SdlWindow)
        let expected = InvalidOperationException failed
        let reached = ResizeArray<string>()
        let mutable destroys = 0

        host.SetNativeDestroy (fun handle ->
            Native.rv_host_destroy handle
            destroys <- destroys + 1)

        window.SetCleanupCompleted (fun phase ->
            reached.Add phase

            if phase = failed then
                failCleanup expected)

        try
            let actual = failure (fun () -> (window :> IDisposable).Dispose())
            check (obj.ReferenceEquals (actual, expected)) "Window must preserve its first cleanup exception"
            check (actual.StackTrace.Contains "failCleanup") "Window must preserve the throwing stack"
            check (reached.ToArray () = phases) "Each real page cleanup phase must finish"
            check (host.Session.IsDisposed && destroys = 1) "Page failure must still destroy the native host exactly once"
            check (not (NativePtr.isNullPtr (SDL3.SDL_GetWindowFromID id))) "SDL ownership must remain retryable after the page exception"
            (window :> IDisposable).Dispose()
            check (NativePtr.isNullPtr (SDL3.SDL_GetWindowFromID id)) "Retry must destroy the actual SDL window"
            (window :> IDisposable).Dispose()
            check (destroys = 1) "SDL cleanup retry must not destroy the host again"

            Console.WriteLine (
                "CLEANUP_WINDOW phase="
                + failed
                + " nativeDestroy="
                + destroys.ToString ()
                + " sdlAlive=false"
            )
        finally
            window.SetCleanupCompleted ignore
            (window :> IDisposable).Dispose()

    phases.Length * 7

let runEgl () =
    if
        not (
            OperatingSystem.IsLinux ()
            && WebCoreHost.Has "presentation-egl"
        )
    then
        invalidOp "This control requires the Linux EGL presentation capability"

    let window =
        Window.Create ("EGL cleanup retry control", 81, 61, hidden = true, renderer = "opengles2")

    let host = window.Host
    let id = SDL3.SDL_GetWindowID (NativePtr.ofNativeInt window.SdlWindow)
    let mutable attempts = 0
    let mutable nativeReleases = 0
    let mutable destroys = 0
    let mutable reject = true

    host.SetNativeDestroy (fun handle ->
        Native.rv_host_destroy handle
        destroys <- destroys + 1)

    check
        (window.SetEglFrameRelease (fun frame ->
            attempts <- attempts + 1

            if reject then
                -5
            else
                let result = Native.rv_gpu_frame_release frame

                if result = 0 then
                    nativeReleases <- nativeReleases + 1

                result))
        "The actual SDL GLES renderer must own the EGL frame"

    try
        host.Document.body.innerHTML <- "<style>body{margin:0;background:red}</style><div style='transform:translateZ(0)'>frame</div>"
        let first = failure window.Present
        check (first :? InvalidOperationException) "Injected release failure must escape real presentation"
        let frame = window.PendingEglFrame
        check (frame <> 0n && attempts = 1 && nativeReleases = 0) "The actual acquired frame must remain owned after failure"
        let blocked = failure (fun () -> (window :> IDisposable).Dispose())
        check (blocked :? InvalidOperationException) "ReleasePending failure must block teardown"
        check (window.PendingEglFrame = frame && attempts = 2) "Failed teardown must retain the same frame for retry"
        check (destroys = 0 && not host.Session.IsDisposed) "Blocked release must retain host ownership"
        check (not (NativePtr.isNullPtr (SDL3.SDL_GetWindowFromID id))) "Blocked release must retain the actual SDL context owner"

        let mutable rejected: exn = null

        let requester =
            Thread (ThreadStart (fun () -> rejected <- failure (fun () -> (window :> IDisposable).Dispose())))

        requester.Start ()

        check
            (requester.Join 5000
             && rejected :? InvalidOperationException)
            "Wrong-thread retry must reject"

        check (attempts = 2 && window.PendingEglFrame = frame) "Wrong-thread rejection must not transfer frame ownership"

        reject <- false
        (window :> IDisposable).Dispose()

        check
            (nativeReleases = 1
             && attempts = 3
             && window.PendingEglFrame = 0n)
            "Owner retry must forward one successful real native release"

        check (destroys = 1 && host.Session.IsDisposed) "Successful release must allow exactly one native host destroy"
        check (NativePtr.isNullPtr (SDL3.SDL_GetWindowFromID id)) "Successful retry must destroy the SDL context owner"
        (window :> IDisposable).Dispose()
        check (nativeReleases = 1 && destroys = 1) "Repeated disposal must release neither owner twice"

        Console.WriteLine (
            "CLEANUP_EGL attempts="
            + attempts.ToString ()
            + " nativeRelease="
            + nativeReleases.ToString ()
            + " nativeDestroy="
            + destroys.ToString ()
            + " pending=0 sdlAlive=false"
        )
    finally
        reject <- false
        (window :> IDisposable).Dispose()

    13

let runEglIsolated () =
    let start = ProcessStartInfo (Environment.ProcessPath)
    start.UseShellExecute <- false
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true

    if IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) then
        start.ArgumentList.Add (Assembly.GetExecutingAssembly().Location)

    start.ArgumentList.Add "--cleanup-egl"
    use child = Process.Start start
    let output = child.StandardOutput.ReadToEndAsync ()
    let errors = child.StandardError.ReadToEndAsync ()

    try
        if not (child.WaitForExit 30000) then
            child.Kill true
            check (child.WaitForExit 5000) "The isolated EGL process must terminate after cancellation"
            failwith "The isolated EGL cleanup control timed out"

        check (output.Wait 5000 && errors.Wait 5000) "The isolated EGL output must finish"

        check
            (child.ExitCode = 0)
            ("Isolated EGL cleanup failed: "
             + output.Result
             + errors.Result)

        check (output.Result.Contains "EGL_CLEANUP_CHECKS=13") "The real EGL cleanup assertions must execute"
        Console.Write output.Result
    finally
        if not child.HasExited then
            child.Kill true
            check (child.WaitForExit 5000) "The isolated EGL process must release its owner"
