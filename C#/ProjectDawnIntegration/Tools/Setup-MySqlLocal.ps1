param([string]$ToolsRoot='E:/task/ProjectDawnTools/mysql',[int]$Port=3307)
$ErrorActionPreference='Stop'
$base=Join-Path $ToolsRoot 'mysql-8.4.11-winx64'
if(-not(Test-Path -LiteralPath "$base/bin/mysqld.exe")){
 $null=New-Item -ItemType Directory -Force $ToolsRoot
 $zip=Join-Path $ToolsRoot 'mysql-8.4.11-winx64.zip'
 if(-not(Test-Path -LiteralPath $zip)){Invoke-WebRequest 'https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.11-winx64.zip' -OutFile $zip}
 if((Get-FileHash -LiteralPath $zip -Algorithm MD5).Hash -ne '2e833921898a9a030ea6bfe81bd811bc'){throw 'Official MySQL package checksum mismatch'}
 Expand-Archive -LiteralPath $zip -DestinationPath $ToolsRoot
}
$private=Join-Path $ToolsRoot 'private'
$null=New-Item -ItemType Directory -Force $private
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $private /inheritance:r /grant:r "${identity}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if($LASTEXITCODE -ne 0){throw 'Cannot secure MySQL private directory'}
$secret=Join-Path $private 'local.json'
if(Test-Path -LiteralPath $secret){throw 'Existing MySQL credentials found. Use Start-MySqlLocal.ps1; initialization never resets data.'}
$data=Join-Path $private 'data'
if(Test-Path -LiteralPath $data){throw 'Data directory already exists; inspect previous initialization.'}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'MySQL port already occupied'}
$rootPassword=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$gamePassword=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$settings=@{host='127.0.0.1';port=$Port;database='projectdawn';rootPassword=$rootPassword;gamePassword=$gamePassword;data=$data;binary=(Join-Path $base 'bin/mysqld.exe')}
$settings|ConvertTo-Json|Set-Content -LiteralPath $secret
$ini=Join-Path $private 'my.ini'
@"
[mysqld]
basedir=$($base.Replace('\','/'))
datadir=$($data.Replace('\','/'))
port=$Port
bind-address=127.0.0.1
mysqlx=0
character-set-server=utf8mb4
collation-server=utf8mb4_0900_ai_ci
default-time-zone=+00:00
innodb_flush_log_at_trx_commit=1
sync_binlog=1
log-bin=dawn-bin
binlog_expire_logs_seconds=604800
max_connections=64
require_secure_transport=ON
slow_query_log=ON
slow_query_log_file=$($private.Replace('\','/'))/mysql-slow.log
long_query_time=0.2
log-error=$($private.Replace('\','/'))/mysql-error.log
"@|Set-Content -LiteralPath $ini -Encoding utf8NoBOM
& $settings.binary "--defaults-file=$ini" --initialize-insecure
if($LASTEXITCODE -ne 0){throw 'MySQL initialization failed; inspect private error log'}
$init=Join-Path $private 'first-start.sql'
@"
ALTER USER 'root'@'localhost' IDENTIFIED BY '$rootPassword';
CREATE DATABASE projectdawn CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'dawn_game'@'localhost' IDENTIFIED BY '$gamePassword';
GRANT SELECT,INSERT,UPDATE,DELETE ON projectdawn.* TO 'dawn_game'@'localhost';
"@|Set-Content -LiteralPath $init -Encoding utf8NoBOM
$p=Start-Process $settings.binary -ArgumentList @("--defaults-file=$ini","--init-file=$init") -WindowStyle Hidden -PassThru
$p.Id|Set-Content (Join-Path $private 'mysql.pid')
$ready=$false
for($i=0;$i -lt 100;$i++){if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){$ready=$true;break};if($p.HasExited){break};Start-Sleep -Milliseconds 200}
if(-not $ready){throw 'MySQL start failed; inspect private error log'}
Remove-Item -LiteralPath $init
Write-Output "[MYSQL_READY] loopback:$Port; utf8mb4; InnoDB; UTC; TLS; separate runtime user; credentials outside project"
