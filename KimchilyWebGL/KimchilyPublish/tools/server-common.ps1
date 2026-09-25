# Shared by start/status/stop. Process command lines are inspected, never logged.
function Get-PublisherContext([string]$StateDirectory = '') {
    $project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if (!$StateDirectory) { $StateDirectory = Join-Path $project '.local' }
    $state = [IO.Path]::GetFullPath($StateDirectory)
    [pscustomobject]@{
        projectRoot = $project; serverPath = Join-Path $project 'server.py'
        launcherPath = Join-Path $project 'tools\run-server.py'
        stateDirectory = $state; pidPath = Join-Path $state 'server.pid'
        connectionPath = Join-Path $state 'connection.json'
        stdoutLog = Join-Path $state 'server.log'; stderrLog = Join-Path $state 'server-error.log'
    }
}

function ConvertTo-PublisherOrigin([string]$Value) {
    $uri = $null
    if (![Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or
        $Value -match '[\\\s]' -or !$uri.Host -or $uri.Scheme -notin @('http', 'https') -or
        $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
        throw 'PublicBaseUrl must be an HTTP(S) origin without credentials, path, query or fragment.'
    }
    $uri.GetLeftPart([UriPartial]::Authority)
}

function Get-PublisherLocalUrl([string]$BindAddress, [int]$Port) {
    $hostName = if ($BindAddress -eq '0.0.0.0') { '127.0.0.1' } elseif ($BindAddress -eq '::') { '[::1]' }
        elseif ($BindAddress.Contains(':')) { "[$BindAddress]" } else { $BindAddress }
    "http://${hostName}:$Port"
}

function Get-PublisherLanCandidates {
    $routes = @(Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue)
    $interfaces = @(Get-NetIPInterface -AddressFamily IPv4 -ErrorAction Stop)
    $adapters = @(Get-NetAdapter -IncludeHidden -ErrorAction Stop)
    foreach ($address in @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop)) {
        if ($address.AddressState -ne 'Preferred' -or $address.SkipAsSource -or
            $address.IPAddress -match '^(0\.|127\.|169\.254\.|22[4-9]\.|23\d\.|24\d\.|25[0-5]\.)') { continue }
        $adapter = $adapters | Where-Object InterfaceIndex -eq $address.InterfaceIndex | Select-Object -First 1
        $interface = $interfaces | Where-Object InterfaceIndex -eq $address.InterfaceIndex | Select-Object -First 1
        if (!$adapter -or $adapter.Status -ne 'Up' -or !$adapter.HardwareInterface -or $adapter.Virtual -or
            $interface.ConnectionState -ne 'Connected' -or
            "$($adapter.Name) $($adapter.InterfaceDescription)" -match 'VPN|Tunnel|WireGuard|Tailscale|ZeroTier|Loopback|WAN Miniport|\bTAP\b|\bTUN\b') { continue }
        $route = $routes | Where-Object InterfaceIndex -eq $address.InterfaceIndex | Sort-Object RouteMetric | Select-Object -First 1
        [pscustomobject]@{
            address = [string]$address.IPAddress; interfaceName = [string]$adapter.Name
            hasDefaultRoute = [bool]$route
            metric = if ($route) { [int]$route.RouteMetric + [int]$interface.InterfaceMetric } else { [int]::MaxValue }
        }
    }
}

function Select-PublisherLanAddress([object[]]$Candidates, [string]$Requested = '') {
    if ($Requested) {
        if (@($Candidates | Where-Object address -eq $Requested).Count -ne 1) {
            throw 'LanAddress must match an active physical LAN IPv4 address. For another interface, set PublicBaseUrl explicitly.'
        }
        return $Requested
    }
    if (!$Candidates.Count) { throw 'No active LAN IPv4 address was found. Connect Wi-Fi/Ethernet or supply PublicBaseUrl explicitly.' }
    $preferred = @($Candidates | Where-Object hasDefaultRoute | Sort-Object metric)
    if ($preferred.Count) { $choices = @($preferred | Where-Object metric -eq $preferred[0].metric) }
    else { $choices = @($Candidates) }
    if ($choices.Count -ne 1) {
        $description = ($choices | ForEach-Object { "$($_.address) ($($_.interfaceName))" }) -join ', '
        throw "Multiple LAN addresses are eligible: $description. Select one with -LanAddress, or set -PublicBaseUrl explicitly."
    }
    $choices[0].address
}

