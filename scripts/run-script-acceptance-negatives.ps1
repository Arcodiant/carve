<#
.SYNOPSIS
    Shows T-235's script acceptance checks failing: a good round folder is copied and broken three ways, and the checks run on each.
.DESCRIPTION
    -Round is a round folder on which tests/Carve.ScriptAcceptance.Tests passes. Each copy is broken in one way (a byte changed in
    prop.carve; prop-edited.carvescript swapped for the exported script; the measurement off, with both edited files replaced by the
    exported pair, so every byte check holds but the count is 3) and the checks run on it. Prints a table of what failed, also
    written to <Round>/acceptance/negatives.tsv, and exits 1 when a broken copy is not caught or a run was stopped, 0 when every one
    is caught. Builds the checks first unless -NoBuild; each run is stopped after -ChecksTimeoutSeconds (exit 124, not caught).
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
$tests = Join-Path $root 'tests/Carve.ScriptAcceptance.Tests/Carve.ScriptAcceptance.Tests.csproj'
if (-not $NoBuild) {
    dotnet build $tests -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) { Write-Err "run-script-acceptance-negatives: the build failed"; exit $LASTEXITCODE }
}
$work = Join-Path ([IO.Path]::GetTempPath()) 'carve-script-acceptance-negatives'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null

function New-Copy([string]$name) {
    $d = Join-Path $work $name
    Copy-Item (Resolve-Path $Round).Path $d -Recurse
    return $d
}

$rows = @()
function Run-Checks([string]$name, [string]$dir) {
    $env:CARVE_SCRIPT_ACCEPTANCE_RUN = $dir
    $checks = Invoke-BoundedDotnet @('test', $tests, '-c', $Configuration, '--no-build', '--nologo') $root $ChecksTimeoutSeconds `
        "run-script-acceptance-negatives: the checks on '$name' are still running after $ChecksTimeoutSeconds s; stopped"
    $out = ($checks.Stdout + $checks.Stderr) -join "`n"
    $failed = @([regex]::Matches($out, '(?m)^\s+Failed (\S+)') | ForEach-Object { $_.Groups[1].Value -replace '^.*\.', '' })
    $script:rows += [pscustomobject]@{
        Broken = $name
        FailedChecks = $(if ($checks.Code -eq 124) { 'stopped (time limit)' } else { $failed -join ', ' })
        Caught = $(if ($checks.Code -ne 124 -and $checks.Code -ne 0 -and $failed.Count -gt 0) { 'yes' } else { 'NO' })
    }
}

# 1. a byte changed in prop.carve (the last digit before the end, so the file still reads)
$d = New-Copy 'byte'
$f = Join-Path $d 'prop.carve'
$b = [IO.File]::ReadAllBytes($f)
$i = [Array]::FindLastIndex($b, [Predicate[byte]] { param($x) $x -ge 0x30 -and $x -le 0x39 })
$b[$i] = if ($b[$i] -eq 0x39) { 0x38 } else { $b[$i] + 1 }
[IO.File]::WriteAllBytes($f, $b)
Run-Checks 'a byte changed in prop.carve' $d

# 2. the edited script swapped for the exported one
$d = New-Copy 'swapped'
Copy-Item (Join-Path $d 'prop.carvescript') (Join-Path $d 'prop-edited.carvescript') -Force
Run-Checks 'edited script swapped for the exported one' $d

# 3. the measurement off: both edited files are the exported pair, so the bytes agree but the count is 3
$d = New-Copy 'measure'
Copy-Item (Join-Path $d 'prop.carvescript') (Join-Path $d 'prop-edited.carvescript') -Force
Copy-Item (Join-Path $d 'prop.carve') (Join-Path $d 'prop-edited.carve') -Force
Run-Checks 'measurement off (edited pair is the exported pair)' $d

$rows | Format-Table -AutoSize | Out-String | Write-Host
New-Item -ItemType Directory -Force (Join-Path (Resolve-Path $Round).Path 'acceptance') | Out-Null
$rows | ConvertTo-Csv -Delimiter "`t" -NoTypeInformation | Set-Content (Join-Path (Resolve-Path $Round).Path 'acceptance/negatives.tsv')
$missed = @($rows | Where-Object { $_.Caught -ne 'yes' })
if ($missed.Count -gt 0) {
    Write-Err "run-script-acceptance-negatives: not caught: $(($missed | ForEach-Object Broken) -join '; ')"
    exit 1
}
exit 0
