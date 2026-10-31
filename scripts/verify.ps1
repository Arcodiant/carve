<#
.SYNOPSIS
    The project's `cairn verify` tests check: build the whole solution warnings-as-errors,
    then run every test project. Exits non-zero on any failure.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'Carve.slnx'

dotnet build $solution -c Debug --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test $solution -c Debug --no-build
exit $LASTEXITCODE
