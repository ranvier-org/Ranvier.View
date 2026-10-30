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

[<Emit("$0.constructor.name + ':' + $0.message")>]
let private disposalError (error: exn) : string = jsNative

[<Emit("[$0 instanceof CSSImageValue, $0 instanceof CSSNumericValue]")>]
let private styleValueSubtypes (value: CSSStyleValue) : bool * bool = jsNative

[<Emit("$0.constructor.name === 'Exception' ? $0.constructor.name + ':' + $0.message : $0.name || 'Error'")>]
let private styleValueError (error: exn) : string = jsNative

[<Emit("""(async () => {
    const frame = document.createElement('iframe');
    try {
        frame.srcdoc = '<body>first</body>';
        let loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        document.body.append(frame); await loaded;
        const oldDocument = frame.contentDocument;
        const oldCall = $0(oldDocument);
        loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        frame.srcdoc = '<body>replacement</body>'; await loaded;
        if (oldDocument === frame.contentDocument) throw new Error('Document identity did not change');
        if (oldCall() !== 'inactive:1px') throw new Error('Old parser context stayed active or retained value changed');
        if ($0(frame.contentDocument)() !== 'active:1px') throw new Error('Replacement parser context failed');
        return 3;
    } finally { frame.remove(); }
})()""")>]
let private styleValueDocumentLifetime (createCall: Document -> System.Func<string>) : JS.Promise<int> = jsNative

let private styleValueCall document =
    let service = Ranvier.Dom.StyleValues.forDocument document
    let retained = service.CreateUnit (1., "px")

    System.Func<string>(fun () ->
        let outcome =
            try
                service.Parse ("width", "1px") |> ignore
                "active"
            with _ ->
                "inactive"

        try
            outcome + ":" + retained.toString ()
        finally
            service.Dispose ())

[<Emit("$0 === undefined ? 'undefined' : $0 === null ? 'null' : typeof $0")>]
let private classifyString (value: string) : string = jsNative

[<Emit("""(async () => {
    const frame = document.createElement('iframe');
    try {
        frame.srcdoc = '<body>first</body>';
        let loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        document.body.append(frame); await loaded;
        const oldDocument = frame.contentDocument;
        const oldCall = $0(oldDocument);
        loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        frame.srcdoc = '<body>replacement</body>'; await loaded;
        if (oldDocument === frame.contentDocument) throw new Error('Document identity did not change');
        if (oldCall() !== 'TypeError') throw new Error('Old document registration did not fail as observed');
        if ($0(frame.contentDocument)() !== 'registered') throw new Error('Old provider registered into replacement');
        return 3;
    } finally { frame.remove(); }
})()""")>]
let private registrationDocumentLifetime (createCall: Document -> System.Func<string>) : JS.Promise<int> = jsNative

let private registrationCall (document: Document) =
    let service = Ranvier.Dom.CustomProperties.forDocument document

    System.Func<string>(fun () ->
        try
            try
                service.Register (PropertyDefinition.Create ("--c12-document-lifetime", false))
                "registered"
            with CustomPropertyException (name, _) ->
                name
        finally
            service.Dispose ())

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

/// <summary>Runs the focused registration fixture and document-navigation ownership proof.</summary>
let runCustomProperties () =
    async {
        let document = document ()

        let count =
            CustomProperties.run
                document
                (fun () -> Ranvier.Dom.CustomProperties.forDocument document)
                (Ranvier.Dom.ComputedStyles.forDocument document)
                disposalError
                "Exception:CustomPropertyService"

        let! context =
            registrationDocumentLifetime registrationCall
            |> Async.AwaitPromise

        return
            {|
                customProperties = count
                customPropertyContext = context
            |}
    }
    |> Async.StartAsPromise

/// <summary>Runs focused typed CSS value assertions against the browser document.</summary>
let runStyleValues () =
    async {
        let count =
            StyleValues.run
                (fun () -> Ranvier.Dom.StyleValues.forDocument (document ()))
                styleValueError
                "Exception:StyleValueService"
                styleValueSubtypes

        let! context =
            styleValueDocumentLifetime styleValueCall
            |> Async.AwaitPromise

        return
            {|
                styleValues = count
                styleValueContext = context
            |}
    }
    |> Async.StartAsPromise

/// <summary>Runs shared DOM and stylesheet assertions against the browser document.</summary>
let run () =
    async {
        let document = document ()
        let cssEscaping = CssEscaping.run document
        let cssSupport = CssSupport.run (CssQueries.forDocument document)

        let customProperties =
            CustomProperties.run
                document
                (fun () -> Ranvier.Dom.CustomProperties.forDocument document)
                (Ranvier.Dom.ComputedStyles.forDocument document)
                disposalError
                "Exception:CustomPropertyService"

        let! customPropertyContext =
            registrationDocumentLifetime registrationCall
            |> Async.AwaitPromise

        let styleValues =
            StyleValues.run (fun () -> Ranvier.Dom.StyleValues.forDocument document) styleValueError "Exception:StyleValueService" styleValueSubtypes

        let! styleValueContext =
            styleValueDocumentLifetime styleValueCall
            |> Async.AwaitPromise

        let computedStyles =
            ComputedStyles.run document (fun () -> Ranvier.Dom.ComputedStyles.forDocument document) errorName

        let mutable computedContextChecks = 0
        let fragmentRectangles = FragmentRectangles.run document
        let fontRules, fontWidthAlias = FontTransitionRules.fonts document
        let transitionRules = FontTransitionRules.transitions document

        let positionTry, positionTryAvailable =
            PositionTry.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let counterStyles, counterSpeakAs =
            CounterStyleRules.run document (fun () -> document.body.getBoundingClientRect () |> ignore)

        let keyframeRules, keyframeLength, invalidKeyTextError =
            KeyframeRules.run
                document
                (Ranvier.Dom.ComputedStyles.forDocument document)
                (fun () -> document.body.getBoundingClientRect () |> ignore)
                errorName

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

        let viewportOperations = ElementOperations.viewport document

        let! semanticOperations, attachmentIdentifier =
            ElementOperations.semantic
                document
                (fun () -> Async.Sleep 20)
                (fun () -> document.body.getBoundingClientRect () |> ignore)
                errorName
                classifyString
                [ "undefined" ]

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) -> post (fun () -> success ()))

        let! construction =
            SheetConstruction.run
                document
                (fun () -> StyleSheets.forDocument document)
                checkpoint
                computed
                disposalError
                "Exception:StyleSheetService"

        let! sheetElements, failedLinkSheet =
            SheetElements.run document computed (fun () -> Async.Sleep 10) errorName

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
                customProperties = customProperties
                customPropertyContext = customPropertyContext
                styleValues = styleValues
                styleValueContext = styleValueContext
                computedStyles = computedStyles
                typedStyles = typedStyles
                fragmentRectangles = fragmentRectangles
                fontRules = fontRules
                fontWidthAlias = fontWidthAlias
                transitionRules = transitionRules
                positionTry = positionTry
                positionTryAvailable = positionTryAvailable
                counterStyles = counterStyles
                counterSpeakAs = counterSpeakAs
                keyframeRules = keyframeRules
                keyframeLength = keyframeLength
                invalidKeyTextError = invalidKeyTextError
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
                viewportOperations = viewportOperations
                semanticOperations = semanticOperations
                attachmentIdentifier = attachmentIdentifier
                construction = construction
                sheetElements = sheetElements
                failedLinkSheet = failedLinkSheet
            |}
    }
    |> Async.StartAsPromise
