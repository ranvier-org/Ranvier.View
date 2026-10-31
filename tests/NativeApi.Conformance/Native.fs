module internal NativeApiConformance.Native

open Ranvier.View.WebCore

let private runTransforms (host: WebCoreHost) =
    let shared =
        TransformValues.run
            (fun () -> Ranvier.View.WebCore.TransformValues.forHost host)
            (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
            (fun () -> Geometry.forHost host)
            (function
             | WebCoreDomException (name, _) -> name
             | error -> error.GetType().Name)

    use transforms = Ranvier.View.WebCore.TransformValues.forHost host
    use values = Ranvier.View.WebCore.StyleValues.forHost host
    use maps = Ranvier.View.WebCore.StyleMaps.forHost host

    let effects =
        TransformValues.runEffects
            host.Document
            transforms
            values
            maps
            (fun () -> host.Frame () |> ignore)
            (fun value -> value :? Ranvier.Dom.CSSTransformValue)
            (fun value -> value :? Ranvier.Dom.CSSTranslate)

    if shared <> 450 || effects <> 21 then
        failwith "Typed transform shared/effect assertions did not run"

    shared + effects

let private runAll () =
    let transformLifecycle = TransformLifecycle.run ()

    if transformLifecycle <> 47 then
        failwith "Typed transform lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C16 LIFETIME PASS assertions={transformLifecycle}"
    let matrixLifecycle = MatrixLifecycle.run ()

    if matrixLifecycle <> 23 then
        failwith "Matrix lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API G08 LIFETIME PASS assertions={matrixLifecycle}"
    let geometryLifecycle = GeometryLifecycle.run ()

    if geometryLifecycle <> 26 then
        failwith "Geometry lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API G07 LIFETIME PASS assertions={geometryLifecycle}"
    let styleMapLifecycle = StyleMapLifecycle.run ()

    if styleMapLifecycle <> 33 then
        failwith "Typed-map lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C13 LIFETIME PASS assertions={styleMapLifecycle}"
    let styleValueLifecycle = StyleValueLifecycle.run ()

    if styleValueLifecycle <> 19 then
        failwith "Typed-style lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C14 LIFETIME PASS assertions={styleValueLifecycle}"
    let registrationLifecycle = CustomPropertyLifecycle.run ()

    if registrationLifecycle <> 10 then
        failwith "Custom-property lifetime assertions did not run"

    System.Console.WriteLine $"NATIVE_API C12 LIFETIME PASS assertions={registrationLifecycle}"
    let mutable nullQueryHostRejected = false

    try
        CssQueries.forHost Unchecked.defaultof<WebCoreHost>
        |> ignore
    with :? System.ArgumentNullException ->
        nullQueryHostRejected <- true

    if not nullQueryHostRejected then
        failwith "CSS query provider accepted a null host"

    let mutable nullComputedHostRejected = false

    try
        Ranvier.View.WebCore.ComputedStyles.forHost Unchecked.defaultof<WebCoreHost>
        |> ignore
    with :? System.ArgumentNullException ->
        nullComputedHostRejected <- true

    if not nullComputedHostRejected then
        failwith "Computed-style provider accepted a null host"

    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    try
        let transforms = runTransforms host
        System.Console.WriteLine $"NATIVE_API C16 PASS assertions={transforms}"

        let matrices =
            MatrixValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMMatrix)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ArgumentNullException"

        if matrices <> 710 then
            failwith "Matrix shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API G08 PASS assertions={matrices}"

        let geometry =
            GeometryValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMRect, value :? Ranvier.Dom.DOMPoint)
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if geometry <> 177 then
            failwith "Geometry shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API G07 PASS assertions={geometry}"
        let viewTables = ViewTables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10_VIEW PASS assertions={viewTables}"
        let viewHyperlinks = ViewHyperlinks.run host.Document

        if viewHyperlinks <> 50 then
            failwith "View hyperlink authoring assertions did not run"

        System.Console.WriteLine $"NATIVE_API D11_VIEW_HYPERLINKS PASS assertions={viewHyperlinks}"
        let cssEscaping = CssEscaping.run host.Document
        System.Console.WriteLine $"NATIVE_API C05 PASS assertions={cssEscaping}"
        let cssQueries = CssQueries.forHost host
        let cssSupport = CssSupport.run cssQueries
        System.Console.WriteLine $"NATIVE_API C03 PASS assertions={cssSupport}"

        let computedStyles =
            ComputedStyles.run host.Document (fun () -> Ranvier.View.WebCore.ComputedStyles.forHost host) (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C01_C02 PASS assertions={computedStyles}"
        let computedService = Ranvier.View.WebCore.ComputedStyles.forHost host

        let customProperties =
            CustomProperties.run
                host.Document
                (fun () -> Ranvier.View.WebCore.CustomProperties.forHost host)
                computedService
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if customProperties <> 50 then
            failwith "Custom-property shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C12 PASS assertions={customProperties}"

        let styleValues =
            StyleValues.run
                (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ObjectDisposedException"
                (fun value -> value :? Ranvier.Dom.CSSImageValue, value :? Ranvier.Dom.CSSNumericValue)

        if styleValues <> 99 then
            failwith "Typed-style shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C14 PASS assertions={styleValues}"
        use styleValueService = Ranvier.View.WebCore.StyleValues.forHost host

        let styleMaps =
            StyleMaps.run
                host.Document
                (fun () -> Ranvier.View.WebCore.StyleMaps.forHost host)
                styleValueService
                computedService
                (fun () -> host.Frame ())
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ObjectDisposedException"
                (fun map -> (map :?> Ranvier.Dom.StylePropertyMap).set("width", Ranvier.Dom.CSSStyleValueOrUSVString.USVString "999px"))
                "InvalidCastException"

        if styleMaps <> 127 then
            failwith "Typed-map shared assertions did not run"

        System.Console.WriteLine $"NATIVE_API C13 PASS assertions={styleMaps}"

        let ruleMutations =
            RuleMutations.run host.Document computedService (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C08 PASS assertions={ruleMutations}"

        let typedStyles =
            TypedStyles.run host.Document computedService (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        System.Console.WriteLine $"NATIVE_API C04 PASS assertions={typedStyles}"
        let fragmentRectangles = FragmentRectangles.run host.Document
        System.Console.WriteLine $"NATIVE_API G06 PASS assertions={fragmentRectangles}"
        let fontRules, fontWidthAlias = FontTransitionRules.fonts host.Document
        System.Console.WriteLine $"NATIVE_API C18 PASS assertions={fontRules}"
        System.Console.WriteLine $"NATIVE_API C18 CAPABILITY fontWidthAlias={fontWidthAlias}"
        let transitionRules = FontTransitionRules.transitions host.Document
        System.Console.WriteLine $"NATIVE_API C22 PASS assertions={transitionRules}"

        let positionTry, positionTryAvailable =
            PositionTry.run host.Document (fun () -> host.Frame ())

        if positionTry <> 44 || not positionTryAvailable then
            failwith $"Position fallback proof incomplete: assertions={positionTry}, available={positionTryAvailable}"

        System.Console.WriteLine $"NATIVE_API C21 PASS assertions={positionTry}"
        System.Console.WriteLine $"NATIVE_API C21 CAPABILITY positionTry={positionTryAvailable}"

        let counterStyles, counterSpeakAs =
            CounterStyleRules.run host.Document (fun () -> host.Frame ())

        if counterStyles <> 64 then
            failwith $"Counter style proof incomplete: assertions={counterStyles}"

        System.Console.WriteLine $"NATIVE_API C19 PASS assertions={counterStyles}"
        System.Console.WriteLine $"NATIVE_API C19 CAPABILITY speakAs={counterSpeakAs}"

        let keyframeRules, keyframeLength, invalidKeyTextError =
            KeyframeRules.run host.Document computedService (fun () -> host.Frame ()) (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)

        if
            keyframeRules <> 82
            || not keyframeLength
            || invalidKeyTextError <> ""
        then
            failwith $"Keyframe rule proof incomplete: assertions={keyframeRules}, length={keyframeLength}, error={invalidKeyTextError}"

        System.Console.WriteLine $"NATIVE_API C11 PASS assertions={keyframeRules}"
        System.Console.WriteLine $"NATIVE_API C11 CAPABILITY length={keyframeLength} invalidKeyTextError={invalidKeyTextError}"
        let lifetimeRectangles = host.Document.body.getClientRects ()
        let lifetimeRectangle = lifetimeRectangles.item 0u

        if isNull lifetimeRectangle then
            failwith "Rendered body has no lifetime-test rectangle"

        let rectangleReads =
            [|
                (fun () -> lifetimeRectangles.length |> ignore)
                (fun () -> lifetimeRectangles.item 0u |> ignore)
                (fun () -> lifetimeRectangle.left |> ignore)
            |]

        for read in rectangleReads do
            let mutable rejected = false

            let worker =
                System.Threading.Thread (
                    System.Threading.ThreadStart (fun () ->
                        try
                            read ()
                        with :? System.InvalidOperationException ->
                            rejected <- true)
                )

            worker.Start ()
            worker.Join ()

            if not rejected then
                failwith "Fragment geometry read accepted the wrong thread"

        let lifetimeComputedElement = host.Document.body
        let lifetimeComputedStyle = computedService.Get (lifetimeComputedElement, None)
        let lifetimeTypedStyle = lifetimeComputedElement.style
        let lifetimeTypedDeclaration = Ranvier.Css.Css.width (Ranvier.Css.Css.px 1.)
        let mutable foreignHostRejected = false

        do
            use otherHost = WebCoreHost.Create (16, 16)

            try
                computedService.Get (otherHost.Document.body, None)
                |> ignore
            with :? System.ArgumentException ->
                foreignHostRejected <- true

        if not foreignHostRejected then
            failwith "Computed-style provider accepted another host's element"

        let checks = Tables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10 PASS assertions={checks}"
        let hyperlinks = Hyperlinks.run host.Document
        System.Console.WriteLine $"NATIVE_API HYPERLINKS PASS assertions={hyperlinks}"
        let reflection = Reflection.run host.Document
        System.Console.WriteLine $"NATIVE_API D11 PASS assertions={reflection}"
        let namespaces = Namespaces.run host.Document (fun () -> host.Frame ())
        System.Console.WriteLine $"NATIVE_API D09 PASS assertions={namespaces}"
        let viewportOperations = ElementOperations.viewport host.Document

        if viewportOperations <> 30 then
            failwith "Selected SVG viewport assertions did not run"

        System.Console.WriteLine $"NATIVE_API D09_VIEWPORT PASS assertions={viewportOperations}"
        let svgStylesheets = Stylesheets.runSvg host.Document
        System.Console.WriteLine $"NATIVE_API SVG_CSSOM PASS assertions={svgStylesheets}"
        let stylesheets = Stylesheets.run host.Document
        System.Console.WriteLine $"NATIVE_API CSSOM PASS assertions={stylesheets}"

        let ownerContext = System.Threading.SynchronizationContext.Current

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) ->
                host.QueueMicrotask (fun () -> ownerContext.Post (System.Threading.SendOrPostCallback (fun _ -> success ()), null)))

        let computed (element: Ranvier.Dom.HTMLElement) property =
            let buffer = Array.zeroCreate<byte> 4096
            let length = [| 0 |]
            let handle = (box element :?> NativeObject).Handle

            let status =
                RanvierWebCoreAbi.Native.rv_element_computed_style (handle, property, buffer, buffer.Length, length)

            if status <> 0 then
                failwith $"Computed style returned {status}"

            System.Text.Encoding.UTF8.GetString (buffer, 0, length[0])

        let mutable construction: Result<int, exn> option = None

        Async.StartImmediate (
            async {
                try
                    let originalViewport = host.PaintSize

                    let! conditionalRules =
                        ConditionalRules.run
                            host.Document
                            computedService
                            originalViewport
                            (fun width height ->
                                async {
                                    host.Resize (width, height)
                                    host.Frame ()
                                })
                            (function
                             | WebCoreDomException (name, _) -> name
                             | error -> error.GetType().Name)

                    if host.PaintSize <> originalViewport then
                        failwith "Conditional rules did not restore the original viewport"

                    System.Console.WriteLine $"NATIVE_API C09 PASS assertions={conditionalRules}"

                    let! cascadeRules, scopeRules, nestedDeclarations, importContentsReadable =
                        CascadeRules.run host.Document computedService (fun () -> Async.Sleep 1) (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)

                    System.Console.WriteLine $"NATIVE_API C10 PASS assertions={cascadeRules}"

                    System.Console.WriteLine
                        $"NATIVE_API C10 CAPABILITY scopeRules={scopeRules} nestedDeclarations={nestedDeclarations} importContentsReadable={importContentsReadable}"

                    let! semanticOperations, attachmentIdentifier =
                        ElementOperations.semantic
                            host.Document
                            (fun () -> Async.Sleep 20)
                            (fun () -> host.Frame ())
                            (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)
                            (fun value -> if isNull value then "null" else "string")
                            (if System.OperatingSystem.IsWindows () then
                                 [ "string:" ]
                             else
                                 [ "string:"; "error:NotSupportedException" ])

                    if semanticOperations <> 26 then
                        failwith "Selected HTML operation assertions did not run"

                    System.Console.WriteLine $"NATIVE_API D11_OPERATIONS PASS assertions={semanticOperations}"
                    System.Console.WriteLine $"NATIVE_API D11 CAPABILITY attachmentIdentifier={attachmentIdentifier}"

                    let! sheetElements, failedLinkSheet =
                        SheetElements.run host.Document computed (fun () -> Async.Sleep 10) (function
                            | WebCoreDomException (name, _) -> name
                            | error -> error.GetType().Name)

                    System.Console.WriteLine $"NATIVE_API D12 PASS assertions={sheetElements}"

                    if sheetElements <> 109 then
                        failwith "Stylesheet/link element assertions did not run"

                    System.Console.WriteLine $"NATIVE_API D12 CAPABILITY failedLinkSheet={failedLinkSheet}"

                    let! checks =
                        SheetConstruction.run
                            host.Document
                            (fun () -> StyleSheets.forHost host)
                            checkpoint
                            computed
                            (fun error -> error.GetType().Name)
                            "ObjectDisposedException"

                    construction <- Some (Ok checks)
                with error ->
                    construction <- Some (Error error)
            }
        )

        let deadline = System.Diagnostics.Stopwatch.StartNew ()

        while construction.IsNone
              && deadline.Elapsed.TotalSeconds < 20.0 do
            host.Cycle 1

        match construction with
        | Some (Ok checks) ->
            if checks <> 78 then
                failwith "Constructed stylesheet preservation assertions did not run"

            System.Console.WriteLine $"NATIVE_API C07 PASS assertions={checks}"
        | Some (Error error) -> raise error
        | None -> failwith "Stylesheet construction fixture did not complete within 20 seconds"

        let svg =
            host.Document.getElementById ("native-api-svg") :?> Ranvier.Dom.HTMLOrSVGElement

        svg.focus ()

        if not (host.Key (0, "Tab", "Tab", "", 0, false)) then
            failwith "Native Tab traversal was not handled"

        let math = host.Document.getElementById ("native-api-math")

        if not (host.Document.activeElement.isSameNode (math)) then
            failwith "Native Tab did not move from SVG to MathML"

        System.Console.WriteLine "NATIVE_API D09 TAB PASS"

        let lifetimeService = StyleSheets.forHost host
        let lifetimeSheet = lifetimeService.Create Ranvier.Dom.StyleSheetOptions.Default
        let mutable wrongThreadRejected = false

        let worker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    Async.StartWithContinuations (
                        lifetimeService.ReplaceAsync (lifetimeSheet, ".wrong-thread {}"),
                        ignore,
                        (fun error -> wrongThreadRejected <- error :? System.InvalidOperationException),
                        ignore
                    ))
            )

        worker.Start ()
        worker.Join ()

        if not wrongThreadRejected then
            failwith "Stylesheet replacement was not rejected off the owning thread"

        let mutable queryWrongThreadRejected = false

        let queryWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        cssQueries.Supports "(display: block)" |> ignore
                    with :? System.InvalidOperationException ->
                        queryWrongThreadRejected <- true)
            )

        queryWorker.Start ()
        queryWorker.Join ()

        if not queryWrongThreadRejected then
            failwith "CSS support query was not rejected off the owning thread"

        let mutable computedWrongThreadRejected = false

        let computedWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        computedService.Get (lifetimeComputedElement, None)
                        |> ignore
                    with :? System.InvalidOperationException ->
                        computedWrongThreadRejected <- true)
            )

        computedWorker.Start ()
        computedWorker.Join ()

        if not computedWrongThreadRejected then
            failwith "Computed-style query was not rejected off the owning thread"

        let mutable typedWrongThreadRejected = false

        let typedWorker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    try
                        Ranvier.Css.Dom.StyleDeclaration.apply lifetimeTypedStyle lifetimeTypedDeclaration
                    with :? System.InvalidOperationException ->
                        typedWrongThreadRejected <- true)
            )

        typedWorker.Start ()
        typedWorker.Join ()

        if not typedWrongThreadRejected then
            failwith "Typed CSS write was not rejected off the owning thread"

        let mutable terminal = ""

        Async.StartWithContinuations (
            lifetimeService.ReplaceAsync (lifetimeSheet, ".before-host-dispose {}"),
            (fun _ -> terminal <- "success"),
            (fun _ -> terminal <- "error"),
            (fun _ -> terminal <- "cancel")
        )

        if terminal <> "" then
            failwith "Stylesheet replacement completed without a checkpoint"

        (host :> System.IDisposable).Dispose()

        for value in [ box lifetimeRectangles; box lifetimeRectangle ] do
            if (value :?> NativeObject).Handle <> 0n then
                failwith "Disposed host retained a fragment geometry handle"

        for read in rectangleReads do
            let mutable rejected = false

            try
                read ()
            with :? System.ObjectDisposedException ->
                rejected <- true

            if not rejected then
                failwith "Fragment geometry read accepted a disposed host"

        System.Console.WriteLine "NATIVE_API G06 LIFETIME PASS assertions=8"

        let mutable disposedQueryRejected = false

        try
            cssQueries.Supports ("display", "block") |> ignore
        with :? System.ObjectDisposedException ->
            disposedQueryRejected <- true

        if not disposedQueryRejected then
            failwith "CSS support query accepted a disposed host"

        let mutable disposedFactoryRejected = false

        try
            CssQueries.forHost host |> ignore
        with :? System.ObjectDisposedException ->
            disposedFactoryRejected <- true

        if not disposedFactoryRejected then
            failwith "CSS query provider accepted a disposed host"

        System.Console.WriteLine "NATIVE_API C03 LIFETIME PASS assertions=4"

        let mutable disposedComputedRejected = false

        try
            computedService.Get (lifetimeComputedElement, None)
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedComputedRejected <- true

        if not disposedComputedRejected then
            failwith "Computed-style query accepted a disposed host"

        let mutable disposedComputedFactoryRejected = false

        try
            Ranvier.View.WebCore.ComputedStyles.forHost host
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedComputedFactoryRejected <- true

        if not disposedComputedFactoryRejected then
            failwith "Computed-style provider accepted a disposed host"

        if
            (box lifetimeComputedStyle :?> NativeObject).Handle
            <> 0n
        then
            failwith "Disposed host retained a computed declaration handle"

        let mutable disposedDeclarationRejected = false

        try
            lifetimeComputedStyle.getPropertyValue "width"
            |> ignore
        with :? System.ObjectDisposedException ->
            disposedDeclarationRejected <- true

        if not disposedDeclarationRejected then
            failwith "Computed declaration accepted a read after host disposal"

        System.Console.WriteLine "NATIVE_API C01_C02 LIFETIME PASS assertions=7"

        let mutable disposedTypedRejected = false

        try
            Ranvier.Css.Dom.StyleDeclaration.apply lifetimeTypedStyle lifetimeTypedDeclaration
        with :? System.ObjectDisposedException ->
            disposedTypedRejected <- true

        if not disposedTypedRejected then
            failwith "Typed CSS write accepted a disposed host"

        System.Console.WriteLine "NATIVE_API C04 LIFETIME PASS assertions=2"

        if terminal <> "cancel" then
            failwith $"Host disposal did not terminate pending replacement: {terminal}"

        if (box lifetimeSheet :?> NativeObject).Handle <> 0n then
            failwith "Disposed host retained a constructed stylesheet handle"

        System.Console.WriteLine "NATIVE_API C07 LIFETIME PASS assertions=4"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"Shared API fixtures leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine "NATIVE_API TEARDOWN PASS groups=D09,D10,D11,CSSOM"
    0

