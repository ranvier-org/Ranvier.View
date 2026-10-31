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

let private svgRealmTargets (target: Document) =
    let root =
        target.createElementNS ("http://www.w3.org/2000/svg", "svg") :?> SVGSVGElement

    root.id <- "native-api-svg-realm"
    target.body.appendChild root |> ignore

    let dictionary =
        DOMMatrix2DInit (m11 = Some 2., m12 = Some 3., m21 = Some 4., m22 = Some 5., m41 = Some 6., m42 = Some 7.)

    let setter = root.createSVGTransform ()
    setter.setMatrix dictionary

    [|
        root.createSVGTransformFromMatrix dictionary
        root.transform.baseVal.createSVGTransformFromMatrix dictionary
        setter
    |]

[<Emit("""(async () => {
    const frame=document.createElement('iframe');
    let checks=0;
    const check=(value,label)=>{checks++;if(!value)throw new Error(label);};
    try {
        const loaded=new Promise(resolve=>frame.addEventListener('load',resolve,{once:true}));
        frame.srcdoc='<!doctype html><body></body>';document.body.append(frame);await loaded;
        for(const target of [document,frame.contentDocument]) {
            const values=$0(target),realm=target.defaultView,fields=['a','b','c','d','e','f'],expected=[2,3,4,5,6,7];
            for(const value of values) {
                check(Object.getPrototypeOf(value)===realm.SVGTransform.prototype,'SVG transform realm');
                check(Object.getPrototypeOf(value.matrix)===realm.SVGMatrix.prototype,'SVG matrix realm');
                for(let i=0;i<fields.length;i++)check(value.matrix[fields[i]]===expected[i],'SVG alias coefficient');
            }
            for(let i=0;i<values.length;i++)for(let j=i+1;j<values.length;j++)check(values[i].matrix!==values[j].matrix,'SVG independent matrix results');
        }
        return checks;
    } finally {document.getElementById('native-api-svg-realm')?.remove();frame.remove();}
})()""")>]
let private svgRealmProof (create: Document -> SVGTransform array) : JS.Promise<int> = jsNative

/// <summary>Runs shared SVG geometry/values and compiled matrix-dictionary adapters in two browser realms.</summary>
let runSvg () =
    async {
        let target = document ()
        let geometry = SvgGeometry.run target errorName
        let values = SvgValues.run target errorName true false

        let! context =
            svgRealmProof svgRealmTargets
            |> Async.AwaitPromise

        if geometry <> 126 || values <> 361 || context <> 54 then
            failwith "SVG shared/realm assertions did not run"

        return
            {|
                svgGeometry = geometry
                svgValues = values
                svgContext = context
            |}
    }
    |> Async.StartAsPromise

[<Emit("new Promise(resolve => setTimeout(resolve, $0))")>]
let private delay (milliseconds: int) : JS.Promise<unit> = jsNative

[<Emit("fetch('/Ahem.ttf').then(r => r.arrayBuffer()).then(b => { let s = ''; for (const v of new Uint8Array(b)) s += String.fromCharCode(v); return btoa(s); })")>]
let private ahem () : JS.Promise<string> = jsNative

[<Emit("globalThis.__nativeApiPaintAt($0, $1)")>]
let private paintAt (x: float) (y: float) : JS.Promise<bool> = jsNative

[<Emit("""(async () => {
    const frame = document.createElement('iframe');
    frame.id = 'native-api-svg-view';
    frame.style.cssText = 'width:300px;height:300px;border:0';
    const loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
    frame.src = URL.createObjectURL(new Blob([$0], {type:'image/svg+xml'}));
    document.body.append(frame);
    await loaded;
    return frame.contentDocument;
})()""")>]
let private svgDocument (markup: string) : JS.Promise<Document> = jsNative

[<Emit("new Promise(resolve => $0.defaultView.requestAnimationFrame(() => $0.defaultView.requestAnimationFrame(() => resolve())))")>]
let private frames (target: Document) : JS.Promise<unit> = jsNative

[<Emit("document.getElementById('native-api-svg-view')?.remove()")>]
let private removeViewFrame () : unit = jsNative

