param([switch]$Auto,[int]$Seconds=80,[int]$Port=28180,[ValidateRange(0,200)][int]$NetworkDelayMs=0)
$ErrorActionPreference='Stop'
if($Port -lt 1024 -or $Port -gt 65535){throw 'Port must be 1024..65535'}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw "Port $Port is already in use"}
$exe=Join-Path $PSScriptRoot 'Build/ProjectDawnRaid.exe'
$hostDll=Join-Path $PSScriptRoot 'UnityPlaytestHost/bin/Debug/netcoreapp3.1/UnityPlaytestHost.dll'
if(-not(Test-Path $exe) -or -not(Test-Path $hostDll)){throw 'Run Build-Unity.ps1 first'}
$run=Join-Path $PSScriptRoot ('Artifacts/unity-4p-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $run | Out-Null
# Fixture accounts only, loopback only, no DB endpoint or production credentials.
$duration=if($Auto){$Seconds+90}else{900}
$server=Start-Process dotnet -ArgumentList ('"{0}" {1} {2}' -f $hostDll,$Port,$duration) -WindowStyle Hidden -RedirectStandardOutput "$run/server.log" -RedirectStandardError "$run/server-error.log" -PassThru
$ready=$false
for($i=0;$i -lt 50;$i++) {
    if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){$ready=$true;break}
    if($server.HasExited){throw 'Playtest server exited'}
    Start-Sleep -Milliseconds 100
}
if(-not $ready){throw 'Playtest server did not start'}
$clientPort=$Port
$proxy=$null
if($NetworkDelayMs -gt 0) {
    $clientPort=$Port+1
    if($clientPort -gt 65535 -or (Get-NetTCPConnection -State Listen -LocalPort $clientPort -ErrorAction SilentlyContinue)){Stop-Process -Id $server.Id;throw 'Latency fixture port unavailable'}
    $proxyScript=Join-Path $PSScriptRoot 'Tools/DelayTcp.py'
    $proxy=Start-Process python -ArgumentList ('"{0}" --listen {1} --target {2} --delay-ms {3} --seconds {4}' -f $proxyScript,$clientPort,$Port,$NetworkDelayMs,$duration) -WindowStyle Hidden -RedirectStandardOutput "$run/proxy.log" -RedirectStandardError "$run/proxy-error.log" -PassThru
    $proxyReady=$false
    for($i=0;$i -lt 50;$i++) {
        if(Get-NetTCPConnection -State Listen -LocalPort $clientPort -ErrorAction SilentlyContinue){$proxyReady=$true;break}
        Start-Sleep -Milliseconds 100
    }
    if(-not $proxyReady){Stop-Process -Id $server.Id;if(-not $proxy.HasExited){Stop-Process -Id $proxy.Id};throw 'Latency fixture failed'}
}
$ids=@()
foreach($slot in 1..4) {
    $out=Join-Path $run "p$slot";New-Item -ItemType Directory -Force $out | Out-Null
    $autoArgs=if($Auto){"--dawn-auto --verify-seconds $Seconds"}else{''}
    $arguments='--dawn {0} --port {1} --output "{2}" -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile "{2}/player.log"' -f $autoArgs,$clientPort,$out
    # Visible windows are intentional: this is the user's actual four-player playtest.
    $player=Start-Process $exe -ArgumentList $arguments -PassThru;$ids+=$player.Id
}
@{server=$server.Id;proxy=if($proxy){$proxy.Id}else{0};networkDelayMs=$NetworkDelayMs;players=$ids;auto=[bool]$Auto;seconds=$Seconds;port=$Port;path=$run}|ConvertTo-Json|Set-Content "$run/processes.json"
Write-Output "Four Unity clients started. Evidence: $run"
