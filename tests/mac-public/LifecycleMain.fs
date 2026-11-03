module Ranvier.View.WebCore.Tests.LifecycleMain

open System
open System.Runtime.CompilerServices

[<EntryPoint; STAThread>]
let main _ =
    Console.WriteLine (
        "DYNAMIC_CODE_SUPPORTED="
        + RuntimeFeature.IsDynamicCodeSupported.ToString ()
    )

    let host = CleanupFaultChecks.runHost ()
    let window = CleanupFaultChecks.runWindow ()
    let common = LifecycleChecks.run ()

    Console.WriteLine (
        "LIFECYCLE_PASS host="
        + host.ToString ()
        + " window="
        + window.ToString ()
        + " common="
        + common.ToString ()
    )

    0
