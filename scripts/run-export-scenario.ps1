<#
.SYNOPSIS
    The export scenario end to end: the scenario, then the viewer on the .glb it exported, then the checks on what the viewer made.
.DESCRIPTION
    Runs scenario Carve.Scenario.Export (the crate saved, reopened and exported into the run folder's
    game/Content/models), then run-viewer.ps1 on that content folder with the two agreed views, its output under the
    run folder's viewer/, then the xunit checks in tests/Carve.ExportViewer.Tests on the run folder (built first unless -NoBuild, and stopped
    after -ChecksTimeoutSeconds with exit 124, as the other steps are). It fails if any step does, or a check is skipped. The run folder's path is the last thing on stdout; everything else goes to stderr.
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
$folder = & (Join-Path $PSScriptRoot 'run-scenario.ps1') export -Configuration $Configuration -CarveArguments $CarveArguments @extra
$code = $LASTEXITCODE
if ($code -ne 0 -or -not $folder) {
    Write-Err "run-export-scenario: the scenario failed ($code)"
    if ($folder) { Write-Output $folder }
    exit $(if ($code -ne 0) { $code } else { 1 })
}
$folder = @($folder)[-1]

# 2. The viewer, on the content folder the scenario filled. The views are quoted so that their commas stay.
& (Join-Path $PSScriptRoot 'run-viewer.ps1') (Join-Path $folder 'game/Content') 'models/crate.glb' (Join-Path $folder 'viewer') `
    'front=1.6,1.2,2.2' 'minus-x=-2.2,1.2,1.6' -Configuration $Configuration @extra
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Err "run-export-scenario: the viewer failed ($code)"
    Write-Output $folder
    exit $code
}

# 3. The checks on the viewer's output, built first unless -NoBuild (the solution build of the suite has built them),
#    and bounded like the other two steps. They are skipped when CARVE_EXPORT_RUN is unset, so a skipped check here
#    is a failure: this run exists to make them run.
$tests = Join-Path $root 'tests/Carve.ExportViewer.Tests/Carve.ExportViewer.Tests.csproj'
if (-not $NoBuild) {
    dotnet build $tests -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$env:CARVE_EXPORT_RUN = $folder
$checks = Invoke-BoundedDotnet @('test', $tests, '-c', $Configuration, '--no-build', '--nologo') $root $ChecksTimeoutSeconds `
    "run-export-scenario: the checks are still running after $ChecksTimeoutSeconds s; stopped"
$checks.Stdout | ForEach-Object { Write-Err $_ }
$checks.Stderr | ForEach-Object { Write-Err $_ }
$code = $checks.Code
if ($code -eq 0 -and ($checks.Stdout -join "`n") -notmatch 'Passed:\s+[1-9]') {
    Write-Err 'run-export-scenario: no check passed; they may have been skipped'
    $code = 1
}
elseif ($code -eq 0 -and ($checks.Stdout -join "`n") -match 'Skipped:\s+[1-9]') {
    Write-Err 'run-export-scenario: a check was skipped'
    $code = 1
}

Write-Output $folder
if ($code -ne 0) { Write-Err "run-export-scenario: the viewer's checks failed ($code)" }
exit $code
