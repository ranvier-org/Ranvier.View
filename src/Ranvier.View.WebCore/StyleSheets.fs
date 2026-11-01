namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Owned stylesheet providers over a WebCore host's document and microtask loop.</summary>
module StyleSheets =
    /// <summary>Creates a provider whose operations and disposal run on the host's creating thread.</summary>
    /// <param name="host">The live owner of constructed sheets, adopted scopes and asynchronous completion.</param>
    let forHost (host: WebCoreHost) : StyleSheetService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()
        let thread = Environment.CurrentManagedThreadId
        let session = host.Session
        let document = host.Document
        let documentOwner = box document :?> NativeObject
        let pending = Dictionary<int, unit -> unit>()
        let mutable nextId = 0
        let mutable disposed = false
        let mutable subscription: IDisposable = null

        let active () =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException "StyleSheetService")

        let feature name =
            if not (WebCoreHost.Has name) then
                Interop.unsupported<unit> name

        let native name (value: obj) =
            if isNull value then
                nullArg name

            match value with
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) ->
                Interop.handle value |> ignore
                target
            | _ -> invalidArg name "The object belongs to another host or provider"

        let scope (value: Node) =
            active ()
            let target = native (nameof value) (box value)
            let isDocument = box value :? Document

            if not (isDocument || box value :? ShadowRoot) then
                invalidArg (nameof value) "A stylesheet scope must be a Document or ShadowRoot"

            let owner =
                if isDocument then
                    unbox<Document> value
                else
                    value.ownerDocument

            if not (obj.ReferenceEquals (owner, document)) then
                invalidArg (nameof value) "The scope belongs to another document"

            feature "dom-adopted-style-sheets"
            target, isDocument

        let engine action =
            try
                action ()
            with WebCoreDomException (name, detail) ->
                raise (StyleSheetException (name, detail))

        let wrap owner handle =
            session.Object (
                handle,
                Kind.StyleSheet,
                owner,
                Func<Session, nativeint, NativeObject>(fun ownerSession value -> Classes.StyleSheet (ownerSession, value))
            )
            |> unbox<CSSStyleSheet>

        let dispose () =
            if not disposed then
                disposed <- true

                if not (isNull subscription) then
                    subscription.Dispose ()

                for cancel in pending.Values |> Seq.toArray do
                    cancel ()

        subscription <- host.Disposed.Subscribe (fun () -> dispose ())

        { new StyleSheetService with
            member _.SupportsBaseUrl = WebCoreHost.Has "dom-constructor:CSSStyleSheet"

            member _.Create options =
                active ()
                feature "dom-constructor:CSSStyleSheet"

                if isNull (box options) then
                    nullArg (nameof options)

                use inputs = new InputScope ()
                let mutable media = NativeDom.RvDomUnionMediaListOrDOMString ()
                media.present <- 1

                match options.Media with
                | MediaText text ->
                    if isNull text then
                        nullArg "options.Media"

                    media.tag <- NativeDom.RV_DOM_UNION_MEDIA_LIST_OR_DOM_STRING_STRING
                    media.arm1 <- inputs.TextSpan text
                | MediaList list ->
                    media.tag <- NativeDom.RV_DOM_UNION_MEDIA_LIST_OR_DOM_STRING_MEDIA_LIST
                    media.arm0 <- (native "options.Media" (box list)).Handle

                let baseUrl =
                    match options.BaseUrl with
                    | Some url -> url
                    | None -> null

                let mutable init = NativeDom.RvDomCSSStyleSheetInit ()
                init.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomCSSStyleSheetInit>())
                init.base_url <- inputs.TextInput (Option.ofObj baseUrl)
                init.media <- inputs.Pin [| media |]
                init.disabled <- if options.Disabled then 1 else 0

                engine (fun () ->
                    let mutable result = 0n

                    NativeDom.rv_dom_css_style_sheet_create (documentOwner.Handle, [| init |], &result)
                    |> Interop.check "CSSStyleSheet.constructor"

                    wrap documentOwner result)

            member _.GetAdopted target =
                let owner, isDocument = scope target

                engine (fun () ->
                    let mutable result = NativeDom.RvDomSequenceCSSStyleSheet ()

                    try
                        let status =
                            if isDocument then
                                NativeDom.rv_dom_document_get_adopted_style_sheets (owner.Handle, &result)
                            else
                                NativeDom.rv_dom_shadow_root_get_adopted_style_sheets (owner.Handle, &result)

                        Interop.check "adoptedStyleSheets.get" status

                        Array.init result.count (fun index ->
                            let handle = Marshal.ReadIntPtr (result.items, index * IntPtr.Size)

                            NativeDom.rv_dom_style_sheet_retain handle
                            |> ignore

                            wrap owner handle)
                    finally
                        NativeDom.rv_dom_sequence_css_style_sheet_release &result)

            member _.SetAdopted(target, sheets) =
                let owner, isDocument = scope target

                if isNull sheets then
                    nullArg (nameof sheets)

                use inputs = new InputScope ()

                let values =
                    sheets
                    |> Array.map (fun sheet -> (native (nameof sheets) (box sheet)).Handle)

                let mutable sequence = NativeDom.RvDomSequenceCSSStyleSheet ()
                sequence.present <- 1
                sequence.count <- values.Length
                sequence.items <- inputs.Pin values

                engine (fun () ->
                    let status =
                        if isDocument then
                            NativeDom.rv_dom_document_set_adopted_style_sheets (owner.Handle, [| sequence |])
                        else
                            NativeDom.rv_dom_shadow_root_set_adopted_style_sheets (owner.Handle, [| sequence |])

                    Interop.check "adoptedStyleSheets.set" status)

            member _.ReplaceAsync(sheet, text) =
                async {
                    let! token = Async.CancellationToken
                    active ()
                    native (nameof sheet) (box sheet) |> ignore

                    if isNull text then
                        nullArg (nameof text)

                    return!
                        Async.FromContinuations (fun (success, error, cancel) ->
                            nextId <- nextId + 1
                            let id = nextId
                            let mutable finished = false
                            let mutable unregister = ignore

                            let finish action =
                                if not finished then
                                    finished <- true
                                    pending.Remove id |> ignore
                                    unregister ()
                                    action ()

                            let cancelled () =
                                finish (fun () -> cancel (OperationCanceledException token))

                            pending[id] <- cancelled
                            let registration = token.Register (Action (fun () -> host.PostToOwner cancelled))
                            unregister <- fun () -> registration.Dispose ()

                            if token.IsCancellationRequested then
                                cancelled ()
                            else
                                let result =
                                    try
                                        engine (fun () -> sheet.replaceSync text)
                                        Ok sheet
                                    with reason ->
                                        Error reason

                                host.QueueMicrotask (fun () ->
                                    if token.IsCancellationRequested then
                                        cancelled ()
                                    else
                                        match result with
                                        | Ok value -> finish (fun () -> success value)
                                        | Error reason -> finish (fun () -> error reason)))
                }

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    raise (InvalidOperationException "A stylesheet service is disposed on its host thread")

                dispose ()
        }
