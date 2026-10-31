namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Native event listeners, dispatch and <c>currentTarget</c>.</summary>
[<Sealed; AbstractClass>]
type internal Events private () =
    static member private Run(context: nativeint, event: nativeint) : int =
        let target = GCHandle.FromIntPtr(context).Target :?> (Session * Registration)
        let session, registration = target
        session.ScriptDepth <- session.ScriptDepth + 1

        try
            try
                registration.Invoke (session.BorrowedEvent event)
            with error ->
                session.ListenerErrors.Add error
        finally
            session.ScriptDepth <- session.ScriptDepth - 1

            if
                session.ScriptDepth = 0
                && not (isNull session.AfterListener)
            then
                session.AfterListener.Invoke ()

        0

    static member val private Callback = Native.EventCallback (fun context event -> Events.Run (context, event))

    static member val private Pointer = Marshal.GetFunctionPointerForDelegate<Native.EventCallback> Events.Callback

    /// <summary>Registers <c>invoke</c> natively on the node; <c>callback</c> identifies the registration for removal.</summary>
    static member internal Listen
        (target: NativeObject, eventType: string, capture: bool, callback: obj, invoke: obj -> unit, ?passive: bool)
        : Registration =
        let session = target.Session

        let registration =
            Registration (Interop.handle (box target), eventType, capture, callback, invoke)

        let context = GCHandle.Alloc (box (session, registration))

        let flags =
            (if capture then 1 else 0)
            ||| (match passive with
                 | None -> 0
                 | Some false -> 4
                 | Some true -> 6)

        let listener =
            Native.rv_node_add_event_listener (registration.Target, eventType, flags, Events.Pointer, GCHandle.ToIntPtr context)

        if listener = 0n then
            context.Free ()
            raise (InvalidOperationException $"rv_node_add_event_listener returned NULL for '{eventType}'")

        registration.Listener <- listener
        registration.Context <- context
        session.Registrations.Add registration |> ignore
        registration

    /// <summary>Removes a registration; a second removal does nothing.</summary>
    static member internal Unlisten(session: Session, registration: Registration) =
        if session.Registrations.Remove registration then
            Native.rv_listener_remove registration.Listener
            |> Interop.check "EventTarget.removeEventListener"

            registration.Context.Free ()

    /// <summary>Removes every registration of the session.</summary>
    static member internal RemoveAll(session: Session) =
        for registration in Seq.toArray session.Registrations do
            Events.Unlisten (session, registration)

    static member private Find(target: NativeObject, eventType: string, capture: bool, callback: obj) =
        let handle = target.Handle

        target.Session.Registrations
        |> Seq.tryFind (fun r ->
            r.Target = handle
            && r.Type = eventType
            && r.Capture = capture
            && obj.ReferenceEquals (r.Callback, callback))

    static member private AddCore
        (target: NativeObject, eventType: string, identity: obj, invoke: obj -> unit, capture: bool, once: bool, passive: bool option)
        : Registration option =
        if
            not (isNull identity)
            && (Events.Find (target, eventType, capture, identity)).IsNone
        then
            if once then
                let registration: Registration ref = ref null

                registration.Value <-
                    Events.Listen (
                        target,
                        eventType,
                        capture,
                        identity,
                        (fun e ->
                            Events.Unlisten (target.Session, registration.Value)
                            invoke e),
                        ?passive = passive
                    )

                Some registration.Value
            else
                Some (Events.Listen (target, eventType, capture, identity, invoke, ?passive = passive))
        else
            None

    static member private Options(options: AddEventListenerOptions) =
        if obj.ReferenceEquals (options, null) then
            false, false, None
        else
            options.capture, options.once, options.passive

    /// <summary>Registers a function listener and returns its registration, or None for a duplicate.</summary>
    static member internal AddRegistered(target: NativeObject, eventType: string, listener: EventListener, options: AddEventListenerOptions) =
        let capture, once, passive = Events.Options options
        Events.AddCore (target, eventType, box listener, (fun event -> listener (unbox<Event> event)), capture, once, passive)

    /// <summary>Registers a function listener, ignoring repeated callback/type/capture registrations.</summary>
    static member Add(target: NativeObject, eventType: string, listener: EventListener, capture: bool option) =
        Events.AddCore (target, eventType, box listener, (fun event -> listener (unbox<Event> event)), defaultArg capture false, false, None)
        |> ignore

    /// <summary>Registers an object listener, preserving the object's removal identity.</summary>
    static member Add(target: NativeObject, eventType: string, listener: EventListenerObject, capture: bool option) =
        Events.AddCore (
            target,
            eventType,
            box listener,
            (fun event -> listener.handleEvent (unbox<Event> event)),
            defaultArg capture false,
            false,
            None
        )
        |> ignore

    /// <summary>Registers a function listener with capture, once and optional passive behavior.</summary>
    static member Add(target: NativeObject, eventType: string, listener: EventListener, options: AddEventListenerOptions) =
        let capture, once, passive = Events.Options options

        Events.AddCore (target, eventType, box listener, (fun event -> listener (unbox<Event> event)), capture, once, passive)
        |> ignore

    /// <summary>Registers an object listener with capture, once and optional passive behavior.</summary>
    static member Add(target: NativeObject, eventType: string, listener: EventListenerObject, options: AddEventListenerOptions) =
        let capture, once, passive = Events.Options options

        Events.AddCore (target, eventType, box listener, (fun event -> listener.handleEvent (unbox<Event> event)), capture, once, passive)
        |> ignore

    static member internal RemoveCore(target: NativeObject, eventType: string, identity: obj, capture: bool) =
        if not (isNull identity) then
            match Events.Find (target, eventType, capture, identity) with
            | Some registration -> Events.Unlisten (target.Session, registration)
            | None -> ()

    /// <summary>Removes the matching function listener in the selected phase.</summary>
    static member Remove(target: NativeObject, eventType: string, listener: EventListener, capture: bool option) =
        Events.RemoveCore (target, eventType, box listener, defaultArg capture false)

    /// <summary>Removes the matching object listener in the selected phase.</summary>
    static member Remove(target: NativeObject, eventType: string, listener: EventListenerObject, capture: bool option) =
        Events.RemoveCore (target, eventType, box listener, defaultArg capture false)

    /// <summary>Removes the matching function listener using only the capture option.</summary>
    static member Remove(target: NativeObject, eventType: string, listener: EventListener, options: EventListenerOptions) =
        Events.RemoveCore (
            target,
            eventType,
            box listener,
            not (obj.ReferenceEquals (options, null))
            && options.capture
        )

    /// <summary>Removes the matching object listener using only the capture option.</summary>
    static member Remove(target: NativeObject, eventType: string, listener: EventListenerObject, options: EventListenerOptions) =
        Events.RemoveCore (
            target,
            eventType,
            box listener,
            not (obj.ReferenceEquals (options, null))
            && options.capture
        )

    /// <summary>
    /// <c>EventTarget.dispatchEvent</c>: dispatches a native event synchronously and returns false when a listener
    /// cancelled it.
    /// </summary>
    static member Dispatch(target: NativeObject, event: Event) : bool =
        match box event with
        | :? NativeObject as native ->
            let session = target.Session
            let mutable prevented = 0
            session.ScriptDepth <- session.ScriptDepth + 1

            try
                Native.rv_node_dispatch (Interop.handle (box target), native.Handle, &prevented)
                |> Interop.check "EventTarget.dispatchEvent"
            finally
                session.ScriptDepth <- session.ScriptDepth - 1

            prevented = 0
        | _ -> raise (ArgumentException "dispatchEvent takes an event of the native backend")

    /// <summary>An init field of a host-created event, else the native value.</summary>
    static member Init<'T>(event: NativeObject, name: string, read: unit -> 'T) : 'T =
        match event.Init with
        | null -> read ()
        | init ->
            match init.TryGetValue name with
            | true, value -> unbox<'T> value
            | _ -> read ()

    /// <summary><c>Event.currentTarget</c>, or the override the view's delegation set for the dispatch.</summary>
    static member CurrentTarget(event: NativeObject) : EventTarget =
        if isNull (box event.CurrentTargetOverride) then
            unbox<EventTarget>(event.Session.Node (Native.rv_event_target (event.Handle, 1)))
        else
            match event.CurrentTargetOverride () with
            | null -> Unchecked.defaultof<EventTarget>
            | target -> unbox<EventTarget> target
