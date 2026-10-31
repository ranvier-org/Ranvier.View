namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvSnapshotOptions</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal SnapshotOptionsNative =
    val mutable Size: int
    val mutable ColorSpace: int
    val mutable Scale: float
    val mutable Crop: int
    val mutable Encoding: int
    val mutable X: float
    val mutable Y: float
    val mutable Width: float
    val mutable Height: float
    val mutable Quality: float

/// <summary>Snapshots of a native host's viewport and elements.</summary>
[<RequireQualifiedAccess>]
module Snapshots =
    /// <summary>Completions of native snapshots, keyed by the context value passed to the host.</summary>
    type private Completions() =
        let gate = obj ()
        let handlers = Dictionary<int64, int -> int -> int -> unit>()
        let mutable next = 0L

        let callback =
            Native.ResourceCompletion (fun context outcome width height ->
                let handler =
                    lock gate (fun () ->
                        match handlers.TryGetValue (int64 context) with
                        | true, handler ->
                            handlers.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome width height))

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

    let private encodingCode encoding =
        match encoding with
        | ImageEncoding.Rgba8 -> 0
        | ImageEncoding.Png -> 1
        | ImageEncoding.Jpeg -> 2
        | ImageEncoding.Webp -> 3

    let private encodingFacility encoding =
        match encoding with
        | ImageEncoding.Rgba8 -> None
        | ImageEncoding.Png -> Some "image-encoding-png"
        | ImageEncoding.Jpeg -> Some "image-encoding-jpeg"
        | ImageEncoding.Webp -> Some "image-encoding-webp"

    let private bytes (operation: uint64) =
        let mutable length = 0L
        let status = Native.rv_snapshot_result (operation, null, 0L, &length)

        if status <> 0 && status <> -6 then
            Interop.check "rv_snapshot_result" status

        let buffer = Array.zeroCreate<byte>(int length)
        let mutable written = 0L

        Native.rv_snapshot_result (operation, buffer, length, &written)
        |> Interop.check "rv_snapshot_result"

        buffer

    /// <summary>A snapshot service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks "snapshot-options". Disposing the service, or the
    /// host, releases every snapshot not yet delivered, which then settles <c>ResourceFailure.Cancelled</c>.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : SnapshotService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "snapshot-options") then
            raise (NotSupportedException "The native library has no snapshot option functions")

        let document = host.Document
        let mutable disposed = false
        let live = HashSet<IDisposable>()

        let check () =
            if disposed then
                raise (ObjectDisposedException (nameof SnapshotService))

            host.EnsureActive ()

            if not (obj.ReferenceEquals (host.Document, document)) then
                raise (InvalidOperationException "The snapshot document is no longer active")

        let facility (name: string) =
            host.EnsureActive ()
            let mutable value = -1

            Native.rv_host_capability_fact (host.Handle, 4, name, 1, &value)
            |> Interop.check "rv_host_capability_fact"

            value = 1

        let supportsColorSpace colorSpace =
            match colorSpace with
            | SnapshotColorSpace.Srgb -> true
            | SnapshotColorSpace.DisplayP3 -> facility "snapshot-display-p3"

        let supportsEncoding encoding =
            match encodingFacility encoding with
            | None -> true
            | Some name -> facility name

        let capture (subject: SnapshotSubject) (options: SnapshotOptions) terminal =
            check ()
            let options = SnapshotContract.options subject options

            if not (supportsColorSpace options.ColorSpace) then
                raise (NotSupportedException "The native library has no snapshots in the color space")

            if not (supportsEncoding options.Encoding) then
                raise (NotSupportedException "The native library has no encoder for the encoding")

            let element =
                match subject with
                | SnapshotSubject.Viewport -> 0n
                | SnapshotSubject.Element element -> Interop.handle (box element)

            let mutable value = SnapshotOptionsNative ()
            value.Size <- sizeof<SnapshotOptionsNative>

            value.ColorSpace <-
                (if options.ColorSpace = SnapshotColorSpace.DisplayP3 then
                     1
                 else
                     0)

            value.Scale <- defaultArg options.Scale 0.0
            value.Encoding <- encodingCode options.Encoding
            value.Quality <- defaultArg options.Quality -1.0

            match options.Crop with
            | Some crop ->
                value.Crop <- 1
                value.X <- crop.X
                value.Y <- crop.Y
                value.Width <- crop.Width
                value.Height <- crop.Height
            | None -> ()

            let mutable operation = 0UL
            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<SnapshotImage>>

            let release () =
                if operation <> 0UL then
                    Native.rv_resource_release operation |> ignore
                    operation <- 0UL

            arbiter <-
                new ResourceArbiter<SnapshotImage> (
                    host.PostToOwner,
                    (fun outcome ->
                        live.Remove arbiter |> ignore
                        terminal outcome),
                    (fun () ->
                        if operation <> 0UL then
                            Native.rv_resource_cancel operation |> ignore),
                    ignore
                )

            let key =
                completions.Add (fun outcome width height ->
                    let settled =
                        if outcome = 0 then
                            try
                                ResourceOutcome.Succeeded
                                    {
                                        PixelWidth = width
                                        PixelHeight = height
                                        Format = options.Encoding
                                        PixelColorSpace = options.ColorSpace
                                        Data = bytes operation
                                    }
                            with error ->
                                ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)
                        else
                            ResourceOutcome.Failed (ResourceFailure.LoadFailed $"The snapshot ended with outcome {outcome}")

                    release ()
                    arbiter.Settle settled |> ignore)

            let mutable token = 0UL

            let status =
                Native.rv_host_snapshot (host.Handle, element, NativePtr.toNativeInt &&value, completions.Pointer, nativeint key, &token)

            if status <> 0 then
                completions.Remove key
                (arbiter :> IDisposable).Dispose()

                match status with
                | -9 -> raise (ArgumentOutOfRangeException ("options", "The crop lies outside the subject"))
                | -12 -> invalidArg "subject" "The element belongs to another document"
                | -5 -> raise (InvalidOperationException "The element has no box to paint")
                | _ -> Interop.check "rv_host_snapshot" status

            operation <- token
            live.Add arbiter |> ignore
            arbiter :> ResourceOperation<SnapshotImage>

        let hostDisposal =
            host.Disposed.Subscribe (fun () ->
                disposed <- true

                for operation in List.ofSeq live do
                    operation.Dispose ())

        { new SnapshotService with
            member _.Available = true

            member _.SupportsColorSpace colorSpace =
                check ()
                supportsColorSpace colorSpace

            member _.SupportsEncoding encoding =
                check ()
                supportsEncoding encoding

            member _.Capture(subject, options, terminal) =
                capture subject options terminal

            member _.Dispose() =
                if not disposed then
                    disposed <- true
                    hostDisposal.Dispose ()

                    for operation in List.ofSeq live do
                        operation.Dispose ()
        }
