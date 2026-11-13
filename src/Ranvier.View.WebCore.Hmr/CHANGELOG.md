---
last_commit_released: 014ca17b4092c925c4749c8e65bfb6cccd1bda90
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

## 0.1.0-alpha.0

Not released. The first release is cut by ShipIt.
