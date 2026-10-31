namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Blobs, Files and FileLists of a native host, with reads of their bytes.</summary>
module BinaryValues =
    let private endings value =
        match value with
        | BlobLineEndings.Transparent -> NativeDom.RV_DOM_ENDING_TYPE_TRANSPARENT
        | BlobLineEndings.Native -> NativeDom.RV_DOM_ENDING_TYPE_NATIVE

    /// <summary>Pinned blob parts for one constructor call.</summary>
    type private Parts(parts: BlobPart array, handle: obj -> nativeint) =
        let pins = List<GCHandle>()

        let pin (value: obj) =
            let pinned = GCHandle.Alloc (value, GCHandleType.Pinned)
            pins.Add pinned
            pinned.AddrOfPinnedObject ()

        let items =
            parts
            |> Array.map (fun part ->
                let mutable item = NativeDom.RvDomUnionBufferSourceOrBlobOrUSVString ()
                item.present <- 1

                match part with
                | BlobPart.Bytes bytes ->
                    let mutable span = NativeDom.RvDomByteSpan ()
                    span.data <- (if bytes.Length = 0 then 0n else pin bytes)
                    span.length <- bytes.Length
                    item.tag <- NativeDom.RV_DOM_UNION_BUFFER_SOURCE_OR_BLOB_OR_USV_STRING_BUFFER_SOURCE
                    item.arm0 <- span
                | BlobPart.Blob blob ->
                    item.tag <- NativeDom.RV_DOM_UNION_BUFFER_SOURCE_OR_BLOB_OR_USV_STRING_BLOB
                    item.arm1 <- handle (box blob)
                | BlobPart.Text text ->
                    let bytes = Encoding.UTF8.GetBytes text
                    let mutable span = NativeDom.RvDomUtf8StringSpan ()
                    span.data <- (if bytes.Length = 0 then 0n else pin bytes)
                    span.length <- bytes.Length
                    item.tag <- NativeDom.RV_DOM_UNION_BUFFER_SOURCE_OR_BLOB_OR_USV_STRING_STRING
                    item.arm2 <- span

                item)

        member _.Sequence =
            let mutable sequence = NativeDom.RvDomSequenceBufferSourceOrBlobOrUSVString ()
            sequence.present <- 1
            sequence.count <- items.Length
            sequence.items <- (if items.Length = 0 then 0n else pin items)
            sequence

        interface IDisposable with
            member _.Dispose() =
                for pinned in pins do
                    pinned.Free ()

                pins.Clear ()

    /// <summary>A binary value service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks the Blob and File constructors or
    /// <c>rv_blob_read_bytes</c>. A read copies the bytes on the host's work queue, after ReadBytes returns.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : BinaryValueService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "dom-binary-values"
                && WebCoreHost.Has "dom-constructor:Blob"
                && WebCoreHost.Has "dom-constructor:File"
            )
        then
            raise (NotSupportedException "The native library has no Blob, File or rv_blob_read_bytes functions")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof BinaryValueService))

            host.EnsureActive ()

        let native (value: obj) (name: string) =
            match value with
            | :? NativeObject as target when
                obj.ReferenceEquals (target.Session, session)
                && target.Handle <> 0n
                ->
                target.Handle
            | _ -> invalidArg name $"The {name} belongs to another provider"

        let wrap (handle: nativeint) (file: bool) =
            session.Object (
                handle,
                Kind.Blob,
                unbox<NativeObject>(box document),
                Func<Session, nativeint, NativeObject>(fun s h -> if file then FileObject (s, h) else BlobObject (s, h))
            )

        let read (blob: nativeint) =
            let mutable length = 0L
            let status = Native.rv_blob_read_bytes (blob, null, 0L, &length)

            if status <> 0 && status <> -6 then
                Interop.check "rv_blob_read_bytes" status

            let bytes = Array.zeroCreate<byte>(int length)
            let mutable written = 0L
            Interop.check "rv_blob_read_bytes" (Native.rv_blob_read_bytes (blob, bytes, length, &written))

            if written <> length then
                invalidOp "WebCore returned a different blob length"

            bytes

        { new BinaryValueService with
            member _.CreateBlob(parts, options) =
                check ()
                let options = BinaryValueContract.blobOptions options

                use parts =
                    new Parts (BinaryValueContract.parts parts, (fun value -> native value "parts"))

                use inputs = new InputScope ()
                let mutable dictionary = NativeDom.RvDomBlobPropertyBag ()
                dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomBlobPropertyBag>())
                dictionary.``type`` <- inputs.TextInput (Option.ofObj (options.Type))
                dictionary.endings <- endings options.Endings
                let mutable result = 0n

                NativeDom.rv_dom_blob_create (Interop.handle (box document), [| parts.Sequence |], [| dictionary |], &result)
                |> Interop.check "Blob constructor"

                wrap result false |> unbox<Blob>

            member _.CreateFile(parts, name, options) =
                check ()
                let options = BinaryValueContract.fileOptions options
                let name = Encoding.UTF8.GetBytes (BinaryValueContract.name name)

                use parts =
                    new Parts (BinaryValueContract.parts parts, (fun value -> native value "parts"))

                let mutable modified = 0L
                let pinned = GCHandle.Alloc (modified, GCHandleType.Pinned)

                try
                    use inputs = new InputScope ()
                    let mutable dictionary = NativeDom.RvDomFilePropertyBag ()
                    dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomFilePropertyBag>())
                    dictionary.``type`` <- inputs.TextInput (Option.ofObj (options.Type))
                    dictionary.endings <- endings options.Endings

                    match options.LastModified with
                    | Some time ->
                        let boxed = pinned.AddrOfPinnedObject ()
                        Marshal.WriteInt64 (boxed, time)
                        dictionary.last_modified <- boxed
                    | None -> dictionary.last_modified <- 0n

                    let mutable result = 0n

                    NativeDom.rv_dom_file_create (Interop.handle (box document), [| parts.Sequence |], name, name.Length, [| dictionary |], &result)
                    |> Interop.check "File constructor"

                    wrap result true |> unbox<File>
                finally
                    pinned.Free ()

            member _.CreateFileList files =
                check ()

                let handles =
                    BinaryValueContract.files files
                    |> Array.map (fun file -> native (box file) "files")

                let result = [| 0n |]

                Native.rv_file_list_create (handles, handles.Length, result)
                |> Interop.check "rv_file_list_create"

                session.Object (
                    result[0],
                    Kind.FileList,
                    unbox<NativeObject>(box document),
                    Func<Session, nativeint, NativeObject>(fun s h -> FileListObject (s, h))
                )
                |> unbox<FileList>

            member _.ReadBytes(blob, terminal) =
                check ()
                let blob = native (box (BinaryValueContract.blob blob)) "blob"

                let arbiter =
                    new ResourceArbiter<byte array> (host.PostToOwner, terminal, ignore, ignore)

                host.PostToOwner (fun () ->
                    let outcome =
                        try
                            ResourceOutcome.Succeeded (read blob)
                        with error ->
                            ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)

                    arbiter.Settle outcome |> ignore)

                arbiter :> ResourceOperation<byte array>

            member _.Dispose() =
                disposed.Value <- true
        }
