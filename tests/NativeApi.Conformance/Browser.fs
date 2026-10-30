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

[<Emit("[$0 instanceof DOMRect, $0 instanceof DOMPoint]")>]
let private geometryKinds (value: obj) : bool * bool = jsNative

[<Emit("$0 instanceof DOMMatrix")>]
let private mutableMatrix (value: DOMMatrixReadOnly) : bool = jsNative

[<Emit("""(async () => {
    const frame=document.createElement('iframe');
    try {
        frame.srcdoc='<body>first</body>';
        let loaded=new Promise(resolve=>frame.addEventListener('load',resolve,{once:true}));
        document.body.append(frame); await loaded;
        const oldDocument=frame.contentDocument, oldCall=$0(oldDocument);
        loaded=new Promise(resolve=>frame.addEventListener('load',resolve,{once:true}));
        frame.srcdoc='<body>replacement</body>'; await loaded;
        if(oldDocument===frame.contentDocument) throw new Error('Geometry document identity unchanged');
        if(oldCall()!=='inactive:7') throw new Error('Old geometry context remained active or changed retained value');
        if($0(frame.contentDocument)()!=='active:7') throw new Error('Replacement geometry context failed');
        return 3;
    } finally { frame.remove(); }
})()""")>]
let private geometryDocumentLifetime (createCall: Document -> System.Func<string>) : JS.Promise<int> = jsNative

let private geometryCall document =
    let service = Geometry.forDocument document
    let retained = service.CreatePoint (x = 7.)

    System.Func<string>(fun () ->
        let outcome =
            try
                service.CreateRect () |> ignore
                "active"
            with _ ->
                "inactive"

        try
            outcome + ":" + string retained.x
        finally
            service.Dispose ())

/// <summary>Runs the shared geometry fixture and actual document-replacement ownership proof.</summary>
let runGeometry () =
    async {
        let count =
            GeometryValues.run (fun () -> Geometry.forDocument (document ())) geometryKinds disposalError "Exception:GeometryService"

        let! context =
            geometryDocumentLifetime geometryCall
            |> Async.AwaitPromise

        if count <> 177 || context <> 3 then
            failwith "Geometry shared/context assertions did not run"

        return
            {|
                geometry = count
                geometryContext = context
            |}
    }
    |> Async.StartAsPromise

let private matrixCall document =
    let service = Geometry.forDocument document
    let retained = service.CreateMatrix ("translate(7px)")

    System.Func<string>(fun () ->
        let outcome =
            try
                service.CreateMatrix () |> ignore
                "active"
            with _ ->
                "inactive"

        try
            outcome + ":" + string retained.e
        finally
            service.Dispose ())

/// <summary>Runs actual browser matrix operations and document-replacement context checks.</summary>
let runMatrix () =
    async {
        let count =
            MatrixValues.run (fun () -> Geometry.forDocument (document ())) mutableMatrix errorName "Error"

        let! context =
            geometryDocumentLifetime matrixCall
            |> Async.AwaitPromise

        if count <> 710 || context <> 3 then
            failwith "Matrix shared/context assertions did not run"

        return
            {|
                matrices = count
                matrixContext = context
            |}
    }
    |> Async.StartAsPromise

[<Emit("$0 instanceof CSSTransformValue")>]
let private transformType (value: CSSStyleValue) : bool = jsNative

[<Emit("$0 instanceof CSSTranslate")>]
let private translateType (value: CSSTransformComponent) : bool = jsNative

let private transformCall document =
    let service = Ranvier.Dom.TransformValues.forDocument document
    use values = Ranvier.Dom.StyleValues.forDocument document

    let retained =
        service.CreateTranslate (values.CreateUnit (7., "px"), values.CreateUnit (0., "px"))

    System.Func<string>(fun () ->
        let outcome =
            try
                service.CreateList [| retained |] |> ignore
                "active"
            with _ ->
                "inactive"

        try
            let retainedX = (retained.toMatrix ()).e
            outcome + ":" + string retainedX
        finally
            service.Dispose ())

