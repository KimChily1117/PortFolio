param([string]$UnityEditor = '', [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $projectRoot
$artifactRoot = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Temp') -Force | Out-Null
$stage = if ($PrepareOnly) { 'webgl-prepare' } else { 'webgl-build' }
$method = if ($PrepareOnly) { 'PrepareScenes' } else { 'Build' }
$marker = if ($PrepareOnly) { 'KIMCHILY_WEBGL_SCENES_READY' } else { 'KIMCHILY_WEBGL_BUILD_READY' }
$log = Join-Path $artifactRoot ($stage + '.log')
$started = [DateTime]::UtcNow
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-buildTarget', 'WebGL', '-executeMethod', ('Kimchily.World.Editor.WebRuntimeBuilder.' + $method),
    '-quit', '-logFile', ('"' + $log + '"'))
Write-Output "Running Unity Web stage: $stage. Log: $log"
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(1800000)) { $process.Kill(); throw "Web build timed out: $log" }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $log) -or
    (Get-Item -LiteralPath $log).LastWriteTimeUtc -lt $started -or
    -not (Select-String -LiteralPath $log -SimpleMatch $marker -Quiet)) {
    throw "Unity Web stage did not complete: $log"
}
if (-not $PrepareOnly) {
    $index = Join-Path $projectRoot 'Builds\WebGL\index.html'
    # Incremental builds retain an unchanged template's timestamp. The fresh
    # successful build log above proves this invocation completed.
    if (-not (Test-Path -LiteralPath $index)) {
        throw "Web build did not produce index.html: $index"
    }
    Write-Output "Web runtime: $index"
}
