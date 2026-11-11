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
- Batch A review: fixed comparator false-pass for reduced live rows/presentation coverage (negative test RED then GREEN, seven comparator tests). Added CPU fallback accounting and corrected invalidation documentation. PR #126 open with CI watched.
- Task 11: ring reuse preserves retained row signals/nodes; compact identity/revision measurement keys eliminate DOM text copies. Single-entry FIFO eviction replaces whole-cache clearing (REPL RED/GREEN). Focused layout suite passed; final suite and long-line comparison in progress.
- Tasks 12–16: pending.

Task 11 evidence: 954 tests passed (18 skips). Five matched long-line cohorts passed: median run p95 107.0605 -> 12.2809 ms; managed bytes/operation 4,133,808 -> 127,268 (96.9% reduction). Raw local evidence is under artifacts/viewport-comparison.

Task 12 ruling: use .NET ImmutableList's persistent balanced tree for the reference text buffer instead of writing a new piece table. This provides indexed lines and structural sharing; only changed line strings are rebuilt. UTF-16 line/column edits preserve CRLF and reject surrogate splits. Undo history is bounded to 64 snapshots and advances semantic revisions.

Task 12 ruling: defer horizontal text segmentation after the unchanged long-line workload improved below the 16.7ms frame interval. Whole-line WebCore shaping preserves bidi/contextual shaping and caret semantics. This leaves extreme single-line cost unbounded; report that limit rather than introduce unqualified shaping changes. Wrapping uses cached native row heights, not estimated character widths.

Batch A merged as #126 after CI 42947829704 succeeded. Batch B independent review found undo anchoring, empty-line height, last wrapped-line reachability and deletion during pinned composition. Fixes use inverse edit ranges, 20px minimum row height, final-line anchoring in wrap mode and composition cancellation when the edited range is removed. Regression tests cover all four. Real 100,000-line document smoke completed 100 edits with p95 7.01ms and 100 dirty CPU paints.
