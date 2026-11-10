# Execution ledger — webcore-performance-plan.md tasks 6–7

## Core and view state

Core continues to own reactive scheduling, batching, keyed identity and ownership. View owns platform text-input coordinate translation and GPU presentation state. Native owns bounded extraction, stale-request validation, GPU readiness and resource lifetime. No duplicate reactive primitives are introduced.

## Preflight

- Authorized: implement tasks 6–7 sequentially in existing worktrees, test task 6 before task 7, push previous work and watch cross-platform CI through gh watch.
- View baseline 4aa5a13, native baseline a591a9b. Both worktrees have no tracked changes; unrelated .agents/.codex and tool .serena directories are preserved. Fetch found no new main commits since the prior audit.
- Pushed both existing branches. Branch pushes do not trigger CI; explicitly dispatch View CI and native GPU build for win-x64/linux-x64/osx-arm64. No release tags or package publication.
- Shared interface: task 6 is additive IME capability/ABI; task 7 extends task-5 readiness contracts without changing legacy transport. ABI versions and generated bindings must advance together.
- SageFs get_daemon_status still returns Transport closed. Continue with existing Serena F# semantics and isolated build/test CLI gates; do not restart the user's daemon.
- Plan is outputs/webcore-performance-plan.md in the originating chat; no separate spec is named. Detailed scope and exit criteria remain the plan's task 6/7 sections.

## Status

- Task 6: local implementation gate passed; source CI and physical IME qualification remain distinct outstanding evidence.
- Task 7: implementation complete for qualification; local Windows regression gate passed, cross-platform source CI and final review running.

## Prior-work CI and fixes

- Native run 42815858854 (a591a9b): Linux GPU build/probes passed; Windows/macOS remain running. Package contract rejected ABI 67 in AT-SPI's allow-list. Fix e0cba00 admits additive ABIs 66–68; distribution tests pass (11, one platform skip). Pushed.
- View run 42815817284 exposed NativeAOT printf reflection roots in generated OwnerTurnResult/SchedulingEvent.ToString. Fix 1bded41 uses primitive formatting under NativeAOT and removes formatted GPU error interpolation. A local NativeAOT publish passed with warnings-as-errors; correct-head CI run 42820113706 is being watched. Pushed. Earlier accidental dispatch 42820011777 used the old head and is not qualification evidence.
- Watches use gh run watch --exit-status with logs under artifacts/ime-xplat. No package publication requested or performed.

## Task 6 implementation and evidence

- ABI 68, ime-surrounding-window-v1: a maximum 4,096 UTF-16-unit selection-centered window, origin, relative caret/anchor, and single-use token. Both endpoints preserve surrogate pairs. Long selections, active composition, arbitrary contenteditable and unsupported control structure explicitly report Unavailable; secure controls report Secure without text.
- Ruling: traverse at most 64 flat inner-editor child nodes, retaining refcounted strings and copying only the window. No full value getter, HTML serialization, text-position scan or layout flush in queries. No claim of general bounded contenteditable extraction. Explicit deletion alone uses the established full-context edit transaction to preserve beforeinput/reentry/undo behavior; that operation can be linear.
- Validation combines document identity/tree generation, focus epoch, editor content/selection epoch, history epoch and host editing sequence. Host focus changes invalidate directly. Patch 0079 removes final from EmptyChromeClient's existing focus callback so the embedder can observe focus round trips; virtual layout is unchanged.
- Native RED missing export, then window-focus round-trip stale deletion RED; GREEN 91 assertions including nonzero-origin edits, Unicode, multiline controls, selection/focus round trips, composition, undo and reentrant beforeinput. Legacy context/delete probes passed; history 662 assertions and observation/reentry all seven modes passed. Catalogue check and probe registry tests passed (29, one skip). Patch applies cleanly to the existing source checkout.
- Private Windows host build uses task-owned overlay of the one EmptyClients.h declaration plus existing static libraries. Shared C:/wk/src and C:/wk/build-win-gpu are not changed. Source CI must still compile the patch on each platform.
- Managed ImeContext is capability gated, with explicit states and stale-delete rejection. Window.BoundedImeContext is off by default; unavailable contexts clear SDL's advertised text. Old runtimes retain legacy behavior. Focused managed tests passed 2/2. SageFs remains disconnected; isolated CLI gates retain hang diagnostics.
- Fixture --bounded-ime retains the trailing editable context and records its document origin/caret. Trimming occurs outside composition and only when the entire selection survives; input DOM identity stays stable. This is synthetic append-tail editor policy, not a general document buffer. Real OS IME acceptance is still unqualified; synthetic ABI tests are not physical Windows/Linux/macOS IME evidence.
- Full managed gate: 941 passed, 17 excluded, zero failures, 25 seconds; artifacts/ime-xplat/results/task6-full.trx and diagnostic log. Host preparation tests pass (6, four platform skips). Five-second typing/IME fixtures at one million logical lines retained 125 live rows and zero CPU frames/dropped events. Origins advanced to 387/390 UTF-16 units, caret 551/532. P95 turns 4.84/11.19 ms are smoke evidence, not comparable hardware qualification. Runtime SHA256 51F2D47ABEC5431BE4C168E87D31B7D3B72A35C21A1525154FD70F627829301D.
- Prior-work View CI now passes Windows x64/arm64, Linux x64, macOS backend and both Metal jobs; browser jobs remain in progress. These use released native dependencies, not the new ABI 68 implementation.

