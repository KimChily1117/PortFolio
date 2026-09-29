param(
    [ValidateSet('install','build','test','start')][string]$Task = 'start',
    [string]$BindAddress = '127.0.0.1',
    [ValidateRange(1,65535)][int]$Port = 8790,
    [string[]]$AllowedOrigin = @('http://127.0.0.1:8788', 'http://localhost:8788')
)
$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'sdk.json') -Raw | ConvertFrom-Json
if ($Task -eq 'install') { & (Join-Path $PSScriptRoot 'install-sdk.ps1'); exit $LASTEXITCODE }
$dotnet = Join-Path $serverRoot ('.tools\dotnet-' + $sdk.version + '\dotnet.exe')
if (!(Test-Path -LiteralPath $dotnet)) { throw 'Run tools\run.ps1 -Task install first.' }
$address = $null
if (![Net.IPAddress]::TryParse($BindAddress, [ref]$address) -or $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'BindAddress must be an IPv4 address, such as 127.0.0.1 or 0.0.0.0.' }
$previousRoot = $env:DOTNET_ROOT
$previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$exitCode = 0
Push-Location $serverRoot
try {
    $env:DOTNET_ROOT = Split-Path $dotnet
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & $dotnet build 'KimchilyServer.slnx' --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Task -eq 'test') {
        & $dotnet 'tests\Kimchily.Server.Checks\bin\Release\net10.0\Kimchily.Server.Checks.dll'
        $exitCode = $LASTEXITCODE
    } elseif ($Task -eq 'start') {
        Write-Output "Kimchily room demo: http://127.0.0.1:$Port/"
        if ($BindAddress -ne '127.0.0.1') { Write-Output "LAN mode: open http://<this-PC-LAN-address>:$Port/ on each phone. Stop with Ctrl+C." }
        $hostArguments = @('--urls', "http://${BindAddress}:$Port")
        $hostArguments += @('--Realtime:ScriptsRoot', (Join-Path $serverRoot 'games'))
        for ($index = 0; $index -lt $AllowedOrigin.Length; $index++) {
            $originUri = $null
            if (![Uri]::TryCreate($AllowedOrigin[$index], [UriKind]::Absolute, [ref]$originUri) -or $originUri.Scheme -notin @('http','https') -or $originUri.PathAndQuery -ne '/' -or $originUri.Fragment -or $originUri.UserInfo) { throw 'AllowedOrigin must be an HTTP(S) origin without a path, query, fragment or credentials.' }
            $hostArguments += "--Realtime:AllowedOrigins:$index"
            $hostArguments += $originUri.GetLeftPart([UriPartial]::Authority)
        }
        & $dotnet 'src\Kimchily.Server.Host\bin\Release\net10.0\Kimchily.Server.Host.dll' @hostArguments
        $exitCode = $LASTEXITCODE
    }
} finally {
    Pop-Location
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
}
exit $exitCode
