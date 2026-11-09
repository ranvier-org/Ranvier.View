---
last_commit_released: 449a485e760f147c10a30aa103f01aaff824c50e
pre_release: rc
include:
  - ../Fable.Ranvier.Anime/
  - ../../Directory.Build.props
updaters:
  - regex:
      file: Ranvier.View.Anime.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: ../Fable.Ranvier.Anime/Fable.Ranvier.Anime.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.45

Released by hand before ShipIt owned this version. See the commit history for its changes.

## 0.1.1 - 2026-10-28

### Compatibility

- Rebuild the browser motion adapter against the shared DOM contracts and matching Ranvier.View 0.1.1 release.

## 0.1.1-alpha.1 - 2026-10-28

### Fixes

- Initialize trusted publishing from the independent ranvier-org/Ranvier.Views repository.

## 0.1.0-alpha.3

## 0.1.0-alpha.2

## 0.1.0-alpha.1
