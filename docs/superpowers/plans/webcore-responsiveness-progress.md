# Execution ledger — plan: webcore-performance-plan.md (chat outputs, 2026-11-10)

Scope: tasks 0–3 only, authorized by user. Baseline dab7ab62ac4d1f2c435adddea05f1341ccc8e43b.

## Core and view state

- Core primitives retained: graph dispatcher, batch/flush, Owner cleanup, asynchronous cancellation policies. No replacement reactive scheduler.
- View-owned state: native host synchronization-context queue, platform wake registration, frame/input timing, and native-result delivery. Task 3 must reuse a core equivalent if one exists; otherwise implement the explicitly authorized host delivery slot, not a graph commit queue.

## Preflight

- Both public heads unchanged since audit (View dab7ab6; native 9f4f71c).
- C:/wk/view-profile has unrelated tracked edits; left untouched.
- Reused C:/wk/view-main; preserved feat/sdl-pointer-motion-rate, switched to new codex/webcore-responsiveness-0-3 at public main. Untracked .agents/ and .codex/ remain untouched.
- Task 0 trace consumed by tasks 1–3; task 1 wake/pending contract consumed by task 2; task 3 delivery uses tasks 1–2 queue.
- Ruling: use the repository's actual CI native test command because build.fsx test groups do not include the WebCore test project. Full relevant native suite is mandatory per task; broader repository gate remains final.
- Ruling: physical 60/120 Hz qualification is hardware-dependent; collect available local evidence and explicitly distinguish unavailable platform/display qualification from deterministic tests.

## Status

- Final smoke after review fixes: seven GPU and seven software scenarios passed, one second each, zero managed/native trace loss, under artifacts/responsiveness-verified-gpu and artifacts/responsiveness-verified-cpu. These remain smoke evidence, not absolute performance qualification. Our SageFs session was stopped and work leases released.

- Final: fixed teardown release isolation — `one failing release cannot prevent other host slots retiring` RED→GREEN; suite 930 passed / 17 skipped / 0 failed.
- Final: fixed presentation identity and missed completion observation — `presentation completion retains the submitted frame identity` RED→GREEN; same full suite, real Windows D3D11 path.
- Final: documentation summary and formatting findings corrected; no deferred minor findings. Native ABI unchanged.
- Final tooling limitation: one RTK-wrapped test run stalled with testhost PID 53540 retaining its output DLL after termination; dotnet-stack could not attach. Cause not established. A subsequent build failed explicitly on that lock. Verification therefore used isolated `--artifacts-path artifacts/review-fixes`, RTK proxy, and 90-second hang diagnostics. The final unfiltered run completed normally in 26 seconds; results are retained in artifacts/review-fixes/results/review-fixes.trx. The stalled run is not counted as passing evidence.

- Final review: independent read-only review of dab7ab6..84730a6. Two Important findings: teardown release isolation and presentation frame identity. Both entered the single fix pass. The teardown regression failed as expected; the delayed-present test also exposed missing completion observation and trace-buffer loss in the test collector. Completion tracing now retains submission identity. The frame-flood test was left intact; a focused delayed-D3D test avoids its deliberate trace overflow. RED: completion expected frame 3, actual frame 4. GREEN: final unfiltered suite 930 passed, 17 platform/browser exclusions, 0 failures.
- Final verification: NativeApi.Conformance exited 0. New API XML audit has zero findings; Fantomas and whitespace cleanup are included. A stale internal reference signature required a clean native-test rebuild.
- Final: Ruling: physical 60/120 Hz latency, tracing overhead, frame-miss thresholds and five long samples remain qualification work; local synthetic smoke establishes execution only. Cost if wrong: platform-specific regressions could remain undiscovered.
- Final: Ruling: Linux/macOS runtime and presentation acceptance remain unverified on this Windows host. Cost if wrong: cross-platform integration defects.
- Final: Ruling: native/managed timestamps remain separate; hardware inventory is supplied by the qualification operator. Cost if wrong: no valid merged cross-boundary latency attribution yet.
- Final: Ruling: timer/native-loop/microtask/deferred-release batches remain exhaustive. Local release maximum was 0.0418 ms and timer maximum 0.0035 ms, with no due timer workload; this is not an overload proof. Cost if wrong: those sources can still stall an interactive turn.
- Final: Ruling: arbitrary callback preemption and universal latency guarantees remain excluded. Cost: long callbacks/checkpoints can exceed budgets.
- Final: Ruling: layout phases, viewport recycling and full IME/undo/Unicode changes remain tasks 4 and 6. Cost: existing layout and context costs persist.
- Final: Ruling: GPU pool/readiness/device-loss/fence changes remain tasks 5 and 7. Cost: existing GPU waits persist.
- Final: Ruling: capture/encoding cancellation remains task 8. Cost: existing capture costs persist.
- Final: Ruling: reactive scheduling remains in core and document revision policy in the application; the authorized slot only transports detached host results. Cost: applications must perform revision validation correctly.

