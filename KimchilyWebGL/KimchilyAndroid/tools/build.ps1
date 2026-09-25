param(
    [switch]$WithUnity,
    [switch]$Offline,
    [string]$UnityEditorRoot = 'C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor',
    [string]$AndroidSdkRoot = '',
    [string[]]$Tasks = @('testDebugUnitTest', 'assembleDebug')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$androidPlayer = Join-Path $UnityEditorRoot 'Data\PlaybackEngines\AndroidPlayer'
if ([string]::IsNullOrWhiteSpace($AndroidSdkRoot)) { $AndroidSdkRoot = Join-Path $androidPlayer 'SDK' }
$adbPath = Join-Path $AndroidSdkRoot 'platform-tools\adb.exe'
$javaDirectory = Join-Path $androidPlayer 'OpenJDK'
$gradleLauncher = Join-Path $androidPlayer 'Tools\gradle\lib\gradle-launcher-7.2.jar'
if (-not (Test-Path -LiteralPath $gradleLauncher)) { throw "Unity bundled Gradle not found: $gradleLauncher" }
if (-not (Test-Path -LiteralPath (Join-Path $AndroidSdkRoot 'platforms\android-32\android.jar'))) {
    throw "This development build requires Android SDK platform 32: $AndroidSdkRoot"
}
Write-Output "Android SDK: $AndroidSdkRoot"
Write-Output "Use this ADB for device tests: $adbPath (tools/device.ps1 uses the same default SDK)."
$env:JAVA_HOME = $javaDirectory
$env:ANDROID_SDK_ROOT = $AndroidSdkRoot
$env:ANDROID_HOME = $AndroidSdkRoot
$env:GRADLE_USER_HOME = Join-Path $projectRoot '.gradle-user-home'
$env:ANDROID_USER_HOME = Join-Path $projectRoot '.android-user-home'
# Windows launchers can supply both PATH and Path. Bee/Stevedore rejects that block.
# Remove inherited aliases and restore one canonical entry in this build process only.
# User and machine environment settings are never modified.
$buildPathValue = [Environment]::GetEnvironmentVariable('PATH', 'Process')
[Environment]::SetEnvironmentVariable('PATH', $null, 'Process')
[Environment]::SetEnvironmentVariable('Path', $null, 'Process')
[Environment]::SetEnvironmentVariable('path', $null, 'Process')
[Environment]::SetEnvironmentVariable('PATH', $buildPathValue, 'Process')
$localProperties = 'sdk.dir=' + $AndroidSdkRoot.Replace('\', '/').Replace(':', '\:') + "`n"
[IO.File]::WriteAllText((Join-Path $projectRoot 'local.properties'), $localProperties, (New-Object Text.UTF8Encoding($false)))
$arguments = @('--no-daemon', '--console=plain', '--stacktrace', '-p', $projectRoot,
    ('-PwithUnity=' + $WithUnity.IsPresent.ToString().ToLowerInvariant())) + $Tasks
if ($Offline) { $arguments += '--offline' }
& (Join-Path $javaDirectory 'bin\java.exe') -classpath $gradleLauncher org.gradle.launcher.GradleMain @arguments
if ($LASTEXITCODE -ne 0) { throw "Android Gradle build failed with exit $LASTEXITCODE." }
$apk = Join-Path $projectRoot 'app\build\outputs\apk\debug\app-debug.apk'
if (($Tasks -contains 'assembleDebug') -and (Test-Path -LiteralPath $apk)) {
    $artifactDirectory = Join-Path $projectRoot 'Artifacts'
    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
    $apkName = if ($WithUnity) { 'kimchily-unity-debug.apk' } else { 'kimchily-native-debug.apk' }
    Copy-Item -LiteralPath $apk -Destination (Join-Path $artifactDirectory $apkName) -Force
    Write-Output "APK: $artifactDirectory\$apkName"
}
