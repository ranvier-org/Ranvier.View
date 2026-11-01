namespace Ranvier.View.WebCore

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Form submissions of a native host.</summary>
module FormSubmissions =
    let private installed = ConditionalWeakTable<WebCoreHost, obj>()

    let private text (pointer: nativeint) =
        if pointer = 0n then
            null
        else
            Marshal.PtrToStringUTF8 pointer

    /// <summary>A FormSubmission service for <c>host</c>'s documents.</summary>
    /// <remarks>
    /// Raises NotSupportedException when the native library lacks form submission reporting (ABI 42), and
    /// InvalidOperationException while another service of the host is undisposed. Host documents stay in place on
    /// submission with or without a service.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation and every handler.</param>
    let forHost (host: WebCoreHost) : FormSubmissionService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if WebCoreHost.Has "form-submission" <> true then
            raise (NotSupportedException "The native library has no form submission reporting (ABI 42)")

        let mutable existing = null

        if installed.TryGetValue (host, &existing) then
            raise (InvalidOperationException "Another FormSubmission service of this host is undisposed")

        let session = host.Session
        let handlers = FormSubmissionContract.Handlers ()
        let mutable disposed = false

        let node (handle: nativeint) =
            if handle = 0n then
                null
            else
                session.Node (Native.rv_node_retain handle)

        let handler =
            Native.FormSubmissionHandler (fun _ pointer ->
                try
                    let raw = Marshal.PtrToStructure<Native.FormSubmission> pointer
                    let body = Array.zeroCreate<byte>(max raw.bodyLength 0)

                    if body.Length > 0 then
                        Marshal.Copy (raw.body, body, 0, body.Length)

                    handlers.Deliver
                        {
                            Method = text raw.method
                            Url = text raw.url
                            Target = text raw.target
                            ContentType = Option.ofObj (text raw.contentType)
                            Body = body
                            Form = unbox<HTMLFormElement>(node raw.form)
                            Submitter =
                                node raw.submitter
                                |> Option.ofObj
                                |> Option.map unbox<HTMLElement>
                        }
                with error ->
                    Interop.callbackFailed error)

        Native.rv_host_set_form_submission_handler (host.Handle, Marshal.GetFunctionPointerForDelegate<Native.FormSubmissionHandler> handler, 0n)
        |> Interop.check "rv_host_set_form_submission_handler"

        installed.Add (host, box handler)

        let release (detach: bool) =
            if not disposed then
                disposed <- true

                if detach then
                    Native.rv_host_set_form_submission_handler (host.Handle, 0n, 0n)
                    |> ignore

                installed.Remove host |> ignore
                handlers.Clear ()
                GC.KeepAlive handler

        let hostDisposal = host.Disposed.Subscribe (fun () -> release false)

        { new FormSubmissionService with
            member _.Subscribe handler =
                if disposed then
                    raise (ObjectDisposedException (nameof FormSubmissionService))

                host.EnsureActive ()
                handlers.Add (FormSubmissionContract.handler handler)

            member _.Dispose() =
                if not disposed then
                    host.EnsureActive ()
                    release true
                    hostDisposal.Dispose ()
        }
