module internal NativeApiConformance.CryptoChecks

open System
open System.Text.RegularExpressions
open Ranvier.Dom

// The getRandomValues outcome of each kind for 0, 16, 65536 and 65544 bytes: None fills, Some names the exception.
// Chromium 156 on http://localhost, Ranvier.WebCore tests/fixtures/crypto-random-oracle.json.
let private integerKinds =
    [ RandomValuesKind.Int8; RandomValuesKind.Uint8; RandomValuesKind.Uint8Clamped; RandomValuesKind.Int16
      RandomValuesKind.Uint16; RandomValuesKind.Int32; RandomValuesKind.Uint32; RandomValuesKind.BigInt64
      RandomValuesKind.BigUint64 ]

let private mismatchedKinds =
    [ RandomValuesKind.Float16; RandomValuesKind.Float32; RandomValuesKind.Float64; RandomValuesKind.DataView ]

let private lengths = [ 0; 16; 65536; 65544 ]

// SHA digests of "", "abc" and 1,000,000 repetitions of "a": FIPS 180 example vectors.
let private vectors =
    [ "SHA-1",
      [ "da39a3ee5e6b4b0d3255bfef95601890afd80709"
        "a9993e364706816aba3e25717850c26c9cd0d89d"
        "34aa973cd4c4daa4f61eeb2bdbad27316534016f" ]
      "SHA-256",
      [ "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        "cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0" ]
      "SHA-384",
      [ "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b"
        "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7"
        "9d0e1809716474cb086e834e310a4a1ced149e9c00f248527972cec5704c2a5b07b8b3dc38ecc4ebae97ddd87f3d8985" ]
      "SHA-512",
      [ "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e"
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f"
        "e718483d0ce769644e2e42c7bc15b4638e1f98b13b2044285632a803afa973ebde0ff244877ea60a4cb0432ce577c31beb009c5c2c49aa2e4eadb217ad8cc09b" ] ]

let private uuid =
    Regex "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"

let private hex (bytes: byte array) =
    bytes |> Array.map (fun b -> (int b).ToString "x2") |> String.concat ""

let private ascii (text: string) =
    text.ToCharArray () |> Array.map byte

let private zero (bytes: byte array) = bytes |> Array.forall (fun b -> b = 0uy)

/// <summary>
/// Checks getRandomValues, randomUUID, SHA digests and SubtleCrypto availability on one provider by properties and
/// published vectors; the result is the assertion count. Random values are checked by length, kind, format,
/// version and variant bits and inequality of two calls, never by value.
/// </summary>
let run (create: unit -> CryptoService) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let truth label condition = equal label true condition

        let raises label (action: unit -> unit) =
            checks <- checks + 1

            let name =
                try
                    action ()
                    None
                with
                | CryptoException (name, _) -> Some name
                | :? ArgumentNullException -> Some "ArgumentNullException"
                | :? ArgumentOutOfRangeException -> Some "ArgumentOutOfRangeException"
                | :? ObjectDisposedException -> Some "ObjectDisposedException"
                | error -> Some error.Message

            match name with
            | None -> failwith $"{label}: expected an exception"
            | Some name -> name

        let service = create ()

        // Kinds, quota and the empty view.
        for kind in integerKinds do
            for length in lengths do
                let bytes = Array.zeroCreate<byte> length

                if length > 65536 then
                    equal $"{kind} {length}" "QuotaExceededError" (raises $"{kind} {length}" (fun () ->
                        service.GetRandomValues (kind, bytes, 0, length)))

                    truth $"{kind} {length} unchanged" (zero bytes)
                else
                    service.GetRandomValues (kind, bytes, 0, length)
                    truth $"{kind} {length} filled" (length < 16 || not (zero bytes))

        for kind in mismatchedKinds do
            for length in lengths do
                let bytes = Array.zeroCreate<byte> length

                equal $"{kind} {length}" "TypeMismatchError" (raises $"{kind} {length}" (fun () ->
                    service.GetRandomValues (kind, bytes, 0, length)))

                truth $"{kind} {length} unchanged" (zero bytes)

        let words = Array.zeroCreate<byte> (16385 * 4)
        service.GetRandomValues (RandomValuesKind.Uint32, words, 0, 16384 * 4)
        truth "Uint32Array(16384) filled" (not (zero words))

        equal "Uint32Array(16385)" "QuotaExceededError" (raises "Uint32Array(16385)" (fun () ->
            service.GetRandomValues (RandomValuesKind.Uint32, words, 0, 16385 * 4)))

        // A range writes only its bytes; two fills differ.
        let framed = Array.zeroCreate<byte> 32
        service.GetRandomValues (RandomValuesKind.Uint8, framed, 8, 16)
        truth "bytes before the range untouched" (zero framed[0..7])
        truth "bytes after the range untouched" (zero framed[24..])
        truth "range filled" (not (zero framed[8..23]))
        let first = Array.zeroCreate<byte> 16
        let second = Array.zeroCreate<byte> 16
        service.GetRandomValues (RandomValuesKind.Uint8, first, 0, 16)
        service.GetRandomValues (RandomValuesKind.Uint8, second, 0, 16)
        truth "two fills differ" (first <> second)

        // Argument errors.
        let small = Array.zeroCreate<byte> 8
        equal "odd Int16 byte length" "RangeError" (raises "odd Int16" (fun () ->
            service.GetRandomValues (RandomValuesKind.Int16, small, 0, 3)))

        equal "null bytes" "ArgumentNullException" (raises "null bytes" (fun () ->
            service.GetRandomValues (RandomValuesKind.Uint8, null, 0, 0)))

        equal "range past the end" "ArgumentOutOfRangeException" (raises "range past the end" (fun () ->
            service.GetRandomValues (RandomValuesKind.Uint8, small, 4, 8)))

        equal "negative offset" "ArgumentOutOfRangeException" (raises "negative offset" (fun () ->
            service.GetRandomValues (RandomValuesKind.Uint8, small, -1, 1)))

        equal "unknown kind" "ArgumentOutOfRangeException" (raises "unknown kind" (fun () ->
            service.GetRandomValues (enum<RandomValuesKind> 13, small, 0, 8)))

        // randomUUID.
        let values =
            List.init 64 (fun _ ->
                match service.RandomUUID () with
                | CryptoOutcome.Value value -> value
                | CryptoOutcome.Unsupported reason -> failwith $"randomUUID unsupported: {reason}")

        truth "randomUUID length 36" (values |> List.forall (fun value -> value.Length = 36))
        truth "randomUUID v4 format, version and variant" (values |> List.forall uuid.IsMatch)
        equal "randomUUID distinct" 64 (List.distinct values).Length

        // Digest vectors, names, ranges and unsupported algorithms.
        let million = Array.create 1000000 (byte 'a')

        let digest (name: string) (data: byte array) offset count =
            async {
                match! service.Digest (name, data, offset, count) with
                | CryptoOutcome.Value bytes -> return hex bytes
                | CryptoOutcome.Unsupported reason -> return failwith $"{name} unsupported: {reason}"
            }

        for name, expected in vectors do
            let! empty = digest name [||] 0 0
            equal $"{name} empty" expected[0] empty
            let! abc = digest name (ascii "abc") 0 3
            equal $"{name} abc" expected[1] abc
            let! longer = digest name million 0 million.Length
            equal $"{name} million a" expected[2] longer
            let! framedAbc = digest (name.ToLowerInvariant ()) (ascii "--abc--") 2 3
            equal $"{name} lowercase name over an offset range" expected[1] framedAbc

        let! mixed = digest "Sha-256" (ascii "abc") 0 3
        let sha256Abc = (snd vectors[1]).[1]
        equal "mixed-case SHA-256" sha256Abc mixed

        for name in [ "MD5"; "SHA256"; "AES-GCM"; "SHA-224" ] do
            let! outcome = service.Digest (name, ascii "abc", 0, 3)

            truth $"{name} digest is Unsupported" (
                match outcome with
                | CryptoOutcome.Unsupported _ -> true
                | CryptoOutcome.Value _ -> false
            )

        equal "digest range past the end" "ArgumentOutOfRangeException" (raises "digest range" (fun () ->
            service.Digest ("SHA-256", small, 6, 4) |> ignore))

        // Availability.
        equal "digest SHA-384 available" (CryptoOutcome.Value ()) (service.Availability (SubtleOperation.Digest, "sha-384"))

        for operation, algorithm in
            [ SubtleOperation.Encrypt, "AES-GCM"
              SubtleOperation.Sign, "HMAC"
              SubtleOperation.GenerateKey, "ECDSA"
              SubtleOperation.ImportKey, "RSA-OAEP"
              SubtleOperation.ExportKey, "Ed25519"
              SubtleOperation.DeriveBits, "PBKDF2"
              SubtleOperation.Digest, "MD5" ] do
            truth $"{CryptoContract.operationName operation} {algorithm} Unsupported" (
                match service.Availability (operation, algorithm) with
                | CryptoOutcome.Unsupported _ -> true
                | CryptoOutcome.Value _ -> false
            )

        // Disposal.
        service.Dispose ()

        ignore (raises "getRandomValues after Dispose" (fun () ->
            service.GetRandomValues (RandomValuesKind.Uint8, small, 0, 8)))

        ignore (raises "randomUUID after Dispose" (fun () ->
            service.RandomUUID () |> ignore))

        return checks
    }

/// <summary>
/// Checks a browser provider in an insecure context: randomUUID and SubtleCrypto read as Unsupported, and
/// getRandomValues still fills; the result is the assertion count.
/// </summary>
let insecure (create: unit -> CryptoService) =
    async {
        let mutable checks = 0

        let truth label condition =
            checks <- checks + 1

            if not condition then
                failwith $"{label}: expected true"

        use service = create ()
        let unsupported outcome =
            match outcome with
            | CryptoOutcome.Unsupported _ -> true
            | CryptoOutcome.Value _ -> false

        truth "randomUUID Unsupported" (unsupported (service.RandomUUID ()))
        let! digest = service.Digest ("SHA-256", [| 1uy |], 0, 1)
        truth "digest Unsupported" (unsupported digest)
        truth "digest availability Unsupported" (unsupported (service.Availability (SubtleOperation.Digest, "SHA-256")))
        let bytes = Array.zeroCreate<byte> 16
        service.GetRandomValues (RandomValuesKind.Uint8, bytes, 0, 16)
        truth "getRandomValues fills" (bytes |> Array.exists (fun b -> b <> 0uy))
        return checks
    }
