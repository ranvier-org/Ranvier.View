namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open System.Threading.Tasks
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>A request WebCore makes of the application's notification presenter, numbered as <c>RV_NOTIFICATION_*</c>.</summary>
type NotificationRequest =
    | Show = 0
    | Cancel = 1
    | Destroyed = 2

/// <summary>An event the presenter reports for a notification, numbered as <c>RV_NOTIFICATION_EVENT_*</c>.</summary>
type NotificationEvent =
    | Show = 0
    | Click = 1
    | Close = 2
    | Error = 3

type internal NotificationPresenterCallback = delegate of context: nativeint * host: nativeint * notification: nativeint * request: int -> unit

/// <summary>A native Notification: its fields and the events its presenter reports.</summary>
type internal NativeNotification(host: WebCoreHost, handle: nativeint) =
    let subscribers = Dictionary<string, List<int * (unit -> unit)>>()
    let listeners = List<nativeint>()
    let delegates = List<Native.EventCallback>()
    let mutable next = 0
    let mutable closed = false

    let text field =
        Interop.text "rv_notification_string" (fun buffer capacity length -> Native.rv_notification_string (handle, field, buffer, capacity, length))
        |> fun value -> if isNull value then "" else value

    member _.Handle = handle

    member _.Close() =
        if not closed then
            closed <- true

            for listener in listeners do
                Native.rv_listener_remove listener |> ignore

            listeners.Clear ()
            subscribers.Clear ()
            Native.rv_event_target_release handle

    member private _.Subscribe(kind: string, callback: unit -> unit) : IDisposable =
        host.EnsureActive ()

        if isNull (box callback) then
            nullArg "callback"

        let list =
            match subscribers.TryGetValue kind with
            | true, list -> list
            | _ ->
                let list = List<int * (unit -> unit)>()
                subscribers[kind] <- list

                let run =
                    Native.EventCallback (fun _ _ ->
                        for _, callback in list.ToArray () do
                            try
                                callback ()
                            with error ->
                                host.Errors.Add error

                        0)

                delegates.Add run

                let listener =
                    Native.rv_event_target_add_listener (handle, kind, 0, Marshal.GetFunctionPointerForDelegate<Native.EventCallback> run, 0n)

                if listener = 0n then
                    raise (InvalidOperationException "rv_event_target_add_listener returned no listener")

                listeners.Add listener
                list

        let id = next
        next <- next + 1
        list.Add ((id, callback))

        { new IDisposable with
            member _.Dispose() =
                list.RemoveAll (fun (candidate, _) -> candidate = id)
                |> ignore
        }

    interface NotificationHandle with
        member _.Title =
            host.EnsureActive ()
            text 0

        member _.Body =
            host.EnsureActive ()
            text 1

        member _.Tag =
            host.EnsureActive ()
            text 2

        member _.Lang =
            host.EnsureActive ()
            text 3

        member _.Icon =
            host.EnsureActive ()
            text 4

        member _.Dir =
            host.EnsureActive ()
            let mutable dir = 0
            let mutable silent = 0

            Native.rv_notification_flags (handle, &dir, &silent)
            |> Interop.check "rv_notification_flags"

            enum<NotificationDirection> dir

        member _.Silent =
            host.EnsureActive ()
            let mutable dir = 0
            let mutable silent = 0

            Native.rv_notification_flags (handle, &dir, &silent)
            |> Interop.check "rv_notification_flags"

            if silent < 0 then None else Some (silent = 1)

        member _.Close() =
            host.EnsureActive ()

            Native.rv_notification_close handle
            |> Interop.check "rv_notification_close"

        member this.OnShow callback =
            this.Subscribe ("show", callback)

        member this.OnClick callback =
            this.Subscribe ("click", callback)

        member this.OnClose callback =
            this.Subscribe ("close", callback)

        member this.OnError callback =
            this.Subscribe ("error", callback)

