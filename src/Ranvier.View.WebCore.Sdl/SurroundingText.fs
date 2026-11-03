namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier.View.WebCore
open RanvierWebCoreAbi

module internal SurroundingText =
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type private Send = delegate of nativeint * nativeint * uint32 * uint32 * uint64 -> byte

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type private EventType = delegate of unit -> uint32

    let private sender =
        lazy
            let mutable library = 0n

            if
                OperatingSystem.IsLinux ()
                && NativeLibrary.TryLoad ("SDL3", typeof<SDL_Window>.Assembly, Nullable (), &library)
            then
                let mutable symbol = 0n
                let mutable eventSymbol = 0n

                if
                    NativeLibrary.TryGetExport (library, "SDL_Ranvier_SetIbusSurrounding", &symbol)
                    && NativeLibrary.TryGetExport (library, "SDL_Ranvier_GetIbusDeleteEventType", &eventSymbol)
                then
                    let getEventType = Marshal.GetDelegateForFunctionPointer<EventType> eventSymbol
                    let eventType = getEventType.Invoke ()

                    if eventType = 0u then
                        NativeLibrary.Free library
                        None
                    else
                        Some (Marshal.GetDelegateForFunctionPointer<Send> symbol, eventType)
                else
                    NativeLibrary.Free library
                    None
            else
                None

    let scalarOffset (text: string) (offset: int) =
        if
            isNull text
            || offset < 0
            || offset > text.Length
            || text.Contains '\000'
        then
            None
        else
            let mutable index = 0
            let mutable count = 0u
            let mutable result = if offset = 0 then Some 0u else None
            let mutable valid = true

            while index < text.Length && valid do
                let mutable rune = Unchecked.defaultof<Rune>

                if Rune.TryGetRuneAt (text, index, &rune) then
                    index <- index + rune.Utf16SequenceLength
                    count <- count + 1u

                    if index = offset then
                        result <- Some count
                else
                    valid <- false

            if valid then result else None

    let utf16Offset (text: string) (offset: uint32) =
        let mutable index = 0
        let mutable count = 0u
        let mutable valid = true
        let mutable result = if offset = 0u then Some 0 else None

        while index < text.Length && valid do
            let mutable rune = Unchecked.defaultof<Rune>

            if Rune.TryGetRuneAt (text, index, &rune) then
                index <- index + rune.Utf16SequenceLength
                count <- count + 1u

                if count = offset then
                    result <- Some index
            else
                valid <- false

        if valid then result else None

    let deleteRange text (cursor: uint32) (delta: int) (count: uint32) =
        let first = int64 cursor + int64 delta
        let last = first + int64 count

        if first < 0L || last > int64 UInt32.MaxValue then
            None
        else
            match utf16Offset text (uint32 first), utf16Offset text (uint32 last) with
            | Some start, Some finish -> Some (start, finish)
            | _ -> None

    type Bridge(window: nativeint, host: WebCoreHost) =
        let send = sender.Value
        let mutable available = false
        let mutable snapshot: (string * uint32 * uint64) option = None

        member _.Available = available
        member _.EventType = send |> Option.map snd |> Option.defaultValue 0u

        member _.Delete(token: uint64, offset: int, count: uint32) =
            let previous = snapshot
            snapshot <- None

            match previous with
            | Some (text, cursor, currentToken) when token = currentToken ->
                match deleteRange text cursor offset count with
                | Some (start, finish) ->
                    let result =
                        Native.rv_host_ime_delete_surrounding (host.Handle, token, start, finish)

                    if count = 0u && result = 0 then 1 else result
                | None -> -9
            | _ -> -10

        member _.Refresh() =
            match send with
            | None ->
                available <- false
                snapshot <- None
            | Some (transmit, _) ->
                if
                    NativePtr.toNativeInt (SDL3.SDL_GetKeyboardFocus ())
                    <> window
                then
                    available <- false
                    snapshot <- None
                elif not (WebCoreHost.Has "ime-surrounding") then
                    transmit.Invoke (window, 0n, 0u, 0u, 0UL)
                    |> ignore

                    available <- false
                    snapshot <- None
                else
                    let length = [| 0 |]
                    let mutable cursor, anchor, state = 0, 0, 0
                    let mutable token = 0UL
                    let mutable bytes = Array.zeroCreate<byte> 1024

                    let call () =
                        Native.rv_host_ime_surrounding (host.Handle, bytes, bytes.Length, length, &cursor, &anchor, &state, &token)

                    let mutable status = call ()

                    if status = -6 && length[0] > bytes.Length then
                        bytes <- Array.zeroCreate<byte> length[0]
                        status <- call ()

                    if
                        status = 0
                        && state = 1
                        && length[0] >= 0
                        && length[0] <= bytes.Length
                    then
                        let text = Encoding.UTF8.GetString (bytes, 0, length[0])

                        match scalarOffset text cursor, scalarOffset text anchor with
                        | Some caret, Some origin ->
                            let encoded = Encoding.UTF8.GetBytes (text + "\000")
                            use pointer = fixed encoded

                            available <-
                                transmit.Invoke (window, NativePtr.toNativeInt pointer, caret, origin, token)
                                <> 0uy

                            snapshot <- if available then Some (text, caret, token) else None
                        | _ ->
                            transmit.Invoke (window, 0n, 0u, 0u, 0UL)
                            |> ignore

                            available <- false
                            snapshot <- None
                    else
                        transmit.Invoke (window, 0n, 0u, 0u, 0UL)
                        |> ignore

                        available <- false
                        snapshot <- None

        member _.Clear() =
            match send with
            | Some (transmit, _) when NativePtr.toNativeInt (SDL3.SDL_GetKeyboardFocus ()) = window ->
                transmit.Invoke (window, 0n, 0u, 0u, 0UL)
                |> ignore
            | _ -> ()

            available <- false
            snapshot <- None
