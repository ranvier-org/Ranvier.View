# Managed IOSurface transport checks

These checks require the matching ABI64/349 macOS GPU host and canonical managed
interop from the same source cohort. An ABI63 P10/P11 host cannot qualify this
source. The native and SDL source pins remain owned by the native producer.

Build the test-only native Metal producer on macOS 26 ARM64:

```sh
python3 tests/mac/build_metal_input.py --output "$RUNNER_TEMP/metal-input"
export RANVIER_MAC_INPUT_HELPER="$RUNNER_TEMP/metal-input/libRanvierMacInputFixture.dylib"
```

Build `tests/Ranvier.View.WebCore.Tests/Ranvier.View.WebCore.Tests.fsproj` with
`UseLocalRanvier=false`, `UseLocalRanvierWebCore=true`, `RanvierWebCoreRoot` set to
the matching canonical interop source, and `RanvierWebCoreNativeDir` set to a
stage containing `runtimes/osx-arm64/native`. Use separate normal/traced output
and package-cache directories, the pinned SDK, and record their resolved graphs.
Run the built test executable with `--filter-test-list "IOSurface transport"`.
This is a focused check, not a full-suite or NativeAOT qualification.

The GPU runtime should run the output, native Metal input, and real-frame retry
cases. The unavailable-input case is intentionally ignored on that profile.
The output case checks odd CSS dimensions at DPR 1, 1.5 and 2, BGRA channel
interpretation and GPU presentation without CPU fallback. Readback occurs only
in the explicit pixel assertions. The input case completes native Metal writes
before binding/notifying and verifies update, caller-storage release, failed
replacement and unbind. The retry case injects completion/release failures only
after acquiring a real frame and checks owner-only cleanup before host teardown.

Retain the native producer's clip/scroll/transform/opacity/stacking and texture
lifecycle matrix separately; these managed checks do not replace it. Capture
the helper, native payload and managed assembly hashes before and after each run.
Actual SDK/runtime results remain pending until this recipe executes against a
coherently built candidate.
