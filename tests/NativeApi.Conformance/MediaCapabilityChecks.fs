module internal NativeApiConformance.MediaCapabilityChecks

open System
open Ranvier.Dom

/// <summary>Task delivery and the engine facts the fixture needs.</summary>
type Driver =
    {
        /// <summary>Lets queued work run, so pending queries deliver.</summary>
        Settle: unit -> Async<unit>
        /// <summary>
        /// The case names the engine rejects with TypeError. WebKit and Chromium differ on bucket containers without
        /// exactly one codec, extra MIME parameters and record encoding (Ranvier.WebCore
        /// tests/fixtures/media-capabilities-cases.json).
        /// </summary>
        Rejected: Set<string>
        /// <summary>The answer every valid configuration has, when the engine has no media provider.</summary>
        Answer: MediaCapabilitiesInfo option
    }

let private video contentType =
    {
        ContentType = contentType
        Width = 640u
        Height = 360u
        Bitrate = 1000000UL
        Framerate = 30.0
        AlphaChannel = None
        HdrMetadataType = None
        ColorGamut = None
        TransferFunction = None
    }

let private audio contentType =
    {
        ContentType = contentType
        Channels = None
        Bitrate = None
        Samplerate = None
        SpatialRendering = None
    }

let private vp8 = "video/webm;codecs=\"vp8\""
let private opus = "audio/webm;codecs=\"opus\""

/// <summary>The decoding cases of Ranvier.WebCore tests/fixtures/media-capabilities-cases.json.</summary>
let private decoding =
    [
        "file avc1", MediaDecodingType.File, Some (video "video/mp4;codecs=\"avc1.42E01E\""), None
        "file vp9", MediaDecodingType.File, Some (video "video/webm;codecs=\"vp09.00.10.08\""), None
        "file vp8", MediaDecodingType.File, Some (video vp8), None
        "file av1", MediaDecodingType.File, Some (video "video/mp4;codecs=\"av01.0.04M.08\""), None
        "file opus", MediaDecodingType.File, None, Some (audio opus)
        "file aac", MediaDecodingType.File, None, Some (audio "audio/mp4;codecs=\"mp4a.40.2\"")
        "file audio and video", MediaDecodingType.File, Some (video vp8), Some (audio opus)
        "file unknown container", MediaDecodingType.File, Some (video "video/x-ranvier-unknown"), None
        "file hdr pq rec2020",
        MediaDecodingType.File,
        Some
            { video "video/webm;codecs=\"vp09.02.10.10\"" with
                HdrMetadataType = Some MediaHdrMetadataType.SmpteSt2086
                ColorGamut = Some MediaColorGamut.Rec2020
                TransferFunction = Some MediaTransferFunction.Pq
            },
        None
        "media-source vp8", MediaDecodingType.MediaSource, Some (video vp8), None
        "webrtc vp8", MediaDecodingType.WebRtc, Some (video "video/VP8"), None
        "audio full",
        MediaDecodingType.File,
        None,
        Some
            { audio opus with
                Channels = Some "2"
                Bitrate = Some 128000UL
                Samplerate = Some 48000u
                SpatialRendering = Some false
            }
        "invalid empty", MediaDecodingType.File, None, None
        "invalid bucket without codecs", MediaDecodingType.File, Some (video "video/mp4"), None
        "invalid two codecs", MediaDecodingType.File, Some (video "video/mp4;codecs=\"avc1.42E01E,mp4a.40.2\""), None
        "invalid zero framerate", MediaDecodingType.File, Some { video vp8 with Framerate = 0.0 }, None
        "invalid audio type as video", MediaDecodingType.File, Some (video opus), None
        "invalid video type as audio", MediaDecodingType.File, None, Some (audio vp8)
        "invalid empty content type", MediaDecodingType.File, Some (video ""), None
        "invalid not a mime type", MediaDecodingType.File, Some (video "not a mime type"), None
        "extra parameter", MediaDecodingType.File, Some (video "video/webm;codecs=\"vp8\";foo=bar"), None
    ]

