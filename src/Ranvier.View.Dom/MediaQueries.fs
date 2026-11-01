/// <summary>Media queries owned by the current reactive scope.</summary>
module Ranvier.View.Dom.MediaQueries

open Ranvier
open global.Ranvier.Dom

/// <summary>
/// Whether <c>query</c> matches, as a signal read: the query and its change subscription belong to the current owner,
/// and disposing the owner removes the subscription and releases the query. Each change writes the signal in a batch.
/// </summary>
let matches (service: MediaQueryService) (query: string) : unit -> bool =
    let list = service.Match query
    let value = createSignal list.Matches

    let subscription =
        list.Subscribe (fun matches -> batch (fun () -> value.Value <- matches))

    onCleanup (fun () ->
        subscription.Dispose ()
        list.Dispose ())

    fun () -> value.Value
