# Contributing to Ranvier.View

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for commit messages and pull request titles:

```text
type(scope): summary
```

The scope is optional and must use lowercase letters, digits, `.`, `_`, `/` or `-`.
Supported contribution types are `build`, `bench`, `chore`, `ci`, `docs`, `feat`,
`fix`, `perf`, `refactor`, `revert`, `style` and `test`.

```text
feat(view): add a keyed table helper
fix: handle an empty input
feat!: remove the obsolete API
```

Mark breaking changes with `!` before the colon, or a `BREAKING CHANGE:` footer.
Explain the migration in the commit body or pull request description.

The **Contribution / Conventional commits** check validates human pull request
titles; bot pull requests are exempt, matching the Ranvier workflow. It does not
lint every commit in the branch. Use squash merging and preserve the conventional
PR title as the squash commit subject. If maintainers allow other merge methods,
every commit that reaches the release branch must follow the convention too.

## Before opening a pull request

Restore the pinned tools with `dotnet fsi build.fsx -- tools`, then run `dotnet fsi build.fsx -- test` for every
check, or `test --group view`, `sugar`, `anime`, `motion`, `hydration`, `arms` or `analyzers` for one CI
shard. `dotnet fsi build.fsx -- ci` runs every CI gate and packs the libraries. `--quick` skips npm installs,
Chromium installation and restores after the first run.

Use .NET 10 (pinned by `global.json`), the .NET 8 runtime and Node.js 26 or later.

For changes spanning this repository and Ranvier, use
`dotnet fsi build.fsx -- dev local [--ranvier-root <path>] [--with-css [--ranvier-css-root <path>]]`. This clones a
missing sibling checkout and enables project references through the ignored `Local.Build.props`. Existing
checkouts are preserved. `dev packages` returns to pinned package references. Commit and submit each repository's
changes separately. CI uses packages.

Use the build CLI's `format` command to apply Fantomas formatting to tracked F# sources, and `format --dry-format`
to check it. Generated sources and the docs samples are excluded through `.fantomasignore`; regenerate them with
`tools/Ranvier.Dom.Generate` and `docs/samples/view/Generate.fsx`.

## EasyBuild release requirements

[EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt) calculates
release versions from conventional commits. Use `feat` for features and `fix`
for fixes; breaking changes must carry the marker above. Inspect the installed
tool's `shipit conventions` command for its supported release categories.

ShipIt owns every package version. CI's `Version ownership` job, part of the required
`CI result`, fails a pull request that changes a `<Version>` or uses a release title,
unless it is ShipIt's release pull request. Do not bump a version or write a
`chore: release` pull request by hand.

Each packaged project has a `regex` entry under `updaters` in a `CHANGELOG.md`, pointing
to exactly one unconditional literal `<Version>` in that project, and the latest heading
in that changelog matches that version. ShipIt computes the next version from that
heading. Use `regex`, not `xml`: the `xml` updater reformats the whole project file. The
one exception is `Ranvier.View.WebCore.Sdl.Ibus`, pinned by
`tools/sdl/publication-pins.json`.

To release a change:

| You want | Do this |
| --- | --- |
| A new prerelease | Merge a `feat`, `perf` or `fix` commit touching the project's folder, then merge the release pull request. |
| A release after a dependency pin change | Title the change `fix(deps): ...`. Commits typed `chore` or `build` release nothing. |
| A stable version | Remove `pre_release` from the project's changelog. The next release drops the suffix: `0.1.0-rc.6` becomes `0.1.0`. |
| A new base version in a prerelease line | Set `force_version` in the changelog for one run. A `feat` or a breaking change on a prerelease only raises its number. |

Commits count for a changelog when they touch its folder or a path in its `include`. A
release of a changelog in `depends_on` also releases its dependents, following the package
graph: `Ranvier.View` with `Ranvier.View.Dom`, then `Ranvier.View.WebCore`, then
`Ranvier.View.WebCore.Sdl`. `pre_release` sets each changelog's channel.

| Changelog | Projects | Channel |
| --- | --- | --- |
| `src/Ranvier.View` | `Ranvier.View`, `Ranvier.View.Dom` | `rc` |
| `src/Ranvier.View.WebCore` | `Ranvier.View.WebCore` | `rc` |
| `src/Ranvier.View.WebCore.Sdl` | `Ranvier.View.WebCore.Sdl` | `rc` |
| `src/Ranvier.View.Anime` | `Ranvier.View.Anime`, `Fable.Ranvier.Anime` | `rc` |
| `tools/Ranvier.Analyzers.Host` | `Ranvier.Analyzers.Host` | `preview` |
| `tools/Ranvier.Analyzers` | `Ranvier.Analyzers` | `preview` |

Add a project to the solution and give it its own changelog/updater for independent
versioning, with `last_commit_released` set to a full commit hash. Use one changelog with
multiple updaters when projects intentionally share a version.

The `bump` command calculates and applies a release locally; use `--dry-run` to
preview it and `--allow-branch master` when working on `master` instead of `main`.
It does not publish packages. `shipit github` creates a release PR by default.
Both need restored tools, a Git repository with full history, and valid
conventional commits. GitHub operations also need GitHub authentication.

After CI succeeds on a push to `main` or `master`, the ShipIt workflow opens or
updates a release PR and starts CI on its branch. Merge it using its
`chore: release ...` title. CI validates and packs that release commit, then the
publish workflow uploads those exact CI artifacts to NuGet using trusted publishing.
Keep the release title prefix: it is how the publish workflow recognizes a release.
Pull request runs on the release branch stay at `action_required`, because GitHub holds
runs started by `GITHUB_TOKEN`. The started CI run reports the required check on the
same commit.

The build CLI's `publish --dry-publish` command builds, tests and packs locally
without uploading anything. Maintainers can also publish locally with
`NUGET_API_KEY` or `--nuget-key`; credentials are masked in command output.

### Maintainer setup

1. Enable squash merging and allow GitHub Actions to create and approve pull
   requests in repository Actions settings. Require the `CI result` and Contribution checks
   in branch protection.
2. Set the Actions variable `NUGET_USER` to your NuGet.org username. Create a NuGet
   trusted publishing policy for `ranvier-org/Ranvier.View` and the workflow
   filename `publish.yml`. No long-lived NuGet key is needed in GitHub secrets.
3. Before the first release, ensure the initial commit is conventional (for example
   `feat: initialise the project`). For an existing package, seed its changelog with
   the actual latest release and set `last_commit_released` to that release's commit.

`shipit init github --dry-run` previews EasyBuild's suggested GitHub settings.
Applying them requires an authenticated `gh` CLI and repository administration
rights; this is an explicit maintainer operation.

### Imported release baseline

The view libraries and the analyzers preserve the versions already published from Ranvier. Each changelog's
`last_commit_released` records the commit that set its current version, so earlier commits are excluded from new
release calculations. Do not reset package versions or attempt to overwrite an existing NuGet version.

When a release requires a newer core, first test the exact upstream and downstream revisions in local-reference
mode. Publish the core, wait until its exact version restores from NuGet, update `RanvierVersion` (and the other
core versions) in `Directory.Build.props`, then run every gate in package mode and publish the views. Ranvier.Css
consumes `Ranvier.View` from this repository: release the views before a CSS release that needs them. Each
repository keeps its own version and publishing identity.