- Task 0: baseline 907/907 tests passed. RED: missing scheduling trace API assertion failed. Added trace and bounded-buffer tests; 909/909 passed before final SDL observation hook. Final suite and paced harness smoke running.
- Task 0 ruling: use a paced synthetic local fixture, label observed presentation completion and separate native clock domain explicitly. Physical refresh-rate/input-to-photon qualification, driver inventory and merged cross-clock timeline remain qualification work, not inferred from this local run.
- Task 0 tooling: bare SageFs #r of host DLL failed dependency resolution and held the output DLL; stopped only our session. Pure trace prototype passed in REPL. New bare session avoids loading build outputs.
- Task 0 implementation gate: 909/909 full native-backed tests pass; six paced GPU scenarios pass, no trace drops. Baseline raw data: artifacts/responsiveness-baseline-paced. Long 60/120 Hz platform qualification remains explicitly unverified.
- Task 1: complete. OwnerWorkQueue prototype passed in SageFs; missing-type RED test failed before implementation. Queue/coalescing, reentrant wake failure, concurrent producers/Stop, shared-window delivery and disposal-race tests added. Full suite: 915/915 passed. SDL registered event type is the stable wake token; retired events carry no host pointer. A 50 ms maximum wait retains recovery from failed signaling; OwnerWakeFailures reports failures. No new native ABI.
- Task 2: complete. Opt-in Window.InteractiveBudget; public Cycle remains exhaustive. Deterministic count/time checks, FIFO/checkpoint, exception/disposal, self-post/frame progress, and IME clear/commit split across SDL batches pass. Full native suite 921/921; six GPU smoke scenarios pass with zero trace drops. Pending queues and truncated SDL batches force the next wait to zero, so remaining work needs no redundant SDL wake. Queue delay, drain counts and budget-exceeded events added. Timer callbacks and deferred release batches remain exhaustive and measured (release count excludes owner cleanup actions); splitting those is deferred because the short fixture does not establish sustained overload. No hard latency claim. Tooling note: a smoke launch overlapped the test lease after run-app lease refusal; both completed successfully, but timings are not comparative qualification evidence.
- Task 3: complete. Full native suite 928/928; seven one-second GPU and CPU fixture scenarios pass with no trace loss. Tests cover 10,000 replacements, concurrent disposal, application/release failures, publication during apply, host teardown, and stale document/text/project/request revisions. Core audit: Api.createStream/Streams.fs coalesces graph publications and FlightPolicy.CancelPrevious owns cancellation; neither offers detached host-result ownership/release without constructing reactive graph state. The user explicitly authorized the plan's host-scoped slot; keep that exception narrow, prefer core primitives for reactive values, and keep document revision state solely in the fixture/application. Pure mailbox prototype passes; missing-type RED failed before implementation. Initial native suite 925/925.
