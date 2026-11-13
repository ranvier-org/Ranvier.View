---
last_commit_released: 54fdc4b3ddf31f701c2c5464e90b2883ef3aaa90
pre_release: alpha
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

## 0.1.0-alpha.1 - 2026-11-13

### 🚀 Features

* *(webcore)* Hot reload dev host for native apps (#146) ([cf5217e](https://github.com/ranvier-org/Ranvier.View/commit/cf5217ed7d2832c316ebb79547d59ce404a8d260))

### 🔗 Dependencies

* `src/Ranvier.View.WebCore.Sdl` updated to 0.1.0-rc.15

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/014ca17b4092c925c4749c8e65bfb6cccd1bda90..54fdc4b3ddf31f701c2c5464e90b2883ef3aaa90)</small></strong>

## 0.1.0-alpha.0

Not released. The first release is cut by ShipIt.
