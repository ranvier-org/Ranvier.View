module Responsiveness.LatestAnalysis

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks

let private concurrency = new SemaphoreSlim (max 1 (Environment.ProcessorCount / 2))

type LatestAnalysis<'Request, 'Result>(analyze: 'Request -> CancellationToken -> 'Result, publish: 'Result -> unit) =
    let gate = obj ()
    let mutable superseded = 0L
    let mutable submitted = 0L
    let mutable completed = 0L
    let mutable cancelled = 0L
    let mutable failures = 0L

    let options =
        BoundedChannelOptions (1, FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true)

    let channel =
        Channel.CreateBounded<'Request>(options, Action<'Request>(fun _ -> Interlocked.Increment (&superseded) |> ignore))

    let mutable stopped = false
    let mutable active: CancellationTokenSource option = None

    let cancel (source: CancellationTokenSource) =
        try
            source
                .CancelAsync()
                .ContinueWith(
                    (fun (task: Task) ->
                        if task.IsFaulted then
                            task.Exception |> ignore),
                    TaskScheduler.Default
                )
            |> ignore
        with :? ObjectDisposedException ->
            ()

    let consume () : Task<unit> =
        task {
            let mutable running = true

            while running do
                let! available = channel.Reader.WaitToReadAsync().AsTask()
                running <- available

                if available then
                    let mutable request = Unchecked.defaultof<'Request>

                    if channel.Reader.TryRead (&request) then
                        use cancellation = new CancellationTokenSource ()

                        let run =
                            lock gate (fun () ->
                                if stopped then
                                    false
                                else
                                    active <- Some cancellation
                                    true)

                        if run then
                            try
                                do! concurrency.WaitAsync (cancellation.Token)

                                try
                                    let result = analyze request cancellation.Token

                                    if
                                        cancellation.IsCancellationRequested
                                        || lock gate (fun () -> stopped)
                                    then
                                        Interlocked.Increment (&cancelled) |> ignore
                                    else
                                        publish result
                                        Interlocked.Increment (&completed) |> ignore
                                finally
                                    concurrency.Release () |> ignore
                            with
                            | :? OperationCanceledException -> Interlocked.Increment (&cancelled) |> ignore
                            | _ -> Interlocked.Increment (&failures) |> ignore
                        else
                            Interlocked.Increment (&cancelled) |> ignore

                        lock gate (fun () -> active <- None)
        }

    let completion: Task = Task.Run (Func<Task>(fun () -> consume () :> Task))

    member _.Publish(request) =
        let accepted, previous =
            lock gate (fun () ->
                if stopped then
                    false, None
                else
                    let accepted = channel.Writer.TryWrite request

                    if accepted then
                        Interlocked.Increment (&submitted) |> ignore

                    accepted, (if accepted then active else None))

        previous |> Option.iter cancel
        accepted

    member _.Complete() =
        channel.Writer.TryComplete () |> ignore

    member _.Completion = completion
    member _.Failures = Interlocked.Read &failures
    member _.Submitted = Interlocked.Read &submitted
    member _.Completed = Interlocked.Read &completed
    member _.Superseded = Interlocked.Read &superseded
    member _.Cancelled = Interlocked.Read &cancelled
    member _.Pending = channel.Reader.Count

    interface IDisposable with
        member _.Dispose() =
            let previous =
                lock gate (fun () ->
                    stopped <- true
                    channel.Writer.TryComplete () |> ignore
                    active)

            previous |> Option.iter cancel
