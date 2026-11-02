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
    type private Send = delegate of nativeint * nativeint * uint32 * uint32 -> byte

    let private sender =
        lazy
            let mutable library = 0n

            if
                OperatingSystem.IsLinux ()
                && NativeLibrary.TryLoad ("SDL3", typeof<SDL_Window>.Assembly, Nullable (), &library)
            then
                let mutable symbol = 0n

                if NativeLibrary.TryGetExport (library, "SDL_Ranvier_SetIbusSurrounding", &symbol) then
                    Some (Marshal.GetDelegateForFunctionPointer<Send> symbol)
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

    type Bridge(window: nativeint, host: WebCoreHost) =
        let send = sender.Value
        let mutable available = false

        member _.Available = available

        member _.Refresh() =
            match send with
            | None -> available <- false
            | Some transmit ->
                if
                    NativePtr.toNativeInt (SDL3.SDL_GetKeyboardFocus ())
                    <> window
                then
                    available <- false
                elif not (WebCoreHost.Has "ime-surrounding") then
                    transmit.Invoke (window, 0n, 0u, 0u) |> ignore
                    available <- false
                else
                    let length = [| 0 |]
                    let mutable cursor, anchor, state = 0, 0, 0
                    let mutable bytes = Array.zeroCreate<byte> 1024

                    let call () =
                        Native.rv_host_ime_surrounding (host.Handle, bytes, bytes.Length, length, &cursor, &anchor, &state)

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
                                transmit.Invoke (window, NativePtr.toNativeInt pointer, caret, origin)
                                <> 0uy
                        | _ -> available <- transmit.Invoke (window, 0n, 0u, 0u) <> 0uy
                    else
                        available <- transmit.Invoke (window, 0n, 0u, 0u) <> 0uy

        member _.Clear() =
            match send with
            | Some transmit when NativePtr.toNativeInt (SDL3.SDL_GetKeyboardFocus ()) = window -> transmit.Invoke (window, 0n, 0u, 0u) |> ignore
            | _ -> ()

            available <- false
