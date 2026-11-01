using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FSharp.Compiler.CodeAnalysis;

namespace Ranvier.Analyzers.Host;

internal static class Bootstrap
{
    private static int Main(string[] args)
    {
        try
        {
            VerifyCompiler();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Ranvier analyzer host: {error.Message}");
            return 78;
        }

        return global::Program.main(args);
    }

    private static void VerifyCompiler()
    {
        var hostAssembly = Assembly.GetExecutingAssembly();
        using var resource = hostAssembly.GetManifestResourceStream("Ranvier.Analyzers.Host.VendorManifest.json")
            ?? throw new InvalidOperationException("The embedded vendor manifest is missing.");
        using var document = JsonDocument.Parse(resource);
        var expected = document.RootElement;
        var compiler = typeof(FSharpChecker).Assembly;
        var name = compiler.GetName();
        using var input = File.OpenRead(compiler.Location);
        var hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        var token = Convert.ToHexString(name.GetPublicKeyToken() ?? []).ToLowerInvariant();
        var matched = hash == expected.GetProperty("compilerSha256").GetString()
            && name.Name == "FSharp.Compiler.Service"
            && name.Version?.ToString() == "43.12.402.0"
            && token == expected.GetProperty("compilerPublicKeyToken").GetString();

        var directory = Environment.GetEnvironmentVariable("RANVIER_ANALYZER_HOST_PROVENANCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            var receipt = new
            {
                processId = Environment.ProcessId,
                hostAssemblyPath = hostAssembly.Location,
                hostAssemblyVersion = hostAssembly.GetName().Version?.ToString(),
                hostAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(hostAssembly.Location))).ToLowerInvariant(),
                compilerAssemblyPath = compiler.Location,
                compilerVersion = name.Version?.ToString(),
                compilerInformationalVersion = compiler.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                compilerPublicKeyToken = token,
                compilerSha256 = hash,
                compilerSourceCommit = expected.GetProperty("compilerSourceCommit").GetString(),
                sourceGuardSha256 = expected.GetProperty("sourceGuardSha256").GetString(),
                cliSourceCommit = expected.GetProperty("cliSourceCommit").GetString(),
                gcTotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                gcIsServer = System.Runtime.GCSettings.IsServerGC,
                gcHeapHardLimitEnvironment = Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"),
                matched
            };
            File.WriteAllText(Path.Combine(directory, $"host-{Environment.ProcessId}.json"),
                JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!matched)
            throw new InvalidOperationException("The loaded compiler does not match this package's pinned vendor manifest.");
    }
}
