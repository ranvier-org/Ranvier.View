namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open Ranvier.Dom
open RanvierWebCoreAbi

#nowarn "9"

/// <summary>The <c>RvMediaVideoConfiguration</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal MediaVideoConfigurationNative =
    val mutable Size: int
    val mutable ContentType: nativeint
    val mutable ContentTypeLength: int
    val mutable Width: uint32
    val mutable Height: uint32
    val mutable Bitrate: uint64
    val mutable Framerate: float
    val mutable AlphaChannel: int
    val mutable HdrMetadataType: int
    val mutable ColorGamut: int
    val mutable TransferFunction: int

/// <summary>The <c>RvMediaAudioConfiguration</c> of the native header.</summary>
[<Struct; StructLayout(LayoutKind.Sequential)>]
type internal MediaAudioConfigurationNative =
    val mutable Size: int
    val mutable ContentType: nativeint
    val mutable ContentTypeLength: int
    val mutable Channels: nativeint
    val mutable ChannelsLength: int
    val mutable Bitrate: int64
    val mutable Samplerate: int64
    val mutable SpatialRendering: int

/// <summary>Media capability queries of a native host, through the engine's MediaCapabilities.</summary>
[<RequireQualifiedAccess>]
module MediaCapabilities =
    /// <summary>Completions of native queries, keyed by the context value passed to the host.</summary>
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

    let private optional (value: 'T option) (code: 'T -> int) =
        match value with
        | Some value -> code value
        | None -> -1

    let private flag value =
        if value then 1 else 0

    let private hdr value =
        match value with
        | MediaHdrMetadataType.SmpteSt2086 -> 0
        | MediaHdrMetadataType.SmpteSt2094_10 -> 1
        | MediaHdrMetadataType.SmpteSt2094_40 -> 2

    let private gamut value =
        match value with
        | MediaColorGamut.Srgb -> 0
        | MediaColorGamut.P3 -> 1
        | MediaColorGamut.Rec2020 -> 2

    let private transfer value =
        match value with
        | MediaTransferFunction.Srgb -> 0
        | MediaTransferFunction.Pq -> 1
        | MediaTransferFunction.Hlg -> 2

    /// <summary>Pins UTF-8 copies of strings for the duration of one native call.</summary>
    type private Pins() =
        let handles = List<GCHandle>()

        member _.Text(value: string) =
            let bytes = Encoding.UTF8.GetBytes value
            let handle = GCHandle.Alloc (bytes, GCHandleType.Pinned)
            handles.Add handle
            struct (handle.AddrOfPinnedObject (), bytes.Length)

        interface IDisposable with
            member _.Dispose() =
                for handle in handles do
                    handle.Free ()

    let private failure outcome =
        match outcome with
        | 3 -> ResourceFailure.Cancelled
        | 4 -> ResourceFailure.Invalidated "The media query's document was replaced or destroyed"
        | _ -> ResourceFailure.LoadFailed $"The media query ended with outcome {outcome}"

    /// <summary>A media capability service for <c>host</c>'s current document.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks "media-capabilities". Disposing the service, or the
    /// host, releases every native query; an undelivered query then delivers <c>ResourceFailure.Cancelled</c>.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : MediaCapabilitiesService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "media-capabilities") then
            raise (NotSupportedException "The native library has no media capability functions")

        let document = host.Document
        let mutable disposed = false
        let live = HashSet<IDisposable>()

        let check () =
            if disposed then
                raise (ObjectDisposedException (nameof MediaCapabilitiesService))

            host.EnsureActive ()

            if not (obj.ReferenceEquals (host.Document, document)) then
                raise (InvalidOperationException "The media capabilities document is no longer active")

        let query encoding kind (video: MediaVideoConfiguration option) (audio: MediaAudioConfiguration option) terminal =
            check ()
            MediaCapabilitiesContract.configuration video audio
            let mutable operation = 0UL
            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<MediaCapabilitiesInfo>>

            let release () =
                if operation <> 0UL then
                    Native.rv_resource_release operation |> ignore
                    operation <- 0UL

            arbiter <-
                new ResourceArbiter<MediaCapabilitiesInfo> (
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
                            let mutable supported = 0
                            let mutable smooth = 0
                            let mutable powerEfficient = 0

                            let status =
                                Native.rv_media_capabilities_info (operation, &supported, &smooth, &powerEfficient)

                            if status = 0 then
                                ResourceOutcome.Succeeded
                                    {
                                        Supported = supported = 1
                                        Smooth = smooth = 1
                                        PowerEfficient = powerEfficient = 1
                                    }
                            else
                                ResourceOutcome.Failed (ResourceFailure.LoadFailed $"rv_media_capabilities_info returned {status}")
                        else
                            ResourceOutcome.Failed (failure outcome)

                    release ()
                    arbiter.Settle settled |> ignore)

            use pins = new Pins ()
            let mutable videoValue = MediaVideoConfigurationNative ()
            let mutable audioValue = MediaAudioConfigurationNative ()

            let videoPointer =
                match video with
                | None -> 0n
                | Some value ->
                    let struct (text, length) = pins.Text value.ContentType
                    videoValue.Size <- sizeof<MediaVideoConfigurationNative>
                    videoValue.ContentType <- text
                    videoValue.ContentTypeLength <- length
                    videoValue.Width <- value.Width
                    videoValue.Height <- value.Height
                    videoValue.Bitrate <- value.Bitrate
                    videoValue.Framerate <- value.Framerate
                    videoValue.AlphaChannel <- optional value.AlphaChannel flag
                    videoValue.HdrMetadataType <- optional value.HdrMetadataType hdr
                    videoValue.ColorGamut <- optional value.ColorGamut gamut
                    videoValue.TransferFunction <- optional value.TransferFunction transfer
                    NativePtr.toNativeInt &&videoValue

            let audioPointer =
                match audio with
                | None -> 0n
                | Some value ->
                    let struct (text, length) = pins.Text value.ContentType
                    audioValue.Size <- sizeof<MediaAudioConfigurationNative>
                    audioValue.ContentType <- text
                    audioValue.ContentTypeLength <- length

                    match value.Channels with
                    | Some channels ->
                        let struct (pointer, count) = pins.Text channels
                        audioValue.Channels <- pointer
                        audioValue.ChannelsLength <- count
                    | None -> ()

                    audioValue.Bitrate <-
                        (match value.Bitrate with
                         | Some bitrate -> int64 bitrate
                         | None -> -1L)

                    audioValue.Samplerate <-
                        (match value.Samplerate with
                         | Some rate -> int64 rate
                         | None -> -1L)

                    audioValue.SpatialRendering <- optional value.SpatialRendering flag
                    NativePtr.toNativeInt &&audioValue

            let mutable token = 0UL

            let status =
                if encoding then
                    Native.rv_media_encoding_info (host.Handle, kind, videoPointer, audioPointer, completions.Pointer, nativeint key, &token)
                else
                    Native.rv_media_decoding_info (host.Handle, kind, videoPointer, audioPointer, completions.Pointer, nativeint key, &token)

            operation <- token

            if status <> 0 then
                completions.Remove key
                operation <- 0UL

                let failed =
                    if status = Native.DomException then
                        MediaCapabilitiesContract.rejected
                    else
                        ResourceFailure.LoadFailed $"The media query returned {status}"

                arbiter.Settle (ResourceOutcome.Failed failed)
                |> ignore

            arbiter :> ResourceOperation<MediaCapabilitiesInfo>

        let hostDisposal =
            host.Disposed.Subscribe (fun () ->
                disposed <- true

                for operation in List.ofSeq live do
                    operation.Dispose ())

        { new MediaCapabilitiesService with
            member _.DecodingInfo(kind, video, audio, terminal) =
                let code =
                    match kind with
                    | MediaDecodingType.File -> 0
                    | MediaDecodingType.MediaSource -> 1
                    | MediaDecodingType.WebRtc -> 2

                query false code video audio terminal

            member _.EncodingInfo(kind, video, audio, terminal) =
                let code =
                    match kind with
                    | MediaEncodingType.Record -> 0
                    | MediaEncodingType.WebRtc -> 1

                query true code video audio terminal

            member _.Dispose() =
                if not disposed then
                    disposed <- true
                    hostDisposal.Dispose ()

                    for operation in List.ofSeq live do
                        operation.Dispose ()
        }
