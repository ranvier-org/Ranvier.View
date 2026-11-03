namespace Ranvier.View.WebCore

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text
open RanvierWebCoreAbi

/// <summary>The result returned by an application's native file picker.</summary>
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type FilePickerResult =
    | Selected of paths: string array
    | Cancelled

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: Ranvier.View.NativeDisplay.Context, depth: int, precedence: int) =
        Ranvier.View.NativeDisplay.union context depth precedence (box this) (fun nested ->
            match this with
            | Selected paths ->
                "Selected",
                [
                    Some (fun _ ->
                        Ranvier.View.NativeDisplay.array context nested (fun c d _ path -> Ranvier.View.NativeDisplay.text c d path) paths)
                ]
            | Cancelled -> "Cancelled", [])

    override this.ToString() =
        if RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            Ranvier.View.NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

/// <summary>A file-control request owned by the host that opened it.</summary>
/// <remarks>Complete on the host thread. Completion returns false when the control or request is no longer live.</remarks>
[<Sealed>]
type FilePickerRequest
    internal (id: uint64, multiple: bool, directories: bool, mimeTypes: string array, extensions: string array, complete: FilePickerResult -> bool) =
    /// <summary>The process-unique request identifier.</summary>
    member _.Id = id
    /// <summary>Whether multiple paths can be selected.</summary>
    member _.Multiple = multiple
    /// <summary>Whether the control requests directories.</summary>
    member _.Directories = directories
    /// <summary>The control's accepted MIME types.</summary>
    member _.MimeTypes = Array.copy mimeTypes
    /// <summary>The control's accepted file extensions.</summary>
    member _.Extensions = Array.copy extensions

    /// <summary>Completes the request with selected absolute paths or cancellation.</summary>
    member _.Complete(result: FilePickerResult) =
        complete result

/// <summary>A picker opening or the invalidation of a previously opened request.</summary>
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type FilePickerEvent =
    | Show of FilePickerRequest
    | Invalidate of requestId: uint64

#if !FABLE_COMPILER
    member internal this.NativeDisplay(context: Ranvier.View.NativeDisplay.Context, depth: int, precedence: int) =
        Ranvier.View.NativeDisplay.union context depth precedence (box this) (fun nested ->
            match this with
            | Show request ->
                "Show",
                [
                    Some (fun _ -> Ranvier.View.NativeDisplay.opaque context nested "Ranvier.View.WebCore.FilePickerRequest" (box request))
                ]
            | Invalidate id -> "Invalidate", [ Some (fun _ -> Ranvier.View.NativeDisplay.uint64 context nested id) ])

    override this.ToString() =
        if RuntimeFeature.IsDynamicCodeSupported then
            sprintf "%+A" this
        else
            Ranvier.View.NativeDisplay.format (fun context depth precedence -> this.NativeDisplay (context, depth, precedence))
#endif

