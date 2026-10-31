namespace Ranvier.View.WebCore

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices
open Ranvier
open RanvierWebCoreAbi

/// <summary>The release function of a handle, by the root of its interface.</summary>
module internal Kind =
    [<Literal>]
    let Node = 0

    [<Literal>]
    let Event = 1

    [<Literal>]
    let Style = 2

    [<Literal>]
    let TokenList = 3

    [<Literal>]
    let StringMap = 4

    [<Literal>]
    let Range = 5

    [<Literal>]
    let Selection = 6

    [<Literal>]
    let Rect = 7

    [<Literal>]
    let NodeList = 8

    [<Literal>]
    let Collection = 9

    [<Literal>]
    let Validity = 10

    [<Literal>]
    let DOMImplementation = 11

    [<Literal>]
    let NamedNodeMap = 12

    [<Literal>]
    let CSSRule = 13

    [<Literal>]
    let CSSRuleList = 14

    [<Literal>]
    let StyleSheet = 15

    [<Literal>]
    let StyleSheetList = 16

    [<Literal>]
    let MediaList = 17

    [<Literal>]
    let RectList = 18

    [<Literal>]
    let StyleValue = 19

    [<Literal>]
    let VariableReferenceValue = 20

    [<Literal>]
    let StyleMap = 21

    [<Literal>]
    let Point = 22

    [<Literal>]
    let Quad = 23

    [<Literal>]
    let Matrix = 24

    [<Literal>]
    let TransformComponent = 25

    [<Literal>]
    let SVGRect = 26

    [<Literal>]
    let SVGMatrix = 27

    [<Literal>]
    let SVGPoint = 28

    [<Literal>]
    let SVGAnimatedRect = 29

    [<Literal>]
    let SVGPreserveAspectRatio = 30

    [<Literal>]
    let SVGAnimatedPreserveAspectRatio = 31

    [<Literal>]
    let SVGTransform = 32

    [<Literal>]
    let SVGTransformList = 33

    [<Literal>]
    let SVGAnimatedTransformList = 34

    [<Literal>]
    let SVGNumber = 35

    [<Literal>]
    let SVGLength = 36

    [<Literal>]
    let SVGAngle = 37

    [<Literal>]
    let SVGNumberList = 38

    [<Literal>]
    let SVGLengthList = 39

    [<Literal>]
    let SVGPointList = 40

    [<Literal>]
    let SVGAnimatedNumber = 41

    [<Literal>]
    let SVGAnimatedLength = 42

    [<Literal>]
    let SVGAnimatedLengthList = 43

    [<Literal>]
    let SVGAnimatedNumberList = 44

    [<Literal>]
    let SVGAnimatedEnumeration = 45

    [<Literal>]
    let SVGAnimatedString = 46

    [<Literal>]
    let SVGAnimatedBoolean = 47

    [<Literal>]
    let SVGStringList = 48

    [<Literal>]
    let SVGViewSpec = 49

    [<Literal>]
    let SVGAnimatedAngle = 50

    [<Literal>]
    let SVGAnimatedInteger = 51

    [<Literal>]
    let EventTarget = 52

    [<Literal>]
    let NodeIterator = 53

    [<Literal>]
    let TreeWalker = 54

    [<Literal>]
    let FormData = 55

    [<Literal>]
    let Blob = 56

    [<Literal>]
    let FileList = 57

    [<Literal>]
    let CaretPosition = 58

    [<Literal>]
    let Url = 59

    [<Literal>]
    let UrlSearchParams = 60

    [<Literal>]
    let UrlPattern = 61

    [<Literal>]
    let VisualViewport = 74

    [<Literal>]
    let Highlight = 86

    [<Literal>]
    let HighlightRegistry = 87

    [<Literal>]
    let FileReader = 98

    // R01 placeholder kinds; root assigns the integrated numbers.
    [<Literal>]
    let CanvasRenderingContext2D = 99

    [<Literal>]
    let CanvasGradient = 100

    [<Literal>]
    let CanvasPattern = 101

    // R02 placeholder kind; root assigns the integrated number.
    [<Literal>]
    let Path2D = 102

    // R04 placeholder kind (shared with R06); root assigns the integrated number.
    [<Literal>]
    let ImageBitmap = 103

