module internal NativeApiConformance.UserTiming

open System
open Ranvier.Dom

/// <summary>Checks shared clock, entry, option, copy, error and clear semantics.</summary>
let run (create: unit -> TimingService) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let rejected expected action =
        let mutable result = "no error"

        try
            action ()
        with TimingException (name, _) ->
            result <- name

        equal "timing error" expected result

    use service = create ()
    use peer = create ()
    service.ClearMarks ()
    service.ClearMeasures ()
    let origin = service.TimeOrigin
    equal "Unix millisecond origin" true (origin > 1_000_000_000_000.)
    equal "providers share origin" origin peer.TimeOrigin
    let mutable previous = service.Now ()

    for _ in 1..100 do
        let current = service.Now ()
        equal "monotonic nonnegative clock" true (current >= previous && current >= 0.)
        previous <- current

    let mark name time =
        service.Mark (
            name,
            { TimingMarkOptions.Default with
                StartTime = Some time
            }
        )

    let first = mark "h02-duplicate" 10.
    let latest = mark "h02-duplicate" 30.
    equal "mark name" "h02-duplicate" latest.Name
    equal "mark kind" TimingEntryKind.Mark latest.Kind
    equal "mark duration" 0. latest.Duration
    equal "first duplicate retained" 10. first.StartTime
    equal "shared mark domain" 2 (peer.GetEntries().Length)

    let fromMarks =
        service.Measure (
            "h02-latest",
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark "h02-duplicate"
                End = TimingEndpoint.Timestamp 40.
            }
        )

    equal "latest duplicate endpoint" 30. fromMarks.StartTime
    equal "latest duplicate duration" 10. fromMarks.Duration
    equal "measure kind" TimingEntryKind.Measure fromMarks.Kind
    mark "h02-insertion-order" 90. |> ignore
    mark "h02-insertion-order" 2. |> ignore

    let latestInserted =
        service.Measure (
            "h02-insertion-order-measure",
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark "h02-insertion-order"
                End = TimingEndpoint.Timestamp 40.
            }
        )

    equal "named mark follows browser timestamp ordering" 90. latestInserted.StartTime
    equal "out-of-order named mark duration" -50. latestInserted.Duration
    let empty = mark "" 0.
    equal "empty mark name retained" "" empty.Name
    service.ClearMarks ("")

    equal
        "empty name clear is selective"
        false
        (service.GetEntries ()
         |> Array.exists (fun entry -> entry.Name = ""))

    let measure name options =
        service.Measure (name, options)

    let fromOrigin =
        measure
            "h02-origin"
            { TimingMeasureOptions.Default with
                End = TimingEndpoint.Timestamp 25.
            }

    equal "omitted start uses zero" 0. fromOrigin.StartTime
    equal "omitted start duration" 25. fromOrigin.Duration

    let startDuration =
        measure
            "h02-start-duration"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Timestamp 5.
                Duration = Some 7.
            }

    equal "start plus duration" 5. startDuration.StartTime
    equal "explicit duration" 7. startDuration.Duration

    let endDuration =
        measure
            "h02-end-duration"
            { TimingMeasureOptions.Default with
                End = TimingEndpoint.Timestamp 5.
                Duration = Some 7.
            }

    equal "end minus duration permits negative start" -2. endDuration.StartTime
    equal "end duration" 7. endDuration.Duration

    let reversed =
        measure
            "h02-reversed"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Timestamp 12.
                End = TimingEndpoint.Timestamp 3.
            }

    equal "reversed endpoints retain negative duration" -9. reversed.Duration
    let before = service.Now ()
    let current = measure "h02-now" TimingMeasureOptions.Default
    let after = service.Now ()
    equal "default measure starts at origin" 0. current.StartTime

    equal
        "default measure ends at current clock"
        true
        (current.Duration >= before
         && current.Duration <= after)

    let payload =
        [|
            TimingDetail.Text "before"
            TimingDetail.Number nan
            TimingDetail.Boolean true
        |]

    let nested =
        [|
            "__proto__", TimingDetail.Array payload
            "constructor", TimingDetail.Null
            "text", TimingDetail.Text "a\000b😀"
        |]

    let copied =
        service.Mark (
            "h02-detail",
            { TimingMarkOptions.Default with
                StartTime = Some 50.
                Detail = TimingDetail.Object nested
            }
        )

    payload[0] <- TimingDetail.Text "after"
    nested[1] <- "changed", TimingDetail.Null

    let inspect detail =
        match detail with
        | TimingDetail.Object fields ->
            equal "reserved object key copied" "__proto__" (fst fields[0])
            equal "object key retained" "constructor" (fst fields[1])
            equal "NUL and Unicode detail retained" (TimingDetail.Text "a\000b😀") (snd fields[2])

            match snd fields[0] with
            | TimingDetail.Array values ->
                equal "source detail cloned" (TimingDetail.Text "before") values[0]

                equal
                    "NaN detail retained"
                    true
                    (match values[1] with
                     | TimingDetail.Number n -> Double.IsNaN n
                     | _ -> false)

                values[0] <- TimingDetail.Text "returned-mutation"
            | _ -> failwith "Missing nested detail array"
        | _ -> failwith "Missing detail object"

    inspect copied.Detail

    service.GetEntries ()
    |> Array.find (fun entry -> entry.Name = "h02-detail")
    |> fun entry -> inspect entry.Detail

    let measuredDetail =
        measure
            "h02-measure-detail"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Timestamp 0.
                End = TimingEndpoint.Timestamp 1.
                Detail = TimingDetail.Text "measure"
            }

    equal "measure detail" (TimingDetail.Text "measure") measuredDetail.Detail
    let baseline = service.GetEntries().Length

    for value in [| -1.; nan; infinity; -infinity |] do
        rejected "TypeError" (fun () -> mark "h02-invalid" value |> ignore)

        rejected "TypeError" (fun () ->
            measure
                "h02-invalid"
                { TimingMeasureOptions.Default with
                    Start = TimingEndpoint.Timestamp value
                }
            |> ignore)

        rejected "TypeError" (fun () ->
            measure
                "h02-invalid"
                { TimingMeasureOptions.Default with
                    Start = TimingEndpoint.Timestamp 0.
                    Duration = Some value
                }
            |> ignore)

    rejected "TypeError" (fun () ->
        measure
            "h02-invalid"
            { TimingMeasureOptions.Default with
                Duration = Some 1.
            }
        |> ignore)

    rejected "TypeError" (fun () ->
        measure
            "h02-invalid"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Timestamp 0.
                End = TimingEndpoint.Timestamp 1.
                Duration = Some 1.
            }
        |> ignore)

    rejected "SyntaxError" (fun () ->
        measure
            "h02-missing"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark "h02-missing"
            }
        |> ignore)

    rejected "NotSupportedError" (fun () -> mark "navigationStart" 0. |> ignore)
    rejected "TypeError" (fun () -> mark null 0. |> ignore)

    rejected "TypeError" (fun () ->
        service.Mark ("h02-null-options", Unchecked.defaultof<TimingMarkOptions>)
        |> ignore)

    rejected "TypeError" (fun () ->
        measure "h02-null-options" Unchecked.defaultof<TimingMeasureOptions>
        |> ignore)

    rejected "TypeError" (fun () ->
        measure null TimingMeasureOptions.Default
        |> ignore)

    rejected "TypeError" (fun () ->
        measure
            "h02-null-mark"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark null
            }
        |> ignore)

    rejected "TypeError" (fun () ->
        measure
            "h02-null-start"
            { TimingMeasureOptions.Default with
                Start = Unchecked.defaultof<TimingEndpoint>
            }
        |> ignore)

    rejected "TypeError" (fun () ->
        measure
            "h02-null-end"
            { TimingMeasureOptions.Default with
                End = Unchecked.defaultof<TimingEndpoint>
            }
        |> ignore)

    rejected "TypeError" (fun () -> service.ClearMarks (null))
    rejected "TypeError" (fun () -> service.ClearMeasures (null))
    let cycle = [| TimingDetail.Null |]
    cycle[0] <- TimingDetail.Array cycle

    rejected "DataCloneError" (fun () ->
        service.Mark (
            "h02-cycle",
            { TimingMarkOptions.Default with
                Detail = TimingDetail.Array cycle
            }
        )
        |> ignore)

    rejected "DataCloneError" (fun () ->
        service.Mark (
            "h02-duplicates",
            { TimingMarkOptions.Default with
                Detail = TimingDetail.Object [| "x", TimingDetail.Null; "x", TimingDetail.Null |]
            }
        )
        |> ignore)

    let malformedObject =
        TimingDetail.Object [| Unchecked.defaultof<string * TimingDetail> |]

    rejected "TypeError" (fun () ->
        measure
            null
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark "h02-missing"
            }
        |> ignore)

    rejected "DataCloneError" (fun () ->
        measure
            "h02-invalid-order"
            { TimingMeasureOptions.Default with
                Start = TimingEndpoint.Mark "h02-missing"
                Detail = malformedObject
            }
        |> ignore)

    rejected "DataCloneError" (fun () ->
        service.Mark (
            "h02-null-field",
            { TimingMarkOptions.Default with
                Detail = malformedObject
            }
        )
        |> ignore)

    rejected "DataCloneError" (fun () ->
        measure
            "h02-null-field"
            { TimingMeasureOptions.Default with
                Detail = malformedObject
            }
        |> ignore)

    let mutable deep = TimingDetail.Null

    for _ in 1..300 do
        deep <- TimingDetail.Array [| deep |]

    rejected "DataCloneError" (fun () ->
        service.Mark (
            "h02-deep",
            { TimingMarkOptions.Default with
                Detail = deep
            }
        )
        |> ignore)

    equal "invalid operations preserve buffer" baseline (service.GetEntries().Length)
    mark "h02-tie-one" 100. |> ignore
    mark "h02-tie-two" 100. |> ignore

    equal
        "stable tie ordering"
        [| "h02-tie-one"; "h02-tie-two" |]
        (service.GetEntries ()
         |> Array.filter (fun e -> e.StartTime = 100.)
         |> Array.map (fun e -> e.Name))

    let sorted = service.GetEntries ()

    equal
        "timestamp ordering"
        true
        (sorted
         |> Array.pairwise
         |> Array.forall (fun (a, b) -> a.StartTime <= b.StartTime))

    service.ClearMarks ("h02-duplicate")

    equal
        "all duplicate marks removed"
        false
        (service.GetEntries ()
         |> Array.exists (fun e -> e.Name = "h02-duplicate"))

    equal
        "clearing marks preserves measures"
        true
        (service.GetEntries ()
         |> Array.exists (fun e -> e.Name = "h02-latest"))

    mark "h02-both-kinds" 3. |> ignore

    measure
        "h02-both-kinds"
        { TimingMeasureOptions.Default with
            End = TimingEndpoint.Timestamp 4.
        }
    |> ignore

    service.ClearMarks ("h02-both-kinds")

    equal
        "mark clear preserves same-name measure"
        [| TimingEntryKind.Measure |]
        (service.GetEntries ()
         |> Array.filter (fun e -> e.Name = "h02-both-kinds")
         |> Array.map (fun e -> e.Kind))

    mark "h02-both-kinds" 3. |> ignore
    service.ClearMeasures ("h02-both-kinds")

    equal
        "measure clear preserves same-name mark"
        [| TimingEntryKind.Mark |]
        (service.GetEntries ()
         |> Array.filter (fun e -> e.Name = "h02-both-kinds")
         |> Array.map (fun e -> e.Kind))

    service.ClearMeasures ("h02-latest")

    equal
        "selective measure clear"
        false
        (service.GetEntries ()
         |> Array.exists (fun e -> e.Name = "h02-latest"))

    service.ClearMarks ()

    equal
        "clear all marks preserves measures"
        true
        (service.GetEntries ()
         |> Array.forall (fun e -> e.Kind = TimingEntryKind.Measure))

    service.ClearMeasures ()
    equal "reset empty buffer" 0 (service.GetEntries().Length)
    equal "reset preserves origin" origin service.TimeOrigin
    equal "reset preserves clock" true (service.Now () >= previous)
    service.Dispose ()
    rejected "InvalidStateError" (fun () -> service.Now () |> ignore)
    equal "peer survives provider disposal" origin peer.TimeOrigin
    checks

/// <summary>Checks deterministic equal-time ordering and independent provider reset.</summary>
let deterministic () =
    let mutable tick = 0.

    let timeline =
        TimingTimeline (
            { new TimingClock with
                member _.TimeOrigin = 1_700_000_000_000.
                member _.Now() = tick
            }
        )

    use service = timeline.CreateService ()
    let checks = run timeline.CreateService
    tick <- 123.25
    let mark = service.Mark ("clock", TimingMarkOptions.Default)

    if mark.StartTime <> 123.25 then
        failwith "Deterministic clock timestamp mismatch"

    checks + 1
