$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'sdk.json') -Raw | ConvertFrom-Json
$toolsRoot = Join-Path $serverRoot '.tools'
$sdkRoot = Join-Path $toolsRoot ('dotnet-' + $sdk.version)
$dotnet = Join-Path $sdkRoot 'dotnet.exe'
$architecture = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
if ($env:OS -ne 'Windows_NT' -or $architecture -ne 'AMD64') { throw 'This installer supports Windows x64. On other platforms install the SDK version in global.json.' }
if (Test-Path -LiteralPath $dotnet) {
    $installed = @(& $dotnet --list-sdks)
    if ($LASTEXITCODE -eq 0 -and ($installed | Where-Object { $_ -like ($sdk.version + ' *') })) {
        Write-Output "KimchilyServer SDK $($sdk.version) is ready: $dotnet"
        exit 0
    }
    throw "Incomplete SDK installation at $sdkRoot. Preserve or rename that directory before retrying."
}
New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
$archive = Join-Path $toolsRoot ('dotnet-sdk-' + $sdk.version + '-win-x64.zip')
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $sdk.sha512) {
    Write-Output "Downloading .NET SDK $($sdk.version) from Microsoft..."
    & curl.exe --fail --location --retry 2 --silent --show-error --output $archive $sdk.windowsX64Url
    if ($LASTEXITCODE -ne 0) { throw 'SDK download failed. Run this installer again to retry.' }
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $sdk.sha512) { throw 'SDK archive SHA-512 verification failed; nothing was executed.' }
Write-Output 'Verified SDK archive. Extracting the workspace-local SDK...'
New-Item -ItemType Directory -Path $sdkRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $sdkRoot)
& $dotnet --list-sdks
if ($LASTEXITCODE -ne 0) { throw 'Installed SDK did not start.' }
Write-Output "KimchilyServer SDK is ready: $dotnet"
