param([int]$TimeoutSeconds = 30, [switch]$Visible)
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'Build\DnfRaidDemo.exe'
$logsPath = Join-Path $PSScriptRoot 'Logs'
$logPath = Join-Path $logsPath 'player-smoke.log'
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
if (-not (Test-Path -LiteralPath $exePath)) { throw "Executable not found: $exePath" }
$arguments = @('--smoke', '-logFile', ('"{0}"' -f $logPath))
$windowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$playerProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
$finished = $playerProcess.WaitForExit([Math]::Max(1, $TimeoutSeconds) * 1000)
$exitCode = $null
if ($finished) {
    $playerProcess.Refresh()
    $exitCode = $playerProcess.ExitCode
} else {
    # Only stop the process that this smoke test started.
    Stop-Process -Id $playerProcess.Id
    $playerProcess.WaitForExit()
}
$log = if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Raw } else { '' }
$issues = @('NullReferenceException', 'MissingReferenceException', 'TypeLoadException',
    'DllNotFoundException', 'IndexOutOfRangeException', 'ArgumentException',
    'StackOverflowException', 'Crash!!!') | Where-Object { $log.Contains($_) }
$passed = $finished -and $exitCode -eq 0 -and $issues.Count -eq 0
[PSCustomObject]@{
    passed = $passed
    unexpectedLogMarkers = @($issues)
    exitCode = $exitCode
    timedOut = -not $finished
    log = $logPath
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $logsPath 'smoke-result.json')
if (-not $passed) { exit 1 }
exit 0

