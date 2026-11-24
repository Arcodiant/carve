<#
.SYNOPSIS
    The weathered scenario end to end: the scenario, then (once stage 5 lands) the checks on the run folder.
.DESCRIPTION
    Runs scenario Carve.Scenario.Weathered (the weathered barrel built, seen, re-baked, saved, reopened and exported into the run
    folder's game/Content/models). It fails if the scenario does. The run folder's path is the last thing on stdout; everything
    else goes to stderr. The load checks in tests/Carve.WeatheredLoad.Tests are run here, as run-planks-scenario.ps1 does, once
    stage 5 writes them.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string[]]$CarveArguments = @()
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'bounded-run.ps1')

$extra = @{}
if ($NoBuild) { $extra.NoBuild = $true }

$folder = & (Join-Path $PSScriptRoot 'run-scenario.ps1') weathered -Configuration $Configuration -CarveArguments $CarveArguments @extra
$code = $LASTEXITCODE
if ($code -ne 0 -or -not $folder) {
    Write-Err "run-weathered-scenario: the scenario failed ($code)"
    if ($folder) { Write-Output @($folder)[-1] }
    exit $(if ($code -ne 0) { $code } else { 1 })
}

Write-Output @($folder)[-1]
exit 0
