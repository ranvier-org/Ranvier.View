namespace Ranvier.View.WebCore

open System
open System.Security.Cryptography
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>crypto.getRandomValues and crypto.randomUUID of a native host, with SHA digests from .NET.</summary>
module Crypto =
    let private hash (algorithm: DigestAlgorithm) (data: byte array) =
        match algorithm with
        | DigestAlgorithm.Sha1 -> SHA1.HashData data
        | DigestAlgorithm.Sha256 -> SHA256.HashData data
        | DigestAlgorithm.Sha384 -> SHA384.HashData data
        | DigestAlgorithm.Sha512 -> SHA512.HashData data

    /// <summary>A crypto service for the document <c>host</c> shows at each call.</summary>
    /// <remarks>
    /// Random values and UUIDs come from the engine's crypto object, and digests from System.Security.Cryptography.
    /// The native host exposes no page script, so both stay available without a secure context. Raises
    /// NotSupportedException when the native library lacks the crypto functions.
    /// </remarks>
    /// <param name="host">The live host whose creating thread runs every operation.</param>
    let forHost (host: WebCoreHost) : CryptoService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-crypto-random") then
            raise (NotSupportedException "The native library has no crypto functions")

        let disposed = ref false

        let check () =
            if disposed.Value then
                raise (ObjectDisposedException (nameof CryptoService))

            host.EnsureActive ()

        { new CryptoService with
            member _.GetRandomValues(kind, bytes, offset, count) =
                check ()
                CryptoContract.randomValues kind bytes offset count
                let whole = offset = 0 && count = bytes.Length
                let buffer = if whole then bytes else Array.sub bytes offset count

                try
                    NativeDom.rv_dom_crypto_get_random_values (Interop.handle (box host.Document), int kind, buffer, count)
                    |> Interop.check "crypto.getRandomValues"
                with WebCoreDomException (name, detail) ->
                    raise (CryptoException (name, detail))

                if not whole then
                    Array.blit buffer 0 bytes offset count

            member _.RandomUUID() =
                check ()
                let buffer = Array.zeroCreate<byte> 64
                let length = [| 0 |]

                NativeDom.rv_dom_crypto_random_uuid (Interop.handle (box host.Document), buffer, buffer.Length, length)
                |> Interop.check "crypto.randomUUID"

                CryptoOutcome.Value (Encoding.ASCII.GetString (buffer, 0, length[0]))

            member _.Digest(algorithm, data, offset, count) =
                check ()
                CryptoContract.range data offset count

                match CryptoContract.digestAlgorithm algorithm with
                | None -> async { return CryptoContract.unsupportedDigest algorithm }
                | Some known ->
                    let digest = hash known (Array.sub data offset count)
                    async { return CryptoOutcome.Value digest }

            member _.Availability(operation, algorithm) =
                check ()
                CryptoContract.availability operation algorithm

            member _.Dispose() =
                disposed.Value <- true
        }