## Task 7 implementation and evidence

- Task 6 committed native c48566c, View d032713 after its local gate. Prior-work View run 42820113706 finished successfully. Native baseline run 42815858854 passed Linux/macOS GPU builds and probes; Windows still compiling. Source qualification for tasks 6–7 runs as 42826506191 on native branch codex/webcore-ime-gpu-6-7 at 1ca7f409, using the same existing worktree. A separate branch avoids cancelling the previous run.
- ABI 69 extends the existing pipeline contract to EGL/ANGLE-Metal only when EGL_KHR_fence_sync is available. Producer fences use zero-time waits. Linux consumer release submits another fence. Mac release requires caller-proven Metal completion; View polls one retained queue marker. The same three-slot ledger spans resize generations; surface allocations retain the 256 MiB accounting charge through retirement. Failed fences remain quarantined rather than proving reuse.
- Native generic delayed-completion test RED (missing pool) → GREEN; covers producer/consumer delay, failed fence quarantine, destruction, discard and existing ledger/memory cases. Windows host compilation and 157 real D3D pipeline assertions passed after portable routing changes; bounded IME probe still passes 91. Probe registry tests pass (29, one skip), AT-SPI distribution 11 (one skip), capability catalogue and generated declarations agree.
- Managed retained completion test RED (missing CompletionFence) → GREEN; verifies pending polls neither wait nor resubmit a marker. Full Windows managed gate 942 passed, 18 excluded in 25 seconds; artifacts/ime-xplat/results/task7-full.trx. Portable test covers two windows, input, resize/DPR and explicit fallback, but is excluded on Windows and must execute in source-artifact qualification.
- Ruling: EGL has no portable owner-loop completion notification and SDL exposes the Metal queue without a managed callback bridge. Reuse scheduled display-interval retries, not an off-thread GL/ABI release path. Cost: readiness observation can lag by a frame interval. Explicit cleanup/readback can wait; SDL_RenderPresent/driver submission can still block.
- Ruling: external EGL/IOSurface inputs retain their legacy contract that reads complete before return. Portable submit explicitly reports Unsupported for those bindings; View uses legacy transport. No claim of nonblocking external image input on these platforms.
- Portable device/context loss is an explicit error requiring window/host recreation; uncertain GPU completion retains resources and budget. No silent CPU fallback on NotReady. Normal CI remains released-runtime compatibility evidence; new workflow webcore-responsiveness.yml consumes exact native build artifacts, hashes payloads, runs integration and produces typing/IME traces across three platforms.
- Source CI package-contract exposed a test harness issue: its extracted rv_abi_has function lacked mocks for the new runtime capability query. Added mocks in test_host_prepare.py; native production function is unchanged by this fix. Source build results remain pending.
