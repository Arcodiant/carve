<#
.SYNOPSIS
    Builds, then runs one scenario on the real carve with the debug extension stacked under it.
.DESCRIPTION
    The scenario is the project Carve.Scenario.<Name>, under scenarios/ or tests/. The run folder's
    path is the only thing on this script's stdout, and the exit code is carve's: 0 passed,
    non-zero failed, 124 if it was stopped for running out of time. Everything else, carve's stderr
    included, goes to stderr. Set Carve:Debug:* with -CarveArguments.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Name,
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 300,
    [switch]$NoBuild,
    [string[]]$CarveArguments = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Write-Err([string]$text) { [Console]::Error.WriteLine($text) }

# Matched without regard to case, so it does not rest on a case-insensitive filesystem.
$scenario = @('scenarios', 'tests') |
    ForEach-Object { Join-Path $root $_ } |
    Where-Object { Test-Path $_ } |
    ForEach-Object { Get-ChildItem $_ -Directory } |
    Where-Object { $_.Name -ieq "Carve.Scenario.$Name" } |
    ForEach-Object { Get-ChildItem $_.FullName -File -Filter '*.csproj' } |
    Where-Object { $_.Name -ieq "$($_.Directory.Name).csproj" } |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $scenario) {
    Write-Err "run-scenario: no project Carve.Scenario.$Name under scenarios/ or tests/"
    exit 2
}

$carveProject = Join-Path $root 'src/Carve.Server/Carve.Server.csproj'
$debugProject = Join-Path $root 'extensions/Carve.Debug/Carve.Debug.csproj'
if (-not $NoBuild) {
    foreach ($project in @($carveProject, $debugProject, $scenario)) {
        dotnet build $project -c $Configuration --nologo -v q 2>&1 | ForEach-Object { Write-Err $_ }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

function Get-Output([string]$project, [string]$file) {
    Join-Path (Split-Path $project -Parent) 'bin' $Configuration 'net10.0' $file
}

$scenarioFile = Split-Path $scenario -Leaf
$arguments = @(
    'exec', (Get-Output $carveProject 'carve.dll'),
    "--Carve:Extensions:0=$(Get-Output $debugProject 'Carve.Debug.dll')",
    "--Carve:Extensions:1=$(Get-Output $scenario ($scenarioFile -replace '\.csproj$', '.dll'))"
) + $CarveArguments

# A queued run keeps what's in CAIRN_ARTIFACTS, which is what `cairn test artifacts` returns.
if ($env:CAIRN_ARTIFACTS -and -not ($CarveArguments -match '^--Carve:Debug:RunFolder=')) {
    $arguments += "--Carve:Debug:RunFolder=$(Join-Path $env:CAIRN_ARTIFACTS $Name.ToLowerInvariant())"
}

# Start-Process joins its arguments with spaces, so each is quoted here.
$quoted = $arguments | ForEach-Object { '"' + $_ + '"' }

$stderr = [IO.Path]::GetTempFileName()
$stdout = [IO.Path]::GetTempFileName()
try {
    $carve = Start-Process dotnet -ArgumentList $quoted -WorkingDirectory $root -NoNewWindow -PassThru `
        -RedirectStandardError $stderr -RedirectStandardOutput $stdout
    $null = $carve.Handle # Without this the exit code is lost once the process has gone.

    if ($carve.WaitForExit($TimeoutSeconds * 1000)) {
        $code = $carve.ExitCode
    }
    else {
        # Only the process this script started.
        $carve.Kill($true)
        $carve.WaitForExit()
        Write-Err "run-scenario: $Name still running after $TimeoutSeconds s; stopped"
        $code = 124
    }

    $lines = @(Get-Content $stderr)
    $lines | ForEach-Object { Write-Err $_ }

    if ((Get-Item $stdout).Length -ne 0) {
        Write-Err "run-scenario: carve wrote $((Get-Item $stdout).Length) bytes to stdout, which belongs to MCP; it wrote:"
        Get-Content $stdout | ForEach-Object { Write-Err "  $_" }
        if ($code -eq 0) { $code = 1 }
    }

    $folder = $lines | ForEach-Object { if ($_ -match '^carve-debug: run folder (.+)$') { $Matches[1] } } | Select-Object -First 1
    if ($folder) { Write-Output $folder }
}
finally {
    Remove-Item $stderr, $stdout -ErrorAction SilentlyContinue
}

exit $code
