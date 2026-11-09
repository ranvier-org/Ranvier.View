---
last_commit_released: 449a485e760f147c10a30aa103f01aaff824c50e
pre_release: preview
depends_on:
  - ../Ranvier.Analyzers.Host/
updaters:
  - regex:
      file: Ranvier.Analyzers.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.1-preview.2

Released by hand before ShipIt owned this version. See the commit history for its changes.

## 0.1.1-preview.1 - 2026-10-28

### Fixes

- Initialize trusted publishing from the independent ranvier-org/Ranvier.Views repository.

## 0.1.0-preview.1