let private wait (milliseconds: int) =
    delay milliseconds |> Async.AwaitPromise

/// <summary>Runs shared SVG shapes, text, standalone views and SMIL controls in Chromium.</summary>
let runSvgSurfaces () =
    async {
        let target = document ()
        let shapes = SvgShapes.run target errorName "Error" false
        let! fontData = ahem () |> Async.AwaitPromise

        let! text =
            SvgText.run target fontData wait (fun (x, y) -> paintAt x y |> Async.AwaitPromise) errorName false

        let! animations = SvgAnimations.run target wait errorName true

        let! standalone =
            svgDocument SvgViewSelection.markup
            |> Async.AwaitPromise

        let mutable arguments = 0

        let outcome action =
            try
                action ()
                "accepted"
            with error ->
                error.Message

        let expect label (expected: string) (actual: string) =
            arguments <- arguments + 1

            if not (actual.Contains expected) then
                failwithf "%s: expected %s, got %s" label expected actual

        expect "null fragment rejects" "fragment" (outcome (fun () -> SvgViews.requestView standalone null))
        expect "HTML document rejects" "SVG XML document" (outcome (fun () -> SvgViews.requestView target "half"))

        let! views =
            SvgViewSelection.run
                standalone
                (fun fragment ->
                    async {
                        SvgViews.requestView standalone fragment
                        return None
                    })
                (fun () -> frames standalone |> Async.AwaitPromise)
                SvgViews.inspect
                false
                true

        removeViewFrame ()
        expect "inactive document rejects" "no longer active" (outcome (fun () -> SvgViews.requestView standalone "half"))

        return
            {|
                svgShapes = shapes
                svgText = text
                svgViews = views
                svgViewArguments = arguments
                svgAnimations = animations
            |}
    }
    |> Async.StartAsPromise

[<Emit("globalThis.__nativeApiPixelAt($0, $1)")>]
let private pixelAt (x: float) (y: float) : JS.Promise<int array> = jsNative

let private pixel (x: float, y: float) =
    async {
        let! value = pixelAt x y |> Async.AwaitPromise
        return value[0], value[1], value[2]
    }

/// <summary>Runs shared SVG paint servers, filters, clipping and resources in Chromium.</summary>
let runSvgPaint () =
    async {
        let target = document ()
        let! paint = SvgPaintServers.run target pixel errorName
        let! filters = SvgFilters.run target pixel errorName
        let! clipping = SvgClipping.run target pixel errorName
        let! resources = SvgResources.run target pixel wait errorName

        return
            {|
                svgPaint = paint
                svgFilters = filters
                svgClipping = clipping
                svgResources = resources
            |}
    }
    |> Async.StartAsPromise

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

[<Emit("""(async () => {
    const frame = document.createElement('iframe');
    try {
        frame.srcdoc = '<body>locale first</body>';
        let loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        document.body.append(frame); await loaded;
        const oldDocument = frame.contentDocument;
        const call = $0(oldDocument);
        if (!call()) throw new Error('Live locale context rejected');
        loaded = new Promise(resolve => frame.addEventListener('load', resolve, {once:true}));
        frame.srcdoc = '<body>locale replacement</body>'; await loaded;
        if (oldDocument === frame.contentDocument) throw new Error('Locale document identity did not change');
        if (call()) throw new Error('Old locale context stayed active');
        if (!$0(frame.contentDocument)()) throw new Error('Replacement locale context rejected');
        return 4;
    } finally { frame.remove(); }
})()""")>]
let private localeDocumentLifetime (createCall: Document -> System.Func<bool>) : JS.Promise<int> = jsNative

let private localeCall document =
    let service = Ranvier.Dom.Locales.forDocument document

    System.Func<bool>(fun () ->
        try
            service.Current |> ignore
            true
        with _ ->
            false)

