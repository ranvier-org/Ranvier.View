---
last_commit_released: 606e0e63e7ab1c64511ede7bbc86a0fae7f817c3
pre_release: rc
depends_on:
  - ../Ranvier.View.WebCore/
updaters:
  - regex:
      file: Ranvier.View.WebCore.Sdl.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: README.md
      pattern: (?<=--version ).*
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.0-rc.13 - 2026-11-12

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.9

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/85021719b00d17b8ff3e989ad79a8fa32cea6492..606e0e63e7ab1c64511ede7bbc86a0fae7f817c3)</small></strong>

## 0.1.0-rc.12 - 2026-11-12

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.8

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/d62f5615106f92d91f56b8ffaa006a1a3a104a77..85021719b00d17b8ff3e989ad79a8fa32cea6492)</small></strong>

## 0.1.0-rc.11 - 2026-11-12

### 🐞 Bug Fixes

* *(sdl)* Draw the crosshair cursor on Windows (#135) ([437c40f](https://github.com/ranvier-org/Ranvier.View/commit/437c40f66d76f5679c5fcdf8f6231126a26cc1e0))

### ⚡ Performance Improvements

* *(webcore)* Native rc.8, cheaper repaints while the pointer moves (#137) ([d62f561](https://github.com/ranvier-org/Ranvier.View/commit/d62f5615106f92d91f56b8ffaa006a1a3a104a77))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.7

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/5ce897e2158038bec5cbb947cd4c7224d037ea18..d62f5615106f92d91f56b8ffaa006a1a3a104a77)</small></strong>

## 0.1.0-rc.10 - 2026-11-11

### ⚡ Performance Improvements

* *(webcore)* Encode snapshots from owned native pixels (#132) ([5ce897e](https://github.com/ranvier-org/Ranvier.View/commit/5ce897e2158038bec5cbb947cd4c7224d037ea18))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.6

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/fffe5dc0149737f6d479a5498f5f8dffa280f9b7..5ce897e2158038bec5cbb947cd4c7224d037ea18)</small></strong>

## 0.1.0-rc.9 - 2026-11-11

### 🐞 Bug Fixes

* *(sdl)* Preserve dirty CPU painting and gate responsiveness regressions (#126) ([960fdfa](https://github.com/ranvier-org/Ranvier.View/commit/960fdfa62bc9c35797ae3c95031165a1c5861ab8))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.5

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/a2c8e6d3b2ba0c79b0da0094848b703f60641402..fffe5dc0149737f6d479a5498f5f8dffa280f9b7)</small></strong>

## 0.1.0-rc.8 - 2026-11-10

### 🐞 Bug Fixes

* *(sdl)* Align CPU runtime instructions with the released dependency ([a2c8e6d](https://github.com/ranvier-org/Ranvier.View/commit/a2c8e6d3b2ba0c79b0da0094848b703f60641402))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7..a2c8e6d3b2ba0c79b0da0094848b703f60641402)</small></strong>

## 0.1.0-rc.7 - 2026-11-10

### ⚡ Performance Improvements

* *(webcore)* Bound editor work and background snapshots ([ab9fa4e](https://github.com/ranvier-org/Ranvier.View/commit/ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore` updated to 0.1.0-rc.4

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/49a348bb770f6151ce7959da47f8a3df9cdd6661..ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7)</small></strong>

## 0.1.0-rc.6

Released by hand before ShipIt owned this version. See the commit history for its changes.
