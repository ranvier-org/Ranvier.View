---
last_commit_released: 335bd1bce23529ca7a9603856fc5c951d7c259f5
include:
  - ../Ranvier.View.Dom/
  - ../../Directory.Build.props
updaters:
  - regex:
      file: Ranvier.View.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: ../Ranvier.View.Dom/Ranvier.View.Dom.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: README.md
      pattern: (?<=dotnet add package Ranvier\.View\.Dom --version )\S+
  - regex:
      file: ../../docs/content/guide/introduction.md
      pattern: (?<=dotnet add package Ranvier\.View\.Dom --version )\S+
  - regex:
      file: ../../docs/content/view/getting-started.md
      pattern: (?<=Include="Ranvier\.View\.Dom" Version=")[^"]+
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.49 - 2026-11-14

### 🐞 Bug Fixes

* *(deps)* Build against the stable Ranvier cohort and release stable versions (#154) ([335bd1b](https://github.com/ranvier-org/Ranvier.View/commit/335bd1bce23529ca7a9603856fc5c951d7c259f5))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/a4afae44bf734666f3df5031e82a947046a82e24..335bd1bce23529ca7a9603856fc5c951d7c259f5)</small></strong>

## 0.1.49-rc.5 - 2026-11-14

### 🚀 Features

* *(view)* Add unified Ranvier.View.Dsl and obsolete Ranvier.View.Sugar (#148) ([a4afae4](https://github.com/ranvier-org/Ranvier.View/commit/a4afae44bf734666f3df5031e82a947046a82e24))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/606e0e63e7ab1c64511ede7bbc86a0fae7f817c3..a4afae44bf734666f3df5031e82a947046a82e24)</small></strong>

## 0.1.49-rc.4 - 2026-11-12

### 🏗️ Breaking changes

* *(view)* Typed onInput, onError, onAbort, onProgress and onResize take a plain Event (#143) ([606e0e6](https://github.com/ranvier-org/Ranvier.View/commit/606e0e63e7ab1c64511ede7bbc86a0fae7f817c3))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/85021719b00d17b8ff3e989ad79a8fa32cea6492..606e0e63e7ab1c64511ede7bbc86a0fae7f817c3)</small></strong>

## 0.1.49-rc.3 - 2026-11-12

### 🏗️ Breaking changes

* *(view-anime)* Motion parts over the unified Ranvier.Anime API (#134) ([8502171](https://github.com/ranvier-org/Ranvier.View/commit/85021719b00d17b8ff3e989ad79a8fa32cea6492))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/ca95dc744b662a837925ba50ac1b97fb7cb48d0c..85021719b00d17b8ff3e989ad79a8fa32cea6492)</small></strong>

## 0.1.49-rc.2

Released by hand before ShipIt owned this version. See the commit history for its changes.

## 0.1.1 - 2026-10-28

### 🏗️ Breaking changes

* *(view)* Share DOM contracts and integrate native presentation ([09686e7](https://github.com/ranvier-org/Ranvier.View/commit/09686e71a96976e6cd58f2e6d00410b763786775))

### 🚀 Features

* *(webcore)* Ranvier's view on a native WebCore page, in an SDL3 window (#1) ([6268ace](https://github.com/ranvier-org/Ranvier.View/commit/6268ace064722b8fb4c9d085b2cb2f34a6876263))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/c91f875b133499c586ce9f5de8ce24dbf589142c..09686e71a96976e6cd58f2e6d00410b763786775)</small></strong>

## 0.1.1-alpha.1 - 2026-10-28

### Fixes

- Initialize trusted publishing from the independent ranvier-org/Ranvier.Views repository.

## 0.1.0-alpha.3

## 0.1.0-alpha.2

## 0.1.0-alpha.1
