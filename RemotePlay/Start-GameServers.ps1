[CmdletBinding()]
param([switch]$Once)

# Machine-local launcher. Run as the owner of the existing MSSQLLocalDB instance.
# Never replaces an unknown port owner or stops a server used by connected players.
$ErrorActionPreference = 'Stop'
$taskStateRoot = Join-Path $PSScriptRoot '.local'
New-Item -ItemType Directory -Path $taskStateRoot -Force | Out-Null
$taskLog = Join-Path $taskStateRoot 'supervisor.log'
$taskMutex = [Threading.Mutex]::new($false, 'Global\KimchilyRemotePlayServers')
$taskLocked = $false
$taskPowerShell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
$taskDawnRoot = 'E:\task\Server'
$taskDawnDll = 'C:\Users\yeop_sun\AppData\Local\ProjectDawn\local-server\server\Server.dll'
$taskDawnConfig = 'E:\task\Server\Artifacts\lan-start-20261001\config.local.json'
$taskUgcRoot = 'E:\task\KimchilyWebGL\KimchilyServer'
$taskUgcDll = Join-Path $taskUgcRoot 'src\Kimchily.Server.Host\bin\Release\net10.0\Kimchily.Server.Host.dll'
$taskUgcDotnet = Join-Path $taskUgcRoot '.tools\dotnet-10.0.401\dotnet.exe'
$taskPublisher = 'E:\task\KimchilyWebGL\KimchilyPublish\tools\start.ps1'

function Write-Status([string]$Message) {
    ('{0} {1}' -f [DateTime]::UtcNow.ToString('o'), $Message) | Add-Content -LiteralPath $taskLog -Encoding UTF8
}

function Test-ServiceHealth([string]$Uri, [string]$ServiceName) {
    try {
        $taskHealth = Invoke-RestMethod -Uri $Uri -TimeoutSec 3 -UseBasicParsing
        return ($taskHealth.service -eq $ServiceName)
    } catch { return $false }
}

