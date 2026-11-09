---
last_commit_released: 49a348bb770f6151ce7959da47f8a3df9cdd6661
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

## 0.1.0-rc.6

Released by hand before ShipIt owned this version. See the commit history for its changes.
