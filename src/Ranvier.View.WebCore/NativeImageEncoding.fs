namespace Ranvier.View.WebCore

open Ranvier.Dom

/// <summary>Encoding values passed to native canvas and snapshot calls.</summary>
[<RequireQualifiedAccess>]
module internal NativeImageEncoding =
    /// <summary>Returns the <c>RV_IMAGE_ENCODING_*</c> value of an encoding.</summary>
    let code =
        function
        | ImageEncoding.Rgba8 -> 0
        | ImageEncoding.Png -> 1
        | ImageEncoding.Jpeg -> 2
        | ImageEncoding.Webp -> 3
