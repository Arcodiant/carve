<#
.SYNOPSIS
    T-168: makes the folder of one weathering acceptance round.
.DESCRIPTION
    -Round is the folder to make (under the worktree's ignored artifacts/T-168/round-N). It gets wrench.carve (samples/wrench.carve), the plant
    wrench-start.carve (version 10; its material 'steel' names graphs/steel-worn.texgraph while graphs/ holds worn-steel.texgraph, one word
    order away), prompt.txt, .mcp.json for carve and patina published (Release) from this worktree into -Servers (not the published copy in
    %LOCALAPPDATA%\carve-mcp) with the round folder as their working folder, and run-session.cmd (one headless session, model pinned).
    -NoPublish reuses the servers already in -Servers.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Round,
    [string]$Servers = (Join-Path $env:TEMP 'carve-m29-t168'),
    [switch]$NoPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$here = Join-Path $PSScriptRoot 'weathering-round'
New-Item -ItemType Directory -Force (Join-Path $Round 'graphs') | Out-Null
$Round = (Resolve-Path $Round).Path

Copy-Item (Join-Path $root 'samples/wrench.carve') (Join-Path $Round 'wrench.carve') -Force
Copy-Item (Join-Path $here 'worn-steel.texgraph') (Join-Path $Round 'graphs/worn-steel.texgraph') -Force
Copy-Item (Join-Path $here 'prompt.txt') (Join-Path $Round 'prompt.txt') -Force

# The plant: version 10, material 'steel' on the steel group, its graph path wrong.
$d = Get-Content (Join-Path $Round 'wrench.carve') -Raw | ConvertFrom-Json -AsHashtable
$mesh = $d.meshes[0]
$steel = @($mesh.groups | Where-Object { $_.name -eq 'steel' })[0].faces
$d.carve = 10
$d.materials = @(@{ name = 'steel'; metallic = 1; base_colour_texture = @{ graph = 'graphs/steel-worn.texgraph'; output = 'base_colour'; size = 512 } })
$d.parts = @(@{ name = $mesh.name; parent = $null; translation = @(0, 0, 0); rotation = @(0, 0, 0, 1); scale = @(1, 1, 1) })
$mesh.materials = @(@{ material = 'steel'; faces = $steel })
$d | ConvertTo-Json -Depth 50 -Compress | Set-Content (Join-Path $Round 'wrench-start.carve') -Encoding utf8NoBOM

if (-not $NoPublish) {
    foreach ($s in @(@('carve', 'src/Carve.Server'), @('patina', 'src/Patina.Server'))) {
        $running = Get-CimInstance Win32_Process -Filter "Name='$($s[0]).exe'" |
            Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith((Join-Path $Servers $s[0]), [StringComparison]::OrdinalIgnoreCase) }
        foreach ($p in $running) { Stop-Process -Id $p.ProcessId -Force -Confirm:$false }
        dotnet publish (Join-Path $root $s[1]) -c Release -o (Join-Path $Servers $s[0]) --nologo -v q 2>&1 | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) { throw "publish of $($s[0]) failed" }
    }
}

$mcp = @{ mcpServers = [ordered]@{
    carve  = @{ command = (Join-Path $Servers 'carve/carve.exe'); args = @() }
    patina = @{ command = (Join-Path $Servers 'patina/patina.exe'); args = @() } } }
$mcp | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $Round '.mcp.json') -Encoding utf8NoBOM

@'
@echo off
rem One fresh headless session in this folder: model pinned to claude-opus-5-5 (the person's ruling of 2026-11-24), no file tools, carve and patina only.
cd /d "%~dp0"
claude -p --model claude-opus-5-5 --allowedTools "mcp__carve__*" "mcp__patina__*" --tools "" --mcp-config .mcp.json --strict-mcp-config --output-format stream-json --verbose < prompt.txt > transcript.jsonl 2> stderr.txt
'@ | Set-Content (Join-Path $Round 'run-session.cmd') -Encoding ascii

Write-Output $Round