let runLocales () =
    async {
        let create () =
            Ranvier.Dom.Locales.forDocument (document ())

        let service = create ()
        let mutable nullRejected = false

        try
            Ranvier.Dom.Locales.forDocument Unchecked.defaultof<Document>
            |> ignore
        with :? System.ArgumentNullException ->
            nullRejected <- true

        if not nullRejected then
            failwith "Locale provider accepted a null document"

        let mutable windowlessRejected = false

        try
            Ranvier.Dom.Locales.forDocument (windowlessDocument ())
            |> ignore
        with :? System.NotSupportedException ->
            windowlessRejected <- true

        if not windowlessRejected then
            failwith "Locale provider fabricated windowless preferences"

        let! context =
            localeDocumentLifetime localeCall
            |> Async.AwaitPromise

        return
            {|
                locales = LocalePreferences.run create
                localeDeterministic = LocalePreferences.deterministic ()
                localeContext = context + 2
                localeProvider = service.ProviderName
                localePolicy = service.LocaleCanonicalization
                timeZonePolicy = service.TimeZoneCanonicalization
                localeAlias = service.CanonicalizeLocale "iw-IL"
                timeZoneAlias = service.CanonicalizeTimeZone "US/Eastern"
            |}
    }
    |> Async.StartAsPromise

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

[<Emit("new Promise(resolve => setTimeout(resolve, 0))")>]
let private macrotask () : JS.Promise<unit> = jsNative

[<Emit("$0.reason")>]
let private rawReason (signal: obj) : obj = jsNative

[<Emit("$0 === $1")>]
let private identical (left: obj) (right: obj) : bool = jsNative

/// <summary>Checks raw realm reason identity, capability facts and the active-window requirement.</summary>
let private cancellationInterop (document: Document) =
    async {
        let mutable checks = 0

        let expect condition message =
            checks <- checks + 1

            if not condition then
                failwith message

        use service = BrowserCancellation.forDocument document
        let source = service.CreateController ()
        let composite = service.Any [| source.Signal |]
        source.Abort (AbortReason.Custom (TimingDetail.Object [| "code", TimingDetail.Number 3.0 |]))
        let raw = BrowserCancellation.rawSignal source.Signal
        expect (identical (rawReason raw) (rawReason raw)) "Raw reason keeps its identity"
        expect (identical (rawReason raw) (rawReason (BrowserCancellation.rawSignal composite))) "AbortSignal.any keeps the raw reason object"
        expect (unbox<float>((rawReason raw)?code) = 3.0) "Raw reason carries the portable payload"
        let timeout = service.Abort AbortReason.Timeout
        expect (unbox<string>((rawReason (BrowserCancellation.rawSignal timeout))?name) = "TimeoutError") "Timeout reason is a realm TimeoutError"
        let defaulted = service.Abort AbortReason.Abort
        expect (unbox<string>((rawReason (BrowserCancellation.rawSignal defaulted))?name) = "AbortError") "Default reason is a realm AbortError"
        use registry = Ranvier.Dom.Capabilities.create ()

        use _registration =
            registry.Register (BrowserCancellation.capabilityProvider document)

        for name in [ "abort"; "timeout"; "any" ] do
            let! report =
                registry.Observe (
                    Ranvier.Dom.Capabilities.key CapabilityCategory.PlatformFacility ("abort-signal-" + name),
                    System.Threading.CancellationToken.None
                )
                |> Async.AwaitTask

            expect (report.Exported.State = CapabilityState.Yes) $"Realm defines AbortSignal.{name}"
            expect (report.Available.State = CapabilityState.Unknown) $"AbortSignal.{name} availability stays unknown"

        try
            BrowserCancellation.forDocument (windowlessDocument ())
            |> ignore

            failwith "Cancellation accepted a document without a Window"
        with :? System.NotSupportedException ->
            checks <- checks + 1

        return checks
    }

[<Emit("fetch($0).then(r => r.arrayBuffer()).then(b => new Uint8Array(b))")>]
let private fixtureBytes (url: string) : JS.Promise<byte array> = jsNative

[<Emit("Uint8Array.from(atob($0), c => c.charCodeAt(0))")>]
let private base64 (text: string) : byte array = jsNative

