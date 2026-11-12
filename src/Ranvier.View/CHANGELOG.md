---
last_commit_released: 85021719b00d17b8ff3e989ad79a8fa32cea6492
pre_release: rc
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
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

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
