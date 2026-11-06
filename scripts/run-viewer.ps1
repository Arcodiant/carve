<#
.SYNOPSIS
    Builds, then runs the model viewer, the stand-in for a game, on one model.
.DESCRIPTION
    Writes <view>.png per view and view.json to the out folder, and exits with the viewer's code: 0
    passed, non-zero failed, 124 if it was stopped for running out of time. Each of Views is
    <name>=<x,y,z>; a word beginning -- goes to the viewer as it is, so
    --Empyrean:Graphics:VirtualSurface=false opens a window. With no view the viewer uses front.
.EXAMPLE
    pwsh scripts/run-viewer.ps1 game/Content models/crate.glb out front=1.6,1.2,2.2 minus-x=-2.2,1.2,1.6
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

function Write-Err([string]$text) { [Console]::Error.WriteLine($text) }

$project = Join-Path $root 'spikes/Carve.ModelViewer/Carve.ModelViewer.csproj'
if (-not $NoBuild) {
    dotnet build $project -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$dll = Join-Path (Split-Path $project -Parent) 'bin' $Configuration 'net10.0' 'Carve.ModelViewer.dll'
$arguments = @('exec', $dll, '--content', $Content, '--model', $Model, '--out', $Out)
foreach ($view in $Views) {
    $arguments += if ($view.StartsWith('--')) { @($view) } else { @('--view', $view) }
}

# Start-Process joins its arguments with spaces, so each is quoted here.
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }

$stderr = [IO.Path]::GetTempFileName()
$stdout = [IO.Path]::GetTempFileName()
try {
    $viewer = Start-Process dotnet -ArgumentList $quoted -WorkingDirectory (Get-Location).Path -NoNewWindow -PassThru `
        -RedirectStandardError $stderr -RedirectStandardOutput $stdout
    $null = $viewer.Handle # Without this the exit code is lost once the process has gone.

    if ($viewer.WaitForExit($TimeoutSeconds * 1000)) {
        $code = $viewer.ExitCode
    }
    else {
        # Only the process this script started.
        $viewer.Kill($true)
        $viewer.WaitForExit()
        Write-Err "run-viewer: still running after $TimeoutSeconds s; stopped"
        $code = 124
    }

    Get-Content $stdout | ForEach-Object { Write-Err $_ }
    Get-Content $stderr | ForEach-Object { Write-Err $_ }
}
finally {
    Remove-Item $stderr, $stdout -ErrorAction SilentlyContinue
}

exit $code
