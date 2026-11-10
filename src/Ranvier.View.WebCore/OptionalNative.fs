namespace Ranvier.View.WebCore

open System.Runtime.InteropServices

// Optional ABI 66/67 entry points, gated by rv_abi_has. Keep the public interop package dependency
// compatible with older runtimes until the coordinated native package release is available.
module internal OptionalNative =
    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_snapshot_bounded(
        nativeint host,
        nativeint element,
        nativeint options,
        nativeint completion,
        nativeint context,
        uint64& operation
    )

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_ime_surrounding_window(
        nativeint host,
        int max_units,
        byte[] buffer,
        int capacity,
        int[] length,
        int& origin,
        int& cursor,
        int& anchor,
        int& state,
        uint64& token
    )

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_ime_delete_surrounding_window(nativeint host, uint64 token, int start, int finish)

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_layout_count(nativeint host, uint32& count)

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_configure_gpu_pipeline(nativeint host, nativeint consumer_device)

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_submit_gpu_frame(nativeint host, uint64 frame_id)

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_host_try_acquire_completed_gpu_frame(nativeint host, uint64& frame_id, nativeint& frame)

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_image_encode_rgba(
        byte[] pixels,
        int64 length,
        int width,
        int height,
        int color_space,
        int encoding,
        double quality,
        nativeint& result
    )

    [<DllImport("RanvierWebCoreHost")>]
    extern int rv_encoded_image_bytes(nativeint image, byte[] buffer, int64 capacity, int64& length)

    [<DllImport("RanvierWebCoreHost")>]
    extern void rv_encoded_image_release(nativeint image)
