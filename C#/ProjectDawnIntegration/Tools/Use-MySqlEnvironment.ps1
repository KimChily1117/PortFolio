param([switch]$Admin,[string]$ToolsRoot='E:/task/ProjectDawnTools/mysql')
$ErrorActionPreference='Stop'
$cfg=Get-Content -LiteralPath (Join-Path $ToolsRoot 'private/local.json') -Raw|ConvertFrom-Json
$user=if($Admin){'root'}else{'dawn_game'}
$pass=if($Admin){$cfg.rootPassword}else{$cfg.gamePassword}
$env:PROJECT_DAWN_DB_PROVIDER='MySql'
$env:PROJECT_DAWN_DB_CONNECTION_STRING="Server=$($cfg.host);Port=$($cfg.port);Database=$($cfg.database);User ID=$user;Password=$pass;SslMode=Required;Pooling=true;MaximumPoolSize=32;Connection Timeout=5;Default Command Timeout=10;"
$env:PROJECT_DAWN_CONFIG_PATH=Join-Path (Split-Path $PSScriptRoot -Parent) 'flow-server.config.json'
$env:PROJECT_DAWN_REWARD_OUTBOX=Join-Path $ToolsRoot 'private/reward-outbox'
