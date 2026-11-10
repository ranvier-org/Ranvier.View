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

- Task 0: baseline 907/907 tests passed. RED: missing scheduling trace API assertion failed. Added trace and bounded-buffer tests; 909/909 passed before final SDL observation hook. Final suite and paced harness smoke running.
- Task 0 ruling: use a paced synthetic local fixture, label observed presentation completion and separate native clock domain explicitly. Physical refresh-rate/input-to-photon qualification, driver inventory and merged cross-clock timeline remain qualification work, not inferred from this local run.
- Task 0 tooling: bare SageFs #r of host DLL failed dependency resolution and held the output DLL; stopped only our session. Pure trace prototype passed in REPL. New bare session avoids loading build outputs.
- Task 0 implementation gate: 909/909 full native-backed tests pass; six paced GPU scenarios pass, no trace drops. Baseline raw data: artifacts/responsiveness-baseline-paced. Long 60/120 Hz platform qualification remains explicitly unverified.
- Task 1: in progress.
- Task 2: pending.
- Task 3: pending.
