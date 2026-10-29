module internal NativeApiConformance.Browser

open Fable.Core
open Ranvier.Dom

[<Emit("document")>]
let private document () : Document = jsNative

/// <summary>Runs shared DOM and stylesheet assertions against the browser document.</summary>
let run () =
    {|
        tables = Tables.run (document ())
        reflection = Reflection.run (document ())
        cssom = Stylesheets.run (document ())
        namespaces =
            Namespaces.run (document ()) (fun () ->
                (document ()).body.getBoundingClientRect()
                |> ignore)
    |}
