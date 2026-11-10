---
last_commit_released: a2c8e6d3b2ba0c79b0da0094848b703f60641402
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
