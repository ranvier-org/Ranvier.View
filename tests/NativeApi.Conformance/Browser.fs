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

[<Emit("""(() => {
    if ($0(null) !== globalThis.CSS.escape(null)) throw new Error('CSS.escape null conversion differs');
    let checks = 1;
    for (let code = 0; code < 65536; code++) {
        const unit = String.fromCharCode(code);
        for (const input of [unit, 'a' + unit, '-' + unit]) {
            if ($0(input) !== globalThis.CSS.escape(input)) throw new Error('CSS.escape differs at code unit ' + code);
            checks++;
        }
    }
    return checks;
})()""")>]
let private cssEscapeOracle (escape: string -> string) : int = jsNative

/// <summary>Runs shared DOM and stylesheet assertions against the browser document.</summary>
let run () =
    async {
        let document = document ()
        let cssEscaping = CssEscaping.run document
        let cssOracle = cssEscapeOracle CSS.escape
        let tables = Tables.run document
        let viewTables = ViewTables.run document
        let reflection = Reflection.run document
        let hyperlinks = Hyperlinks.run document
        let cssom = Stylesheets.run document
        let svgCssom = Stylesheets.runSvg document

        let namespaces =
            Namespaces.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) -> post (fun () -> success ()))

        let! construction =
            SheetConstruction.run document (fun () -> StyleSheets.forDocument document) checkpoint computed

        return
            {|
                tables = tables
                cssEscaping = cssEscaping
                cssOracle = cssOracle
                viewTables = viewTables
                reflection = reflection
                hyperlinks = hyperlinks
                cssom = cssom
                svgCssom = svgCssom
                namespaces = namespaces
                construction = construction
            |}
    }
    |> Async.StartAsPromise
