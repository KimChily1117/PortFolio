param(
    [ValidateSet('WebGL', 'Android', 'StandaloneWindows64')][string]$Target = 'WebGL',
    [string]$UnityEditor = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$creatorRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $creatorRoot
New-Item -ItemType Directory -Path (Join-Path $creatorRoot 'Artifacts') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $creatorRoot 'Temp') -Force | Out-Null
$log = Join-Path $creatorRoot ('Artifacts\world-build-' + $Target + '.log')
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $creatorRoot + '"'),
    '-buildTarget', $Target, '-executeMethod', 'Kimchily.Creator.Project.CreatorProjectSetup.BuildStarterWorld',
    '-quit', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $creatorRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(1200000)) { $process.Kill(); throw "World build timed out: $log" }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'KIMCHILY_CREATOR_BUILD_READY' -Quiet)) {
    throw "World build did not complete: $log"
}
Get-Content -LiteralPath (Join-Path $creatorRoot 'Artifacts\last-build.txt')
