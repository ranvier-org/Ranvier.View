#i "nuget: https://nuget.cloudsmith.io/shayanhabibi/shayanhabibi-partas-build/v3/index.json"
#r "nuget: Partas.Build, 0.8.2"
#r "nuget: Partas.Build.Baked, 0.3.0"
#r "nuget: Partas.Build.EasyBuild.ShipIt, 0.1.0"

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open Partas.Build

module Spec =
    let root = __SOURCE_DIRECTORY__

    /// The packed libraries in dependency order. Each packs untraced and again as its `.Traced` package.
    let sources = [
        "src/Fable.Ranvier.Anime/Fable.Ranvier.Anime.fsproj"
        "src/Ranvier.View/Ranvier.View.fsproj"
        "src/Ranvier.View.Dom/Ranvier.View.Dom.fsproj"
        "src/Ranvier.View.Anime/Ranvier.View.Anime.fsproj"
        "src/Ranvier.View.WebCore/Ranvier.View.WebCore.fsproj"
        "src/Ranvier.View.WebCore.Sdl/Ranvier.View.WebCore.Sdl.fsproj"
    ]

    let analyzers = "tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj"

    /// The projects `analyze` checks, by group. Fable-only projects are left out.
    let analyzedGroups = [
        "library", [ "src/Fable.Ranvier/Fable.Ranvier.fsproj"; "src/Fable.Ranvier.Anime/Fable.Ranvier.Anime.fsproj" ]
        "view",
        [
            "src/Ranvier.View/Ranvier.View.fsproj"
            "src/Ranvier.View.Dom/Ranvier.View.Dom.fsproj"
            "src/Ranvier.View.Anime/Ranvier.View.Anime.fsproj"
            "experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj"
            "examples/Fable.Ranvier.Playground/Playground.fsproj"
            "examples/Fable.Ranvier.Playground/tests/DomTests.fsproj"
            "docs/samples/view/Ranvier.Docs.ViewSamples.fsproj"
        ]
        "examples",
        [
            "examples/Ranvier.Motion/Motion.fsproj"
            "examples/Ranvier.Motion/Server/ServerChecks.fsproj"
            "examples/Ranvier.Motion/tests/MotionTests.fsproj"
            "examples/Ranvier.Hydration/Shared.fsproj"
            "examples/Ranvier.Hydration/Server/Server.fsproj"
        ]
    ]

    /// The browser examples the docs site copies under examples/.
    let browserExamples = [ "examples/Ranvier.Motion"; "examples/Fable.Ranvier.Playground"; "examples/Ranvier.Hydration" ]

    let groups = [ "all"; "view"; "sugar"; "anime"; "motion"; "hydration"; "arms"; "analyzers" ]

    let localTools =
        use manifest = Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".config/dotnet-tools.json")))
        manifest.RootElement.GetProperty("tools").EnumerateObject()
        |> Seq.map (fun tool -> tool.Name, tool.Value.GetRawText())
        |> Seq.toList

    /// The runtime identifier of the trimmed publish check: this machine's platform.
    let trimRuntime =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then "win-x64"
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then "osx-arm64"
        else "linux-x64"

/// Runs `file` with `arguments` in the repository root and returns its standard output; a non-zero exit fails.
let capture (file: string) (arguments: string list) : string =
    let info = ProcessStartInfo(file, WorkingDirectory = Spec.root, RedirectStandardOutput = true, UseShellExecute = false)
    for argument in arguments do
        info.ArgumentList.Add argument
    use proc = Process.Start info
    let output = proc.StandardOutput.ReadToEnd()
    proc.WaitForExit()
    if proc.ExitCode <> 0 then
        failwith $"""{file} {String.Join(" ", arguments)} exited with {proc.ExitCode}"""
    output

