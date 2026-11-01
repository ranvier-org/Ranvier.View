# R12 snapshot and export options (native provider)

## Core and view state

- **Core primitives used:** none in the provider. A capture is a `ResourceOperation` from the H05 contract, settled
  by `ResourceArbiter` on the host's owner context; a view that captures in a scope disposes the operation with
  `onCleanup`, and disposing a pending capture delivers `ResourceFailure.Cancelled`.
- **View-owned state:** the provider's table of completion contexts for native captures in flight, which is native
  callback state the core cannot hold.

## Design

- `Ranvier.Dom` `SnapshotService` (`Snapshots.fs`): `Capture` of the viewport or an element with a scale, a crop, a
  color space and an `ImageEncoding` (shared with canvas export), and `Available`, `SupportsColorSpace` and
  `SupportsEncoding`. `BrowserSnapshots.forDocument` reports capture unavailable: an ordinary browser context has no DOM
  capture member, and `Capture` raises NotSupportedException rather than paint another subject.
- `Ranvier.View.WebCore/Snapshots.fs`: `Snapshots.forHost` marshals `RvSnapshotOptions` and calls `rv_host_snapshot`;
  a delivered snapshot is copied with `rv_snapshot_result`. Color space and encoding support follow the
  `snapshot-display-p3` and `image-encoding-*` capability facts, and an unsupported one raises NotSupportedException
  before the native call.

## Proof

`tests/NativeApi.Conformance/SnapshotChecks.fs` runs 23 shared checks on native JIT and NativeAOT (element size at the
device scale and at 1, 2 and 0.5, RGBA8 pixels, a crop, PNG, JPEG and WEBP signatures, display-p3 unsupported on slim,
argument errors, cancellation, a disposed service) and 6 on Chromium (capture unavailable for the viewport and an
element, no color space or encoding, nothing delivered, a disposed service).
