---
last_commit_released: 2c3be18ece285b107e4075e6bd42fe708254b438
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

## 0.1.7 - 2026-11-27

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.5

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/79abd73e119b8cbfad6ab21827d3d6126e3fcef8..2c3be18ece285b107e4075e6bd42fe708254b438)</small></strong>

## 0.1.6 - 2026-11-26

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.4

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/8cb88430848239a8140ca69b3551b1194cbc2018..79abd73e119b8cbfad6ab21827d3d6126e3fcef8)</small></strong>

## 0.1.5 - 2026-11-23

### 🐞 Bug Fixes

* *(deps)* Align cohort to Ranvier 0.2.1, Css 0.3.0, and Anime 0.3.0 ([8cb8843](https://github.com/ranvier-org/Ranvier.View/commit/8cb88430848239a8140ca69b3551b1194cbc2018))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.3

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/8be254d1a7d360618074ec541607ee7dc02c1892..8cb88430848239a8140ca69b3551b1194cbc2018)</small></strong>

## 0.1.4 - 2026-11-18

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.2

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/39f51599d3bea2342b108c01bd95939e8a00ff1d..8be254d1a7d360618074ec541607ee7dc02c1892)</small></strong>

## 0.1.3 - 2026-11-16

### 🐞 Bug Fixes

* *(deps)* Move Hmr to Ranvier.FSharp.Checker 0.1.1 and FSharp.Core 10.0.100 (#170) ([39f5159](https://github.com/ranvier-org/Ranvier.View/commit/39f51599d3bea2342b108c01bd95939e8a00ff1d))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.1

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/f4381df54fc577d03ff1c0ebc06d512ce104f1c4..39f51599d3bea2342b108c01bd95939e8a00ff1d)</small></strong>

## 0.1.2 - 2026-11-15

### 🐞 Bug Fixes

* *(hmr)* Evaluate the dependents of an edit again and reload edits across files (#163) ([d8cbc99](https://github.com/ranvier-org/Ranvier.View/commit/d8cbc99aaeef99f6d09b30744d6cf15964e933b3))
* *(webcore)* Print exceptions the host swallowed and report binding failures (#160) ([51224b2](https://github.com/ranvier-org/Ranvier.View/commit/51224b27429b1785f8666aa6ddd1b9d1aed87c13))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.2.0

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/733a089f71e9f0896c1c60fa2e22007419b4fd25..f4381df54fc577d03ff1c0ebc06d512ce104f1c4)</small></strong>

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
