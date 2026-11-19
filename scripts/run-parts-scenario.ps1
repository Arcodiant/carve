<#
.SYNOPSIS
    The parts scenario end to end: the scenario, then the viewer on the rover .glb it exported, then the checks on the run folder.
.DESCRIPTION
    Runs scenario Carve.Scenario.Parts (the rover built as a part tree, seen, saved, reopened and exported into the run folder's
    game/Content/models), then run-viewer.ps1 on that content folder with --sky and the two agreed views (output under the run
    folder's viewer/; a merged load), then the xunit checks in tests/Carve.PartsLoad.Tests on the run folder (built first unless -NoBuild,
    stopped after -ChecksTimeoutSeconds with exit 124). It fails if any step does, or a check is skipped.
    The run folder's path is the last thing on stdout; everything else goes to stderr.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [int]$ChecksTimeoutSeconds = 180,
    [int]$ScenarioTimeoutSeconds = 900,
    [string[]]$CarveArguments = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

. (Join-Path $PSScriptRoot 'bounded-run.ps1')

$extra = @{}
if ($NoBuild) { $extra.NoBuild = $true }

# 1. The scenario.
$folder = & (Join-Path $PSScriptRoot 'run-scenario.ps1') parts -Configuration $Configuration -TimeoutSeconds $ScenarioTimeoutSeconds -CarveArguments $CarveArguments @extra
$code = $LASTEXITCODE
if ($code -ne 0 -or -not $folder) {
    Write-Err "run-parts-scenario: the scenario failed ($code)"
    if ($folder) { Write-Output $folder }
    exit $(if ($code -ne 0) { $code } else { 1 })
}
$folder = @($folder)[-1]

# 2. The viewer, on the content folder the scenario filled, lit with Carve's fixed sky. The views are quoted so that their commas stay.
& (Join-Path $PSScriptRoot 'run-viewer.ps1') (Join-Path $folder 'game/Content') 'models/rover.glb' (Join-Path $folder 'viewer') `
    'viewer-front=1.2,0.5,2.2' 'viewer-angle=1.6,1.2,2.2' '--sky' -Configuration $Configuration @extra
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Err "run-parts-scenario: the viewer failed ($code)"
    Write-Output $folder
    exit $code
}

# 3. The checks on the content folder, built first unless -NoBuild (the solution build of the suite has built them),
#    and bounded like the other two steps. They are skipped when CARVE_PARTS_RUN is unset, so a skipped check here
#    is a failure: this run exists to make them run.
$tests = Join-Path $root 'tests/Carve.PartsLoad.Tests/Carve.PartsLoad.Tests.csproj'
if (-not $NoBuild) {
    dotnet build $tests -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) {
        $code = $LASTEXITCODE
        Write-Err "run-parts-scenario: building the checks failed ($code)"
        Write-Output $folder
        exit $code
    }
}

$env:CARVE_PARTS_RUN = $folder
$checks = Invoke-BoundedDotnet @('test', $tests, '-c', $Configuration, '--no-build', '--nologo') $root $ChecksTimeoutSeconds `
    "run-parts-scenario: the checks are still running after $ChecksTimeoutSeconds s; stopped"
$checks.Stdout | ForEach-Object { Write-Err $_ }
$checks.Stderr | ForEach-Object { Write-Err $_ }
$code = $checks.Code
if ($code -eq 0 -and ($checks.Stdout -join "`n") -notmatch 'Passed:\s+[1-9]') {
    Write-Err 'run-parts-scenario: no check passed; they may have been skipped'
    $code = 1
}
elseif ($code -eq 0 -and ($checks.Stdout -join "`n") -match 'Skipped:\s+[1-9]') {
    Write-Err 'run-parts-scenario: a check was skipped'
    $code = 1
}

Write-Output $folder
if ($code -ne 0) { Write-Err "run-parts-scenario: the checks failed ($code)" }
exit $code
