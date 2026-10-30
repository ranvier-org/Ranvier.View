# H02 checkpoint for the six-agent relaunch

This restart checkpoint is historical. The resumed implementation uses the existing host frame
clock with an origin captured at construction. A raw Chromium oracle demonstrated that named
endpoints select the latest timestamp, with insertion order on ties; the shared native timeline
and strengthened fixture now follow that behavior. The native domain shares managed frame/timer
timestamps; correspondence with DOM Event.timeStamp remains unverified and is not claimed.
Null object fields now reject explicitly. Both providers validate measure name, options and copied
detail before resolving endpoints, strengthening the shared fixture to 180 checks and its
deterministic run to 181. Final current evidence is recorded separately in
`docs/research/native-api-delivery/user-timing-runtime-evidence.json`.

Stop requested by the user at this checkpoint. No H02 source is committed or published.

## Ownership and exact trees

- Core: `C:/Users/shaya/.codex/worktrees/native-api-timing/Ranvier`, branch
  `codex/native-api-timing`, HEAD `b248f0d2fd985c56a4440ce744f9f4bbca52c3be` (public DOM alpha.12).
- Views: `C:/wk/rv-timing-views`, branch `codex/native-api-timing`, HEAD
  `e1c4cb6098c83a525d783a7e3b629a74570830c3` (public Views 0.1.3).
- Tracking: https://github.com/ranvier-org/Ranvier.WebCore/issues/109, Implementation/open.
- Own only the new timing files, compile lists, focused fixtures and this checkpoint.
  Generated contracts, root Native17/source, other consumer trees and frozen project snapshots
  are outside this lane. No producer ABI, package versions or source pins changed.

Core dirty files: `src/Ranvier.Dom/Timing.fs`, `src/Fable.Ranvier.Dom/BrowserTiming.fs`, their two
project compile lists (including the standalone Fable source-pack list), and
`docs/superpowers/plans/2026-10-30-user-timing.md`.
Views dirty files: `src/Ranvier.View.WebCore/Timing.fs`, its project compile list,
`tests/NativeApi.Conformance/UserTiming.fs`, `tests/UserTiming/`, this handover and the durable
evidence JSON named below. No existing general conformance runner is wired yet.

## Current design

TimingClock exposes monotonic milliseconds and a fixed Unix-millisecond origin. TimingTimeline
is a synchronized injected-clock user-entry buffer. Native providers share a Stopwatch timeline
through a ConditionalWeakTable keyed by host, validate the live host/thread for every operation,
and preserve entries on provider disposal. The timing origin starts on first timing-provider
acquisition for the host. It is not yet tied to native DOM-event or frame timestamp origins;
review that boundary against the selected H02 requirement before claiming completion.

Browser providers call the actual active document realm's Performance.now/timeOrigin/mark/
measure/getEntries/clear methods. Navigation invalidates the borrowed document provider.
Mark options include explicit time and detail. Measure options support start/end/duration and
detail, including omitted endpoints and reversed endpoints producing negative durations.
Named endpoints select the latest *inserted* mark, independently of its startTime.

The explicit endpoint DU includes Omitted, avoiding Fable Some-null erasure. Clear operations
use unit/string overloads for the same reason. Numeric optional fields retain their values.
Portable details support null, booleans, numbers (including NaN), strings, arrays and unique-key
objects, with a 256-level maximum and explicit DataCloneError for cycles/malformed data.
Insertion and returned snapshots copy details. Browser interoperability uses real plain values,
including reserved object keys via Object.fromEntries. Unsupported cyclic/Date external entry
details reject explicitly when snapshots are requested. Legacy navigation timing endpoint names
are explicitly NotSupportedError because native hosts lack that domain. Disposed timing
providers use shared TimingException InvalidStateError, avoiding erased CLR exception identity.
PerformanceObserver and H03/H14 are deferred.

These are draft API choices; root approved the ownership boundary and required complete options
and portable details, but has not independently reviewed the final source.

## Evidence and exact limits

- Latest source: deterministic fixture **177** passes in owned Sage bare session (explicit
  checks, not compiled-away asserts). Shared run contributes **176** of those checks.
- Latest source Windows JIT: **176 shared +177 deterministic +22 lifecycle/concurrency**;
  500 concurrent duplicate marks; native wrapper teardown zero. Exit zero in
  `artifacts/h02-native-jit.log`.
