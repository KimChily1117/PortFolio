param([string]$CompilerDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# The Unity editor starts this installer once. Neither Node nor npm is installed globally.
$nodeVersion = 'v24.21.0'
$typescriptVersion = '5.9.3'
$compilerRoot = [IO.Path]::GetFullPath($CompilerDirectory)
$toolsRoot = Join-Path $compilerRoot '.tools'
$readyPath = Join-Path $toolsRoot 'ready.json'
$mutex = $null
$locked = $false
$fileLock = $null
$stage = $null
$exitCode = 0

function Quote-ProcessArgument([string]$Value) {
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    '"' + [regex]::Replace($escaped, '(\\+)$', '$1$1') + '"'
}

function Invoke-SetupProcess([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds = 15) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable
    $info.Arguments = ($Arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' '
    $info.WorkingDirectory = $compilerRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw "Could not start $Executable" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
            throw "Toolchain setup command timed out after $TimeoutSeconds seconds. Retry setup in Unity."
        }
        if (![Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 3000)) {
            throw 'Toolchain setup command ended without closing its output streams.'
        }
        [pscustomobject]@{ exitCode = $process.ExitCode; stdout = $stdout.Result; stderr = $stderr.Result }
    }
    finally { $process.Dispose() }
}

function Test-PinnedNode([string]$NodePath) {
    if (!(Test-Path -LiteralPath $NodePath -PathType Leaf)) { return $false }
    if (!(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($NodePath)) 'node_modules\npm\bin\npm-cli.js') -PathType Leaf)) { return $false }
    try {
        $result = Invoke-SetupProcess $NodePath @('--version')
        return ($result.exitCode -eq 0 -and $result.stdout.Trim() -eq $nodeVersion)
    }
    catch { return $false }
}

function Test-PinnedCompiler([string]$NodePath, [string]$Directory) {
    $module = Join-Path $Directory 'node_modules\typescript\lib\typescript.js'
    if (!(Test-Path -LiteralPath $module -PathType Leaf)) { return $false }
    try {
        $check = "const ts=require(process.argv[1]);if(ts.version!=='$typescriptVersion'||typeof ts.createProgram!=='function')process.exit(2);"
        $result = Invoke-SetupProcess $NodePath @('-e', $check, $module)
        return ($result.exitCode -eq 0)
    }
    catch { return $false }
}

function Remove-SetupDirectory([string]$Directory) {
    # Every recursive cleanup is confined to a generated child of this compiler's .tools.
    $absolute = [IO.Path]::GetFullPath($Directory)
    $prefix = [IO.Path]::GetFullPath($toolsRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup outside the toolchain directory: $absolute"
    }
    if (Test-Path -LiteralPath $absolute) { Remove-Item -LiteralPath $absolute -Recurse -Force }
}

function Move-SetupDirectory([string]$Source, [string]$Destination) {
    $sourcePath = [IO.Path]::GetFullPath($Source)
    $destinationPath = [IO.Path]::GetFullPath($Destination)
    $prefix = $compilerRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$sourcePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        !$destinationPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to move toolchain directories outside the selected compiler directory.'
    }
    Move-Item -LiteralPath $sourcePath -Destination $destinationPath
}

