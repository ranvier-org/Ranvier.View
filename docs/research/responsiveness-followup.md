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

Batch B is PR #127. Task 13 adds atomic bounded admission to the existing host FIFO rather than a second commit queue. Accepted/Full/Closed distinguish caller retry from shutdown, and pending/peak/rejected/discarded counts expose overload. Ordinary Post remains compatible. The reference analysis worker uses a capacity-one .NET Channel and process-wide concurrency semaphore, with asynchronous cancellation callbacks so producer/owner calls do not run arbitrary cancellation handlers synchronously. Core graph batching and keyed DOM projection remain in use.

Tool friction: SageFs `#load LatestAnalysis.fs` did not return; cancel_eval acknowledged cancellation. The compiled worker built and its burst test passed via the CLI. No daemon restart was performed.

Batch C review fixes: replace recursive async consumption with one iterative task loop (processed-payload weak-reference test); count submitted/completed/superseded/cancelled/failed requests; reject analysis errors and work deferred beyond the measured interval in the comparator. The real document scenario now sends immutable snapshots to detached declaration scanning and validates the captured document/text/request revision on owner application. Tail drain is reported separately and must apply the latest request. A 100,000-line smoke accounted for all 100 requests, applied the latest, and recorded zero failures.

Task 14 automated qualification adds matched typing and long-line performance gates on Windows/Linux/macOS against fixed baseline 960fdfa with identical native packages. Visible OS input-to-present timestamps, physical IME candidate placement, mixed-DPI hardware and 120Hz display coverage still require appropriate hardware; hosted synthetic traces must not be labelled that evidence.

Task 15: add an opt-in raw-byte ceiling checked before capture (and rechecked when a queued preview starts). Uncropped elements conservatively reserve the existing native 64MiB maximum because CSS boxes exclude shadow/overflow paint bounds. Cropped subjects use rounded bounds; encoded output and codec workspace are explicitly outside this raw-pixel ceiling. Raw RGBA results return their existing owned array without a redundant encoder/copy or worker dispatch. Codec-lifetime tests now explicitly request PNG.

Task 15 review: use authoritative CSS viewport dimensions rather than dividing truncated paint dimensions by fractional device scale. Custom budgets reject the 101x101 / 1.25 device-scale / 8x capture edge before painting. The default 64MiB ceiling remains enforced by native capture so conservative crop margins do not reject native-valid boundary captures.

Task 15 ruling: retain owned arrays for PNG rather than copy them into a pool or expose borrowed native buffers. Pooling the existing public array result would either add another copy or violate retained-result lifetime. A native leased-buffer ABI is deferred; PNG owner capture/copy cost remains a measured limitation. No native ABI change is needed for this batch.
