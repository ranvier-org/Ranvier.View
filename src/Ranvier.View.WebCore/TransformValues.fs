namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for actual Typed OM transform components and lists.</summary>
module TransformValues =
    type private Create = delegate of nativeint * byref<nativeint> -> int

    /// <summary>Creates a provider bound to a live document and its native session.</summary>
    /// <param name="document">The construction context retained by returned component/list wrappers.</param>
    /// <remarks>Provider disposal preserves returned objects. Native values require the live host session and owning thread.</remarks>
    let forDocument (document: Document) : TransformValueService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        document.nodeType |> ignore

        for name in
            [
                "CSSTransformValue"
                "CSSTranslate"
                "CSSRotate"
                "CSSScale"
                "CSSSkew"
                "CSSSkewX"
                "CSSSkewY"
                "CSSPerspective"
                "CSSMatrixComponent"
            ] do
            let feature = "dom-constructor:" + name

            if not (WebCoreHost.Has feature) then
                Interop.unsupported<unit> feature

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A transform service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "TransformValueService")

            Interop.handle (box owner)

        let numeric value =
            Interop.valueHandle session (box value)

        let numberish name value =
            if isNull (box value) then
                nullArg name

            let mutable input = NativeDom.RvDomUnionDoubleOrCSSNumericValue ()
            input.present <- 1

            match value with
            | DoubleOrCSSNumericValue.Double value ->
                input.tag <- 1
                input.arm0 <- value
            | DoubleOrCSSNumericValue.CSSNumericValue value ->
                input.tag <- 2
                input.arm1 <- numeric value

            input

        let perspective (scope: InputScope) value =
            if isNull (box value) then
                nullArg (nameof value)

            let mutable input =
                NativeDom.RvDomUnionCSSNumericValueOrDOMStringOrCSSOMKeywordValue ()

            input.present <- 1

            match value with
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSNumericValue value ->
                input.tag <- 1
                input.arm0 <- numeric value
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString value ->
                input.tag <- 2
                input.arm1 <- scope.TextSpan value
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSOMKeywordValue value ->
                input.tag <- 3
                input.arm2 <- Interop.valueHandle session (box value)

            input

        let engine action =
            try
                action ()
            with WebCoreDomException (name, detail) ->
                raise (StyleValueException (name, detail))

        let result kind (factory: Session -> nativeint -> NativeObject) (action: Create) =
            let target = active ()

            engine (fun () ->
                let mutable output = 0n

                action.Invoke (target, &output)
                |> Interop.check "Typed transform construction"

                session.Object (
                    output,
                    kind,
                    owner,
                    Func<Session, nativeint, NativeObject>(fun s h ->
                        let value = factory s h
                        value.BindDocumentContext owner
                        value)
                ))

        let componentResult action =
            result Kind.TransformComponent (fun s h -> Classes.CSSTransformComponent (s, h)) action

        { new TransformValueService with
            member _.CreateList components =
                try
                    active () |> ignore

                    if isNull components then
                        nullArg (nameof components)

                    let handles =
                        components
                        |> Array.map (fun value -> Interop.transformHandle session Kind.TransformComponent (box value))

                    use scope = new InputScope ()
                    let mutable input = NativeDom.RvDomSequenceCSSTransformComponent ()
                    input.present <- 1
                    input.count <- handles.Length
                    input.items <- scope.Pin handles

                    result
                        Kind.StyleValue
                        (fun s h -> Classes.CSSStyleValue (s, h))
                        (Create (fun target output -> NativeDom.rv_dom_css_transform_value_create (target, [| input |], &output)))
                    |> unbox<CSSTransformValue>
                finally
                    GC.KeepAlive (box (components))

            member _.CreateTranslate(x, y) =
                try
                    active () |> ignore
                    let x, y = numeric x, numeric y

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_translate_create (target, x, y, 0n, &output)))
                    |> unbox<CSSTranslate>
                finally
                    GC.KeepAlive (box (x, y))

            member _.CreateTranslate(x, y, z) =
                try
                    active () |> ignore
                    let x, y, z = numeric x, numeric y, numeric z

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_translate_create (target, x, y, z, &output)))
                    |> unbox<CSSTranslate>
                finally
                    GC.KeepAlive (box (x, y, z))

            member _.CreateRotate(angle) =
                try
                    active () |> ignore
                    let angle = numeric angle

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_rotate_create_1 (target, angle, &output)))
                    |> unbox<CSSRotate>
                finally
                    GC.KeepAlive (box (angle))

            member _.CreateRotate(x, y, z, angle) =
                try
                    active () |> ignore
                    let x, y, z = numberish (nameof x) x, numberish (nameof y) y, numberish (nameof z) z
                    let angle = numeric angle

                    componentResult (
                        Create (fun target output -> NativeDom.rv_dom_css_rotate_create_4 (target, [| x |], [| y |], [| z |], angle, &output))
                    )
                    |> unbox<CSSRotate>
                finally
                    GC.KeepAlive (box (x, y, z, angle))

            member _.CreateScale(x, y) =
                try
                    active () |> ignore
                    let x, y = numberish (nameof x) x, numberish (nameof y) y

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_scale_create (target, [| x |], [| y |], null, &output)))
                    |> unbox<CSSScale>
                finally
                    GC.KeepAlive (box (x, y))

            member _.CreateScale(x, y, z) =
                try
                    active () |> ignore
                    let x, y, z = numberish (nameof x) x, numberish (nameof y) y, numberish (nameof z) z

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_scale_create (target, [| x |], [| y |], [| z |], &output)))
                    |> unbox<CSSScale>
                finally
                    GC.KeepAlive (box (x, y, z))

            member _.CreateSkew(ax, ay) =
                try
                    active () |> ignore
                    let ax, ay = numeric ax, numeric ay

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_skew_create (target, ax, ay, &output)))
                    |> unbox<CSSSkew>
                finally
                    GC.KeepAlive (box (ax, ay))

            member _.CreateSkewX ax =
                try
                    active () |> ignore
                    let ax = numeric ax

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_skew_x_create (target, ax, &output)))
                    |> unbox<CSSSkewX>
                finally
                    GC.KeepAlive (box (ax))

            member _.CreateSkewY ay =
                try
                    active () |> ignore
                    let ay = numeric ay

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_skew_y_create (target, ay, &output)))
                    |> unbox<CSSSkewY>
                finally
                    GC.KeepAlive (box (ay))

            member _.CreatePerspective length =
                try
                    active () |> ignore
                    use scope = new InputScope ()
                    let input = perspective scope length

                    componentResult (Create (fun target output -> NativeDom.rv_dom_css_perspective_create (target, [| input |], &output)))
                    |> unbox<CSSPerspective>
                finally
                    GC.KeepAlive (box (length))

            member _.CreateMatrixComponent(matrix, ?is2D) =
                try
                    active () |> ignore
                    let matrix = Interop.transformHandle session Kind.Matrix (box matrix)
                    use scope = new InputScope ()
                    let mutable input = NativeDom.RvDomCSSMatrixComponentOptions ()
                    input.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomCSSMatrixComponentOptions>())

                    input.is2_d <-
                        match is2D with
                        | None -> 0n
                        | Some value -> scope.Pin [| if value then 1 else 0 |]

                    componentResult (
                        Create (fun target output -> NativeDom.rv_dom_css_matrix_component_create (target, matrix, [| input |], &output))
                    )
                    |> unbox<CSSMatrixComponent>
                finally
                    GC.KeepAlive (box (matrix, is2D))

            member _.Get(value, index) =
                try
                    active () |> ignore
                    Interop.valueHandle session (box value) |> ignore

                    engine (fun () ->
                        if index >= value.length then
                            None
                        else
                            let entry = value.item index in if isNull entry then None else Some entry)
                finally
                    GC.KeepAlive (box (value, index))

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    invalidOp "A transform service is disposed on its document's owning thread"

                disposed <- true
        }

    /// <summary>Creates a borrowed typed transform provider for the live host's document.</summary>
    /// <param name="host">The host supplying the construction context and lifetime.</param>
    let forHost (host: WebCoreHost) : TransformValueService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
