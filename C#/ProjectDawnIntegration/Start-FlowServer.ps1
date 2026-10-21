param([int]$Port=28182,[switch]$CloudRuntime,[ValidateSet('MySql','SqlServer')][string]$Provider='MySql',[string]$DatabaseConfigPath='E:\task\Server\Server\Server\bin\Debug\netcoreapp3.1\config.json')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if($Provider -eq 'MySql'){
 if(-not $env:PROJECT_DAWN_DB_CONNECTION_STRING){
  & (Join-Path $PSScriptRoot 'Tools/Start-MySqlLocal.ps1')
  . (Join-Path $PSScriptRoot 'Tools/Use-MySqlEnvironment.ps1')
 }
 $CloudRuntime=$true
} elseif(-not $env:PROJECT_DAWN_DB_CONNECTION_STRING){
 $source=Get-Content -LiteralPath $DatabaseConfigPath -Raw|ConvertFrom-Json
 $env:PROJECT_DAWN_DB_CONNECTION_STRING=$source.connectionString
}
$env:PROJECT_DAWN_DB_PROVIDER=$Provider
if(-not $env:PROJECT_DAWN_DB_CONNECTION_STRING){throw 'Configure PROJECT_DAWN_DB_CONNECTION_STRING on the server.'}
$env:PROJECT_DAWN_CONFIG_PATH=Join-Path $PSScriptRoot 'flow-server.config.json'
$env:PROJECT_DAWN_MATCH_HISTORY_DISABLED='false'
if($CloudRuntime){& dotnet (Join-Path $PSScriptRoot 'FlowHost/bin/Debug/net10.0/FlowHost.dll') $Port}
else{& dotnet (Join-Path $PSScriptRoot 'FlowHost/bin/Debug/netcoreapp3.1/FlowHost.dll') $Port}
exit $LASTEXITCODE
