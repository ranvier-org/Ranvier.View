/// Runs the fixture app under the hot reload host and reads the outcome of each scenario step.
module Ranvier.View.WebCore.Hmr.Tests.Host

open System
open System.Diagnostics
open System.IO
open System.Text.Json

type Step =
    {
        Name: string
        Ok: bool
        Detail: string
    }

type Run =
    {
        Steps: Step list
        /// The host's standard output and standard error.
        Log: string
        /// The scratch copy of the fixture the run edited.
        Scratch: string
    }

let private timeout = TimeSpan.FromMinutes 4.0

/// Plays `scenarios/<scenario>.json` against a scratch copy of the fixture, in a hidden window on the software
/// renderer. A host that prints no results within four minutes is killed and yields no steps.
let run (scenario: string) : Run =
    let beside = AppContext.BaseDirectory

    let scratch =
        Path.Combine (Path.GetTempPath (), "ranvier-hmr-tests", Guid.NewGuid().ToString "N")

    let sources = Path.Combine (scratch, "fixture")
    Directory.CreateDirectory sources |> ignore

    for file in Directory.GetFiles (Path.Combine (beside, "fixture")) do
        File.Copy (file, Path.Combine (sources, Path.GetFileName file))

    let script = Path.Combine (scratch, scenario + ".json")
    File.Copy (Path.Combine (beside, "scenarios", scenario + ".json"), script)

    let info =
        ProcessStartInfo (
            "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = scratch
        )

    for argument in
        [
            Path.Combine (beside, "HmrFixture.dll")
            "dev"
            "-hidden"
            "-software"
            "-sources"
            sources
            "-bench"
            script
        ] do
        info.ArgumentList.Add argument

    use host = Process.Start info
    let output = host.StandardOutput.ReadToEndAsync ()
    let errors = host.StandardError.ReadToEndAsync ()

    if not (host.WaitForExit timeout) then
        host.Kill true
        host.WaitForExit ()

    let log = output.Result + errors.Result
    let results = Path.Combine (scratch, "results")

    let steps =
        if Directory.Exists results then
            [
                for file in Directory.GetFiles (results, "*.json") do
                    use rows = JsonDocument.Parse (File.ReadAllText file)

                    for row in rows.RootElement.EnumerateArray () do
                        yield
                            {
                                Name = row.GetProperty("step").GetString()
                                Ok = row.GetProperty("ok").GetBoolean()
                                Detail = row.GetProperty("detail").GetString()
                            }
            ]
        else
            []

    {
        Steps = steps
        Log = log
        Scratch = scratch
    }

/// Fails with every failed step and the host's log, or deletes the scratch copy when all steps passed.
let expectAllPassed (run: Run) =
    let failed = run.Steps |> List.filter (fun step -> not step.Ok)

    if run.Steps.IsEmpty || not failed.IsEmpty then
        let lines =
            failed
            |> List.map (fun step -> $"  FAILED {step.Name}: {step.Detail}")

        let summary =
            if run.Steps.IsEmpty then
                "The host wrote no results."
            else
                $"{failed.Length} of {run.Steps.Length} steps failed:"

        failwith (
            String.Join (
                "\n",
                summary :: lines
                @ [ "Host log:"; run.Log; $"Scratch copy: {run.Scratch}" ]
            )
        )
    else
        // RANVIER_HMR_TEST_LOG=1 prints the host's log of a passing run: the line of each reload.
        if Environment.GetEnvironmentVariable "RANVIER_HMR_TEST_LOG" = "1" then
            Console.WriteLine run.Log

        try
            Directory.Delete (run.Scratch, true)
        with _ ->
            ()
