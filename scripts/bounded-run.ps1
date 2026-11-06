# Dot-sourced by the run scripts: what they share for starting one process and stopping it when it overruns.

function Write-Err([string]$text) { [Console]::Error.WriteLine($text) }

<#
.SYNOPSIS
    Runs dotnet with the given arguments, its output held in temp files, and stops it after TimeoutSeconds.
.DESCRIPTION
    Returns the exit code, or 124 with OnTimeout written to stderr if it was stopped for running out of time, and what
    it wrote: Stdout and Stderr as lines, and StdoutBytes. The caller decides where those go.
#>
function Invoke-BoundedDotnet([string[]]$Arguments, [string]$WorkingDirectory, [int]$TimeoutSeconds, [string]$OnTimeout) {
    # Start-Process joins its arguments with spaces, so each is quoted here.
    $quoted = $Arguments | ForEach-Object { '"' + $_ + '"' }

    $stderr = [IO.Path]::GetTempFileName()
    $stdout = [IO.Path]::GetTempFileName()
    try {
        $process = Start-Process dotnet -ArgumentList $quoted -WorkingDirectory $WorkingDirectory -NoNewWindow -PassThru `
            -RedirectStandardError $stderr -RedirectStandardOutput $stdout
        $null = $process.Handle # Without this the exit code is lost once the process has gone.

        if ($process.WaitForExit($TimeoutSeconds * 1000)) {
            $code = $process.ExitCode
        }
        else {
            # Only the process this script started.
            $process.Kill($true)
            $process.WaitForExit()
            Write-Err $OnTimeout
            $code = 124
        }

        [pscustomobject]@{
            Code = $code
            Stdout = @(Get-Content $stdout)
            Stderr = @(Get-Content $stderr)
            StdoutBytes = (Get-Item $stdout).Length
        }
    }
    finally {
        Remove-Item $stderr, $stdout -ErrorAction SilentlyContinue
    }
}
