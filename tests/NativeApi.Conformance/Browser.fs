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

[<Emit("document.implementation.createHTMLDocument('')")>]
let private windowlessDocument () : Document = jsNative

[<Emit("$0.name || 'Error'")>]
let private errorName (error: exn) : string = jsNative

[<Emit("[globalThis.innerWidth, globalThis.innerHeight]")>]
let private viewportSize () : struct (int * int) = jsNative

[<Emit("globalThis.__nativeApiResizeViewport($0, $1)")>]
let private resizeViewport (width: int) (height: int) : JS.Promise<unit> = jsNative

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
        let cssSupport = CssSupport.run (CssQueries.forDocument document)

        let computedStyles =
            ComputedStyles.run document (fun () -> Ranvier.Dom.ComputedStyles.forDocument document) errorName

        let mutable computedContextChecks = 0
        let fragmentRectangles = FragmentRectangles.run document
        let fontRules, fontWidthAlias = FontTransitionRules.fonts document
        let transitionRules = FontTransitionRules.transitions document

        let positionTry, positionTryAvailable =
            PositionTry.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let typedStyles =
            TypedStyles.run document (Ranvier.Dom.ComputedStyles.forDocument document) errorName

        try
            Ranvier.Dom.ComputedStyles.forDocument Unchecked.defaultof<Document>
            |> ignore

            failwith "Computed-style provider accepted a null document"
        with :? System.ArgumentNullException ->
            computedContextChecks <- computedContextChecks + 1

        try
            Ranvier.Dom.ComputedStyles.forDocument (windowlessDocument ())
            |> ignore

            failwith "Computed-style provider accepted a document without a Window"
        with :? System.NotSupportedException ->
            computedContextChecks <- computedContextChecks + 1

        let mutable queryContextChecks = 0

        try
            CssQueries.forDocument Unchecked.defaultof<Document>
            |> ignore

            failwith "CSS query provider accepted a null document"
        with :? System.ArgumentNullException ->
            queryContextChecks <- queryContextChecks + 1

        try
            CssQueries.forDocument (windowlessDocument ())
            |> ignore

            failwith "CSS query provider accepted a document without a Window"
        with :? System.NotSupportedException ->
            queryContextChecks <- queryContextChecks + 1

        let cssOracle = cssEscapeOracle CSS.escape
        let tables = Tables.run document
        let viewTables = ViewTables.run document
        let viewHyperlinks = ViewHyperlinks.run document
        let reflection = Reflection.run document
        let hyperlinks = Hyperlinks.run document
        let cssom = Stylesheets.run document

        let ruleMutations =
            RuleMutations.run document (Ranvier.Dom.ComputedStyles.forDocument document) errorName

        let svgCssom = Stylesheets.runSvg document

        let namespaces =
            Namespaces.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) -> post (fun () -> success ()))

        let! construction =
            SheetConstruction.run document (fun () -> StyleSheets.forDocument document) checkpoint computed

        let! conditionalRules =
            ConditionalRules.run
                document
                (Ranvier.Dom.ComputedStyles.forDocument document)
                (viewportSize ())
                (fun width height -> resizeViewport width height |> Async.AwaitPromise)
                errorName

        let! cascadeRules, scopeRules, nestedDeclarations, importContentsReadable =
            CascadeRules.run document (Ranvier.Dom.ComputedStyles.forDocument document) (fun () -> Async.Sleep 1) errorName

        return
            {|
                cascadeRules = cascadeRules
                scopeRules = scopeRules
                nestedDeclarations = nestedDeclarations
                importContentsReadable = importContentsReadable
                tables = tables
                cssEscaping = cssEscaping
                cssSupport = cssSupport
                computedStyles = computedStyles
                typedStyles = typedStyles
                fragmentRectangles = fragmentRectangles
                fontRules = fontRules
                fontWidthAlias = fontWidthAlias
                transitionRules = transitionRules
                positionTry = positionTry
                positionTryAvailable = positionTryAvailable
                conditionalRules = conditionalRules
                computedContextChecks = computedContextChecks
                queryContextChecks = queryContextChecks
                cssOracle = cssOracle
                viewTables = viewTables
                viewHyperlinks = viewHyperlinks
                reflection = reflection
                hyperlinks = hyperlinks
                cssom = cssom
                ruleMutations = ruleMutations
                svgCssom = svgCssom
                namespaces = namespaces
                construction = construction
            |}
    }
    |> Async.StartAsPromise