function Test-PublisherHealth([string]$LocalUrl, [int]$TimeoutMilliseconds = 800) {
    $response = $null; $reader = $null
    try {
        $request = [Net.HttpWebRequest]::Create("$LocalUrl/health")
        $request.Timeout = $TimeoutMilliseconds; $request.ReadWriteTimeout = $TimeoutMilliseconds
        $request.Proxy = $null; $request.AllowAutoRedirect = $false; $request.KeepAlive = $false
        $response = $request.GetResponse()
        if ([int]$response.StatusCode -ne 200) { return $false }
        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        $health = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop
        return ($health.status -eq 'ok' -and $health.service -eq 'Kimchily local publisher' -and $health.schemaVersion -eq 1)
    }
    catch { return $false }
    finally { if ($reader) { $reader.Dispose() }; if ($response) { $response.Dispose() } }
}

function Get-PublisherPortOwners([int]$Port) {
    @(Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
        Where-Object { $_.State -in @('Listen', 'Bound') } |
        Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object {
            $owner = Get-Process -Id $_ -ErrorAction SilentlyContinue
            "PID $_ ($($owner.ProcessName))"
        })
}

function Get-PublisherOption([string]$CommandLine, [string]$Name, [string]$DefaultValue = '') {
    $match = [regex]::Match($CommandLine, '(?:^|\s)--' + [regex]::Escape($Name) + '(?:=|\s+)(?:"([^"]*)"|([^\s"]+))')
    if (!$match.Success) { return $DefaultValue }
    if ($match.Groups[1].Success) { return $match.Groups[1].Value }
    $match.Groups[2].Value
}

