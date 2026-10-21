param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    & python Tools/SyncAvatarCatalog.py
    if($LASTEXITCODE -ne 0){throw "Avatar catalog validation failed"}
    & ./Protocol/Generate.ps1
    Copy-Item Client/*.cs UnityClient/Assets/DawnNetwork/
    New-Item -ItemType Directory -Force UnityClient/Assets/Resources/DawnLua | Out-Null
    Copy-Item Lua/bakal_patterns.lua UnityClient/Assets/Resources/DawnLua/bakal_patterns.txt
    & ./Tools/Restore-UnityDependencies.ps1
    if($LASTEXITCODE -ne 0){throw 'Unity dependency restore failed'}
    Copy-Item Server/Server/Packet/Protocol.cs UnityClient/Assets/DawnNetwork/Protocol.cs
    Copy-Item Server/Server/Packet/Raid.cs UnityClient/Assets/DawnNetwork/Raid.cs
    & dotnet build UnityPlaytestHost/UnityPlaytestHost.csproj -p:NuGetAudit=false
    if($LASTEXITCODE -ne 0){throw 'Playtest server build failed'}
    & dotnet build FlowHost/FlowHost.csproj -p:NuGetAudit=false
    if($LASTEXITCODE -ne 0){throw 'SQL flow server build failed'}
    & dotnet build FlowHost/FlowHost.csproj -p:DawnCloud=true -p:NuGetAudit=false
    if($LASTEXITCODE -ne 0){throw 'MySQL .NET 10 server build failed'}
    $project=Join-Path $PSScriptRoot 'UnityClient'
    $log=Join-Path $PSScriptRoot 'Logs/unity-client-build.log'
    $process=Start-Process $UnityPath -ArgumentList ('-batchmode -quit -projectPath "{0}" -executeMethod DawnBuild.Build -logFile "{1}"' -f $project,$log) -WindowStyle Hidden -PassThru
    $process.WaitForExit();$process.Refresh()
    if($process.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -SimpleMatch 'Build Finished, Result: Success.' -Quiet)){throw "Unity build failed: $log"}
    Copy-Item Cloud/Start-ProjectDawn.cmd Build/Start-ProjectDawn.cmd
} finally {Pop-Location}
