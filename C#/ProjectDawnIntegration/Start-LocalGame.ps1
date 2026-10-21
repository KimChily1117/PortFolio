param([int]$Port=28182)
$ErrorActionPreference='Stop'
$listener=Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
if($listener){
 $owner=Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
 if($owner.CommandLine -notlike '*ProjectDawnIntegration*FlowHost.dll*'){throw 'Selected port is used by another application.'}
}else{
 $logs=Join-Path $PSScriptRoot 'Logs';New-Item -ItemType Directory -Force $logs|Out-Null
 $server=Start-Process pwsh -ArgumentList ('-NoProfile -File "{0}" -Port {1}' -f (Join-Path $PSScriptRoot 'Start-FlowServer.ps1'),$Port) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$logs/local-flow-server.log" -RedirectStandardError "$logs/local-flow-server-error.log"
 $ready=$false
 for($i=0;$i -lt 60;$i++){if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){$ready=$true;break};if($server.HasExited){break};Start-Sleep -Milliseconds 200}
 if(-not $ready){throw 'SQL game server could not start. See Logs/local-flow-server-error.log.'}
}
Start-Process (Join-Path $PSScriptRoot 'Build/ProjectDawnRaid.exe') -ArgumentList "--flow --port $Port"
