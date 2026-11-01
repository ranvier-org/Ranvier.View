/// <summary>Reads of Blob and File bytes owned by the current reactive scope.</summary>
/// <remarks>
/// Each read gets a child scope of the current owner. The outcome is delivered under that scope inside a batch, and the
/// scope is disposed after the callback returns. Disposing the returned handle, or any ancestor scope, cancels a read
/// that has not delivered, and its callback is skipped.
/// </remarks>
module Ranvier.View.Dom.BlobReads

open System
open global.Ranvier.Dom

let private read (service: BinaryValueService) (blob: Blob) (map: byte array -> 'T) (receive: ResourceOutcome<'T> -> unit) =
    if isNull (box service) then
        nullArg (nameof service)

    if isNull (box receive) then
        nullArg (nameof receive)

    let outcome: ResourceOutcome<byte array> ref = ref Unchecked.defaultof<_>

    Frames.owned
        (fun run ->
            service.ReadBytes (
                blob,
                fun value ->
                    outcome.Value <- value
                    run ()
            ))
        (fun () ->
            match outcome.Value with
            | ResourceOutcome.Succeeded bytes -> receive (ResourceOutcome.Succeeded (map bytes))
            | ResourceOutcome.Failed failure -> receive (ResourceOutcome.Failed failure))

/// <summary>Delivers the bytes of <c>blob</c> to <c>receive</c>.</summary>
let bytes (service: BinaryValueService) (blob: Blob) (receive: ResourceOutcome<byte array> -> unit) : IDisposable =
    read service blob id receive

/// <summary>Delivers the text of <c>blob</c>, decoded as <c>Blob.text()</c> decodes it, to <c>receive</c>.</summary>
let text (service: BinaryValueService) (blob: Blob) (receive: ResourceOutcome<string> -> unit) : IDisposable =
    read service blob BinaryValueContract.text receive

/// <summary>Delivers the data URL of <c>blob</c>, as <c>FileReader.readAsDataURL</c> writes it, to <c>receive</c>.</summary>
let dataUrl (service: BinaryValueService) (blob: Blob) (receive: ResourceOutcome<string> -> unit) : IDisposable =
    if isNull (box blob) then
        nullArg (nameof blob)

    let blobType = blob.``type``
    read service blob (BinaryValueContract.dataUrl blobType) receive