/// <summary>The encoding cases of the same fixture.</summary>
let private encoding =
    [
        "record vp8", MediaEncodingType.Record, Some (video vp8), None
        "record opus", MediaEncodingType.Record, None, Some (audio opus)
        "webrtc vp8", MediaEncodingType.WebRtc, Some (video "video/VP8"), None
        "invalid empty", MediaEncodingType.Record, None, None
        "invalid zero framerate", MediaEncodingType.Record, Some { video vp8 with Framerate = 0.0 }, None
    ]

/// <summary>
/// Replays the media capability cases on one provider, then cancellation and disposal before delivery; the result is
/// the assertion count. A rejected case fails with <c>LoadFailed "TypeError"</c>, and every terminal arrives after the
/// starting call returns.
/// </summary>
let run (create: unit -> MediaCapabilitiesService) (driver: Driver) =
    async {
        let mutable checks = 0

        let expect label condition =
            checks <- checks + 1

            if not condition then
                failwith label

        use service = create ()

        let await label (start: (ResourceOutcome<MediaCapabilitiesInfo> -> unit) -> ResourceOperation<MediaCapabilitiesInfo>) =
            async {
                let mutable outcome = None
                use operation = start (fun value -> outcome <- Some value)
                expect (label + ": not settled inside the call") outcome.IsNone
                let mutable turns = 0

                while outcome.IsNone && turns < 400 do
                    do! driver.Settle ()
                    turns <- turns + 1

                expect (label + ": settled") outcome.IsSome
                return outcome.Value
            }

        let check label (name: string) outcome =
            match outcome with
            | ResourceOutcome.Failed failure ->
                expect (label + ": rejected") (driver.Rejected.Contains name)
                expect (label + ": TypeError") (failure = MediaCapabilitiesContract.rejected)
            | ResourceOutcome.Succeeded info ->
                expect (label + ": resolved") (not (driver.Rejected.Contains name))

                match driver.Answer with
                | Some answer -> expect (label + ": the engine's answer") (info = answer)
                | None -> expect (label + ": a smooth answer is supported") (info.Supported || not info.Smooth)

        for name, kind, videoValue, audioValue in decoding do
            let! outcome =
                await ("decoding " + name) (fun terminal -> service.DecodingInfo (kind, videoValue, audioValue, terminal))

            check ("decoding " + name) name outcome

        for name, kind, videoValue, audioValue in encoding do
            let! outcome =
                await ("encoding " + name) (fun terminal -> service.EncodingInfo (kind, videoValue, audioValue, terminal))

            check ("encoding " + name) ("encoding " + name) outcome

        // Cancelling before delivery settles Cancelled once, and nothing follows.
        let mutable cancelled = []

        let operation =
            service.DecodingInfo (MediaDecodingType.File, Some (video vp8), None, (fun value -> cancelled <- value :: cancelled))

        operation.Cancel ()

        for _ in 1..5 do
            do! driver.Settle ()

        expect "cancel settles Cancelled once" (cancelled = [ ResourceOutcome.Failed ResourceFailure.Cancelled ])
        operation.Dispose ()

        // A null content type is an argument error, before any query starts.
        let nullType =
            try
                service.DecodingInfo (MediaDecodingType.File, Some (video null), None, ignore)
                |> ignore

                false
            with _ ->
                true

        expect "null content type" nullType
        service.Dispose ()

        let disposed =
            try
                service.DecodingInfo (MediaDecodingType.File, Some (video vp8), None, ignore)
                |> ignore

                false
            with _ ->
                true

        expect "a disposed service rejects queries" disposed
        return checks
    }

/// <summary>The case names WebKit rejects, each prefixed by its kind for encoding.</summary>
let webkitRejected =
    set
        [
            "invalid empty"
            "invalid bucket without codecs"
            "invalid two codecs"
            "invalid zero framerate"
            "invalid audio type as video"
            "invalid video type as audio"
            "invalid empty content type"
            "invalid not a mime type"
            "encoding invalid empty"
            "encoding invalid zero framerate"
        ]

/// <summary>The case names Chromium 156 rejects (tests/fixtures/media-capabilities-oracle.json).</summary>
let chromiumRejected =
    set
        [
            "invalid empty"
            "invalid zero framerate"
            "invalid audio type as video"
            "invalid video type as audio"
            "invalid empty content type"
            "invalid not a mime type"
            "extra parameter"
            "encoding record vp8"
            "encoding record opus"
            "encoding invalid empty"
            "encoding invalid zero framerate"
        ]
