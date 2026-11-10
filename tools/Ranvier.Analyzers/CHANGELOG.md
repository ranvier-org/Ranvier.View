---
last_commit_released: 3331a3075863963e22924e99991b85f70d0ff1cf
pre_release: preview
depends_on:
  - ../Ranvier.Analyzers.Host/
updaters:
  - regex:
      file: Ranvier.Analyzers.fsproj
      pattern: (?<=<Version>)[^<]+(?=</Version>)
  - regex:
      file: README.md
      pattern: (?<=Include="Ranvier.Analyzers" Version=")[^"]+
  - regex:
      file: ../../docs/content/analyzers/index.md
      pattern: (?<=Include="Ranvier.Analyzers" Version=")[^"]+|(?<=candidate at `)[^`]+
---

# Changelog

This changelog is generated from conventional commits by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

## 0.1.1-preview.3 - 2026-11-10

### 🔗 Dependencies

* `tools/Ranvier.Analyzers.Host` updated to 0.1.0-preview.2

<strong><small>[View changes on Github](https://github.com/ranvier-org/Ranvier.View/compare/449a485e760f147c10a30aa103f01aaff824c50e..3331a3075863963e22924e99991b85f70d0ff1cf)</small></strong>

## 0.1.1-preview.2

Released by hand before ShipIt owned this version. See the commit history for its changes.

## 0.1.1-preview.1 - 2026-10-28

### Fixes

- Initialize trusted publishing from the independent ranvier-org/Ranvier.Views repository.

## 0.1.0-preview.1
