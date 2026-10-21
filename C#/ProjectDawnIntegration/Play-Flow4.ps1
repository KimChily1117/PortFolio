param([int]$Port=28182,[int]$ReturnAfter=65,[string]$AccountPrefix=('PD_Flow_'+(Get-Date -Format 'MMddHHmmss')),[ValidateRange(0,200)][int]$DelayMs=30,[ValidateRange(0,50)][int]$JitterMs=0,[switch]$SavedLooks,[switch]$Record,[switch]$CloudRuntime,[ValidateSet('MySql','SqlServer')][string]$Provider='MySql')
$ErrorActionPreference='Stop'
if($JitterMs -gt $DelayMs){throw 'JitterMs must not exceed DelayMs.'}
$run=Join-Path $PSScriptRoot ('Artifacts/flow-4p-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $run|Out-Null
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Flow server port is occupied.'}
if($DelayMs -gt 0 -and (Get-NetTCPConnection -State Listen -LocalPort ($Port+1) -ErrorAction SilentlyContinue)){throw 'Delay proxy port is occupied by a previous run. Stop that run before starting a new one.'}
$runtimeArg=if($CloudRuntime){' -CloudRuntime'}else{''}
$runtimeArg+=' -Provider '+$Provider
$server=Start-Process pwsh -ArgumentList (('-NoProfile -File "{0}" -Port {1}' -f (Join-Path $PSScriptRoot 'Start-FlowServer.ps1'),$Port)+$runtimeArg) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/server.log" -RedirectStandardError "$run/server-error.log"
$ready=$false
for($i=0;$i -lt 60;$i++){if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){$ready=$true;break};if($server.HasExited){throw 'Flow server failed; inspect server-error.log'};Start-Sleep -Milliseconds 200}
if(-not $ready){throw 'Flow server startup timed out'}
$proxy=$null;$clientPort=$Port
if($DelayMs -gt 0){$clientPort=$Port+1;$proxy=Start-Process python -ArgumentList ('"{0}" --listen {1} --target {2} --delay-ms {3} --jitter-ms {4} --seconds 240' -f (Join-Path $PSScriptRoot 'Tools/DelayTcp.py'),$clientPort,$Port,$DelayMs,$JitterMs) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/proxy.log" -RedirectStandardError "$run/proxy-error.log";Start-Sleep -Milliseconds 400}
if($proxy -and $proxy.HasExited){throw 'Delay proxy startup failed. Inspect proxy-error.log; clients were not launched.'}
$ids=@();$accounts=@()
foreach($i in 1..4){$out=Join-Path $run "p$i";New-Item -ItemType Directory -Force $out|Out-Null;$name=$AccountPrefix+'_'+$i;$accounts+=$name
 $style=if($SavedLooks){''}else{"--style-index $i"}
 if($Record){$style+=' --record-four'}
 $args='--flow --flow-auto {4} --account {0} --character {0} --port {1} --return-after {2} --output "{3}" -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile "{3}/player.log"' -f $name,$clientPort,$ReturnAfter,$out,$style
 $p=Start-Process (Join-Path $PSScriptRoot 'Build/ProjectDawnRaid.exe') -ArgumentList $args -PassThru;$ids+=$p.Id
}
@{server=$server.Id;proxy=if($proxy){$proxy.Id}else{0};players=$ids;accounts=$accounts;port=$Port;delayMs=$DelayMs;jitterMs=$JitterMs;jitterSeed=42;path=$run;dbProvider=$Provider}|ConvertTo-Json|Set-Content "$run/processes.json"
Write-Output $run
