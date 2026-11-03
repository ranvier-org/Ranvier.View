# Public View46 Mac consumer

This consumer builds only three existing lifecycle fixture files. It restores the
normal or traced public View46 package family, Core DOM58/runtime preview1,
Interop preview11 and the matching public Native preview11 Slim package into an
owned NuGet.org cache. It does not reference provider source projects.
FSharp.Core may come from NuGet.org or the exact SDK403 library-packs directory.
The SDK variant must match the installed SDK package and every DLL/resource member
of the pinned public package. Ranvier packages still require NuGet.org origins.
The corrective job chooses the public framework variant, because the retained Mac
SDK DLL/resource bytes differ from that package. It overlays only those framework
files in an owned copy; the original outputs, consumer and Ranvier DLLs remain
unchanged. The original mismatch remains a recorded admission failure.

The immutable input file records all eight audited View package hashes, both DOM
hashes, Interop and the native package closure. Every route verifies its actual
assets graph and restored bytes. The Mac ARM64 native outputs must match the four
public Slim dylibs and ABI63. Mixed modes, source projects, stale dependencies,
GPU packages and unknown native packages fail admission.

Both completed JIT builds are reused from the pinned first artifact with all
original bytes checked; their restore-only checks do not recompile them. Only the
two missing AOT publishes compile the consumer. Normal and traced each run the
complete common lifecycle fixture under JIT and
strict NativeAOT: seven host, 35 window and 49 common checks. AOT must generate
native code with no IL diagnostics and report dynamic code disabled. Each route
records output hashes before and after execution and preserves its failure
independently. Commands run in bounded owned process groups with child cleanup.

The workflow is unarmed (`workflow_dispatch` only). Review the exact controller
commit and raw `inputs.json` SHA before one coordinated activation. The completed
5e028 source-built 849-case Mac suite is a separate proof; this controller does
not claim to repeat that suite through packages.
