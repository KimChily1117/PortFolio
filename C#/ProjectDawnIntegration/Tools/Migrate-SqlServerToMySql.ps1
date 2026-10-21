param([string]$SourceConfig='E:/task/Server/Server/Server/bin/Debug/netcoreapp3.1/config.json')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try{
 . ./Tools/Use-MySqlEnvironment.ps1 -Admin
 $env:PROJECT_DAWN_SQLSERVER_SOURCE=(Get-Content -LiteralPath $SourceConfig -Raw|ConvertFrom-Json).connectionString
 $null=New-Item -ItemType Directory -Force Artifacts/mysql-migration
 & dotnet build MySqlTools/MySqlTools.csproj -p:DawnCloud=true -p:NuGetAudit=false
 if($LASTEXITCODE -ne 0){throw 'Migration tool build failed'}
 & dotnet MySqlTools/bin/Debug/net10.0/MySqlTools.dll migrate Artifacts/mysql-migration/copy-verification.json
 if($LASTEXITCODE -ne 0){throw 'Migration failed; existing target data is never overwritten'}
}finally{
 Remove-Item Env:PROJECT_DAWN_SQLSERVER_SOURCE,Env:PROJECT_DAWN_DB_CONNECTION_STRING -ErrorAction SilentlyContinue
 Pop-Location
}
