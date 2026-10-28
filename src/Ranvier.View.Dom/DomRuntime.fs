module Ranvier.View.Dom.DomRuntime

open Fable.Core
open global.Ranvier.Dom

/// <summary>Returns the document installed on the current thread, or the browser document under Fable.</summary>
/// <exception cref="T:System.InvalidOperationException">A .NET DOM host is not installed on the current thread.</exception>
#if FABLE_COMPILER
[<Emit("document")>]
let document () : Document = jsNative
#else
let document () : Document =
    Ranvier.View.Native.document ()
#endif

/// <summary>Queues <c>callback</c> on the current DOM host's microtask queue.</summary>
/// <exception cref="T:System.InvalidOperationException">A .NET DOM host is not installed on the current thread.</exception>
#if FABLE_COMPILER
[<Emit("queueMicrotask($0)")>]
let queueMicrotask (callback: unit -> unit) : unit = jsNative
#else
let queueMicrotask (callback: unit -> unit) : unit =
    Ranvier.View.Native.queueMicrotask callback
#endif
