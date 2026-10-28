# Native application gap closure

Goal: close every non-paused gap in the [settled specification](../specs/2026-10-28-native-app-gaps.md), with merged changes, published dependencies and exercised runtime behavior.

Architecture: WebKit IDL and `gen/dom.json` generate the native ABI, F# interop and shared DOM interfaces. Ranvier owns two thin interface projects. Ranvier.WebCore publishes managed interop and a five-RID native meta-package. Ranvier.View consumes those packages without Fable on .NET. SDL owns the window and platform event loop; native platform bridges retain WebCore's thread and lifetime contracts.

Tech: Python generator, C++ WebCore host, F#/.NET 10/Fable, SDL3, Windows UI Automation, GitHub Actions and NuGet trusted publishing.

## Constraints and acceptance

- Keep the settled Answers, repository ownership, CRLF, RTK, leases, message files and conventional PR titles. Never rebase pushed history or handle credentials. Existing user authorization covers implementation, PRs, merges and completion.
- Preserve completed macOS/AOT behavior and core docs CI. Do not touch other agents' checkouts.
- Paused: anime port, macOS 15, Linux/macOS GPU, size trimming, dependency mirror, sccache and old feasibility evidence commits.
- Require actual package restore and runtime assertions. An open PR, generated file, successful compile or manifest-only native check does not close a runtime gap.
- Report hardware checks that cannot be reproduced honestly; seek hosted real ARM and meaningful density/input tests.

## Ordered implementation batches

### 1. ABI 21 generator and contracts

Files: native `gen/idl.py`, `gen/domgen.py`, `gen/dom.json`, generated DOM headers/glue/interop/report, generator fixture tests and ABI driver DOM cases.

1. Add failing fixture tests for namespace creation, second documents, union inputs/results, sequences and variadics.
2. Implement transport mappings, stable names, validation and retained result ownership. Preserve old symbols and dictionary lock order.
3. Emit one F# interface source set, overloads per union arm, ParamArray variadics and discriminated union results. Include XML contracts.
4. Generate from pinned WebKit, check determinism, compile real native glue and run actual DOM cases. Measure output and binary-size differences.

### 2. Packages and release

Files: native `packaging/`, `scripts/build.py`, `.github/workflows/build.yml`, canonical interop sources and managed project.

1. Add failing package contract tests, including selecting resources correctly when all RID targets import.
2. Add five-RID `Ranvier.WebCore.Native` meta-package and `Ranvier.WebCore` managed assembly built from canonical sources; no copied interop in consumers.
3. Gate coordinated packages on all five successful ABI21 runtimes; retain OIDC publishing. Verify nupkg dependency/assets and isolated restore/publish.
4. Merge, release and verify published packages from a clean consumer before migrating Views.

### 3. Shared DOM interfaces and Fable decoupling

Files: own isolated core branch, two DOM project files sharing generated sources, core CI generation pin; Views DOM/Fable/backend projects and wrapper generator.

1. Establish explicit project API impact with semantic tools. Keep the core cleanup agent's work separate.
2. Build both interface projects from identical sources; Fable-specific attributes are conditional. Pin the native generator check in core CI.
3. Move View public types and wrappers onto the generated interfaces. Browser components must compile unchanged for native; browser-only extras use explicit Xantham casts.
4. Remove .NET Fable/Xantham references and copied Native files; unpend the four conformance cases and remove mapped-member NotSupportedException paths.
5. Run complete browser/twin/native suites, all analyzer groups and NativeAOT. Pack and publish the now-decoupled backend/presenter with tested dependencies.

### 4. Remaining platform execution

Files: Views/native CI and platform-specific loading, backend and SDL tests.

1. Exercise Linux x64 native loading, complete backend suite, SDL visible smoke and both published apps under a display server. Fix WPE initialization/input/clipboard behavior where tests expose it.
2. Exercise Windows ARM64 on a real hosted ARM runner, including native loading, full backend suite, SDL and NativeAOT smoke/sample.
3. Keep macOS 26 and Windows x64 gates green; record exact platform counts and evidence.

### 5. Windows accessibility bridge

