param([int]$Port=28186,[int]$DelayMs=30,[ValidateSet('MySql','SqlServer')][string]$Provider='MySql',[ValidateSet('skip','full')][string]$Cinematic='full')
$ErrorActionPreference='Stop'
$run=Join-Path $PSScriptRoot ('Artifacts/rewards-4p-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $run|Out-Null
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Fixture port occupied'}
if(Get-NetTCPConnection -State Listen -LocalPort ($Port+1) -ErrorAction SilentlyContinue){throw 'Fixture proxy port occupied'}
if($Provider -eq 'MySql'){& ./Tools/Start-MySqlLocal.ps1;. ./Tools/Use-MySqlEnvironment.ps1}
else{$env:PROJECT_DAWN_DB_CONNECTION_STRING=(Get-Content 'E:/task/Server/Server/Server/bin/Debug/netcoreapp3.1/config.json' -Raw|ConvertFrom-Json).connectionString}
$env:PROJECT_DAWN_DB_PROVIDER=$Provider
$env:PROJECT_DAWN_CONFIG_PATH=Join-Path $PSScriptRoot 'flow-server.config.json'
$env:PROJECT_DAWN_REWARD_OUTBOX=Join-Path $run 'outbox'
$env:PROJECT_DAWN_MATCH_HISTORY_DISABLED='true'
$hostDll=Join-Path $PSScriptRoot 'RewardPlaytestHost/bin/Debug/net10.0/RewardPlaytestHost.dll'
$server=Start-Process 'dotnet' -ArgumentList ('"{0}" {1}' -f $hostDll,$Port) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/server.log" -RedirectStandardError "$run/server-error.log"
Remove-Item Env:/PROJECT_DAWN_DB_CONNECTION_STRING
$ready=$false
for($i=0;$i -lt 60;$i++){if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){$ready=$true;break};if($server.HasExited){throw 'Fixture server failed'};Start-Sleep -Milliseconds 200}
if(-not $ready){throw 'Fixture server startup timed out'}
$clientPort=$Port+1
$proxy=Start-Process python -ArgumentList ('"{0}" --listen {1} --target {2} --delay-ms {3} --seconds 180' -f (Join-Path $PSScriptRoot 'Tools/DelayTcp.py'),$clientPort,$Port,$DelayMs) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/proxy.log" -RedirectStandardError "$run/proxy-error.log"
Start-Sleep -Milliseconds 300
if($proxy.HasExited){throw 'Fixture proxy failed to start'}
$ids=@();$accounts=@();$prefix='PD_RwdFixture_'+(Get-Date -Format 'MMddHHmmss')
foreach($i in 1..4){
 $out=Join-Path $run "p$i";New-Item -ItemType Directory -Force $out|Out-Null;$name=$prefix+'_'+$i;$accounts+=$name
 $clientArgs='--flow --flow-auto --style-index {4} --record-four --account {0} --character {0} --port {1} --return-after 120 --output "{2}" -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile "{3}"' -f $name,$clientPort,$out,"$out/player.log",$i
 $clientArgs+=' --cinematic-auto '+$Cinematic
 $p=Start-Process (Join-Path $PSScriptRoot 'Build/ProjectDawnRaid.exe') -ArgumentList $clientArgs -PassThru;$ids+=$p.Id
}
@{server=$server.Id;proxy=$proxy.Id;players=$ids;accounts=$accounts;port=$Port;delayMs=$DelayMs;path=$run;dbProvider=$Provider;cinematic=$Cinematic;fixture='Boss HP reduced to 500 one second after intro, real attack/SQL grant path'}|ConvertTo-Json|Set-Content "$run/processes.json"
'Test-only reduced boss HP. All SQL account, matching, attack, clear and item grant operations are real. Not a normal-difficulty clear demonstration.'|Set-Content "$run/FIXTURE.txt"
Write-Output $run
