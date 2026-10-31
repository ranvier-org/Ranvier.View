namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>FormData objects, SubmitEvents and FormDataEvents of a native host.</summary>
module FormData =
    let private bytes (value: string) =
        Encoding.UTF8.GetBytes value

    let private entryValue (file: nativeint -> File) (union: NativeDom.RvDomUnionFileOrUSVString) =
        if union.present <> 1 then
            invalidOp "WebCore returned an absent FormData entry value"

        match union.tag with
        | NativeDom.RV_DOM_UNION_FILE_OR_USV_STRING_STRING ->
            let text = Interop.spanText union.arm1

            if isNull text then
                invalidOp "WebCore returned a null FormData string entry"

            FormDataEntryValue.String text
        | NativeDom.RV_DOM_UNION_FILE_OR_USV_STRING_FILE -> FormDataEntryValue.File (file union.arm0)
        | other -> invalidOp $"WebCore returned the unknown FormData entry tag {other}"

    let inline private sequence<'T, 'U when 'T: struct> (items: nativeint) (count: int) ([<InlineIfLambda>] read: 'T -> 'U) =
        if count < 0 || (count > 0 && items = 0n) then
            invalidOp "WebCore returned an invalid FormData sequence"

        Array.init count (fun index -> read (Marshal.PtrToStructure<'T>(items + nativeint (index * Marshal.SizeOf<'T>()))))

    /// <summary>A FormData service for <c>host</c>'s current document.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the DOMFormData constructor.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : FormDataService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if
            not (
                WebCoreHost.Has "dom-constructor:DOMFormData"
                && WebCoreHost.Has "dom-form-data-entries"
            )
        then
            raise (NotSupportedException "The native library has no FormData functions")

        let session = host.Session
        let document = host.Document
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof FormDataService))

            host.EnsureActive ()

        let native (value: obj) (name: string) =
            match value with
            | null -> 0n
            | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) -> Interop.handle value
            | _ -> invalidArg name $"The {name} belongs to another provider"

        let formData (value: DOMFormData) =
            native (box (FormDataContract.formData value)) "formData"

        let wrap (handle: nativeint) =
            session.Object (
                handle,
                Kind.FormData,
                unbox<NativeObject>(box document),
                Func<Session, nativeint, NativeObject>(fun s h -> DOMFormDataObject (s, h))
            )
            |> unbox<DOMFormData>

        let value =
            entryValue (fun handle ->
                if handle = 0n then
                    invalidOp "WebCore returned a File entry without a handle"

                NativeDom.rv_dom_blob_retain handle |> ignore

                session.Object (
                    handle,
                    Kind.Blob,
                    unbox<NativeObject>(box document),
                    Func<Session, nativeint, NativeObject>(fun s h -> FileObject (s, h))
                )
                |> unbox<File>)

        let create (form: nativeint) (submitter: nativeint) =
            let mutable result = 0n

            NativeDom.rv_dom_dom_form_data_create (Interop.handle (box document), form, submitter, &result)
            |> Interop.check "FormData constructor"

            wrap result

        let init (options: EventOptions) =
            (if options.Bubbles then 1 else 0), (if options.Cancelable then 1 else 0), (if options.Composed then 1 else 0)

        { new FormDataService with
            member _.Create() =
                check ()
                create 0n 0n

            member _.FromForm(form, submitter) =
                check ()
                let form = native (box (FormDataContract.form form)) "form"
                create form (native (box submitter) "submitter")

            member _.Get(target, name) =
                check ()
                let target = formData target
                let name = bytes (FormDataContract.name name)
                let mutable result = NativeDom.RvDomUnionFileOrUSVStringNullable ()

                try
                    NativeDom.rv_dom_dom_form_data_get (target, name, name.Length, &result)
                    |> Interop.check "FormData.get"

                    if result.present = 0 then
                        None
                    else
                        let mutable arm = NativeDom.RvDomUnionFileOrUSVString ()
                        arm.present <- result.present
                        arm.tag <- result.tag
                        arm.arm0 <- result.arm0
                        arm.arm1 <- result.arm1
                        Some (value arm)
                finally
                    NativeDom.rv_dom_union_file_or_usv_string_nullable_release &result

            member _.GetAll(target, name) =
                check ()
                let target = formData target
                let name = bytes (FormDataContract.name name)
                let mutable result = NativeDom.RvDomSequenceFileOrUSVString ()

                try
                    NativeDom.rv_dom_dom_form_data_get_all (target, name, name.Length, &result)
                    |> Interop.check "FormData.getAll"

                    if result.present <> 1 then
                        invalidOp "WebCore returned an absent FormData value sequence"

                    sequence result.items result.count value
                finally
                    NativeDom.rv_dom_sequence_file_or_usv_string_release &result

            member _.Entries target =
                check ()
                let target = formData target
                let mutable result = NativeDom.RvDomSequencePairUSVStringAndUnionFileOrUSVString ()

                try
                    NativeDom.rv_dom_dom_form_data_entries (target, Interop.handle (box document), &result)
                    |> Interop.check "FormData.entries"

                    if result.present <> 1 then
                        invalidOp "WebCore returned an absent FormData entry snapshot"

                    sequence result.items result.count (fun (entry: NativeDom.RvDomPairUSVStringAndUnionFileOrUSVString) ->
                        let name = Interop.spanText entry.key

                        if entry.present <> 1 || isNull name then
                            invalidOp "WebCore returned an invalid FormData entry"

                        {
                            Name = name
                            Value = value entry.value
                        })
                finally
                    NativeDom.rv_dom_sequence_pair_usv_string_and_union_file_or_usv_string_release &result

            member _.CreateSubmitEvent(eventType, options, submitter) =
                check ()
                let eventType = bytes (FormDataContract.eventType eventType)
                let bubbles, cancelable, composed = init options
                let mutable dictionary = NativeDom.RvDomSubmitEventInit ()
                dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomSubmitEventInit>())
                dictionary.bubbles <- bubbles
                dictionary.cancelable <- cancelable
                dictionary.composed <- composed
                dictionary.submitter <- native (box submitter) "submitter"
                let mutable result = 0n

                NativeDom.rv_dom_submit_event_create (Interop.handle (box document), eventType, eventType.Length, [| dictionary |], &result)
                |> Interop.check "SubmitEvent constructor"

                unbox<SubmitEvent>(session.Event result)

            member _.CreateFormDataEvent(eventType, options, data) =
                check ()
                let eventType = bytes (FormDataContract.eventType eventType)
                let bubbles, cancelable, composed = init options
                let mutable dictionary = NativeDom.RvDomFormDataEventInit ()
                dictionary.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomFormDataEventInit>())
                dictionary.bubbles <- bubbles
                dictionary.cancelable <- cancelable
                dictionary.composed <- composed
                dictionary.form_data <- formData data
                let mutable result = 0n

                NativeDom.rv_dom_form_data_event_create (Interop.handle (box document), eventType, eventType.Length, [| dictionary |], &result)
                |> Interop.check "FormDataEvent constructor"

                unbox<FormDataEvent>(session.Event result)

            member _.Dispose() =
                disposed.Value <- true
        }
