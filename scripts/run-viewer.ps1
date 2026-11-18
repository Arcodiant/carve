<#
.SYNOPSIS
    Builds, then runs the model viewer, the stand-in for a game, on one model.
.DESCRIPTION
    Writes <view>.png per view and view.json to the out folder, and exits with the viewer's code: 0
    passed, non-zero failed, 124 if it was stopped for running out of time. Each of Views is
    <name>=<x,y,z>; a word beginning -- goes to the viewer as it is, so
    --Empyrean:Graphics:VirtualSurface=false opens a window, and
    --sky lights the model with Carve's fixed sky as well as the viewer's sun, and
    --targets Bulk=0.5,Lean=0 sets the morph target weights it starts with (the next word is its value).
    With no view the viewer uses front.
.EXAMPLE
    pwsh scripts/run-viewer.ps1 game/Content models/crate.glb out front=1.6,1.2,2.2 minus-x=-2.2,1.2,1.6
.EXAMPLE
    pwsh scripts/run-viewer.ps1 game/Content models/barrel.glb out front=1.6,1.2,2.2 --sky
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory, Position = 0)][string]$Content,
    [Parameter(Mandatory, Position = 1)][string]$Model,
    [Parameter(Mandatory, Position = 2)][string]$Out,
    [Parameter(ValueFromRemainingArguments)][string[]]$Views = @(),
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 120,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

. (Join-Path $PSScriptRoot 'bounded-run.ps1')

$project = Join-Path $root 'spikes/Carve.ModelViewer/Carve.ModelViewer.csproj'
if (-not $NoBuild) {
    dotnet build $project -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$dll = Join-Path (Split-Path $project -Parent) 'bin' $Configuration 'net10.0' 'Carve.ModelViewer.dll'
$arguments = @('exec', $dll, '--content', $Content, '--model', $Model, '--out', $Out)
$valueOf = $null
foreach ($view in $Views) {
    if ($valueOf) { $arguments += $view; $valueOf = $null }   # the value of --targets, as it is
    elseif ($view -eq '--targets') { $arguments += $view; $valueOf = $view }
    elseif ($view.StartsWith('--')) { $arguments += $view }
    else { $arguments += @('--view', $view) }
}

$run = Invoke-BoundedDotnet $arguments (Get-Location).Path $TimeoutSeconds "run-viewer: still running after $TimeoutSeconds s; stopped"
$run.Stdout | ForEach-Object { Write-Err $_ }
$run.Stderr | ForEach-Object { Write-Err $_ }
exit $run.Code
