# Current View46 on public Mac P11 Slim

This consumer compiles the qualified View source `5e02890` on macOS26 ARM64 with public Core DOM alpha.58 and Interop/native preview.11. View46 itself is source under qualification. It uses the exact four signed Slim dylibs in the public package; it does not use the private Cocoa GPU/ABI64 profile.

The two normal/traced JIT routes execute the complete `Ranvier.View.WebCore.Tests` entry point with `--summary`, without a filter or `dotnet test` discovery. All 849 named cases must appear exactly; at least 833 pass and at most 16 may be ignored, restricted to the explicit Mac UIA/EGL/AT-SPI and browser-only case list. The inventory comes from current source's actual Windows run, whose formatter successor is independently equivalent; the ignored-name policy is corroborated by the prior Mac suite after excluding its five additional IOSurface cases. This uses prior case names, not prior native runtime bytes or a current Mac success claim. The two NativeAOT routes compile the retained framework-free common lifecycle fixture, including cleanup faults, owner-thread admission and disposal. Each must report all 91 checks and dynamic code disabled. No Expecto/test framework is linked into that AOT app.

`inputs.json` pins the source, package hashes, SDK/compiler dependency and fixture bytes. The reused lifecycle logic is unchanged; two copied fixtures received Fantomas whitespace formatting, with both original and current hashes retained. Every assets graph must contain the proper public normal/traced Core/DOM pair and public Interop; source Interop, stale dependencies, opposing modes and package-backed View providers fail. The original repository feed mappings remain intact for Xantham source-test dependencies. Ranvier packages must restore from NuGet.org into fresh mode-specific caches, and the copied runtime DLLs must match their admitted package members.

Reviewed fixture/inventory text hashes normalize only CRLF to LF, matching Git's text storage across Windows and Mac. Bare CR or other byte changes reject. The receipt separately records every actual raw source hash before and after; native packages, DLLs and dylibs always use raw byte hashes. The initial Mac text-admission failure is retained separately and is not a product/runtime verdict.

The controller aggregates all four routes and preserves each failure. It records ordinary source builds, strict AOT logs/binlogs, actual unfiltered summaries, package graphs, native/managed hashes before and after, source archives and process cleanup. All commands use owned process groups, an 8-GiB aggregate RSS bound and finite deadlines. The original public dylibs and both source checkouts remain unchanged.

The workflow activates only on the exact owned `codex/project2-view46-mac-public` push branch, or an explicit dispatch with matching source/input hashes. Its first push is held until Root/QA review; do not push intermediate controller drafts. It has read-only permissions, no engine build, package publish, tag or shared cache. Artifacts retain the original outcomes, replay binaries, source and compact identity evidence for three days.

Local controls:

```sh
python3 -I tests/mac-public/test_controller.py
```

An actual Mac run is still required; local source controls do not qualify the four consumer routes.

Run `41640096050` qualified the normal routes (833 passed/16 allowed ignores; strict lifecycle 91, dynamic code false). Its traced builds succeeded but the initial graph guard assumed unsuffixed source-project IDs. The corrected guard requires `.Traced` source projects at alpha.46 in traced mode, preserving package mode/source/version checks. The follow-up workflow runs only `--mode traced`; it explicitly marks the result as partial and does not rebuild or claim to rerun the retained normal proof.
