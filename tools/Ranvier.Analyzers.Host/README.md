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
The default is a JSON number in `runtimeconfig.template.json`: the SDK's MSBuild option conversion
only recognizes 32-bit integers and would emit the 4 GiB value as a string.

To reproduce the compiler producer, install .NET SDK 10.0.403 and run
`python build_compiler.py --work <empty-work-directory>` from this directory. The producer fetches the
754 Git blobs in `compiler-sources.json` (24,333,291 bytes), verifies every original blob, and applies
only the reviewed guard. It uses an isolated source/output directory and never replaces an SDK or
global tool assembly. `--prepare-only` verifies and patches the source without invoking .NET.
Restore uses a work-local NuGet.org configuration, package cache and HTTP cache, so the upstream
VMR source mapping and the developer's cache do not select the producer's dependencies.

The producer records its source manifest, patch, command, SDK and output hash. A fresh output that
differs from `vendor-spec.json` fails closed and must be reviewed before packaging. The qualification
pin identifies the Linux CI build; compiler source-path literals and embedded debug data make outputs
from different checkout roots byte-distinct even with the same source and SDK. A matching assembly
identity alone does not authorize a different payload.
The compiler producer disables inherited SourceLink generation: its exported sources are not files
in the containing Ranvier.View revision. Otherwise that unrelated revision enters the embedded PDB
and changes the DLL on every host-source commit. The actual compiler source and guard remain pinned
in the vendor manifest and source-build receipt; embedded source/debug data is retained.
The workflow retains that output and its diagnostics, then packs and tests the host on Linux and Windows
only when the compiler identity matches. These startup/package checks do not replace the full
analyzer suite, external-consumer tests or documented `build.fsx analyze` acceptance.
