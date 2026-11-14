namespace Ranvier.View.Dsl

open System
open global.Ranvier
open Ranvier.View.Representation
open Ranvier.View.Template

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
module DslProps =
#if !FABLE_COMPILER
    [<Sealed; AbstractClass>]
    type Instance<'t when 't: (new: unit -> 't) and 't: not struct> private () =
        [<DefaultValue>]
        static val mutable private value: 't

        static member Value =
            if isNull (box Instance<'t>.value) then
                lock typeof<Instance<'t>> (fun () ->
                    if isNull (box Instance<'t>.value) then
                        Instance<'t>.value <- new 't ())

            Instance<'t>.value
#endif

    let inline ofType<'t when 't: (new: unit -> 't) and 't: not struct> () : 't =
#if FABLE_COMPILER
        Unchecked.defaultof<'t>
#else
        Instance<'t>.Value
#endif

#if FABLE_COMPILER
    [<Fable.Core.Emit("$0[$1] = $2")>]
    let setExpando (target: obj) (name: string) (value: 'T) : unit =
        Fable.Core.Util.jsNative
#endif

/// <summary>Core contract types and helpers re-exported for single-open DSL usage (<c>open Ranvier.View.Dsl</c>).</summary>
[<AutoOpen>]
module Contracts =
    type Setup = Ranvier.View.Representation.Setup
    type TemplateComponent = Ranvier.View.Template.TemplateComponent
    type Tag = Ranvier.View.Representation.Tag
    type Operations<'view> = Ranvier.View.Representation.Operations<'view>
    type Component<'props> = Ranvier.View.Template.Component<'props>
    type TemplateView<'inst> = Ranvier.View.Template.TemplateView<'inst>
    type Part<'native, 'inst> = Ranvier.View.Template.Part<'native, 'inst>
    type Source<'inst, 'value> = Ranvier.View.Template.Source<'inst, 'value>
    type Arm = Ranvier.View.Template.Arm
    type PropertySerialization = Ranvier.View.Template.PropertySerialization
    type UnsafeHtml = Ranvier.View.Template.UnsafeHtml
    type BoundaryStatus = Ranvier.View.Representation.BoundaryStatus
    type BoundaryFailure = Ranvier.View.Representation.BoundaryFailure
    type BrowserPolicy = Ranvier.View.Representation.BrowserPolicy
    type NodeRef<'native> = Ranvier.View.Representation.NodeRef<'native>
    type MotionContext = Ranvier.View.Representation.MotionContext
    type Reflow = Ranvier.View.Representation.Reflow

    module NodeRef =
        let create () : NodeRef<'native> =
            Ranvier.View.Representation.NodeRef.create ()

    module PropertyHtml =
        let text = Ranvier.View.Template.PropertyHtml.text
        let number = Ranvier.View.Template.PropertyHtml.number
        let presence = Ranvier.View.Template.PropertyHtml.presence
        let optional = Ranvier.View.Template.PropertyHtml.optional
        let childText = Ranvier.View.Template.PropertyHtml.childText
        let browserOnly = Ranvier.View.Template.PropertyHtml.browserOnly

[<AutoOpen>]
module Builders =
    [<Sealed>]
    type private EmptyArm private () =
        [<DefaultValue>]
        static val mutable private shared: Component<unit -> unit>

        static member Get() =
            // Reads shared without the lock; null re-checks under the lock.
            if isNull (box EmptyArm.shared) then
#if FABLE_COMPILER
                EmptyArm.shared <- DslParts.defineView (fun _ (_: unit -> unit) -> ()) (DslParts.fragment [||])
#else
                lock typeof<EmptyArm> (fun () ->
                    if isNull (box EmptyArm.shared) then
                        EmptyArm.shared <- DslParts.defineView (fun _ (_: unit -> unit) -> ()) (DslParts.fragment [||]))
#endif
            EmptyArm.shared

    /// <summary>The zero-root arm selected by <c>show</c> while its condition is false, created on first use.</summary>
    let internal emptyArm () : Component<unit -> unit> =
        EmptyArm.Get ()

    /// <summary>A void element, accepted as a child by <c>Element</c>.</summary>
    [<AbstractClass>]
    type VoidElement<'inst> internal (view: TemplateView<'inst>) =
        /// <summary>The element's view, for a position that takes a <c>TemplateView</c>.</summary>
        member _.View = view

    /// <summary>A component used as a tag: yields the child view of <c>definition</c>.</summary>
    /// <remarks>Subclasses take a private primary constructor and public overloads.</remarks>
    [<AbstractClass>]
    type ComponentTag<'props, 'inst>(definition: Component<'props>, props: 'inst -> 'props) =
        inherit VoidElement<'inst>(DslParts.renderChild definition props)

    /// <summary>Static base scope providing common attributes, events, hooks, raw HTML, and composition helpers for elements.</summary>
    [<AbstractClass>]
    type TagBase<'native, 'inst when 'native :> Dom.Element> internal () =
        /// <summary>A part that does nothing; useful in conditional expressions inside element constructors.</summary>
        static member inline none: Part<'native, 'inst> = TemplateParts.none

        /// <summary>Flattens an array of parts into an element or document entry without heap allocation when unneeded.</summary>
        static member inline batch(parts: Part<'native, 'inst> array) : Part<'native, 'inst> =
            TemplateParts.batch parts

        /// <summary>Runs a dynamic part in its own isolated reactive effect instead of the instance render effect.</summary>
        static member inline isolated(part: Part<'native, 'inst>) : Part<'native, 'inst> =
            TemplateParts.isolatedPart part

        /// <summary>Marks a class attribute or reflected class property for ordered merging with the element's other class sources.</summary>
        static member inline composableClass(part: Part<'native, 'inst>) : Part<'native, 'inst> =
            TemplateParts.composableClass part

        static member inline attribute(name: string, value: string) : Part<'native, 'inst> =
            TemplateParts.attribute name value

        static member inline attribute(name: string, value: string option) : Part<'native, 'inst> =
            TemplateParts.optionalAttribute name value

        static member inline attribute(name: string, read: 'inst -> string option) : Part<'native, 'inst> =
            TemplateParts.readAttribute name read

        static member inline attribute(name: string, source: Source<'inst, string option>) : Part<'native, 'inst> =
            TemplateParts.sourceAttribute name source

        static member inline data(suffix: string, value: string) : Part<'native, 'inst> =
            TemplateParts.attribute (Ranvier.View.Typed.prefixedAttributeName "data-" suffix) value

        static member inline data(suffix: string, value: string option) : Part<'native, 'inst> =
            TemplateParts.optionalAttribute (Ranvier.View.Typed.prefixedAttributeName "data-" suffix) value

        static member inline data(suffix: string, read: 'inst -> string option) : Part<'native, 'inst> =
            TemplateParts.readAttribute (Ranvier.View.Typed.prefixedAttributeName "data-" suffix) read

        static member inline data(suffix: string, source: Source<'inst, string option>) : Part<'native, 'inst> =
            TemplateParts.sourceAttribute (Ranvier.View.Typed.prefixedAttributeName "data-" suffix) source

        static member inline aria(suffix: string, value: string) : Part<'native, 'inst> =
            TemplateParts.attribute (Ranvier.View.Typed.prefixedAttributeName "aria-" suffix) value

        static member inline aria(suffix: string, value: string option) : Part<'native, 'inst> =
            TemplateParts.optionalAttribute (Ranvier.View.Typed.prefixedAttributeName "aria-" suffix) value

        static member inline aria(suffix: string, read: 'inst -> string option) : Part<'native, 'inst> =
            TemplateParts.readAttribute (Ranvier.View.Typed.prefixedAttributeName "aria-" suffix) read

        static member inline aria(suffix: string, source: Source<'inst, string option>) : Part<'native, 'inst> =
            TemplateParts.sourceAttribute (Ranvier.View.Typed.prefixedAttributeName "aria-" suffix) source

        static member inline style(value: string) : Part<'native, 'inst> =
            TemplateParts.attribute "style" value

        static member inline style(value: string option) : Part<'native, 'inst> =
            TemplateParts.optionalAttribute "style" value

        static member inline style(read: 'inst -> string) : Part<'native, 'inst> =
            TemplateParts.readAttribute "style" (fun instance -> TemplateParts.present (read instance))

        static member inline style(read: 'inst -> string option) : Part<'native, 'inst> =
            TemplateParts.readAttribute "style" read

        static member inline style(source: Source<'inst, string>) : Part<'native, 'inst> =
            TemplateParts.sourceAttribute "style" (TemplateParts.mapSource TemplateParts.present source)

        static member inline style(source: Source<'inst, string option>) : Part<'native, 'inst> =
            TemplateParts.sourceAttribute "style" source

        /// <summary>
        /// Handles <c>eventName</c>. The handler runs unbatched: a handler that groups several writes wraps them in
        /// <c>Api.batch</c>, and effects then run once, after the outermost batch.
        /// </summary>
        static member inline on(eventName: string, handler: 'inst -> Dom.Event -> unit) : Part<'native, 'inst> =
            TemplateParts.event eventName handler

        /// <summary>Runs once the element is placed; the returned cleanup runs when the instance is disposed, before its nodes leave.</summary>
        static member inline mounted(hook: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
            TemplateParts.mounted hook

        /// <summary>
        /// Sets the <c>NodeRef</c> selected from the instance to the element while the element is placed, and to <c>None</c>
        /// once the instance is disposed.
        /// </summary>
        static member inline ref(select: 'inst -> NodeRef<'native>) : Part<'native, 'inst> =
            TemplateParts.refNode select

        /// <summary>
        /// The widget owns the element's content once placed, and the template declares the element empty.
        /// The cleanup destroys the widget.
        /// </summary>
        static member inline widget(attach: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
            TemplateParts.widget attach

        /// <summary>
        /// The integration borrows the element once placed; the cleanup removes what it added.
        /// The integration keeps the element under its parent; moving it is reported after the cleanup.
        /// </summary>
        static member inline lend(attach: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
            TemplateParts.lend attach

        /// <summary>
        /// Runs once the element is placed, with whether the element was adopted from server HTML and whether its row or arm
        /// was created by a region pass after the region's first. The returned cleanup runs when the instance is disposed.
        /// The server omits the hook.
        /// </summary>
        static member inline motion(hook: Func<'inst, 'native, MotionContext, unit -> unit>) : Part<'native, 'inst> =
            TemplateParts.motion hook

        /// <summary>
        /// On a row or arm root element: when the row is retired, its node stays in place with its content and <c>exit</c>
        /// runs with <c>finish</c>; the node leaves when <c>finish</c> is called. The returned function cancels the exit when
        /// the removal is forced. An exit applies once the element is placed. The server omits the hook.
        /// </summary>
        static member inline exit(exit: Func<'inst, 'native, unit -> unit, unit -> unit>) : Part<'native, 'inst> =
            TemplateParts.exit exit

        /// <summary>
        /// On a region's parent element: <c>Before</c> runs before the first region pass under the element in a flush,
        /// <c>After</c> once the pass's nodes and commits are in place. The server omits the hook.
        /// </summary>
        static member inline reflow(measure: Func<'inst, 'native, Reflow>) : Part<'native, 'inst> =
            TemplateParts.reflow measure

        /// <summary>
        /// <c>first</c> and <c>second</c> on one element as one part: the hook of <c>second</c> runs after the hook of
        /// <c>first</c>, and the cleanup of <c>second</c> before the cleanup of <c>first</c>. Both are hook parts, at most one
        /// of them other than a motion hook, and the part has that hook's kind; other parts raise. When the hook of <c>second</c>
        /// raises, the cleanup of <c>first</c> runs.
        /// </summary>
        static member inline both(first: Part<'native, 'inst>, second: Part<'native, 'inst>) : Part<'native, 'inst> =
            TemplateParts.both first second

        /// <summary>
        /// The element's content, parsed as HTML without escaping. The template declares the element empty. Scripts,
        /// handlers and URLs in the markup run as <c>UnsafeHtml</c> states for the client and for server HTML.
        /// </summary>
        static member inline unsafeHtml(value: UnsafeHtml) : Part<'native, 'inst> =
            TemplateParts.unsafeHtml value

        /// <summary>
        /// The element's content, parsed as HTML without escaping and parsed again when the reader returns different
        /// markup. Scripts, handlers and URLs in the markup run as <c>UnsafeHtml</c> states for the client and for server HTML.
        /// </summary>
        static member inline unsafeHtml(read: 'inst -> UnsafeHtml) : Part<'native, 'inst> =
            TemplateParts.readUnsafeHtml read

        /// <summary>
        /// The element's content, parsed as HTML without escaping and updated from a direct source.
        /// </summary>
        static member inline unsafeHtml(source: Source<'inst, UnsafeHtml>) : Part<'native, 'inst> =
            TemplateParts.sourceUnsafeHtml source

    /// <summary>Helpers for composing element parts.</summary>
    module Part =
        let inline none<'native, 'inst> : Part<'native, 'inst> = TemplateParts.none

        let inline batch (parts: Part<'native, 'inst> array) : Part<'native, 'inst> =
            TemplateParts.batch parts

        let inline isolated (part: Part<'native, 'inst>) : Part<'native, 'inst> =
            TemplateParts.isolatedPart part

        let inline composableClass (part: Part<'native, 'inst>) : Part<'native, 'inst> =
            TemplateParts.composableClass part

    /// <summary>Entries for the document's head: shown once the instance is placed, withdrawn when it is disposed.</summary>
    [<AbstractClass; Sealed>]
    type Document =
        static member title(value: string) : TemplateView<'inst> =
            DslParts.documentTitle value

        static member title(read: 'inst -> string) : TemplateView<'inst> =
            DslParts.readDocumentTitle read

        static member title(source: Source<'inst, string>) : TemplateView<'inst> =
            DslParts.sourceDocumentTitle source

        /// <summary>A meta element, keyed by its static name, property, http-equiv or charset attribute.</summary>
        static member meta([<ParamArray>] parts: Part<Dom.HTMLMetaElement, 'inst> array) : TemplateView<'inst> =
            DslParts.documentMeta parts

        /// <summary>A link element, keyed by its <c>rel</c> when that is <c>canonical</c> and unkeyed otherwise.</summary>
        static member link([<ParamArray>] parts: Part<Dom.HTMLLinkElement, 'inst> array) : TemplateView<'inst> =
            DslParts.documentLink parts

        /// <summary>A link element, keyed by its <c>rel</c> when that is <c>canonical</c> and by <c>key</c> otherwise.</summary>
        static member link(key: string, [<ParamArray>] parts: Part<Dom.HTMLLinkElement, 'inst> array) : TemplateView<'inst> =
            DslParts.documentKeyedLink key parts

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    let sourceOf<'inst, 'value> (read: Func<obj, obj>) : Source<'inst, 'value> =
        Source read

    /// <summary>A source over the signal or memo returned by <c>accessor</c>.</summary>
    let inline from<'inst, ^s, 'value when ^s: (member Value: 'value)> ([<InlineIfLambda>] accessor: 'inst -> ^s) : Source<'inst, 'value> =
        sourceOf (Func<obj, obj>(fun instance -> box ((^s: (member Value: 'value) (accessor (unbox<'inst> instance))))))

    /// <summary>A text slot; the same node as a yielded reader.</summary>
    let text (read: 'inst -> string) : TemplateView<'inst> =
        TemplateParts.readText read

    /// <summary>Runs a text slot in its own isolated reactive effect instead of the instance render effect.</summary>
    let isolated (view: TemplateView<'inst>) : TemplateView<'inst> =
        TemplateParts.isolatedView view

    /// <summary>The children as one view, with no element of their own.</summary>
    let fragment (children: TemplateView<'inst> array) : TemplateView<'inst> =
        DslParts.fragment children

    /// <summary>A view hole: renders the view read from the instance as it is created, between region markers.</summary>
    let view (read: 'inst -> Program.View) : TemplateView<'inst> =
        DslParts.viewHole read

    /// <summary>An arm definition whose instance is its props.</summary>
    let armOf (view: TemplateView<unit -> 'payload>) : Component<unit -> 'payload> =
        DslParts.defineView (fun _ payload -> payload) view

    /// <summary>An arm that renders <c>definition</c>; its props read the payload of the arm currently selected.</summary>
    let arm (definition: Component<unit -> 'payload>) (payload: 'payload) : Arm =
        TemplateParts.arm definition payload

    /// <summary>Renders the arm that <c>arms</c> selects for the instance's union value, between region markers.</summary>
    let matchWith (read: 'inst -> 'union) (arms: 'union -> Arm) : TemplateView<'inst> =
        DslParts.matchWith read arms

    /// <summary>
    /// Renders the arm selected for the instance's key, between region markers;
    /// a key change under <c>=</c> or a change of the selected definition remounts the arm.
    /// </summary>
    let branch<'inst, 'key when 'key: equality> (key: 'inst -> 'key) (arms: 'key -> Arm) : TemplateView<'inst> =
        DslParts.branch key arms

    /// <summary>
    /// Renders one <c>row</c> instance per item, between region markers;
    /// items with equal keys under <c>=</c> keep their row, whose props read the current item.
    /// </summary>
    let each<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: Component<unit -> 'item>)
        : TemplateView<'inst> =
        DslParts.each key items row

    /// <summary>One row per item; a row reads its current item, and items with equal keys keep their row.</summary>
    let forEach<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: TemplateView<unit -> 'item>)
        : TemplateView<'inst> =
        each key items (DslParts.defineView (fun _ (current: unit -> 'item) -> current) row)

    /// <summary>One row per item; a row's instance is the item it was created for, and items with equal keys keep their row.</summary>
    let forEachFixed<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: TemplateView<'item>)
        : TemplateView<'inst> =
        each key items (DslParts.defineView (fun _ (current: unit -> 'item) -> current ()) row)

    /// <summary>Renders <c>content</c> while <c>condition</c> holds; a change to false runs its cleanups and removes its nodes.</summary>
    let show (condition: 'inst -> bool) (content: TemplateView<'inst>) : TemplateView<'inst> =
        let shown =
            DslParts.defineView (fun _ (current: unit -> 'inst) -> current ()) content

        let hidden = emptyArm ()

        matchWith (fun instance -> if condition instance then ValueSome instance else ValueNone) (function
            | ValueSome instance -> arm shown instance
            | ValueNone -> arm hidden ())

    /// <summary>Renders <c>content</c> while <c>condition</c> holds and <c>fallback</c> otherwise; each change of the condition remounts.</summary>
    let showElse (condition: 'inst -> bool) (content: TemplateView<'inst>) (fallback: TemplateView<'inst>) : TemplateView<'inst> =
        let shown =
            DslParts.defineView (fun _ (current: unit -> 'inst) -> current ()) content

        let other =
            DslParts.defineView (fun _ (current: unit -> 'inst) -> current ()) fallback

        matchWith
            (fun instance ->
                if condition instance then
                    Choice1Of2 instance
                else
                    Choice2Of2 instance)
            (function
             | Choice1Of2 instance -> arm shown instance
             | Choice2Of2 instance -> arm other instance)

    /// <summary>
    /// A boundary between region markers: <c>content</c> once every binding under it has read a value,
    /// <c>pending</c> until then, and <c>failed</c> after a failure under the content.
    /// </summary>
    let boundary (pending: Component<unit>) (failed: Component<BoundaryFailure>) (content: Component<BoundaryStatus>) : TemplateView<'inst> =
        DslParts.boundary pending failed content

    /// <summary>Inlines a child template; its props are read from the instance, untracked, once per instance.</summary>
    let render (definition: Component<'props>) (props: 'inst -> 'props) : TemplateView<'inst> =
        DslParts.renderChild definition props

    /// <summary>Inlines a child template with fixed props.</summary>
    let renderOf (definition: Component<'props>) (props: 'props) : TemplateView<'inst> =
        DslParts.renderChild definition (fun _ -> props)

    module Component =
        /// <summary>Defines a component from per-instance setup and a template shared by every instance.</summary>
        let template (setup: Setup -> 'props -> 'inst) (view: TemplateView<'inst>) : Component<'props> =
            DslParts.defineView setup view

        /// <summary>A component from the type's static <c>Create</c> (setup) and <c>View</c> (template, read once).</summary>
        let inline ofMembers< ^T, 'props when ^T: (static member Create: Setup * 'props -> ^T) and ^T: (static member View: TemplateView< ^T >)>
            ()
            : Component<'props> =
            DslParts.defineView
                (fun setup props -> (^T: (static member Create: Setup * 'props -> ^T) (setup, props)))
                ((^T: (static member View: TemplateView< ^T >) ()))

        /// <summary>A component from a constructor (setup) and the type's static <c>View</c> (template, read once).</summary>
        let inline ofClass ([<InlineIfLambda>] create: Setup * 'props -> ^T) : Component<'props> =
            DslParts.defineView (fun setup props -> create (setup, props)) ((^T: (static member View: TemplateView< ^T >) ()))

        /// <summary>Renders an instance as a Program view; the DOM backend materialises the compiled template.</summary>
        let render (definition: Component<'props>) (props: 'props) : Program.View =
            Ranvier.View.Template.Component.render definition props

        /// <summary>Renders an instance in any representation through the default expansion.</summary>
        let renderWith (ops: Operations<'view>) (definition: Component<'props>) (props: 'props) : 'view =
            Ranvier.View.Template.Component.renderWith ops definition props
