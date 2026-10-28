---
title: Introduction
order: 1
---

## Installation

```bash frame="terminal"
dotnet add package Ranvier.View.Dom --version 0.1.1-alpha.1
```

`Ranvier.View.Dom` brings in `Ranvier.View` and the [Ranvier core](https://ranvier-org.github.io/Ranvier/).
Add `Ranvier.View.Anime` for motion parts, `Fable.Ranvier.Anime` for the anime.js bindings alone, and
`Ranvier.Analyzers` for build-time diagnostics. A server that only renders HTML needs `Ranvier.View`.
The view packages share one version and support .NET 8, .NET 10 and Fable. They are alpha releases; the API can
change before 1.0.

The view packages depend on `Xantham.Fable.Core.TS`, which restores from the public
[Xantham package feed](https://nuget.cloudsmith.io/shayanhabibi/xantham/v3/index.json). Add it to the application's
`nuget.config`:

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="xantham" value="https://nuget.cloudsmith.io/shayanhabibi/xantham/v3/index.json" />
</packageSources>
```

Each package also ships as a `.Traced` package built over `Ranvier.Traced`, for development builds that record the
graph's event log.

## Where to go next

- [Views](../view/index.md): templates, sugar, collections, boundaries, events and testing.
- [Server rendering](../server/index.md): HTML on .NET, hydration and state transfer.
- [Analyzers](../analyzers/index.md): the RAN rules and how to enable them.
- [Browser examples](../examples/index.md): the playground, Motion pages and a hydration snapshot.

Typed CSS for views lives in [Ranvier.Css](https://ranvier-org.github.io/Ranvier.Css/).

## Contributing

Clone [ranvier-org/Ranvier.View](https://github.com/ranvier-org/Ranvier.View) and install .NET 10, the .NET 8
runtime and Node.js 26 or later. The same Partas.Build workflows run locally and in CI:

```bash frame="terminal"
dotnet fsi build.fsx -- tools
dotnet fsi build.fsx -- test
```

Use `dev local` to work against a sibling Ranvier checkout and `dev packages` to return to pinned NuGet
dependencies. See [CONTRIBUTOR.md](https://github.com/ranvier-org/Ranvier.View/blob/main/CONTRIBUTOR.md) for the
full workflow and release requirements.
