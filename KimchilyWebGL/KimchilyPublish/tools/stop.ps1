param([string]$StateDirectory = '', [switch]$Json)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'server-common.ps1')
$context = Get-PublisherContext $StateDirectory
$mutex = $null
$exitCode = 0
try {
    $mutex = Enter-PublisherLock $context
    $state = Get-PublisherState $context -SkipUnregisteredPortCheck
    if ($state.status -in @('conflict', 'error')) { throw $state.message }
    if ($state.processVerified) {
        $process = Get-Process -Id $state.pid -ErrorAction Stop
        # Open a handle and recheck creation time before terminating this exact verified process.
        $null = $process.Handle
        if ($process.StartTime.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ') -ne $state.processStartTimeUtc) {
            throw 'Publisher process identity changed before stop. No process was stopped.'
        }
        $process.Kill()
        if (!$process.WaitForExit(5000)) { throw 'Publisher did not exit within 5 seconds. Check status and logs.' }
    }
    if (Test-Path -LiteralPath $context.pidPath) { Remove-Item -LiteralPath $context.pidPath }
    $state.pid = $null; $state.processStartTimeUtc = $null
    $state.processVerified = $false; $state.healthy = $false; $state.status = 'stopped'
    $state.message = 'Publisher is stopped. No unrelated process was modified.'
    Save-PublisherState $context $state
    Write-PublisherResult $state -Json:$Json
}
catch { Write-PublisherFailure $context $_.Exception.Message -Json:$Json; $exitCode = 1 }
finally { if ($mutex) { $mutex.ReleaseMutex(); $mutex.Dispose() } }
exit $exitCode
