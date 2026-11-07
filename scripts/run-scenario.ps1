<#
.SYNOPSIS
    Builds, then runs one scenario on the real carve with the debug extension stacked under it.
.DESCRIPTION
    The scenario is the project Carve.Scenario.<Name>, under scenarios/ or tests/. The run folder's
    path is the only thing on this script's stdout, and the exit code is carve's: 0 passed,
    non-zero failed, 124 if it was stopped for running out of time. Everything else, carve's stderr
    included, goes to stderr. Set Carve:Debug:* with -CarveArguments.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Name,
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 300,
    [switch]$NoBuild,
    [string[]]$CarveArguments = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

. (Join-Path $PSScriptRoot 'bounded-run.ps1')

# Matched without regard to case, so it does not rest on a case-insensitive filesystem.
$scenario = @('scenarios', 'tests') |
    ForEach-Object { Join-Path $root $_ } |
    Where-Object { Test-Path $_ } |
    ForEach-Object { Get-ChildItem $_ -Directory } |
    Where-Object { $_.Name -ieq "Carve.Scenario.$Name" } |
    ForEach-Object { Get-ChildItem $_.FullName -File -Filter '*.csproj' } |
    Where-Object { $_.Name -ieq "$($_.Directory.Name).csproj" } |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $scenario) {
    Write-Err "run-scenario: no project Carve.Scenario.$Name under scenarios/ or tests/"
    exit 2
}

$carveProject = Join-Path $root 'src/Carve.Server/Carve.Server.csproj'
$debugProject = Join-Path $root 'extensions/Carve.Debug/Carve.Debug.csproj'
if (-not $NoBuild) {
    foreach ($project in @($carveProject, $debugProject, $scenario)) {
        dotnet build $project -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

function Get-Output([string]$project, [string]$file) {
    Join-Path (Split-Path $project -Parent) 'bin' $Configuration 'net10.0' $file
}

$scenarioFile = Split-Path $scenario -Leaf
$arguments = @(
    'exec', (Get-Output $carveProject 'carve.dll'),
    "--Carve:Extensions:0=$(Get-Output $debugProject 'Carve.Debug.dll')",
    "--Carve:Extensions:1=$(Get-Output $scenario ($scenarioFile -replace '\.csproj$', '.dll'))"
) + $CarveArguments

# A queued run keeps what's in CAIRN_ARTIFACTS, which is what `cairn test artifacts` returns.
if ($env:CAIRN_ARTIFACTS -and -not ($CarveArguments -match '^--Carve:Debug:RunFolder=')) {
    $arguments += "--Carve:Debug:RunFolder=$(Join-Path $env:CAIRN_ARTIFACTS $Name.ToLowerInvariant())"
}

$run = Invoke-BoundedDotnet $arguments $root $TimeoutSeconds "run-scenario: $Name still running after $TimeoutSeconds s; stopped"
$code = $run.Code
$run.Stderr | ForEach-Object { Write-Err $_ }

if ($run.StdoutBytes -ne 0) {
    Write-Err "run-scenario: carve wrote $($run.StdoutBytes) bytes to stdout, which belongs to MCP; it wrote:"
    $run.Stdout | ForEach-Object { Write-Err "  $_" }
    if ($code -eq 0) { $code = 1 }
}

$folder = $run.Stderr | ForEach-Object { if ($_ -match '^carve-debug: run folder (.+)$') { $Matches[1] } } | Select-Object -First 1
if ($folder) { Write-Output $folder }

exit $code
