[CmdletBinding()]
param([ValidateSet('start', 'stop', 'status')][string]$Action = 'status')

$ErrorActionPreference = 'Stop'
$taskName = 'Kimchily-RemotePlay-Servers'
$taskLauncher = Join-Path $PSScriptRoot 'Start-GameServers.ps1'
$taskKnownLaunchers = @($taskLauncher, 'E:\task\RemotePlay\Start-GameServers.ps1')
$taskServices = @(
    @{ Name = 'Dawn'; Port = 8080; Executables = @('dotnet.exe'); Markers = @('C:\Users\yeop_sun\AppData\Local\ProjectDawn\local-server\server\Server.dll') },
    @{ Name = 'Publisher'; Port = 8788; Executables = @('python.exe', 'pythonw.exe'); Markers = @('E:\task\KimchilyWebGL\KimchilyPublish\server.py') },
    @{ Name = 'Realtime'; Port = 8790; Executables = @('dotnet.exe'); Markers = @('E:\task\KimchilyWebGL\KimchilyServer\.tools\dotnet-10.0.401\dotnet.exe', 'Kimchily.Server.Host.dll') }
)

function Test-OwnedProcess($Process, [string[]]$Executables, [string[]]$Markers) {
    if (!$Process -or !$Process.CommandLine -or $Process.Name -notin $Executables) { return $false }
    foreach ($taskMarker in $Markers) {
        if ($Process.CommandLine.IndexOf($taskMarker, [StringComparison]::OrdinalIgnoreCase) -lt 0) { return $false }
    }
    return $true
}

function Get-OwnedServers {
    foreach ($taskService in $taskServices) {
        $taskListeners = @(Get-NetTCPConnection -LocalPort $taskService.Port -State Listen -ErrorAction SilentlyContinue)
        if (!$taskListeners.Count) { continue }
        $taskOwners = @($taskListeners.OwningProcess | Select-Object -Unique)
        if ($taskOwners.Count -ne 1) { throw "Port $($taskService.Port) has unexpected ownership; no process was stopped." }
        $taskProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($taskOwners[0])"
        if (!(Test-OwnedProcess $taskProcess $taskService.Executables $taskService.Markers)) {
            throw "Port $($taskService.Port) belongs to an unrecognized process; it was left unchanged."
        }
        [pscustomobject]@{ Service = $taskService.Name; Port = $taskService.Port; Process = $taskProcess; Definition = $taskService }
    }
}

function Stop-VerifiedProcess($Process, [string[]]$Executables, [string[]]$Markers) {
    if ($Process.ProcessId -eq $PID) { throw 'Refusing to stop this control process.' }
    $taskCurrent = Get-CimInstance Win32_Process -Filter "ProcessId=$($Process.ProcessId)"
    if (!$taskCurrent) { return }
    if ($taskCurrent.CreationDate -ne $Process.CreationDate -or !(Test-OwnedProcess $taskCurrent $Executables $Markers)) {
        throw "Process $($Process.ProcessId) changed identity; it was left unchanged."
    }
    Stop-Process -Id $taskCurrent.ProcessId -Force -ErrorAction Stop
}

function Stop-AutomaticLauncher {
    $taskExisting = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($taskExisting) {
        if (!$taskExisting.Actions.Count) { throw 'The existing scheduled task has no recognized action; it was left unchanged.' }
        foreach ($taskExistingAction in $taskExisting.Actions) {
            $taskMatched = $false
            foreach ($taskPath in $taskKnownLaunchers) {
                if ($taskExistingAction.Arguments -and $taskExistingAction.Arguments.IndexOf($taskPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $taskMatched = $true }
            }
            if (!$taskMatched) { throw 'A different scheduled task uses this name; it was left unchanged.' }
        }
        Disable-ScheduledTask -TaskName $taskName | Out-Null
        Stop-ScheduledTask -TaskName $taskName
        if ((Get-ScheduledTask -TaskName $taskName).State -ne 'Disabled') { throw 'Could not disable automatic startup.' }
    }
    # Also stop a known supervisor launched with powershell -File outside the task.
    foreach ($taskProcess in @(Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('powershell.exe', 'pwsh.exe') -and $_.ProcessId -ne $PID })) {
        foreach ($taskPath in $taskKnownLaunchers) {
            if (Test-OwnedProcess $taskProcess @('powershell.exe', 'pwsh.exe') @($taskPath)) {
                Stop-VerifiedProcess $taskProcess @('powershell.exe', 'pwsh.exe') @($taskPath)
                break
            }
        }
    }
    # A watcher running inside an interactive terminal has no script path in its
    # process command line. Detect its mutex rather than killing the terminal.
    $taskWatcherMutex = [Threading.Mutex]::new($false, 'Global\KimchilyRemotePlayServers')
    $taskWatcherReleased = $false
    try {
        try { $taskWatcherReleased = $taskWatcherMutex.WaitOne(5000) }
        catch [Threading.AbandonedMutexException] { $taskWatcherReleased = $true }
        if (!$taskWatcherReleased) { throw 'A manual watcher is still running. Press Ctrl+C in its terminal, then retry this command.' }
    } finally {
        if ($taskWatcherReleased) { $taskWatcherMutex.ReleaseMutex() }
        $taskWatcherMutex.Dispose()
    }
}

# This launcher has the same machine-specific paths as Start-GameServers.ps1.
# Refuse to change a laptop's tasks or processes when copied there for VPN use.
if (!(Test-Path -LiteralPath 'E:\task\Server') -or !(Test-Path -LiteralPath 'E:\task\KimchilyWebGL')) {
    throw 'Run this command on the server PC, which has E:\task\Server and E:\task\KimchilyWebGL.'
}

try {
    # Validate every listening service before changing tasks or stopping anything.
    $taskOwned = @(Get-OwnedServers)
    if ($Action -eq 'status') {
        foreach ($taskService in $taskServices) {
            $taskEntry = $taskOwned | Where-Object { $_.Service -eq $taskService.Name }
            [pscustomobject]@{ Service = $taskService.Name; Port = $taskService.Port; Running = [bool]$taskEntry; ProcessId = $taskEntry.Process.ProcessId }
        }
        $taskExisting = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        Write-Output "Automatic startup: $(if ($taskExisting) { $taskExisting.State } else { 'Not registered' })"
        exit 0
    }

    Stop-AutomaticLauncher
    if ($Action -eq 'start') {
        # One startup pass; no background watcher that restarts stopped servers.
        & 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File $taskLauncher -Once
        exit $LASTEXITCODE
    }

    foreach ($taskEntry in @(Get-OwnedServers)) {
        Stop-VerifiedProcess $taskEntry.Process $taskEntry.Definition.Executables $taskEntry.Definition.Markers
    }
    for ($taskAttempt = 0; $taskAttempt -lt 10; $taskAttempt++) {
        if (!(Get-OwnedServers)) { Write-Output 'All three game services stopped. Automatic startup is disabled.'; exit 0 }
        Start-Sleep -Milliseconds 500
    }
    throw 'A game service is still listening after the stop request.'
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
