namespace Ranvier.View.WebCore.Sdl

open System
open System.Collections.Generic

/// <summary>Owner-thread weak window registrations with strong dispatch snapshots.</summary>
[<Sealed>]
type internal WindowRegistry<'Window when 'Window: not struct>() =
    let entries = Dictionary<uint32, WeakReference<'Window>>()

    /// <summary>Registers the live window for an SDL window ID.</summary>
    member _.Add(id, window) =
        entries[id] <- WeakReference<'Window>(window)

    /// <summary>Removes the window after its page closes.</summary>
    member _.Remove(id) =
        entries.Remove id |> ignore

    /// <summary>Keeps live windows alive during dispatch and removes dead registrations.</summary>
    member _.Snapshot() =
        let live = ResizeArray<'Window>()
        let stale = ResizeArray<uint32>()

        for KeyValue (id, weak) in entries do
            let mutable value = Unchecked.defaultof<'Window>

            if weak.TryGetTarget &value then
                live.Add value
            else
                stale.Add id

        for id in stale do
            entries.Remove id |> ignore

        live.ToArray ()
