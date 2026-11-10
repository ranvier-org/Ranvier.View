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

- Task 4: complete; native b26a1b3, View 4a67889. Passed its full gate before task 5 began.
- Task 5: complete; final suite 939 passed, 17 excluded. Native 47510ca plus pixel regression a591a9b; View consumer 25684f3, with viewport review fix e24c288.

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

## Task 5 evidence and rulings

- ABI 67 adds gpu-frame-pipeline-v1, retained consumer configuration, monotonic submission IDs and newest-completed acquisition. NotReady is positive 1; device loss is -12. Legacy calls retain their contracts.
- Three slots cover all resize generations; accounted RGBA/depth and external-input caches share a 256 MiB cap. Zero-time keyed-mutex checks cover production, acquisition and retirement. Destroyed resources remain charged until completion queries prove retirement or devices are removed. Minimum charge is 64 KiB; driver/private WebCore allocations are outside this cap.
- Native pure state tests passed (exhaustion, late/out-of-order completion, retirement, memory arithmetic). Real pipeline test passed 157 assertions, including three held leases, resize backpressure, owner-thread checks, busy external producer cache, actual device removal, document replacement, host teardown with a held frame, and translucent pixel equality with legacy at fractional DPR. Legacy external-input acceptance: 306 assertions, 42 frames. Native frame acceptance passed all four teardown modes after final retirement changes. Canonical interop: 355 declarations, zero differences. GPU tracing: 346 assertions.
- Private runtime SHA256 B46E3147CFDE704A6B6153C7F594DE8BFCD8AC913BF394D3949B6891C2D2C8EF. Shared WebKit objects remained unchanged. New-path readiness never requests a positive-time mutex wait. Driver submission and layout still execute on owner.
- View opt-in Window.GpuFramePipeline preserves the current image on congestion, retries at a scheduled interval, and does not choose CPU fallback on NotReady. Explicit Present retains its force-frame behavior; a regression failed before the fix and passed afterward. Device loss, SDL resize and direct host resize/DPR replacement are covered. Diagnostic ReadPixels remains synchronous.
- Full initial task-5 suite: 937 passed, 17 excluded. Final suite after review fixes: 939 passed, 17 excluded, zero failed in 25 seconds (artifacts/layout-gpu/results/task5-final.trx and .log). Native runtime staged after each managed build; one early focused run skipped against the old runtime and was rerun with the correct ABI, not counted as acceptance.
- Nine one-second pipeline fixture scenarios on 10,000,000 logical lines passed with 125 live rows, zero CPU frames and zero dropped managed/native trace records. Typing P95 3.20 ms; scroll 8.47 ms; resize 9.75 ms. Long-line P95 89.34 ms remains expensive. These are synthetic hidden-window smoke results, not a speedup claim or physical 60/120 Hz qualification.
- Ruling: keep released rc.5 package defaults and add four capability-gated private imports for ABI 66/67 instead of referencing an unpublished package version. Canonical native managed declarations are regenerated. Default-package build passed; old-runtime layout/legacy-presentation checks passed 14 tests, with three new-capability tests correctly skipped. Cost if wrong: release packaging must still coordinate the new native runtime and canonical interop; opting in on an old runtime remains legacy behavior.
- SageFs disconnected with Transport closed during the final managed gate. Used normal isolated dotnet builds/tests for that step; no daemon restart or changes to other sessions.
- Task-owned SageFs session stop and lease release were attempted at completion; the connector still returned Transport closed. No other session was touched. XML documentation audit passed with zero findings.

## Final whole-branch review

- Fresh reviewer found no critical issue and two important issues. Both were reproduced as failing tests before fixes.
- Final: fixed pending GPU submission stranded by direct host Resize/SetDeviceScale — tracked submitted physical dimensions and resubmitted on mismatch; direct-host-resize test RED→GREEN, final suite 939/939 executed passed.
- Final: fixed viewport edits/scrolling at document boundaries — derive slot and scroll from actual mounted start, clamp the first line to a full viewport, and initialize small documents correctly; viewport-boundaries test RED→GREEN for 10 and 1,000 lines, final suite 939/939 executed passed.
- Final: Ruling: the review's composition-test limitation is intentional fixture scope, not evidence of recycled-contenteditable selection safety. Input is a separate stable textarea and display rows are noneditable. Cost if wrong: consumers needing selection inside recycled editable rows need additional design and real-platform IME acceptance; no such guarantee is made here.
- Deferred minor: no test of selection/composition inside recycled editable rows; the fixture does not implement that interaction.