/// <summary>Runs typed transform matrices, actual rendered effects and document-replacement ownership checks.</summary>
let runTransforms () =
    async {
        let document = document ()

        let count =
            TransformValues.run
                (fun () -> Ranvier.Dom.TransformValues.forDocument document)
                (fun () -> Ranvier.Dom.StyleValues.forDocument document)
                (fun () -> Geometry.forDocument document)
                errorName

        use transforms = Ranvier.Dom.TransformValues.forDocument document
        use values = Ranvier.Dom.StyleValues.forDocument document
        use maps = Ranvier.Dom.StyleMaps.forDocument document

        let effects =
            TransformValues.runEffects
                document
                transforms
                values
                maps
                (fun () -> document.body.getBoundingClientRect () |> ignore)
                transformType
                translateType

        let! context =
            geometryDocumentLifetime transformCall
            |> Async.AwaitPromise

        if count <> 450 || effects <> 21 || context <> 3 then
            failwith "Typed transform shared/effect/context assertions did not run"

        return
            {|
                transformValues = count + effects
                transformContext = context
            |}
    }
    |> Async.StartAsPromise

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

[<Emit("""(async () => {
    const frame = document.createElement('iframe');
    try {
        frame.srcdoc = '<body>first</body>';
        let loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        document.body.append(frame); await loaded;
        const original = frame.contentDocument;
        const oldCall = $0(original);
        loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        frame.srcdoc = '<body>replacement</body>'; await loaded;
        if (original === frame.contentDocument) throw new Error('Map context document identity did not change');
        if (oldCall() !== 'inactive:17px') throw new Error('Old map provider stayed active or retained inline value changed');
        if ($0(frame.contentDocument)() !== 'active:17px') throw new Error('Replacement map context failed');
        return 3;
    } finally { frame.remove(); }
})()""")>]
let private styleMapDocumentLifetime (createCall: Document -> System.Func<string>) : JS.Promise<int> = jsNative

let private styleMapCall (document: Document) =
    let service = Ranvier.Dom.StyleMaps.forDocument document
    let element = document.createElement "div" :?> HTMLElement
    let map = service.Attribute (element :> ElementCSSInlineStyle)
    map.set ("width", CSSStyleValueOrUSVString.USVString "17px")
    let value = service.Get (map, "width") |> Option.get

    System.Func<string>(fun () ->
        let outcome =
            try
                service.Entries map |> ignore
                "active"
            with _ ->
                "inactive"

        try
            outcome + ":" + value.toString ()
        finally
            service.Dispose ())

/// <summary>Runs focused typed map assertions and document-navigation ownership proof.</summary>
let runStyleMaps () =
    async {
        let document = document ()
        use values = Ranvier.Dom.StyleValues.forDocument document
        let computed = Ranvier.Dom.ComputedStyles.forDocument document

        let count =
            StyleMaps.run
                document
                (fun () -> Ranvier.Dom.StyleMaps.forDocument document)
                values
                computed
                (fun () -> document.body.getBoundingClientRect () |> ignore)
                styleValueError
                "Exception:StyleMapService"
                (fun map -> (unbox<StylePropertyMap> map).set("width", CSSStyleValueOrUSVString.USVString "999px"))
                "TypeError"

        let! context =
            styleMapDocumentLifetime styleMapCall
            |> Async.AwaitPromise

        return
            {|
                styleMaps = count
                styleMapContext = context
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
        let timing = UserTiming.run (fun () -> Ranvier.Dom.Timing.forDocument document)
        let timingDeterministic = UserTiming.deterministic ()

        if timing <> 180 || timingDeterministic <> 181 then
            failwith "User timing assertions did not run"

        let! geometryProof = runGeometry () |> Async.AwaitPromise
        let! matrixProof = runMatrix () |> Async.AwaitPromise
        let! transformProof = runTransforms () |> Async.AwaitPromise
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

        use styleValueService = Ranvier.Dom.StyleValues.forDocument document

        let styleMaps =
            StyleMaps.run
                document
                (fun () -> Ranvier.Dom.StyleMaps.forDocument document)
                styleValueService
                (Ranvier.Dom.ComputedStyles.forDocument document)
                (fun () -> document.body.getBoundingClientRect () |> ignore)
                styleValueError
                "Exception:StyleMapService"
                (fun map -> (unbox<StylePropertyMap> map).set("width", CSSStyleValueOrUSVString.USVString "999px"))
                "TypeError"

        let! styleMapContext =
            styleMapDocumentLifetime styleMapCall
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
                geometry = geometryProof.geometry
                geometryContext = geometryProof.geometryContext
                matrices = matrixProof.matrices
                matrixContext = matrixProof.matrixContext
                transformValues = transformProof.transformValues
                transformContext = transformProof.transformContext
                cascadeRules = cascadeRules
                timing = timing
                timingDeterministic = timingDeterministic
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
                styleMaps = styleMaps
                styleMapContext = styleMapContext
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