module Options =
    let group =
        Input.option "--group"
        |> Input.mapFromAmong (Spec.groups |> List.map (fun name -> name, name))
        |> Input.def "all"
        |> Input.description "View checks to run (default: all)"
    let analyzeGroups =
        Input.option<string list> "--group"
        |> Input.arity Arity.ZeroOrMore
        |> Input.allowMultipleArgumentsPerToken
        |> Input.acceptOnlyFromAmong (Spec.analyzedGroups |> List.map fst |> List.toArray)
        |> Input.description "The analyzed project groups; every group when omitted"
    let analyzeBuild = Input.option<bool> "--build" |> Input.description "Builds the analyzed projects first"
    let dryFormat = Input.option<bool> "--dry-format" |> Input.description "Check formatting without rewriting files"
    let dryPublish = Input.option<bool> "--dry-publish" |> Input.description "Build and pack without uploading packages"
    let ranvierRoot =
        Input.option<string> "--ranvier-root"
        |> Input.def (Path.GetFullPath(Path.Combine(Spec.root, "../Ranvier")))
        |> Input.description "Path to the sibling Ranvier repository"
    let withCss = Input.option<bool> "--with-css" |> Input.description "Also use a sibling Ranvier.Css checkout"
    let ranvierCssRoot =
        Input.option<string> "--ranvier-css-root"
        |> Input.def (Path.GetFullPath(Path.Combine(Spec.root, "../Ranvier.Css")))
        |> Input.description "Path to the sibling Ranvier.Css repository (with --with-css)"

