# Analyzer Host Publication Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement these steps. The coordinator owns this checkout; acceptance owns independent review, merging and publication monitoring.

**Goal:** Publish and publicly qualify the exact owned analyzer/Host package pair that passed the complete Windows/Linux qualification, resolving View#14 without losing fixed-buffer diagnostics.

**Architecture:** The existing trusted `.github/workflows/publish.yml` publishes the Ubuntu-built pair from a successful main-branch release qualification. Both Windows and Linux must consume that same pair before publication. Public qualification restores only from NuGet.org into fresh caches and verifies repository signatures plus unchanged package content.

**Tech Stack:** GitHub Actions, .NET SDK 10.0.403, Python, NuGet trusted publishing.

**Spec:** Ranvier.View#14; `tools/Ranvier.Analyzers.Host/README.md`; `tools/Ranvier.Analyzers.Host/vendor-spec.json`.

## Global Constraints

- Compiler SHA256 remains `7fd7d2eb612f03a75bafe1fec971d1caad3352fcaa876eabde81db62111e8031`, assembly version `43.12.402.0`, empty public-key token.
- Host `0.1.0-preview.1` and Analyzers `0.1.1-preview.2` are the candidate identities; verify availability before pushing immutable versions.
- Preserve both complete 234-test provider runs and all six canonical analyzer groups. No skipped or filtered suite constitutes acceptance.
- Preserve the existing general package publication path and trusted workflow identity.
- Retain the accepted final-base dependency stack; do not disguise its changes as an isolated analyzer merge into main.
- NuGet repository signing can change ZIP bytes. Verify official signatures and exact non-signature content instead of demanding unsigned/signed whole-file identity.

## Review Focus

- A successful unrelated workflow must never trigger analyzer publication.
- A successful qualification on an unmerged branch must never publish through trusted main credentials.
- A skipped qualification leg must not look like complete evidence.
- A substituted package pair or changed payload must fail verification before publication or public acceptance.
- Public consumer restoration must not read the candidate directory or an inherited package cache as a feed.

## Task 1: Qualify one pair across platforms

**Files:** `.github/workflows/analyzer-host.yml`.

- [x] Add `candidate-consumers` with Windows/Linux legs downloading `analyzer-host-ubuntu-latest-${{ github.sha }}` and running the existing four external-consumer scenarios.
- [x] Parse YAML and verify the fixed artifact name and both platform entries.
- [x] Commit as `419cd91b9703361db44d321115266de35d281a34`; actual run `41376585122` is independently monitored.
- [ ] Review actual package fingerprints and both consumer results; keep predecessor run `41369757300` separate.

## Task 2: Add the trusted publication route

**Files:** `.github/workflows/publish.yml`, `.github/workflows/analyzer-host.yml`, a focused publication verifier and its tests under `tools/Ranvier.Analyzers.Host/tests/`.

- [ ] Pin tests for rejection of wrong repository, workflow path/name, event, source SHA, non-main branch, non-release commit, missing/duplicate/skipped/failed qualification jobs, and incomplete or substituted package pairs.
- [ ] Extend qualification to release pushes on main while retaining branch/PR qualification.
- [ ] Extend the existing publish workflow trigger to analyzer qualification; require its existing general publication job to select only `CI` events.
- [ ] Add an analyzer-only job requiring a successful push qualification from this repository's main/master branch with the established `chore: release ` commit prefix. Check actual run metadata and the complete expected job set before obtaining credentials.
- [ ] Download only the qualified run's Ubuntu pair, validate both nuspec identities, exact Host dependency, compiler manifest and compiler payload against pinned source, and record both uploaded SHA256 values.
- [ ] Authenticate through the existing NuGet trusted-publishing workflow, push Host before Analyzers, and fail on unexpected existing versions rather than silently skipping duplicates.
- [ ] Run focused verifier tests on Windows and WSL, validate workflow syntax and obtain independent acceptance review before merge/publication.

## Task 3: Prove public consumption

**Files:** `tools/Ranvier.Analyzers.Host/tests/test_consumer.py`, public qualification workflow wiring, analyzer documentation.

- [ ] Review the outside-root public-source proposal, correcting unsigned-versus-repository-signed package comparison before adoption.
- [ ] Require NuGet.org-only source configuration and a fresh per-scenario package cache; reject private feeds in the restored assets graph.
- [ ] Verify public packages using official NuGet signature verification and compare every original payload entry, identity and Host manifest with the tested pair. Reject added non-signature entries, removed entries, duplicate names and changed payload bytes.
- [ ] Execute all four existing package-only consumer scenarios on Windows and Linux against the public pair, retaining actual loaded compiler identity, GC settings, diagnostics, signature output and package-content comparison evidence.
- [ ] Publish the matching source changes through the accepted stack, update documentation to the actual package versions, and close View#14 only after independent public acceptance. Private DOM dependency qualification does not prove the separate full native/Core/View release cohort.
