param(
    [string]$BindAddress = '0.0.0.0',
    [int]$Port = 8788,
    [string]$PublicBaseUrl = '',
    [string]$LanAddress = '',
    [string]$StateDirectory = '',
    [switch]$Json
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'server-common.ps1')
$context = Get-PublisherContext $StateDirectory
$mutex = $null
$exitCode = 0
try {
    if ($BindAddress -notmatch '^[A-Za-z0-9.:-]+$' -or $Port -lt 1 -or $Port -gt 65535) { throw 'Invalid bind address or port.' }
    if ($PublicBaseUrl -and $LanAddress) { throw 'Use either PublicBaseUrl or LanAddress, not both.' }
    if (!$PublicBaseUrl) {
        if ($BindAddress -in @('0.0.0.0', '::') -or $LanAddress) {
            $address = Select-PublisherLanAddress @(Get-PublisherLanCandidates) $LanAddress
            $PublicBaseUrl = "http://${address}:$Port"
        }
        else { $PublicBaseUrl = Get-PublisherLocalUrl $BindAddress $Port }
    }
    $PublicBaseUrl = ConvertTo-PublisherOrigin $PublicBaseUrl
    $mutex = Enter-PublisherLock $context
    $state = Get-PublisherState $context $BindAddress $Port $PublicBaseUrl -SkipUnregisteredPortCheck
    if ($state.status -in @('conflict', 'error')) { throw $state.message }
    if ($state.processVerified) {
        if ($state.bindAddress -ne $BindAddress -or $state.port -ne $Port -or $state.publicUrl -ne $PublicBaseUrl) {
            throw 'A registered publisher is running with different BindAddress, Port or PublicBaseUrl. Requested settings were not applied. Stop it explicitly before changing them.'
        }
        $process = Get-Process -Id $state.pid -ErrorAction Stop
        Wait-PublisherReady $process $state.localUrl $Port
        $state = Get-PublisherState $context
        $state.message = 'Publisher already ready; reusing the registered process without starting another.'
        Save-PublisherState $context $state
    }
    else {
        $owners = @(Get-PublisherPortOwners $Port)
        if ($owners.Count) { throw "Port $Port is occupied: $($owners -join ', '). No process was stopped. Choose another port or explicitly stop its owner." }
        if (Test-PublisherHealth (Get-PublisherLocalUrl $BindAddress $Port)) { throw 'An unregistered publisher responds on the requested port. No duplicate was started.' }
        New-Item -ItemType Directory -Force -Path $context.stateDirectory | Out-Null
        $pythonPath = (Get-Command python -ErrorAction Stop).Source
        $arguments = @('-u', ('"' + $context.launcherPath + '"'), ('"' + $context.serverPath + '"'), '--host', $BindAddress, '--port', "$Port", '--public-base-url', $PublicBaseUrl,
            '--data-dir', ('"' + $context.stateDirectory + '"'))
        # WMI creates an independent hidden process. Start-Process can pass the caller's
        # redirected pipe handles to Python, preventing Unity/CLI ReadToEnd from completing.
        $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]0 }
        $created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
            CommandLine = ('"' + $pythonPath + '" ' + ($arguments -join ' '))
            CurrentDirectory = $context.projectRoot; ProcessStartupInformation = $startup
        } -ErrorAction Stop
        if ($created.ReturnValue -ne 0) { throw "Windows could not start the publisher (WMI code $($created.ReturnValue))." }
        $process = Get-Process -Id $created.ProcessId -ErrorAction Stop
        # Register ownership before waiting, so a slow/unhealthy child can be inspected and stopped safely.
        $process.Id | Set-Content -LiteralPath $context.pidPath
        $identity = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)" -ErrorAction Stop
        $state = New-PublisherState $context $BindAddress $Port $PublicBaseUrl
        $state.pid = $process.Id; $state.status = 'unhealthy'; $state.message = 'Publisher is starting; health is not verified yet.'
        if ($identity) { $state.processStartTimeUtc = $identity.CreationDate.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ'); $state.processVerified = $true }
        Save-PublisherState $context $state
        Wait-PublisherReady $process $state.localUrl $Port
        $state = Get-PublisherState $context
        if ($state.status -ne 'running') { throw $state.message }
        Save-PublisherState $context $state
    }
    Write-PublisherResult $state -Json:$Json
}
catch { Write-PublisherFailure $context $_.Exception.Message -Json:$Json; $exitCode = 1 }
finally { if ($mutex) { $mutex.ReleaseMutex(); $mutex.Dispose() } }
exit $exitCode
