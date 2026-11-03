# Final ABI63 public consumer arming

This lane prepares public package checks for `win-x64`, `linux-x64` and `linux-arm64`, each normal and traced.
Both x64 and ARM64 consumer jobs are statically disabled. `inputs/unarmed.json` records allocated Native P8 and the
proposed View46 version families, with provider allocation, managed source and actual runtime hashes incomplete.
Neither a dispatch nor an input value can enable a job. The pull-request job runs hermetic policy
tests only. No public or ARM consumer run has been dispatched or qualified by this preparation.

Before changing the disabled job, fill a reviewed input JSON with the actual public View/Native/provider versions,
managed/Native/provider/WebKit source pins and the selected RID's independently checked host and provider hashes. Preserve
Core preview.1, DOM alpha.56, CSS alpha.2 and CSS add-ons alpha.1. The provider follows the matching Native version.
Keep the complete five-RID native aggregate dependency graph; this consumer matrix executes only the three named
RIDs and does not qualify Windows ARM64 or macOS by implication.

Native P8 source `b3073f604a4fba7a1a565d2893ff5791c603b759` includes the reviewed manifest, worker-first-owner and IME
corrections and GPU producer oracle. Its Linux wheel/clipboard/rich-drop fixtures are byte-identical to reviewed
`541ca21f16b1d0399e9c6eec6b0a27e58915c1b4`; they are not copied into View. Source adoption does not replace actual
matched producer/catalogue/IME receipts. ARM provider/managed qualification remains retained active work on the
actual P8 ARM producer, with its own disabled job. Missing ARM provider hashes do not enable ARM or block the
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
