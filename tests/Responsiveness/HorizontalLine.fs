module Responsiveness.HorizontalLine

open System

type Projection =
    {
        Start: int
        Length: int
        TotalLength: int
        Pitch: float
    }

    member this.IsWindowed = this.Length <> this.TotalLength

    member this.LogicalColumn(offset: int) =
        this.Start + max 0 (min this.Length offset)

    member this.LocalColumn(column: int) =
        max 0 (min this.Length (column - this.Start))

let eligible (text: string) =
    text |> Seq.forall (fun c -> c >= ' ' && c <= '~')

let project (length: int) (safe: bool) (pitch: float) (width: int) (scroll: float) =
    if length < 0 then
        invalidArg "length" "negative line length"

    let full =
        {
            Start = 0
            Length = length
            TotalLength = length
            Pitch = pitch
        }

    if
        not safe
        || length <= 2048
        || width <= 0
        || not (Double.IsFinite pitch)
        || pitch <= 0.
    then
        full
    else
        let needed = Math.Ceiling (float width / pitch) + 65.

        if needed > 2048. then
            full
        else
            let visible = int needed

            let column =
                if Double.IsNaN scroll then
                    0
                else
                    int (min (float length) (max 0. (Math.Floor (scroll / pitch))))

            let first = max 0 (min (length - visible) (column - 32))

            { full with
                Start = first
                Length = min length visible
            }
