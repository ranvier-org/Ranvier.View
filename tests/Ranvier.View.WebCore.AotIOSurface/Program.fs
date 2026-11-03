module Ranvier.View.WebCore.Tests.IOSurfaceAotMain

open System
open System.Runtime.CompilerServices
open Ranvier.View.WebCore

[<EntryPoint; STAThread>]
let main _ =
    Console.WriteLine (
        "DYNAMIC_CODE_SUPPORTED="
        + RuntimeFeature.IsDynamicCodeSupported.ToString ()
    )

    if RuntimeFeature.IsDynamicCodeSupported then
        invalidOp "This executable must be a native AOT image"

    if
        not (
            OperatingSystem.IsMacOS ()
            && WebCoreHost.Has "iosurface-frames"
            && WebCoreHost.Has "iosurface-input"
        )
    then
        invalidOp "This profile requires macOS IOSurface GPU input and output"

    for name, action in
        [
            "input-admission", IOSurfaceChecks.inputAdmission
            "fractional-frames", IOSurfaceChecks.fractionalFrames
            "native-input", IOSurfaceChecks.nativeInput
            "cleanup-retry", IOSurfaceChecks.cleanupRetry
        ] do
        let before = IOSurfaceChecks.assertionCount ()
        action ()

        Console.WriteLine (
            "IOSURFACE_CASE_PASS "
            + name
            + " assertions="
            + (IOSurfaceChecks.assertionCount () - before).ToString()
        )

    let count = IOSurfaceChecks.assertionCount ()

    if count <> 53 then
        invalidOp "All 53 existing applicable IOSurface assertions must execute"

    Console.WriteLine (
        "IOSURFACE_STATIC_PASS cases=4 unsupported=1 assertions="
        + count.ToString ()
    )

    0
