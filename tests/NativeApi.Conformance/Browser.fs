module internal NativeApiConformance.Browser

open Fable.Core
open Ranvier.Dom

[<Emit("document")>]
let private document () : Document = jsNative

/// <summary>Runs the shared table assertions against the browser document.</summary>
let run () =
    Tables.run (document ())
