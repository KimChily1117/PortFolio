param([switch]$Run)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
if ($cmakeCommand) { $cmakeExe = $cmakeCommand.Source }
else {
    $vswhereExe = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vsPath = & $vswhereExe -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $cmakeExe = Join-Path $vsPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
}
& $cmakeExe -S $projectRoot -B "$projectRoot\build-win" -G 'Visual Studio 17 2022' -A Win32
if ($LASTEXITCODE) { throw 'CMake configure failed' }
& $cmakeExe --build "$projectRoot\build-win" --config Release --parallel 8
if ($LASTEXITCODE) { throw 'Build failed' }
$ctestExe = Join-Path (Split-Path $cmakeExe) 'ctest.exe'
& $ctestExe --test-dir "$projectRoot\build-win" -C Release --output-on-failure
if ($LASTEXITCODE) { throw 'Tests failed' }
$outputDir = "$projectRoot\build-win\bin\AsterIdle\Release"
Write-Host "Ready: $outputDir\AsterIdle.exe"
if ($Run) { Start-Process -FilePath "$outputDir\AsterIdle.exe" -WorkingDirectory $outputDir -WindowStyle Hidden }
