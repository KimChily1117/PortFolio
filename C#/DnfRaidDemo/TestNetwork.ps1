param([int]$TimeoutSeconds = 30)
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'Build\DnfRaidDemo.exe'
$logsPath = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
if (-not (Test-Path -LiteralPath $exePath)) { throw "Executable not found: $exePath" }
$hostLog = Join-Path $logsPath 'network-host.log'
$clientLog = Join-Path $logsPath 'network-client.log'
$hostArgs = @('-batchmode', '-nographics', '--net-test-host', '-logFile', ('"{0}"' -f $hostLog))
$clientArgs = @('-batchmode', '-nographics', '--net-test-client', '-logFile', ('"{0}"' -f $clientLog))
$hostProcess = Start-Process -FilePath $exePath -ArgumentList $hostArgs -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 900
$clientProcess = Start-Process -FilePath $exePath -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
$hostFinished = $hostProcess.WaitForExit($TimeoutSeconds * 1000)
$clientFinished = $clientProcess.WaitForExit(5000)
if (-not $hostFinished) { Stop-Process -Id $hostProcess.Id }
if (-not $clientFinished) { Stop-Process -Id $clientProcess.Id }
$hostProcess.WaitForExit()
$clientProcess.WaitForExit()
$hostProcess.Refresh()
$clientProcess.Refresh()
$hostPassed = $hostFinished -and $hostProcess.ExitCode -eq 0 -and
    (Select-String -LiteralPath $hostLog -SimpleMatch '[DNF_NET_TEST_PASSED]' -Quiet)
$clientPassed = $clientFinished -and $clientProcess.ExitCode -eq 0 -and
    (Select-String -LiteralPath $clientLog -SimpleMatch '[DNF_NET_TEST_PASSED]' -Quiet)
[PSCustomObject]@{
    passed = $hostPassed -and $clientPassed
    hostPassed = $hostPassed
    clientPassed = $clientPassed
    hostExitCode = $hostProcess.ExitCode
    clientExitCode = $clientProcess.ExitCode
    hostLog = $hostLog
    clientLog = $clientLog
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $logsPath 'network-test-result.json')
if (-not ($hostPassed -and $clientPassed)) { exit 1 }
exit 0
