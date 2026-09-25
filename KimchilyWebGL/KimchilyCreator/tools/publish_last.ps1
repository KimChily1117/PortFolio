param(
    [string]$ServerUrl = 'http://127.0.0.1:8788',
    [string]$TokenFile = '',
    [string]$UnityEditor = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$creatorRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-KimchilyUnityProject -ProjectPath $creatorRoot
$buildDirectory = (Get-Content -LiteralPath (Join-Path $creatorRoot 'Artifacts\last-build.txt') -Raw).Trim()
if (!$TokenFile) { $TokenFile = Join-Path $creatorRoot '..\KimchilyPublish\.local\token' }
$TokenFile = [IO.Path]::GetFullPath($TokenFile)
if (!(Test-Path -LiteralPath $TokenFile)) { throw 'Start the local publisher before publishing.' }
$log = Join-Path $creatorRoot 'Artifacts\editor-publish.log'
$result = Join-Path $creatorRoot 'Artifacts\publish-result.json'
$started = [DateTime]::UtcNow
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $creatorRoot + '"'),
    '-executeMethod', 'Kimchily.Creator.Editor.WorldPublisherBatch.Publish',
    '-publishBuildDirectory', ('"' + $buildDirectory + '"'), '-publishServerUrl', $ServerUrl,
    '-publishTokenFile', ('"' + $TokenFile + '"'), '-publishResultFile', ('"' + $result + '"'),
    '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $creatorRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(300000)) { $process.Kill(); throw "Editor publication timed out: $log" }
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result) -or (Get-Item -LiteralPath $result).LastWriteTimeUtc -lt $started) {
    throw "Editor publication did not complete: $log"
}
$published = Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
Write-Output ('Published: ' + $published.publishUrl)
Write-Output ('Result: ' + $result)
