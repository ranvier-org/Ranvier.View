namespace Ranvier.View.WebCore

open System
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Text.Json
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Typed and custom event construction and standalone targets on a native host.</summary>
module EventConstruction =
    let private write (detail: TimingDetail) =
        use stream = new MemoryStream ()

        (use writer = new Utf8JsonWriter (stream)

         let rec emit value =
             match value with
             | TimingDetail.Null -> writer.WriteNullValue ()
             | TimingDetail.Boolean flag -> writer.WriteBooleanValue flag
             | TimingDetail.Number number -> writer.WriteNumberValue number
             | TimingDetail.Text text -> writer.WriteStringValue text
             | TimingDetail.Array items ->
                 writer.WriteStartArray ()
                 Array.iter emit items
                 writer.WriteEndArray ()
             | TimingDetail.Object fields ->
                 writer.WriteStartObject ()

                 for key, item in fields do
                     writer.WritePropertyName key
                     emit item

                 writer.WriteEndObject ()

         emit detail)

        Encoding.UTF8.GetString (stream.ToArray ())

    let rec private read (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null -> TimingDetail.Null
        | JsonValueKind.True -> TimingDetail.Boolean true
        | JsonValueKind.False -> TimingDetail.Boolean false
        | JsonValueKind.Number -> TimingDetail.Number (element.GetDouble ())
        | JsonValueKind.String -> TimingDetail.Text (element.GetString ())
        | JsonValueKind.Array -> TimingDetail.Array [| for item in element.EnumerateArray () -> read item |]
        | JsonValueKind.Object ->
            TimingDetail.Object
                [|
                    for property in element.EnumerateObject () -> property.Name, read property.Value
                |]
        | other -> raise (InvalidOperationException $"Unexpected detail JSON value {other}")

    let private modifiers (value: EventModifiers) = int value

    /// <summary>An event construction domain for <c>host</c>'s current document.</summary>
    /// <remarks>Raises NotSupportedException when the native library lacks the "event-construction" functions.</remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : EventService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "event-construction") then
            raise (NotSupportedException "The native library has no event construction functions")

        let session = host.Session
        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof EventService))

            host.EnsureActive ()

        let native (target: EventTarget) =
            match box target with
            | null -> struct (0n, false)
            | :? NativeObject as value when obj.ReferenceEquals (value.Session, session) ->
                if value.Kind = Kind.EventTarget then
                    struct (value.Handle, false)
                else
                    struct (Native.rv_node_as_event_target value.Handle, true)
            | _ -> raise (ArgumentException "The related target belongs to another provider")

        { new EventService with
            member _.Create(eventType, construction) =
                check ()
                let construction = EventContract.construction eventType construction
                let allocated = Collections.Generic.List<nativeint>()

                let utf8 (text: string) =
                    let pointer = Marshal.StringToCoTaskMemUTF8 text
                    allocated.Add pointer
                    pointer

                let mutable init = Native.EventInit ()
                init.size <- Marshal.SizeOf<Native.EventInit>()
                let mutable related = struct (0n, false)

                let options (value: EventOptions) =
                    init.bubbles <- (if value.Bubbles then 1 else 0)
                    init.cancelable <- (if value.Cancelable then 1 else 0)
                    init.composed <- (if value.Composed then 1 else 0)

                let kind =
                    match construction with
                    | EventConstruction.Event value ->
                        options value
                        0
                    | EventConstruction.Custom (value, detail) ->
                        options value
                        init.customDetailJson <- utf8 (write detail)
                        1
                    | EventConstruction.UI (value, detail) ->
                        options value
                        init.detail <- detail
                        2
                    | EventConstruction.Mouse (value, fields) ->
                        options value
                        init.detail <- fields.Detail
                        init.screenX <- fields.ScreenX
                        init.screenY <- fields.ScreenY
                        init.clientX <- fields.ClientX
                        init.clientY <- fields.ClientY
                        init.button <- fields.Button
                        init.buttons <- fields.Buttons
                        init.modifiers <- modifiers fields.Modifiers
                        related <- native fields.RelatedTarget
                        3
                    | EventConstruction.Keyboard (value, fields) ->
                        options value
                        init.detail <- fields.Detail
                        init.key <- utf8 fields.Key
                        init.code <- utf8 fields.Code
                        init.location <- fields.Location
                        init.repeat <- (if fields.Repeat then 1 else 0)
                        init.isComposing <- (if fields.IsComposing then 1 else 0)
                        init.modifiers <- modifiers fields.Modifiers
                        4
                    | EventConstruction.Focus (value, detail, target) ->
                        options value
                        init.detail <- detail
                        related <- native target
                        5

                let struct (relatedHandle, owned) = related
                init.relatedTarget <- relatedHandle

                try
                    let handle = Native.rv_host_construct_event (session.Host, kind, Native.utf8 eventType, Native.utf8Length eventType, &init)

                    if handle = 0n then
                        raise (ArgumentException $"The engine rejected the {eventType} event construction")

                    unbox<Event>(session.Event handle)
                finally
                    if owned then
                        Native.rv_event_target_release relatedHandle

                    for pointer in allocated do
                        Marshal.FreeCoTaskMem pointer

            member _.Detail event =
                check ()

                match box event with
                | null -> nullArg "event"
                | :? NativeObject as value when obj.ReferenceEquals (value.Session, session) ->
                    if Native.rv_event_interface value.Handle <> 8 then
                        None
                    else
                        let length = [| 0 |]
                        let status = Native.rv_custom_event_detail_json (value.Handle, null, 0, length)

                        if status = -9 then
                            raise (InvalidOperationException "The CustomEvent detail belongs to a replaced document")

                        let buffer = Array.zeroCreate<byte> length[0]

                        Native.rv_custom_event_detail_json (value.Handle, buffer, buffer.Length, length)
                        |> Interop.check "CustomEvent.detail"

                        use document = JsonDocument.Parse (ReadOnlyMemory<byte>(buffer, 0, length[0]))
                        Some (read document.RootElement)
                | _ -> raise (ArgumentException "The event belongs to another provider")

            member _.CreateTarget() =
                check ()
                let handle = Native.rv_host_create_event_target session.Host

                if handle = 0n then
                    raise (InvalidOperationException "rv_host_create_event_target returned NULL")

                unbox<EventTarget>(session.Target handle)

            member _.Dispose() =
                disposed.Value <- true
        }
