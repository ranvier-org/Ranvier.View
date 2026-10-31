namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Media query evaluation of a native host, with the preferences the host supplies.</summary>
module MediaQueries =
    // RvMediaPreferences: size, color_scheme, reduced_motion, contrast, pointer, hover, any_pointer, any_hover, text_scale.
    let private Size = 36

    let private pointerValue =
        function
        | None -> 0
        | Some PointerAccuracy.NoPointer -> 1
        | Some PointerAccuracy.Coarse -> 2
        | Some PointerAccuracy.Fine -> 3
        | Some PointerAccuracy.CoarseAndFine -> 4

    let private pointerOf =
        function
        | 1 -> Some PointerAccuracy.NoPointer
        | 2 -> Some PointerAccuracy.Coarse
        | 3 -> Some PointerAccuracy.Fine
        | 4 -> Some PointerAccuracy.CoarseAndFine
        | _ -> None

    let private flag (value: bool option) =
        match value with
        | None -> 0
        | Some false -> 1
        | Some true -> 2

    let private flagOf =
        function
        | 1 -> Some false
        | 2 -> Some true
        | _ -> None

    let private encode (preferences: MediaPreferences) =
        [|
            Size
            (match preferences.ColorScheme with
             | None -> 0
             | Some ColorSchemePreference.Light -> 1
             | Some ColorSchemePreference.Dark -> 2)
            (match preferences.ReducedMotion with
             | None -> 0
             | Some MotionPreference.NoPreference -> 1
             | Some MotionPreference.Reduce -> 2)
            (match preferences.Contrast with
             | None -> 0
             | Some ContrastPreference.NoPreference -> 1
             | Some ContrastPreference.More -> 2)
            pointerValue preferences.Pointer
            flag preferences.Hover
            pointerValue preferences.AnyPointer
            flag preferences.AnyHover
            BitConverter.SingleToInt32Bits (
                match preferences.TextScale with
                | Some scale -> float32 scale
                | None -> 0.0f
            )
        |]

    let private decode (values: int[]) : MediaPreferences =
        let scale = BitConverter.Int32BitsToSingle values[8]

        {
            ColorScheme =
                match values[1] with
                | 1 -> Some ColorSchemePreference.Light
                | 2 -> Some ColorSchemePreference.Dark
                | _ -> None
            ReducedMotion =
                match values[2] with
                | 1 -> Some MotionPreference.NoPreference
                | 2 -> Some MotionPreference.Reduce
                | _ -> None
            Contrast =
                match values[3] with
                | 1 -> Some ContrastPreference.NoPreference
                | 2 -> Some ContrastPreference.More
                | _ -> None
            Pointer = pointerOf values[4]
            Hover = flagOf values[5]
            AnyPointer = pointerOf values[6]
            AnyHover = flagOf values[7]
            TextScale = if scale = 0.0f then None else Some (float scale)
        }

    let private call (host: WebCoreHost) (values: int[]) (operation: nativeint -> nativeint -> int) name =
        let memory = Marshal.AllocHGlobal Size

        try
            Marshal.Copy (values, 0, memory, values.Length)

            operation host.Session.Host memory
            |> Interop.check name

            let result = Array.zeroCreate<int> values.Length
            Marshal.Copy (memory, result, 0, result.Length)
            result
        finally
            Marshal.FreeHGlobal memory

    let private supply (host: WebCoreHost) (preferences: MediaPreferences) =
        host.EnsureActive ()
        Ranvier.Dom.MediaQueries.validate preferences

        call host (encode preferences) (fun h p -> Native.rv_host_set_media_preferences (h, p)) "rv_host_set_media_preferences"
        |> ignore

    let private current (host: WebCoreHost) () =
        host.EnsureActive ()
        let values = Array.zeroCreate<int> 9
        values[0] <- Size

        call host values (fun h p -> Native.rv_host_media_preferences (h, p)) "rv_host_media_preferences"
        |> decode

    /// <summary>
    /// The media features whose user preference the engine has no input for: WebCore evaluates forced-colors as
    /// none and prefers-contrast less and custom as never matching, and has no reduced-transparency or reduced-data
    /// feature.
    /// </summary>
    let unavailable =
        [
            "forced-colors"
            "prefers-contrast: less"
            "prefers-contrast: custom"
            "prefers-reduced-transparency"
            "prefers-reduced-data"
        ]

    /// <summary>The provider over <c>host</c>'s window and media preferences.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the "media-preferences" functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let provider (host: WebCoreHost) : MediaQueryProvider =
        let window = Viewport.provider host

        if not (WebCoreHost.Has "media-preferences") then
            raise (NotSupportedException "The native library has no media preference functions")

        {
            MatchMedia = window.MatchMedia
            Preferences =
                Some
                    {
                        Supply = supply host
                        Current = current host
                        Unavailable = unavailable
                    }
        }

    /// <summary>A media query service for <c>host</c>'s current document.</summary>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : MediaQueryService =
        Ranvier.Dom.MediaQueries.create (provider host)
