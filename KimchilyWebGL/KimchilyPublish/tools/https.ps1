param(
    [ValidateSet('start', 'status', 'stop')][string]$Action = 'status',
    [string]$LanAddress = '',
    [int]$Port = 8789,
    [switch]$Json
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'server-common.ps1')
$publisher = Get-PublisherContext
$tlsDirectory = Join-Path $publisher.stateDirectory 'tls'
$controlDirectory = Join-Path $publisher.stateDirectory 'tls-process'
$statePath = Join-Path $controlDirectory 'connection.json'
$launcher = Join-Path $PSScriptRoot 'https-server.py'
$certificateTool = Join-Path $PSScriptRoot 'local-cert.py'
$caPath = Join-Path $tlsDirectory 'ca.pem'
$certificatePath = Join-Path $tlsDirectory 'server.pem'
$keyPath = Join-Path $tlsDirectory 'server-key.pem'
$mutex = $null
$exitCode = 0

function Test-HttpsHealth([int]$HttpsPort) {
    try {
        $result = & python $certificateTool --check "https://127.0.0.1:$HttpsPort" --ca $caPath 2>$null
        return ($LASTEXITCODE -eq 0 -and $result -contains 'HTTPS_HEALTH_VERIFIED')
    }
    catch { return $false }
}