function New-PublisherState($Context, [string]$BindAddress = '0.0.0.0', [int]$Port = 8788, [string]$PublicUrl = '') {
    [pscustomobject][ordered]@{
        schemaVersion = 1; status = 'stopped'; localUrl = Get-PublisherLocalUrl $BindAddress $Port
        publicUrl = $PublicUrl; bindAddress = $BindAddress; port = $Port; pid = $null
        processVerified = $false; healthy = $false; message = 'Publisher is stopped.'
        stdoutLog = $Context.stdoutLog; stderrLog = $Context.stderrLog; stateDirectory = $Context.stateDirectory
        serverPath = $Context.serverPath; processStartTimeUtc = $null; updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Get-PublisherState($Context, [string]$BindAddress = '0.0.0.0', [int]$Port = 8788, [string]$PublicUrl = '', [switch]$SkipUnregisteredPortCheck) {
    $state = New-PublisherState $Context $BindAddress $Port $PublicUrl
    $saved = $null
    if (Test-Path -LiteralPath $Context.connectionPath) {
        try {
            $saved = Get-Content -LiteralPath $Context.connectionPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
            if ($saved.schemaVersion -ne 1 -or $saved.bindAddress -notmatch '^[A-Za-z0-9.:-]+$' -or
                [int]$saved.port -lt 1 -or [int]$saved.port -gt 65535 -or
                [IO.Path]::GetFullPath($saved.serverPath) -ne $Context.serverPath -or
                [IO.Path]::GetFullPath($saved.stateDirectory) -ne $Context.stateDirectory) { throw 'Invalid saved settings.' }
            $public = if ($saved.publicUrl) { ConvertTo-PublisherOrigin $saved.publicUrl } else { '' }
            $state = New-PublisherState $Context $saved.bindAddress ([int]$saved.port) $public
        }
        catch { $state.status = 'error'; $state.message = 'connection.json is invalid; no process was modified.'; return $state }
    }
    $registeredId = 0
    if (Test-Path -LiteralPath $Context.pidPath) {
        if (![int]::TryParse((Get-Content -LiteralPath $Context.pidPath -Raw).Trim(), [ref]$registeredId) -or $registeredId -le 0) {
            $state.status = 'error'; $state.message = 'server.pid is invalid; no process was modified.'; return $state
        }
    }
    if (!$registeredId) {
        $owners = if ($SkipUnregisteredPortCheck) { @() } else { @(Get-PublisherPortOwners $state.port) }
        if ($owners.Count) { $state.status = 'conflict'; $state.message = "No verified publisher PID; port $($state.port) is occupied: $($owners -join ', '). No process was modified." }
        return $state
    }
    $registered = Get-CimInstance Win32_Process -Filter "ProcessId=$registeredId" -ErrorAction Stop
    if (!$registered) { $state.message = 'Publisher is stopped (stale PID file).'; return $state }
    $state.pid = $registeredId
    # Accept the legacy direct server command or our exact detached launcher + server pair.
    $scriptPattern = '^\s*(?:"[^"\r\n]+"|[^\s"]+)\s+(?:-u\s+)?(?:"?' +
        [regex]::Escape($Context.launcherPath) + '"?\s+)?"?' + [regex]::Escape($Context.serverPath) + '"?(?=\s|$)'
    if ($registered.Name -notmatch '^python(?:w|[0-9.]+)?\.exe$' -or !$registered.CommandLine -or
        ![regex]::IsMatch($registered.CommandLine.Replace('/', '\'), $scriptPattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $state.status = 'conflict'; $state.message = "Registered PID $registeredId belongs to another process. No process was modified."; return $state
    }
    $hostName = Get-PublisherOption $registered.CommandLine 'host' '127.0.0.1'
    $serverPort = Get-PublisherOption $registered.CommandLine 'port' '8788'
    $serverOrigin = ConvertTo-PublisherOrigin (Get-PublisherOption $registered.CommandLine 'public-base-url')
    $dataDirectory = [IO.Path]::GetFullPath((Get-PublisherOption $registered.CommandLine 'data-dir' (Join-Path $Context.projectRoot '.local')))
    # Win32_Process reports microseconds; Process.StartTime has an extra 100ns digit.
    $startedAt = $registered.CreationDate.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.ffffffZ')
    if ($dataDirectory -ne $Context.stateDirectory -or ($saved -and
        ($saved.pid -ne $registeredId -or $saved.bindAddress -ne $hostName -or [string]$saved.port -ne $serverPort -or
        $saved.publicUrl -ne $serverOrigin -or $saved.processStartTimeUtc -ne $startedAt))) {
        $state.status = 'conflict'; $state.message = "Registered PID $registeredId no longer matches the saved publisher identity/settings. No process was modified."; return $state
    }
    $state.bindAddress = $hostName; $state.port = [int]$serverPort; $state.publicUrl = $serverOrigin
    $state.localUrl = Get-PublisherLocalUrl $hostName $state.port
    $state.processStartTimeUtc = $startedAt; $state.processVerified = $true
    $state.healthy = Test-PublisherHealth $state.localUrl
    if ($state.healthy) { $state.status = 'running'; $state.message = 'Publisher is running and health verified.' }
    else { $state.status = 'unhealthy'; $state.message = 'Publisher process is verified, but health is unavailable. Check the server logs.' }
    $state
}

function Save-PublisherState($Context, $State) {
    New-Item -ItemType Directory -Force -Path $Context.stateDirectory | Out-Null
    $temporary = $Context.connectionPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $State.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($temporary, ($State | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $Context.connectionPath -Force
}

function Wait-PublisherReady($Process, [string]$LocalUrl, [int]$Port) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt 10000) {
        $Process.Refresh()
        if ($Process.HasExited) { throw "Publisher exited before becoming ready. Port $Port may be occupied or blocked/reserved by Windows (WinError 10013)." }
        $remaining = [int](10000 - $timer.ElapsedMilliseconds)
        if ($remaining -le 0) { break }
        if (Test-PublisherHealth $LocalUrl ([Math]::Min(500, $remaining))) {
            $Process.Refresh()
            if (!$Process.HasExited) { return }
        }
        $remaining = [int](10000 - $timer.ElapsedMilliseconds)
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([Math]::Min(100, $remaining)) }
    }
    throw 'Publisher did not become ready within 10 seconds. It remains registered for status/stop; it was not stopped automatically.'
}

function Enter-PublisherLock($Context) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $name = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Context.stateDirectory.ToLowerInvariant()))).Replace('-', '') }
    finally { $hash.Dispose() }
    $mutex = [Threading.Mutex]::new($false, "Local\KimchilyPublisher_$name")
    try { $acquired = $mutex.WaitOne(15000) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (!$acquired) { $mutex.Dispose(); throw 'Another publisher management operation is busy. Retry after it finishes.' }
    $mutex
}

function Write-PublisherResult($State, [switch]$Json) {
    if ($Json) { $State | ConvertTo-Json -Depth 4 -Compress }
    else {
        Write-Output "$($State.status): $($State.message)"
        Write-Output "Local URL: $($State.localUrl)"
        if ($State.publicUrl) { Write-Output "Phone/public URL: $($State.publicUrl)" }
        if ($State.pid) { Write-Output "PID: $($State.pid)" }
        Write-Output "Logs: $($State.stdoutLog) and $($State.stderrLog)"
    }
}

function Write-PublisherFailure($Context, [string]$Message, [switch]$Json) {
    $state = New-PublisherState $Context
    $state.status = 'error'; $state.message = $Message
    Write-PublisherResult $state -Json:$Json
}
