namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Threading

[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal OpenFileName =
    val mutable Size: uint32
    val mutable Owner: nativeint
    val mutable Instance: nativeint
    val mutable Filter: nativeint
    val mutable CustomFilter: nativeint
    val mutable MaxCustomFilter: uint32
    val mutable FilterIndex: uint32
    val mutable File: nativeint
    val mutable MaxFile: uint32
    val mutable FileTitle: nativeint
    val mutable MaxFileTitle: uint32
    val mutable InitialDir: nativeint
    val mutable Title: nativeint
    val mutable Flags: uint32
    val mutable FileOffset: uint16
    val mutable FileExtension: uint16
    val mutable DefaultExtension: nativeint
    val mutable CustomData: nativeint
    val mutable Hook: nativeint
    val mutable Template: nativeint
    val mutable Reserved: nativeint
    val mutable ReservedInt: uint32
    val mutable FlagsEx: uint32

[<UnmanagedFunctionPointer(CallingConvention.Winapi)>]
type internal OpenFileHook = delegate of nativeint * uint32 * nativeint * nativeint -> nativeint

module internal WindowsFileDialog =
    [<DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)>]
    extern int openFile(OpenFileName& options)

    [<DllImport("comdlg32.dll")>]
    extern uint32 CommDlgExtendedError()

    [<DllImport("user32.dll")>]
    extern nativeint GetParent(nativeint window)

    [<DllImport("user32.dll")>]
    extern int PostMessageW(nativeint window, uint32 message, nativeint wParam, nativeint lParam)

    [<DllImport("user32.dll")>]
    extern int IsWindow(nativeint window)

    [<DllImport("user32.dll")>]
    extern uint32 GetWindowThreadProcessId(nativeint window, uint32& processId)

    type State() =
        let gate = obj ()
        let mutable window = 0n
        let mutable cancelled = false

        member _.SetWindow(value) =
            lock gate (fun () ->
                window <- value

                if cancelled && window <> 0n then
                    PostMessageW (window, 0x0111u, 2n, 0n) |> ignore)

        member _.Cancel() =
            lock gate (fun () ->
                cancelled <- true

                if window <> 0n then
                    PostMessageW (window, 0x0111u, 2n, 0n) |> ignore)

        member _.Finish() =
            lock gate (fun () ->
                window <- 0n
                cancelled)

    let paths (buffer: nativeint) maximum =
        let parts = ResizeArray<string> ()
        let mutable offset = 0
        let mutable finished = false

        while not finished && offset < maximum do
            let part = Marshal.PtrToStringUni (buffer + nativeint (offset * 2))

            if String.IsNullOrEmpty part then
                finished <- true
            else
                parts.Add part
                offset <- offset + part.Length + 1

        if not finished || parts.Count = 0 then
            raise (InvalidOperationException "The Windows file dialog returned an incomplete selection")
        elif parts.Count = 1 then
            [| parts[0] |]
        else
            parts
            |> Seq.skip 1
            |> Seq.map (fun name -> Path.Combine (parts[0], name))
            |> Seq.toArray

    let filter (extensions: string array) =
        let patterns =
            extensions
            |> Array.filter (fun ext ->
                not (String.IsNullOrEmpty ext)
                && ext[0] = '.'
                && (ext
                    |> Seq.forall (fun ch ->
                        Char.IsLetterOrDigit ch
                        || ch = '.'
                        || ch = '_'
                        || ch = '-')))
            |> Array.map (fun ext -> "*" + ext)

        if patterns.Length = 0 then
            "All files\000*.*\000\000"
        else
            "Accepted files\000"
            + String.concat ";" patterns
            + "\000All files\000*.*\000\000"

    let show owner (request: FilePickerRequest) (state: State) =
        let hook =
            OpenFileHook (fun child message _ _ ->
                try
                    if message = 0x0110u then
                        state.SetWindow (GetParent child)
                    elif message = 0x0002u then
                        state.SetWindow 0n
                with _ ->
                    ()

                0n)

        let buffer = Marshal.AllocHGlobal (65536 * 2)
        let filterBuffer = Marshal.StringToHGlobalUni (filter request.Extensions)

        try
            Marshal.Copy (Array.zeroCreate<byte> (65536 * 2), 0, buffer, 65536 * 2)
            let mutable options = OpenFileName ()
            options.Size <- uint32 (Marshal.SizeOf<OpenFileName> ())
            options.Owner <- owner
            options.Filter <- filterBuffer
            options.FilterIndex <- 1u
            options.File <- buffer
            options.MaxFile <- 65536u

            options.Flags <-
                0x00080000u
                ||| 0x00000020u
                ||| 0x00001000u
                ||| 0x00000800u
                ||| 0x00000008u
                ||| (if request.Multiple then 0x00000200u else 0u)

            options.Hook <- Marshal.GetFunctionPointerForDelegate hook
            let accepted = openFile &options <> 0
            let error = if accepted then 0u else CommDlgExtendedError ()
            let cancelled = state.Finish ()
            GC.KeepAlive hook

            if cancelled then
                Cancelled
            elif accepted then
                Selected (paths buffer 65536)
            elif error = 0u then
                Cancelled
            else
                raise (
                    InvalidOperationException (
                        "Windows file dialog failed: "
                        + error.ToString (Globalization.CultureInfo.InvariantCulture)
                    )
                )
        finally
            state.Finish () |> ignore
            Marshal.FreeHGlobal filterBuffer
            Marshal.FreeHGlobal buffer