function Get-OwnedListener([int]$Port, [string[]]$CommandMarkers) {
    $taskListeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    if (!$taskListeners.Count) { return $null }
    $taskOwners = @($taskListeners.OwningProcess | Select-Object -Unique)
    if ($taskOwners.Count -ne 1) { throw "Port $Port has unexpected listener ownership." }
    $taskProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($taskOwners[0])"
    if (!$taskProcess -or !$taskProcess.CommandLine) { throw "Cannot verify port $Port owner." }
    foreach ($taskMarker in $CommandMarkers) {
        if ($taskProcess.CommandLine.IndexOf($taskMarker, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Port $Port belongs to another process; left unchanged."
        }
    }
    return [int]$taskProcess.ProcessId
}

function Start-Hidden([string]$Name, [string]$Executable, [string[]]$Arguments, [string]$Directory) {
    $taskStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
    $taskProcess = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $Directory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $taskStateRoot "$Name-$taskStamp.log") `
        -RedirectStandardError (Join-Path $taskStateRoot "$Name-$taskStamp-error.log") -PassThru
    Write-Status "Started $Name PID=$($taskProcess.Id)"
    return $taskProcess
}

function Wait-Service([string]$Uri, [string]$Name) {
    for ($taskAttempt = 0; $taskAttempt -lt 20; $taskAttempt++) {
        if (Test-ServiceHealth $Uri $Name) { return }
        Start-Sleep -Seconds 1
    }
    throw "$Name did not become healthy; inspect .local logs."
}

function Ensure-Dawn {
    $taskOwner = Get-OwnedListener 8080 @($taskDawnDll)
    if (!$taskOwner) {
        if (!(Test-Path -LiteralPath $taskDawnDll)) { throw 'Build the latest Dawn Task source before starting.' }
        if (!(Test-Path -LiteralPath $taskDawnConfig)) { throw 'Dawn local config is missing.' }
        $taskDbOutput = & 'C:\Program Files\Microsoft SQL Server\150\Tools\Binn\SqlLocalDB.exe' start MSSQLLocalDB 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'MSSQLLocalDB could not start for this Windows account.' }
        $taskOldConfig = $env:PROJECT_DAWN_CONFIG_PATH
        try {
            $env:PROJECT_DAWN_CONFIG_PATH = $taskDawnConfig
            $taskProcess = Start-Hidden 'dawn' 'C:\Program Files\dotnet\dotnet.exe' @($taskDawnDll) $taskDawnRoot
        } finally { $env:PROJECT_DAWN_CONFIG_PATH = $taskOldConfig }
    }
    Wait-Service 'http://127.0.0.1:8090/api/health' 'ProjectDawnGameServer'
    return Get-OwnedListener 8080 @($taskDawnDll)
}

function Ensure-Publisher {
    $taskOwner = Get-OwnedListener 8788 @('E:\task\KimchilyWebGL\KimchilyPublish\server.py')
    if (!$taskOwner) {
        # Windows PowerShell preserves the publisher's JSON timestamp as a string.
        $taskResult = & $taskPowerShell -NoProfile -ExecutionPolicy Bypass -File $taskPublisher -LanAddress 192.168.0.4 -Json 2>&1
        if ($LASTEXITCODE -ne 0) { throw ('Publisher startup failed: ' + ($taskResult -join ' ')) }
        Write-Status 'Started publisher with the existing local publish store.'
    }
    Wait-Service 'http://127.0.0.1:8788/health' 'Kimchily local publisher'
    return Get-OwnedListener 8788 @('E:\task\KimchilyWebGL\KimchilyPublish\server.py')
}

function Ensure-Realtime {
    # Existing manual launches may use a path relative to this known .NET runtime.
    $taskOwner = Get-OwnedListener 8790 @($taskUgcDotnet, 'Kimchily.Server.Host.dll')
    if (!$taskOwner) {
        if (!(Test-Path -LiteralPath $taskUgcDll)) { throw 'Build the UGC realtime host before starting.' }
        $taskArguments = @($taskUgcDll, '--urls', 'http://0.0.0.0:8790', '--Realtime:ScriptsRoot', (Join-Path $taskUgcRoot 'games'),
            '--Realtime:AllowedOrigins:0', 'http://127.0.0.1:8788', '--Realtime:AllowedOrigins:1', 'http://localhost:8788',
            '--Realtime:AllowedOrigins:2', 'http://192.168.0.4:8788')
        $taskProcess = Start-Hidden 'ugc-realtime' $taskUgcDotnet $taskArguments $taskUgcRoot
    }
    Wait-Service 'http://127.0.0.1:8790/health' 'kimchily-realtime'
    return Get-OwnedListener 8790 @($taskUgcDotnet, 'Kimchily.Server.Host.dll')
}

try {
    try { $taskLocked = $taskMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $taskLocked = $true }
    if (!$taskLocked) { Write-Output 'The game server supervisor is already running.'; exit 0 }
    Write-Status "Supervisor started. User=$([Security.Principal.WindowsIdentity]::GetCurrent().Name) Once=$Once"
    do {
        $taskResults = @()
        foreach ($taskService in @('Dawn','Publisher','Realtime')) {
            try {
                $taskOwner = & "Ensure-$taskService"
                $taskResults += [ordered]@{service=$taskService;ready=$true;pid=$taskOwner}
            } catch {
                Write-Status "$taskService ERROR: $($_.Exception.Message)"
                $taskResults += [ordered]@{service=$taskService;ready=$false;error=$_.Exception.Message}
            }
        }
        $taskSnapshot = [ordered]@{checkedAtUtc=[DateTime]::UtcNow.ToString('o');user=[Security.Principal.WindowsIdentity]::GetCurrent().Name;services=$taskResults}
        $taskSnapshot | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskStateRoot 'status.json') -Encoding UTF8
        if ($Once) {
            $taskSnapshot | ConvertTo-Json -Depth 6
            if (@($taskResults | Where-Object { !$_.ready }).Count) { exit 1 }
            exit 0
        }
        # Keep the scheduled action alive and retry missing services once per minute.
        Start-Sleep -Seconds 60
    } while ($true)
} finally {
    if ($taskLocked) { $taskMutex.ReleaseMutex() }
    $taskMutex.Dispose()
}
