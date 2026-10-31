namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>A native host's font set over the host document's FontFaceSet ("font-readiness").</summary>
[<RequireQualifiedAccess>]
module Fonts =
    /// <summary>Completions and events from native calls, keyed by the context value passed to the host.</summary>
    type private NativeCallbacks() =
        let gate = obj ()
        let completions = Dictionary<int64, int -> int -> unit>()
        let watchers = Dictionary<int64, int -> string list -> unit>()
        let mutable next = 0L

        let completion =
            Native.ResourceCompletion (fun context outcome width _ ->
                let handler =
                    lock gate (fun () ->
                        match completions.TryGetValue (int64 context) with
                        | true, handler ->
                            completions.Remove (int64 context) |> ignore
                            Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler outcome width))

        let event =
            Native.FontsEventCallback (fun context kind families lengths count ->
                let names =
                    [
                        for index in 0 .. count - 1 do
                            let name = Marshal.ReadIntPtr (families, index * IntPtr.Size)
                            let length = Marshal.ReadInt32 (lengths, index * 4)
                            let bytes = Array.zeroCreate<byte> length
                            Marshal.Copy (name, bytes, 0, length)
                            Encoding.UTF8.GetString bytes
                    ]

                let handler =
                    lock gate (fun () ->
                        match watchers.TryGetValue (int64 context) with
                        | true, handler -> Some handler
                        | _ -> None)

                handler
                |> Option.iter (fun handler -> handler kind names))

        member _.Completion =
            Marshal.GetFunctionPointerForDelegate<Native.ResourceCompletion> completion

        member _.Event = Marshal.GetFunctionPointerForDelegate<Native.FontsEventCallback> event

        member _.AddCompletion(handler) =
            lock gate (fun () ->
                next <- next + 1L
                completions[next] <- handler
                next)

        member _.AddWatcher(handler) =
            lock gate (fun () ->
                next <- next + 1L
                watchers[next] <- handler
                next)

        member _.Remove(key: int64) =
            lock gate (fun () ->
                completions.Remove key |> ignore
                watchers.Remove key |> ignore)

    let private callbacks = NativeCallbacks ()

    let private utf8 (value: string) =
        Encoding.UTF8.GetBytes value

    let private failure (status: int) =
        match Interop.failure "rv_fonts_load" status with
        | WebCoreDomException (name, _) -> ResourceFailure.LoadFailed name
        | error -> ResourceFailure.LoadFailed error.Message

    /// <summary>Creates a font set whose callbacks and events run on the host's owner context.</summary>
    /// <remarks>Disposing the set, or the host, cancels and releases its native operations and ends its watch. The
    /// watch follows the host's current document.</remarks>
    let forHost (host: WebCoreHost) : FontSet =
        if isNull (box host) then
            nullArg "host"

        host.EnsureActive ()

        if WebCoreHost.Has "font-readiness" <> true then
            raise (NotSupportedException "The native host lacks font-readiness.")

        let live = HashSet<IDisposable>(HashIdentity.Reference)
        let handlers = List<FontSetEvent -> unit>()
        let mutable watch = 0n
        let mutable watchDocument: Document = null
        let mutable watchKey = 0L
        let mutable disposed = false

        let ensure () =
            if disposed then
                raise (ObjectDisposedException "FontSet")

            host.EnsureActive ()

        let deliver (kind: int) (families: string list) =
            let value =
                match kind with
                | 0 -> FontSetEvent.Loading
                | 1 -> FontSetEvent.LoadingDone families
                | _ -> FontSetEvent.LoadingError families

            for handler in List.ofSeq handlers do
                handler value

        let unwatch () =
            if watch <> 0n then
                Native.rv_fonts_unwatch watch
                callbacks.Remove watchKey
                watch <- 0n
                watchDocument <- null

        /// Watches the current document while subscriptions exist.
        let rewatch () =
            if handlers.Count = 0 then
                unwatch ()
            elif
                watch = 0n
                || not (obj.ReferenceEquals (watchDocument, host.Document))
            then
                unwatch ()
                watchKey <- callbacks.AddWatcher deliver
                let handle = [| 0n |]

                Native.rv_fonts_watch (host.Handle, callbacks.Event, nativeint watchKey, handle)
                |> Interop.check "rv_fonts_watch"

                watch <- handle[0]
                watchDocument <- host.Document

        let documentReplaced =
            host.DocumentReplaced.Subscribe (fun () ->
                if not disposed then
                    rewatch ())

        let start (terminal: ResourceOutcome<'T> -> unit) (begin': nativeint -> struct (int * uint64)) (value: int -> 'T) =
            let mutable operation = 0UL
            let mutable arbiter = Unchecked.defaultof<ResourceArbiter<'T>>

            let release () =
                if operation <> 0UL then
                    Native.rv_resource_release operation |> ignore
                    operation <- 0UL

            arbiter <-
                new ResourceArbiter<'T> (
                    host.PostToOwner,
                    (fun outcome ->
                        live.Remove arbiter |> ignore
                        release ()
                        terminal outcome),
                    (fun () ->
                        if operation <> 0UL then
                            Native.rv_resource_cancel operation |> ignore),
                    ignore
                )

            live.Add arbiter |> ignore

            let key =
                callbacks.AddCompletion (fun outcome width ->
                    match outcome with
                    | 0 ->
                        arbiter.Settle (ResourceOutcome.Succeeded (value width))
                        |> ignore
                    | 1 ->
                        arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.LoadFailed "NetworkError"))
                        |> ignore
                    | 3 ->
                        arbiter.Settle (ResourceOutcome.Failed ResourceFailure.Cancelled)
                        |> ignore
                    | _ ->
                        arbiter.Settle (ResourceOutcome.Failed (ResourceFailure.Invalidated "document replaced or host destroyed"))
                        |> ignore)

            let struct (status, token) = begin' (nativeint key)
            operation <- token

            if status <> 0 then
                callbacks.Remove key

                arbiter.Settle (ResourceOutcome.Failed (failure status))
                |> ignore

            arbiter :> ResourceOperation<'T>

        let disposeAll () =
            if not disposed then
                disposed <- true

                for operation in List.ofSeq live do
                    operation.Dispose ()

                live.Clear ()
                handlers.Clear ()
                unwatch ()
                documentReplaced.Dispose ()

        let hostDisposal = host.Disposed.Subscribe (fun () -> disposeAll ())

        { new FontSet with
            member _.Status =
                ensure ()
                let mutable status = 0

                Native.rv_fonts_status (host.Handle, &status)
                |> Interop.check "rv_fonts_status"

                if status = 1 then
                    FontSetStatus.Loading
                else
                    FontSetStatus.Loaded

            member _.Check(font, text) =
                ensure ()
                FontSetContract.validate font text
                let font = utf8 font
                let text = utf8 text
                let mutable loaded = 0

                match Native.rv_fonts_check (host.Handle, font, font.Length, text, text.Length, &loaded) with
                | 0 -> Ok (loaded = 1)
                | status ->
                    match Interop.failure "rv_fonts_check" status with
                    | WebCoreDomException (name, _) -> Error name
                    | error -> raise error

            member _.Load(font, text, terminal) =
                ensure ()
                FontSetContract.validate font text
                let font = utf8 font
                let text = utf8 text

                start
                    terminal
                    (fun context ->
                        let mutable operation = 0UL

                        let status =
                            Native.rv_fonts_load (host.Handle, font, font.Length, text, text.Length, callbacks.Completion, context, &operation)

                        struct (status, operation))
                    id

            member _.Ready terminal =
                ensure ()

                start
                    terminal
                    (fun context ->
                        let mutable operation = 0UL

                        let status =
                            Native.rv_fonts_ready (host.Handle, callbacks.Completion, context, &operation)

                        struct (status, operation))
                    ignore

            member _.Subscribe handler =
                ensure ()

                if isNull (box handler) then
                    nullArg "handler"

                handlers.Add handler
                rewatch ()
                let mutable active = true

                { new IDisposable with
                    member _.Dispose() =
                        if active then
                            active <- false
                            handlers.Remove handler |> ignore

                            if not disposed then
                                rewatch ()
                }

          interface IDisposable with
              member _.Dispose() =
                  hostDisposal.Dispose ()
                  disposeAll ()
        }
