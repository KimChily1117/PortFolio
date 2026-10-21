param([switch]$VerifyRestore,[string]$ToolsRoot='E:/task/ProjectDawnTools/mysql')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$private=Join-Path $ToolsRoot 'private'
$cfg=Get-Content -LiteralPath "$private/local.json" -Raw|ConvertFrom-Json
$bin=Split-Path $cfg.binary -Parent
$stamp=Get-Date -Format yyyyMMdd_HHmmss
$dir=Join-Path $private "backups/$stamp"
$null=New-Item -ItemType Directory -Force $dir
$client=Join-Path $dir 'client.cnf'
@"
[client]
host=127.0.0.1
port=$($cfg.port)
user=root
password=$($cfg.rootPassword)
ssl-mode=REQUIRED
default-character-set=utf8mb4
"@|Set-Content -LiteralPath $client -Encoding utf8NoBOM
try{
 $dump=Join-Path $dir 'projectdawn.sql'
 & "$bin/mysqldump.exe" "--defaults-extra-file=$client" --single-transaction --routines --triggers --set-gtid-purged=OFF --no-tablespaces "--result-file=$dump" projectdawn
 if($LASTEXITCODE -ne 0){throw 'MySQL dump failed'}
 $report=[ordered]@{backup=$dump;sha256=(Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash;restoreVerified=$false}
 if($VerifyRestore){
  $restore='dawn_restore_'+$stamp
  & "$bin/mysql.exe" "--defaults-extra-file=$client" -e "CREATE DATABASE $restore CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci"
  if($LASTEXITCODE -ne 0){throw 'Restore target creation failed'}
  $p=Start-Process "$bin/mysql.exe" -ArgumentList @("--defaults-extra-file=$client",$restore) -RedirectStandardInput $dump -RedirectStandardError "$dir/restore-error.log" -WindowStyle Hidden -PassThru -Wait
  if($p.ExitCode -ne 0){throw 'Restore failed; private log retained'}
  . (Join-Path $PSScriptRoot 'Use-MySqlEnvironment.ps1') -Admin
  $dotnet='dotnet';$tool=Join-Path $project 'MySqlTools/bin/Debug/net10.0/MySqlTools.dll'
  & $dotnet $tool fingerprint "$dir/source.json";if($LASTEXITCODE -ne 0){throw 'Source fingerprint failed'}
  $env:PROJECT_DAWN_DB_CONNECTION_STRING=$env:PROJECT_DAWN_DB_CONNECTION_STRING.Replace('Database=projectdawn;','Database='+$restore+';')
  & $dotnet $tool fingerprint "$dir/restored.json";if($LASTEXITCODE -ne 0){throw 'Restore fingerprint failed'}
  if((Get-Content "$dir/source.json" -Raw) -ne (Get-Content "$dir/restored.json" -Raw)){throw 'Restore content mismatch; stop writes before verification'}
  $report.restoreVerified=$true;$report.restoreDatabase=$restore
 }
 $report|ConvertTo-Json|Set-Content -LiteralPath "$dir/report.json"
 $public=Join-Path $project 'Artifacts/mysql-migration';$null=New-Item -ItemType Directory -Force $public
 $report|ConvertTo-Json|Set-Content -LiteralPath "$public/backup-verification.json"
 Write-Output "[MYSQL_BACKUP_COMPLETE] restoreVerified=$($report.restoreVerified); private backup and separate restore database retained"
}finally{
 Remove-Item -LiteralPath $client -ErrorAction SilentlyContinue
 Remove-Item Env:PROJECT_DAWN_DB_CONNECTION_STRING -ErrorAction SilentlyContinue
}