[<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
type internal FilePickerCallback = delegate of context: nativeint * request: uint64 * action: int * flags: int -> unit

/// <summary>Connects a native file-control picker to an application provider.</summary>
module FilePickers =
    let private installed = ConditionalWeakTable<WebCoreHost, obj>()

    /// <summary>Installs one provider for a host, returning its registration.</summary>
    /// <remarks>Show and engine invalidation are delivered on a later host turn. Disposal invalidates an open request
    /// before returning. The provider owns its native dialog; dispose on the host thread.</remarks>
    /// <param name="host">The live native host.</param>
    /// <param name="provider">Receives show and invalidation events on the host thread.</param>
    let install (host: WebCoreHost) (provider: FilePickerEvent -> unit) : IDisposable =
        if isNull (box host) then
            nullArg (nameof host)

        if isNull (box provider) then
            nullArg (nameof provider)

        host.EnsureActive ()

        if not (WebCoreHost.Has "file-picker") then
            raise (NotSupportedException "The native host does not provide file picker requests")

        match installed.TryGetValue host with
        | true, _ -> invalidOp "The host already has a file picker provider"
        | _ -> ()

        let mutable disposed = false
        let mutable active = ValueNone
        let mutable currentProvider = ValueSome provider
        let mutable subscription: IDisposable = null
        let token = obj ()

        let complete id result =
            host.EnsureActive ()

            if disposed then
                raise (ObjectDisposedException "File picker provider")

            let deliver outcome pointers lengths count =
                let wasActive = active = ValueSome id

                if wasActive then
                    active <- ValueNone

                try
                    let status =
                        Native.rv_host_file_picker_complete (host.Handle, id, outcome, pointers, lengths, count)

                    if
                        status < 0
                        && status <> -10
                        && wasActive
                        && not disposed
                        && active = ValueNone
                    then
                        active <- ValueSome id

                    status
                with _ ->
                    if wasActive && not disposed && active = ValueNone then
                        active <- ValueSome id

                    reraise ()

            let status =
                match result with
                | Cancelled -> deliver 0 null null 0
                | Selected paths ->
                    if isNull paths then
                        nullArg "paths"

                    if paths |> Array.exists isNull then
                        invalidArg "paths" "Paths cannot contain null"

                    let bytes = paths |> Array.map Encoding.UTF8.GetBytes
                    let pins = ResizeArray<GCHandle>()

                    try
                        for data in bytes do
                            pins.Add (GCHandle.Alloc (data, GCHandleType.Pinned))

                        let pointers =
                            pins
                            |> Seq.map (fun pin -> pin.AddrOfPinnedObject ())
                            |> Seq.toArray

                        let lengths = bytes |> Array.map Array.length
                        deliver 1 pointers lengths pointers.Length
                    finally
                        for pin in pins do
                            pin.Free ()

            if status = -10 then
                false
            else
                Interop.check "rv_host_file_picker_complete" status
                true

        let filters id kind count =
            Array.init count (fun index ->
                Interop.text "rv_host_file_picker_filter" (fun buffer capacity length ->
                    Native.rv_host_file_picker_filter (host.Handle, id, kind, index, buffer, capacity, length)))

        let deliver id action flags =
            if not disposed then
                try
                    if action = 1 then
                        if active = ValueSome id then
                            active <- ValueNone

                            match currentProvider with
                            | ValueSome notify -> notify (Invalidate id)
                            | ValueNone -> ()
                    elif action = 0 then
                        let mutable mimeCount = 0

                        let status =
                            Native.rv_host_file_picker_filter_count (host.Handle, id, 0, &mimeCount)

                        if status <> -10 then
                            Interop.check "rv_host_file_picker_filter_count" status
                            let mutable extensionCount = 0

                            Native.rv_host_file_picker_filter_count (host.Handle, id, 1, &extensionCount)
                            |> Interop.check "rv_host_file_picker_filter_count"

                            let request =
                                FilePickerRequest (
                                    id,
                                    flags &&& 1 <> 0,
                                    flags &&& 2 <> 0,
                                    filters id 0 mimeCount,
                                    filters id 1 extensionCount,
                                    complete id
                                )

                            active <- ValueSome id

                            match currentProvider with
                            | ValueSome notify -> notify (Show request)
                            | ValueNone -> ()
                with error ->
                    host.Errors.Add error

        let callback =
            FilePickerCallback (fun _ id action flags ->
                if not disposed then
                    host.PostToOwner (fun () -> deliver id action flags))

        Native.rv_host_set_file_picker_handler (host.Handle, Marshal.GetFunctionPointerForDelegate<FilePickerCallback> callback, 0n)
        |> Interop.check "rv_host_set_file_picker_handler"

        installed.Add (host, token)

        let close () =
            if not disposed then
                disposed <- true
                let previousProvider = currentProvider
                currentProvider <- ValueNone

                if not (isNull subscription) then
                    let current = subscription
                    subscription <- null
                    current.Dispose ()

                Native.rv_host_set_file_picker_handler (host.Handle, 0n, 0n)
                |> ignore

                installed.Remove host |> ignore

                match active with
                | ValueSome id ->
                    active <- ValueNone

                    try
                        match previousProvider with
                        | ValueSome notify -> notify (Invalidate id)
                        | ValueNone -> ()
                    with error ->
                        host.Errors.Add error
                | ValueNone -> ()

                GC.KeepAlive callback

        subscription <- host.Disposed.Subscribe (fun () -> close ())

        { new IDisposable with
            member _.Dispose() =
                if not disposed then
                    host.EnsureActive ()
                    close ()
        }
