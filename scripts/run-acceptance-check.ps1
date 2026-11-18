<#
.SYNOPSIS
    T-49's checks on one acceptance round: the frame scenario, then the load checks, on a round folder a fresh agent left.
.DESCRIPTION
    -Round is the folder holding character.carve and game/Content/models (the agent's scratch folder). Runs scenario
    Carve.Scenario.Acceptance with CARVE_ACCEPTANCE_RUN set to it (it writes <Round>/acceptance/differences.tsv and compare/),
    then the xunit checks in tests/Carve.AcceptanceLoad.Tests on the same folder (built first unless -NoBuild, stopped after
    -ChecksTimeoutSeconds with exit 124). It fails if a step does or a check is skipped. The scenario's run folder is the last
    thing on stdout; everything else goes to stderr. Not a suite: the round folder is an input only an acceptance run makes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Round,
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [int]$ChecksTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'bounded-run.ps1')

$env:CARVE_ACCEPTANCE_RUN = (Resolve-Path $Round).Path
$extra = @{}
if ($NoBuild) { $extra.NoBuild = $true }

$folder = & (Join-Path $PSScriptRoot 'run-scenario.ps1') acceptance -Configuration $Configuration @extra
$code = $LASTEXITCODE
if ($code -ne 0 -or -not $folder) {
    Write-Err "run-acceptance-check: the scenario failed ($code)"
    if ($folder) { Write-Output $folder }
    exit $(if ($code -ne 0) { $code } else { 1 })
}
$folder = @($folder)[-1]

$tests = Join-Path $root 'tests/Carve.AcceptanceLoad.Tests/Carve.AcceptanceLoad.Tests.csproj'
if (-not $NoBuild) {
    dotnet build $tests -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) { Write-Output $folder; exit $LASTEXITCODE }
}

$checks = Invoke-BoundedDotnet @('test', $tests, '-c', $Configuration, '--no-build', '--nologo') $root $ChecksTimeoutSeconds `
    "run-acceptance-check: the checks are still running after $ChecksTimeoutSeconds s; stopped"
$checks.Stdout | ForEach-Object { Write-Err $_ }
$checks.Stderr | ForEach-Object { Write-Err $_ }
$code = $checks.Code
if ($code -eq 0 -and ($checks.Stdout -join "`n") -notmatch 'Passed:\s+[1-9]') {
    Write-Err 'run-acceptance-check: no check passed; they may have been skipped'
    $code = 1
}
elseif ($code -eq 0 -and ($checks.Stdout -join "`n") -match 'Skipped:\s+[1-9]') {
    Write-Err 'run-acceptance-check: a check was skipped'
    $code = 1
}

Write-Output $folder
exit $code
