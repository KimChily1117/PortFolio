param([switch]$Unity, [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Logs') -Force | Out-Null
Push-Location $PSScriptRoot
try {
    foreach ($framework in @('net8.0', 'netcoreapp3.1')) {
        & dotnet run --project Tests/ProjectDawn.Combat.Tests.csproj -f $framework -c Release -p:NuGetAudit=false 2>&1 |
            Tee-Object -FilePath (Join-Path $PSScriptRoot "Logs/combat-core-$framework.log")
        if ($LASTEXITCODE -ne 0) { throw "Core validation failed: $framework" }
    }
    & dotnet run --project TimelineTests/ProjectDawn.TimelineTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/timeline-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Presentation timeline validation failed' }
    & dotnet run --project ClockTests/ProjectDawn.ClockTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/clock-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Clock validation failed' }
    & dotnet run --project LuaTests/ProjectDawn.LuaTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/lua-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Lua/meteor validation failed' }
    & dotnet run --project ContentTests/ProjectDawn.ContentTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/content-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Shared content validation failed' }
    & dotnet build Server/Server/Server.csproj -c Release -p:NuGetAudit=false --nologo 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/server-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed' }
    & dotnet run --project NetworkTests/ProjectDawn.NetworkTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/network-validation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Four-player network validation failed' }
    & dotnet run --project Server/ServerRegressionTests/ServerRegressionTests.csproj -c Release -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'Logs/server-regression.log')
    if ($LASTEXITCODE -ne 0) { throw 'Existing server regression failed' }
    if ($Unity) {
        $source = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Tests/Program.cs'))
        $source = $source.Replace('internal static class Program', 'public static class CombatCoreValidation')
        $source = $source.Replace('private static int Main()', 'public static void Run() { if (Execute() != 0) throw new Exception("Combat core validation failed"); UnityEngine.Debug.Log("[DAWN_COMBAT_CORE_VALIDATED]"); }' + "`n" + '    private static int Execute()')
        [System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'UnityValidation/Assets/Editor/CombatCoreValidation.cs'), $source, [System.Text.UTF8Encoding]::new($false))
        $log = Join-Path $PSScriptRoot 'Logs/unity-core-validation.log'
        $project = Join-Path $PSScriptRoot 'UnityValidation'
        $arguments = '-batchmode -nographics -quit -projectPath "{0}" -executeMethod CombatCoreValidation.Run -logFile "{1}"' -f $project, $log
        $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        $process.Refresh()
        if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch '[DAWN_COMBAT_CORE_VALIDATED]' -Quiet)) {
            throw "Unity validation failed; see $log"
        }
    }
} finally { Pop-Location }
