param(
    [string]$Sdk = $env:ANDROID_HOME,
    [string]$Java,
    [string]$BuildRoot,
    [switch]$PrepareOnly,
    [switch]$SkipBuild,
    [switch]$Install,
    [string]$Device
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($PrepareOnly -and ($Install -or $SkipBuild)) { throw '-PrepareOnly cannot install or skip a build.' }
if (!$Sdk) { $Sdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
$Sdk = [IO.Path]::GetFullPath($Sdk)
function Test-Jdk11([string]$Path) {
    if (!$Path -or !(Test-Path -LiteralPath "$Path\bin\java.exe") -or !(Test-Path -LiteralPath "$Path\release")) { return $false }
    return [bool]((Get-Content -LiteralPath "$Path\release") -match '^JAVA_VERSION="11\.')
}
if ($Java) {
    if (!(Test-Jdk11 $Java)) { throw 'This Gradle 6.7.1 project requires -Java pointing to JDK 11.' }
} else {
    $candidates = @($env:JAVA_HOME)
    $candidates += @(Get-ChildItem -Path 'C:\Program Files\Microsoft\jdk-11*','C:\Program Files\Java\jdk-11*' -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    $candidates += @(Get-ChildItem -LiteralPath 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK' })
    $Java = $candidates | Where-Object { Test-Jdk11 $_ } | Select-Object -First 1
    if (!$Java) { throw 'Install JDK 11 or pass its directory with -Java.' }
}
foreach ($required in @('platform-tools\adb.exe','platforms\android-33\android.jar','build-tools\30.0.3\aapt.exe','ndk\21.0.6113669\source.properties','cmake\3.18.1\bin\cmake.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $Sdk $required))) { throw "Missing Android SDK component: $required" }
}
if ($BuildRoot) {
    $BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
    if ($BuildRoot.StartsWith((Join-Path $projectRoot 'Resources'), [StringComparison]::OrdinalIgnoreCase)) { throw 'BuildRoot must be outside Resources.' }
    New-Item -ItemType Directory -Path $BuildRoot -Force | Out-Null
    $apkPath = Join-Path $BuildRoot 'app\outputs\apk\debug\app-debug.apk'
} else { $apkPath = Join-Path $projectRoot 'proj.android\app\build\outputs\apk\debug\app-debug.apk' }
$assetStats = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Resources') -File -Recurse | Measure-Object -Property Length -Sum
$buildDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($apkPath))
Write-Host "Java: $Java"
Write-Host "SDK: $Sdk"
Write-Host ('Assets: {0} files, {1:N2} GiB; build drive free: {2:N2} GiB' -f $assetStats.Count,($assetStats.Sum / 1GB),($buildDrive.AvailableFreeSpace / 1GB))
Write-Host "APK: $apkPath"
$adbExe = Join-Path $Sdk 'platform-tools\adb.exe'
if ($PrepareOnly) {
    & $adbExe devices -l
    if ($LASTEXITCODE) { throw 'ADB inventory failed' }
    return
}
if (!$SkipBuild) {
    if ($buildDrive.AvailableFreeSpace -lt ($assetStats.Sum * 3 + 2GB)) { throw 'Insufficient build space. Pass -BuildRoot on a drive with at least three asset copies plus 2 GiB free.' }
    $gradleExe = "$projectRoot\.deps\gradle-6.7.1\bin\gradle.bat"
    if (!(Test-Path -LiteralPath $gradleExe)) {
        $url = 'https://services.gradle.org/distributions/gradle-6.7.1-bin.zip'
        $archive = "$projectRoot\.deps\gradle-6.7.1-bin.zip"
        Invoke-WebRequest -Uri $url -OutFile $archive
        $expected = (Invoke-WebRequest -Uri "$url.sha256").Content.Trim()
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Gradle checksum mismatch' }
        Expand-Archive -LiteralPath $archive -DestinationPath "$projectRoot\.deps" -Force
    }
    Set-Content -LiteralPath "$projectRoot\proj.android\local.properties" -Value ('sdk.dir=' + $Sdk.Replace('\','/')) -Encoding ascii
    $oldJava = $env:JAVA_HOME
    Push-Location "$projectRoot\proj.android"
    try {
        $env:JAVA_HOME = $Java
        $gradleArgs = @(':app:assembleDebug','--no-daemon','--console=plain','--max-workers=4')
        if ($BuildRoot) { $gradleArgs += ('-PasterBuildRoot=' + $BuildRoot.Replace('\','/')) }
        & $gradleExe @gradleArgs
        if ($LASTEXITCODE) { throw 'Android build failed' }
    } finally { $env:JAVA_HOME = $oldJava; Pop-Location }
}
if (!(Test-Path -LiteralPath $apkPath)) { throw "APK not found: $apkPath" }
$apk = Get-Item -LiteralPath $apkPath
$badging = & (Join-Path $Sdk 'build-tools\30.0.3\aapt.exe') dump badging $apkPath
if ($LASTEXITCODE -or !($badging -match "package: name='com.portfolio.asteridle'") -or !($badging -match "native-code:.*'arm64-v8a'")) { throw 'APK package/arm64 validation failed.' }
Write-Host ('Verified APK: {0:N2} GiB' -f ($apk.Length / 1GB))
if ($Install) {
    if (!$Device) {
        $deviceRows = @((& $adbExe devices) | Where-Object { $_ -match '^\S+\s+device$' })
        if ($deviceRows.Count -ne 1) { throw 'Pass -Device with exactly one authorized device serial.' }
        $Device = ($deviceRows[0] -split '\s+')[0]
    }
    $api = (& $adbExe -s $Device shell getprop ro.build.version.sdk).Trim()
    $abi = (& $adbExe -s $Device shell getprop ro.product.cpu.abilist).Trim()
    if ($LASTEXITCODE -or $api -notmatch '^\d+$' -or [int]$api -lt 24 -or $abi -notmatch 'arm64-v8a') { throw 'Device must be authorized, API 24+, and arm64-v8a.' }
    $disk = & $adbExe -s $Device shell df -k /data
    $diskLine = $disk | Where-Object { $_ -match '^/.*\s+\d+\s+\d+\s+\d+\s+' } | Select-Object -Last 1
    if (!$diskLine) { throw 'Could not verify device storage.' }
    $available = [long](($diskLine.Trim() -split '\s+')[3]) * 1KB
    if ($available -lt ($apk.Length * 3 + 512MB)) { throw 'Device needs room for APK transfer, installation and extraction. Free space without uninstalling this app, then retry.' }
    & $adbExe -s $Device install -r --no-streaming $apkPath
    if ($LASTEXITCODE) { throw 'APK update failed. Existing app/data were not intentionally removed.' }
    $launch = & $adbExe -s $Device shell am start -W -n 'com.portfolio.asteridle/.AppActivity'
    $launch | Write-Host
    if ($LASTEXITCODE -or !($launch -match '^Status: ok')) { throw 'App launch failed' }
}
$receipt = [ordered]@{ apk=$apkPath; bytes=$apk.Length; sha256=(Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash.ToLowerInvariant(); java=$Java; sdk=$Sdk; buildRoot=$BuildRoot; installed=[bool]$Install; device=$Device; timestamp=(Get-Date).ToString('o') }
$receipt | ConvertTo-Json | Set-Content -LiteralPath "$projectRoot\.deps\android-last-build.json" -Encoding utf8
