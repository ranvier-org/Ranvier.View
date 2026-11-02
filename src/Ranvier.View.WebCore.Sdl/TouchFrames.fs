namespace Ranvier.View.WebCore.Sdl

open System
open System.Collections.Generic
open Ranvier.View.WebCore
open SDL

/// <summary>Builds complete touch frames from SDL contact events.</summary>
type internal TouchFrames() =
    let contacts = Dictionary<struct (uint64 * uint64), PointerSample>()
    let mutable nextId = 1

    /// <summary>The number of active contacts.</summary>
    member _.Count = contacts.Count

    /// <summary>Updates one contact and returns its frame in CSS pixels.</summary>
    member _.Update(device, finger, action, x, y, pressure, modifiers) =
        let key = struct (device, finger)
        let mutable previous = Unchecked.defaultof<PointerSample>
        let found = contacts.TryGetValue (key, &previous)

        if action <> 1 && not found then
            [||]
        else
            let mutable sample =
                if found then
                    previous
                else
                    PointerSample.Create (2, nextId, action, x, y)

            if not found then
                nextId <- nextId + 1

            sample.Action <- action
            sample.MovementX <- if found then x - previous.X else 0.0
            sample.MovementY <- if found then y - previous.Y else 0.0
            sample.X <- x
            sample.Y <- y
            sample.Pressure <- Math.Clamp (pressure, 0.0f, 1.0f)
            sample.Modifiers <- modifiers
            sample.Button <- if action = 1 || action = 2 then 0 else -1
            sample.Buttons <- if action = 2 || action = 3 then 0 else 1
            let frame = ResizeArray<PointerSample>()

            for KeyValue (otherKey, value) in contacts do
                if otherKey <> key then
                    let mutable stationary = value
                    stationary.Action <- 4
                    stationary.Modifiers <- modifiers
                    stationary.MovementX <- 0.0
                    stationary.MovementY <- 0.0
                    frame.Add stationary

            frame.Add sample

            if action = 2 || action = 3 then
                contacts.Remove key |> ignore
            else
                contacts[key] <- sample

            frame.ToArray ()

    /// <summary>Cancels and clears all active contacts.</summary>
    member _.Cancel(modifiers) =
        let frame =
            contacts.Values
            |> Seq.map (fun value ->
                let mutable sample = value
                sample.Action <- 3
                sample.Buttons <- 0
                sample.Modifiers <- modifiers
                sample)
            |> Seq.toArray

        contacts.Clear ()
        frame

/// <summary>Tracks SDL pen axes between pointer samples.</summary>
type internal PenSamples() =
    let pens = Dictionary<uint32, PointerSample>()
    let mutable nextId = 1

    /// <summary>Updates a pen and returns its current sample in CSS pixels.</summary>
    member _.Update(id, action, x, y, button, buttons, modifiers, axis: (SDL_PenAxis * float32) option) =
        let mutable previous = Unchecked.defaultof<PointerSample>
        let found = pens.TryGetValue (id, &previous)

        let mutable sample =
            if found then
                previous
            else
                PointerSample.Create (1, nextId, action, x, y)

        if not found then
            nextId <- nextId + 1

        sample.MovementX <- if found then x - previous.X else 0.0
        sample.MovementY <- if found then y - previous.Y else 0.0
        sample.X <- x
        sample.Y <- y
        sample.Action <- action
        sample.Button <- button
        sample.Buttons <- buttons
        sample.Modifiers <- modifiers
        sample.ClickCount <- if action = 1 || action = 2 then 1 else 0

        match axis with
        | Some (_, value) when not (Single.IsFinite value) -> ()
        | Some (SDL_PenAxis.SDL_PEN_AXIS_PRESSURE, value) -> sample.Pressure <- Math.Clamp (value, 0.0f, 1.0f)
        | Some (SDL_PenAxis.SDL_PEN_AXIS_XTILT, value) -> sample.TiltX <- int (Math.Clamp (value, -90.0f, 90.0f))
        | Some (SDL_PenAxis.SDL_PEN_AXIS_YTILT, value) -> sample.TiltY <- int (Math.Clamp (value, -90.0f, 90.0f))
        | Some (SDL_PenAxis.SDL_PEN_AXIS_ROTATION, value) -> sample.Twist <- (int value % 360 + 360) % 360
        | Some (SDL_PenAxis.SDL_PEN_AXIS_TANGENTIAL_PRESSURE, value) -> sample.TangentialPressure <- Math.Clamp (value, -1.0f, 1.0f)
        | _ -> ()

        pens[id] <- sample
        sample

    /// <summary>Removes a pen, returning a cancellation for an active contact.</summary>
    member _.Leave(id, modifiers) =
        let mutable sample = Unchecked.defaultof<PointerSample>

        if pens.Remove (id, &sample) && sample.Buttons <> 0 then
            sample.Action <- 3
            sample.Buttons <- 0
            sample.Modifiers <- modifiers
            ValueSome sample
        else
            ValueNone

    /// <summary>Cancels active pens and clears their state.</summary>
    member _.Cancel(modifiers) =
        let active =
            pens.Values
            |> Seq.filter (fun sample -> sample.Buttons <> 0)
            |> Seq.map (fun value ->
                let mutable sample = value
                sample.Action <- 3
                sample.Buttons <- 0
                sample.Modifiers <- modifiers
                sample)
            |> Seq.toArray

        pens.Clear ()
        active