function ConvertTo-HttpsUtcTimestamp($Value) {
    # PowerShell 7 converts JSON timestamps into DateTime, while Windows
    # PowerShell 5 keeps strings. Compare the same exact UTC precision in both.
    $format = 'yyyy-MM-ddTHH:mm:ss.ffffffZ'
    if ($Value -is [datetime]) { return $Value.ToUniversalTime().ToString($format, [Globalization.CultureInfo]::InvariantCulture) }
    if ($Value -isnot [string]) { throw 'Invalid HTTPS process timestamp. No process was modified.' }
    $parsed = [datetime]::MinValue
    $style = [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
    if (![datetime]::TryParseExact($Value, $format, [Globalization.CultureInfo]::InvariantCulture, $style, [ref]$parsed)) {
        throw 'Invalid HTTPS process timestamp. No process was modified.'
    }
    $parsed.ToString($format, [Globalization.CultureInfo]::InvariantCulture)
}

function Read-HttpsState {
    if (!(Test-Path -LiteralPath $statePath)) {
        return [pscustomobject]@{ status = 'stopped'; pid = $null; processVerified = $false; healthy = $false; message = 'HTTPS web app is stopped.' }
    }
    $savedState = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($savedState.schemaVersion -ne 1 -or [int]$savedState.port -lt 1 -or [int]$savedState.port -gt 65535 -or
        $savedState.launcher -ne $launcher -or $savedState.dataDirectory -ne $publisher.stateDirectory) {
        throw 'Invalid HTTPS process state. No process was modified.'
    }
    $savedState.processStartTimeUtc = ConvertTo-HttpsUtcTimestamp $savedState.processStartTimeUtc
    $savedState.processVerified = $false; $savedState.healthy = $false
    if (!$savedState.pid) { $savedState.status = 'stopped'; return $savedState }
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$($savedState.pid)" -ErrorAction Stop
    if (!$processInfo) { $savedState.status = 'stopped'; $savedState.pid = $null; return $savedState }
    $pattern = '^\s*(?:"[^"\r\n]+"|[^\s"]+)\s+(?:-u\s+)?"?' + [regex]::Escape($launcher) + '"?(?=\s|$)'
    if ($processInfo.Name -notmatch '^python(?:w|[0-9.]+)?\.exe$' -or !$processInfo.CommandLine -or
        ![regex]::IsMatch($processInfo.CommandLine.Replace('/', '\'), $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase) -or
        (Get-PublisherOption $processInfo.CommandLine 'port') -ne [string]$savedState.port -or
        (Get-PublisherOption $processInfo.CommandLine 'host') -ne '0.0.0.0' -or
        (Get-PublisherOption $processInfo.CommandLine 'public-base-url') -ne $savedState.publicUrl -or
        (Get-PublisherOption $processInfo.CommandLine 'link-origin') -ne $savedState.httpOrigin -or
        [IO.Path]::GetFullPath((Get-PublisherOption $processInfo.CommandLine 'data-dir')) -ne $publisher.stateDirectory -or
        [IO.Path]::GetFullPath((Get-PublisherOption $processInfo.CommandLine 'tls-cert')) -ne $certificatePath -or
        [IO.Path]::GetFullPath((Get-PublisherOption $processInfo.CommandLine 'tls-key')) -ne $keyPath -or
        $processInfo.CreationDate.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ') -ne $savedState.processStartTimeUtc) {
        throw 'HTTPS PID belongs to a different process or settings. No process was modified.'
    }
    $savedState.processVerified = $true
    $savedState.healthy = Test-HttpsHealth $savedState.port
    $savedState.status = if ($savedState.healthy) { 'running' } else { 'unhealthy' }
    $savedState.message = if ($savedState.healthy) { 'HTTPS web app is running; certificate and health verified.' } else { 'HTTPS process is running but health failed; inspect its log.' }
    $savedState
}

function Save-HttpsState($Value) {
    $temporary = $statePath + '.tmp'
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $statePath -Force
}

try {
    $mutex = Enter-PublisherLock ([pscustomobject]@{stateDirectory=$controlDirectory})
    $state = Read-HttpsState
    if ($Action -eq 'start') {
        if ($Port -lt 1 -or $Port -gt 65535) { throw 'Invalid HTTPS port.' }
        $address = Select-PublisherLanAddress @(Get-PublisherLanCandidates) $LanAddress
        $publicUrl = "https://${address}:$Port"
        $httpState = Get-PublisherState $publisher
        if ($httpState.status -ne 'running') { throw 'Start the HTTP publisher with tools/start.ps1 first; it provides authoring and phone certificate setup.' }
        $httpOrigin = ConvertTo-PublisherOrigin $httpState.publicUrl
        if ($state.processVerified) {
            if ($state.publicUrl -ne $publicUrl -or $state.httpOrigin -ne $httpOrigin) {
                throw 'HTTPS is running with different settings. Stop it explicitly before changing the address or port.'
            }
            if (!$state.healthy) { throw $state.message }
        }
        else {
            $owners = @(Get-PublisherPortOwners $Port)
            if ($owners.Count) { throw "HTTPS port $Port is occupied: $($owners -join ', '). No process was stopped." }
            New-Item -ItemType Directory -Path $controlDirectory -Force | Out-Null
            $certificateInfo = & python $certificateTool --directory $tlsDirectory --address $address --port $Port
            if ($LASTEXITCODE -ne 0) { throw 'Certificate setup failed. Install TLS dependencies with tools/install-tls.ps1 and inspect the preceding error.' }
            $publicInfo = $certificateInfo | ConvertFrom-Json
            $pythonPath = (Get-Command python -ErrorAction Stop).Source
            $launchArguments = @('-u', ('"' + $launcher + '"'), '--host', '0.0.0.0', '--port', "$Port", '--public-base-url', $publicUrl,
                '--data-dir', ('"' + $publisher.stateDirectory + '"'), '--tls-cert', ('"' + $certificatePath + '"'),
                '--tls-key', ('"' + $keyPath + '"'), '--link-origin', $httpOrigin)
            $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
            $created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
                CommandLine=('"' + $pythonPath + '" ' + ($launchArguments -join ' ')); CurrentDirectory=$publisher.projectRoot; ProcessStartupInformation=$startup
            }
            if ($created.ReturnValue -ne 0) { throw "Windows could not start HTTPS (WMI $($created.ReturnValue))." }
            $child = Get-Process -Id $created.ProcessId -ErrorAction Stop
            $identity = Get-CimInstance Win32_Process -Filter "ProcessId=$($child.Id)"
            $state = [pscustomobject]@{
                schemaVersion=1; status='starting'; pid=$child.Id; port=$Port; publicUrl=$publicUrl; httpOrigin=$httpOrigin
                setupUrl=($httpOrigin+'/dev/setup'); caSha256=$publicInfo.caSha256; launcher=$launcher; dataDirectory=$publisher.stateDirectory
                processStartTimeUtc=$identity.CreationDate.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ')
                processVerified=$true; healthy=$false; message='HTTPS web app is starting.'; log=(Join-Path $controlDirectory 'server.log')
            }
            Save-HttpsState $state
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while ($timer.ElapsedMilliseconds -lt 15000) {
                $child.Refresh()
                if ($child.HasExited) { throw "HTTPS exited during startup. Log: $($state.log)" }
                if (Test-HttpsHealth $Port) { break }
                Start-Sleep -Milliseconds 150
            }
            $state = Read-HttpsState
            Save-HttpsState $state
            if (!$state.healthy) { throw $state.message }
        }
    }
    elseif ($Action -eq 'stop') {
        if ($state.processVerified) {
            $child = Get-Process -Id $state.pid -ErrorAction Stop
            $null = $child.Handle
            if ($child.StartTime.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ') -ne $state.processStartTimeUtc) {
                throw 'HTTPS process identity changed. No process was stopped.'
            }
            $child.Kill()
            if (!$child.WaitForExit(5000)) { throw 'HTTPS process did not exit.' }
        }
        if (Test-Path -LiteralPath $statePath) {
            $state.pid=$null; $state.processVerified=$false; $state.healthy=$false; $state.status='stopped'; $state.message='HTTPS web app is stopped. HTTP authoring is unchanged.'
            Save-HttpsState $state
        }
    }
    if ($Json) { $state | ConvertTo-Json -Compress }
    else {
        Write-Output "$($state.status): $($state.message)"
        if ($state.publicUrl) { Write-Output "Web app: $($state.publicUrl)" }
        if ($state.setupUrl) { Write-Output "Phone certificate setup: $($state.setupUrl)" }
    }
}
catch {
    if ($Json) { @{status='error';message=$_.Exception.Message} | ConvertTo-Json -Compress }
    else { Write-Output $_.Exception.Message }
    $exitCode=1
}
finally { if ($mutex) { $mutex.ReleaseMutex(); $mutex.Dispose() } }
exit $exitCode