/// <summary>Provides Windows file dialogs for native file controls.</summary>
module WindowsFilePickers =
    /// <summary>Whether this process runs on Windows.</summary>
    let available = OperatingSystem.IsWindows ()

    /// <summary>Whether this provider supports directory selection.</summary>
    let supportsDirectories = false

    /// <summary>Installs the Windows file dialog provider for a host and its owning window.</summary>
    /// <remarks>The window must belong to this process. Directory requests are cancelled; MIME-only filters show all files.</remarks>
    /// <param name="host">The native host whose owner thread processes completion.</param>
    /// <param name="ownerWindow">The live Win32 window handle owning each dialog.</param>
    let install (host: WebCoreHost) (ownerWindow: nativeint) : IDisposable =
        if not available then
            raise (PlatformNotSupportedException "Windows file dialogs require Windows")

        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        let mutable processId = 0u

        if
            ownerWindow = 0n
            || WindowsFileDialog.IsWindow ownerWindow = 0
            || WindowsFileDialog.GetWindowThreadProcessId (ownerWindow, &processId) = 0u
            || processId <> uint32 Environment.ProcessId
        then
            invalidArg (nameof ownerWindow) "A live window owned by this process is required"

        let pending = Dictionary<uint64, WindowsFileDialog.State> ()
        let mutable disposed = false

        let provider event =
            match event with
            | Invalidate id ->
                match pending.TryGetValue id with
                | true, state ->
                    pending.Remove id |> ignore
                    state.Cancel ()
                | _ -> ()
            | Show request when request.Directories -> request.Complete Cancelled |> ignore
            | Show request ->
                let state = WindowsFileDialog.State ()
                pending.Add (request.Id, state)

                let thread =
                    Thread (
                        ThreadStart (fun () ->
                            let result =
                                try
                                    Ok (WindowsFileDialog.show ownerWindow request state)
                                with error ->
                                    Error error

                            host.PostToOwner (fun () ->
                                match pending.TryGetValue request.Id with
                                | true, current when
                                    obj.ReferenceEquals (current, state)
                                    && not disposed
                                    ->
                                    pending.Remove request.Id |> ignore

                                    match result with
                                    | Ok selection -> request.Complete selection |> ignore
                                    | Error error ->
                                        host.Errors.Add error
                                        request.Complete Cancelled |> ignore
                                | _ -> ()))
                    )

                try
                    thread.IsBackground <- true
                    thread.SetApartmentState ApartmentState.STA
                    thread.Start ()
                with error ->
                    pending.Remove request.Id |> ignore
                    state.Cancel ()
                    host.Errors.Add error
                    request.Complete Cancelled |> ignore

        let registration = FilePickers.install host provider

        { new IDisposable with
            member _.Dispose() =
                if not disposed then
                    registration.Dispose ()
                    disposed <- true

                    for state in pending.Values do
                        state.Cancel ()

                    pending.Clear () }
