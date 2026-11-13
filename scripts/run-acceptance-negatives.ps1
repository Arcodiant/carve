<#
.SYNOPSIS
    Shows T-49's acceptance checks failing: a good round folder is copied and broken five ways, and the checks run on each.
.DESCRIPTION
    -Round is a round folder that passes scripts/run-acceptance-check.ps1. Each copy is broken in one way (a vertex with no weight, the
    hat piece missing, the clips file missing, a model file that is another character's, a different character's model file) and
    the checks of tests/Carve.AcceptanceLoad.Tests, and for the last two the frame scenario, run on it. Prints a table of what failed.
    Needs -NoBuild only if everything is already built. The table is also written to <Round>/acceptance/negatives.tsv.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Round,
    [Parameter(Mandatory)][string]$OtherRound,
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tests = Join-Path $root 'tests/Carve.AcceptanceLoad.Tests/Carve.AcceptanceLoad.Tests.csproj'
$work = Join-Path ([IO.Path]::GetTempPath()) 'carve-acceptance-negatives'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null

function New-Copy([string]$name) {
    $d = Join-Path $work $name
    Copy-Item (Resolve-Path $Round).Path $d -Recurse
    return $d
}

$rows = @()
function Run-Checks([string]$name, [string]$dir, [bool]$frames) {
    $env:CARVE_ACCEPTANCE_RUN = $dir
    $sceneFailed = $false
    if ($frames) {
        & (Join-Path $PSScriptRoot 'run-scenario.ps1') acceptance -Configuration $Configuration -NoBuild *> $null
        $sceneFailed = $LASTEXITCODE -ne 0
    }
    $out = dotnet test $tests -c $Configuration --no-build --nologo 2>&1 | Out-String
    $failed = [regex]::Matches($out, '(?m)^\s+Failed (\S+)') | ForEach-Object { $_.Groups[1].Value -replace '^.*\.', '' }
    $script:rows += [pscustomobject]@{ Broken = $name; Scenario = $(if ($frames) { if ($sceneFailed) { 'fails' } else { 'passes' } } else { '-' }); FailedChecks = ($failed -join ', ') }
}

# 1. a vertex with no weight
$d = New-Copy 'unweighted'
$f = Join-Path $d 'character.carve'
$j = Get-Content $f -Raw | ConvertFrom-Json -Depth 50
$j.meshes[0].weights = @($j.meshes[0].weights | Select-Object -Skip 1)
($j | ConvertTo-Json -Depth 50 -Compress) | Set-Content $f -Encoding utf8
Run-Checks 'a vertex of the body with no weight' $d $false

# 2. the hat piece missing, 3. the clips file missing
$d = New-Copy 'no-hat'; Remove-Item (Join-Path $d 'game/Content/models/character.hat.glb')
Run-Checks 'hat piece missing' $d $false
$d = New-Copy 'no-clips'; Remove-Item (Join-Path $d 'game/Content/models/character.clips.glb')
Run-Checks 'clips file missing' $d $false

# 4. the model file is another character's: loads, but is not this character
$d = New-Copy 'other-model'
Copy-Item (Join-Path $OtherRound 'game/Content/models/character.glb') (Join-Path $d 'game/Content/models/character.glb') -Force
Run-Checks "model file of another character" $d $true

# 5. the saved file is another character's while the glb is this one's (render_glb no longer shows what render shows)
$d = New-Copy 'other-saved'
Copy-Item (Join-Path $OtherRound 'character.carve') (Join-Path $d 'character.carve') -Force
Run-Checks 'saved file of another character' $d $true

$rows | Format-Table -AutoSize | Out-String | Write-Host
$rows | ConvertTo-Csv -Delimiter "`t" -NoTypeInformation | Set-Content (Join-Path (Resolve-Path $Round).Path 'acceptance/negatives.tsv')
