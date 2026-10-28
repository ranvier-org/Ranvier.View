namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices

/// <summary>Keeps pinned container arrays and UTF-8 buffers alive until the native call returns.</summary>
[<Sealed>]
type internal InputScope() =
    let pins = List<GCHandle>()
    let strings = List<nativeint>()

    member _.Pin<'T when 'T: struct>(values: 'T array) =
        if isNull values || values.Length = 0 then 0n
        else
            let pin = GCHandle.Alloc(values, GCHandleType.Pinned)
            pins.Add pin
            pin.AddrOfPinnedObject()

    member _.Text(value: string) =
        if isNull value then 0n
        else
            let pointer = Marshal.StringToCoTaskMemUTF8 value
            strings.Add pointer
            pointer

    interface IDisposable with
        member _.Dispose() =
            for pin in pins do pin.Free()
            for pointer in strings do Marshal.FreeCoTaskMem pointer
            pins.Clear()
            strings.Clear()
