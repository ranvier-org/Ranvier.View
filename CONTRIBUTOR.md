# Contributing to Ranvier.Views

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

Each packaged project has a `CHANGELOG.md` next to its `.fsproj`. Its YAML
`updaters` entry points to exactly one unconditional literal `<Version>` in that
project. Keep the version in the project and the latest changelog heading in
sync. ShipIt owns release version changes; do not run a separate numeric bump or
hand-edit generated release entries.

Add a project to the solution and give it its own changelog/updater for independent
versioning. The build CLI's `shipit init project --changelog <path> --project <path>`
command can register this. Use one changelog with multiple XML updaters when
projects intentionally share a version.

The `bump` command calculates and applies a release locally; use `--dry-run` to
preview it and `--allow-branch master` when working on `master` instead of `main`.
It does not publish packages. `shipit github` creates a release PR by default.
Both need restored tools, a Git repository with full history, and valid
conventional commits. GitHub operations also need GitHub authentication.

After CI succeeds on a push to `main` or `master`, the ShipIt workflow opens or
updates a release PR. Merge it using its `chore: release ...` title. CI validates
and packs that release commit, then the publish workflow uploads those exact CI
artifacts to NuGet using trusted publishing. Keep the release title prefix: it
is how the publish workflow recognizes a release.

The build CLI's `publish --dry-publish` command builds, tests and packs locally
without uploading anything. Maintainers can also publish locally with
`NUGET_API_KEY` or `--nuget-key`; credentials are masked in command output.

### Maintainer setup

1. Enable squash merging and allow GitHub Actions to create and approve pull
   requests in repository Actions settings. Require the `CI result` and Contribution checks
   in branch protection.
2. For release PRs that trigger CI automatically, set `SHIPIT_TOKEN` to a fine-grained
   token with repository Contents and Pull requests write permissions. The workflow
   falls back to `GITHUB_TOKEN`; PRs created with that token require a manual CI run
   on the release branch because GitHub suppresses automatic workflow triggers.
3. Set the Actions variable `NUGET_USER` to your NuGet.org username. Create a NuGet
   trusted publishing policy for `ranvier-org/Ranvier.Views` and the workflow
   filename `publish.yml`. No long-lived NuGet key is needed in GitHub secrets.
4. Before the first release, ensure the initial commit is conventional (for example
   `feat: initialise the project`). For an existing package, seed its changelog with
   the actual latest release and set `last_commit_released` to that release's commit.

`shipit init github --dry-run` previews EasyBuild's suggested GitHub settings.
Applying them requires an authenticated `gh` CLI and repository administration
rights; this is an explicit maintainer operation.

### Imported release baseline

The view libraries and the analyzers preserve the versions already published from Ranvier. Each changelog records
the initial import commit as `last_commit_released`, so the migration itself is excluded from new release
calculations. Do not reset package versions or attempt to overwrite an existing NuGet version.

When a release requires a newer core, first test the exact upstream and downstream revisions in local-reference
mode. Publish the core, wait until its exact version restores from NuGet, update `RanvierVersion` (and the other
core versions) in `Directory.Build.props`, then run every gate in package mode and publish the views. Ranvier.Css
consumes `Ranvier.View` from this repository: release the views before a CSS release that needs them. Each
repository keeps its own version and publishing identity.
