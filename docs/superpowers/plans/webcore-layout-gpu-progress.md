# Execution ledger — webcore-performance-plan.md tasks 4–5

## Core and view state

Core retains graph batching, ownership, keyed projection and reactive scheduling. View owns DOM viewport nodes, layout-read diagnostics and platform frame transport. Editor text, revision identity and measurement caches remain fixture/application state. Native owns bounded GPU surfaces and readiness/lifetime tracking.

## Preflight and stalled-run investigation

- User authorized tasks 4–5 in existing worktrees, after investigating the prior stall. View baseline is 1f68611 in C:/wk/view-main. Public heads remain View dab7ab6 and native 9f4f71c, unchanged since audit.
- Reused clean C:/wk/rw-gpu-combined on new branch codex/webcore-layout-gpu-4-5 at native 9f4f71c; preserved its prior branch codex/project2-windows-coherent-product-f. Unrelated worktrees are untouched.
- Recovered original report: AppData/Local/Temp/rtk_dotnet_testresults_1a1c4cd9204139a41/shaya_I_PASS_BUTTER_2026-11-10_17_33_00_net10.0.trx. Start 17:32:59.137, finish 17:33:23.887 (+08:00). 929 executed: 928 passed, one known blocked-present trace-test failure, zero timeout/abort/in-progress. The test body did not remain running: the apparent stall followed result completion.
- PID 53540 is marked HasExited=True but retains one waiting thread (102980) and 856 handles. Managed attach cannot observe an exited runtime. No matching Application crash/WER event was found in the incident window. The exact native/process-output shutdown cause cannot be recovered from the retained evidence.
- Unchanged baseline with RTK proxy, VSTest diagnostics and 60-second hang-dump collection: 930 passed, 17 excluded, 0 failures in 26 seconds. Evidence: artifacts/review-fixes/results/stall-investigation.trx and stall-investigation.log.
- Ruling: retain isolated build outputs and diagnostic/hang collection for native-backed tests; do not make a speculative WebCore scheduling fix for the historical shutdown incident. Cost if wrong: the intermittent shutdown defect could recur; future runs retain identifying diagnostics.
- Shared interface: task 4 retains the existing synchronous DOM and layout semantics; task 5 adds a capability-gated readiness ABI while retaining legacy acquire/release. Native contract/tests precede the View consumer.

## Status

- Task 4: implemented and tested; native and View commit pending.
- Task 5: pending; starts after task 4 passes its gate.

## Task 4 evidence and rulings

- Native ABI 66 adds capability-gated rv_host_layout_count. WebCore increments the sampled counter in LocalFrameViewLayoutContext::didLayout, so clean checks and actual layout work are distinguishable without forcing layout. Scope is main frame, including subtree layouts; excludes style-only changes and child frames. Reset/wrap are documented. No layout calls were removed.
- Red: native layout probe failed on the missing export. Green: 55 layout assertions; 337 host-tracing assertions; 26 partial-repaint assertions. Generated interop check: 352 declarations, zero differences. Capability catalogue check passed.
- Private host-only build reuses C:/wk/build-win-gpu libraries; compiles all six host objects into native artifacts/layout-gpu and checks shared object mtimes are unchanged. Runtime SHA256 C49FD1BD29246D5CA344B15F123E3A5B3E671F8D437C210AB1B451F8BF5818E7. The inherited PATH exceeded vcvars batch limits; private build bootstrap uses a minimal Windows PATH. No shared source/build outputs were modified.
- Managed red: missing layoutForHost/Measurement APIs. Green: four focused tests; full suite 934 passed, 17 excluded, zero failures in 27 seconds, with VSTest diagnostics and hang collection. XML audit: zero findings. Artifacts: artifacts/layout-gpu/results/task4-full.trx and task4-full.log.
- Ruling: phase annotations are explicit and synchronous, with no interception, scheduling or second reactive transaction manager. Fixture uses core Graph/Api.batch and keyed each slots. Warning Pending=1 means a read followed declared writes; Count reports actual layout passes or -1 when unsupported. Cost: callers must annotate their own phases; arbitrary third-party DOM writes are not inferred.
- Ruling: keep an independent bounded textarea for input/selection and pin the displayed range during composition, applying queued scrolling after commit/cancel. This is an application fixture, not a general contenteditable virtualizer. Tests cover node reuse, selected range preservation, commit/cancel and painting. Cost: scrolling the displayed range is deferred while composing. Variable-height wrapping and physical platform IMEs remain outside this fixture.
- Cache keys include text/font/width/style revision; font events, DPR, wrap and viewport dimension changes invalidate. Capacity is twice the live row count. Fixed 25 visible rows plus 100 overscan; document text is synthesized, not allocated in full.
- Nine GPU fixture scenarios passed at one second each with no dropped trace events, including independent long-line and bidi workloads. Typing at 1,000 versus 10,000,000 logical lines: both 125 live rows, 220 iterations, 218 cache entries; 8,436,008 versus 8,436,000 allocated bytes. P95 2.42 versus 2.99 ms. These short synthetic runs establish smoke/scaling evidence, not physical refresh qualification.
- Prior task-3 fixture comparison: 150 static pre lines, P95 2.25 ms, ~3.16 MB allocations. New fixture performs real row mutation/measurement and uses a different staged runtime; this is a workload comparison, not a speedup claim. Long-line P95 81.94 ms and bidi P95 45.08 ms show remaining shaping/viewport costs.
- Managed integration currently builds against the local canonical interop using UseLocalRanvierWebCore=true and RanvierWebCoreRoot=C:/wk/rw-gpu-combined. Package coordination remains last in task 5; nothing is published.
