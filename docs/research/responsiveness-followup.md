# Responsiveness follow-up execution ledger

Baseline: View `7bf646a706eafe1012fad2cdbfbd3df4e62ee477`, native `f61977ca19ebda64070cdb74750774a3f9bc05e6`.

Authorization: implement tasks 9–16, test edge cases between batches, create PRs, watch CI, merge green batches and release.

## Decisions

- Reuse `C:/wk/view-main`; preserve untracked `.agents/` and `.codex/`. No remote changes since the measured release at start.
- Timing gate: five matched fixed-operation runs, median run p95 <= baseline ×1.15 or baseline +0.5 ms (whichever is larger); allocations <= baseline ×1.20 or +256 bytes/op. Reject unmatched conditions, nonfinite metrics, differing completed counts or unfinished work. Shared-runner timing is informational; deterministic paint/cache/admission contracts gate ordinary CI.
- OS presentation timestamps and physical IME/hardware qualification must not be fabricated. Record unavailable evidence explicitly; implement automated coverage where infrastructure supports it.
- Use the CLI for SDL/native integration tests, which require their own process and native runtime. Serena cannot resolve the standalone fixture Program.fs in LoadedProjects; file fallback is used for that file.

## Progress

- Task 9: comparator tests RED (missing module) then GREEN (6 tests); fixed-operation fixture protocol added. Remaining evidence/CI integration pending.
- Task 10: real SDL regression test RED (full paints 1 -> 2), GREEN after splitting content/full invalidation. Full suite: 952 passed, 18 platform/browser skips. An immediate Step can be refresh-throttled; the test waits up to one second for the actual paint. Content removal pixels and explicit full presentation covered.
- Batch A timing: five interleaved CPU typing runs passed the gate; median per-run p95 9.7486 -> 6.3169 ms. Initial local timing included concurrent build/test activity, so this is corroboration, not controlled hardware qualification. Dedicated output avoids an unrelated old process (PID 53540) holding normal test DLLs.
- Tasks 11–16: pending.