function Save-Ready([string]$NodeDirectoryName) {
    $ready = [ordered]@{
        schemaVersion = 1
        nodeRelativePath = "$NodeDirectoryName/node.exe"
        nodeVersion = $nodeVersion
        typescriptVersion = $typescriptVersion
    } | ConvertTo-Json
    if ((Test-Path -LiteralPath $readyPath) -and
        [IO.File]::ReadAllText($readyPath) -eq $ready) { return }
    $temporary = Join-Path $toolsRoot ('ready-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, $ready, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $readyPath -Force
}

try {
    if ($env:OS -ne 'Windows_NT') { throw 'Automatic TypeScript toolchain setup currently supports Windows x64 and ARM64.' }
    if (!(Test-Path -LiteralPath $compilerRoot -PathType Container)) { throw "Compiler directory does not exist: $compilerRoot" }
    $architecture = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
    # Official archive digests: https://nodejs.org/dist/v24.21.0/SHASUMS256.txt
    switch ($architecture.ToUpperInvariant()) {
        'AMD64' { $platform = 'x64'; $archiveHash = '158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541' }
        'ARM64' { $platform = 'arm64'; $archiveHash = '8779b1bde1d39f8d420e3b57aa657b39891af434d3de44a919044cec06785921' }
        default { throw "Unsupported Windows architecture: $architecture" }
    }
    $nodeDirectoryName = "node-$nodeVersion-win-$platform"
    $nodeDirectory = Join-Path $toolsRoot $nodeDirectoryName
    $nodePath = Join-Path $nodeDirectory 'node.exe'
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $lockName = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($compilerRoot.ToLowerInvariant()))).Replace('-', '') }
    finally { $hasher.Dispose() }
    $mutex = [Threading.Mutex]::new($false, "Local\KimchilyTypeScript_$lockName")
    try { $locked = $mutex.WaitOne(180000) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another Unity editor is installing this TypeScript toolchain. Retry setup after it finishes.' }
    $lockTimer = [Diagnostics.Stopwatch]::StartNew()
    while (!$fileLock) {
        try { $fileLock = [IO.File]::Open((Join-Path $toolsRoot 'install.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch [IO.IOException] {
            if ($lockTimer.Elapsed.TotalSeconds -ge 180) { throw 'The TypeScript toolchain is busy. Retry setup after the current operation finishes.' }
            Start-Sleep -Milliseconds 250
        }
    }
    if ((Test-PinnedNode $nodePath) -and (Test-PinnedCompiler $nodePath $compilerRoot)) {
        Save-Ready $nodeDirectoryName
        Write-Output "Kimchily TypeScript is ready (Node $nodeVersion, TypeScript $typescriptVersion)."
    }
    else {
        # A previously written marker must not advertise a broken/partial installation.
        if (Test-Path -LiteralPath $readyPath) { Remove-Item -LiteralPath $readyPath -Force }
        $packagePath = Join-Path $compilerRoot 'package.json'
        $lockPath = Join-Path $compilerRoot 'package-lock.json'
        $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
        if ($package.dependencies.typescript -ne $typescriptVersion -or !(Test-Path -LiteralPath $lockPath -PathType Leaf)) {
            throw "The compiler package and lockfile must pin TypeScript $typescriptVersion. Restore the package files before retrying."
        }
        $stage = Join-Path $toolsRoot ('.install-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $stage | Out-Null
        if (!(Test-PinnedNode $nodePath)) {
            $archive = Join-Path $stage "$nodeDirectoryName.zip"
            $url = "https://nodejs.org/dist/$nodeVersion/$nodeDirectoryName.zip"
            Write-Output "Downloading the pinned Node runtime ($nodeVersion, Windows $platform)..."
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing -TimeoutSec 120
            # Unity can inherit a restricted PSModulePath from its launcher. Use
            # .NET directly instead of module-provided Get-FileHash/Expand-Archive.
            $archiveStream = [IO.File]::OpenRead($archive)
            $archiveHasher = [Security.Cryptography.SHA256]::Create()
            try { $actualHash = [BitConverter]::ToString($archiveHasher.ComputeHash($archiveStream)).Replace('-', '').ToLowerInvariant() }
            finally { $archiveHasher.Dispose(); $archiveStream.Dispose() }
            if ($actualHash -ne $archiveHash) {
                throw 'The downloaded Node runtime failed its SHA-256 check. No downloaded program was run. Retry setup.'
            }
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [IO.Compression.ZipFile]::ExtractToDirectory($archive, $stage)
            $extracted = Join-Path $stage $nodeDirectoryName
            if (!(Test-PinnedNode (Join-Path $extracted 'node.exe'))) { throw 'The verified Node archive could not run the required Node version.' }
            $previousNode = Join-Path $stage 'previous-node'
            if (Test-Path -LiteralPath $nodeDirectory) { Move-SetupDirectory $nodeDirectory $previousNode }
            try { Move-SetupDirectory $extracted $nodeDirectory }
            catch {
                if (Test-Path -LiteralPath $previousNode) { Move-SetupDirectory $previousNode $nodeDirectory }
                throw
            }
        }
        # npm v3 lockfiles contain an empty key that Windows PowerShell 5.1 cannot
        # represent with ConvertFrom-Json. Parse them with our verified Node runtime.
        $lockCheck = "const fs=require('node:fs');const p=JSON.parse(fs.readFileSync(process.argv[1],'utf8'));if(!p.packages||!p.packages['node_modules/typescript']||p.packages['node_modules/typescript'].version!=='$typescriptVersion')process.exit(2);"
        $lockResult = Invoke-SetupProcess $nodePath @('-e', $lockCheck, $lockPath)
        if ($lockResult.exitCode -ne 0) { throw "The compiler lockfile must pin TypeScript $typescriptVersion. Restore package-lock.json before retrying." }
        $stagedCompiler = Join-Path $stage 'compiler'
        New-Item -ItemType Directory -Path $stagedCompiler | Out-Null
        Copy-Item -LiteralPath $packagePath, $lockPath -Destination $stagedCompiler
        $npm = Join-Path $nodeDirectory 'node_modules\npm\bin\npm-cli.js'
        if (!(Test-Path -LiteralPath $npm -PathType Leaf)) { throw 'The local Node runtime is missing npm. Remove its .tools/node-v24.21.0-win-* directory and retry setup.' }
        Write-Output 'Installing the pinned TypeScript compiler...'
        $install = Invoke-SetupProcess $nodePath @($npm, 'ci', '--prefix', $stagedCompiler, '--ignore-scripts', '--no-audit', '--no-fund', '--no-update-notifier', '--fetch-retries=1', '--fetch-timeout=60000') 180
        [IO.File]::WriteAllText((Join-Path $toolsRoot 'install.log'), ($install.stdout + $install.stderr), [Text.UTF8Encoding]::new($false))
        if ($install.exitCode -ne 0) { throw "TypeScript dependency installation failed (exit $($install.exitCode)). Check $(Join-Path $toolsRoot 'install.log') and retry setup." }
        if (!(Test-PinnedCompiler $nodePath $stagedCompiler)) { throw 'The installed TypeScript compiler failed verification. Existing dependencies were preserved.' }
        $modules = Join-Path $compilerRoot 'node_modules'
        $previousModules = Join-Path $stage 'previous-node-modules'
        if (Test-Path -LiteralPath $modules) { Move-SetupDirectory $modules $previousModules }
        try {
            Move-SetupDirectory (Join-Path $stagedCompiler 'node_modules') $modules
            if (!(Test-PinnedCompiler $nodePath $compilerRoot)) { throw 'The TypeScript compiler failed verification after installation.' }
        }
        catch {
            if (Test-Path -LiteralPath $modules) { Move-SetupDirectory $modules (Join-Path $stage 'failed-node-modules') }
            if (Test-Path -LiteralPath $previousModules) { Move-SetupDirectory $previousModules $modules }
            throw
        }
        Save-Ready $nodeDirectoryName
        Write-Output "Kimchily TypeScript is ready (Node $nodeVersion, TypeScript $typescriptVersion)."
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $exitCode = 1
}
finally {
    if ($stage) {
        if ($exitCode -ne 0 -and ((Test-Path -LiteralPath (Join-Path $stage 'previous-node')) -or
            (Test-Path -LiteralPath (Join-Path $stage 'previous-node-modules')))) {
            [Console]::Error.WriteLine("Previous toolchain files were retained for recovery: $stage")
        }
        else {
            try { Remove-SetupDirectory $stage }
            catch { [Console]::Error.WriteLine("Temporary setup files could not be removed: $stage") }
        }
    }
    if ($fileLock) { $fileLock.Dispose() }
    if ($locked) { $mutex.ReleaseMutex() }
    if ($mutex) { $mutex.Dispose() }
}
exit $exitCode
