module Ranvier.View.Dom.DomEvents

open Ranvier.View
#if FABLE_COMPILER
open Fable.Core
#endif

/// <summary>Adds a direct event listener and returns its removal.</summary>
#if FABLE_COMPILER
[<Emit("(() => { $0.addEventListener($1, $2); return () => $0.removeEventListener($1, $2); })()")>]
let listenDirect (node: obj) (eventName: string) (handler: obj -> unit) : unit -> unit = jsNative
#else
let listenDirect (node: obj) (eventName: string) (handler: obj -> unit) : unit -> unit =
    Native.listen node eventName handler
#endif