Files: native UIA provider/internal header, host ABI/CMake, independent Windows UIA client tests; Views SDL attachment and fixtures.

1. Prove missing OS tree/action with a separate UIA client.
2. Implement native providers, HWND attachment, identity/tree/geometry/events/action/value/toggle/text contracts, owner-thread routing and shutdown.
3. Attach/detach in the presenter and exercise actual OS discovery and mutations, removed nodes, retained providers and NativeAOT. No managed COM dependency.

### 6. Windows GPU presentation

Files: native GPU exports/package path as needed, Views presenter options and Windows GPU tests/CI.

1. Verify current GPU ABI: accelerated compositing presently reads back RGBA; do not claim an exported texture exists.
2. Implement and exercise Windows GPU presentation using the supported transport; if texture sharing is required, add an explicit lifetime/synchronization contract and test it.
3. Verify accelerated capability/mode and painted/composited output through the visible presenter, including resize and teardown. Keep CPU fallback exercised.

### 7. Land and reconcile all gaps

For every batch: review the complete diff, fix actionable findings, commit with a message file, push a conventional PR, attach it, wait for checks and merge under existing authorization. Verify latest default-branch runs and package consumers. Update the gaps doc and READMEs with evidence and explicit remaining qualifications. Completion requires all non-paused batches, not this plan or open PRs.

## Review focus

1. ABI layout/overload collisions and union/sequence memory ownership.
2. Native main-thread affinity, secondary-document lifetime and provider teardown.
3. Browser/native API parity and accidental Fable dependencies in the .NET graph.
4. RID resource collisions, partial package publication and untested package consumers.
5. False-positive smoke checks: empty tests, software fallback labelled GPU, manifest-only checks labelled runtime, or UIA provider self-tests labelled OS-client verification.

## Execution ledger

