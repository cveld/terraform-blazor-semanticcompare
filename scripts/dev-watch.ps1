<#
.SYNOPSIS
    Starts the single `dotnet watch` dev server (http://localhost:5206) if it is not running yet,
    streaming all of its output to a central log file that every session can read.

.DESCRIPTION
    Log file: %LOCALAPPDATA%\semanticcompare\dotnet-watch.log (outside the repo, so it is the same
    for every checkout/worktree and never ends up in git).
    Read it with:  Get-Content "$env:LOCALAPPDATA\semanticcompare\dotnet-watch.log" -Tail 50
    Follow it with: Get-Content "$env:LOCALAPPDATA\semanticcompare\dotnet-watch.log" -Wait -Tail 20

    -Restart stops the running watch first (e.g. after the hot reload gave up on a rude edit).
#>
param([switch]$Restart)

$port = 5206
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'BlazorSemanticCompare\BlazorSemanticCompare.csproj'
$logDir = Join-Path $env:LOCALAPPDATA 'semanticcompare'
$log = Join-Path $logDir 'dotnet-watch.log'

function Get-WatchProcess {
    Get-CimInstance Win32_Process |
        Where-Object { $_.CommandLine -match 'dotnet.*watch' -and $_.CommandLine -match "--urls http://localhost:$port" }
}

$running = @(Get-WatchProcess)
if ($running.Count -gt 0 -and -not $Restart) {
    Write-Host "dotnet watch is already running (PID $($running.ProcessId -join ', ')). Log: $log"
    return
}

if ($running.Count -gt 0) {
    # Kill the whole tree (watch, the hosted app and the hot-reload child) so the port is freed.
    foreach ($p in $running) { & taskkill /PID $p.ProcessId /T /F 2>$null | Out-Null }
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force $logDir | Out-Null
"=== dotnet watch started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Set-Content $log

# Hidden, detached pwsh that merges all streams into the log (append, shared-read).
$command = "dotnet watch run --project '$project' --urls http://localhost:$port --non-interactive *>> '$log'"
Start-Process pwsh -WindowStyle Hidden -ArgumentList '-NoProfile', '-Command', $command

Write-Host "dotnet watch started on http://localhost:$port. Log: $log"
