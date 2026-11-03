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

The repository's `build.fsx analyze` command resolves the restored host and its runtime configuration
through `SelectRanvierAnalyzerHost`, then invokes that host with separate process arguments. Host
receipts include the actual arguments and working directory so qualification can verify every
project in each group. The provider test executable copies the same compiler from the restored
Host package; its optional `RANVIER_ANALYZER_TEST_COMPILER_SHA256` and
`RANVIER_ANALYZER_TEST_PROVENANCE_DIR` settings verify and record the compiler loaded inside the
test process before running the unfiltered suite.

Repository qualification preserves the checked-in Cloudsmith mappings and uses exact private
package mappings in a fresh cache. The workflow privately packs the four DOM alpha.55 dependencies
from Core `54f004b3baa4136777998b65e0f9941d78151224`; those build receipts do not prove public
publication or replace the later public-package acceptance run.

The two `candidate-consumers` jobs both restore the Ubuntu-built package pair, so publication has
one artifact identity across Windows and Linux. The existing trusted `publish.yml` workflow accepts
only a successful same-repository main/master release qualification with all fourteen jobs present
and successful. Before obtaining NuGet credentials it verifies that both candidate receipts describe
the actual package bytes, exact compiler manifest and all four consumer scenarios. Host is published
before Analyzers; an unexpected existing version fails instead of silently skipping an upload.

Public acceptance uses the same expected pair as comparison input, with `test_consumer.py
--run-external --public-source --host-feed <expected-pair> --evidence-dir <outside-checkout>`.
The expected directory is absent from the restore sources. Each platform restores only from
NuGet.org into a fresh cache and checks the source graph, actual Host/compiler execution and all four
consumer scenarios. NuGet.org adds a repository signature, so acceptance compares every original ZIP
member and permits only the added `.signature.p7s`; official `dotnet nuget verify --all` must also
prove the pinned NuGet.org repository signer. A changed payload, extra archive member, duplicate
member, untrusted signature or private restore source fails acceptance. Public indexing and consumer
evidence are retained separately from the private qualification.
