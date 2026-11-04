module Ranvier.View.WebCore.Tests.IOSurfaceTests

open System
open Expecto
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private requireGpu () =
    if
        not (
            OperatingSystem.IsMacOS ()
            && WebCoreHost.Has "iosurface-frames"
        )
    then
        skiptest "requires the macOS IOSurface GPU runtime"

[<Tests>]
let tests =
    testList
        "IOSurface transport"
        [
            hostCase "unavailable IOSurface input rejects before resolving its native endpoint"
            <| fun host ->
                if WebCoreHost.Has "iosurface-input" then
                    skiptest "requires a runtime without IOSurface input"

                IOSurfaceChecks.unavailableInput host

            testCase "IOSurface input rejects foreign-thread and disposed host admission"
            <| fun () -> onHostThread IOSurfaceChecks.inputAdmission

            testCase "Metal presents BGRA IOSurface frames at fractional backing scales"
            <| fun () ->
                onHostThread (fun () ->
                    requireGpu ()
                    IOSurfaceChecks.fractionalFrames ())

            testCase "public IOSurface binding presents completed native Metal input and retains its storage"
            <| fun () ->
                onHostThread (fun () ->
                    requireGpu ()

                    if not (WebCoreHost.Has "iosurface-input") then
                        skiptest "requires the macOS IOSurface GPU runtime"

                    if String.IsNullOrWhiteSpace (Environment.GetEnvironmentVariable "RANVIER_MAC_INPUT_HELPER") then
                        skiptest "set RANVIER_MAC_INPUT_HELPER to the tests/mac/build_metal_input.py output"

                    IOSurfaceChecks.nativeInput ())

            testCase "Metal completion and native release failures preserve a real frame for owner cleanup"
            <| fun () ->
                onHostThread (fun () ->
                    requireGpu ()
                    IOSurfaceChecks.cleanupRetry ())
        ]
