module UserTimingNative

open System
open System.Threading
open System.Threading.Tasks
open Ranvier.Dom
open Ranvier.View.WebCore

let run () =
    let deterministic = NativeApiConformance.UserTiming.deterministic ()

    if deterministic <> 181 then
        failwith $"Deterministic timing fixture count changed: {deterministic}"

    use host = WebCoreHost.Create (320, 240)
    let shared = NativeApiConformance.UserTiming.run (fun () -> Timing.forHost host)

    if shared <> 180 then
        failwith $"Shared timing fixture count changed: {shared}"

    let mutable life = 0

    let check label condition =
        life <- life + 1

        if not condition then
            failwith label

    use delayedHost = WebCoreHost.Create (100, 100)
    Thread.Sleep 25
    let mutable firstFrame = -1.

    delayedHost.RequestAnimationFrame (fun time -> firstFrame <- time)
    |> ignore

    delayedHost.Frame ()
    use delayed = Timing.forHost delayedHost
    check "Late timing acquisition restarted the host frame clock" (firstFrame >= 20. && delayed.Now () >= firstFrame)
    let delayedOrigin = delayed.TimeOrigin
    let beforeFrame = delayed.Now ()
    let mutable secondFrame = -1.

    delayedHost.RequestAnimationFrame (fun time -> secondFrame <- time)
    |> ignore

    delayedHost.Frame ()

    check
        "Timing and frame callbacks use different domains"
        (secondFrame >= beforeFrame
         && secondFrame <= delayed.Now ())

    check "Timing origin changed after a frame" (delayed.TimeOrigin = delayedOrigin)
    (delayedHost :> IDisposable).Dispose()
    use service = Timing.forHost host

    let operations =
        [|
            (fun () -> service.Now () |> ignore)
            (fun () -> service.TimeOrigin |> ignore)
            (fun () ->
                service.Mark ("foreign-thread", TimingMarkOptions.Default)
                |> ignore)
            (fun () ->
                service.Measure ("foreign-thread", TimingMeasureOptions.Default)
                |> ignore)
            (fun () -> service.GetEntries () |> ignore)
            (fun () -> service.ClearMarks ())
            (fun () -> service.ClearMeasures ())
        |]

    for operation in operations do
        let mutable rejected = false

        let worker =
            Thread (
                ThreadStart (fun () ->
                    try
                        operation ()
                    with :? InvalidOperationException ->
                        rejected <- true)
            )

        worker.Start ()
        worker.Join ()
        check "Host timing operation accepted a foreign thread" rejected

    use foreignHost = WebCoreHost.Create (100, 100)
    use foreign = Timing.forHost foreignHost

    service.Mark ("only-own-host", TimingMarkOptions.Default)
    |> ignore

    check "Separate hosts shared a timing buffer" (foreign.GetEntries().Length = 0)
    service.ClearMarks ()
    (foreignHost :> IDisposable).Dispose()
    let session = host.Session
    (host :> IDisposable).Dispose()

    for operation in operations do
        let mutable rejected = false

        try
            operation ()
        with :? ObjectDisposedException ->
            rejected <- true

        check "Host timing operation accepted a disposed host" rejected

    check "Timing provider retained native wrappers" (session.LiveObjects = 0)

    let clock =
        { new TimingClock with
            member _.TimeOrigin = 1_700_000_000_000.
            member _.Now() = 100.
        }

    let timeline = TimingTimeline (clock)

    Parallel.For (
        0,
        500,
        fun index ->
            use producer = timeline.CreateService ()

            producer.Mark (string (index % 5), TimingMarkOptions.Default)
            |> ignore
    )
    |> ignore

    use snapshot = timeline.CreateService ()
    check "Concurrent duplicate marks were lost" (snapshot.GetEntries().Length = 500)

    for name in 0..4 do
        check
            "Concurrent duplicate-name entries were merged"
            (snapshot.GetEntries ()
             |> Array.filter (fun entry -> entry.Name = string name)
             |> Array.length
                =
                100)

    if life <> 25 then
        failwith $"Native timing lifecycle count changed: {life}"

    Console.WriteLine
        $"NATIVE_API H02 PASS assertions={shared} deterministic={deterministic} lifecycle={life} concurrent=500 teardown={session.LiveObjects}"
