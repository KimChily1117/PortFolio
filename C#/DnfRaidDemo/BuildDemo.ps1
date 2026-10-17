param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe',
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'UnityProject'
$logsPath = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
$method = if ($ValidateOnly) { 'DemoBuild.ValidateProject' } else { 'DemoBuild.BuildWindows' }
$logName = if ($ValidateOnly) { 'validate.log' } else { 'build.log' }
$logPath = Join-Path $logsPath $logName
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"{0}"' -f $projectPath),
    '-buildTarget', 'Win64', '-executeMethod', $method, '-logFile', ('"{0}"' -f $logPath))
$buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
$buildProcess.Refresh()
Write-Output "Unity exit code: $($buildProcess.ExitCode)"
Write-Output "Log: $logPath"
if ($buildProcess.ExitCode -ne 0) { exit 1 }
$successToken = if ($ValidateOnly) { '[DNF_BUILD_VALIDATED]' } else { '[DNF_BUILD_SUCCEEDED]' }
if (-not (Select-String -LiteralPath $logPath -SimpleMatch $successToken -Quiet)) {
    Write-Error "Unity exited without the expected success marker. Inspect $logPath"
    exit 1
}
exit 0
