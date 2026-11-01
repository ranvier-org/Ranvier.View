namespace Ranvier.View.WebCore

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>The strings and Files an app hands to a drop or a paste, or takes from a copy.</summary>
/// <remarks>Each string is a type, such as <c>text/plain</c>, and its value. <c>Files</c> is never a string type.</remarks>
type TransferPayload =
    {
        Strings: struct (string * string) array
        Files: File array
    }

    /// <summary>No strings and no Files.</summary>
    static member Empty = { Strings = [||]; Files = [||] }

/// <summary>DataTransfer construction and the app's side of drops, clipboard payloads and range replacement.</summary>
module DataTransfers =
    // The App drop operations of rv_host_drag.
    [<Literal>]
    let private DragOver = 0

    [<Literal>]
    let private DragDrop = 2

    [<Literal>]
    let private EditCopy = 0

    [<Literal>]
    let private EditPaste = 2

    /// <summary>The callbacks a host's clipboard holds, alive while the host is.</summary>
    type private Callbacks =
        {
            Write: Native.ClipboardWrite
            Read: Native.ClipboardRead
            WriteFile: Native.ClipboardWriteFile
            Flush: unit -> unit
        }

    let private callbacks = ConditionalWeakTable<WebCoreHost, Callbacks>()

    let private native (host: WebCoreHost) (value: obj) (name: string) =
        match value with
        | :? NativeObject as target when
            obj.ReferenceEquals (target.Session, host.Session)
            && target.Handle <> 0n
            ->
            target.Handle
        | _ -> invalidArg name $"The {name} belongs to another host"

    let private file (host: WebCoreHost) (handle: nativeint) =
        NativeDom.rv_dom_blob_retain handle |> ignore

        host.Session.Object (
            handle,
            Kind.Blob,
            unbox<NativeObject>(box host.Document),
            Func<Session, nativeint, NativeObject>(fun s h -> FileObject (s, h))
        )
        |> unbox<File>

    let private payload (value: TransferPayload) =
        if
            isNull (box value)
            || isNull value.Strings
            || isNull value.Files
            || value.Files
               |> Array.exists (fun file -> isNull (box file))
        then
            nullArg "payload"

        value

    /// <summary>A DataTransfer service for <c>host</c>'s current document.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : DataTransferService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-constructor:DataTransfer") then
            raise (NotSupportedException "The native library has no DataTransfer constructor")

        let disposed = ref false

        { new DataTransferService with
            member _.Create() =
                if disposed.Value then
                    raise (ObjectDisposedException (nameof DataTransferService))

                host.EnsureActive ()
                let document = unbox<NativeObject>(box host.Document)
                let mutable result = 0n
                Interop.check "DataTransfer constructor" (NativeDom.rv_dom_data_transfer_create (document.Handle, &result))

                host.Session.Object (
                    result,
                    Kind.DataTransfer,
                    document,
                    Func<Session, nativeint, NativeObject>(fun s h ->
                        let target = DataTransferObject (s, h) :> NativeObject
                        target.BindDocumentContext document
                        target)
                )
                |> unbox<DataTransfer>

            member _.Dispose() =
                disposed.Value <- true
        }

    /// <summary>
    /// Drags <c>value</c> from outside the page over the point x, y, in CSS pixels of the viewport, and releases it
    /// there: dragenter, dragover and drop fire with its strings and Files. The result is the RV_DROP_* operation the
    /// page accepted: 1 copy, 2 move, 4 link, or 0 when it refused the drop.
    /// </summary>
    let drop (host: WebCoreHost) (x: int) (y: int) (allowed: int) (value: TransferPayload) =
        host.EnsureActive ()
        let value = payload value
        Interop.check "rv_host_set_drag" (Native.rv_host_set_drag (host.Handle, 0))
        let data = Native.rv_drag_data_create ()

        try
            for struct (kind, text) in value.Strings do
                Interop.check "rv_drag_data_add" (Native.rv_drag_data_add (data, kind, Native.utf8 text, Native.utf8Length text))

            for item in value.Files do
                Interop.check "rv_drag_data_add_file" (Native.rv_drag_data_add_file (data, native host (box item) "file"))

            let over = Native.rv_host_drag (host.Handle, DragOver, x, y, allowed, data, 0)

            if over < 0 then
                Interop.check "rv_host_drag" over

            let dropped = Native.rv_host_drag (host.Handle, DragDrop, x, y, allowed, data, 0)

            if dropped < 0 then
                Interop.check "rv_host_drag" dropped

            dropped
        finally
            Native.rv_drag_data_release data

    /// <summary>
    /// Moves the page's clipboard data through the app, in the App clipboard style: <c>write</c> takes the strings and
    /// Files of each copy, and <c>read</c> returns the payload of a paste, or None to refuse it, which fails the
    /// paste with NotAllowedError.
    /// </summary>
    let setClipboard (host: WebCoreHost) (write: TransferPayload -> unit) (read: unit -> TransferPayload option) =
        host.EnsureActive ()

        if isNull (box write) then
            nullArg (nameof write)

        if isNull (box read) then
            nullArg (nameof read)

        let strings = Collections.Generic.List<struct (string * string)>()
        let files = Collections.Generic.List<File>()

        let flush () =
            if strings.Count > 0 || files.Count > 0 then
                let copied =
                    {
                        Strings = strings.ToArray ()
                        Files = files.ToArray ()
                    }

                strings.Clear ()
                files.Clear ()
                write copied

        let entry =
            {
                Write =
                    Native.ClipboardWrite (fun _ kind data length first ->
                        try
                            if first <> 0 then
                                flush ()

                            strings.Add (struct (Marshal.PtrToStringUTF8 kind, Marshal.PtrToStringUTF8 (data, length)))
                        with error ->
                            Interop.callbackFailed error)
                WriteFile =
                    Native.ClipboardWriteFile (fun _ handle first ->
                        try
                            if first <> 0 then
                                flush ()

                            files.Add (file host handle)
                        with error ->
                            Interop.callbackFailed error)
                Read =
                    Native.ClipboardRead (fun _ request ->
                        try
                            match read () with
                            | None -> Interop.check "rv_clipboard_deny" (Native.rv_clipboard_deny request)
                            | Some value ->
                                let value = payload value

                                for struct (kind, text) in value.Strings do
                                    Interop.check
                                        "rv_clipboard_offer"
                                        (Native.rv_clipboard_offer (request, kind, Native.utf8 text, Native.utf8Length text))

                                for item in value.Files do
                                    Interop.check "rv_clipboard_offer_file" (Native.rv_clipboard_offer_file (request, native host (box item) "file"))
                        with error ->
                            Interop.callbackFailed error)
                Flush = flush
            }

        callbacks.AddOrUpdate (host, entry)

        Interop.check
            "rv_host_set_clipboard"
            (Native.rv_host_set_clipboard (
                host.Handle,
                0,
                Marshal.GetFunctionPointerForDelegate<Native.ClipboardWrite> entry.Write,
                Marshal.GetFunctionPointerForDelegate<Native.ClipboardRead> entry.Read,
                0n
            ))

        Interop.check
            "rv_host_set_clipboard_file_write"
            (Native.rv_host_set_clipboard_file_write (
                host.Handle,
                Marshal.GetFunctionPointerForDelegate<Native.ClipboardWriteFile> entry.WriteFile,
                0n
            ))

    /// <summary>
    /// Runs a copy as a menu would, and hands what it copied to the <c>write</c> of setClipboard; the result is
    /// whether the copy applied.
    /// </summary>
    let copy (host: WebCoreHost) =
        host.EnsureActive ()
        let status = Native.rv_host_edit (host.Handle, EditCopy)

        if status < 0 then
            Interop.check "rv_host_edit" status

        match callbacks.TryGetValue host with
        | true, entry -> entry.Flush ()
        | _ -> ()

        status = 1

    /// <summary>Runs a paste as a menu would; the result is whether the paste applied.</summary>
    let paste (host: WebCoreHost) =
        host.EnsureActive ()
        let status = Native.rv_host_edit (host.Handle, EditPaste)

        if status < 0 then
            Interop.check "rv_host_edit" status

        status = 1

    /// <summary>
    /// Replaces <c>range</c> with <c>text</c> as an insertReplacementText edit, as a spelling correction does: a
    /// cancelable beforeinput with the range as its target range and a text/plain and text/html dataTransfer, then the
    /// edit and input. The result is whether the edit applied.
    /// </summary>
    let replaceRange (host: WebCoreHost) (range: AbstractRange) (text: string) =
        host.EnsureActive ()

        if isNull text then
            nullArg (nameof text)

        let status =
            Native.rv_host_replace_range (host.Handle, native host (box range) "range", Native.utf8 text, Native.utf8Length text)

        if status < 0 then
            Interop.check "rv_host_replace_range" status

        status = 1
