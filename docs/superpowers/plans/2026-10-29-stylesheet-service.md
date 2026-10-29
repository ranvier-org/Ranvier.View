# C07 stylesheet service implementation

Implements C07 in the shared native API task document. All 103 selected groups remain in scope; this is the C07 contract/file decision required by Task 0.

## Shared contract

Add `src/Ranvier.Dom/StyleSheets.fs`, also compiled/packed by Fable.Ranvier.Dom after Generated/Dom.fs. Keep generated DOM sources canonical. New public types:

- `StyleSheetMedia = MediaText of string | MediaList of MediaList`.
- `StyleSheetOptions = { BaseUrl: string option; Media: StyleSheetMedia; Disabled: bool }`, with `Default` using no explicit base URL, empty media text and disabled=false.
- `StyleSheetService.SupportsBaseUrl: bool` reports explicit base URL support; unsupported explicit options fail instead of silently using the page's base URL.
- `StyleSheetService.Create(options: StyleSheetOptions): CSSStyleSheet`, bound to one document/realm.
- `StyleSheetService.ReplaceAsync(sheet: CSSStyleSheet, text: string): Async<CSSStyleSheet>`: cold operation; parsing begins when started on the owning loop, completion/error is asynchronous, result preserves sheet identity. Cancellation suppresses delivery without undoing parsing that already happened.
- `StyleSheetService.GetAdopted(scope: Node): CSSStyleSheet array`: returns an ordered snapshot for a Document or ShadowRoot. Mutating that array does not adopt sheets.
- `StyleSheetService.SetAdopted(scope: Node, sheets: CSSStyleSheet array): unit`: replaces the ordered list atomically through the engine, preserving its same-document and constructed-sheet validation.

Existing `CSSStyleSheet.replaceSync` remains the synchronous operation. The browser provider, compiled only for Fable, uses the realm's CSSStyleSheet constructor, native Promise replace and Array.from for adoption snapshots. The native provider uses typed ABI factories, native replacement parsing and the host microtask checkpoint. It guards thread, session and disposed handles, preserves duplicate reference accounting and anchors constructed sheets to the document.

The service inherits IDisposable: disposal cancels outstanding Async operations exactly once and prevents new requests. Native host disposal also cancels pending operations; an operation started after disposal fails without calling the engine. A cold operation started off the owning native thread fails through its Async error continuation without parsing. Cancellation before start prevents parsing; cancellation after parsing preserves the resulting sheet but produces a cancellation terminal outcome. No queued success/error continuation may run after cancellation or disposal. Cancellation from another thread is marshalled to the owning host loop while it remains alive; host disposal terminates pending work even if that loop will no longer be pumped.

Public provider entry points are `Ranvier.Dom.StyleSheets.forDocument(document: Document): StyleSheetService` in `src/Fable.Ranvier.Dom/BrowserStyleSheets.fs` and `Ranvier.View.WebCore.StyleSheets.forHost(host: WebCoreHost): StyleSheetService` in the backend file. The browser constructor comes from the document's own Window; a document without a Window is unsupported. The browser provider uses a fresh Array.from snapshot for GetAdopted and validates scope kind before assignment. DOM errors cross the facade as StyleSheetException with the engine's name and message.

The native engine's replace implementation parses synchronously then resolves its promise. A real Chromium probe likewise allowed immediate replaceSync and insertRule between two replace calls; both promises completed in request order and the second replacement supplied the final rules. Shared acceptance pins that observable ordering, immediate mutation and final state instead of introducing a pending-mutation lock absent from the inspected providers. Unsuccessful replacement must leave a previously valid sheet intact.

Observed Chromium limitations are explicit: it ignores the WebKit baseURL constructor option, so the provider probes validation and rejects explicit options when unsupported. Native tests verify actual explicit URL resolution; Chromium tests verify capability=false and rejection, while default document-base resolution is shared. Chromium also partially changes adoption before rejecting an invalid list; the facade snapshots and restores the previous list on that synchronous error. These are recorded provider differences, not evidence that all browser engines implement WebKit's constructor options.

## Native bridge

Add explicit configured attribute bridges for Document/ShadowRoot.adoptedStyleSheets. Their source IDL uses a JS wrapper with type any; the native transport uses a retained sequence of CSSStyleSheet handles over TreeScope.adoptedStyleSheets/setAdoptedStyleSheets. Mark these records as facade bindings, preserve the original shared DOM declarations, and advertise capability only when both accessors are generated. Do not add JS global-object requirements or emulate style application.

## Files and acceptance

- Native: gen/dom.json, gen/domgen.py, generated outputs; tests/generator/test_domgen.py and tests/abi/cssom_surface.py. Verify document/shadow adoption, order, duplication, clearing, non-constructed/foreign-document atomic errors, replacement effects and released sequence handles.
- Core: the shared service file, Fable's BrowserStyleSheets.fs and both package projects, including all three files in the packed Fable source project. Check fresh semantic/API impact, both frameworks and all four package consumers.
- Views: `src/Ranvier.View.WebCore/StyleSheets.fs` after Host.fs, plus the project entry. Provider constructor `StyleSheets.forHost(host: WebCoreHost): StyleSheetService`.
- Shared fixture: extend NativeApi.Conformance with identical document/shadow style effects, replacement identity/errors, snapshot independence, ordering, cancellation and microtask completion assertions. Native runner pumps host microtasks; browser runner awaits the fixture. Run Windows JIT/NativeAOT and Chromium locally, then the existing platform lanes.

Constructor acceptance includes default media/disabled values, both media alternatives, explicit/default base URL, invalid base URL errors and resulting relative resource URL resolution. Async acceptance includes overlapping replace calls, intervening replaceSync/insertRule, errors, cancellation before start/before delivery, service disposal and native host disposal while completion is queued. Snapshot reads and full replacements must preserve sheet object identity, order and duplicates. Record these exact signatures and routes in contracts.json C07; retain an explicit pending API-snapshot state until semantic extraction verifies the persisted declarations.

Keep ABI/member generation deterministic and old records unchanged. Use focused checks during implementation; full backend suites run at the next integrated batch boundary. Native PR12 is the current foundation; stack subsequent work without rebasing it. C07 stays unchecked until provider, package and platform acceptance is verified.
