---
last_commit_released: 05f4226a2f10c7bf47a1c90475c3b577eddae592
include:
  - ../Fable.Ranvier.View.Anime/
  - ../../Directory.Build.props
depends_on:
  - ../Ranvier.View/
updaters:
  - regex:
      file: Ranvier.View.Anime.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: ../Fable.Ranvier.View.Anime/Fable.Ranvier.View.Anime.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.3.1 - 2026-11-27

### 🔗 Dependencies

* `src/Ranvier.View` updated to 0.7.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/2c3be18ece285b107e4075e6bd42fe708254b438..05f4226a2f10c7bf47a1c90475c3b577eddae592)</small></strong>

## 0.3.0 - 2026-11-27

### 🚀 Features

* *(analyzers)* Support Ranvier 0.2.4 CEs and remove Fable.Core dependency (#198) ([2c3be18](https://github.com/ranvier-org/Ranvier.View/commit/2c3be18ece285b107e4075e6bd42fe708254b438))

### 🔗 Dependencies

* `src/Ranvier.View` updated to 0.6.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/79abd73e119b8cbfad6ab21827d3d6126e3fcef8..2c3be18ece285b107e4075e6bd42fe708254b438)</small></strong>

## 0.2.0 - 2026-11-26

### 🚀 Features

* *(analyzers)* Add RAN016-RAN018 computation expression rules and upgrade to Ranvier 0.2.3 (#196) ([79abd73](https://github.com/ranvier-org/Ranvier.View/commit/79abd73e119b8cbfad6ab21827d3d6126e3fcef8))

### 🔗 Dependencies

* `src/Ranvier.View` updated to 0.5.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/8cb88430848239a8140ca69b3551b1194cbc2018..79abd73e119b8cbfad6ab21827d3d6126e3fcef8)</small></strong>

## 0.1.47 - 2026-11-23

### 🐞 Bug Fixes

* *(deps)* Align cohort to Ranvier 0.2.1, Css 0.3.0, and Anime 0.3.0 ([8cb8843](https://github.com/ranvier-org/Ranvier.View/commit/8cb88430848239a8140ca69b3551b1194cbc2018))

### 🔗 Dependencies

* `src/Ranvier.View` updated to 0.4.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/335bd1bce23529ca7a9603856fc5c951d7c259f5..8cb88430848239a8140ca69b3551b1194cbc2018)</small></strong>

## 0.1.46 - 2026-11-14

### 🐞 Bug Fixes

* *(deps)* Build against the stable Ranvier cohort and release stable versions (#154) ([335bd1b](https://github.com/ranvier-org/Ranvier.View/commit/335bd1bce23529ca7a9603856fc5c951d7c259f5))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/a4afae44bf734666f3df5031e82a947046a82e24..335bd1bce23529ca7a9603856fc5c951d7c259f5)</small></strong>

## 0.1.46-rc.3 - 2026-11-14

### 🚀 Features

* *(view)* Add unified Ranvier.View.Dsl and obsolete Ranvier.View.Sugar (#148) ([a4afae4](https://github.com/ranvier-org/Ranvier.View/commit/a4afae44bf734666f3df5031e82a947046a82e24))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/85021719b00d17b8ff3e989ad79a8fa32cea6492..a4afae44bf734666f3df5031e82a947046a82e24)</small></strong>

## 0.1.46-rc.2 - 2026-11-12

### 🏗️ Breaking changes

* *(view-anime)* Motion parts over the unified Ranvier.Anime API (#134) ([8502171](https://github.com/ranvier-org/Ranvier.View/commit/85021719b00d17b8ff3e989ad79a8fa32cea6492))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/3331a3075863963e22924e99991b85f70d0ff1cf..85021719b00d17b8ff3e989ad79a8fa32cea6492)</small></strong>

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
