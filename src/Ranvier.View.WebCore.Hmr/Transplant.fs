/// Moves state from an old instance record into a fresh one: Signal values by field name.
module Ranvier.View.WebCore.Hmr.Transplant

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open Ranvier

let private signalDef = typedefof<Signal<int>>

let isSignal (t: Type) =
    not (isNull t)
    && t.IsGenericType
    && t.GetGenericTypeDefinition () = signalDef

type Outcome =
    {
        mutable Copied: int
        mutable Converted: int
        Skipped: ResizeArray<string>
    }

    static member Create() =
        {
            Copied = 0
            Converted = 0
            Skipped = ResizeArray ()
        }

let private inst =
    BindingFlags.Instance
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

let private peek (signalType: Type) (signal: obj) =
    match signalType.GetProperty ("Peek", inst) with
    | null -> signalType.GetProperty("Value", inst).GetValue signal
    | p -> p.GetValue signal

let private write (signalType: Type) (signal: obj) (value: obj) =
    signalType.GetProperty("Value", inst).SetValue(signal, value)

/// A value whose type came from another load context (or another FSI interaction) crosses as JSON.
let private convert (value: obj) (target: Type) : obj voption * bool =
    if isNull value then
        ValueSome null, false
    elif target.IsInstanceOfType value then
        ValueSome value, false
    else
        try
            let json = Text.Json.JsonSerializer.Serialize (value, value.GetType ())
            ValueSome (Text.Json.JsonSerializer.Deserialize (json, target)), true
        with _ ->
            ValueNone, false

let copyInto (outcome: Outcome) (oldInst: obj) (fresh: obj) =
    if not (isNull oldInst || isNull fresh) then
        let ot, nt = oldInst.GetType (), fresh.GetType ()

        if
            FSharpType.IsRecord (ot, true)
            && FSharpType.IsRecord (nt, true)
        then
            let olds =
                FSharpType.GetRecordFields (ot, true)
                |> Array.map (fun p -> p.Name, p)
                |> dict

            for nf in FSharpType.GetRecordFields (nt, true) do
                if isSignal nf.PropertyType then
                    match olds.TryGetValue nf.Name with
                    | true, ofield when isSignal ofield.PropertyType ->
                        let oldSignal = ofield.GetValue oldInst
                        let freshSignal = nf.GetValue fresh
                        let value = peek ofield.PropertyType oldSignal

                        match convert value (nf.PropertyType.GetGenericArguments().[0]) with
                        | ValueSome v, converted ->
                            write nf.PropertyType freshSignal v
                            outcome.Copied <- outcome.Copied + 1

                            if converted then
                                outcome.Converted <- outcome.Converted + 1
                        | ValueNone, _ -> outcome.Skipped.Add $"{nt.Name}.{nf.Name}"
                    | _ -> ()
