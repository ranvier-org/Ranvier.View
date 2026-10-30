namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Borrowed document providers for native point, rectangle and quad values.</summary>
module Geometry =
    type private Create = delegate of nativeint * byref<nativeint> -> int

    let private pointInput (init: DOMPointInit) =
        let init = if isNull init then DOMPointInit () else init
        let mutable value = NativeDom.RvDomDOMPointInit ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDOMPointInit>())
        value.x <- init.x
        value.y <- init.y
        value.z <- init.z
        value.w <- init.w
        value

    let private rectInput (init: DOMRectInit) =
        let init = if isNull (box init) then DOMRectInit.Empty else init
        let mutable value = NativeDom.RvDomDOMRectInit ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDOMRectInit>())
        value.x <- defaultArg init.X 0.
        value.y <- defaultArg init.Y 0.
        value.width <- defaultArg init.Width 0.
        value.height <- defaultArg init.Height 0.
        value

    let private matrixInput (scope: InputScope) (init: DOMMatrixInit) =
        let init = if isNull init then DOMMatrixInit () else init

        let number value =
            match value with
            | None -> 0n
            | Some value -> scope.Pin [| value |]

        let mutable value = NativeDom.RvDomDOMMatrixInit ()
        value.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDOMMatrixInit>())
        value.a <- number init.a
        value.b <- number init.b
        value.c <- number init.c
        value.d <- number init.d
        value.e <- number init.e
        value.f <- number init.f
        value.m11 <- number init.m11
        value.m12 <- number init.m12
        value.m21 <- number init.m21
        value.m22 <- number init.m22
        value.m41 <- number init.m41
        value.m42 <- number init.m42
        value.m13 <- init.m13
        value.m14 <- init.m14
        value.m23 <- init.m23
        value.m24 <- init.m24
        value.m31 <- init.m31
        value.m32 <- init.m32
        value.m33 <- init.m33
        value.m34 <- init.m34
        value.m43 <- init.m43
        value.m44 <- init.m44

        value.is2_d <-
            match init.is2D with
            | None -> 0n
            | Some value -> scope.Pin [| if value then 1 else 0 |]

        value

    let private matrixString (scope: InputScope) text =
        let mutable value = NativeDom.RvDomUnionDOMStringOrSequenceOfUnrestrictedDouble ()
        value.present <- 1
        value.tag <- 1
        value.arm0 <- scope.TextSpan text
        value

    let private matrixValues (scope: InputScope) (values: float array) =
        if isNull values then
            nullArg (nameof values)

        let mutable sequence = NativeDom.RvDomSequenceUnrestrictedDouble ()
        sequence.present <- 1
        sequence.count <- values.Length
        sequence.items <- scope.Pin values
        let mutable value = NativeDom.RvDomUnionDOMStringOrSequenceOfUnrestrictedDouble ()
        value.present <- 1
        value.tag <- 2
        value.arm1 <- sequence
        value

    /// <summary>Creates a borrowed geometry provider bound to a real document and its owning native session.</summary>
    /// <param name="document">The live document supplying the construction context.</param>
    /// <remarks>Provider disposal preserves returned geometry values. Values require their live host session and owning thread.</remarks>
    let forDocument (document: Document) : GeometryService =
        if isNull (box document) then
            nullArg (nameof document)

        let owner =
            match box document with
            | :? NativeObject as value -> value
            | _ -> invalidArg (nameof document) "The document must belong to the native backend"

        document.nodeType |> ignore

        for feature in
            [
                "dom-geometry-rect"
                "dom-geometry-rect-readonly"
                "dom-geometry-point"
                "dom-geometry-point-readonly"
                "dom-geometry-quad"
                "dom-geometry-matrix"
                "dom-geometry-matrix-readonly"
            ] do
            if not (WebCoreHost.Has feature) then
                Interop.unsupported<unit> feature

        let session = owner.Session
        let thread = Environment.CurrentManagedThreadId
        let mutable disposed = false

        let active () =
            if Environment.CurrentManagedThreadId <> thread then
                invalidOp "A geometry service is used on its document's owning thread"

            if disposed then
                raise (ObjectDisposedException "GeometryService")

            Interop.handle (box owner)

        let result kind factory (action: Create) =
            let target = active ()

            try
                let mutable handle = 0n

                action.Invoke (target, &handle)
                |> Interop.check "Geometry construction"

                session.Object (handle, kind, owner, Func<Session, nativeint, NativeObject>(factory))
            with WebCoreDomException (name, detail) ->
                raise (GeometryException (name, detail))

        let rect action =
            result Kind.Rect (fun s h -> Classes.DOMRectReadOnly (s, h)) action
            |> unbox<DOMRectReadOnly>

        let point action =
            result Kind.Point (fun s h -> Classes.DOMPointReadOnly (s, h)) action
            |> unbox<DOMPointReadOnly>

        let quad action =
            result Kind.Quad (fun s h -> DOMQuadObject (s, h) :> NativeObject) action
            |> unbox<DOMQuad>

        let matrix action =
            result
                Kind.Matrix
                (fun s h ->
                    let value = Classes.DOMMatrixReadOnly (s, h)
                    value.BindDocumentContext owner
                    value)
                action
            |> unbox<DOMMatrixReadOnly>

        { new GeometryService with
            member _.CreateRect(?x, ?y, ?width, ?height) =
                rect (
                    Create (fun target output ->
                        NativeDom.rv_dom_dom_rect_create (
                            target,
                            defaultArg x 0.,
                            defaultArg y 0.,
                            defaultArg width 0.,
                            defaultArg height 0.,
                            &output
                        ))
                )
                :?> DOMRect

            member _.CreateReadOnlyRect(?x, ?y, ?width, ?height) =
                rect (
                    Create (fun target output ->
                        NativeDom.rv_dom_dom_rect_read_only_create (
                            target,
                            defaultArg x 0.,
                            defaultArg y 0.,
                            defaultArg width 0.,
                            defaultArg height 0.,
                            &output
                        ))
                )

            member _.RectFrom init =
                rect (Create (fun target output -> NativeDom.rv_dom_dom_rect_from_rect (target, [| rectInput init |], &output))) :?> DOMRect

            member _.ReadOnlyRectFrom init =
                rect (Create (fun target output -> NativeDom.rv_dom_dom_rect_read_only_from_rect (target, [| rectInput init |], &output)))

            member _.CreatePoint(?x, ?y, ?z, ?w) =
                point (
                    Create (fun target output ->
                        NativeDom.rv_dom_dom_point_create (target, defaultArg x 0., defaultArg y 0., defaultArg z 0., defaultArg w 1., &output))
                )
                :?> DOMPoint

            member _.CreateReadOnlyPoint(?x, ?y, ?z, ?w) =
                point (
                    Create (fun target output ->
                        NativeDom.rv_dom_dom_point_read_only_create (
                            target,
                            defaultArg x 0.,
                            defaultArg y 0.,
                            defaultArg z 0.,
                            defaultArg w 1.,
                            &output
                        ))
                )

            member _.PointFrom init =
                point (Create (fun target output -> NativeDom.rv_dom_dom_point_from_point (target, [| pointInput init |], &output))) :?> DOMPoint

            member _.ReadOnlyPointFrom init =
                point (Create (fun target output -> NativeDom.rv_dom_dom_point_read_only_from_point (target, [| pointInput init |], &output)))

            member _.CreateQuad(?p1, ?p2, ?p3, ?p4) =
                quad (
                    Create (fun target output ->
                        NativeDom.rv_dom_dom_quad_create (
                            target,
                            [| pointInput (defaultArg p1 Unchecked.defaultof<_>) |],
                            [| pointInput (defaultArg p2 Unchecked.defaultof<_>) |],
                            [| pointInput (defaultArg p3 Unchecked.defaultof<_>) |],
                            [| pointInput (defaultArg p4 Unchecked.defaultof<_>) |],
                            &output
                        ))
                )

            member _.QuadFromRect init =
                quad (Create (fun target output -> NativeDom.rv_dom_dom_quad_from_rect (target, [| rectInput init |], &output)))

            member _.QuadFrom init =
                let target = active ()
                let init = if isNull (box init) then DOMQuadInit.Empty else init
                use scope = new InputScope ()

                let corner value =
                    match value with
                    | None -> 0n
                    | Some value -> scope.Pin [| pointInput value |]

                let mutable input = NativeDom.RvDomDOMQuadInit ()
                input.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDOMQuadInit>())
                input.p1 <- corner init.P1
                input.p2 <- corner init.P2
                input.p3 <- corner init.P3
                input.p4 <- corner init.P4
                quad (Create (fun _ output -> NativeDom.rv_dom_dom_quad_from_quad (target, [| input |], &output)))

            member _.CreateMatrix() =
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_create (target, null, &output))) :?> DOMMatrix

            member _.CreateMatrix(text: string) =
                active () |> ignore
                use scope = new InputScope ()

                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_create (target, [| matrixString scope text |], &output)))
                :?> DOMMatrix

            member _.CreateMatrix(values: float array) =
                active () |> ignore
                use scope = new InputScope ()
                let input = matrixValues scope values
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_create (target, [| input |], &output))) :?> DOMMatrix

            member _.CreateReadOnlyMatrix() =
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_read_only_create (target, null, &output)))

            member _.CreateReadOnlyMatrix(text: string) =
                active () |> ignore
                use scope = new InputScope ()
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_read_only_create (target, [| matrixString scope text |], &output)))

            member _.CreateReadOnlyMatrix(values: float array) =
                active () |> ignore
                use scope = new InputScope ()
                let input = matrixValues scope values
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_read_only_create (target, [| input |], &output)))

            member _.MatrixFrom init =
                active () |> ignore
                use scope = new InputScope ()
                let input = matrixInput scope init
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_from_matrix (target, [| input |], &output))) :?> DOMMatrix

            member _.ReadOnlyMatrixFrom init =
                active () |> ignore
                use scope = new InputScope ()
                let input = matrixInput scope init
                matrix (Create (fun target output -> NativeDom.rv_dom_dom_matrix_read_only_from_matrix (target, [| input |], &output)))

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    invalidOp "A geometry service is disposed on its document's owning thread"

                disposed <- true
        }

    /// <summary>Creates a borrowed geometry provider bound to the live host's document.</summary>
    /// <param name="host">The host supplying the construction context and lifetime.</param>
    let forHost (host: WebCoreHost) : GeometryService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        forDocument host.Document
