# Ranvier analyzer host

This package isolates Ranvier's compiler expression-conversion fix with the pinned FSharp.Analyzers.Cli 0.41.0 host.
The compiler retains lowered byref-to-nativeint IL conversions as typed IL-assembly expressions so analysis keeps the surrounding calls and diagnostics.
It does not skip files or analyzer rules.

The package uses its own ID and records vendor source, patch and payload hashes. The current experimental compiler is unsigned;
its assembly version does not make it an official Microsoft compiler distribution. Upstream MIT licenses and notices ship with the payload.

Consumers use the supported `FSharpAnalyzersExe` MSBuild property through the package's build assets.
The host checks the compiler actually loaded by its process before invoking the upstream CLI. A mismatch fails the run.
Set `RANVIER_ANALYZER_HOST_PROVENANCE_DIR` to write one JSON receipt per process, without changing diagnostic stdout.

The host uses a 4 GiB managed heap limit. The MSBuild integration writes its runtime configuration into the consumer's
`obj` directory and invokes it through `dotnet exec --runtimeconfig`. Set `RanvierAnalyzerHeapHardLimitBytes` to a positive
decimal byte count, for example `-p:RanvierAnalyzerHeapHardLimitBytes=6442450944`, to choose another limit for that build.
The limit covers the managed heap; the process also uses native memory. Direct invocation uses the packaged 4 GiB default.
The optional provenance receipt records the runtime's actual available GC memory and GC mode.
