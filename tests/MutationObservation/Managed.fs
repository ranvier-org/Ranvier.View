module Ranvier.View.WebCore.Tests.MutationManaged

open System
open System.Runtime.CompilerServices
open System.Runtime.ExceptionServices
open System.Threading

let private retainedOwner () =
    use completed = new ManualResetEventSlim ()
    let mutable result = 0
    let mutable failure: ExceptionDispatchInfo = null

    let owner =
        Thread (
            (fun () ->
                try
                    result <- ObservationChecks.runNative ()
                with error ->
                    failure <- ExceptionDispatchInfo.Capture error

                completed.Set ()
                Thread.Sleep Timeout.Infinite),
            64 * 1024 * 1024
        )

    owner.IsBackground <- true
    owner.Start ()
    completed.Wait ()

    if not (isNull failure) then
        failure.Throw ()

    result

[<EntryPoint>]
let main arguments =
    try
        let checks = CleanupChecks.runPure ()

        Console.WriteLine (
            "Mutation cleanup: "
            + checks.ToString ()
            + " assertions PASS"
        )

        Console.WriteLine (
            "Mutation marshalling: "
            + (ObservationChecks.runMarshalling ()).ToString()
            + " assertions PASS"
        )

        let pure =
            arguments.Length = 1
            && String.Equals (arguments[0], "--pure-only", StringComparison.Ordinal)

        if not pure then
            let windowOnly =
                arguments.Length = 1
                && String.Equals (arguments[0], "--window-dispose-only", StringComparison.Ordinal)

            let retained =
                arguments.Length = 1
                && String.Equals (arguments[0], "--retained-owner", StringComparison.Ordinal)

            Console.WriteLine (
                "Mutation native: "
                + (if windowOnly then
                       WindowCleanupChecks.runNative ()
                   elif retained then
                       retainedOwner ()
                   else
                       ObservationChecks.runNative ()
                       + WindowCleanupChecks.runNative ())
                    .ToString()
                + " assertions PASS"
            )

        Console.WriteLine (
            "DynamicCodeSupported="
            + RuntimeFeature.IsDynamicCodeSupported.ToString ()
            + ";DynamicCodeCompiled="
            + RuntimeFeature.IsDynamicCodeCompiled.ToString ()
        )

        0
    with error ->
        Console.Error.WriteLine (
            error.Message
            + Environment.NewLine
            + error.StackTrace
        )

        1
