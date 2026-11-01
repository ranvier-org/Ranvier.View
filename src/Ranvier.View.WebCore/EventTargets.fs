namespace Ranvier.View.WebCore

open Ranvier.Dom

/// <summary>A standalone native EventTarget that is not a node.</summary>
type internal StandaloneEventTarget(session: Session, handle: nativeint) =
    inherit NativeObject(session, handle, Kind.EventTarget)

    member private this.Live() =
        if this.Handle = 0n then
            raise (System.ObjectDisposedException "EventTarget")

    interface EventTarget with
        member this.addEventListener(eventType: string, listener: (Event -> unit), ?capture: bool) =
            this.Live ()
            Events.Add (this, eventType, listener, capture)

        member this.addEventListener(eventType: string, listener: EventListenerObject, ?capture: bool) =
            this.Live ()
            Events.Add (this, eventType, listener, capture)

        member this.addEventListener(eventType: string, listener: (Event -> unit), options: AddEventListenerOptions) =
            this.Live ()
            Events.Add (this, eventType, listener, options)

        member this.addEventListener(eventType: string, listener: EventListenerObject, options: AddEventListenerOptions) =
            this.Live ()
            Events.Add (this, eventType, listener, options)

        member this.removeEventListener(eventType: string, listener: (Event -> unit), ?capture: bool) =
            this.Live ()
            Events.Remove (this, eventType, listener, capture)

        member this.removeEventListener(eventType: string, listener: EventListenerObject, ?capture: bool) =
            this.Live ()
            Events.Remove (this, eventType, listener, capture)

        member this.removeEventListener(eventType: string, listener: (Event -> unit), options: EventListenerOptions) =
            this.Live ()
            Events.Remove (this, eventType, listener, options)

        member this.removeEventListener(eventType: string, listener: EventListenerObject, options: EventListenerOptions) =
            this.Live ()
            Events.Remove (this, eventType, listener, options)

        member this.dispatchEvent(event: Event) =
            this.Live ()
            Events.Dispatch (this, event)