/// <summary>A DOM exception WebCore raised; <c>name</c> is the DOM exception name, such as <c>NotFoundError</c>.</summary>
exception WebCoreDomException of name: string * detail: string with
    override this.Message = this.name + ": " + this.detail

/// <summary>A native event listener: its target, type, phase, the callback it was added with, and its registration.</summary>
[<Sealed; AllowNullLiteral>]
type internal Registration(target: nativeint, eventType: string, capture: bool, callback: obj, invoke: obj -> unit) =
    member _.Target = target
    member _.Type = eventType
    member _.Capture = capture
    member _.Callback = callback
    member _.Invoke = invoke
    member val Listener = 0n with get, set
    member val Context = Runtime.InteropServices.GCHandle () with get, set

/// <summary>
/// A managed object over a native WebCore handle. The object holds one reference to the handle; a node or an event has
/// one object per handle.
/// </summary>
/// <remarks>
/// Collection of the object queues its reference's release, which the host thread drains on its next loop cycle. Host
/// disposal releases every reference and leaves <c>Handle</c> zero.
/// </remarks>
[<AbstractClass; AllowNullLiteral>]
type NativeObject(session: Session, handle: nativeint, kind: int) =
    let mutable handle = handle
    let lifetimeAnchors = List<NativeObject>()
    let mutable contextDocument: NativeObject = null

    /// <summary>The session of the host the object belongs to.</summary>
    member _.Session = session

    /// <summary>The native handle, or zero after host disposal.</summary>
    member _.Handle = handle

    member internal _.Kind = kind

    member internal _.ContextDocument = contextDocument

    member internal _.LifetimeAnchors = lifetimeAnchors :> seq<NativeObject>

    /// <summary>Binds the first checked document context without rebinding cached objects.</summary>
    member internal this.BindDocumentContext(document: NativeObject) =
        if isNull document then
            nullArg (nameof document)

        if
            not (obj.ReferenceEquals (session, document.Session))
            || not (box document :? Ranvier.Dom.Document)
        then
            invalidArg (nameof document) "The document must belong to this object's native session"

        if document.Handle = 0n then
            raise (ObjectDisposedException "Document context")

        if isNull contextDocument then
            contextDocument <- document
            this.KeepAlive document

    /// <summary>Keeps an owner wrapper reachable until this object is detached or collected.</summary>
    member internal this.KeepAlive(anchor: NativeObject) =
        if
            not (isNull anchor)
            && not (obj.ReferenceEquals (this, anchor))
        then
            lifetimeAnchors.Add anchor

    member val internal Expandos: Ranvier.View.Native.ExpandoSlots = null with get, set

    /// <summary>The owner whose claim keeps the object alive, or null.</summary>
    member val internal ClaimedBy: Owner = Unchecked.defaultof<_> with get, set

    member val internal CurrentTargetOverride: unit -> obj = Unchecked.defaultof<_> with get, set

    /// <summary>The init fields of an event created by the host, which take precedence over the native values.</summary>
    member val internal Init: Dictionary<string, obj> = null with get, set

    /// <summary>Releases the reference now, on the host thread.</summary>
    member internal this.Detach() =
        if handle <> 0n then
            Session.Release (kind, handle)
            handle <- 0n
            lifetimeAnchors.Clear ()
            contextDocument <- null
            GC.SuppressFinalize this

    override _.Finalize() =
        if handle <> 0n then
            session.QueueRelease (kind, handle)

    override _.ToString() =
        $"{session.KindName kind} 0x{handle:x}"

