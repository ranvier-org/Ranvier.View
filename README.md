# Ranvier.View

Template components, server rendering, hydration, DOM bindings, motion parts and build-time analyzers for
[Ranvier](https://github.com/ranvier-org/Ranvier), on .NET and Fable.

[Repository](https://github.com/ranvier-org/Ranvier.View) · [Documentation](https://ranvier-org.github.io/Ranvier.View/)

Ranvier.View is the view layer of the Ranvier family:

- [Ranvier](https://github.com/ranvier-org/Ranvier) ([docs](https://ranvier-org.github.io/Ranvier/)): the reactive
  core, `Ranvier.Elmish`, `Ranvier.Query`, `Ranvier.Wire` and `Ranvier.CSharp`.
- [Ranvier.Css](https://github.com/ranvier-org/Ranvier.Css) ([docs](https://ranvier-org.github.io/Ranvier.Css/)):
  typed CSS, generated bindings, static extraction and `Ranvier.Css.View`.
- [Ranvier.Persistence](https://github.com/ranvier-org/Ranvier.Persistence): persisted signals and store adapters.

The view projects, experiments, examples and checks were copied from the Ranvier repository. The core and CSS
packages are pinned NuGet dependencies, so this repository builds on its own; a sibling checkout is supported for
coordinated development.

## Packages

| Package | Contents |
| --- | --- |
| `Ranvier.View` | Typed templates, view sugar, keyed lists, arms, boundaries and server HTML rendering. |
| `Ranvier.View.Dom` | The browser renderer: mounting, hydration of server HTML, delegated events and generated HTML bindings. |
| `Fable.Ranvier.View.Anime` | Motion parts for templates over anime.js, for Fable projects; omitted by the server renderer. |
| `Ranvier.View.Anime` | The same motion parts over the native `Ranvier.Anime`, for .NET hosts. |
| `Fable.Ranvier.Anime` | Typed anime.js bindings for Fable and the portable animation API over them, published from [ranvier-org/Ranvier.Anime](https://github.com/ranvier-org/Ranvier.Anime). |
| `Ranvier.Anime` | The native .NET port of anime.js with the same portable API, published from [ranvier-org/Ranvier.Anime](https://github.com/ranvier-org/Ranvier.Anime). |
| `Ranvier.Analyzers` | F# analyzers that report Ranvier traps at build time. |

The four libraries share a version and depend on each other at exactly it. Each also packs as a `.Traced` package
over the traced core packages, and carries its F# sources under `fable/` for Fable consumers.

## Requirements for an app

- **.NET SDK.** A package's minimum `FSharp.Core` sets the oldest SDK that restores it without a warning.

  | Packages | Minimum `FSharp.Core` | .NET SDK |
  | --- | --- | --- |
  | `Ranvier.View`, `Ranvier.View.Dom` | 8.0.100 | 8 or later |
  | `Ranvier.View.WebCore` 0.1.2, `Ranvier.View.WebCore.Sdl` 0.2.0, `Ranvier.View.WebCore.Hmr` after 0.1.2 | 10.0.100 | any .NET 10 SDK |
  | `Ranvier.View.WebCore.Hmr` 0.1.2 | 10.1.403 | 10.0.403 or later |

  With Hmr 0.1.2 on an older SDK, restore reports `NU1605: Detected package downgrade: FSharp.Core from 10.1.403 to
  ...`, and the build treats it as an error when warnings are errors. On an SDK 10.0.1xx the app also fails when it
  loads the package: `Could not load file or assembly 'FSharp.Core, Version=10.1.0.0'`. Update the package or the
  SDK, or set the version in the app's project:

  ```xml
  <PackageReference Update="FSharp.Core" Version="10.1.403" />
  ```

- **One version of `Ranvier.View` and `Ranvier.View.Dom`.** `Ranvier.View.Dom` requires `Ranvier.View` at exactly its
  own version, and `Ranvier.View.WebCore` brings both. An app that also names an older `Ranvier.View` gets
  `NU1605: Detected package downgrade: Ranvier.View from 0.1.50 to 0.1.49`. Set the app's reference to the version
  `Ranvier.View.Dom` resolves to, or remove it.
- **`Ranvier.View.WebCore.Sdl` and `Ranvier.View.WebCore.Hmr` move together.** Hmr 0.1.2 is built against Sdl 0.2.0,
  whose `Window.Create` has a different compiled signature from 0.1.x. Upgrade both in one change.

## Layout

- `src/`: the four libraries and `Fable.Ranvier`, the unpacked Fable DOM helper the experiments use.
- `experiments/`: `Ranvier.View.Arms` (`Arms.Match` and its Fable plugin), the representation checks, the DOM twin and
  the view testing API.
- `tests/Ranvier.View.Twin.Tests`, `fable/`: the twin conformance suite and the Fable suite of the Motion parts.
- `examples/`: the Fable playground, the Motion pages and the server hydration example.
- `tools/`: the DOM binding generator and the analyzers with their tests and truth fixtures.
- `docs/`: the Nacara site (Views, Server, Analyzers and the browser examples) and its compiled samples.

## Setup and checks

Install .NET 10, the .NET 8 runtime and Node.js 26 or later. All workflows run through Partas.Build:

```powershell
rtk dotnet fsi build.fsx -- tools
rtk dotnet fsi build.fsx -- test
```

`test` builds the solution and runs every group. CI runs each group as a shard:

```powershell
rtk dotnet fsi build.fsx -- test --group view       # representation checks, docs samples, playground, twin
rtk dotnet fsi build.fsx -- test --group sugar      # DOM generator output, README samples, generated types
rtk dotnet fsi build.fsx -- test --group motion     # Motion parts and the Motion example pages
rtk dotnet fsi build.fsx -- test --group hydration  # server hydration example and alternative backends
rtk dotnet fsi build.fsx -- test --group arms       # Arms.Match plugin, discovery, suites, budgets, trim
rtk dotnet fsi build.fsx -- test --group analyzers  # analyzer unit tests and truth fixtures
```

`--quick` skips npm installs, Chromium installation and restores. Other commands are `build`, `analyze`, `format`,
`format --dry-format`, `pack`, `docs`, `bump` and `shipit`. `publish --dry-publish` validates and packs without
uploading. Run `--help` for details.

## Working across repositories

```powershell
rtk dotnet fsi build.fsx -- dev local
rtk dotnet fsi build.fsx -- dev local --with-css
rtk dotnet fsi build.fsx -- dev packages
```

`dev local` uses `../Ranvier` (and, with `--with-css`, `../Ranvier.Css`), cloning a missing checkout, and writes the
ignored `Local.Build.props`. `--ranvier-root` and `--ranvier-css-root` select other locations. Existing checkouts are
never reset or updated. `dev packages` returns to the pinned NuGet versions in `Directory.Build.props`. For a one-off
build, pass `-p:UseLocalRanvier=true -p:RanvierRoot=<path>`; `Local.Build.props.example` configures an IDE manually.

## Releases

Each package has a ShipIt changelog with a project version updater. Trusted NuGet publishing and GitHub permissions
are described in [CONTRIBUTOR.md](CONTRIBUTOR.md#maintainer-setup).

A change spanning the core and the views is validated in local mode at explicit revisions first. Release the core,
wait until its version restores from NuGet, update `RanvierVersion` here, run every gate in package mode, then release
the views.
