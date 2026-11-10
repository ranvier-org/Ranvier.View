# Responsiveness fixture

Run `rtk dotnet run --project tests/Responsiveness -c Release -- --scenario all --seconds 60 --repetitions 5 --output artifacts/responsiveness`.

Options: `--lines 1000|100000|1000000`, `--unbounded-dom` (deliberately materializes every line), `--renderer software` (CPU presentation), `--visible`, and `--no-trace`. Scenarios are idle, typing, scroll, ime, publication, resize, or all. Input is synthetic and paced at approximately 125 operations/second; it is not an OS input-to-photon benchmark. Initial startup gets ten loop turns of warmup.

Each sample saves summary, managed events, and native events. Managed events use Stopwatch ticks and identify host, sequence and frame. Native events retain their original clock domain; do not merge timestamps without calibration. PresentCompleted is owner-thread observation of completion, not a display scanout timestamp. Counts dropped by either buffer are explicit; samples with loss are unsuitable for detailed timeline analysis. Both exported event collections are capped at 500,000 entries.

Use the same staged runtime hash, renderer, device, resolution, display refresh and driver for comparisons. Record the latter hardware properties separately. Run traced and untraced samples to establish instrumentation overhead. The current summary describes total synthetic turn time and allocation, not isolated raster performance. On hidden windows there is no claim of physical 60/120 Hz qualification.
# Interactive scheduling

Use `--interactive` to opt into 64 posted callbacks or 2 ms per checkpointed turn, and 256 SDL events or 2 ms per batch. Generic posts remain lossless and may accumulate under sustained overload. The queue-delay trace makes that backlog visible. Microtasks, timers, native run-loop work and deferred release batches remain cooperative and exhaustive.

`posted` is the deliberately unbounded ordinary-post control. `publication` uses one detached-result slot, one worker at a time, and owner-side document/text/project/request revision validation before changing the visible decoration. The sample cancels its worker on shutdown and checks revisions regardless of cancellation. The document ID is a fresh open-document generation, so reopening a path cannot accept an old result. For reactive values, use Ranvier core async/stream primitives instead of this host transport.

`LatestPublication` transfers ownership of every published value, including values rejected after disposal. Its apply callback borrows the payload; the optional release callback runs once afterward or when replaced/rejected, on whichever thread owns that operation. Release must be thread-safe and cannot access native handles. Dispose clears pending payloads; a delivery already claimed may complete. Revision validation is application policy, not inferred by the generic slot.