/// <summary>Notifications on native hosts, presented by the application.</summary>
module Notifications =
    let private utf8 (value: string) =
        Encoding.UTF8.GetBytes value

    /// <summary>Reports a presenter event for a notification of a native service; it dispatches on a later host turn.</summary>
    let report (notification: NotificationHandle) (event: NotificationEvent) =
        match box notification with
        | :? NativeNotification as native ->
            Native.rv_notification_report (native.Handle, int event)
            |> Interop.check "rv_notification_report"
        | _ -> raise (ArgumentException "The notification does not belong to a native host")

    /// <summary>A notification service for <c>host</c>'s document.</summary>
    /// <remarks>
    /// <c>permission</c> is the application's setting, which RequestPermission returns. <c>presenter</c> receives each
    /// request on a later host turn and answers with <c>report</c>; without one a granted show fires error. A host has
    /// one presenter and permission: the latest service's. Disposal removes the presenter and resets the permission.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    /// <param name="permission">The permission the application grants this host's document.</param>
    /// <param name="presenter">The application's presenter, called with the notification and the request.</param>
    let forHost
        (host: WebCoreHost)
        (permission: NotificationPermission)
        (presenter: (NotificationHandle -> NotificationRequest -> unit) option)
        : NotificationService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        let shown = List<NativeNotification>()
        let mutable disposed = false
        let mutable hostGone = false

        let find (target: nativeint) : NotificationHandle =
            match
                shown
                |> Seq.tryFind (fun item -> Native.rv_event_target_equals (item.Handle, target) = 1)
            with
            | Some item -> item :> NotificationHandle
            | None ->
                let item = new NativeNotification (host, Native.rv_event_target_retain target)
                shown.Add item
                item :> NotificationHandle

        let callback =
            NotificationPresenterCallback (fun _ _ target request ->
                match presenter with
                | Some present when not (isNull (box present)) ->
                    try
                        present (find target) (enum<NotificationRequest> request)
                    with error ->
                        host.Errors.Add error
                | _ -> ())

        let callbackPointer =
            Marshal.GetFunctionPointerForDelegate<NotificationPresenterCallback> callback

        Native.rv_host_set_notification_permission (host.Handle, int permission)
        |> Interop.check "rv_host_set_notification_permission"

        Native.rv_host_set_notification_presenter (host.Handle, (if presenter.IsSome then callbackPointer else 0n), 0n)
        |> Interop.check "rv_host_set_notification_presenter"

        let close () =
            if not disposed then
                disposed <- true
                GC.KeepAlive callback

                for item in shown.ToArray () do
                    item.Close ()

                shown.Clear ()

        let check () =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException (nameof NotificationService))

        let service =
            { new NotificationService with
                member _.Permission =
                    check ()
                    let mutable value = 0

                    Native.rv_host_notification_permission (host.Handle, &value)
                    |> Interop.check "rv_host_notification_permission"

                    enum<NotificationPermission> value

                member this.RequestPermission() =
                    check ()
                    let value = this.Permission
                    let completion = TaskCompletionSource<NotificationPermission>()
                    host.PostToOwner (fun () -> completion.SetResult value)
                    completion.Task

                member _.Show(title, options, signal) =
                    check ()
                    NotificationContract.check title options
                    let titleBytes = utf8 title
                    let body = utf8 options.Body
                    let tag = utf8 options.Tag
                    let lang = utf8 options.Lang
                    let icon = utf8 options.Icon

                    let pins =
                        [|
                            GCHandle.Alloc (body, GCHandleType.Pinned)
                            GCHandle.Alloc (tag, GCHandleType.Pinned)
                            GCHandle.Alloc (lang, GCHandleType.Pinned)
                            GCHandle.Alloc (icon, GCHandleType.Pinned)
                        |]

                    try
                        let mutable init = Native.NotificationOptions ()
                        init.size <- 64
                        init.dir <- int options.Dir

                        init.silent <-
                            match options.Silent with
                            | Some true -> 1
                            | Some false -> 0
                            | None -> -1

                        init.body_length <- body.Length
                        init.tag_length <- tag.Length
                        init.lang_length <- lang.Length
                        init.icon_length <- icon.Length
                        init.body <- pins[0].AddrOfPinnedObject()
                        init.tag <- pins[1].AddrOfPinnedObject()
                        init.lang <- pins[2].AddrOfPinnedObject()
                        init.icon <- pins[3].AddrOfPinnedObject()
                        let created = [| 0n |]

                        Native.rv_notification_create (host.Handle, titleBytes, titleBytes.Length, &init, created)
                        |> Interop.check "rv_notification_create"

                        let item = new NativeNotification (host, created[0])
                        shown.Add item
                        let handle = item :> NotificationHandle
                        NotificationContract.bind (NotificationContract.signal signal) handle
                        handle
                    finally
                        for pin in pins do
                            pin.Free ()

                member _.Dispose() =
                    if not disposed && not hostGone then
                        Native.rv_host_set_notification_presenter (host.Handle, 0n, 0n)
                        |> ignore

                        Native.rv_host_set_notification_permission (host.Handle, 0)
                        |> ignore

                    close ()
            }

        host.Disposed.Add (fun () ->
            hostGone <- true
            close ())

        service

    let mutable private badgeHandlers = 0

    /// <summary>An application badge service for <c>host</c>; <c>handler</c> receives each badge on a later host turn.</summary>
    /// <remarks>Without a handler the slim embed has no badge service, and Set and Clear fault with NotSupportedException.</remarks>
    let badgeForHost (host: WebCoreHost) (handler: (uint64 option -> unit) option) : AppBadgeService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        let handler =
            handler
            |> Option.filter (fun value -> not (isNull (box value)))

        let mutable disposed = false

        if handler.IsSome then
            badgeHandlers <- badgeHandlers + 1

        let deliver (value: uint64 option) =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException (nameof AppBadgeService))

            let completion = TaskCompletionSource<unit>()

            match handler with
            | Some handle ->
                host.PostToOwner (fun () ->
                    try
                        handle value
                        completion.SetResult ()
                    with error ->
                        completion.SetException error)
            | None -> host.PostToOwner (fun () -> completion.SetException (NotSupportedException "The slim embed has no application badge service"))

            completion.Task

        { new AppBadgeService with
            member _.Set count =
                deliver count

            member _.Clear() =
                deliver (Some 0UL)

            member _.Dispose() =
                if not disposed then
                    disposed <- true

                    if handler.IsSome then
                        badgeHandlers <- badgeHandlers - 1
        }

    /// <summary>Capability facts for the application badge on native hosts: available while a handler is registered.</summary>
    let badgeCapabilityProvider: CapabilityProvider =
        let key = Capabilities.key CapabilityCategory.PlatformFacility "app-badge"

        {
            Id = "webcore-app-badge"
            KnownKeys = [| key |]
            Observe =
                fun requested _ ->
                    if requested = key then
                        let observation =
                            Capabilities.unknown key "webcore-app-badge" "Application handler" "process" "Unobserved"

                        let available =
                            if badgeHandlers > 0 then
                                {
                                    State = CapabilityState.Yes
                                    Reason = "An application handler receives the badge."
                                }
                            else
                                {
                                    State = CapabilityState.No
                                    Reason = "The slim embed has no application badge service."
                                }

                        Task.FromResult (
                            Some
                                { observation with
                                    Implemented = available
                                    Available = available
                                }
                        )
                    else
                        Task.FromResult None
        }
