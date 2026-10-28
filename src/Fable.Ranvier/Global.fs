#if FABLE_COMPILER
module Fable.Ranvier.Global

open Fable.Core
open global.Ranvier.Dom
open Fable.Core.TS

[<Global "globalThis">]
let globalThis: GlobalThis = jsNative

[<Global "window">]
let window: Fable.Core.TS.Dom.Window = jsNative

[<Global "document">]
let document: Document = jsNative

let isBrowser = lazy (Option.ofObj document |> Option.isSome)
#else
namespace Fable.Ranvier

open global.Ranvier.Dom

/// <summary>
/// The DOM globals of the host installed on the current thread.
/// </summary>
[<Sealed; AbstractClass>]
type Global =
    static member document: Document = global.Ranvier.View.Dom.DomRuntime.document ()

    static member queueMicrotask(callback: unit -> unit) : unit =
        global.Ranvier.View.Dom.DomRuntime.queueMicrotask callback

    static member isBrowser =
        lazy
            (try
                not (isNull (Global.document))
             with :? System.InvalidOperationException ->
                 false)
#endif
