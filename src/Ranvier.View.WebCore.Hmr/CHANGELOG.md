---
last_commit_released: 733a089f71e9f0896c1c60fa2e22007419b4fd25
depends_on:
  - ../Ranvier.View.WebCore.Sdl/
updaters:
  - regex:
      file: Ranvier.View.WebCore.Hmr.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: README.md
      pattern: (?<=--version ).*
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.1 - 2026-11-14

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.1.1

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/335bd1bce23529ca7a9603856fc5c951d7c259f5..733a089f71e9f0896c1c60fa2e22007419b4fd25)</small></strong>

## 0.1.0 - 2026-11-14

### 🐞 Bug Fixes

* *(deps)* Build against the stable Ranvier cohort and release stable versions (#154) ([335bd1b](https://github.com/ranvier-org/Ranvier.View/commit/335bd1bce23529ca7a9603856fc5c951d7c259f5))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.1.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/a4afae44bf734666f3df5031e82a947046a82e24..335bd1bce23529ca7a9603856fc5c951d7c259f5)</small></strong>

## 0.1.0-alpha.2 - 2026-11-14

### 🐞 Bug Fixes

* *(webcore)* Hot reload host disposes RunWith props before the window; Ranvier.Wgpu example (#150) ([cff2d86](https://github.com/ranvier-org/Ranvier.View/commit/cff2d868ec74bf1f68ae6623e964dacd12096798))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.1.0-rc.16

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/54fdc4b3ddf31f701c2c5464e90b2883ef3aaa90..a4afae44bf734666f3df5031e82a947046a82e24)</small></strong>

## 0.1.0-alpha.1 - 2026-11-13

### 🚀 Features

* *(webcore)* Hot reload dev host for native apps (#146) ([cf5217e](https://github.com/ranvier-org/Ranvier.View/commit/cf5217ed7d2832c316ebb79547d59ce404a8d260))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.1.0-rc.15

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/014ca17b4092c925c4749c8e65bfb6cccd1bda90..54fdc4b3ddf31f701c2c5464e90b2883ef3aaa90)</small></strong>

## 0.1.0-alpha.0

Not released. The first release is cut by ShipIt.
