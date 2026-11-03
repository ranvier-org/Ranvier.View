# Public View46 Mac consumer

This consumer builds only three existing lifecycle fixture files. It restores the
normal or traced public View46 package family, Core DOM58/runtime preview1,
Interop preview11 and the matching public Native preview11 Slim package into an
owned NuGet.org cache. It does not reference provider source projects.

The immutable input file records all eight audited View package hashes, both DOM
hashes, Interop and the native package closure. Every route verifies its actual
assets graph and restored bytes. The Mac ARM64 native outputs must match the four
public Slim dylibs and ABI63. Mixed modes, source projects, stale dependencies,
GPU packages and unknown native packages fail admission.

Normal and traced each run the complete common lifecycle fixture under JIT and
strict NativeAOT: seven host, 35 window and 49 common checks. AOT must generate
native code with no IL diagnostics and report dynamic code disabled. Each route
records output hashes before and after execution and preserves its failure
independently. Commands run in bounded owned process groups with child cleanup.

The workflow is unarmed (`workflow_dispatch` only). Review the exact controller
commit and raw `inputs.json` SHA before one coordinated activation. The completed
5e028 source-built 849-case Mac suite is a separate proof; this controller does
not claim to repeat that suite through packages.
