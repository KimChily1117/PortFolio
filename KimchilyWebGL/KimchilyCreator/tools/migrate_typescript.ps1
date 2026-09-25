param([string]$UnityEditor = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$creatorRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $creatorRoot
$log = Join-Path $creatorRoot 'Artifacts\typescript-migration.log'
New-Item -ItemType Directory -Path (Split-Path -Parent $log) -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $creatorRoot 'Temp') -Force | Out-Null
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $creatorRoot + '"'),
    '-executeMethod', 'Kimchily.Creator.Project.CreatorProjectSetup.MigrateStarterWorldToTypeScript',
    '-quit', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $creatorRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(600000)) { $process.Kill(); throw "TypeScript migration timed out: $log" }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'KIMCHILY_TYPESCRIPT_MIGRATION_READY' -Quiet)) {
    throw "TypeScript migration failed: $log"
}
Write-Output "Starter scene now uses TypeScript. Original scene backup: Artifacts/typescript-migration. Log: $log"
