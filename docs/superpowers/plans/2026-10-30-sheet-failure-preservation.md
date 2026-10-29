# C07 failed replacement preservation

Continue from reviewed D09/D11 commit `46d9680`, merging current main without rebase.
Use official Core alpha.12, CSS DOM alpha.1 and native preview.11; no production changes.

- [x] Retain the inline sheet, live rule list, each top-level rule and a declaration object.
  Use multiple meaningful rules including a grouped body and an attached matching element.
- [x] Attempt asynchronous replacement of the non-constructed sheet with the same rule count
  but different declarations. Assert exact NotAllowedError and asynchronous delivery.
- [x] Compare every serialized rule before delivery, after rejection and after another checkpoint;
  preserve retained identities, parents/declarations and computed matching-element dimensions.
- [x] Replace catch-all unsupported-baseURL and disposed-service tests with exact observable
  provider errors. Preserve cancellation coverage and distinguish CLR/Fable exception identity.
- [ ] Run focused shared JIT, Chromium and NativeAOT checks with exact count guards and teardown.
  Request independent review, commit and open one source PR containing D09/D11/C07 proof.

Canonical facade inspection: native and browser `active()` throw ObjectDisposedException;
browser explicit BaseUrl when SupportsBaseUrl=false throws NotSupportedException before construction.
Native invalid base URL uses engine StyleSheetException(NotAllowedError); browser rejects unsupported
BaseUrl first. ReplaceAsync wraps engine errors as StyleSheetException and reports them asynchronously.
Service disposal cancels pending completion. Its source raises ObjectDisposedException on requests
after disposal, but Fable compilation erases this specific BCL type (qualification below).

No selected API signature changes. Existing cancellation-after-parse behavior remains covered.
Failed replacement of a non-constructed sheet is the selected error case; malformed CSS is parsed
per normal CSS recovery rules and is not assumed to reject.

## Evidence

The C07 fixture now runs **78 assertions** per provider (previously40), with exact runner guards.
The earlier count-only predicate demonstrably accepts same-count corrupted declarations in a
Sage negative control; the full serialized-rule comparison rejects them. Its identical-rule
positive control passes. The persisted fixture loads with clean types in Sage.

Windows x64 JIT, Windows x64 NativeAOT and Chromium each pass the final78 assertions against
official dependencies. Chromium exposed FABLE0112: `ObjectDisposedException` type tests
compile to false in Fable5.24.0. The observed exception has constructor `Exception`, message
`StyleSheetService`, and no DOM error-name field. The fixture now asserts this exact browser
descriptor and exact CLR `ObjectDisposedException` for Create/GetAdopted/SetAdopted/ReplaceAsync.
All three final runs pass with exact profile descriptors. D09 viewport30 and D11 operation26
also pass in these runs, along with existing context/lifetime, Tab traversal and clean teardown.

This is a concrete exception-identity limitation. The public `StyleSheetService` summary directs
callers to dispose the provider with its view, and `ReplaceAsync` remarks promise cancellation of
pending completion on service/native-host disposal. Public XML docs promise no typed exception
identity for later requests and expose no shared name-based disposal-error contract. The observed
browser error is not labeled ObjectDisposedException parity; the limitation creates no new
prerequisite for the original C07 preservation/cancellation scope.

All existing cancellation and provider/host-lifetime checks remain. Failed replacement preserves
every top-level serialized rule (including grouped declarations), sheet/list/rule/declaration
identities and parent sheets, and the attached matching node's73px width/29px height. Assertions
run before rejection delivery, after exact NotAllowedError and after a further checkpoint.
Final broad platform/traced/released Views acceptance remains with the central lane; frozen
published0.1.2/0.1.3 consumer profiles are unchanged.

Commands use the same official restore configuration and lease discipline as the element-operation
plan. Native publication: `dotnet publish tests/NativeApi.Conformance/Native.fsproj -c Release
-r win-x64 -p:PublishAot=true -p:OS=Windows_NT -o artifacts/d09-d11-c07-aot-official11 --nologo`.
Run JIT from `tests/NativeApi.Conformance/bin/Release/net10.0/win-x64/Native.dll` and AOT from
`artifacts/d09-d11-c07-aot-official11/Native.exe`. Browser uses the unchanged Fickl/Chromium harness.
Fresh FCS checks for both conformance projects are clean; XML audit0, formatting and comment checks pass.
Existing FSharp.Core IL3053/IL2104 and SameValueZero IL2075 AOT warnings remain.

Independent parent review of the complete fixture, provider callbacks, count guards and plan
found no actionable correctness issue. Own Sage session25525738 is stopped and all leases released.
