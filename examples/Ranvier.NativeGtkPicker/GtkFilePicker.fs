module internal GtkFilePicker

open System
open System.Runtime.InteropServices
open Ranvier.View.WebCore

module private Gtk =
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type Response = delegate of nativeint * int * nativeint -> unit

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern int gtk_init_check(nativeint argc, nativeint argv)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint gtk_file_chooser_native_new(string title, nativeint parent, int action, string accept, string cancel)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void gtk_native_dialog_show(nativeint dialog)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void gtk_native_dialog_destroy(nativeint dialog)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint gtk_file_chooser_get_filename(nativeint chooser)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint gtk_file_filter_new()

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void gtk_file_filter_add_pattern(nativeint filter, string pattern)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void gtk_file_filter_add_mime_type(nativeint filter, string mimeType)

    [<DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void gtk_file_chooser_add_filter(nativeint chooser, nativeint filter)

    [<DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint g_signal_connect_data(nativeint instance, string signal, nativeint callback, nativeint data, nativeint destroy, int flags)

    [<DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void g_signal_handler_disconnect(nativeint instance, unativeint handler)

    [<DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void g_object_unref(nativeint value)

    [<DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern void g_free(nativeint value)

    [<DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern int g_main_context_pending(nativeint context)

    [<DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)>]
    extern int g_main_context_iteration(nativeint context, int mayBlock)

type private Active(request: FilePickerRequest, dialog: nativeint, handler: unativeint) =
    member _.Request = request
    member _.Dialog = dialog
    member _.Handler = handler

type Provider internal (pump: unit -> unit, isOpen: unit -> bool, close: unit -> unit) =
    member _.Pump() = pump ()
    member _.IsOpen = isOpen ()

    interface IDisposable with
        member _.Dispose() = close ()

type Operations(connect: nativeint -> nativeint -> unativeint, show: nativeint -> unit, complete: FilePickerRequest -> FilePickerResult -> bool) =
    member _.Connect dialog callback =
        connect dialog callback

    member _.Show dialog =
        show dialog

    member _.Complete request result =
        complete request result

let systemOperations =
    Operations (
        (fun dialog callback -> Gtk.g_signal_connect_data (dialog, "response", callback, 0n, 0n, 0)),
        Gtk.gtk_native_dialog_show,
        (fun request result -> request.Complete result)
    )

let tryInstallWith (operations: Operations) (host: WebCoreHost) =
    let mutable library = 0n

    if
        not (OperatingSystem.IsLinux ())
        || not (NativeLibrary.TryLoad ("libgtk-3.so.0", &library))
    then
        None
    else
        NativeLibrary.Free library

        if not (WebCoreHost.Has "file-picker") then
            None
        else
            let mutable active: Active option = None
            let mutable disposed = false
            let ownerThread = Environment.CurrentManagedThreadId

            let ensureOwner () =
                if Environment.CurrentManagedThreadId <> ownerThread then
                    invalidOp "The GTK picker must run on its host's owner thread"

            let destroyDialog dialog =
                try
                    Gtk.gtk_native_dialog_destroy dialog
                finally
                    Gtk.g_object_unref dialog

            let closeDialog () =
                match active with
                | Some current ->
                    active <- None

                    try
                        Gtk.g_signal_handler_disconnect (current.Dialog, current.Handler)
                    finally
                        destroyDialog current.Dialog
                | None -> ()

            let response =
                Gtk.Response (fun dialog result _ ->
                    let pending = active

                    try
                        ensureOwner ()

                        match active with
                        | Some current when current.Dialog = dialog ->
                            let selection =
                                if result = -3 then
                                    let value = Gtk.gtk_file_chooser_get_filename dialog

                                    if value = 0n then
                                        Cancelled
                                    else
                                        try
                                            let path = Marshal.PtrToStringUTF8 value

                                            if
                                                isNull path
                                                || not (IO.Path.IsPathFullyQualified path)
                                            then
                                                Cancelled
                                            else
                                                Selected [| path |]
                                        finally
                                            Gtk.g_free value
                                else
                                    Cancelled

                            closeDialog ()

                            if not disposed then
                                operations.Complete current.Request selection
                                |> ignore
                        | _ -> ()
                    with error ->
                        host.Errors.Add error

                        if Environment.CurrentManagedThreadId = ownerThread then
                            match pending with
                            | Some current when current.Dialog = dialog ->
                                try
                                    match active with
                                    | Some next when
                                        next.Dialog = dialog
                                        && next.Request.Id = current.Request.Id
                                        ->
                                        closeDialog ()
                                    | _ -> ()
                                with cleanupError ->
                                    host.Errors.Add cleanupError

                                if not disposed then
                                    try
                                        operations.Complete current.Request Cancelled
                                        |> ignore
                                    with cancelError ->
                                        host.Errors.Add cancelError
                            | _ -> ())

            let receive event =
                ensureOwner ()

                match event with
                | Invalidate id ->
                    match active with
                    | Some current when current.Request.Id = id -> closeDialog ()
                    | _ -> ()
                | Show request ->
                    let mutable unattached = 0n
                    let mutable completionAttempted = false

                    let cancel () =
                        completionAttempted <- true
                        operations.Complete request Cancelled |> ignore

                    try
                        closeDialog ()

                        if request.Multiple || request.Directories then
                            cancel ()
                        else
                            let dialog =
                                Gtk.gtk_file_chooser_native_new ("Choose a file", 0n, 0, "Open", "Cancel")

                            unattached <- dialog

                            if dialog = 0n then
                                cancel ()
                            else
                                let extensions = request.Extensions
                                let mimeTypes = request.MimeTypes

                                if extensions.Length > 0 || mimeTypes.Length > 0 then
                                    let filter = Gtk.gtk_file_filter_new ()

                                    if filter = 0n then
                                        failwith "GTK could not create a file filter"

                                    try
                                        for extension in extensions do
                                            let pattern =
                                                if extension.StartsWith "." then
                                                    "*" + extension
                                                else
                                                    "*." + extension

                                            Gtk.gtk_file_filter_add_pattern (filter, pattern)

                                        for mimeType in mimeTypes do
                                            Gtk.gtk_file_filter_add_mime_type (filter, mimeType)

                                        Gtk.gtk_file_chooser_add_filter (dialog, filter)
                                    with _ ->
                                        Gtk.g_object_unref filter
                                        reraise ()

                                let handler =
                                    operations.Connect dialog (Marshal.GetFunctionPointerForDelegate<Gtk.Response> response)

                                if handler = 0un then
                                    unattached <- 0n
                                    destroyDialog dialog
                                    cancel ()
                                else
                                    active <- Some (Active (request, dialog, handler))

                                    unattached <- 0n
                                    operations.Show dialog
                    with _ ->
                        try
                            if unattached <> 0n then
                                destroyDialog unattached
                            else
                                match active with
                                | Some current when current.Request.Id = request.Id -> closeDialog ()
                                | _ -> ()
                        with cleanupError ->
                            host.Errors.Add cleanupError

                        if not completionAttempted && not disposed then
                            try
                                cancel ()
                            with cancelError ->
                                host.Errors.Add cancelError

                        reraise ()

            let registration = FilePickers.install host receive

            try
                if Gtk.gtk_init_check (0n, 0n) = 0 then
                    registration.Dispose ()
                    None
                else
                    let pump () =
                        if not disposed then
                            ensureOwner ()

                        let mutable remaining = 32

                        while not disposed
                              && remaining > 0
                              && Gtk.g_main_context_pending 0n <> 0 do
                            Gtk.g_main_context_iteration (0n, 0) |> ignore
                            remaining <- remaining - 1

                    let close () =
                        if not disposed then
                            ensureOwner ()
                            disposed <- true
                            registration.Dispose ()
                            closeDialog ()
                            GC.KeepAlive response

                    Some (new Provider (pump, (fun () -> active.IsSome), close))
            with
            | :? DllNotFoundException
            | :? EntryPointNotFoundException ->
                registration.Dispose ()
                None
            | _ ->
                registration.Dispose ()
                reraise ()

let tryInstall host =
    tryInstallWith systemOperations host