[<Literal>]
let private ResourcePng =
    "iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAYAAACddGYaAAAAEUlEQVR4nGP4z8DwH4YZkDkAm34L9XKwuTwAAAAASUVORK5CYII="

/// <summary>Runs the shared resource-service assertions over Image.decode, FontFace and document.fonts.</summary>
/// <remarks>Chromium resolves decode for a PNG with corrupt image data, and a revoked object URL reaches no provider.</remarks>
let runResources () =
    async {
        let document = document ()

        let! font =
            fixtureBytes "/fixtures/Ahem.ttf"
            |> Async.AwaitPromise

        let fixture: Resources.Fixture =
            {
                Png = base64 ResourcePng
                CorruptPng =
                    base64
                        "iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAYAAACddGYaAAAAKUlEQVR4nAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJ4rAmh4AAAAASUVORK5CYII="
                Font = font
            }

        let image (url: string) =
            let element = document.createElement "img"

            let source =
                if url.EndsWith "/element.png" then
                    "data:image/png;base64,"
                    + ResourcePng
                    + "#/element.png"
                else
                    url

            element.setAttribute ("src", source)
            element

        let engine: Resources.Engine =
            {
                CorruptImageDecodes = true
                TransportReleaseRequests = 0
                ReleaseEngineCaches = ignore
            }

        let! count =
            Resources.run engine fixture (fun provider -> BrowserResources.forDocument document provider) image

        return {| resources = count |}
    }
    |> Async.StartAsPromise

[<Emit("globalThis.__nativeApiSetActive($0)")>]
let private setActive (active: bool) : JS.Promise<unit> = jsNative

[<Emit("globalThis.__nativeApiMouse($0, $1, $2)")>]
let private mouse (action: string) (x: int) (y: int) : JS.Promise<unit> = jsNative

[<Emit("globalThis.__nativeApiTab()")>]
let private tab () : JS.Promise<unit> = jsNative

/// <summary>Runs the shared input state assertions with Playwright input and page activation.</summary>
/// <remarks>The page runs in new headless Chromium, where bringing another page to the front deactivates it.</remarks>
let runInputState () =
    async {
        let document = document ()

        let driver: InputStateChecks.Driver =
            {
                SetActive = fun active -> setActive active |> Async.AwaitPromise
                Move = fun x y -> mouse "move" x y |> Async.AwaitPromise
                Press = fun x y -> mouse "down" x y |> Async.AwaitPromise
                Release = fun x y -> mouse "up" x y |> Async.AwaitPromise
                Tab = fun () -> tab () |> Async.AwaitPromise
            }

        let! count =
            InputStateChecks.run document (fun () -> BrowserInput.forDocument document) BrowserInput.capabilities CapabilityState.Yes driver

        return {| inputState = count |}
    }
    |> Async.StartAsPromise

[<Emit("globalThis.__nativeApiViewport($0, $1, $2)")>]
let private emulateViewport (width: int) (height: int) (scale: float) : JS.Promise<unit> = jsNative

[<Emit("globalThis.__nativeApiPaintSize()")>]
let private paintSize () : JS.Promise<int array> = jsNative

[<Emit("new Promise(resolve => { let done = false; const finish = () => { if (!done) { done = true; setTimeout(resolve, 100) } }; requestAnimationFrame(() => requestAnimationFrame(finish)); setTimeout(finish, 400) })")>]
let private viewportFrames () : JS.Promise<unit> = jsNative

/// <summary>Runs the shared viewport assertions with Chromium device-metrics emulation and page activation.</summary>
/// <remarks>Headless Chromium keeps the page visible, so the visibility steps are native-only.</remarks>
[<Emit("new Promise(resolve => setTimeout(resolve, $0))")>]
let private sleep (milliseconds: float) : JS.Promise<unit> = jsNative

[<Emit("new Promise(resolve => requestAnimationFrame(() => resolve()))")>]
let private animationFrame () : JS.Promise<unit> = jsNative

[<Emit("Promise.resolve()")>]
let private resolved () : JS.Promise<unit> = jsNative

