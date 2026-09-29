# Integration checks use their own ephemeral port and data/state directory.
# The normal .local server, token, worlds and PID are never stopped or modified.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'server-common.ps1')
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $project ('Artifacts\server-management\' + [Guid]::NewGuid().ToString('N'))
$testContext = Get-PublisherContext (Join-Path $testRoot 'server state')
$shell = Join-Path $PSHOME 'powershell.exe'
$checks = [Collections.Generic.List[string]]::new()
$managedStarted = $false
$savedConnection = $null
$managedId = $null

function Assert-Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAILED: $Name" }
    $checks.Add($Name)
}
function Expect-Failure([scriptblock]$Action, [string]$Name) {
    $failed = $false
    try { $null = & $Action } catch { $failed = $true }
    Assert-Check $failed $Name
}
function Get-FailureMessage([scriptblock]$Action) {
    try { $null = & $Action } catch { return $_.Exception.Message }
    throw 'Expected the action to fail.'
}
function Invoke-Management([string]$Tool, [string[]]$Options = @(), [int]$ExpectedExit = 0, [string]$State = $testContext.stateDirectory) {
    $output = @(& $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "$Tool.ps1") -StateDirectory $State -Json @Options)
    $resultCode = $LASTEXITCODE
    $text = $output -join "`n"
    $result = $text | ConvertFrom-Json -ErrorAction Stop
    if ($resultCode -ne $ExpectedExit) { throw "$Tool returned exit $resultCode instead of $ExpectedExit : $($result.message)" }
    if ($result.schemaVersion -ne 1) { throw "$Tool emitted an unexpected schema." }
    $tokenPath = Join-Path $State 'token'
    if (Test-Path -LiteralPath $tokenPath) {
        $tokenValue = (Get-Content -LiteralPath $tokenPath -Raw).Trim()
        if ($tokenValue -and $text.Contains($tokenValue)) { throw 'A management response exposed the local token.' }
    }
    $result
}

