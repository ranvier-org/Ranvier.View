---
last_commit_released: 2c3be18ece285b107e4075e6bd42fe708254b438
depends_on:
  - ../Ranvier.Analyzers.Host/
updaters:
  - regex:
      file: Ranvier.Analyzers.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: README.md
      pattern: (?<=Include="Ranvier.Analyzers" Version=")[^"]+
  - regex:
      file: ../../docs/content/analyzers/index.md
      pattern: (?<=Include="Ranvier.Analyzers" Version=")[^"]+|(?<=package at `)[^`]+
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.3.0 - 2026-11-27

### 🚀 Features

* *(analyzers)* Support Ranvier 0.2.4 CEs and remove Fable.Core dependency (#198) ([2c3be18](https://github.com/ranvier-org/Ranvier.View/commit/2c3be18ece285b107e4075e6bd42fe708254b438))

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.3.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/79abd73e119b8cbfad6ab21827d3d6126e3fcef8..2c3be18ece285b107e4075e6bd42fe708254b438)</small></strong>

## 0.2.0 - 2026-11-26

### 🚀 Features

* *(analyzers)* Add RAN016-RAN018 computation expression rules and upgrade to Ranvier 0.2.3 (#196) ([79abd73](https://github.com/ranvier-org/Ranvier.View/commit/79abd73e119b8cbfad6ab21827d3d6126e3fcef8))

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.2.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/335bd1bce23529ca7a9603856fc5c951d7c259f5..79abd73e119b8cbfad6ab21827d3d6126e3fcef8)</small></strong>

## 0.1.1 - 2026-11-14

### 🐞 Bug Fixes

* *(deps)* Build against the stable Ranvier cohort and release stable versions (#154) ([335bd1b](https://github.com/ranvier-org/Ranvier.View/commit/335bd1bce23529ca7a9603856fc5c951d7c259f5))

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.1.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/a4afae44bf734666f3df5031e82a947046a82e24..335bd1bce23529ca7a9603856fc5c951d7c259f5)</small></strong>

## 0.1.1-preview.4 - 2026-11-14

### 🚀 Features

* *(view)* Add unified Ranvier.View.Dsl and obsolete Ranvier.View.Sugar (#148) ([a4afae4](https://github.com/ranvier-org/Ranvier.View/commit/a4afae44bf734666f3df5031e82a947046a82e24))

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.1.0-preview.3

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/3331a3075863963e22924e99991b85f70d0ff1cf..a4afae44bf734666f3df5031e82a947046a82e24)</small></strong>

## 0.1.1-preview.3 - 2026-11-10

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.1.0-preview.2

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/449a485e760f147c10a30aa103f01aaff824c50e..3331a3075863963e22924e99991b85f70d0ff1cf)</small></strong>

## 0.1.1-preview.2

Released by hand before ShipIt owned this version. See the commit history for its changes.

## 0.1.1-preview.1 - 2026-10-28

### Fixes

- Initialize trusted publishing from the independent ranvier-org/Ranvier.Views repository.

## 0.1.0-preview.1
