param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 $output=Join-Path $root ('Artifacts/cloud-release-'+(Get-Date -Format yyyyMMdd-HHmmss))
 $stage=Join-Path $output 'release'
 New-Item -ItemType Directory -Force $stage|Out-Null
 & $Dotnet publish FlowHost/FlowHost.csproj -c Release -r linux-x64 --self-contained true -p:DawnCloud=true -o "$stage/server"
 if($LASTEXITCODE -ne 0){throw 'Cloud server publish failed'}
 & $Dotnet publish CloudGateway/CloudGateway.csproj -c Release -r linux-x64 --self-contained true -o "$stage/gateway"
 if($LASTEXITCODE -ne 0){throw 'TLS gateway publish failed'}
 Copy-Item Data "$stage/Data" -Recurse
 '{"dataPath":"Data"}'|Set-Content "$stage/flow-server.config.json" -Encoding utf8NoBOM
 New-Item -ItemType Directory -Force "$stage/ops"|Out-Null
 Copy-Item Cloud/launch.py,Cloud/dawn-server.service,Cloud/dawn-gateway.service "$stage/ops/"
 Copy-Item Cloud/freevm "$stage/ops/freevm" -Recurse
 Copy-Item Server/Server/Migrations/MySql/001_initial.sql,Server/Server/Migrations/MySql/002_member_auth.sql "$stage/ops/"
 $zip=Join-Path $output 'release.zip'
 Compress-Archive -Path "$stage/*" -DestinationPath $zip
 $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
 @{artifact=$zip;sha256=$hash;runtime='net10.0';platform='linux-x64';createdUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content "$output/manifest.json"
 Write-Output $output
}finally{Pop-Location}
