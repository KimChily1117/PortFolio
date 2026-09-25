param(
    [string]$UnityEditor = '',
    [ValidateSet('PlayMode', 'EditMode')][string]$Platform = 'PlayMode'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $projectRoot
$artifactRoot = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Temp') -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets\Generated\DemoWorld.unity'))) {
    throw 'Prepare sample scenes (or run export_webgl.ps1 -PrepareOnly) before running the bridge tests.'
}
$log = Join-Path $artifactRoot ('runtime-' + $Platform.ToLowerInvariant() + '.log')
$result = Join-Path $artifactRoot ('runtime-' + $Platform.ToLowerInvariant() + '.xml')
$started = [DateTime]::UtcNow
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
    '-runTests', '-testPlatform', $Platform, '-testResults', ('"' + $result + '"'),
    '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(600000)) { $process.Kill(); throw "Runtime tests timed out: $log" }
if ($process.ExitCode -ne 0) { throw "Runtime tests exited $($process.ExitCode): $log" }
if (-not (Test-Path -LiteralPath $result)) { throw 'Unity did not generate the test report.' }
if ((Get-Item -LiteralPath $result).LastWriteTimeUtc -lt $started) { throw 'Unity test report was not updated.' }
[xml]$report = Get-Content -LiteralPath $result -Raw
$run = $report.'test-run'
$minimum = if ($Platform -eq 'PlayMode') { 54 } else { 12 }
if ($run.result -ne 'Passed' -or [int]$run.passed -lt $minimum -or [int]$run.failed -ne 0 -or
    [int]$run.skipped -ne 0 -or [int]$run.inconclusive -ne 0) {
    throw "Incomplete or failing runtime tests: $result"
}
Write-Output "Unity runtime $Platform tests passed: $($run.passed). Report: $result"