/// <summary>Runs the shared frame fixture on the manual scheduler and on the document Window.</summary>
let runFrames () =
    async {
        let manual = ManualFrameScheduler ()

        let! manualCount =
            FrameChecks.run
                (fun () -> manual :> FrameScheduler)
                {
                    Checkpoint = fun () -> async { manual.RunMicrotasks () }
                    Elapse = fun milliseconds -> async { manual.Advance milliseconds }
                    NextFrame = fun () -> async { manual.RunFrame () }
                }

        let document = document ()

        let! count =
            FrameChecks.run
                (fun () -> BrowserFrames.forDocument document)
                {
                    Checkpoint = fun () -> resolved () |> Async.AwaitPromise
                    Elapse = fun milliseconds -> sleep (milliseconds + 5.0) |> Async.AwaitPromise
                    NextFrame = fun () -> animationFrame () |> Async.AwaitPromise
                }

        return
            {|
                frames = count
                manualFrames = manualCount
            |}
    }
    |> Async.StartAsPromise

[<Emit("new Promise(resolve => requestIdleCallback(() => resolve()))")>]
let private idlePeriod () : JS.Promise<unit> = jsNative

[<Emit("new Promise(resolve => { const end = performance.now() + $0; const spin = setInterval(() => { const s = performance.now(); while (performance.now() - s < 15) {} if (performance.now() >= end) { clearInterval(spin); setTimeout(resolve, 0); } }, 0); })")>]
let private starve (milliseconds: float) : JS.Promise<unit> = jsNative

/// <summary>Runs the shared idle fixture on the manual scheduler and on the document Window.</summary>
let runIdle () =
    async {
        let manual = ManualFrameScheduler ()

        let! manualCount =
            IdleChecks.run
                (fun () -> manual :> IdleScheduler, manual :> FrameScheduler)
                {
                    Idle = fun () -> async { manual.RunIdle 50.0 }
                    NextFrame = fun () -> async { manual.RunFrame () }
                    Starve = fun milliseconds -> async { manual.Advance milliseconds }
                }

        let document = document ()

        let! count =
            IdleChecks.run
                (fun () -> (BrowserIdle.forDocument document).Value, BrowserFrames.forDocument document)
                {
                    Idle = fun () -> idlePeriod () |> Async.AwaitPromise
                    NextFrame = fun () -> animationFrame () |> Async.AwaitPromise
                    Starve = fun milliseconds -> starve milliseconds |> Async.AwaitPromise
                }

        let! observation =
            (BrowserIdle.capabilities document).Observe IdleCapabilities.key System.Threading.CancellationToken.None
            |> Async.AwaitTask

        return
            {|
                idle = count
                manualIdle = manualCount
                available = (observation.Value.Available.State = CapabilityState.Yes)
            |}
    }
    |> Async.StartAsPromise

