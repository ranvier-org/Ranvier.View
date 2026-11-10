namespace Ranvier.View.WebCore.Sdl

/// <summary>Reports a terminal GPU command failure after execution has stopped.</summary>
type internal GpuCompletionFailure(message: string) =
    inherit System.InvalidOperationException(message)

/// <summary>Retains one owner-thread GPU completion marker across nonblocking retries.</summary>
type internal CompletionFence(start: unit -> nativeint, ready: nativeint -> bool, wait: nativeint -> unit, release: nativeint -> unit) =
    let mutable pending = 0n
    let mutable failed = false

    let ensure () =
        if pending = 0n then
            pending <- start ()

            if pending = 0n then
                invalidOp "GPU completion marker creation failed"

    member _.TryComplete() =
        if failed then
            raise (GpuCompletionFailure "GPU execution failed")

        ensure ()

        try
            if ready pending then
                release pending
                pending <- 0n
                true
            else
                false
        with :? GpuCompletionFailure ->
            release pending
            pending <- 0n
            failed <- true
            reraise ()

    member this.Complete() =
        if not failed then
            ensure ()
            wait pending

            try
                if not (this.TryComplete ()) then
                    invalidOp "GPU completion marker did not finish"
            with :? GpuCompletionFailure ->
                ()

    member _.Reset() =
        if pending <> 0n then
            invalidOp "A GPU marker is still pending"

        failed <- false
