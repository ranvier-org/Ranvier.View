module Responsiveness.ViewportModel

open System.Collections.Generic

let viewportKeys total first visible =
    [| max 0 (first - 50) .. min (total - 1) (first + visible + 49) |]

type MeasurementKey =
    {
        Text: string
        Font: string
        Width: int
        StyleRevision: int64
    }

type MeasurementEnvironment =
    {
        FontRevision: int64
        DeviceScale: float
        Wrap: bool
        Width: int
        Height: int
    }

type MeasurementCache(capacity: int) =
    let values = Dictionary<MeasurementKey, float>()
    let mutable environment: MeasurementEnvironment option = None

    do
        if capacity < 1 then
            invalidArg "capacity" "positive capacity required"

    member _.Configure value =
        if environment <> Some value then
            values.Clear ()
            environment <- Some value

    member _.Count = values.Count

    member _.Measure(key, measure: unit -> float) =
        match values.TryGetValue key with
        | true, value -> value
        | _ ->
            let value = measure ()

            if values.Count >= capacity then
                values.Clear ()

            values[key] <- value
            value
