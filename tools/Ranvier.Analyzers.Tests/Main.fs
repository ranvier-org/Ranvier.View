module Ranvier.Analyzers.Tests.Main

open Expecto
open System
open System.IO
open System.Security.Cryptography
open System.Text.Json

let private recordCompilerProvenance (argv: string array) =
    let expected =
        Environment.GetEnvironmentVariable "RANVIER_ANALYZER_TEST_COMPILER_SHA256"

    let directory =
        Environment.GetEnvironmentVariable "RANVIER_ANALYZER_TEST_PROVENANCE_DIR"

    if
        not (
            String.IsNullOrWhiteSpace expected
            && String.IsNullOrWhiteSpace directory
        )
    then
        if
            String.IsNullOrWhiteSpace expected
            || String.IsNullOrWhiteSpace directory
        then
            invalidOp "Provider compiler qualification requires both the expected hash and provenance directory."

        let assembly = typeof<FSharp.Compiler.CodeAnalysis.FSharpChecker>.Assembly
        let identity = assembly.GetName ()

        let hash =
            SHA256.HashData (File.ReadAllBytes assembly.Location)
            |> Convert.ToHexString
            |> _.ToLowerInvariant()

        let token =
            identity.GetPublicKeyToken ()
            |> Option.ofObj
            |> Option.defaultValue Array.empty

        let matches = String.Equals (hash, expected, StringComparison.OrdinalIgnoreCase)

        let receipt =
            {|
                compilerPath = assembly.Location
                compilerVersion = string identity.Version
                compilerPublicKeyToken = Convert.ToHexString(token).ToLowerInvariant()
                compilerSha256 = hash
                expectedCompilerSha256 = expected
                matches = matches
                gcTotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
                gcIsServer = Runtime.GCSettings.IsServerGC
                gcHeapHardLimitEnvironment = Environment.GetEnvironmentVariable "DOTNET_GCHeapHardLimit"
                arguments = argv
            |}

        Directory.CreateDirectory directory |> ignore
        File.WriteAllText (Path.Combine (directory, $"provider-{Environment.ProcessId}.json"), JsonSerializer.Serialize receipt)

        if not matches then
            invalidOp "The provider process loaded a compiler that differs from the qualification hash."

[<EntryPoint>]
let main argv =
    recordCompilerProvenance argv
    runTestsInAssemblyWithCLIArgs [] argv
