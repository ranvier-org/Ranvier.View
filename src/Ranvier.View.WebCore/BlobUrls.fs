namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Object URLs, engine reads of blobs and blob URLs, FileReader and file-backed Files of a native host.</summary>
[<RequireQualifiedAccess>]
module BlobUrls =
    let private ReadText = 0
    let private ReadBytes = 1

    /// <summary>Completions of native blob reads, keyed by the context value passed to the host.</summary>
    type private Completions() =
        let gate = obj ()
        let handlers = Dictionary<int64, int -> unit>()
        let mutable next = 0L

        let callback =
            Native.ResourceCompletion (fun context outcome _ _ ->
                let handler =
                    lock gate (fun () ->
                        match handlers.TryGetValue (int64 context) with
                        | true, handler ->
                            handlers.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome))

        member _.Pointer =
            Marshal.GetFunctionPointerForDelegate<Native.ResourceCompletion> callback

        member _.Add(handler) =
            lock gate (fun () ->
                next <- next + 1L
                handlers[next] <- handler
                next)

        member _.Remove(key: int64) =
            lock gate (fun () -> handlers.Remove key |> ignore)

    let private completions = Completions ()

    let private failure (outcome: int) (error: string) =
        match outcome with
        | 3 -> ResourceFailure.Cancelled
        | 4 -> ResourceFailure.Invalidated "The blob read's document was replaced or destroyed"
        | _ -> ResourceFailure.LoadFailed (if isNull error then "The blob read failed" else error)

    let private result (operation: uint64) =
        let mutable length = 0L
        let status = Native.rv_blob_read_result (operation, null, 0L, &length)

        if status <> 0 && status <> -6 then
            Interop.check "rv_blob_read_result" status

        let bytes = Array.zeroCreate<byte>(int length)
        let mutable written = 0L

        Native.rv_blob_read_result (operation, bytes, length, &written)
        |> Interop.check "rv_blob_read_result"

        bytes

    let private text name (call: byte array -> int -> int array -> int) =
        try
            Interop.text name call
        with _ ->
            null

    /// <summary>A blob URL service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks the "blob-urls" functions. Disposing the service,
    /// or the host, releases every native read; a read in flight then delivers <c>ResourceFailure.Cancelled</c>.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : BlobUrlService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "blob-urls"
                && WebCoreHost.Has "dom-constructor:FileReader"
            )
        then
            raise (NotSupportedException "The native library has no blob URL or FileReader functions")

        let session = host.Session
        let document = host.Document
        let documentHandle = Interop.handle (box document)
        let mutable disposed = false
        let live = HashSet<IDisposable>()

        let check () =
            if disposed then
                raise (ObjectDisposedException (nameof BlobUrlService))

            host.EnsureActive ()

            if not (obj.ReferenceEquals (host.Document, document)) then
                raise (InvalidOperationException "The blob URL document is no longer active")

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when
                obj.ReferenceEquals (target.Session, session)
                && target.Handle <> 0n
                ->
                target.Handle
            | _ -> invalidArg name $"The {name} belongs to another provider"

        /// Starts a native read; its value is built on the owner context from the operation's result.
        let read (terminal: ResourceOutcome<'T> -> unit) (start: nativeint -> nativeint -> struct (int * uint64)) (value: uint64 -> 'T) =
            let mutable operation = 0UL
            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<'T>>

            let release () =
                if operation <> 0UL then
                    Native.rv_resource_release operation |> ignore
                    operation <- 0UL

            arbiter <-
                new ResourceArbiter<'T> (
                    host.PostToOwner,
                    (fun outcome ->
                        live.Remove arbiter |> ignore
                        terminal outcome),
                    (fun () ->
                        if operation <> 0UL then
                            Native.rv_resource_cancel operation |> ignore),
                    ignore
                )

            live.Add arbiter |> ignore

            let key =
                completions.Add (fun outcome ->
                    let settled =
                        if outcome = 0 then
                            try
                                ResourceOutcome.Succeeded (value operation)
                            with error ->
                                ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)
                        else
                            ResourceOutcome.Failed (
                                failure outcome (text "rv_blob_read_error" (fun b c l -> Native.rv_blob_read_error (operation, b, c, l)))
                            )

                    release ()
                    arbiter.Settle settled |> ignore)

            let struct (status, token) = start completions.Pointer (nativeint key)
            operation <- token

            if status <> 0 then
                completions.Remove key
                release ()

                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed $"The blob read returned {status}"))
                |> ignore

            arbiter :> ResourceOperation<'T>

        let decode (bytes: byte array) =
            Encoding.UTF8.GetString bytes

        let reader (value: FileReader) =
            native (box (BlobUrlContract.reader value)) "reader"

        let hostDisposal =
            host.Disposed.Subscribe (fun () ->
                disposed <- true

                for operation in List.ofSeq live do
                    operation.Dispose ())

        { new BlobUrlService with
            member _.CreateObjectUrl blob =
                check ()
                let blob = native (box (BlobUrlContract.blob blob)) "blob"
                Interop.text "URL.createObjectURL" (fun b c l -> NativeDom.rv_dom_domurl_create_object_url (documentHandle, blob, b, c, l))

            member _.RevokeObjectUrl url =
                check ()
                let bytes = Encoding.UTF8.GetBytes (BlobUrlContract.url url)

                NativeDom.rv_dom_domurl_revoke_object_url (documentHandle, bytes, bytes.Length)
                |> Interop.check "URL.revokeObjectURL"

            member _.ReadUrl(url, terminal) =
                check ()
                let bytes = Encoding.UTF8.GetBytes (BlobUrlContract.blobUrl url)

                read
                    terminal
                    (fun pointer context ->
                        let mutable token = 0UL

                        let status =
                            Native.rv_blob_url_read (host.Handle, bytes, bytes.Length, pointer, context, &token)

                        struct (status, token))
                    (fun operation ->
                        {
                            ContentType = Interop.text "rv_blob_read_type" (fun b c l -> Native.rv_blob_read_type (operation, b, c, l))
                            Bytes = result operation
                        })

            member _.ReadText(blob, terminal) =
                check ()
                let blob = native (box (BlobUrlContract.blob blob)) "blob"

                read
                    terminal
                    (fun pointer context ->
                        let mutable token = 0UL

                        let status =
                            Native.rv_blob_read (host.Handle, blob, ReadText, pointer, context, &token)

                        struct (status, token))
                    (fun operation -> decode (result operation))

            member _.ReadBytes(blob, terminal) =
                check ()
                let blob = native (box (BlobUrlContract.blob blob)) "blob"

                read
                    terminal
                    (fun pointer context ->
                        let mutable token = 0UL

                        let status =
                            Native.rv_blob_read (host.Handle, blob, ReadBytes, pointer, context, &token)

                        struct (status, token))
                    result

            member _.CreateFileReader() =
                check ()
                let mutable handle = 0n

                NativeDom.rv_dom_file_reader_create (documentHandle, &handle)
                |> Interop.check "FileReader constructor"

                session.Object (
                    handle,
                    Kind.FileReader,
                    unbox<NativeObject>(box document),
                    Func<Session, nativeint, NativeObject>(fun s h -> FileReaderObject (s, h))
                )
                |> unbox<FileReader>

            member _.Result value =
                let target = reader value
                let mutable kind = 0
                let mutable length = 0L
                let status = Native.rv_file_reader_result (target, &kind, null, 0L, &length)

                if status <> 0 && status <> -6 then
                    Interop.check "FileReader.result" status

                let bytes = Array.zeroCreate<byte>(int length)

                if length > 0L then
                    Native.rv_file_reader_result (target, &kind, bytes, length, &length)
                    |> Interop.check "FileReader.result"

                match kind with
                | 1 -> FileReaderResult.Text (decode bytes)
                | 2 -> FileReaderResult.Bytes bytes
                | _ -> FileReaderResult.Empty

            member _.Error value =
                let target = reader value

                match Interop.text "FileReader.error" (fun b c l -> Native.rv_file_reader_error (target, b, c, l)) with
                | null -> None
                | name -> Some name

            member _.Dispose() =
                if not disposed then
                    disposed <- true
                    hostDisposal.Dispose ()

                    for operation in List.ofSeq live do
                        operation.Dispose ()
        }

    /// <summary>
    /// A File backed by the file at <c>path</c>, which the app supplies and trusts. The path must be absolute; the
    /// file is opened when it is read, and a read of a missing file fails with <c>NotFoundError</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentException"><c>path</c> is not absolute.</exception>
    let createFile (host: WebCoreHost) (path: string) : File =
        if isNull (box host) then
            nullArg (nameof host)

        if isNull path then
            nullArg (nameof path)

        host.EnsureActive ()

        if not (IO.Path.IsPathFullyQualified path) then
            invalidArg (nameof path) "The path must be absolute"

        let bytes = Encoding.UTF8.GetBytes path
        let result = [| 0n |]

        Native.rv_file_create_from_path (host.Handle, bytes, bytes.Length, result)
        |> Interop.check "rv_file_create_from_path"

        host.Session.Object (
            result[0],
            Kind.Blob,
            unbox<NativeObject>(box host.Document),
            Func<Session, nativeint, NativeObject>(fun s h -> FileObject (s, h))
        )
        |> unbox<File>
