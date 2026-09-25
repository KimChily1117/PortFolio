param(
    [ValidateSet('Status', 'Install', 'Launch', 'OpenWorld')]
    [string]$Action = 'Status',
    [string]$Serial = '',
    [string]$UnityEditorRoot = 'C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor',
    [string]$AndroidSdkRoot = '',
    [string]$ApkPath = '',
    [switch]$NativePreview,
    [string]$PublishResponse = '',
    [string]$WorldLink = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($AndroidSdkRoot)) {
    $AndroidSdkRoot = Join-Path $UnityEditorRoot 'Data\PlaybackEngines\AndroidPlayer\SDK'
}
# Use the exact SDK that Unity/build.ps1 uses. Do not fall back to PATH or the user SDK.
$adbPath = Join-Path $AndroidSdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adbPath -PathType Leaf)) { throw "Unity SDK ADB not found: $adbPath" }
Write-Output "ADB: $adbPath"

function Invoke-KimchilyAdb([string[]]$AdbArguments) {
    & $adbPath @AdbArguments
    if ($LASTEXITCODE -ne 0) { throw "ADB failed with exit $LASTEXITCODE. Use the same Unity SDK ADB for this session." }
}

if ($Action -eq 'Status') {
    Invoke-KimchilyAdb -AdbArguments @('devices', '-l')
    return
}

if ([string]::IsNullOrWhiteSpace($Serial)) {
    $deviceLines = @(Invoke-KimchilyAdb -AdbArguments @('devices', '-l'))
    $connected = @($deviceLines | Where-Object { $_ -match '^\S+\s+device(?:\s|$)' } |
        ForEach-Object { ($_ -split '\s+')[0] })
    if ($connected.Count -ne 1) { throw 'Connect and authorize one device, or specify -Serial explicitly.' }
    $Serial = $connected[0]
}
if ($Serial -notmatch '^[A-Za-z0-9_.:-]+$') { throw 'Invalid ADB device serial.' }

switch ($Action) {
    'Install' {
        if ([string]::IsNullOrWhiteSpace($ApkPath)) {
            $name = if ($NativePreview) { 'kimchily-native-debug.apk' } else { 'kimchily-unity-debug.apk' }
            $ApkPath = Join-Path $projectRoot "Artifacts\$name"
        }
        $ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
        # Old integrated APKs predate QR support. Inspect the actual package before replacing the app.
        $aaptPath = Join-Path $AndroidSdkRoot 'build-tools\32.0.0\aapt.exe'
        if (-not (Test-Path -LiteralPath $aaptPath -PathType Leaf)) { throw "AAPT not found: $aaptPath" }
        $manifest = (& $aaptPath dump xmltree $ApkPath AndroidManifest.xml | Out-String)
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the APK manifest.' }
        if ($manifest -notmatch 'com\.kimchily\.app' -or $manifest -notmatch 'android\.intent\.action\.VIEW' -or
            $manifest -notmatch 'com\.journeyapps\.barcodescanner\.CaptureActivity') {
            throw 'This APK predates QR/link support or is not Kimchily. Build the current Android sources first.'
        }
        if (-not $NativePreview -and $manifest -notmatch 'com\.kimchily\.app\.KimchilyUnityActivity') {
            throw 'Unity is missing from this APK. Build with -WithUnity, or explicitly select -NativePreview.'
        }
        Write-Output "APK: $ApkPath"
        Invoke-KimchilyAdb -AdbArguments @('-s', $Serial, 'install', '-r', $ApkPath)
    }
    'Launch' {
        Invoke-KimchilyAdb -AdbArguments @('-s', $Serial, 'shell', 'am', 'start', '-W', '-n', 'com.kimchily.app/.MainActivity')
    }
    'OpenWorld' {
        if (-not [string]::IsNullOrWhiteSpace($PublishResponse)) {
            if (-not [string]::IsNullOrWhiteSpace($WorldLink)) { throw 'Choose -PublishResponse or -WorldLink, not both.' }
            $WorldLink = (Get-Content -LiteralPath $PublishResponse -Raw -Encoding UTF8 | ConvertFrom-Json).launchUrl
        }
        if ([string]::IsNullOrWhiteSpace($WorldLink) -or $WorldLink.Length -gt 4096 -or
            $WorldLink -notmatch '^kimchily://world\?' -or $WorldLink -match '[\x00-\x20\x7f]') {
            throw 'Provide a Kimchily launchUrl using -PublishResponse or -WorldLink.'
        }
        # ADB joins shell arguments. Keep literal POSIX quotes around the URI so &sha256 is not a shell operator.
        $singleQuote = [string][char]39
        $quoteEscape = $singleQuote + [char]34 + $singleQuote + [char]34 + $singleQuote
        $quotedLink = $singleQuote + $WorldLink.Replace($singleQuote, $quoteEscape) + $singleQuote
        Invoke-KimchilyAdb -AdbArguments @('-s', $Serial, 'shell', 'am', 'start', '-W',
            '-a', 'android.intent.action.VIEW', '-c', 'android.intent.category.BROWSABLE',
            '-d', $quotedLink, '-p', 'com.kimchily.app')
    }
}
