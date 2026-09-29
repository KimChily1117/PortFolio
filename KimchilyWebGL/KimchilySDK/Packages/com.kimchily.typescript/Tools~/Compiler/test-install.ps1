param([switch]$ColdStart)
# Run install.ps1 once first. Tests never modify the live compiler. -ColdStart
# downloads into the fixture to exercise checksum/extraction without PS modules.
$ErrorActionPreference = 'Stop'
$compilerRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$installer = Join-Path $compilerRoot 'install.ps1'
$sourceTools = Join-Path $compilerRoot '.tools'
$ready = Get-Content -LiteralPath (Join-Path $sourceTools 'ready.json') -Raw | ConvertFrom-Json
$runtimeName = $ready.nodeRelativePath.Split('/')[0]
if ($ready.schemaVersion -ne 1 -or $runtimeName -notmatch '^node-v24\.21\.0-win-(x64|arm64)$') {
    throw 'Run install.ps1 successfully before running these checks.'
}
# Keep cold extraction below Windows PowerShell 5.1's legacy MAX_PATH limit;
# nesting another complete compiler under this already-deep package exceeds it.
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('KimchilyTypeScriptTest-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $testRoot "compiler's space"
$fixtureTools = Join-Path $fixture '.tools'
$fixtureReady = Join-Path $fixtureTools 'ready.json'
$checks = [Collections.Generic.List[string]]::new()
$shell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Assert-Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAILED: $Name" }
    $checks.Add($Name)
}

function Start-FixtureInstaller {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $shell
    $info.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $installer + '" -CompilerDirectory "' + $fixture + '"'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # Reproduce Unity launched from a host that cannot supply Windows PS modules.
    $info.EnvironmentVariables['PSModulePath'] = Join-Path $fixtureTools 'empty-modules'
    $process = [Diagnostics.Process]::Start($info)
    [pscustomobject]@{ process = $process; stdout = $process.StandardOutput.ReadToEndAsync(); stderr = $process.StandardError.ReadToEndAsync() }
}

function Complete-FixtureInstaller($Run, [int]$ExpectedExit = 0) {
    try {
        if (!$Run.process.WaitForExit(300000)) {
            $Run.process.Kill()
            throw 'The fixture installer exceeded its test deadline.'
        }
        if (![Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($Run.stdout, $Run.stderr), 3000)) { throw 'Fixture output did not close.' }
        if ($Run.process.ExitCode -ne $ExpectedExit) { throw "Installer exited $($Run.process.ExitCode): $($Run.stderr.Result)" }
        $Run.stdout.Result
    }
    finally { $Run.process.Dispose() }
}

New-Item -ItemType Directory -Force -Path $fixtureTools | Out-Null
Copy-Item -LiteralPath (Join-Path $compilerRoot 'package.json'), (Join-Path $compilerRoot 'package-lock.json') -Destination $fixture
if (!$ColdStart) {
    Copy-Item -LiteralPath (Join-Path $sourceTools $runtimeName) -Destination $fixtureTools -Recurse
    Copy-Item -LiteralPath (Join-Path $compilerRoot 'node_modules') -Destination $fixture -Recurse
}

$output = Complete-FixtureInstaller (Start-FixtureInstaller)
$installed = Get-Content -LiteralPath $fixtureReady -Raw | ConvertFrom-Json
Assert-Check ($installed.schemaVersion -eq 1 -and $installed.nodeVersion -eq 'v24.21.0' -and
    $installed.typescriptVersion -eq '5.9.3' -and $installed.nodeRelativePath -eq "$runtimeName/node.exe") 'Ready metadata identifies the verified portable runtime and pinned compiler'
if ($ColdStart) {
    Assert-Check ($output -match 'Downloading' -and $output -match 'Installing') 'Cold setup verifies and extracts the runtime with a restricted PSModulePath'
}
else {
    Assert-Check ($output -notmatch 'Downloading|Installing') 'Existing valid tools are reused in a path with spaces and an apostrophe'
}
$timestamp = (Get-Item -LiteralPath $fixtureReady).LastWriteTimeUtc
$output = Complete-FixtureInstaller (Start-FixtureInstaller)
Assert-Check ((Get-Item -LiteralPath $fixtureReady).LastWriteTimeUtc -eq $timestamp -and $output -notmatch 'Downloading|Installing') 'Repeated setup is idempotent and keeps the existing ready marker'

$sharedLock = [IO.File]::Open((Join-Path $fixtureTools 'install.lock'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
$waiting = $null
try {
    $waiting = Start-FixtureInstaller
    Start-Sleep -Milliseconds 800
    $waiting.process.Refresh()
    Assert-Check (!$waiting.process.HasExited) 'Setup waits while a compiler holds the shared toolchain lock'
}
finally { $sharedLock.Dispose() }
$null = Complete-FixtureInstaller $waiting
Assert-Check (Test-Path -LiteralPath $fixtureReady) 'Setup finishes after the active compiler releases its lock'

# Failed repair keeps the current files and withdraws stale readiness; a retry repairs them.
$typescriptModule = Join-Path $fixture 'node_modules\typescript\lib\typescript.js'
$brokenModule = "module.exports = { version: 'broken' };"
[IO.File]::WriteAllText($typescriptModule, $brokenModule)
[IO.File]::WriteAllText((Join-Path $fixture 'package-lock.json'), '{invalid')
$null = Complete-FixtureInstaller (Start-FixtureInstaller) 1
Assert-Check (!(Test-Path -LiteralPath $fixtureReady) -and [IO.File]::ReadAllText($typescriptModule) -eq $brokenModule) 'A failed repair preserves existing dependencies and removes stale readiness'
Copy-Item -LiteralPath (Join-Path $compilerRoot 'package-lock.json') -Destination $fixture -Force
$package = Get-Content -LiteralPath (Join-Path $fixture 'package.json') -Raw | ConvertFrom-Json
$package | Add-Member -NotePropertyName scripts -NotePropertyValue @{} -Force
$package.scripts.preinstall = 'node -e "require(''fs'').writeFileSync(process.env.INIT_CWD+''/lifecycle-ran.txt'',''unexpected'')"'
[IO.File]::WriteAllText((Join-Path $fixture 'package.json'), ($package | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$output = Complete-FixtureInstaller (Start-FixtureInstaller)
Assert-Check ((Test-Path -LiteralPath $fixtureReady) -and $output -match 'Installing' -and $output -notmatch 'Downloading') 'Retry repairs dependencies using the existing verified Node runtime'
Assert-Check (!(Test-Path -LiteralPath (Join-Path $fixture 'lifecycle-ran.txt'))) 'npm lifecycle scripts are disabled during automatic setup'

$result = [ordered]@{ passed = $checks.Count; failed = 0; checks = $checks.ToArray() }
$resultsPath = Join-Path $testRoot 'results.json'
[IO.File]::WriteAllText($resultsPath, ($result | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Output "Toolchain installer checks: $($checks.Count) passed. Evidence: $resultsPath"