- Actual Chromium 156.0.8078.4: **160 shared +161 deterministic +11 context/interoperability**
  passed an earlier source revision. `artifacts/h02-browser.log` is historical. The final
  176/177 source and count guards have **not** yet been compiled/executed in Chromium.
- NativeAOT is **not proven**. First publish correctly failed NETSDK1207 on referenced
  netstandard2.1 projects. Restricting TargetFrameworks to net10.0 made publish exit zero, but
  emitted a managed runtime directory and **no Native.exe**. Explicit SelfContained did not
  resolve that. Do not treat the successful publish exit as AOT evidence.
- Diagnostic clue: ILCompilerTargetsPath/IlcPath are empty during project evaluation despite
  restored ILCompiler packages. The generated conditional NuGet props require TargetFramework
  during the early import; the commands lacked explicit `-f net10.0`. Next diagnose with that
  explicit framework (and the existing net10-only referenced-source restriction), then require
  an actual native executable and successful execution. Preserve previous attempts.
- Added `tests/UserTiming/Directory.Build.props` isolates Native/Browser obj folders. The first
  project layout shared obj state; future commands use the new separation. Runtime code did
  not change with this build-layout correction.
- Shared Core and restored Fable projects had fresh FCS zero before the last bounded API
  changes; final fresh FCS/API snapshot is still pending. Initial native Release build had
  zero warnings/errors. Final tracked diff checks passed at checkpoint; untracked files still
  need the normal format/XML/comment/CRLF gates.
- No full repository tests/analyzers, final source packaging consumers, CI, other native
  platforms, normal/traced official packages or public Views acceptance have been completed.

`docs/research/native-api-delivery/user-timing-checkpoint.json` records current source/log and
executed binary hashes, counts, graph versions and these qualifications. It is checkpoint
provenance, not a completion or release acceptance manifest.

## Reusable commands and remaining work

Temporary driver `C:/wk/h02-run.py` sets the explicit Core source path in its child environment,
captures stdout/stderr and exit codes, and preserves previous log attempts. Modes: browser-compile,
browser, native-jit, native-aot-publish, native-aot. Correct AOT invocation before reusing it.
Browser uses installed Fickl 0.6.9/Fable 5.24 and Playwright dependencies at
`C:/wk/ranvier-views/tests/NativeApi.Conformance/package.json`; no install is required.
The old compiled Native JIT assembly is under `tests/UserTiming/bin/Release/net10.0/win-x64/`.
The unsuccessful managed publish directories are `artifacts/h02-aot` and `artifacts/h02-aot-final`.

1. Independently review the draft contract, native host-domain origin and ownership/lifetime.
2. Finish final actual browser 176/177 checks; repair genuine failures without weakening tests.
3. Produce and execute a real NativeAOT binary; test lifecycle/concurrency as on JIT.
4. Wire H02 into the full shared conformance runners and supported-platform CI. The focused
   tests alone do not complete repository acceptance.
5. Run final FCS/public API, formatter, XML audit, comment-hygiene and applicable normal/traced
   full tests and analyzer groups. Complete source-pack/API consumer checks.
6. Coordinate the eventual Core/Views version allocation and integration with root after the
   active producer cohort; do not overwrite Native17 or copy generated contracts from newer
   lanes. Commit via message files, preserve CRLF, no rebase of published branches.
7. Update issue109/project with exact final proofs, keeping it open until its own release and
   applicable platform/profile/public consumption requirements are met.

## Clean resource handoff

Owned Sage bare session `2ed7078b` was stopped. All owned build/test leases were released.
All owned shell processes are terminal. No Serena process was started for this work; the
existing shared Serena/daemon processes were left alone. No CI watcher was started.

## Exact resume prompt

Continue H02 user timing from `C:/wk/rv-timing-views/docs/HANDOVER-H02.md`. Read that file and
`docs/research/native-api-delivery/user-timing-checkpoint.json` before editing. Reuse the isolated
Core and Views codex/native-api-timing worktrees and their dirty files; do not reset them. Finish
the final 176/177 browser proof and diagnose the missing native executable from AOT publish
(likely explicit -f net10.0 needed for conditional ILCompiler imports). Preserve prior failures,
validate real native execution, then integrate full conformance/gates and coordinate version
allocation with root. Respect rtk, FCS semantics, Sage inner loop/leases, CRLF, commit-message
files, no credentials or rebases, and stop only owned sessions. Do not touch active Native17,
other consumer lanes, frozen acceptance docs, paused Anime or deferred groups.