/// <summary>
/// The managed objects of one WebCore host: the node and event table, the owner claims that keep objects alive, and the
/// queue of releases from collected objects.
/// </summary>
and [<Sealed; AllowNullLiteral>] Session internal (host: nativeint) =
    let table = Dictionary<nativeint, WeakReference<NativeObject>>()

    let objectTable =
        Dictionary<struct (int * nativeint), WeakReference<NativeObject>>()

    let others = List<WeakReference<NativeObject>>()
    let releases = ConcurrentQueue<struct (int * nativeint)>()
    let held = HashSet<NativeObject>(HashIdentity.Reference)
    let claims = ConditionalWeakTable<Owner, HashSet<NativeObject>>()
    let errors = List<exn>()
    let mutable disposed = false
    let mutable released = 0

    static member internal Release(kind: int, handle: nativeint) =
        match kind with
        | Kind.Node -> Native.rv_node_release handle
        | Kind.Event -> Native.rv_event_release handle
        | Kind.Style -> NativeDom.rv_dom_css_style_declaration_release handle
        | Kind.TokenList -> NativeDom.rv_dom_dom_token_list_release handle
        | Kind.StringMap -> NativeDom.rv_dom_dom_string_map_release handle
        | Kind.Range -> NativeDom.rv_dom_abstract_range_release handle
        | Kind.Selection -> NativeDom.rv_dom_dom_selection_release handle
        | Kind.Rect -> NativeDom.rv_dom_dom_rect_read_only_release handle
        | Kind.NodeList -> NativeDom.rv_dom_node_list_release handle
        | Kind.Collection -> NativeDom.rv_dom_html_collection_release handle
        | Kind.Validity -> NativeDom.rv_dom_validity_state_release handle
        | Kind.DOMImplementation -> NativeDom.rv_dom_dom_implementation_release handle
        | Kind.NamedNodeMap -> NativeDom.rv_dom_named_node_map_release handle
        | Kind.CSSRule -> NativeDom.rv_dom_css_rule_release handle
        | Kind.CSSRuleList -> NativeDom.rv_dom_css_rule_list_release handle
        | Kind.StyleSheet -> NativeDom.rv_dom_style_sheet_release handle
        | Kind.StyleSheetList -> NativeDom.rv_dom_style_sheet_list_release handle
        | Kind.MediaList -> NativeDom.rv_dom_media_list_release handle
        | Kind.RectList -> NativeDom.rv_dom_dom_rect_list_release handle
        | Kind.StyleValue -> NativeDom.rv_dom_css_style_value_release handle
        | Kind.VariableReferenceValue -> NativeDom.rv_dom_cssom_variable_reference_value_release handle
        | Kind.StyleMap -> NativeDom.rv_dom_style_property_map_read_only_release handle
        | Kind.Point -> NativeDom.rv_dom_dom_point_read_only_release handle
        | Kind.Quad -> NativeDom.rv_dom_dom_quad_release handle
        | Kind.Matrix -> NativeDom.rv_dom_dom_matrix_read_only_release handle
        | Kind.TransformComponent -> NativeDom.rv_dom_css_transform_component_release handle
        | Kind.SVGRect -> NativeDom.rv_dom_svg_rect_release handle
        | Kind.SVGMatrix -> NativeDom.rv_dom_svg_matrix_release handle
        | Kind.SVGPoint -> NativeDom.rv_dom_svg_point_release handle
        | Kind.SVGAnimatedRect -> NativeDom.rv_dom_svg_animated_rect_release handle
        | Kind.SVGPreserveAspectRatio -> NativeDom.rv_dom_svg_preserve_aspect_ratio_release handle
        | Kind.SVGAnimatedPreserveAspectRatio -> NativeDom.rv_dom_svg_animated_preserve_aspect_ratio_release handle
        | Kind.SVGTransform -> NativeDom.rv_dom_svg_transform_release handle
        | Kind.SVGTransformList -> NativeDom.rv_dom_svg_transform_list_release handle
        | Kind.SVGAnimatedTransformList -> NativeDom.rv_dom_svg_animated_transform_list_release handle
        | Kind.SVGNumber -> NativeDom.rv_dom_svg_number_release handle
        | Kind.SVGLength -> NativeDom.rv_dom_svg_length_release handle
        | Kind.SVGAngle -> NativeDom.rv_dom_svg_angle_release handle
        | Kind.SVGNumberList -> NativeDom.rv_dom_svg_number_list_release handle
        | Kind.SVGLengthList -> NativeDom.rv_dom_svg_length_list_release handle
        | Kind.SVGPointList -> NativeDom.rv_dom_svg_point_list_release handle
        | Kind.SVGAnimatedNumber -> NativeDom.rv_dom_svg_animated_number_release handle
        | Kind.SVGAnimatedLength -> NativeDom.rv_dom_svg_animated_length_release handle
        | Kind.SVGAnimatedLengthList -> NativeDom.rv_dom_svg_animated_length_list_release handle
        | Kind.SVGAnimatedNumberList -> NativeDom.rv_dom_svg_animated_number_list_release handle
        | Kind.SVGAnimatedEnumeration -> NativeDom.rv_dom_svg_animated_enumeration_release handle
        | Kind.SVGAnimatedString -> NativeDom.rv_dom_svg_animated_string_release handle
        | Kind.SVGAnimatedBoolean -> NativeDom.rv_dom_svg_animated_boolean_release handle
        | Kind.SVGStringList -> NativeDom.rv_dom_svg_string_list_release handle
        | Kind.SVGViewSpec -> NativeDom.rv_dom_svg_view_spec_release handle
        | Kind.SVGAnimatedAngle -> NativeDom.rv_dom_svg_animated_angle_release handle
        | Kind.SVGAnimatedInteger -> NativeDom.rv_dom_svg_animated_integer_release handle
        | Kind.EventTarget -> Native.rv_event_target_release handle
        | Kind.NodeIterator -> NativeDom.rv_dom_node_iterator_release handle
        | Kind.TreeWalker -> NativeDom.rv_dom_tree_walker_release handle
        | Kind.FormData -> NativeDom.rv_dom_dom_form_data_release handle
        | Kind.Blob -> NativeDom.rv_dom_blob_release handle
        | Kind.FileList -> NativeDom.rv_dom_file_list_release handle
        | Kind.CaretPosition -> NativeDom.rv_dom_caret_position_release handle
        | Kind.Url -> NativeDom.rv_dom_domurl_release handle
        | Kind.UrlSearchParams -> NativeDom.rv_dom_url_search_params_release handle
        | Kind.UrlPattern -> NativeDom.rv_dom_url_pattern_release handle
        | Kind.VisualViewport -> Native.rv_event_target_release handle
        | Kind.FileReader -> Native.rv_event_target_release handle
        | Kind.Highlight -> NativeDom.rv_dom_highlight_release handle
        | Kind.HighlightRegistry -> NativeDom.rv_dom_highlight_registry_release handle
        | Kind.CanvasRenderingContext2D -> NativeDom.rv_dom_canvas_rendering_context2_d_release handle
        | Kind.CanvasGradient -> NativeDom.rv_dom_canvas_gradient_release handle
        | Kind.CanvasPattern -> NativeDom.rv_dom_canvas_pattern_release handle
        | Kind.Path2D -> NativeDom.rv_dom_path2_d_release handle
        | Kind.ImageBitmap -> NativeDom.rv_dom_image_bitmap_release handle
        | _ -> ()

    member internal _.KindName(kind: int) =
        match kind with
        | Kind.Node -> "node"
        | Kind.Event -> "event"
        | _ -> "object"

    /// <summary>The <c>RvHost</c> handle.</summary>
    member _.Host = host

    member val internal Registrations = HashSet<Registration>(HashIdentity.Reference)

    /// <summary>The interceptions of members by name, in the order they were added; a null target is every node.</summary>
    member val internal Interceptions = List<struct (obj * string * Func<Func<obj array, obj>, obj, obj array, obj>)>()

    member internal this.Intercepting = this.Interceptions.Count > 0

    /// <summary>The listener calls and managed dispatches in progress.</summary>
    member val internal ScriptDepth = 0 with get, set

    /// <summary>Runs after a listener call that leaves no script on the stack.</summary>
    member val internal AfterListener: Action = null with get, set

    member val internal NodeFactory: Func<Session, nativeint, NativeObject> = null with get, set
    member val internal EventFactory: Func<Session, nativeint, NativeObject> = null with get, set
    member val internal TargetFactory: Func<Session, nativeint, NativeObject> = null with get, set

    /// <summary>The exceptions raised by event listeners, in raise order; each dispatch continues past them.</summary>
    member _.ListenerErrors = errors

    /// <summary>The live node and event objects.</summary>
    member _.LiveObjects =
        let mutable count = 0

        for weak in table.Values do
            match weak.TryGetTarget () with
            | true, _ -> count <- count + 1
            | _ -> ()

        count

    /// <summary>The references released for collected objects so far.</summary>
    member _.Released = released

    member internal _.Claim(target: NativeObject) =
        match Graph.TryCurrent with
        | ValueSome graph ->
            let owner =
                try
                    getOwner ()
                with :? InvalidOperationException ->
                    Unchecked.defaultof<_>

            if
                not (isNull (box owner))
                && not (obj.ReferenceEquals (owner, graph.Root))
                && not owner.IsDisposed
                && not (obj.ReferenceEquals (target.ClaimedBy, owner))
            then
                target.ClaimedBy <- owner

                let set =
                    match claims.TryGetValue owner with
                    | true, set -> set
                    | _ ->
                        let set = HashSet<NativeObject>(HashIdentity.Reference)
                        claims.Add (owner, set)

                        owner.OnCleanup (fun () ->
                            claims.Remove owner |> ignore

                            for o in set do
                                if obj.ReferenceEquals (o.ClaimedBy, owner) then
                                    o.ClaimedBy <- Unchecked.defaultof<_>

                            set.Clear ())

                        set

                set.Add target |> ignore

                true
            else
                not (isNull (box owner))
                && obj.ReferenceEquals (target.ClaimedBy, owner)
        | ValueNone -> false

    /// <summary>Keeps an object with expandos alive: under the current owner, else until host disposal.</summary>
    member internal this.Pin(target: NativeObject) =
        if target.Kind = Kind.Node && not (this.Claim target) then
            held.Add target |> ignore

    member private this.Wrap(handle: nativeint, factory: Func<Session, nativeint, NativeObject>, kind: int) : NativeObject =
        if handle = 0n then
            null
        else
            let mutable existing = null

            match table.TryGetValue handle with
            | true, weak when
                weak.TryGetTarget (&existing)
                && existing.Handle = handle
                ->
                Session.Release (kind, handle)

                if kind = Kind.Node then
                    this.Claim existing |> ignore

                existing
            | _ ->
                let created = factory.Invoke (this, handle)
                table[handle] <- WeakReference<NativeObject>(created)

                if kind = Kind.Node then
                    this.Claim created |> ignore

                created

    /// <summary>The object of a node handle the caller owns a reference to, or null for zero.</summary>
    member this.Node(handle: nativeint) : NativeObject =
        this.Wrap (handle, this.NodeFactory, Kind.Node)

    /// <summary>The object of an event handle the caller owns a reference to, or null for zero.</summary>
    member this.Event(handle: nativeint) : NativeObject =
        this.Wrap (handle, this.EventFactory, Kind.Event)

    /// <summary>The object of a standalone EventTarget handle the caller owns a reference to, or null for zero.</summary>
    member internal this.Target(handle: nativeint) : NativeObject =
        this.Wrap (handle, this.TargetFactory, Kind.EventTarget)

    /// <summary>Enters an object created for an owned node or event handle in the table.</summary>
    member internal _.Register(created: NativeObject) =
        table[created.Handle] <- WeakReference<NativeObject>(created)

    /// <summary>The object of an event handle valid for the current listener call.</summary>
    member internal this.BorrowedEvent(handle: nativeint) : NativeObject =
        let mutable existing = null

        match table.TryGetValue handle with
        | true, weak when
            weak.TryGetTarget (&existing)
            && existing.Handle = handle
            ->
            existing
        | _ -> this.Event (Native.rv_event_retain handle)

    /// <summary>Records an object outside the node and event table for release at host disposal.</summary>
    member internal _.Track<'T when 'T :> NativeObject>(target: 'T) : 'T =
        others.Add (WeakReference<NativeObject>(target))
        target

    /// <summary>Makes <c>target</c> the wrapper <c>Object</c> returns for its kind and handle while no other is live.</summary>
    member internal _.Remember(target: NativeObject) =
        let key = struct (target.Kind, target.Handle)
        let mutable existing = null

        match objectTable.TryGetValue key with
        | true, weak when weak.TryGetTarget (&existing) && existing.Handle = target.Handle -> ()
        | _ -> objectTable[key] <- WeakReference<NativeObject>(target)

    /// <summary>Consumes an owned reference, reusing a live wrapper or creating one anchored to its owner.</summary>
    member internal this.Object(handle: nativeint, kind: int, owner: NativeObject, factory: Func<Session, nativeint, NativeObject>) =
        if handle = 0n then
            null
        elif disposed then
            Session.Release (kind, handle)
            raise (ObjectDisposedException (nameof Session))
        else
            let key = struct (kind, handle)
            let mutable existing = null

            match objectTable.TryGetValue key with
            | true, weak when
                weak.TryGetTarget (&existing)
                && existing.Handle = handle
                ->
                Session.Release (kind, handle)
                existing
            | _ ->
                let created =
                    try
                        factory.Invoke (this, handle)
                    with _ ->
                        Session.Release (kind, handle)
                        reraise ()

                created.KeepAlive owner
                objectTable[key] <- WeakReference<NativeObject>(created)
                this.Track created

    member internal _.QueueRelease(kind: int, handle: nativeint) =
        if not disposed then
            releases.Enqueue (struct (kind, handle))

    /// <summary>Releases the references of collected objects; runs on the host thread.</summary>
    member _.Drain() =
        let mutable item = Unchecked.defaultof<_>

        while releases.TryDequeue (&item) do
            let struct (kind, handle) = item

            if kind = Kind.Node || kind = Kind.Event then
                match table.TryGetValue handle with
                | true, weak ->
                    match weak.TryGetTarget () with
                    | true, _ -> ()
                    | _ -> table.Remove handle |> ignore
                | _ -> ()
            else
                let key = struct (kind, handle)

                match objectTable.TryGetValue key with
                | true, weak when not (fst (weak.TryGetTarget ())) -> objectTable.Remove key |> ignore
                | _ -> ()

            Session.Release (kind, handle)
            released <- released + 1

        if others.Count > 64 then
            others.RemoveAll (fun weak -> not (fst (weak.TryGetTarget ())))
            |> ignore

    /// <summary>Releases every reference, live objects included; their handles read zero afterwards.</summary>
    member this.Dispose() =
        if not disposed then
            let live =
                Seq.append table.Values others
                |> Seq.choose (fun weak ->
                    match weak.TryGetTarget () with
                    | true, target -> Some target
                    | _ -> None)
                |> Seq.toArray

            GC.Collect ()
            GC.WaitForPendingFinalizers ()
            this.Drain ()
            disposed <- true

            for target in live do
                target.Detach ()

            table.Clear ()
            objectTable.Clear ()
            others.Clear ()
            held.Clear ()
            GC.KeepAlive live

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>Status handling, strings and handles of the generated members.</summary>
module internal Interop =
    [<Sealed; AbstractClass>]
    type private Buffers =
        [<ThreadStatic; DefaultValue>]
        static val mutable private text: byte array

        [<ThreadStatic; DefaultValue>]
        static val mutable private length: int array

        static member Text
            with get () =
                if isNull Buffers.text then
                    Buffers.text <- Array.zeroCreate 512

                Buffers.text
            and set (value: byte array) = Buffers.text <- value

        static member Length =
            if isNull Buffers.length then
                Buffers.length <- Array.zeroCreate 1

            Buffers.length

    [<Literal>]
    let private BufferTooSmall = -6

    let private lastException (part: int) =
        let buffer = Array.zeroCreate<byte> 256
        let length = [| 0 |]
        let mutable status = Native.rv_last_exception (part, buffer, buffer.Length, length)

        if status = BufferTooSmall then
            let larger = Array.zeroCreate<byte> length[0]
            status <- Native.rv_last_exception (part, larger, larger.Length, length)

            if status = 0 && length[0] >= 0 then
                Text.Encoding.UTF8.GetString (larger, 0, length[0])
            else
                ""
        elif status = 0 && length[0] >= 0 then
            Text.Encoding.UTF8.GetString (buffer, 0, length[0])
        else
            ""

    /// <summary>The exception for a failed status of the operation <c>name</c>.</summary>
    let failure (name: string) (status: int) : exn =
        match status with
        | -3 -> WebCoreDomException (lastException 0, lastException 1)
        | -11 -> InvalidCastException $"{name}: the object does not implement the member's interface"
        | -9 -> NotSupportedException $"{name} is not supported by this WebCore build"
        | -2 -> InvalidOperationException $"{name} was called off the host thread"
        | -1 -> ArgumentNullException (name, $"{name} received a null argument")
        | other -> InvalidOperationException $"{name} returned status {other}"

    [<Sealed; AbstractClass>]
    type private Callbacks =
        [<ThreadStatic; DefaultValue>]
        static val mutable private pending: exn

        static member Pending
            with get () = Callbacks.pending
            and set (value: exn) = Callbacks.pending <- value

    /// <summary>
    /// Records the exception a host callback raised. The next failed call that reports
    /// <c>ExistingExceptionError</c> on the thread rethrows it with its original stack.
    /// </summary>
    let callbackFailed (error: exn) =
        Callbacks.Pending <- error

    /// <summary>Raises the status's exception unless it is <c>RV_OK</c>.</summary>
    let check (name: string) (status: int) =
        if status <> 0 then
            let pending = Callbacks.Pending
            Callbacks.Pending <- null

            if
                status = -3
                && not (isNull pending)
                && lastException 0 = "ExistingExceptionError"
            then
                Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(pending).Throw()

            raise (failure name status)

    /// <summary>The string a getter copies out, or null for a null string.</summary>
    let text (name: string) (call: byte array -> int -> int array -> int) : string =
        let length = Buffers.Length
        let mutable buffer = Buffers.Text
        let mutable status = call buffer buffer.Length length

        if status = BufferTooSmall then
            buffer <- Array.zeroCreate (max length[0] (buffer.Length * 2))
            Buffers.Text <- buffer
            status <- call buffer buffer.Length length

        check name status

        if length[0] < 0 then
            null
        else
            Text.Encoding.UTF8.GetString (buffer, 0, length[0])

    /// <summary>Raises the <c>NotSupportedException</c> of a member outside the native backend.</summary>
    let unsupported<'T> (name: string) : 'T =
        raise (NotSupportedException $"{name} is not available in the native backend")

    /// <summary>The handle of a backend object, or zero for null.</summary>
    let handle (value: obj) : nativeint =
        match value with
        | null -> 0n
        | :? NativeObject as target ->
            if target.Handle = 0n then
                raise (ObjectDisposedException (target.ToString (), "The object's host was disposed"))

            target.Handle
        | other -> raise (ArgumentException $"A {other.GetType().Name} is not an object of the native backend")

    /// <summary>Validates a non-null typed value from the same host session.</summary>
    let valueHandle (session: Session) (value: obj) =
        match value with
        | null -> nullArg (nameof value)
        | :? NativeObject as target when
            obj.ReferenceEquals (session, target.Session)
            && (target.Kind = Kind.StyleValue
                || target.Kind = Kind.VariableReferenceValue)
            ->
            handle value
        | _ -> invalidArg (nameof value) "The typed value must belong to the same native host"

    /// <summary>Validates a transform component or matrix argument against its receiver's native session.</summary>
    let transformHandle (session: Session) (kind: int) (value: obj) =
        if isNull value then
            nullArg (nameof value)

        match value with
        | :? NativeObject as target when
            obj.ReferenceEquals (session, target.Session)
            && target.Kind = kind
            ->
            handle value
        | _ -> invalidArg (nameof value) "The transform input must have the expected type in the same native host"

    /// <summary>Validates an SVG member input's release kind, native session and permitted null value.</summary>
    let svgHandle (session: Session) (kind: int) (allowNull: bool) (value: obj) =
        match value with
        | null when allowNull -> 0n
        | null -> nullArg (nameof value)
        | :? NativeObject as target when
            obj.ReferenceEquals (session, target.Session)
            && target.Kind = kind
            ->
            handle value
        | _ -> invalidArg (nameof value) "The SVG input must have the expected type in the same native host"

    /// <summary>Finds a retained acquisition document without substituting the host's current document.</summary>
    let contextDocument (target: NativeObject) =
        handle (box target) |> ignore
        let visited = HashSet<NativeObject>(HashIdentity.Reference)

        let rec find (value: NativeObject) =
            if not (visited.Add value) then
                None
            elif not (isNull value.ContextDocument) then
                Some value.ContextDocument
            else
                let direct =
                    match box value with
                    | :? Ranvier.Dom.Document -> Some value
                    | :? Ranvier.Dom.Node as node ->
                        let owner = node.ownerDocument

                        if isNull owner then
                            None
                        else
                            Some (box owner :?> NativeObject)
                    | :? Ranvier.Dom.StyleSheet as sheet ->
                        let ownerNode = sheet.ownerNode

                        if isNull ownerNode then
                            None
                        elif box ownerNode :? Ranvier.Dom.Document then
                            Some (box ownerNode :?> NativeObject)
                        else
                            let owner = ownerNode.ownerDocument

                            if isNull owner then
                                None
                            else
                                Some (box owner :?> NativeObject)
                    | _ -> None

                match direct with
                | Some document -> Some document
                | None -> value.LifetimeAnchors |> Seq.tryPick find

        match find target with
        | Some document when obj.ReferenceEquals (target.Session, document.Session) ->
            handle (box document) |> ignore
            document
        | Some _ -> invalidOp "The acquisition document belongs to another native session"
        | None -> invalidOp "The native object has no retained acquisition document context"

    /// <summary>The live document handle bound to a context-requiring object.</summary>
    let contextHandle target =
        handle (box (contextDocument target))

    /// <summary>Copies a length-delimited owned result before its container is released.</summary>
    let spanText (span: NativeDom.RvDomUtf8StringSpan) =
        if span.length < 0 then
            null
        elif span.data = 0n then
            invalidOp "WebCore returned a null UTF-8 span with a non-null length"
        else
            Runtime.InteropServices.Marshal.PtrToStringUTF8 (span.data, span.length)

    /// <summary>
    /// Runs <c>original</c> through the interceptions of <c>name</c> on <c>target</c>, the latest outermost, each given
    /// the inner call, the target and the arguments.
    /// </summary>
    let intercept (target: NativeObject) (name: string) (args: obj array) (original: obj array -> obj) : obj =
        let mutable call = original

        for struct (on, memberName, wrap) in target.Session.Interceptions do
            if
                memberName = name
                && (isNull on || obj.ReferenceEquals (on, target))
            then
                let inner = call
                call <- fun values -> wrap.Invoke (Func<_, _> inner, box target, values)

        call args

    /// <summary>The string, or empty for null: a DOMString result that WebCore reports as null.</summary>
    let nonNull (value: string) =
        if isNull value then "" else value

    let handleOr (value: 'T option) : nativeint =
        match value with
        | Some target -> handle (box target)
        | None -> 0n

    let optionOf<'T> (value: NativeObject) : 'T option =
        if isNull value then None else Some (unbox<'T> value)
