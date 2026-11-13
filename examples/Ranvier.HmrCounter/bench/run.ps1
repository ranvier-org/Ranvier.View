# Runs the hot reload host on a scenario of scripted edits against the live window, with a hard timeout.
#   pwsh bench/run.ps1 -Strategy fine|fsi|alc [-Scenario bench/scenarios.json] [-TimeoutSec 240] [-Extra -software,-nochecker]
# The sample's sources are snapshot first and restored afterwards, whatever the run did. Build the sample first.
param(
    [string]$Strategy = "fine",
    [string]$Scenario = "bench/scenarios.json",
    [int]$TimeoutSec = 240,
    [string[]]$Extra = @()
)
$project = Split-Path $PSScriptRoot -Parent
Set-Location $project
$exe = Get-ChildItem (Join-Path $project "bin/Debug/net10.0") -Filter "Ranvier.HmrCounter*" |
    Where-Object { $_.Name -in @("Ranvier.HmrCounter.exe", "Ranvier.HmrCounter") } | Select-Object -First 1
if (-not $exe) { throw "Build examples/Ranvier.HmrCounter first." }
$tag = ($Strategy + (($Extra -join "") -replace "[^a-z]", ""))
$log = Join-Path $PSScriptRoot "log-$tag.txt"
$snapshot = Join-Path ([IO.Path]::GetTempPath()) ("hmr-bench-" + [guid]::NewGuid())
New-Item -ItemType Directory $snapshot | Out-Null
Get-ChildItem $project -Filter *.fs | Copy-Item -Destination $snapshot
try {
    $argList = @("dev", "-hmr", $Strategy, "-bench", (Resolve-Path $Scenario).Path) + $Extra
    $p = Start-Process -FilePath $exe.FullName -ArgumentList $argList -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -NoNewWindow
    if (-not $p.WaitForExit($TimeoutSec * 1000)) {
        "TIMEOUT after $TimeoutSec s; killing $($p.Id)"
        $p.Kill($true)
        $p.WaitForExit(5000) | Out-Null
    }
}
finally {
    Get-ChildItem $snapshot -Filter *.fs | Copy-Item -Destination $project -Force
    Remove-Item $snapshot -Recurse -Force
}
Get-Content $log
if ((Get-Item "$log.err").Length -gt 0) { "--- stderr ---"; Get-Content "$log.err" | Select-Object -First 30 }
