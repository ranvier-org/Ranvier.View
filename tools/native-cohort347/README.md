# Final ABI63 public consumer arming

This lane prepares public package checks for `win-x64`, `linux-x64` and `linux-arm64`, each normal and traced.
Both x64 and ARM64 consumer jobs are statically disabled. `inputs/unarmed.json` records allocated Native P11 and the
proposed View46 version families, with provider allocation, managed source and actual runtime hashes incomplete.
Neither a dispatch nor an input value can enable a job. The pull-request job runs hermetic policy
tests only. No public or ARM consumer run has been dispatched or qualified by this preparation.

Before changing the disabled job, fill a reviewed input JSON with the actual public View/Native/provider versions,
managed/Native/provider/WebKit source pins and the selected RID's independently checked host and provider hashes. Preserve
Core preview.1, DOM alpha.58, CSS alpha.2 and CSS add-ons alpha.1. DOM alpha.58 includes the intersection
diagnostic fix from `5e95b1993b745cd9bc9211edd85be7a49f9d3e58` and version-only merge
`59c68218e1e4f6b6bbde1afb8f8502a3ee587abc`; public release and signed-package audit remain pending.
Public DOM alpha.57 hashes remain in `graph.json` as historical evidence.
The provider follows the matching Native version.
Keep the complete five-RID native aggregate dependency graph; this consumer matrix executes only the three named
RIDs and does not qualify Windows ARM64 or macOS by implication.

Native P11 source `8b8db298eba294876c27bd9a3200881f872684df` includes the reviewed view-transition closure fix.
Its Slim packages have been audited; the GPU producer and final consumer qualification remain pending.
Source equivalence does not authenticate a package or qualify a new Host binary. The wheel and clipboard fixtures retain reviewed
`541ca21f16b1d0399e9c6eec6b0a27e58915c1b4`; the link-drop fixture adopts platform-specific expectations from
`333c806ae4e5269e88be26d73778c01425de6b8d`. These native fixtures are not copied into View. Source adoption does not replace actual
matched producer/catalogue/IME receipts. Actual private P9 x64/ARM provider and managed qualification is retained in
`graph.json`; it is not relabeled as P11. Matching P11 qualification remains required for the disabled jobs.
Missing final public ARM provider hashes do not enable ARM or block the
independent x64 guard. An input JSON cannot substitute for passing producer/independent runtime receipts.

`run.py` restores only the public SDL package and, on Linux, the optional public provider into a new owned cache.
The isolated project links the existing complete accessibility consumer program and has no source project references
or direct higher Ranvier Core/interop override. Its IL error settings match the strict source fixture. The verifier
checks normal/traced identities, public dependency minimums, official cache origins, source claims, five-RID package
versions, complete native manifest assets, real file hashes and PE/ELF architecture. The full reviewed header hash
preserves all original 344 declarations and the exact surrounding/delete/style tail, not merely their names.

Source strings in nuspecs and manifests remain producer/caller claims. Comparing them to reviewed expected pins
does not authenticate build provenance. Independent producer receipts and the public publisher's artifact/signature
checks remain authoritative. The verifier does not infer provenance from a label or an ABI number.

The provider aggregate's schema2 `runtimes` records are selected by the actual RID. Global Native/source/capability
contracts and all recorded payload bytes are checked before comparing the selected module/Host hashes. This does
not treat an unselected RID as executed or upgrade a source claim to authenticated provenance.

Each armed job publishes and checks both JIT and strict NativeAOT outputs, verifies all 347 real exports in a separate
process and executes the actual consumer. Linux runs provider-present, module-absent and unavailable-bus controls on
an owned accessibility bus/registry, with three Windows, three direct Hosts and non-BMP style checks. It compares the
actual loaded native paths and unchanged binary hashes, uses no forced library search path, and reaps owned processes.
CI supplies its own Xvfb display; a local replay may use the already approved display without stopping it.

The Windows profile explicitly requires actual UIA attachment for its qualification Window and checks the same
Window/Host lifetimes. It does not infer UIA availability from a hidden/software Window generally, and does not use
the Linux provider expectation for a missing-module test. The existing Windows OS UIA probe and native attach-failure/
teardown fixtures remain separate acceptance controls in the published native-package and full backend routes.
GPU rendering remains with its existing producer/public GPU consumer oracles; this lane selects Slim runtimes.

All commands have bounded process-tree supervision reused from the public shared-API consumer. A failure retains
the first failing command, exact public assets and graph/native/control receipts. Existing output/cache directories
are refused. The workflow has no publication credentials or publishing step.

The policy tests use synthetic restore metadata and binary headers only to test rejection behavior; they never load
those headers as libraries, create a Host or claim renderer proof. A matching public cohort is required for actual
restore/compile/JIT/AOT execution. Run the hermetic checks with:

```powershell
rtk proxy python -I tests/native-packages/test_cohort347.py
```
