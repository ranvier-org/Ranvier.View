namespace Ranvier.View.WebCore

open System
open System.Runtime.CompilerServices
open Ranvier.Dom

/// <summary>Monotonic user-timing domains shared by providers for the same live host.</summary>
module Timing =
    let private domains = ConditionalWeakTable<WebCoreHost, TimingTimeline>()

    let private createDomain (host: WebCoreHost) =
        TimingTimeline (host.TimingClock)

    /// <summary>Creates a borrowed host provider; entries and origin are shared across its providers.</summary>
    /// <param name="host">The live host whose creating thread executes the operations.</param>
    let forHost (host: WebCoreHost) : TimingService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        let timeline =
            domains.GetValue (host, ConditionalWeakTable<WebCoreHost, TimingTimeline>.CreateValueCallback(createDomain))

        let service = timeline.CreateService ()

        let run action =
            host.EnsureActive ()
            action ()

        { new TimingService with
            member _.TimeOrigin = run (fun () -> service.TimeOrigin)

            member _.Now() =
                run service.Now

            member _.Mark(name, options) =
                run (fun () -> service.Mark (name, options))

            member _.Measure(name, options) =
                run (fun () -> service.Measure (name, options))

            member _.GetEntries() =
                run service.GetEntries

            member _.ClearMarks() =
                run (fun () -> service.ClearMarks ())

            member _.ClearMarks(name: string) =
                run (fun () -> service.ClearMarks name)

            member _.ClearMeasures() =
                run (fun () -> service.ClearMeasures ())

            member _.ClearMeasures(name: string) =
                run (fun () -> service.ClearMeasures name)

            member _.Dispose() =
                service.Dispose ()
        }