[<EntryPoint>]
let main args =
    if Array.contains "--transforms-only" args then
        let lifecycle = TransformLifecycle.run ()
        let host = WebCoreHost.Create (64, 64)
        let session = host.Session

        try
            let count = runTransforms host

            if count <> 471 || lifecycle <> 47 then
                failwith "Typed transform focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C16 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C16 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Typed transform fixture leaked native wrappers"

        System.Console.WriteLine "NATIVE_API C16 TEARDOWN PASS"
        0
    elif Array.contains "--matrix-only" args then
        let lifecycle = MatrixLifecycle.run ()
        use host = WebCoreHost.Create (64, 64)

        let count =
            MatrixValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMMatrix)
                (function
                | WebCoreDomException (name, _) -> name
                | error -> error.GetType().Name)
                "ArgumentNullException"

        if count <> 710 || lifecycle <> 23 then
            failwith "Matrix shared/lifetime assertions did not run"

        System.Console.WriteLine $"NATIVE_API G08 PASS assertions={count}"
        System.Console.WriteLine $"NATIVE_API G08 LIFETIME PASS assertions={lifecycle}"
        (host :> System.IDisposable).Dispose()

        if host.Session.LiveObjects <> 0 then
            failwith "Matrix wrappers leaked"

        System.Console.WriteLine "NATIVE_API G08 TEARDOWN PASS"
        0
    elif Array.contains "--geometry-only" args then
        let lifecycle = GeometryLifecycle.run ()
        use host = WebCoreHost.Create (64, 64)

        let count =
            GeometryValues.run
                (fun () -> Geometry.forHost host)
                (fun value -> value :? Ranvier.Dom.DOMRect, value :? Ranvier.Dom.DOMPoint)
                (fun error -> error.GetType().Name)
                "ObjectDisposedException"

        if count <> 177 || lifecycle <> 26 then
            failwith "Geometry shared/lifetime assertions did not run"

        System.Console.WriteLine $"NATIVE_API G07 PASS assertions={count}"
        System.Console.WriteLine $"NATIVE_API G07 LIFETIME PASS assertions={lifecycle}"
        (host :> System.IDisposable).Dispose()

        if host.Session.LiveObjects <> 0 then
            failwith "Geometry wrappers leaked"

        System.Console.WriteLine "NATIVE_API G07 TEARDOWN PASS"
        0
    elif Array.contains "--style-maps-only" args then
        let lifecycle = StyleMapLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            use values = Ranvier.View.WebCore.StyleValues.forHost host
            let computed = Ranvier.View.WebCore.ComputedStyles.forHost host

            let count =
                StyleMaps.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.StyleMaps.forHost host)
                    values
                    computed
                    (fun () -> host.Frame ())
                    (function
                    | WebCoreDomException (name, _) -> name
                    | error -> error.GetType().Name)
                    "ObjectDisposedException"
                    (fun map -> (map :?> Ranvier.Dom.StylePropertyMap).set("width", Ranvier.Dom.CSSStyleValueOrUSVString.USVString "999px"))
                    "InvalidCastException"

            if count <> 127 || lifecycle <> 33 then
                failwith "Typed-map focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C13 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C13 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Style-map fixture leaked native wrappers"

        0
    elif Array.contains "--style-values-only" args then
        let lifecycle = StyleValueLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            let count =
                StyleValues.run
                    (fun () -> Ranvier.View.WebCore.StyleValues.forHost host)
                    (function
                    | WebCoreDomException (name, _) -> name
                    | error -> error.GetType().Name)
                    "ObjectDisposedException"
                    (fun value -> value :? Ranvier.Dom.CSSImageValue, value :? Ranvier.Dom.CSSNumericValue)

            if count <> 99 || lifecycle <> 19 then
                failwith "Typed-style focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C14 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C14 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith "Typed-style fixture leaked native wrappers"

        System.Console.WriteLine "NATIVE_API C14 TEARDOWN PASS"

        0
    elif Array.contains "--custom-properties-only" args then
        let lifecycle = CustomPropertyLifecycle.run ()
        let host = WebCoreHost.Create (320, 240)
        let session = host.Session

        try
            let count =
                CustomProperties.run
                    host.Document
                    (fun () -> Ranvier.View.WebCore.CustomProperties.forHost host)
                    (Ranvier.View.WebCore.ComputedStyles.forHost host)
                    (fun error -> error.GetType().Name)
                    "ObjectDisposedException"

            if count <> 50 || lifecycle <> 10 then
                failwith "Custom-property focused shared/lifetime assertions did not run"

            System.Console.WriteLine $"NATIVE_API C12 PASS assertions={count}"
            System.Console.WriteLine $"NATIVE_API C12 LIFETIME PASS assertions={lifecycle}"
        finally
            (host :> System.IDisposable).Dispose()

        if session.LiveObjects <> 0 then
            failwith $"Custom-property fixture leaked {session.LiveObjects} managed native objects"

        System.Console.WriteLine "NATIVE_API C12 TEARDOWN PASS"
        0
    else
        runAll ()
