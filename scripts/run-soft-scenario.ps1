<#
.SYNOPSIS
    The soft character scenario end to end: the scenario, then the checks on the content folder it exported.
.DESCRIPTION
    Runs scenario Carve.Scenario.Soft (the gardener built flat, creased at the brim, subdivided and smoothed, saved, reopened and exported into the run
    folder's game/Content/models), then the xunit checks in tests/Carve.SoftLoad.Tests on the run folder (built first
    unless -NoBuild, stopped after -ChecksTimeoutSeconds with exit 124). It fails if any step does, or a check is skipped.
    The run folder's path is the last thing on stdout; everything else goes to stderr.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [int]$ChecksTimeoutSeconds = 180,
    [string[]]$CarveArguments = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

. (Join-Path $PSScriptRoot 'bounded-run.ps1')

$extra = @{}
if ($NoBuild) { $extra.NoBuild = $true }

# 1. The scenario.
$folder = & (Join-Path $PSScriptRoot 'run-scenario.ps1') soft -Configuration $Configuration -CarveArguments $CarveArguments @extra
$code = $LASTEXITCODE
if ($code -ne 0 -or -not $folder) {
    Write-Err "run-soft-scenario: the scenario failed ($code)"
    if ($folder) { Write-Output $folder }
    exit $(if ($code -ne 0) { $code } else { 1 })
}
$folder = @($folder)[-1]

# 2. The checks on the content folder, built first unless -NoBuild (the solution build of the suite has built them),
#    and bounded like the other two steps. They are skipped when CARVE_SOFT_RUN is unset, so a skipped check here
#    is a failure: this run exists to make them run.
$tests = Join-Path $root 'tests/Carve.SoftLoad.Tests/Carve.SoftLoad.Tests.csproj'
if (-not $NoBuild) {
    dotnet build $tests -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) {
        $code = $LASTEXITCODE
        Write-Err "run-soft-scenario: building the checks failed ($code)"
        Write-Output $folder
        exit $code
    }
}

$env:CARVE_SOFT_RUN = $folder
$checks = Invoke-BoundedDotnet @('test', $tests, '-c', $Configuration, '--no-build', '--nologo') $root $ChecksTimeoutSeconds `
    "run-soft-scenario: the checks are still running after $ChecksTimeoutSeconds s; stopped"
$checks.Stdout | ForEach-Object { Write-Err $_ }
$checks.Stderr | ForEach-Object { Write-Err $_ }
$code = $checks.Code
if ($code -eq 0 -and ($checks.Stdout -join "`n") -notmatch 'Passed:\s+[1-9]') {
    Write-Err 'run-soft-scenario: no check passed; they may have been skipped'
    $code = 1
}
elseif ($code -eq 0 -and ($checks.Stdout -join "`n") -match 'Skipped:\s+[1-9]') {
    Write-Err 'run-soft-scenario: a check was skipped'
    $code = 1
}

Write-Output $folder
if ($code -ne 0) { Write-Err "run-soft-scenario: the checks failed ($code)" }
exit $code
