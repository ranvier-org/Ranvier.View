# Ranvier.Analyzers

F# analyzers that report Ranvier traps at build time. Built against FSharp.Analyzers.SDK 0.41.0.

```xml
<PackageReference Include="Ranvier.Analyzers" Version="0.1.1-preview.3" PrivateAssets="all" />
<PropertyGroup>
  <RanvierAnalyze>true</RanvierAnalyze>
</PropertyGroup>
```

The candidate package restores its pinned `Ranvier.Analyzers.Host` automatically and requires the .NET 10 SDK.
`dotnet build` prints `Foo.fs(12,9): warning RAN001: ...` after each build that compiles. See the
[analyzer rules and fixes](https://ranvier-org.github.io/Ranvier/analyzers/).

The host carries an unsigned, source-patched F# compiler that preserves analysis inside fixed buffers. Its package
includes the source pin, patch, licenses and runtime hash guard; it is a Ranvier distribution, not an official Microsoft compiler.

The Fable rules, RAN001 and RAN002, run in a project that a tool manifest's `fable` or `fickl`, a `Fable.*` package
reference, or `<RanvierFableAnalyzers>true</RanvierFableAnalyzers>` marks as compiled by Fable.
