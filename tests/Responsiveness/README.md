# Responsiveness fixture

Run `rtk dotnet run --project tests/Responsiveness -c Release -- --scenario all --seconds 60 --repetitions 5 --output artifacts/responsiveness`.

Options: `--lines 1000|100000|1000000`, `--unbounded-dom` (deliberately materializes every line), `--renderer software` (CPU presentation), `--visible`, and `--no-trace`. Scenarios are idle, typing, scroll, ime, publication, resize, or all. Input is synthetic and paced at approximately 125 operations/second; it is not an OS input-to-photon benchmark. Initial startup gets ten loop turns of warmup.

The viewport has 25 visible rows and 50 overscan rows on either side (125 live rows for documents of at least that size). Ranvier's keyed `each` projection retains slot nodes; core `batch` updates their text before geometry reads. Logical text is synthesized by line number, so this measures viewport costs rather than a real document buffer or language service. `long-line` and `bidi` are separate shaping workloads. The input textarea is a stable, bounded editing context; selection never lives in recycled display rows. Composition pins the displayed range and applies a pending scroll after commit/cancel.

The line-width cache is bounded to twice the live row count. Text, font, width and style revision form its key; font events, device scale, wrapping and viewport dimensions invalidate it. Scrolling changes text keys and cannot grow the cache with document length. The fixture uses fixed 20px rows and does not implement variable-height wrapping layout.

`Frames.layoutForHost` annotates synchronous application phases without adding a scheduler or changing getters. With scheduling tracing enabled, `Measurement.Pending = 1` warns that declared writes preceded the read; `Count` records actual main-frame layout passes, or -1 on older native runtimes. Native ABI 66's `host-layout-metrics` counter does not itself flush work. It excludes child frames/style-only changes and must not be compared across document replacement. Nothing automatically intercepts arbitrary DOM writes or reads.

Each sample saves summary, managed events, and native events. Managed events use Stopwatch ticks and identify host, sequence and frame. Native events retain their original clock domain; do not merge timestamps without calibration. PresentCompleted is owner-thread observation of completion, not a display scanout timestamp. Counts dropped by either buffer are explicit; samples with loss are unsuitable for detailed timeline analysis. Both exported event collections are capped at 500,000 entries.

Use the same staged runtime hash, renderer, device, resolution, display refresh and driver for comparisons. Record the latter hardware properties separately. Run traced and untraced samples to establish instrumentation overhead. The current summary describes total synthetic turn time and allocation, not isolated raster performance. On hidden windows there is no claim of physical 60/120 Hz qualification.
# Interactive scheduling

Use `--interactive` to opt into 64 posted callbacks or 2 ms per checkpointed turn, and 256 SDL events or 2 ms per batch. Generic posts remain lossless and may accumulate under sustained overload. The queue-delay trace makes that backlog visible. Microtasks, timers, native run-loop work and deferred release batches remain cooperative and exhaustive.

Use `--gpu-pipeline` to opt into `Window.GpuFramePipeline`. Windows requires native ABI 67's
`gpu-frame-pipeline-v1`; older runtimes keep the legacy path. NotReady preserves the current image and
retries at the display interval without selecting CPU readback. `ReadPixels` remains an explicit,
synchronous diagnostic operation. Layout and GPU command submission still run on the owner thread.

The managed package remains compatible with the released rc.5 dependency. Four internal optional ABI
imports are capability-gated, so consumers can stage an ABI 66/67 native runtime without requiring an
unpublished managed package. Native release packaging must include the regenerated canonical bindings
and host together; enable the pipeline only after validating that runtime on the target Windows hardware.
The feature is off by default. ABI 69 adds Linux EGL and macOS ANGLE/Metal support when
`EGL_KHR_fence_sync` is available. Linux submits producer and consumer fences; macOS polls producer
completion and the SDL Metal queue. Both use the same three-slot pool across resize generations and
256 MiB process accounting cap. Failed, unproved fences retain their resources and charge. Recreate
the window/host after portable device or context loss. External image inputs retain their legacy
completed-read path. Cleanup/readback remain explicit synchronous operations, and SDL presentation
or driver submission may still block. Completion is retried through the existing display-interval
timer; there is no arbitrary-thread native release or busy loop.

The manually dispatched `WebCore responsiveness qualification` workflow consumes an exact native
build run's GPU packages, records payload hashes, runs the managed suite and captures typing/IME
traces on Windows, Linux and macOS. Ordinary CI against released packages verifies compatibility;
it cannot qualify the new native implementation.

Use `--bounded-ime` to enable `Window.BoundedImeContext` on ABI 68. Control queries copy at most
4,096 UTF-16 units and visit at most 64 flat editor nodes. Selection direction and a document origin
are retained. Arbitrary contenteditable, active composition and selections that do not fit explicitly
return Unavailable; there is no full-text fallback on this path. Older runtimes retain legacy behavior.
Deletion validates the token and then uses the existing editing transaction; only explicit deletion
may still serialize the full control. The fixture keeps a trailing input context, trims outside
composition without discarding a selection, and reports its absolute origin/caret. It does not retain
a complete document buffer or qualify physical platform IMEs.

`posted` is the deliberately unbounded ordinary-post control. `publication` uses one detached-result slot, one worker at a time, and owner-side document/text/project/request revision validation before changing the visible decoration. The sample cancels its worker on shutdown and checks revisions regardless of cancellation. The document ID is a fresh open-document generation, so reopening a path cannot accept an old result. For reactive values, use Ranvier core async/stream primitives instead of this host transport.

`LatestPublication` transfers ownership of every published value, including values rejected after disposal. Its apply callback borrows the payload; the optional release callback runs once afterward or when replaced/rejected, on whichever thread owns that operation. Release must be thread-safe and cannot access native handles. Dispose clears pending payloads; a delivery already claimed may complete. Revision validation is application policy, not inferred by the generic slot.
# Regression comparison

Use `--iterations 600 --repetitions 5 --no-trace` for matched fixed-work cohorts.
Run baseline and candidate in interleaved fresh processes on the same machine,
with identical renderer, viewport and editor options. Set `RANVIER_BENCH_COMMIT`
to the tested commit. Keep the raw JSON and runtime receipt.

`python tests/Responsiveness/compare.py BASELINE CANDIDATE --scenario typing`
fails for a median p95 increase exceeding both 15% and 0.5 ms, or per-operation
allocation exceeding both 20% and 256 bytes. It rejects unmatched conditions,
invalid data and unfinished work. Queue workloads currently mark unknown pending
counts as -1 and cannot pass this timing gate. Shared CI runners enforce structural
contracts; timing acceptance requires matched controlled runs.
