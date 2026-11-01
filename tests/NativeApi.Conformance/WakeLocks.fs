module internal NativeApiConformance.WakeLocks

open System
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Checks requests, denial, the release event, signal-bound release and argument handling on one provider.</summary>
/// <remarks><c>granted</c> grants screen locks and <c>denied</c> rejects them; both belong to visible documents.</remarks>
let run (granted: WakeLockService) (denied: WakeLockService) (cancellation: CancellationService) =
    task {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith (String.Concat [| label; ": expected "; string expected; ", got "; string actual |])

        let fault (label: string) (expected: exn -> bool) (pending: Task<WakeLockSentinel>) =
            task {
                checks <- checks + 1

                let! outcome =
                    task {
                        try
                            let! _ = pending
                            return None
                        with error ->
                            return Some error
                    }

                match outcome with
                | Some error when expected error -> ()
                | Some error -> failwith (label + ": unexpected " + error.Message)
                | None -> failwith (label + ": expected a rejection")
            }

        let named name (error: exn) =
            match error with
            | WakeLockException (actual, _) -> actual = name
            | _ -> false

        let aborted (error: exn) =
            match error with
            | AbortedException _ -> true
            | _ -> false

        do! fault "Denied request" (named "NotAllowedError") (denied.Request (WakeLockType.Screen, None))
        do! fault "Unknown type" (named "TypeError") (granted.Request (enum<WakeLockType> 7, None))

        let! first = granted.Request (WakeLockType.Screen, None)
        let! second = granted.Request (WakeLockType.Screen, None)
        equal "Granted lock is held" false first.Released
        equal "Granted lock type" WakeLockType.Screen first.Type
        equal "Each request has its own sentinel" false (obj.ReferenceEquals (first, second))

        let log = ResizeArray<string>()
        use _first = first.OnRelease (fun () -> log.Add "first")
        use _second = first.OnRelease (fun () -> log.Add "second")
        let dropped = first.OnRelease (fun () -> log.Add "dropped")
        dropped.Dispose ()
        let releasing = first.Release ()
        equal "Release runs callbacks in order before returning" "first,second" (String.Join (",", log))
        equal "Released after Release returns" true first.Released
        do! releasing
        do! first.Release ()
        equal "A released sentinel runs no callback" 2 log.Count
        equal "A released sentinel stays released" true first.Released
        equal "Other locks stay held" false second.Released
        do! second.Release ()
        equal "Second lock released" true second.Released

        let controller = cancellation.CreateController ()
        let! bound = granted.Request (WakeLockType.Screen, Some controller.Signal)
        let mutable boundReleases = 0
        use _bound = bound.OnRelease (fun () -> boundReleases <- boundReleases + 1)
        equal "Signal-bound lock is held" false bound.Released
        controller.Abort ()
        equal "Abort releases the lock" true bound.Released
        equal "Abort runs the release callback once" 1 boundReleases

        do! fault "Already-aborted signal" aborted (granted.Request (WakeLockType.Screen, Some (cancellation.Abort AbortReason.Abort)))

        let! unbound =
            granted.Request (WakeLockType.Screen, Some (Unchecked.defaultof<AbortSignal>))

        equal "Some null signal reads as no signal" false unbound.Released
        do! unbound.Release ()

        checks <- checks + 1

        let rejected =
            try
                unbound.OnRelease (Unchecked.defaultof<unit -> unit>)
                |> ignore

                false
            with _ ->
                true

        if not rejected then
            failwith "A null release callback is rejected"

        return checks
    }
