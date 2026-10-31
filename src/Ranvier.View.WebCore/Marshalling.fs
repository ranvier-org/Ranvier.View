namespace Ranvier.View.WebCore

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open RanvierWebCoreAbi

/// <summary>Keeps pinned container arrays and UTF-8 buffers alive until the native call returns.</summary>
[<Sealed>]
type internal InputScope() =
    let pins = List<GCHandle>()
    let strings = List<nativeint>()

    member _.Pin<'T when 'T: struct>(values: 'T array) =
        if isNull values || values.Length = 0 then
            0n
        else
            let pin = GCHandle.Alloc (values, GCHandleType.Pinned)
            pins.Add pin
            pin.AddrOfPinnedObject ()

    member _.Text(value: string) =
        if isNull value then
            0n
        else
            let pointer = Marshal.StringToCoTaskMemUTF8 value
            strings.Add pointer
            pointer

    /// <summary>Encodes a WebIDL USVString, retaining embedded NUL bytes.</summary>
    member _.Bytes(value: string) =
        Text.Encoding.UTF8.GetBytes (if isNull value then "null" else value)

    /// <summary>Pins a length-delimited string for the duration of this scope.</summary>
    member this.TextSpan(value: string) =
        let bytes = this.Bytes value
        let mutable span = Unchecked.defaultof<NativeDom.RvDomUtf8StringSpan>
        span.data <- this.Pin (if bytes.Length = 0 then [| 0uy |] else bytes)
        span.length <- bytes.Length
        span

    /// <summary>Pins a length-delimited string with its presence; <c>None</c> is absent.</summary>
    member this.TextInput(value: string option) =
        let mutable input = Unchecked.defaultof<NativeDom.RvDomUtf8StringInput>

        match value with
        | Some text ->
            let span = this.TextSpan text
            input.present <- 1
            input.data <- span.data
            input.length <- span.length
        | None -> ()

        input

    interface IDisposable with
        member _.Dispose() =
            for pin in pins do
                pin.Free ()

            for pointer in strings do
                Marshal.FreeCoTaskMem pointer

            pins.Clear ()
            strings.Clear ()
