module internal NativeApiConformance.Browser

open Fable.Core
open Fable.Core.JsInterop
open Ranvier.Dom

[<Emit("document")>]
let private document () : Document = jsNative

[<Emit("queueMicrotask($0)")>]
let private post (action: unit -> unit) : unit = jsNative

[<Emit("$0.ownerDocument.defaultView.getComputedStyle($0).getPropertyValue($1)")>]
let private computed (element: HTMLElement) (property: string) : string = jsNative

/// <summary>Runs shared DOM and stylesheet assertions against the browser document.</summary>
let run () =
    async {
        let document = document ()
        let tables = Tables.run document
        let reflection = Reflection.run document
        let cssom = Stylesheets.run document

        let namespaces =
            Namespaces.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) -> post (fun () -> success ()))

        let! construction =
            SheetConstruction.run document (fun () -> StyleSheets.forDocument document) checkpoint computed

        return
            {|
                tables = tables
                reflection = reflection
                cssom = cssom
                namespaces = namespaces
                construction = construction
            |}
    }
    |> Async.StartAsPromise
