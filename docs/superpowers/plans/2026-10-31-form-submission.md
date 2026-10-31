# D14 form submission

Route: Engine (ranvier-org/Ranvier.WebCore#23). Native producer: Native34/ABI42 over source patch 0037, stacked on
H13 (Native33/ABI41), G09 and H11. Managed: DOM alpha.37 (Core) and Views 0.1.27 / backend alpha.27.

## Core and view state

This group touches the shared DOM contract and the WebCore host adapter, not the view renderer.

- **Core primitives used:** none. A form submission is DOM state the engine produces after its submit and formdata
  events; the service reports it and keeps no reactive graph of its own.
- **View-owned state:** none beyond the native adapter: the registered native handler delegate, the handler list of
  the service and the one-service-per-host marker. Each is a native registration with no core equivalent.

## Contract (Ranvier.Dom)

- `FormSubmission` (hand-written, `FormSubmission.fs`): `Method`, `Url`, `Target`, `ContentType`, `Body`, `Form`
  and `Submitter`.
- `FormSubmissionService`: `Subscribe handler -> IDisposable`. A submission reaches the handlers from a later task,
  after its submit and formdata events and interactive validation; a later submission of a form replaces its pending
  one; `dialog` method forms close their dialog as usual; a submission with no handler is dropped.
- Browser provider: `BrowserFormSubmission.forDocument` (Fable.Ranvier.Dom). It cancels the submit events that reach
  the Window uncancelled and replaces `HTMLFormElement.prototype.submit` in the realm until disposal, then encodes
  the entry list as the HTML form submission algorithm does, in UTF-8. Native provider:
  `Ranvier.View.WebCore.FormSubmissions.forHost`, over `rv_host_set_form_submission_handler`.

## Native surface (ABI42, additive, `rv_abi_has("form-submission")`)

- Patch 0037 adds `LocalFrameLoaderClient::shouldScheduleFormSubmission`, called by `FrameLoader::submitForm` after
  the sandbox and CSP checks and before target-frame resolution. The host client copies the submission into an
  `RvFormSubmission` and posts it to the handler; the frame loader skips scheduling it, so the host document is not
  unloaded.
- Host settings: forms are unsandboxed, interactive form validation is on, and the default text encoding is UTF-8.

## Qualifications

- text/plain bodies follow the HTML text/plain encoding (`name=value` CRLF, unescaped). The Chromium 156 producer
  oracle records a percent-encoded body for the same submission; the shared fixture compares provider output, which
  both providers encode per the specification.
- A browser application listener that cancels a submission is registered on the form, the document or the Window
  before the service.
- File parts carry their headers and no file bytes.

## Verification

- `tests/NativeApi.Conformance/SubmissionChecks.fs` (26 shared checks) on native JIT, NativeAOT and Chromium: GET,
  urlencoded, text/plain and multipart encodings, submitter overrides, `formtarget`, `submit()`, cancellation,
  interactive validation and `formnovalidate`, coalescing of two submissions of a form in one task, dialog forms,
  subscriptions, a second service and disposal.
- Native lifetime (7 checks): exception types, thread rejection, a submission pending at host disposal and service
  disposal after the host.
- Native producer: `tests/abi/form_submission.py` against the Chromium 156 oracle with its teardown part, 30 repeats.
