param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& $Dotnet build (Join-Path $root 'Build/UnityDependencies/UnityDependencies.csproj') -c Release --disable-build-servers -m:1
if($LASTEXITCODE -ne 0){throw 'Dependency restore failed'}
$plugins=Join-Path $root 'UnityClient/Assets/Plugins'
New-Item -ItemType Directory -Force $plugins|Out-Null
Get-ChildItem (Join-Path $root 'Build/UnityDependencies/bin/Release/netstandard2.0') -Filter '*.dll' |
 Where-Object Name -ne 'UnityDependencies.dll' | Copy-Item -Destination $plugins
Write-Output 'Unity plugin dependencies restored from pinned NuGet packages. Game resources remain separate.'
