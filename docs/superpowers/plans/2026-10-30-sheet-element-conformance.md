# D12 stylesheet and link element conformance

Stack on reviewed Views PR12 at `d37625ff9db7e062eae8d36af15bd5461d8cf616`.
Selected issue: https://github.com/ranvier-org/Ranvier.WebCore/issues/21.
Keep official Core alpha.12, CSS DOM alpha.1 and native preview.11. Scope is a new shared
SheetElements fixture, runner wiring and evidence; report any demonstrated missing primitive.

Existing Stylesheets58/SVG31 already cover basic inline/SVG sheet association, owners,
media/disabled cascade and CSSOM mutation. D12 adds live element content/type replacement,
associated-sheet retention, real link resource completion/error and authored Ranvier.Css output.

- [x] Render typed Ranvier.Css rules into an attached style element; inspect its associated
  sheet/owner and actual computed dimensions. Replace text and preserve old retained rule objects.
- [x] Exercise selected style/link properties and live token lists, media/disabled changes and
  associated sheet identity. Avoid claiming that token reflection proves unrelated fetch features.
- [x] Attach load/error listeners through the actual shared EventTarget facade. Load generated
  CSS from deterministic data URLs, replace href and observe a second completion/new sheet/effect.
  A deliberately malformed base64 data URL must produce a real error with no external network.
- [x] Verify events target the owner element, actual completion counts, loading-state/sheet
  observations, matching cascade and cleanup. Keep data-origin CSSOM security explicit if applicable.
- [ ] Focused actual Windows JIT/NativeAOT/Chromium proof, exact counts, clean lifecycle; independent
  review before a bounded source PR. Platform/released-package acceptance stays open separately.

Pinned source `b95c91b1c0ee43cc86d25ff7af73038f67a88e04` declares HTMLStyleElement.disabled/media/type/blocking,
HTMLLinkElement's19 own properties and inherited nullable LinkStyle.sheet. Fresh FCS check is clean.
Referenced public signatures and Sage typed consumers confirm the event callback overloads and sheet serializer.
No public signatures are changed in this fixture batch.

## Evidence

Windows x64 JIT, Windows x64 NativeAOT and real Fable Chromium each pass109 shared D12 assertions using official
Core alpha.12, native preview.11 and CSS DOM alpha.1.
Both conformance projects freshly typecheck cleanly with zero FCS diagnostics. Exact109
assertion guards prevent a partial fixture from passing. Production APIs remain unchanged.

Typed `Ranvier.Css.Rule.style`/`Selector.className`/`Css.width`/`Css.height`/`Declaration.important`
are serialized with `Stylesheet.render`, associated with the actual HTMLStyleElement.sheet,
and loaded via data CSS through HTMLLinkElement.sheet. CSSOM rule declarations and parent
identity match, and actual computed dimensions agree. Replacing inline style text creates a
new associated sheet while the retained old rule stays readable with its original parent/text.
Changing CSS/non-CSS type removes/restores the active association and matching effect.

Link callbacks observe exactly `load, load, error, load` for initial load, href replacement,
malformed-base64 resource and recovery, with correct target/currentTarget identities. Listeners
are then removed before loading a third successful generated payload. Its distinct97px/43px
computed dimensions and another owner-loop checkpoint prove resource completion while the
recorded callback count stays4. Media/disabled changes operate on the current association,
which can replace its sheet. A retained former sheet's disabled/media fields are not asserted
to mirror a link's later state. Computed width/height settle to the current generated97px/43px or the
explicit inline19px/13px fallback as appropriate, under bounded resource/effect waits. The
disabled inline style also restores the actual19px fallback before being reenabled.

Malformed `data:text/css;base64,%%%` yields a replacement associated sheet, actual19px fallback,
and exact SecurityError when its rules are inspected, in both engines. Successful data CSS is
readable in both. The failed resource's protected rule list is not claimed empty/readable;
resource error reporting and recovery are fully exercised. Initial probes corrected unsupported
assumptions about link-to-retained-sheet flag mirroring and error-sheet null/retained identity.
No per-provider failure profile or missing engine primitive is inferred from those assumptions.

Selected token-list/property tests cover style blocking and link relList/sizes/blocking plus
the19 own link declarations. Reflection does not claim execution of preload/image/SRI/fetch
priority policies; integrity/crossOrigin test values are reset before the stylesheet loads.
Existing Stylesheets58/SVG31 remain the basic inline/SVG rule-editing/identity proofs.

All resource fixtures are deterministic data URLs. No external HTTP request, host credential,
new loader facade or JavaScript evaluation bridge is used. Broader platform/traced/released
Views acceptance remains open separately. Existing parent batches and teardown remain green.

Final native command: `dotnet publish tests/NativeApi.Conformance/Native.fsproj -c Release
-r win-x64 -p:PublishAot=true -p:OS=Windows_NT -o artifacts/d12-aot-official11 --nologo`, then
run the emitted Native.exe and corresponding win-x64/Native.dll under owned Sage run leases.
Browser uses the existing Fickl build and Playwright Chromium harness. Whole shared totals:
1289 native /1302 browser, plus CSS.escape oracle/context/lifetime/Tab checks.
Official runtime hashes match in AOT output: host
`1b401d382add034921d822b650cd11e85f12d3911f121f5e17dd0ccd508dccc1` and WebCore
`6e2ede0eef34e9e699cf6434a2dd4c8723d692254d141f594cdcf3ded91c720c`.
Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 AOT warnings remain.
XML audit0, formatting and comment checks pass. Complete untruncated referenced type results
are captured in `docs/research/native-api-delivery/sheet-elements-public-api.json`.

Independent parent review found no task/correctness/lifecycle defects, and requested two proof
strengthenings: actual disabled-style fallback and a distinct completed load after unsubscribe.
Both were added and pass all three final engine modes, raising the exact count from105 to109.
Owned Sage sessions0a06b05f/32c4ab8f are stopped; leases are released. No production repair was necessary.