- 2026-10-28: macOS/AOT code and docs merged; Views main CI 40666001024 passed. Native main CI 40665494427 still running. Full remaining scope confirmed by user; ABI21, platform/GPU and UIA inventories dispatched independently.
- Ruling: keep explicitly paused work paused under the full-gap request, matching the handover. Cost if wrong: those optional tracks need a separate scope change.
- Package scaffold committed as native `44ffcb4`: four package contract tests pass, both packages pack locally, and an isolated consumer restores all five native runtime dependencies and loads ABI 20. This proves the existing-binary graph only; ABI 21 release verification remains pending.
- Ruling: managed interop stays independent of the native flavor; Views references managed interop plus the same-version native aggregate, or the Windows GPU runtime package. An unconditional slim dependency would collide with GPU native assets. Cost if wrong: consumers must add an explicit native package reference.
- Shared contracts scaffold lives in the isolated core branch `codex/dom-interfaces`; it is not ready to build until the native generator output passes compiler and browser conversion checks.
- Platform PR View#4 is under test. Real Linux loading exposed missing distro libraries, then a 15-byte dirty/full paint mismatch; the platform agent is diagnosing without weakening the assertion. Real Windows ARM64 runner coverage is in progress.
- Windows UIA has passed an independent OS client against an isolated updated host DLL: discovery, geometry, Invoke, name events, Value, Toggle, text ranges and disconnected providers. Production presenter attachment and CI verification remain pending.
- 2026-10-28: View#4 merged as `688feec`; run `40675534807` passed 773 backend tests per Windows x64, real Windows ARM64, Linux x64 and macOS 26 job, with 16 existing skips, visible sample and both NativeAOT app runs. Native main run `40665494427` also finished successfully on all five RIDs.
- Core interface packages are in Ranvier#249 at `942132df`: normal/traced packages pass isolated assembly-identity and Fable/JavaScript consumer checks locally and in clean Ubuntu CI `40688099092`. The IDL drift check passes at generator pin `83d5519d`. Merge/publication waits for the duplicate `HTMLSelectElement.remove()` contract-slot correction and final checks.
- Native WebCore#5 at `8e3cb7d` has passing package-contract and Windows ARM64 jobs in run `40683638755`; the other native builds remain in progress. Listener option dictionaries are on separate reachable branch `codex/dom-listener-options`, preserving the running native build.
- Wrapper migration now uses the canonical JSON model and contract assembly without Xantham. Generation maps 1,077 member implementations to 1,060 native functions, with 17 event/template shims and 13 members lacking concrete native exports. The isolated static wrapper compile found the shared select/remove slot collision; full backend runtime verification remains pending.
- Copied backend interop sources/headers are removed in the working branch. The backend project now references canonical managed interop plus the five-RID native aggregate at a coordinated version, with opt-in local source/runtime overrides. Backend integration is not yet build-verified.
- Windows GPU native commits `aca69fac` and `860e880` pass private real-WebCore/SDL texture-sharing, resize, CPU fallback and teardown checks. The SDL integration also passes local actual-backbuffer checks and an independent UIA Invoke/disconnect client. Coordinated native packaging, JIT/AOT CI and publication remain pending.
- Shared View/Twin migration has a full green Twin run: 1,071 passed and 12 existing ignored. Browser migration and backend event/member migration remain in progress.
- Corrected shared contracts are now core PR249 head `37362e50`, pinned to native generator `7213a5d`; the inherited select/remove slot fixture is green. All four local packages and isolated consumers pass again. The updated main CI is green; updated performance checks remain monitored before merge.
- Isolated canonical wrapper compile and actual ABI21 runtime proof pass with zero build warnings/errors. Runtime checks cover namespace-aware SVG wrappers, secondary-document identity, mixed node/string variadics, omitted/bool/dictionary import overloads, UTF-8 string variadics, owned string unions and sequences, node sequence identity and repeated release/GC. The private runtime's 317 files were SHA256-verified before copying; packaged/released backend execution still follows integration.
- The full browser pipeline also passes: six page builds, 1,205 DOM tests, 51 sugar checks, 41 counter checks, 782 conformance inspections and 33 Chromium tests. Native UIA/GPU integration now passes local JIT and NativeAOT visible sample checks with an independent OS client; published GPU-package CI remains pending.
- Core#249 merged as `f371df76`; master CI `40694699765`, DOM `40694699753`, Perf `40694699792` and dependency submission `40694699775` passed. OIDC publish `40695381647` succeeded, exactly four new interface packages indexed, and fresh official-NuGet normal/traced .NET and Fable/Node consumers passed. Evidence: `C:/wk/dom-public-consumers/aaa016f777db4ddeab8cc5c1e5d3d9b5/public-proof.json`.
- The complete generated-wrapper runtime proof also passed NativeAOT on Windows x64 (native PE32+, no CLR header). FSharp.Core IL3053/IL2104 warnings remain; no new wrapper warning appeared. Proof sources/output live in `C:/wk/wrapper-migration-proof`. Native union tags are validated against canonical literal constants before emission.
- Native PR5 now includes the composed DOM/UIA/GPU batch and artifact-layout repair at `c25dbe83`; final run `40697093058` is in progress. The earlier run passed all five native jobs but failed aggregate packaging because mixed upload paths nested nupkgs. Strict source/cohort checks were preserved.
- Root generator SageFs session `4a6b8f93` is stopped and confirmed NoSession; the MCP ownership error after connection replacement required its exact dashboard session-stop endpoint. Root's unused Serena tree was already closed. No root build/run leases remain.
- Composed View integration is committed as `a27dcb23` for isolated test migration. Fresh backend and SDL checks are clean; the native test project initially reports 131 legacy contract errors and remains in progress. No integration PR has been opened yet.
- Removed all six provisional SDL interop declarations and routed GPU/UIA calls through canonical `Ranvier.WebCore`. The migrated sample builds with zero warnings/errors and passes JIT and NativeAOT strict GPU/UIA smoke against the private combined runtime: 480x320 backbuffer, scale 1, two GPU frames, clipboard and independent UIA tree/Invoke/name-update/disconnect checks. Published-cohort CI remains pending. NativeAOT retains existing FSharp.Core/view reflection, trimming and generic-recursion warnings.
- Local NativeAOT initially failed because this shell lacked the `OS` environment variable, making the compiler infer a non-Windows host despite SDK RID `win-x64`. Setting `OS=Windows_NT` for the actual Windows build process restored publishing; no repository workaround or tool installation was needed.
- Integration exposed two omitted inline style getters: native return type `CSSStyleProperties` was missing from shared contracts. Generator-only fix `e81b32b` adds its IDL-backed marker inheriting `CSSStyleDeclaration`, restoring both getters without changing native outputs. Core#250 merged as `ef19d86b` after CI `40712311463`, DOM `40712311166` and Perf `40712311435` passed. Four alpha.2 packs and isolated normal/traced managed/browser consumers passed locally; main CI/publication and fresh official-feed verification remain pending.
- Independent production review found dictionary node handles incorrectly pinned as pointer payloads; the first full backend run also exposed omitted CSS priority encoded as null. Fix `e6b334d` passes handles directly and applies canonical IDL string defaults. Re-review is clean. The native test migration is integrated as `f72deae`: 781 passed, 12 existing ignored, zero failures/errors; all four ABI21 cases enabled. Expanded JIT and NativeAOT smoke each pass 15 checks, including CSS/popover regressions and real hydration node retention/click. Logs: `C:/wk/rv-native-tests-full-fixed.log`, `C:/wk/rv-native-aotsmoke-jit-final.log`, `C:/wk/rv-native-aotsmoke-aot-final.log`.
- Native View libraries now participate in normal/traced packing, with distinct SDL traced package identity. A published-package acceptance workflow is being added to exercise actual public backend/presenter packages and both runtime flavors, with fresh cache, exact native cohort and payload hash checks. No final public-package success is claimed yet.
- Root SageFs sessions `feaeaf7f` and `e9743a50` are stopped; all root work leases are released. Serena remains disconnected; foreign process trees were inspected but not stopped.
- Core alpha.2 main CI `40715311270`, DOM `40715311331`, dependency submission `40715311432` and Perf `40715311370` passed. OIDC `40716357543` succeeded, with four Created responses for exactly the corrected DOM IDs. After delayed indexing, all four public versions appeared; fresh official-feed normal/traced .NET and Fable/Node checks passed. Evidence: `C:/wk/dom-public-consumers/e798170a3c3243d48e32051b6db602c2/public-proof.json`. No re-upload or local-feed substitution was used.
- Published-package acceptance is integrated as `5a49e4f`: 17 hermetic verifier tests, eight MSBuild configurations and workflow lint passed. The four-job manual workflow still awaits actual native/backend/presenter publication before its runtime acceptance can run. Real preview.4 restore separately confirmed the SDK runtime/resource asset shape at `C:/wk/rv-published-schema/obj/project.assets.json`.
- Docs run `40715829701` exposed a replay recording/readiness race (endpoint captured at 64 while recording grew to 79). Isolated fix PR251 (`ee9f79d`) adds generation-guarded preparation completion and disabled playback controls. Local docs build passed 205 pages/40 scenarios, map tests 106, browser tests 17; independent review is clean. PR CI `40720619645` passed; performance and hosted post-merge deployment remain pending. This is separate from the earlier live-editor selector fix.
- Wrapper class discovery now uses actual shared interface ancestry rooted in canonical native types, so release model `06b8983` and corrected catalogue `e81b32b` produce byte-identical wrapper code with alpha.2 contracts. SHA256: `8D6B7FA589FA97F622A34206EE69D2D66F9EAC68D2890FF3FF794B629F0B14C7`. The new CI regeneration gate uses restored package inputs and rejects local core/interop/runtime overrides. Root session `e22c8549` is stopped; no root work lease remains.
- Native PR5 merged as `6222b6f9` after run `40706181592` passed all five RID builds/runtime checks and managed aggregation. Immutable preview.6 tag points to that exact tree. Redundant same-SHA queued main run `40722826201` was canceled only after tag run `40722932631` was active.
- Local source integration against the actual seven CI packages (no private native project/stage) built with zero warnings/errors and passed 781 tests, 12 existing ignored, zero failures/errors. All 24 selected win-x64 native/resource payload bytes match CI source commit `0b09d8cb404d4322720b443c2753c427eef2c4fd`; canonical package model/DLL plus public DOM alpha.2 regenerate both committed outputs exactly. Evidence: `C:/wk/native-ci-package-tests.log`, `C:/wk/ranvier-views/artifacts/native-ci-source-tests`. Ignored Local.Build.props now uses `C:/wk/rv-ci-package-cache`; prior private configuration is backed up at `C:/wk/private-native-props-before-ci-7216ba9a120b491fbfa24e3314a84be4.xml`.
- Local packing produced all eight View/View.Dom/backend/SDL normal/traced nupkgs with correct canonical dependencies, and all four package-only fixtures compiled. Strict output verification then caught a real explicit-RID deployment defect: all five native targets assign `_RanvierWebKitResources` unconditionally, so the final linux-arm64 import overwrites the Windows path and no canonical icon tree is copied. Evidence: `C:/wk/rv-local-pack-proof-a0d42145de/resource-evaluation.json`. Runtime acceptance was held; no copying workaround or verifier weakening was used.
- Preview.6 release run `40722932631` was canceled before publication; its tag remains immutable. Native agent owns the narrow RID-conditioned PropertyGroup fix, real five-RID NuGet build/publish regression and preview.7 version in PR6. ABI remains 21; Core DOM alpha.2 is unchanged. Root defaults now target preview.7; new exact-tag Slim/GPU publication and public consumers are still required. Legacy harmless root PNG duplicates are outside this fix.
- Docs PR251 merged as `3d4fa2d0`. Exact master CI `40723383230`, Perf `40723383277`/report `40723612054`, and hosted Docs build/deploy `40723851862` passed. Hosted build: 205 pages, 40 scenarios, 17 browser tests. The same replay readiness/play/step/scrub/speed-reset test passed against the public site after deployment. Docs work is closed. App archive rejected the clean managed docs worktree because it is protected by a pinned task/workspace; it was left intact, with own sessions/leases stopped.
- Packaging correction is native PR6 head `6c18ac0104b1292fa98e12b957e0459be68e857b`: the resource-root PropertyGroup now has the same RID condition as its ItemGroup. Real five-RID local NuGet restores preserve six icon names, nested resource paths and exact RID-specific native/resource bytes under both Windows x64/ARM64 build and publish. SDK10 proof: `C:/wk/abi21-generator-scratch/resources-final-sdk10`. Existing package/release guards also pass; ABI and C++ outputs are unchanged. New CI `40729363033` package-contract job passed, including the real SDK regression; all five native jobs remain in progress. Native agent monitors this exact run in process `68841`. Preview.6 tag run `40722932631` is confirmed terminal cancelled, without publication.
- Native correction PR6 merged as `eef227e280664a10e3af75a7d5c75dc218b37a23` after exact-head run `40729363033` passed all five native runtime jobs, package contracts and managed aggregation. Root verified the merged tree is byte-identical to green head `6c18ac0`, confirmed preview.7 did not exist, and pushed immutable `v0.1.0-preview.7` at that merged SHA. Corrected release run `40746907938` is queued; native agent owns Slim OIDC monitoring and subsequent same-tag Windows GPU dispatch. Public publication and actual corrected package consumers remain unverified. Parent consumer preparation is ready on clean integration commit `e8180f7` with fresh proof/feed/cache paths; preview.6 evidence remains untouched.
- Fresh source integration against the corrected seven-package preview.7 PR cohort built all eight projects with zero warnings/errors and passed the complete backend suite: 781 passed, 12 existing ignored, zero failures/errors in 10.0 seconds. All 24 selected win-x64 native/resource payload bytes match the actual nupkg; source `9d872b250957ef9ba74a8272b5bb268420e30910` is the CI synthetic merge whose tree equals the green head and merged release tree. Canonical restored package inputs regenerate Objects.fs and report.txt exactly (hashes unchanged). Evidence: `C:/wk/native-preview7-ci-package-tests.log`, `C:/wk/ranvier-views/artifacts/native-preview7-ci-source-tests`, feed `C:/wk/native-abi21-preview7-ci-feed/proof.json`. Root ignored props now use fresh cache `C:/wk/rv-preview7-ci-package-cache`; no private source/runtime override. Build/test/run leases are released. Package-only normal/traced JIT/AOT acceptance is in progress separately; public tag publication remains pending.
