module internal NativeApiConformance.Browser

open Fable.Core
open Ranvier.Dom

[<Emit("document")>]
let private document () : Document = jsNative

/// <summary>Runs the shared table and HTML reflection assertions against the browser document.</summary>
let run () =
    {|
        tables = Tables.run (document ())
        reflection = Reflection.run (document ())
    |}
