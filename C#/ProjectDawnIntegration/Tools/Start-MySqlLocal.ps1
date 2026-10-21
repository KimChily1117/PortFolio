param([string]$ToolsRoot='E:/task/ProjectDawnTools/mysql')
$ErrorActionPreference='Stop'
$private=Join-Path $ToolsRoot 'private'
$cfg=Get-Content -LiteralPath (Join-Path $private 'local.json') -Raw|ConvertFrom-Json
$listener=Get-NetTCPConnection -State Listen -LocalPort $cfg.port -ErrorAction SilentlyContinue
if($listener){
 $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
 if($process.ExecutablePath -ne $cfg.binary){throw 'MySQL port belongs to another process'}
 return
}
$p=Start-Process $cfg.binary -ArgumentList "--defaults-file=$private/my.ini" -WindowStyle Hidden -PassThru
$p.Id|Set-Content (Join-Path $private 'mysql.pid')
for($i=0;$i -lt 100;$i++){if(Get-NetTCPConnection -State Listen -LocalPort $cfg.port -ErrorAction SilentlyContinue){return};if($p.HasExited){break};Start-Sleep -Milliseconds 200}
throw 'MySQL startup failed; inspect private error log'
