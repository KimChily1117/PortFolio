[CmdletBinding()]
param(
    [string]$ConfigPath,
    [switch]$CheckDatabase,
    [switch]$InitializeDatabase
)

$ErrorActionPreference = 'Stop'
$taskProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\Server\Server\Server.csproj')).Path
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot '..\Server\Server\config.example.json'
}
$taskConfigPath = (Resolve-Path -LiteralPath $ConfigPath).Path
if (!$CheckDatabase -and (Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue)) {
    throw 'TCP port 8080 is already in use. Stop the existing server before starting another one.'
}

# Build outside the checkout so tracked legacy bin/obj files remain untouched.
$taskBuildRoot = Join-Path $env:LOCALAPPDATA 'ProjectDawn\local-server'
$taskOutputPath = Join-Path $taskBuildRoot 'server'
$taskPropsPath = Join-Path $taskBuildRoot 'isolated-build.props'
New-Item -ItemType Directory -Path $taskBuildRoot -Force | Out-Null
$taskEscapedBuildRoot = [Security.SecurityElement]::Escape($taskBuildRoot.Replace('\', '/'))
$taskPropsTemplate = @'
<Project>
  <PropertyGroup>
    <BaseIntermediateOutputPath>{0}/obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>
    <DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)/obj/**;$(MSBuildProjectDirectory)/bin/**</DefaultItemExcludes>
  </PropertyGroup>
</Project>
'@
[IO.File]::WriteAllText($taskPropsPath, ($taskPropsTemplate -f $taskEscapedBuildRoot))
& dotnet build $taskProjectPath -o $taskOutputPath "-p:DirectoryBuildPropsPath=$taskPropsPath" -p:GeneratePackageOnBuild=false -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }

$taskPreviousConfig = $env:PROJECT_DAWN_CONFIG_PATH
try {
    $env:PROJECT_DAWN_CONFIG_PATH = $taskConfigPath
    $taskServerArguments = @()
    if ($CheckDatabase) { $taskServerArguments += '--check-database' }
    if ($InitializeDatabase) { $taskServerArguments += '--initialize-database' }
    & dotnet (Join-Path $taskOutputPath 'Server.dll') @taskServerArguments
    exit $LASTEXITCODE
} finally {
    $env:PROJECT_DAWN_CONFIG_PATH = $taskPreviousConfig
}
