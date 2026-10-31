namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>
/// Host NodeFilters over managed <c>NodeFilter</c> functions, for <c>createNodeIterator</c> and <c>createTreeWalker</c>.
/// </summary>
/// <remarks>
/// A filter's function stays reachable while the engine holds the filter: the iterator or walker created with it,
/// and filter getter results. It is released after the last of them is released. An exception the function raises
/// stops the traversal and is rethrown, with its original stack, by the traversal call.
/// </remarks>
[<Sealed; AbstractClass>]
type internal Traversal private () =
    static let mutable live = 0

    static member private Invoke(context: nativeint, node: nativeint) : int =
        try
            let session, filter = GCHandle.FromIntPtr(context).Target :?> (Session * NodeFilter)

            let target = unbox<Node>(session.Node (Native.rv_node_retain node))

            match filter target with
            | NodeFilterResult.Accept
            | NodeFilterResult.Reject
            | NodeFilterResult.Skip as result -> int result
            | other -> raise (ArgumentOutOfRangeException ("filter", other, "A NodeFilter returns Accept, Reject or Skip"))
        with error ->
            Interop.callbackFailed error
            0

    static member private Free(context: nativeint) =
        GCHandle.FromIntPtr(context).Free()
        live <- live - 1

    static member val private Callback = Native.NodeFilterCallback (fun context node -> Traversal.Invoke (context, node))

    static member val private Release = Native.NodeFilterRelease (fun context -> Traversal.Free context)

    static member val private CallbackPointer = Marshal.GetFunctionPointerForDelegate<Native.NodeFilterCallback> Traversal.Callback

    static member val private ReleasePointer = Marshal.GetFunctionPointerForDelegate<Native.NodeFilterRelease> Traversal.Release

    /// <summary>The managed filter functions the engine still holds, on every host.</summary>
    static member Live = live

    /// <summary>
    /// Creates a traversal object of <c>document</c>. <c>create</c> makes the native call with the document, root,
    /// whatToShow and filter handles; the host filter reference it receives is dropped after the call.
    /// </summary>
    static member Create
        (
            document: NativeObject,
            root: Node,
            whatToShow: uint32 option,
            filter: NodeFilter option,
            create: nativeint -> nativeint -> uint32 -> nativeint -> nativeint,
            wrap: Session -> nativeint -> NativeObject
        ) : NativeObject =
        let target = Interop.handle (box document)
        let rootHandle = Interop.handle (box root)

        let filterHandle =
            match filter with
            | Some filter when not (isNull (box filter)) ->
                let context = GCHandle.Alloc (box (document.Session, filter))

                let handle =
                    Native.rv_node_filter_create (target, Traversal.CallbackPointer, GCHandle.ToIntPtr context, Traversal.ReleasePointer)

                if handle = 0n then
                    context.Free ()
                    raise (InvalidOperationException "rv_node_filter_create returned NULL")

                live <- live + 1
                handle
            | _ -> 0n

        try
            let result =
                create target rootHandle (defaultArg whatToShow 0xFFFFFFFFu) filterHandle

            document.Session.Track (wrap document.Session result)
        finally
            if filterHandle <> 0n then
                NativeDom.rv_dom_node_filter_release filterHandle

    /// <summary>
    /// The managed function of the filter handle <c>get</c> returns, or None for no filter. The handle is released.
    /// </summary>
    static member FilterOf(get: unit -> nativeint) : NodeFilter option =
        let handle = get ()

        if handle = 0n then
            None
        else
            try
                let mutable context = 0n

                Native.rv_node_filter_context (handle, &context)
                |> Interop.check "NodeFilter"

                if context = 0n then
                    raise (InvalidOperationException "The NodeFilter was not created by this host")

                let _, filter = GCHandle.FromIntPtr(context).Target :?> (Session * NodeFilter)

                Some filter
            finally
                NativeDom.rv_dom_node_filter_release handle
