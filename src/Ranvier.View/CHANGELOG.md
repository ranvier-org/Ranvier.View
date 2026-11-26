---
last_commit_released: 79abd73e119b8cbfad6ab21827d3d6126e3fcef8
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

## 0.5.0 - 2026-11-26

### 🚀 Features

* *(analyzers)* Add RAN016-RAN018 computation expression rules and upgrade to Ranvier 0.2.3 (#196) ([79abd73](https://github.com/ranvier-org/Ranvier.View/commit/79abd73e119b8cbfad6ab21827d3d6126e3fcef8))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/8cb88430848239a8140ca69b3551b1194cbc2018..79abd73e119b8cbfad6ab21827d3d6126e3fcef8)</small></strong>

## 0.4.0 - 2026-11-23

### 🐞 Bug Fixes

* *(deps)* Align cohort to Ranvier 0.2.1, Css 0.3.0, and Anime 0.3.0 ([8cb8843](https://github.com/ranvier-org/Ranvier.View/commit/8cb88430848239a8140ca69b3551b1194cbc2018))
* *(view)* Avoid inline capture of private mutable leavingCount under Fable precompile (#189) ([0bcf6c2](https://github.com/ranvier-org/Ranvier.View/commit/0bcf6c23a5fef09242b36f0a07779df58959f9af))

### ⚡ Performance Improvements

* *(view)* Add Rocq-verified HydrationCursor defunctionalization and lazy boundary copy-on-write (#183) ([687faa3](https://github.com/ranvier-org/Ranvier.View/commit/687faa3381568266bfe27eb2e8c23f81ef16ee93))
* *(view)* Add Rocq-verified reusable Queue.Flush buffer and hoisted slot commits (#184) ([ff183e4](https://github.com/ranvier-org/Ranvier.View/commit/ff183e4e3d46d30dee207300e5da92e120636e09))
* *(view)* Hoist leaf SSR scopes and unbox IdentityScope windows (#187) ([b9c4bf3](https://github.com/ranvier-org/Ranvier.View/commit/b9c4bf370b7de543b9cba82764f3c8caec2269bd))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/8be254d1a7d360618074ec541607ee7dc02c1892..8cb88430848239a8140ca69b3551b1194cbc2018)</small></strong>

## 0.3.0 - 2026-11-18

### 🐞 Bug Fixes

* *(view)* Close cancelled reflows and run Slot.Dropped on in-flush replacements (#176) ([ff3ed51](https://github.com/ranvier-org/Ranvier.View/commit/ff3ed5105473168a01f4a6c96a0b5c1d7246f86d))
* *(view)* Isolate multi-window hosts, reclaim CPU buffers, add ISO/JIS keys and clickthrough ([8be254d](https://github.com/ranvier-org/Ranvier.View/commit/8be254d1a7d360618074ec541607ee7dc02c1892))

### ⚡ Performance Improvements

* *(view)* Add Rocq-verified keyed layout, row-key, and Departures fast paths (#178) ([cce6469](https://github.com/ranvier-org/Ranvier.View/commit/cce646919166e619ece122e1acbb2ec5a144768a))
* *(view)* Add Rocq-verified Placement and TeardownWindow zero-allocation paths (#179) ([52ce46c](https://github.com/ranvier-org/Ranvier.View/commit/52ce46ce66344a3bbe1c2e65f185fb8d10fd6dca))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/39f51599d3bea2342b108c01bd95939e8a00ff1d..8be254d1a7d360618074ec541607ee7dc02c1892)</small></strong>

## 0.2.0 - 2026-11-16

### ⚡ Performance Improvements

* *(view)* Remove commit-queue allocations and inline WebCore interop checks (#166) ([1882b4c](https://github.com/ranvier-org/Ranvier.View/commit/1882b4c69f44f0723aeb1269b72e862246fd0ad7))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/733a089f71e9f0896c1c60fa2e22007419b4fd25..39f51599d3bea2342b108c01bd95939e8a00ff1d)</small></strong>

## 0.1.50 - 2026-11-14

### 🐞 Bug Fixes

* *(dom)* Read aggregate errors in both Fable shapes (#158) ([733a089](https://github.com/ranvier-org/Ranvier.View/commit/733a089f71e9f0896c1c60fa2e22007419b4fd25))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/335bd1bce23529ca7a9603856fc5c951d7c259f5..733a089f71e9f0896c1c60fa2e22007419b4fd25)</small></strong>

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
