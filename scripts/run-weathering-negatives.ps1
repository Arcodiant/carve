<#
.SYNOPSIS
    T-168: shows the weathering checks can fail. Builds a reference round (a hand-written graph that wears the steel's edges, dirties the drive and
    masks both to the steel group, a synthetic transcript) and four broken copies, runs tests/Carve.WeatheringAcceptance.Tests on each (the .glb
    test left out: no export is made here) and writes <Out>/negatives.tsv with the tests each one failed.
.DESCRIPTION
    Breaks: no-wear (the edge wear forced to 0 and the occlusion reach to nothing: the stand-in graph), grip-wear (the group mask names 'all', so the grip wears), no-dirt (occlusion reach 0),
    unresolved (the saved file still names graphs/steel-worn.texgraph, which is not there). Exits 1 when the reference fails or a break passes.
    -Plant is the round's wrench-start.carve. Built first unless -NoBuild.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Plant,
    [string]$Out = (Join-Path $env:TEMP 'carve-t168-negatives'),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tests = Join-Path $root 'tests/Carve.WeatheringAcceptance.Tests/Carve.WeatheringAcceptance.Tests.csproj'
if (-not $NoBuild) { dotnet build $tests -c Debug --nologo -v q | Out-Host; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
$reference = Get-Content (Join-Path $PSScriptRoot 'weathering-round/reference-steel.texgraph') -Raw

function New-Round($name, $graph, $graphPath) {
    $dir = Join-Path $Out $name
    New-Item -ItemType Directory -Force (Join-Path $dir 'graphs') | Out-Null
    Set-Content (Join-Path $dir 'graphs/steel.texgraph') $graph -Encoding utf8NoBOM
    $d = Get-Content $Plant -Raw | ConvertFrom-Json -AsHashtable
    $d.materials[0].base_colour_texture.graph = $graphPath
    $d | ConvertTo-Json -Depth 50 -Compress | Set-Content (Join-Path $dir 'wrench-weathered.carve') -Encoding utf8NoBOM
    $lines = @(
        @{ type = 'system'; subtype = 'init'; model = 'claude-opus-5-5' },
        @{ type = 'assistant'; message = @{ content = @(@{ type = 'tool_use'; name = 'mcp__carve__render'; input = @{} }) } },
        @{ type = 'user'; message = @{ content = @(@{ type = 'tool_result'; content = "the graph file 'graphs/steel-worn.texgraph' is not there" }) } },
        @{ type = 'assistant'; message = @{ content = @(@{ type = 'tool_use'; name = 'mcp__carve__define_material'; input = @{ name = 'steel'; base_colour_texture = @{ graph = 'graphs/steel.texgraph'; output = 'base_colour' } } }) } })
    ($lines | ForEach-Object { $_ | ConvertTo-Json -Depth 10 -Compress }) | Set-Content (Join-Path $dir 'transcript.jsonl') -Encoding utf8NoBOM
    $dir
}

$variants = [ordered]@{
    'reference'  = @($reference, 'graphs/steel.texgraph')
    'no-wear'    = @($reference.Replace('"in_high": 0.85, "in_low": 0.6 }', '"in_high": 0.85, "in_low": 0.6, "out_high": 0 }').Replace('"reach": 0.035', '"reach": 0.0001'), 'graphs/steel.texgraph')
    'grip-wear'  = @($reference.Replace('"group": "steel"', '"group": "all"'), 'graphs/steel.texgraph')
    'no-dirt'    = @($reference.Replace('"reach": 0.035', '"reach": 0.0001'), 'graphs/steel.texgraph')
    'unresolved' = @($reference, 'graphs/steel-worn.texgraph')
}
$rows = @("variant`tcaught`tfailed tests")
$bad = $false
foreach ($name in $variants.Keys) {
    $dir = New-Round $name $variants[$name][0] $variants[$name][1]
    $env:CARVE_WEATHERING_RUN = $dir
    $log = dotnet test $tests -c Debug --no-build --nologo --filter 'FullyQualifiedName!~The_glb' 2>&1 | Out-String
    Remove-Item Env:CARVE_WEATHERING_RUN
    $failed = [regex]::Matches($log, '(?m)^\s*Failed (\S+)') | ForEach-Object { $_.Groups[1].Value -replace '.*\.' } | Sort-Object -Unique
    $caught = [bool]$failed
    if (($name -eq 'reference') -eq $caught) { $bad = $true }
    $rows += "$name`t$caught`t$($failed -join ', ')"
    if ($name -ne 'reference' -and -not $caught) { ($log -split "`n" | Select-String 'Error Message' -Context 0,1 | Select-Object -First 4) | Out-Host }
    if ($name -eq 'reference' -and $caught) { ($log -split "`n" | Select-String 'Error Message' -Context 0,1 | Select-Object -First 4) | Out-Host }
}
$rows | Set-Content (Join-Path $Out 'negatives.tsv') -Encoding utf8NoBOM
$rows | Out-Host
exit ([int]$bad)
