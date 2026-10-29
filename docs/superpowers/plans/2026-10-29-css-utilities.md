# Shared CSS utilities: C05 contract and acceptance

The active goal still includes all 98 required and five conditional groups. This batch implements the complete C05 identifier-escaping operation; C03 support queries require the existing engine parser and remain separate required work.

## Contract fixed before implementation

- Public API: `Ranvier.Dom.CSS.escape : identifier:string -> string`, supplied by both Ranvier.Dom and Fable.Ranvier.Dom, including their traced variants.
- Route: pure shared F# facade. It accepts complete UTF-16 strings, including embedded U+0000 and unpaired surrogate code units. It requires neither a live document nor page JavaScript on native/server targets.
- A null string reference follows CSS.escape's DOMString conversion and becomes the text `null`. The typed function requires one argument; arbitrary JavaScript coercion is outside its string signature.
- Serialization profile: [CSSOM 26 August 2021, identifier serialization](https://www.w3.org/TR/2021/WD-cssom-1-20210826/#serialize-an-identifier). Pinned WebCore b95c91b1c0ee43cc86d25ff7af73038f67a88e04 and the installed Chromium CSS.escape implementation both preserve code units at or above U+0080, including C1 controls and unpaired surrogates. The newer October 2026 draft references a narrower non-ASCII identifier set; do not claim that newer draft profile is implemented.
- Native ABI reason: current string inputs are null-terminated UTF-8. Routing this operation through them would truncate embedded U+0000, whereas the shared facade can serialize complete input. No ABI exports or common generated DOM interfaces need to change.
- No ownership, thread, callback or cancellation state; no additional package dependencies or reflection dispatch.

## Owners and changes

- Core: new `src/Ranvier.Dom/CSS.fs`, shared by both contract packages; both package project files and their standalone Fable source ordering; focused normal/traced native/Fable package consumers; alpha.9 release preparation and README.
- Views: `tests/NativeApi.Conformance/CssEscaping.fs`, both runners/project files, Chromium oracle loop, capability/contract/acceptance evidence and the alpha.9 dependency when available.
- Existing native implementation and paused work remain outside this batch's edits.

## Verification

1. Observe the missing shared API fail in SageFs, implement the function in the session, check leading digits, controls, NUL, hyphens, punctuation and UTF-16 preservation, then persist and rebuild the project session.
2. Pack and consume all four contract variants with fresh caches, checking executable behavior in .NET and Fable rather than only type presence.
3. Run the same fixed vectors and real selector round trips in native JIT, NativeAOT and Chromium. Compare compiled F# escaping against real CSS.escape across every UTF-16 code unit at multiple positions, including a hyphen prefix.
4. Verify public packages and platform CI before marking C05 complete. Record the supported serialization profile and unverified platforms explicitly.

Preflight: the Core project type-checks cleanly. FCS find/impact reports no existing CSS module or CSS.escape symbol; this is an additive public API, not an internal-only change despite the tool's generic not-found wording. SageFs evaluation of Ranvier.Dom.CSS.escape fails because CSS is absent. Direct Chromium probing confirms C1 controls, directional marks and unpaired surrogate preservation in its current implementation.
