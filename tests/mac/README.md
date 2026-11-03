# Managed IOSurface transport checks

## Current source integration

Core and view state: this integration uses the existing host/session owner and SDL window disposal.
The retained View-owned state is the imported native texture and its in-flight GPU frame lease; it is
transport state, with no additional reactive ownership or scheduling primitive.

The reviewed `ae534a5` product and `c7cc031` framework-free checks are forward-applied to View47 source.
Current Core58, formatter/lifetime fixes and package metadata are preserved. The existing Native preview11
package does not provide the new managed IOSurface bindings: until a matching canonical ABI64/349
Interop package is allocated, compile this source only with the explicit `UseLocalRanvierWebCore=true`
override against that canonical source. Do not publish these changes with the old default Interop pin.
No new View or Native version is allocated here.

The prior actual Mac JIT/AOT proof used a private derivative Host with older coherent engines. It is
separate from the required future matching producer/runtime and public package qualification. Preserve
its native pixel failures and do not infer final producer acceptance from the source merge.

For a future public consumer, the existing verifier accepts `--rid osx-arm64 --abi 64` together with exact
`--native-version`, `--view-version`, `--view-package` and `--flavor`. It checks the public NuGet package
origin, matching Slim/GPU source cohort, selected native assets, complete package/output file hashes and
opposing normal/traced families. This is static package admission, not a loaded-runtime or Metal proof.
The current Windows verifier default stays `win-x64`; its existing controls remain required.

Before adoption run normal/traced unfiltered tests and the static `AotIOSurface` consumer on the matching
Mac GPU cohort. Require dynamic code false, all four cases/53 assertions, actual loaded ABI64/capabilities
and hashes, and no CPU fallback. Retain resize, scale, scroll, input lifetime and second-host controls as
independent native gates. After allocation/publication repeat with package references and fresh caches;
the source override is explicitly forbidden in that public route. Keep all dispatches unarmed until QA
has reviewed the exact source, package identities and activation.

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
