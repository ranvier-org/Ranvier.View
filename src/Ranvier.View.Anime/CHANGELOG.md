---
last_commit_released: 3331a3075863963e22924e99991b85f70d0ff1cf
pre_release: rc
include:
  - ../../Directory.Build.props
updaters:
  - regex:
      file: Ranvier.View.Anime.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.46-rc.1 - 2026-11-10

### 🐞 Bug Fixes

* *(native)* Qualify View46 lifecycle for P11 and DOM58 (#73) ([3fe45e3](https://github.com/ranvier-org/Ranvier.View/commit/3fe45e3d60f8cfa51df681ac708633e7ae936ac4))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/449a485e760f147c10a30aa103f01aaff824c50e..3331a3075863963e22924e99991b85f70d0ff1cf)</small></strong>

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
