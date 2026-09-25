param([string]$UnityEditor = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $projectRoot
$artifactRoot = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Temp') -Force | Out-Null
$log = Join-Path $artifactRoot 'android-export.log'
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-buildTarget', 'Android', '-executeMethod', 'Kimchily.World.Editor.AndroidRuntimeBuilder.Export',
    '-quit', '-logFile', ('"' + $log + '"'))
Write-Output "Exporting Android Unity library. Log: $log"
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(1800000)) { $process.Kill(); throw "Android export timed out: $log" }
if ($process.ExitCode -ne 0) { throw "Android export exited $($process.ExitCode): $log" }
if (-not (Select-String -LiteralPath $log -SimpleMatch 'KIMCHILY_ANDROID_EXPORT_READY' -Quiet)) {
    throw "Unity exited without completing Android export: $log"
}
Write-Output "Android unityLibrary exported to $projectRoot\Builds\Android\unityLibrary"
