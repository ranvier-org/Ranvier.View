namespace Ranvier.View.WebCore

open System

/// <summary>Cooperative limits checked between complete posted-work checkpoints.</summary>
type OwnerTurnBudget =
    {
        MaxCallbacks: int
        MaxMilliseconds: float
    }

    static member Default =
        {
            MaxCallbacks = 64
            MaxMilliseconds = 2.0
        }

    member this.Validate() =
        if
            this.MaxCallbacks <= 0
            || not (Double.IsFinite this.MaxMilliseconds)
            || this.MaxMilliseconds <= 0.0
        then
            invalidArg "budget" "Callback and time limits must be positive and finite."

type OwnerTurnResult =
    {
        Executed: int
        Remaining: bool
        BudgetExceeded: bool
    }

type internal TurnLimit(budget: OwnerTurnBudget, clock: unit -> int64, frequency: int64) =
    do budget.Validate ()
    let start = clock ()
    let mutable count = 0

    member _.Complete() =
        count <- count + 1

    member _.Executed = count

    member _.Exceeded =
        count >= budget.MaxCallbacks
        || float (clock () - start) * 1000.0
           / float frequency
           >= budget.MaxMilliseconds
