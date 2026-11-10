namespace Ranvier.View.WebCore

open System
open System.Diagnostics
open System.Threading

/// <summary>Owner-thread work measured by the scheduling trace.</summary>
type SchedulingPhase =
    | Input = 0
    | Posted = 1
    | Microtasks = 2
    | NativeCycle = 3
    | Releases = 4
    | Timers = 5
    | Frame = 6
    | Present = 7
    | PresentCompleted = 8
    | EventDrain = 9
    | QueueDelay = 10
    | BudgetExceeded = 11
    | Mutation = 12
    | Measurement = 13
    | Presentation = 14

/// <summary>A phase on the Stopwatch clock; durations are ticks, not milliseconds.</summary>
[<Struct>]
type SchedulingEvent =
    {
        HostId: int64
        Sequence: int64
        FrameId: int64
        Phase: SchedulingPhase
        Started: int64
        Duration: int64
        Pending: int
        Count: int
    }

    override this.ToString() =
        if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            let invariant = System.Globalization.CultureInfo.InvariantCulture

            System.String.Concat
                [|
                    "{ HostId = "
                    this.HostId.ToString (invariant)
                    "; Sequence = "
                    this.Sequence.ToString (invariant)
                    "; FrameId = "
                    this.FrameId.ToString (invariant)
                    "; Phase = "
                    this.Phase.ToString ()
                    "; Started = "
                    this.Started.ToString (invariant)
                    "; Duration = "
                    this.Duration.ToString (invariant)
                    "; Pending = "
                    this.Pending.ToString (invariant)
                    "; Count = "
                    this.Count.ToString (invariant)
                    " }"
                |]

type internal HostSchedulingTrace() =
    static let mutable nextHost = 0L
    let hostId = Interlocked.Increment &nextHost
    let mutable events: SchedulingEvent array = Array.empty
    let mutable count = 0
    let mutable sequence = 0L
    let mutable dropped = 0L
    let mutable frame = 0L

    member _.Enabled = events.Length <> 0
    member _.FrameId = frame

    member _.NextFrame() =
        frame <- frame + 1L

    member _.SetEnabled enabled =
        events <- if enabled then Array.zeroCreate 4096 else Array.empty
        count <- 0
        dropped <- 0L

    member _.Record(phase, started, duration, pending, ?processed, ?frameId) =
        if events.Length <> 0 then
            sequence <- sequence + 1L

            if count = events.Length then
                dropped <- dropped + 1L
            else
                events[count] <-
                    {
                        HostId = hostId
                        Sequence = sequence
                        FrameId = defaultArg frameId frame
                        Phase = phase
                        Started = started
                        Duration = duration
                        Pending = pending
                        Count = defaultArg processed 0
                    }

                count <- count + 1

    member this.Start() =
        if this.Enabled then Stopwatch.GetTimestamp () else 0L

    member this.Finish(phase, started, pending, ?processed, ?frameId) =
        if started <> 0L then
            this.Record (phase, started, Stopwatch.GetTimestamp () - started, pending, ?processed = processed, ?frameId = frameId)

    member _.Drain() =
        let result = events |> Array.take count
        let lost = dropped
        count <- 0
        dropped <- 0L
        struct (result, lost)
