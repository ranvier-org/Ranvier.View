namespace Ranvier.View.WebCore.Sdl

/// <summary>Retains one owner-thread GPU completion marker across nonblocking retries.</summary>
type internal CompletionFence(start: unit -> nativeint, ready: nativeint -> bool, wait: nativeint -> unit, release: nativeint -> unit) =
    let mutable pending = 0n

    let ensure () =
        if pending = 0n then
            pending <- start ()

            if pending = 0n then
                invalidOp "GPU completion marker creation failed"

    member _.TryComplete() =
        ensure ()

        if ready pending then
            release pending
            pending <- 0n
            true
        else
            false

    member this.Complete() =
        ensure ()
        wait pending

        if not (this.TryComplete ()) then
            invalidOp "GPU completion marker did not finish"
