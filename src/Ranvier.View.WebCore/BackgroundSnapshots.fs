namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Owner-thread capture with detached worker encoding and one replaceable pending preview per host.</summary>
type BackgroundSnapshotService =
    inherit SnapshotService

    /// <summary>Captures when capacity becomes available; a newer preview cancels the pending preview.</summary>
    abstract Preview:
        subject: SnapshotSubject * options: SnapshotOptions * terminal: (ResourceOutcome<SnapshotImage> -> unit) -> ResourceOperation<SnapshotImage>

/// <summary>Opt-in background snapshot encoding. Legacy Snapshots.forHost remains synchronous.</summary>
[<RequireQualifiedAccess>]
module BackgroundSnapshots =
    type private Request =
        {
            Start: (unit -> unit) -> unit
            Cancel: unit -> unit
        }

    /// <summary>Owner-thread admission; cancellation never releases a running codec's slot early.</summary>
    type private Capacity() =
        let mutable active = false
        let mutable pending: Request option = None

        let rec start request =
            active <- true
            let mutable finished = false

            request.Start (fun () ->
                if not finished then
                    finished <- true
                    active <- false
                    let next = pending
                    pending <- None
                    next |> Option.iter start)

        member _.Admit(preview, request) =
            if not active then
                start request
            elif not preview then
                raise (InvalidOperationException "Snapshot encoding is busy; retry the export after completion")
            else
                let previous = pending
                pending <- Some request

                previous
                |> Option.iter (fun item -> item.Cancel ())

        member _.Withdraw(request) =
            match pending with
            | Some item when obj.ReferenceEquals (item, request) -> pending <- None
            | _ -> ()

    let private capacities = ConditionalWeakTable<WebCoreHost, Capacity>()

    let private encode (image: SnapshotImage) (options: SnapshotOptions) =
        let mutable encoded = 0n

        try
            OptionalNative.rv_image_encode_rgba (
                image.Data,
                int64 image.Data.Length,
                image.PixelWidth,
                image.PixelHeight,
                (if image.PixelColorSpace = SnapshotColorSpace.Srgb then
                     0
                 else
                     1),
                NativeImageEncoding.code options.Encoding,
                defaultArg options.Quality -1.0,
                &encoded
            )
            |> Interop.check "rv_image_encode_rgba"

            let mutable length = 0L
            let status = OptionalNative.rv_encoded_image_bytes (encoded, null, 0L, &length)

            if status <> -6 then
                Interop.check "rv_encoded_image_bytes" status

            let data = Array.zeroCreate<byte>(int length)

            OptionalNative.rv_encoded_image_bytes (encoded, data, length, &length)
            |> Interop.check "rv_encoded_image_bytes"

            { image with
                Format = options.Encoding
                Data = data
            }
        finally
            if encoded <> 0n then
                OptionalNative.rv_encoded_image_release encoded

    let internal createWithBudget (host: WebCoreHost) (launch: (unit -> unit) -> unit) (maximumRawBytes: int64) : BackgroundSnapshotService =
        host.EnsureActive ()

        if
            maximumRawBytes < 1L
            || maximumRawBytes > 64L * 1024L * 1024L
        then
            invalidArg "maximumRawBytes" "raw byte budget must be between 1 and 67108864"

        if not (WebCoreHost.Has "detached-image-encoding-v1") then
            raise (NotSupportedException "The native library has no detached image encoder")

        let raw = Snapshots.forHostBounded true host
        let capacity = capacities.GetValue (host, fun _ -> Capacity ())
        let document = host.Document
        let live = HashSet<ResourceOperation<SnapshotImage>>()
        let mutable disposed = false

        let check () =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException "BackgroundSnapshotService")

            if not (obj.ReferenceEquals (document, host.Document)) then
                invalidOp "The snapshot document is no longer active"

        let capture preview subject options terminal =
            check ()
            let options = SnapshotContract.options subject options

            if
                not (
                    raw.SupportsColorSpace options.ColorSpace
                    && raw.SupportsEncoding options.Encoding
                )
            then
                raise (NotSupportedException "The requested snapshot format is unavailable")

            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<SnapshotImage>>
            let mutable request = Unchecked.defaultof<Request>
            let mutable captureOperation: ResourceOperation<SnapshotImage> option = None
            let mutable pendingPixels: SnapshotPixels option = None

            arbiter <-
                new ResourceArbiter<SnapshotImage> (
                    (fun action -> action ()),
                    (fun outcome ->
                        live.Remove (arbiter :> ResourceOperation<_>)
                        |> ignore

                        try
                            terminal outcome
                        with error ->
                            host.Errors.Add error),
                    (fun () ->
                        capacity.Withdraw request

                        pendingPixels
                        |> Option.iter (fun pixels -> pixels.Dispose ())

                        pendingPixels <- None

                        captureOperation
                        |> Option.iter (fun operation -> operation.Dispose ())),
                    ignore
                )

            let startOwnedPixels finished =
                let pixels = SnapshotPixels.Capture (host, subject, options)
                pendingPixels <- Some pixels

                let queued =
                    host.TryPostToOwner (fun () ->
                        let captured = pendingPixels
                        pendingPixels <- None

                        match captured with
                        | None -> finished ()
                        | Some pixels ->
                            let settle result =
                                try
                                    if
                                        disposed
                                        || not (obj.ReferenceEquals (document, host.Document))
                                    then
                                        arbiter.Cancel ()
                                    else
                                        arbiter.Settle result |> ignore
                                finally
                                    finished ()

                            if options.Encoding = ImageEncoding.Rgba8 then
                                let result =
                                    try
                                        use owned = pixels
                                        ResourceOutcome.Succeeded (owned.Read options)
                                    with error ->
                                        ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)

                                settle result
                            else
                                try
                                    launch (fun () ->
                                        let result =
                                            try
                                                use owned = pixels
                                                ResourceOutcome.Succeeded (owned.Read options)
                                            with error ->
                                                ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)

                                        host.TryPostToOwner (fun () -> settle result)
                                        |> ignore)
                                with error ->
                                    pixels.Dispose ()
                                    settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)))

                if not queued then
                    pendingPixels <- None
                    pixels.Dispose ()
                    arbiter.Cancel ()
                    finished ()

            request <-
                {
                    Cancel = arbiter.Cancel
                    Start =
                        fun finished ->
                            try
                                check ()
                                let scale = defaultArg options.Scale (float host.SnapshotDeviceScale)

                                let width, height =
                                    match options.Crop, subject with
                                    | Some crop, _ -> Math.Ceiling (crop.Width) + 1., Math.Ceiling (crop.Height) + 1.
                                    | None, SnapshotSubject.Viewport ->
                                        let struct (w, h) = host.SnapshotViewportSize
                                        float w, float h
                                    | None, SnapshotSubject.Element _ -> 0., 0.

                                let bytes =
                                    if width = 0. && height = 0. then
                                        64. * 1024. * 1024.
                                    else
                                        Math.Ceiling (width * scale)
                                        * Math.Ceiling (height * scale)
                                        * 4.

                                // Native capture enforces its exact ceiling without conservative crop rounding.
                                if
                                    maximumRawBytes < 67108864L
                                    && (not (Double.IsFinite bytes)
                                        || bytes > float maximumRawBytes)
                                then
                                    invalidOp "Snapshot exceeds capture limits (raw byte budget); crop the subject or raise the budget"

                                if WebCoreHost.Has "snapshot-pixels-v1" then
                                    startOwnedPixels finished
                                else
                                    captureOperation <-
                                        Some (
                                            raw.Capture (
                                                subject,
                                                { options with
                                                    Encoding = ImageEncoding.Rgba8
                                                },
                                                fun outcome ->
                                                    captureOperation
                                                    |> Option.iter (fun operation -> operation.Dispose ())

                                                    captureOperation <- None

                                                    match outcome with
                                                    | ResourceOutcome.Failed failure ->
                                                        try
                                                            arbiter.Settle (ResourceOutcome.Failed failure)
                                                            |> ignore
                                                        finally
                                                            finished ()
                                                    | ResourceOutcome.Succeeded pixels when options.Encoding = ImageEncoding.Rgba8 ->
                                                        try
                                                            arbiter.Settle (ResourceOutcome.Succeeded pixels)
                                                            |> ignore
                                                        finally
                                                            finished ()
                                                    | ResourceOutcome.Succeeded pixels ->
                                                        try
                                                            launch (fun () ->
                                                                let result =
                                                                    try
                                                                        ResourceOutcome.Succeeded (encode pixels options)
                                                                    with error ->
                                                                        ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message)

                                                                host.TryPostToOwner (fun () ->
                                                                    try
                                                                        if
                                                                            disposed
                                                                            || not (obj.ReferenceEquals (document, host.Document))
                                                                        then
                                                                            arbiter.Cancel ()
                                                                        else
                                                                            arbiter.Settle result |> ignore
                                                                    finally
                                                                        finished ())
                                                                |> ignore)
                                                        with error ->
                                                            try
                                                                arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message))
                                                                |> ignore
                                                            finally
                                                                finished ()
                                            )
                                        )
                            with error ->
                                try
                                    arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed error.Message))
                                    |> ignore
                                finally
                                    finished ()
                }

            live.Add (arbiter :> ResourceOperation<_>)
            |> ignore

            try
                capacity.Admit (preview, request)
            with error ->
                live.Remove (arbiter :> ResourceOperation<_>)
                |> ignore

                raise error

            arbiter :> ResourceOperation<_>

        let cancelAll () =
            for operation in List.ofSeq live do
                operation.Cancel ()

        let hostDisposal =
            host.Disposed.Subscribe (fun () ->
                disposed <- true
                cancelAll ())

        let replacement = host.DocumentReplaced.Subscribe (fun () -> cancelAll ())

        { new BackgroundSnapshotService with
            member _.Available = true

            member _.SupportsColorSpace colorSpace =
                check ()
                raw.SupportsColorSpace colorSpace

            member _.SupportsEncoding encoding =
                check ()
                raw.SupportsEncoding encoding

            member _.Capture(subject, options, terminal) =
                capture false subject options terminal

            member _.Preview(subject, options, terminal) =
                capture true subject options terminal

            member _.Dispose() =
                hostDisposal.Dispose ()
                replacement.Dispose ()
                disposed <- true
                cancelAll ()
                raw.Dispose ()
        }

    let internal create host launch =
        createWithBudget host launch (64L * 1024L * 1024L)

    /// <summary>Creates a snapshot service with a raw-pixel byte ceiling checked before painting.</summary>
    /// <remarks>Uncropped elements reserve the native 64 MiB ceiling because paint bounds can include overflow and shadows.
    /// Cropped subjects use conservative rounded bounds. Codec workspace and encoded output are additional memory.</remarks>
    /// <param name="maximumRawBytes">A positive ceiling no greater than 67108864 bytes.</param>
    /// <param name="host">The host whose owner thread creates and uses the service.</param>
    let forHostWithByteBudget maximumRawBytes (host: WebCoreHost) =
        createWithBudget host (fun work -> Task.Run (Action work) |> ignore) maximumRawBytes

    /// <summary>Creates an owner-thread service; Capture rejects while this host's encoder is busy.</summary>
    /// <remarks>Capture paints immediately. Preview may paint later. Encoding receives copied pixels only;
    /// cancellation suppresses delivery but retains capacity until the codec returns. Maximum pixels: 64 MiB.</remarks>
    /// <param name="host">The host whose creating thread starts and cancels operations.</param>
    let forHost (host: WebCoreHost) =
        create host (fun work -> Task.Run (Action work) |> ignore)