let runViewport () =
    async {
        let document = document ()

        let settle () =
            async {
                for _ in 1..3 do
                    do! viewportFrames () |> Async.AwaitPromise
            }

        let driver: ViewportChecks.Driver =
            {
                Resize =
                    fun width height scale ->
                        async {
                            do!
                                emulateViewport width height scale
                                |> Async.AwaitPromise

                            do! settle ()
                        }
                Settle = settle
                SetActive =
                    fun active ->
                        async {
                            do! setActive active |> Async.AwaitPromise
                            do! settle ()
                        }
                SetVisible = None
                PaintSize =
                    fun () ->
                        async {
                            let! size = paintSize () |> Async.AwaitPromise
                            return size[0], size[1]
                        }
                PaintRounds = true
                OuterIsViewport = false
            }

        let! count =
            ViewportChecks.run document (fun () -> BrowserViewport.forDocument document) driver

        return {| viewport = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared fractional scrolling assertions under device-metrics emulation.</summary>
let runViewportScroll () =
    async {
        let document = document ()

        let settle () =
            async {
                for _ in 1..3 do
                    do! viewportFrames () |> Async.AwaitPromise
            }

        let driver: ScrollChecks.Driver =
            {
                SetScale =
                    fun scale ->
                        async {
                            do!
                                emulateViewport 320 240 scale
                                |> Async.AwaitPromise

                            do! settle ()
                        }
                Settle = settle
                Snapping = ScrollChecks.RoundHalfUp
            }

        let! count =
            ScrollChecks.run document (fun () -> BrowserViewportScroll.forDocument document) (fun () -> BrowserViewport.forDocument document) driver

        return {| scroll = count |}
    }
    |> Async.StartAsPromise

[<Emit("globalThis.__nativeApiPageScale($0)")>]
let private emulatePageScale (scale: float) : JS.Promise<unit> = jsNative

[<Emit("globalThis.__nativeApiWheel($0, $1, $2, $3)")>]
let private wheel (x: int) (y: int) (deltaX: float) (deltaY: float) : JS.Promise<unit> = jsNative

[<Emit("$0.defaultView.scrollTo($1, $2)")>]
let private windowScrollTo (document: Document) (x: float) (y: float) : unit = jsNative

[<Emit("new Promise(resolve => { const frame = document.createElement('iframe'); frame.onload = () => resolve(frame); frame.srcdoc = '<body>first'; document.body.append(frame) })")>]
let private createFrame () : JS.Promise<obj> = jsNative

[<Emit("$0.contentDocument")>]
let private frameDocument (frame: obj) : Document = jsNative

[<Emit("new Promise(resolve => { $0.onload = () => resolve(); $0.srcdoc = '<body>second' })")>]
let private navigateFrame (frame: obj) : JS.Promise<unit> = jsNative

[<Emit("$0.remove()")>]
let private removeFrame (frame: obj) : unit = jsNative

/// <summary>Runs the shared visual viewport assertions under device-metrics and page-scale emulation.</summary>
let runVisualViewport () =
    async {
        let document = document ()
        let mutable size = 320, 240
        let mutable deviceScale = 1.0

        let settle () =
            async {
                for _ in 1..3 do
                    do! viewportFrames () |> Async.AwaitPromise
            }

        let emulate () =
            async {
                do!
                    emulateViewport (fst size) (snd size) deviceScale
                    |> Async.AwaitPromise

                do! settle ()
            }

        let driver: VisualViewportChecks.Driver =
            {
                Resize =
                    fun width height ->
                        size <- width, height
                        emulate ()
                SetDeviceScale =
                    fun scale ->
                        deviceScale <- scale
                        emulate ()
                SetPageScale =
                    fun service scale _ _ ->
                        async {
                            match service.SetPageScale (scale, 0, 0) with
                            | Error (VisualViewportUnavailable.Unsupported _) -> ()
                            | other -> failwith $"The browser page scale input: {other}"

                            do! emulatePageScale scale |> Async.AwaitPromise
                            do! settle ()
                        }
                Wheel =
                    fun x y dx dy ->
                        async {
                            do! wheel x y dx dy |> Async.AwaitPromise
                            do! settle ()
                        }
                Settle = settle
                Engine = VisualViewportChecks.Chromium
                HostInsets = false
            }

        let! count =
            VisualViewportChecks.run document (windowScrollTo document) (fun () -> BrowserVisualViewport.forDocument document) driver

        // A navigation gives the frame a new Window: the held visual viewport reads zeros, which the provider reports
        // as Detached, and a provider for the new document reads it.
        let! frame = createFrame () |> Async.AwaitPromise
        let held = BrowserVisualViewport.forDocument (frameDocument frame)
        let mutable navigation = 0

        let expect condition message =
            navigation <- navigation + 1

            if not condition then
                failwith message

        expect (Result.isOk (held.Current ())) "the frame's visual viewport reads before navigation"
        do! navigateFrame frame |> Async.AwaitPromise
        expect (held.Current () = Error VisualViewportUnavailable.Detached) "the held visual viewport is detached after navigation"
        let next = BrowserVisualViewport.forDocument (frameDocument frame)
        expect (Result.isOk (next.Current ())) "the navigated document's visual viewport reads"
        held.Dispose ()
        next.Dispose ()
        removeFrame frame

        return
            {|
                visualViewport = count
                navigation = navigation
            |}
    }
    |> Async.StartAsPromise

[<Emit("globalThis.__nativeApiKey($0)")>]
let private key (name: string) : JS.Promise<unit> = jsNative

[<Emit("navigator.userAgent.includes('Windows')")>]
let private crlfLineEndings () : bool = jsNative

[<Emit("globalThis.__nativeApiClick($0, $1)")>]
let private click (x: int) (y: int) : JS.Promise<unit> = jsNative

/// <summary>Runs the shared dialog, popover and command invoker assertions with Playwright keyboard and mouse input.</summary>
let runDialogs () =
    async {
        let document = document ()

        let driver: DialogChecks.Driver =
            {
                Settle = fun () -> viewportFrames () |> Async.AwaitPromise
                Press = fun name -> key name |> Async.AwaitPromise
                Click = fun x y -> click x y |> Async.AwaitPromise
                Backend = DialogChecks.Chromium
            }

        let! count =
            DialogChecks.run document (fun () -> BrowserToggleEvents.forDocument document) driver errorName

        return {| dialogs = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared Blob, File, FileList and FormData File assertions.</summary>
let runBinary () =
    async {
        let document = document ()

        let driver: BinaryChecks.Driver =
            {
                Settle = fun () -> Async.Sleep 1
                CrlfLineEndings = crlfLineEndings ()
            }

        let! count =
            BinaryChecks.run document (fun () -> BrowserBinaryValues.forDocument document) (fun () -> BrowserForms.forDocument document) driver

        return {| binary = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared form submission assertions.</summary>
let runSubmissions () =
    async {
        let document = document ()

        let driver: SubmissionChecks.Driver = { Settle = fun () -> Async.Sleep 20 }

        let! count =
            SubmissionChecks.run document (fun () -> BrowserFormSubmission.forDocument document) driver

        return {| submissions = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared event detail assertions.</summary>
let runEventDetails () =
    async {
        let document = document ()

        let driver: EventDetailChecks.Driver = { Settle = fun () -> Async.Sleep 16 }

        let! count =
            EventDetailChecks.run document (fun () -> BrowserEventDetails.forDocument document) driver

        return {| eventDetails = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared caret hit-testing assertions under device-metrics emulation.</summary>
let runCaret () =
    async {
        let document = document ()

        let settle () =
            async {
                for _ in 1..3 do
                    do! viewportFrames () |> Async.AwaitPromise
            }

        let! font = ahem () |> Async.AwaitPromise

        let driver: CaretChecks.Driver =
            {
                SetScale =
                    fun scale ->
                        async {
                            do!
                                emulateViewport 320 240 scale
                                |> Async.AwaitPromise

                            do! settle ()
                        }
                Settle = settle
                Font = font
                Qualified = Map.empty
            }

        let! count =
            CaretChecks.run document (BrowserViewportScroll.forDocument document) errorName driver

        return
            {|
                caret = count
                browser = CaretOracle.browser
            |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared form collection, FormData and form event assertions.</summary>
let runForms () =
    let document = document ()

    let count =
        FormChecks.run document (fun () -> BrowserForms.forDocument document) errorName

    {| forms = count |}

/// <summary>Runs the shared URL, URLSearchParams and URLPattern assertions.</summary>
let runUrls () =
    let document = document ()

    let count =
        UrlChecks.run (fun () -> BrowserUrls.forDocument document) errorName UrlChecks.Chromium

    {| urls = count |}

/// <summary>Runs the shared ARIA element reflection assertions.</summary>
let runAria () =
    {|
        aria = AriaChecks.run (document ()) AriaChecks.Chromium
    |}

/// <summary>Runs the shared text segmentation assertions.</summary>
let runSegments () =
    let count =
        TextSegmentationChecks.run (fun () -> BrowserTextSegmentation.create ()) errorName TextSegmentationChecks.Chromium

    {| segments = count |}

[<Emit("window.__rvSetScreen($0, $1, $2)")>]
let private setScreen (width: int) (height: int) (orientation: int) : JS.Promise<unit> = jsNative

/// <summary>Runs the shared screen assertions; browser.mjs drives the screen through CDP.</summary>
let runScreen () =
    async {
        let document = document ()

        let driver: ScreenChecks.Driver =
            {
                Set = fun (width, height, orientation) -> setScreen width height (int orientation) |> Async.AwaitPromise
                Settle =
                    fun () ->
                        async {
                            for _ in 1..2 do
                                do! viewportFrames () |> Async.AwaitPromise
                        }
                LockResolves = true
            }

        let! count = ScreenChecks.run (fun () -> BrowserScreen.forDocument document) driver
        return {| screen = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the shared custom highlight assertions.</summary>
let runHighlights () =
    let document = document ()
    let count = HighlightChecks.run (fun () -> BrowserHighlights.forDocument document) document HighlightChecks.Chromium
    {| highlights = count |}

/// <summary>Runs the shared crypto assertions in a secure context.</summary>
let runCrypto () =
    async {
        let document = document ()
        let! count = CryptoChecks.run (fun () -> BrowserCrypto.forDocument document)
        return {| crypto = count |}
    }
    |> Async.StartAsPromise

/// <summary>Runs the insecure-context crypto assertions.</summary>
let runCryptoInsecure () =
    async {
        let document = document ()
        let! count = CryptoChecks.insecure (fun () -> BrowserCrypto.forDocument document)
        return {| crypto = count |}
    }
    |> Async.StartAsPromise

[<Emit("$0.constructor.name")>]
let private constructorName (element: Element) : string = jsNative

let private domModel (document: Document) =
    let traversal, reentry = TreeTraversal.run document errorName false
    let mathml = MathMLElements.run document constructorName "MathMLElement"

    {|
        traversal = traversal
        traversalReentry = $"{reentry.Iterator}/{reentry.Walker}/{reentry.Reference}"
        mathml = mathml
    |}

/// <summary>Runs the shared tree traversal and MathML fixtures against the browser document.</summary>
let runDomModel () =
    async { return domModel (document ()) }
    |> Async.StartAsPromise

/// <summary>Runs shared DOM and stylesheet assertions against the browser document.</summary>
let run () =
    async {
        let! localeResult = runLocales () |> Async.AwaitPromise
        let document = document ()
        let! svgProof = runSvg () |> Async.AwaitPromise
        let timing = UserTiming.run (fun () -> Ranvier.Dom.Timing.forDocument document)
        let timingDeterministic = UserTiming.deterministic ()

        if timing <> 180 || timingDeterministic <> 181 then
            failwith "User timing assertions did not run"

        let! capabilities =
            Capabilities.run (fun () -> BrowserCapabilities.forDocument document)
            |> Async.AwaitTask

        let! cancellation =
            Cancellation.run document (fun () -> BrowserCancellation.forDocument document) (fun () ->
                macrotask ()
                |> Async.AwaitPromise
                |> Async.StartAsTask)
            |> Async.AwaitTask

        let! cancellationInterop = cancellationInterop document

        let domModelResult = domModel document

        let customEvents =
            CustomEvents.run document (fun () -> BrowserEvents.forDocument document) (fun () -> BrowserCancellation.forDocument document)

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
                svgGeometry = svgProof.svgGeometry
                svgValues = svgProof.svgValues
                svgContext = svgProof.svgContext
                geometry = geometryProof.geometry
                geometryContext = geometryProof.geometryContext
                matrices = matrixProof.matrices
                matrixContext = matrixProof.matrixContext
                transformValues = transformProof.transformValues
                transformContext = transformProof.transformContext
                cascadeRules = cascadeRules
                locales = localeResult.locales
                localeDeterministic = localeResult.localeDeterministic
                localeContext = localeResult.localeContext
                timing = timing
                timingDeterministic = timingDeterministic
                capabilities = capabilities
                cancellation = cancellation
                cancellationInterop = cancellationInterop
                customEvents = customEvents
                traversal = domModelResult.traversal
                traversalReentry = domModelResult.traversalReentry
                mathml = domModelResult.mathml
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
