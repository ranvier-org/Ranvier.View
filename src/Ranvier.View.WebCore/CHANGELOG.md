---
last_commit_released: ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7
pre_release: rc
depends_on:
  - ../Ranvier.View/
updaters:
  - regex:
      file: Ranvier.View.WebCore.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.0-rc.4 - 2026-11-10

### ⚡ Performance Improvements

* *(webcore)* Bound editor work and background snapshots ([ab9fa4e](https://github.com/ranvier-org/Ranvier.View/commit/ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7))

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/db7e6130fd8f17d4b843238b044846d37c870be2..ab9fa4e4a2723dcf171cfe81c6278292d05ff9c7)</small></strong>

## 0.1.0-rc.3

Released by hand before ShipIt owned this version. See the commit history for its changes.