module Stage =
    let restoreTools = stage "restore tools in isolated SDK processes" {
        for name, registration in Spec.localTools do
            stage $"restore {name}" {
                run (fun _ ->
                    let directory = Path.Combine(Spec.root, ".config/.tool-restore")
                    Directory.CreateDirectory directory |> ignore
                    let json = "{\"version\":1,\"isRoot\":true,\"tools\":{" + Text.Json.JsonSerializer.Serialize(name) + ":" + registration + "}}"
                    File.WriteAllText(Path.Combine(directory, name + ".json"), json))
                run (Cmd.ofList "dotnet" [ "tool"; "restore"; "--tool-manifest"; Path.Combine(Spec.root, ".config/.tool-restore", name + ".json"); "--verbosity"; "q" ])
            }
    }

    let restore = input {
        let! quick = Baked.Common.quick
        and! config = Baked.Dotnet.configOrRelease
        return stage "restore" {
            when' (not quick)
            restoreTools
            stage "restore solution" { run (cmd $"dotnet restore Ranvier.View.slnx -p:Configuration={config}") }
        }
    }

    let build = input {
        let! config = Baked.Dotnet.configOrRelease
        return stage "build" {
            for project in Spec.sources @ [ Spec.analyzers ] do
                stage $"build {project}" { run (cmd $"dotnet build {project} -c {config} -m:1 --no-restore") }
        }
    }

    let buildSolution = input {
        let! config = Baked.Dotnet.configOrRelease
        return stage "build solution" { run (cmd $"dotnet build Ranvier.View.slnx -c {config} --no-restore") }
    }

    let format = input {
        let! check = Options.dryFormat
        return stage "format" {
            run (fun _ ->
                // Tracked sources in batches, so each command stays under the Windows command-line limit.
                let files =
                    (capture "git" [ "ls-files"; "*.fs" ]).Split('\n', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                    |> Array.toList
                let batches = files |> List.chunkBySize 150
                for batch in batches do
                    let arguments = [ "fantomas" ] @ batch @ (if check then [ "--check" ] else [])
                    capture "dotnet" arguments |> ignore)
        }
    }

    /// Installs a directory's pinned npm packages, unless `--quick`.
    let npm (directory: string) = input {
        let! quick = Baked.Common.quick
        return stage $"npm ci in {directory}" {
            when' (not quick)
            workingDir (Path.Combine(Spec.root, directory))
            run "npm ci"
        }
    }

    let chromium (directory: string) = input {
        let! quick = Baked.Common.quick
        return stage "install Chromium" {
            when' (not quick)
            workingDir (Path.Combine(Spec.root, directory))
            run "npx playwright install --with-deps chromium"
        }
    }

    /// The view layer: the .NET representation checks, the docs samples, the playground's jsdom and Chromium suites,
    /// and the conformance cases on the DOM twin compared with the jsdom artefacts.
    let view = input {
        let! npm = npm "examples/Fable.Ranvier.Playground"
        and! chromium = chromium "examples/Fable.Ranvier.Playground"
        return stage "view" {
            stage "representation checks" { run "dotnet run --project experiments/Ranvier.View.Checks/RepresentationChecks.fsproj -c Release" }
            stage "docs view samples" {
                run "dotnet run --project docs/samples/view/Ranvier.Docs.ViewSamples.fsproj -c Release"
                run "dotnet fsi docs/samples/view/Generate.fsx --check"
            }
            npm
            chromium
            stage "playground tests" {
                workingDir (Path.Combine(Spec.root, "examples/Fable.Ranvier.Playground"))
                run "npm test"
            }
            stage "conformance cases on the twin" { run "dotnet test tests/Ranvier.View.Twin.Tests -c Release" }
            stage "compare the jsdom and twin artefacts" {
                workingDir (Path.Combine(Spec.root, "examples/Fable.Ranvier.Playground"))
                run "node tests/conformance-diff.mjs"
            }
        }
    }

    /// The DOM generator's gates: its output, the README samples and the generated usage types.
    let sugar = input {
        let! npm = npm "tools/Ranvier.Dom.Generate"
        return stage "view sugar" {
            npm
            run "dotnet fsi tools/Ranvier.Dom.Generate/GeneratorTests.fsx"
            run "dotnet fsi tools/Ranvier.Dom.Generate/CheckDocs.fsx"
            run "dotnet fsi tools/Ranvier.Dom.Generate/CheckTypes.fsx"
        }
    }

    /// The Fable.Ranvier.Anime bindings under jsdom, their emit and bundle budget, and typed CSS animation in Chromium.
    let anime = input {
        let! npm = npm "fable/Ranvier.Anime.Tests"
        and! chromium = chromium "fable/Ranvier.Anime.Tests"
        return stage "anime bindings" {
            npm
            chromium
            stage "bindings tests" {
                workingDir (Path.Combine(Spec.root, "fable/Ranvier.Anime.Tests"))
                run "npm test"
                run "npm run test:browser"
            }
        }
    }

    /// The Ranvier.View.Anime parts under jsdom with their server twins, and the Motion example pages.
    let motion = input {
        let! partsNpm = npm "fable/Ranvier.View.Anime.Tests"
        and! examplesNpm = npm "examples/Ranvier.Motion"
        and! chromium = chromium "examples/Ranvier.Motion"
        return stage "motion" {
            partsNpm
            stage "Motion part tests" {
                workingDir (Path.Combine(Spec.root, "fable/Ranvier.View.Anime.Tests"))
                run "npm test"
            }
            stage "server renders against their twins" {
                run "dotnet run --project fable/Ranvier.View.Anime.Tests/Ranvier.View.Anime.Tests.fsproj -c Release"
            }
            examplesNpm
            chromium
            stage "Motion example tests" {
                workingDir (Path.Combine(Spec.root, "examples/Ranvier.Motion"))
                run "npm test"
                run "npm run size:only"
            }
        }
    }

    /// The server hydration example in Chromium and the alternative backend sources.
    let hydration = input {
        let! npm = npm "examples/Ranvier.Hydration"
        and! chromium = chromium "examples/Ranvier.Hydration"
        return stage "hydration" {
            npm
            chromium
            stage "hydration example tests" {
                workingDir (Path.Combine(Spec.root, "examples/Ranvier.Hydration"))
                run "npm test"
            }
            stage "alternative backends" {
                run "dotnet build docs/samples/server-backends/Oxpecker/Oxpecker.fsproj -c Release"
                run "dotnet build docs/samples/server-backends/Suave/Suave.fsproj -c Release"
                run "node docs/samples/server-backends/smoke.mjs"
            }
        }
    }

    /// Arms.Match: the Fable plugin's tests, the discovery app on both targets, the jsdom suites, the bundle budget,
    /// Arms.Prepare on both runtime targets, and the trim annotation.
    let arms = input {
        let! npm = npm "examples/Fable.Ranvier.Playground"
        return stage "arms" {
            // The plugin tests' source-map reader resolves @jridgewell/trace-mapping from the playground's packages.
            npm
            stage "plugin" {
                run "dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release"
                run "dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx"
            }
            stage "discovery app on both targets" {
                run (fun _ ->
                    let checks = "experiments/Ranvier.View.Arms/Checks/Discovery"
                    let net = capture "dotnet" [ "run"; "--project"; $"{checks}/Discovery.fsproj"; "-c"; "Release" ]
                    capture "dotnet" [ "fickl"; $"{checks}/Discovery.fsproj"; "--outDir"; $"{checks}/out"; "-c"; "Release"; "--noCache" ] |> ignore
                    let fable = capture "node" [ $"{checks}/out/Main.js" ]
                    let lines (text: string) = text.Replace("\r\n", "\n").TrimEnd()
                    if lines net <> lines fable then
                        failwith $"The discovery app's output differs between .NET and Fable.\n.NET:\n{net}\nFable:\n{fable}"
                    if not ((File.ReadAllText(Path.Combine(Spec.root, checks, "out/Main.js"))).Contains "armsTag_L") then
                        failwith "The Fable output holds no lowered Arms.Match site (armsTag_L)."
                    for assembly in [ "Ranvier.View.Arms.FablePlugin.dll"; "Fable.AST.dll" ] do
                        if File.Exists(Path.Combine(Spec.root, checks, "bin/Release/net10.0", assembly)) then
                            failwith $"{assembly} is in the .NET output of the discovery app.")
            }
            stage "arms suites" {
                workingDir (Path.Combine(Spec.root, "examples/Fable.Ranvier.Playground"))
                run "npm run test:arms"
            }
            stage "bundle budget" { run "node experiments/Ranvier.View.Arms/Checks/size.mjs" }
            stage "committed Chromium and heap results" {
                run "node experiments/Ranvier.View.Arms/Checks/chromium-gate.mjs"
                run "node --test experiments/Ranvier.View.Arms/Checks/pooled-interval.test.mjs"
            }
            stage "Arms.Prepare on net10.0 and net8.0" {
                run "dotnet run --project experiments/Ranvier.View.Arms/Checks/DotnetCost/DotnetCost.fsproj -c Release -f net10.0 -- --prepare"
                run "dotnet run --project experiments/Ranvier.View.Arms/Checks/DotnetCost/DotnetCost.fsproj -c Release -f net8.0 -- --prepare"
            }
            stage "trim annotation" { run $"bash experiments/Ranvier.View.Arms/Checks/TrimCallSite/check.sh {Spec.trimRuntime}" }
        }
    }

    /// The analyzers' unit tests and the compiler facts the analyzers describe.
    let analyzerChecks = stage "analyzers" {
        run "dotnet test tools/Ranvier.Analyzers.Tests -c Release"
        run "dotnet fsi tools/Ranvier.Analyzers.Truth/Truth.fsx"
    }

    let testsWith (group: InputSpec<string>) = input {
        let! group = group
        and! skip = Baked.Common.skipTests
        and! view = view
        and! sugar = sugar
        and! anime = anime
        and! motion = motion
        and! hydration = hydration
        and! arms = arms
        let chosen name = group = "all" || group = name
        return stage "view checks" {
            when' (not skip)
            stage "workflow scripts" { run "node --test .github/scripts/contrib.test.cjs" }
            stage "view group" { when' (chosen "view"); view }
            stage "sugar group" { when' (chosen "sugar"); sugar }
            stage "anime group" { when' (chosen "anime"); anime }
            stage "motion group" { when' (chosen "motion"); motion }
            stage "hydration group" { when' (chosen "hydration"); hydration }
            stage "arms group" { when' (chosen "arms"); arms }
            stage "analyzers group" { when' (chosen "analyzers"); analyzerChecks }
        }
    }
    let tests = testsWith (InputSpec.ofInput Options.group)
    let allTests = testsWith (InputSpec.ret "all")

    /// Builds the analyzers, then runs them over the chosen `Spec.analyzedGroups` with every RAN code an error, writing a
    /// SARIF report per group to artifacts/analyzers. Without `--build`, the analyzed projects must be built already.
    let analyze = input {
        let! groups = Options.analyzeGroups
        and! build = Options.analyzeBuild
        let chosen = Spec.analyzedGroups |> List.filter (fun (name, _) -> List.isEmpty groups || List.contains name groups)
        return stage "analyze" {
            stage "build analyzers" { run (cmd $"dotnet build {Spec.analyzers} -c Release -v q") }
            for name, projects in chosen do
                stage $"build the {name} projects" {
                    when' build
                    run (Cmd.ofList "dotnet" [ "new"; "sln"; "--format"; "slnx"; "--name"; name; "--output"; "artifacts/analyzers/build"; "--force" ])
                    run (Cmd.ofList "dotnet" ([ "sln"; $"artifacts/analyzers/build/{name}.slnx"; "add" ] @ projects))
                    run (Cmd.ofList "dotnet" [ "build"; $"artifacts/analyzers/build/{name}.slnx"; "-c"; "Release"; "-m"; "-v"; "q" ])
                }
                stage $"run analyzers over the {name} projects" {
                    run (fun _ -> Directory.CreateDirectory(Path.Combine(Spec.root, "artifacts/analyzers")) |> ignore)
                    run (
                        Cmd.ofList
                            "dotnet"
                            ([ "fsharp-analyzers"; "--project" ]
                             @ projects
                             @ [ "--exclude-files"; "**/Generated/**"
                                 "--analyzers-path"; "tools/Ranvier.Analyzers/bin/Release/net10.0"
                                 "-c"; "Release"; "--fail-on-type-errors"; "--treat-as-error"; "RAN*"; "--code-root"; "."
                                 "--report"; $"artifacts/analyzers/ranvier-{name}.sarif" ])
                    )
                }
        }
    }

    let packWith (traced: bool) = input {
        let! config = Baked.Dotnet.configOrRelease
        let label = if traced then "traced" else "untraced"
        return stage $"pack {label} libraries" {
            for project in Spec.sources do
                stage $"pack {label} {project}" {
                    run (
                        if traced then
                            cmd $"dotnet pack {project} -c {config} -p:RanvierTrace=true -m:1 -o bin"
                        else
                            cmd $"dotnet pack {project} -c {config} --no-build --no-restore -o bin"
                    )
                }
        }
    }

    /// <summary>Packs the libraries, their <c>.Traced</c> variants and the analyzers.</summary>
    /// <remarks>The traced packs restore and build again after every untraced pack.</remarks>
    let pack = input {
        let! clean = Baked.Stages.cleanWith (InputSpec.ret false) [] [ "bin/*.nupkg"; "bin/*.snupkg" ]
        and! untraced = packWith false
        and! traced = packWith true
        and! config = Baked.Dotnet.configOrRelease
        return stage "pack" {
            clean
            untraced
            stage "pack analyzers" { run (cmd $"dotnet pack {Spec.analyzers} -c {config} --no-build --no-restore -o bin") }
            traced
        }
    }

    let publish = input {
        let! key = Baked.NuGet.apiKey.option
        and! dry = Options.dryPublish
        return stage "publish" {
            when' (not dry)
            stage "push (requires NUGET_API_KEY or --nuget-key)" {
                failIfIgnored
                when' key.IsSome
                run (cmd $"dotnet nuget push bin/*.nupkg --source https://api.nuget.org/v3/index.json --skip-duplicate"
                     |> Cmd.secretOption "--api-key" (Option.defaultValue "" key))
            }
        }
    }

    let local = input {
        let! root = Options.ranvierRoot
        and! withCss = Options.withCss
        and! cssRoot = Options.ranvierCssRoot
        let root = Path.GetFullPath(root, Spec.root)
        let cssRoot = Path.GetFullPath(cssRoot, Spec.root)
        return stage "configure sibling checkouts" {
            stage "clone Ranvier if missing" {
                when' (not (Directory.Exists root))
                run (cmd $"git clone https://github.com/ranvier-org/Ranvier.git {root}")
            }
            stage "clone Ranvier.Css if missing" {
                when' (withCss && not (Directory.Exists cssRoot))
                run (cmd $"git clone https://github.com/ranvier-org/Ranvier.Css.git {cssRoot}")
            }
            stage "write local configuration" {
                run (fun _ ->
                    if not (File.Exists(Path.Combine(root, "src/Ranvier/Ranvier.fsproj"))) then
                        invalidArg "ranvier-root" "The path is not a Ranvier checkout."
                    if withCss && not (File.Exists(Path.Combine(cssRoot, "src/Ranvier.Css/Ranvier.Css.fsproj"))) then
                        invalidArg "ranvier-css-root" "The path is not a Ranvier.Css checkout."
                    let escape (path: string) = Security.SecurityElement.Escape path
                    let css =
                        if withCss then
                            $"<UseLocalRanvierCss>true</UseLocalRanvierCss><RanvierCssRoot>{escape cssRoot}</RanvierCssRoot>"
                        else
                            ""
                    File.WriteAllText(
                        Path.Combine(Spec.root, "Local.Build.props"),
                        $"<Project><PropertyGroup><UseLocalRanvier>true</UseLocalRanvier><RanvierRoot>{escape root}</RanvierRoot>{css}</PropertyGroup></Project>"
                    ))
            }
        }
    }
    let packages = stage "configure published package dependencies" {
        run (fun _ ->
            File.WriteAllText(
                Path.Combine(Spec.root, "Local.Build.props"),
                "<Project><PropertyGroup><UseLocalRanvier>false</UseLocalRanvier><UseLocalRanvierCss>false</UseLocalRanvierCss></PropertyGroup></Project>"
            ))
    }

    let docs = input {
        let! watch = Baked.Common.watch
        and! quick = Baked.Common.quick
        return stage "documentation" {
            stage "browser examples" {
                when' (not watch)
                for directory in Spec.browserExamples do
                    stage $"build {directory}" {
                        workingDir (Path.Combine(Spec.root, directory))
                        stage "npm ci" { when' (not quick); run "npm ci" }
                        run "npm run build"
                    }
            }
            run (cmd $"dotnet run --project docs/docs.fsproj --" |> Cmd.arg (if watch then "watch" else "build"))
        }
    }

exit <| rootCommand fsi.CommandLineArgs[1..] {
    description "Ranvier.View development, validation and releases"
    workingDir Spec.root
    command "tools" {
        description "Restore pinned tools individually to avoid SDK tool-metadata collisions on fresh runners"
        Command.pipeline { Stage.restoreTools }
    }
    command "dev" {
        description "Switch between published and sibling Ranvier references"
        command "local" {
            description "Clone Ranvier if missing and configure local project references (--with-css for Ranvier.Css too)"
            Command.pipeline { Stage.local; Stage.restore }
        }
        command "packages" {
            description "Switch back to pinned published Ranvier and Ranvier.Css dependencies"
            Command.pipeline { Stage.packages; Stage.restore }
        }
    }
    command "build" {
        description "Build the whole solution"
        Command.pipeline { Stage.restore; Stage.buildSolution }
    }
    command "test" {
        alias "tests"
        description "Run view checks; --group selects a CI shard (default: all)"
        Command.pipeline { Stage.restore; Stage.buildSolution; Stage.tests }
    }
    command "ci" {
        description "Run every CI gate locally and pack the validated libraries"
        Command.pipeline { Stage.restore; Stage.format; Stage.buildSolution; Stage.allTests; Stage.build; Stage.pack }
    }
    command "analyze" {
        description "Run the Ranvier analyzers over the repository's projects, every finding an error; build first, or pass --build"
        Command.pipeline { Stage.restore; Stage.analyze }
    }
    command "format" {
        description "Format tracked F# sources (--dry-format to check)"
        Command.pipeline { Stage.restore; Stage.format }
    }
    command "pack" {
        description "Build and pack the libraries untraced and traced, with Fable sources where supported, and the analyzers"
        Command.pipeline { Stage.restore; Stage.build; Stage.pack }
    }
    command "publish" {
        description "Validate every gate and publish (--dry-publish to skip upload)"
        Command.pipeline { Stage.restore; Stage.buildSolution; Stage.allTests; Stage.build; Stage.pack; Stage.publish }
    }
    command "docs" {
        description "Build the browser examples and the documentation (--watch to serve it)"
        Command.pipeline { Stage.restore; Stage.docs }
    }
    command "bump" {
        description "Calculate release versions with ShipIt in local mode"
        Command.pipeline { Partas.Build.EasyBuild.ShipIt.Stages.bump }
    }
    addCommand Partas.Build.EasyBuild.ShipIt.Commands.shipit
}
