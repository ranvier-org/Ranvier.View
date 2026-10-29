# D09/D11 remaining element operation proof

Base: Views PR10 head `0fe6d91aa5046b34b00a6d092153d6be390041fb`.
Use official Core alpha.12, CSS DOM alpha.1 and native preview.11. Preserve C11 separately.

- [x] D09: exercise the actual shared SVGElement.viewportElement facade on outer/nested SVG,
  grouped descendants, reparenting and detached trees. Assert closest viewport node identity
  and null for roots/orphans when the provider exposes the selected getter. Report absent
  browser properties explicitly rather than silently substituting ownerSVGElement.
- [x] D11: invoke typed marquee start/stop and prove rendered content moves, stops and resumes
  with bounded timer-driven checkpoints. Include detached calls and repeated operations.
- [x] D11: compile the typed source attachmentIdentifier getter and record its exact runtime
  result/error per provider. Distinguish compile availability, build feature support, runtime
  enablement and browser property presence. Do not enable unrelated attachment functionality.
- [x] Run focused Windows JIT, Chromium and NativeAOT gates with exact assertion guards,
  clean teardown and released dependencies; capture source/FCS qualifications and evidence.
- [ ] Request independent review, commit bounded fixture/wiring/evidence changes, stop owned resources.

Pinned source: WebKit `b95c91b1c0ee43cc86d25ff7af73038f67a88e04`.
SVGElement.idl declares nullable viewportElement; its source walks ancestor svg/image/symbol
nodes. HTMLMarqueeElement start/stop delegate to its renderer when present.
HTMLSourceElement.attachmentIdentifier is nonstandard, conditional ATTACHMENT_ELEMENT,
and gated by AttachmentElementEnabled in the source IDL. The public contract can compile
even when a given binary/profile cannot execute the getter.

Fresh FCS Native.fsproj check: clean, zero diagnostics. Sage bare-session typed consumers
compile all four selected operations against the existing released contract assembly.
Local.Build.props contains restore configuration/cache paths only; no private source/runtime override.

## Results

Windows x64 JIT, Windows x64 NativeAOT and pinned Chromium each pass **30 D09 viewport**
and **26 D11 operation** assertions. The full shared runners, existing Tab traversal,
25 native context/lifetime assertions and teardown pass. Exact count guards cover both additions.
These are source Views consumers of official Core alpha.12/CSS DOM alpha.1/native preview.11;
released Views backend/platform integration is owned by the parent lane and remains unverified here.

- `SVGElement.viewportElement` uses the actual Fable getter in Chromium. Closest viewport,
  repeated identity, grouping, reparenting, attached/detached trees and root/orphan null agree.
- Marquee checks observe child rectangle movement after start, eight stationary timer checkpoints
  after stop, and movement after resume. Detached and repeated calls preserve content/ownership.
  The checkpoint yields 20ms while native pumps `host.Cycle` and browser processes its event loop;
  movement has a bounded 100-checkpoint deadline. Teardown stops the marquee before removal.
- Source getter compiles on both providers. Native Windows returns an empty **string**, repeatedly
  while detached, attached and removed; Chromium returns JavaScript **undefined**, not null/string/error.
  The classifier preserves this distinction without coercion or substituting another property.
- The native getter is compiled behind `ENABLE(ATTACHMENT_ELEMENT)` and returns exact
  `RV_ERR_UNSUPPORTED` when that build flag is absent. The pinned Windows binary takes the callable
  path. Unassociated sources return `nullAtom()` through `AttachmentAssociatedElement`, serialized
  as an empty string. Other native platforms have not been run in this lane; their harness permits
  only the exact empty string or `NotSupportedException` profile, with the actual outcome logged.
- IDL `EnabledByDeprecatedGlobalSetting=AttachmentElementEnabled` controls JavaScript exposure;
  its pinned default is false. Native direct generated glue does not evaluate this exposure gate.
  Successful direct ABI access proves getter availability, and does not establish that attachment
  rendering/global enablement is active. No attachment feature or setting was enabled for this test.
  Chromium exposes no value for the typed selected getter in this profile.

Commands (all prefixed with `rtk`; external gates used owned Sage leases):

```text
dotnet build tests/NativeApi.Conformance/Native.fsproj -c Release --nologo
dotnet tests/NativeApi.Conformance/bin/Release/net10.0/Native.dll
dotnet publish tests/NativeApi.Conformance/Native.fsproj -c Release -r win-x64 -p:PublishAot=true -p:OS=Windows_NT -o artifacts/d09-d11-aot-official11 --nologo
artifacts/d09-d11-aot-official11/Native.exe
dotnet fickl tests/NativeApi.Conformance/Browser.fsproj --outDir tests/NativeApi.Conformance/generated --configuration Release
node tests/NativeApi.Conformance/browser.mjs
```

Browser dependency path: `NATIVE_API_BROWSER_PACKAGE=C:/wk/ranvier-views/tests/NativeApi.Conformance/package.json`.
Native runtime hashes in JIT output and AOT publication exactly match the official preview.11 files:
host `1b401d382add034921d822b650cd11e85f12d3911f121f5e17dd0ccd508dccc1`;
WebCore `6e2ede0eef34e9e699cf6434a2dd4c8723d692254d141f594cdcf3ded91c720c`.
Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 AOT warnings remain.

Fresh FCS Native/Browser checks are clean with zero diagnostics. Complete untruncated referenced
type results are in `docs/research/native-api-delivery/element-operations-public-api.json`.
Sage typed consumers and persisted fixture loading pass; pure negative controls reject stationary
movement and changing stopped geometry, with corresponding positive controls passing. XML audit:0;
comment hygiene: no FOR-REVIEW comments. Independent parent review found no actionable correctness issue.

No production changes, browser facade replacement or attachment enablement is included.
Existing acceptance documentation remains owned by the tracker. Platform/traced/public Views
acceptance stays open until its own applicable gates run.
