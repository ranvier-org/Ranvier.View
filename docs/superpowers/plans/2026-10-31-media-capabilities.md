# B06 media capability queries (native provider)

## Core and view state

- **Core primitives used:** none in the provider. A query is a `ResourceOperation` from the H05 contract, settled by
  `ResourceArbiter` on the host's owner context. A view that queries in a scope starts the query under that scope and
  disposes the operation with `onCleanup`; disposing a pending query delivers `ResourceFailure.Cancelled`.
- **View-owned state:** the provider's table of completion contexts for native queries in flight. It is native callback
  state: the core has no representation of a native completion callback.

## Design

- `Ranvier.Dom` `MediaCapabilitiesService` (`MediaCapabilities.fs`): `DecodingInfo` and `EncodingInfo` over typed video
  and audio configurations. A configuration the engine rejects fails with `LoadFailed "TypeError"`.
  `BrowserMediaCapabilities.forDocument` calls the realm's `navigator.mediaCapabilities`.
- `Ranvier.View.WebCore/MediaCapabilities.fs`: `MediaCapabilities.forHost` marshals the configurations into
  `RvMediaVideoConfiguration` and `RvMediaAudioConfiguration` and calls `rv_media_decoding_info` or
  `rv_media_encoding_info`; a SUCCEEDED completion reads `rv_media_capabilities_info`. RV_ERR_DOM_EXCEPTION is the
  engine's TypeError.

## Proof

`tests/NativeApi.Conformance/MediaCapabilityChecks.fs` runs 107 shared checks on native JIT, NativeAOT and Chromium:
the 21 decoding and 5 encoding cases of Ranvier.WebCore `tests/fixtures/media-capabilities-cases.json`, each settled
after the starting call returns, each engine's TypeError set, cancellation before delivery, a null content type and a
disposed service. Native answers every valid case unsupported, not smooth and not power efficient: no flavor has a media
engine configuration provider. WebKit and Chromium differ on five TypeError cases (bucket containers without exactly
one codec, an extra MIME parameter, record encoding). The native runner adds host disposal before delivery.