try {
    Assert-Check ((New-PublisherState $testContext).port -eq 8788) 'The cloned publisher defaults to isolated port 8788'
    $fast = [pscustomobject]@{ address = '192.168.10.2'; interfaceName = 'Ethernet'; hasDefaultRoute = $true; metric = 10 }
    $slow = [pscustomobject]@{ address = '192.168.20.2'; interfaceName = 'Wi-Fi'; hasDefaultRoute = $true; metric = 30 }
    Assert-Check ((Select-PublisherLanAddress @($slow, $fast)) -eq $fast.address) 'LAN prefers the lowest effective default-route metric'
    $slow.metric = 10
    Expect-Failure { Select-PublisherLanAddress @($fast, $slow) } 'Ambiguous LAN selection requires an override'
    Assert-Check ((Select-PublisherLanAddress @($fast, $slow) $slow.address) -eq $slow.address) 'Explicit LAN selection resolves ambiguity'
    Expect-Failure { Select-PublisherLanAddress @() } 'Missing LAN address is an actionable error'
    Expect-Failure { ConvertTo-PublisherOrigin 'http://user:secret@example.com' } 'Credential-bearing public URL is rejected'
    Expect-Failure { ConvertTo-PublisherOrigin 'http://example.com/worlds' } 'Public URL path is rejected'

    # A clean interpreter reproduces a new laptop without changing installed packages.
    $pythonPath = (Get-Command python -ErrorAction Stop).Source
    $cleanPythonRoot = Join-Path $testRoot 'clean python'
    & $pythonPath -m venv --without-pip $cleanPythonRoot
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the isolated dependency-check interpreter.' }
    $cleanPython = Join-Path $cleanPythonRoot 'Scripts\python.exe'
    $probeContext = [pscustomobject]@{ projectRoot = Join-Path $testRoot "dependency probe's project" }
    $probeDependencies = Join-Path $probeContext.projectRoot '.deps'
    New-Item -ItemType Directory -Force -Path $probeDependencies | Out-Null
    $missingQr = Get-FailureMessage { Assert-PublisherPythonDependencies $probeContext $cleanPython }
    Assert-Check ($missingQr -match 'qrcode' -and $missingQr -match '-m pip install --target' -and
        $missingQr.Contains($cleanPython) -and $missingQr.Contains("dependency probe''s project") -and
        $missingQr -notmatch '10013|occupied') 'Missing qrcode reports the selected interpreter and a quoted install command, not a port error'
    [IO.File]::WriteAllText((Join-Path $probeDependencies 'qrcode.py'), '# import fixture')
    $missingPillow = Get-FailureMessage { Assert-PublisherPythonDependencies $probeContext $cleanPython }
    Assert-Check ($missingPillow -match 'Pillow \(PIL.Image\)') 'Missing Pillow is diagnosed after the project-local qrcode import succeeds'
    New-Item -ItemType Directory -Force -Path (Join-Path $probeDependencies 'PIL') | Out-Null
    [IO.File]::WriteAllText((Join-Path $probeDependencies 'PIL\__init__.py'), '')
    [IO.File]::WriteAllText((Join-Path $probeDependencies 'PIL\Image.py'), '# import fixture')
    Assert-PublisherPythonDependencies $probeContext $cleanPython
    Assert-Check $true 'Dependency checks use the project-local .deps directory with spaces and apostrophes'

    $exitInfo = [Diagnostics.ProcessStartInfo]::new($cleanPython, '-c "raise SystemExit(23)"')
    $exitInfo.UseShellExecute = $false; $exitInfo.CreateNoWindow = $true; $exitInfo.WindowStyle = 'Hidden'
    $exited = [Diagnostics.Process]::Start($exitInfo)
    try {
        if (!$exited.WaitForExit(10000)) { throw 'The startup-failure test process did not exit.' }
        $startupFailure = Get-FailureMessage { Wait-PublisherReady $exited 'http://127.0.0.1:1' $testContext }
        Assert-Check ($startupFailure.Contains($testContext.stderrLog) -and $startupFailure -match 'exited before becoming ready' -and
            $startupFailure -notmatch '10013|occupied') 'An exited publisher points to its error log without assuming a port conflict'
    }
    finally { $exited.Dispose() }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $testPort = $listener.LocalEndpoint.Port
    $listener.Stop()
    $options = @('-BindAddress', '127.0.0.1', '-Port', "$testPort", '-PublicBaseUrl', "http://127.0.0.1:$testPort")
    $running = Invoke-Management 'start' $options
    $managedStarted = $true; $managedId = $running.pid
    Assert-Check ($running.status -eq 'running' -and $running.healthy -and $running.processVerified) 'Isolated publisher starts only after health verification'
    Assert-Check ($running.port -eq $testPort -and $running.localUrl -eq "http://127.0.0.1:$testPort") 'Connection information matches the requested port'
    $savedConnection = Get-Content -LiteralPath $testContext.connectionPath -Raw -Encoding UTF8
    Assert-Check (!($savedConnection | ConvertFrom-Json).PSObject.Properties['token']) 'Connection JSON has no token field'
    $again = Invoke-Management 'start' $options
    Assert-Check ($again.pid -eq $managedId) 'Repeated start reuses the same PID'
    $status = Invoke-Management 'status'
    Assert-Check ($status.status -eq 'running' -and $status.pid -eq $managedId) 'Status verifies the registered live publisher'

    $changedOptions = @('-BindAddress', '127.0.0.1', '-Port', "$testPort", '-PublicBaseUrl', "http://localhost:$testPort")
    $rejected = Invoke-Management 'start' $changedOptions 1
    Assert-Check ($rejected.message -match 'different' -and (Test-PublisherHealth $running.localUrl)) 'Configuration mismatch is explicit and preserves the running server'
    $otherState = Join-Path $testRoot 'occupied port state'
    $occupied = Invoke-Management 'start' $options 1 $otherState
    Assert-Check ($occupied.message -match 'occupied' -and (Test-PublisherHealth $running.localUrl)) 'Occupied port rejects another server without stopping its owner'

    $altered = $savedConnection | ConvertFrom-Json
    $altered.processStartTimeUtc = '2000-01-01T00:00:00.000000Z'
    Save-PublisherState $testContext $altered
    $identityFailure = Invoke-Management 'stop' @() 1
    Assert-Check ($identityFailure.message -match 'identity/settings' -and (Test-PublisherHealth $running.localUrl)) 'Creation-time mismatch prevents termination'
    [IO.File]::WriteAllText($testContext.connectionPath, $savedConnection, [Text.UTF8Encoding]::new($false))
    $PID | Set-Content -LiteralPath $testContext.pidPath
    $wrongProcess = Invoke-Management 'stop' @() 1
    Assert-Check ($wrongProcess.message -match 'another process' -and (Test-PublisherHealth $running.localUrl)) 'A stale PID pointing to another process is never terminated'
    $managedId | Set-Content -LiteralPath $testContext.pidPath

    $stopped = Invoke-Management 'stop'
    $managedStarted = $false
    Assert-Check ($stopped.status -eq 'stopped' -and !(Test-PublisherHealth $running.localUrl)) 'Stop terminates only the registered publisher and confirms exit'
    Assert-Check ((Invoke-Management 'stop').status -eq 'stopped') 'Stopping an already stopped server is idempotent'
    Assert-Check ((Invoke-Management 'status').status -eq 'stopped') 'Status distinguishes a stopped server from a health failure'
    2147483647 | Set-Content -LiteralPath $testContext.pidPath
    Assert-Check ((Invoke-Management 'stop').status -eq 'stopped') 'A dead stale PID is safely cleared'

    $restart = Invoke-Management 'start' $options
    $managedStarted = $true; $managedId = $restart.pid
    Assert-Check ($restart.status -eq 'running' -and $restart.pid -ne $running.pid) 'A stopped server can be started again'
    $savedConnection = Get-Content -LiteralPath $testContext.connectionPath -Raw -Encoding UTF8
    [IO.File]::WriteAllText($testContext.connectionPath, '{invalid', [Text.UTF8Encoding]::new($false))
    $invalid = Invoke-Management 'stop' @() 1
    Assert-Check ($invalid.message -match 'invalid' -and (Test-PublisherHealth $running.localUrl)) 'Invalid connection metadata fails closed without stopping the server'
    [IO.File]::WriteAllText($testContext.connectionPath, $savedConnection, [Text.UTF8Encoding]::new($false))
    $null = Invoke-Management 'stop'
    $managedStarted = $false

    $result = [ordered]@{ schemaVersion = 1; passed = $checks.Count; failed = 0; testPort = $testPort; isolatedStateDirectory = $testContext.stateDirectory; checks = $checks.ToArray() }
    $evidencePath = Join-Path $testRoot 'results.json'
    [IO.File]::WriteAllText($evidencePath, ($result | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-Output "Server management checks: $($checks.Count) passed. Evidence: $evidencePath"
}
finally {
    # Restore test-owned metadata if an assertion failed during a tamper test, then use the same verified stop path.
    if ($managedStarted) {
        if ($savedConnection) { [IO.File]::WriteAllText($testContext.connectionPath, $savedConnection, [Text.UTF8Encoding]::new($false)) }
        if ($managedId) { $managedId | Set-Content -LiteralPath $testContext.pidPath }
        $null = Invoke-Management 'stop'
    }
}
