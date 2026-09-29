# Build only: .\tools\build_chili_island.ps1
# Regenerate original art and publish: .\tools\build_chili_island.ps1 -RegenerateArt -Publish
# Unity always opens the separate build clone; the interactive Creator stays open.
param(
    [string]$UnityEditor = '',
    [switch]$RegenerateArt,
    [switch]$Publish,
    [string]$ServerUrl = 'http://127.0.0.1:8788',
    [string]$TokenFile = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$creatorRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $creatorRoot '..'))
$cloneRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot 'Artifacts\ChiliIslandBuild'))
$cloneArtifacts = Join-Path $cloneRoot 'Artifacts'
$sceneRelative = 'Assets\Demos\ChiliIsland\Scenes\ChiliIsland.unity'
$needsArt = $RegenerateArt -or !(Test-Path -LiteralPath (Join-Path $creatorRoot $sceneRelative))
Assert-KimchilyUnityProject -ProjectPath $creatorRoot

# 제작자의 서버 TS를 먼저 빌드한다. 결과는 승인된 서버 번들과 클라이언트의 규칙 해시다.
# 이 단계에서 실패하면 Unity 콘텐츠를 게시하지 않아 서로 다른 규칙의 배포를 막는다.
& (Join-Path $workspaceRoot 'KimchilyServer\tools\compile-script.ps1')
if (-not $?) { throw 'Server TypeScript compilation failed before the Unity build.' }

function Assert-ChiliCloneRoute {
    # Do not follow a linked output directory into another project.
    $cursorPath = $cloneRoot
    while ($cursorPath -and $cursorPath -ne $workspaceRoot) {
        if (Test-Path -LiteralPath $cursorPath) {
            $item = Get-Item -LiteralPath $cursorPath
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Linked clone output directories are not allowed: $cursorPath"
            }
        }
        $cursorPath = [IO.Path]::GetDirectoryName($cursorPath)
    }
    if ($cursorPath -ne $workspaceRoot -or $cloneRoot -eq $creatorRoot) {
        throw 'Chili Island must build in the separate Artifacts/ChiliIslandBuild clone.'
    }
}

function Assert-ChiliCloneIdle {
    $unityLock = Join-Path $cloneRoot 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $unityLock) {
        $handle = $null
        try { $handle = [IO.File]::Open($unityLock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch { throw "The Chili Island clone is already open or busy. Close that clone before building: $cloneRoot" }
        finally { if ($handle) { $handle.Dispose() } }
    }
}

function Copy-ChiliTree {
    param([string]$Source, [string]$Destination)
    if (!(Test-Path -LiteralPath $Source -PathType Container)) { throw "Source directory is missing: $Source" }
    # /E copies additions and changes; it never purges destination files or the cached Library.
    & robocopy.exe $Source $Destination /E /R:1 /W:1 /COPY:DAT /DCOPY:DAT /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    $copyExitCode = $LASTEXITCODE
    if ($copyExitCode -ge 8) { throw "Copy failed ($copyExitCode): $Source -> $Destination" }
}

function Quote-ChiliArgument {
    param([string]$Value)
    if ($Value.Contains('"') -or $Value.Contains("`r") -or $Value.Contains("`n")) {
        throw 'Unity arguments must not contain quotation marks or line breaks.'
    }
    return '"' + $Value + '"'
}

function Invoke-ChiliUnity {
    param(
        [string]$Method,
        [string]$Log,
        [string]$Marker = '',
        [switch]$Graphics,
        [switch]$Publisher,
        [string[]]$ExtraArguments = @(),
        [int]$TimeoutMilliseconds = 1200000
    )
    Assert-ChiliCloneIdle
    $arguments = @('-batchmode')
    if (!$Graphics) { $arguments += '-nographics' }
    $arguments += @('-projectPath', (Quote-ChiliArgument $cloneRoot), '-buildTarget', 'WebGL',
        '-executeMethod', $Method, '-logFile', (Quote-ChiliArgument $Log))
    # The asynchronous publisher exits Unity itself after the HTTP result is written.
    if (!$Publisher) { $arguments += '-quit' }
    $arguments += $ExtraArguments
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $cloneRoot -WindowStyle Hidden -PassThru
    try {
        if (!$process.WaitForExit($TimeoutMilliseconds)) {
            $process.Kill()
            throw "Chili Island Unity operation timed out: $Log"
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $Log) -or (Get-Item -LiteralPath $Log).LastWriteTimeUtc -lt $started) {
            throw "Chili Island Unity operation failed: $Log"
        }
        if ($Marker -and !(Select-String -LiteralPath $Log -SimpleMatch $Marker -Quiet)) {
            throw "Chili Island completion marker was not found: $Log"
        }
    }
    finally { $process.Dispose() }
    return $started
}

Assert-ChiliCloneRoute
Assert-ChiliCloneIdle
New-Item -ItemType Directory -Path $cloneArtifacts -Force | Out-Null
$runnerLock = $null
try {
    try { $runnerLock = [IO.File]::Open((Join-Path $cloneArtifacts 'chili-build.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { throw "Another Chili Island build runner is using this clone: $cloneRoot" }
    Assert-ChiliCloneIdle
    foreach ($directory in @('Assets', 'Packages', 'ProjectSettings')) {
        Copy-ChiliTree -Source (Join-Path $creatorRoot $directory) -Destination (Join-Path $cloneRoot $directory)
    }

    # UPM file: paths are relative to Packages/manifest.json, not the Unity project root.
    # Keep dependencies pointed at the original local packages after moving the project deeper.
    $manifestPath = Join-Path $cloneRoot 'Packages\manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($dependency in $manifest.dependencies.PSObject.Properties) {
        $value = [string]$dependency.Value
        if (!$value.StartsWith('file:', [StringComparison]::OrdinalIgnoreCase)) { continue }
        $reference = $value.Substring(5)
        if ($reference.StartsWith('//')) {
            $packagePath = ([Uri]$value).LocalPath
        }
        elseif ([IO.Path]::IsPathRooted($reference)) { $packagePath = $reference }
        else { $packagePath = Join-Path (Join-Path $creatorRoot 'Packages') $reference }
        $packagePath = [IO.Path]::GetFullPath($packagePath)
        if (!(Test-Path -LiteralPath $packagePath)) { throw "Local package is missing: $($dependency.Name) -> $packagePath" }
        $dependency.Value = 'file:' + $packagePath.Replace('\', '/')
    }
    [IO.File]::WriteAllText($manifestPath, (($manifest | ConvertTo-Json -Depth 32) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    Assert-KimchilyUnityProject -ProjectPath $cloneRoot
    Write-Output "Build clone: $cloneRoot"

    if ($needsArt -or !(Test-Path -LiteralPath (Join-Path $cloneRoot $sceneRelative))) {
        $artLog = Join-Path $cloneArtifacts 'chili-island-art.log'
        $null = Invoke-ChiliUnity -Method 'Kimchily.Creator.Project.ChiliIslandArt.Generate' -Log $artLog -Marker 'KIMCHILY_CHILI_ART_READY' -Graphics
        if (!(Test-Path -LiteralPath (Join-Path $cloneRoot $sceneRelative))) { throw "Generated Chili Island scene is missing: $artLog" }
        # Copy back only this generated demo tree, never Assets/World, MyWorld, or project settings.
        Copy-ChiliTree -Source (Join-Path $cloneRoot 'Assets\Demos\ChiliIsland') -Destination (Join-Path $creatorRoot 'Assets\Demos\ChiliIsland')
        foreach ($metaRelative in @('Assets\Demos.meta', 'Assets\Demos\ChiliIsland.meta')) {
            $sourceMeta = Join-Path $cloneRoot $metaRelative
            $targetMeta = Join-Path $creatorRoot $metaRelative
            if ((Test-Path -LiteralPath $sourceMeta) -and !(Test-Path -LiteralPath $targetMeta)) {
                Copy-Item -LiteralPath $sourceMeta -Destination $targetMeta
            }
        }
        Write-Output "Generated demo assets copied to: $(Join-Path $creatorRoot 'Assets\Demos\ChiliIsland')"
        Write-Output "Art previews and export artifacts remain in: $cloneArtifacts"
    }

    $buildLog = Join-Path $cloneArtifacts 'chili-island-build.log'
    $buildStarted = Invoke-ChiliUnity -Method 'Kimchily.Creator.Project.ChiliIslandArt.BuildWorld' -Log $buildLog -Marker 'KIMCHILY_CHILI_WORLD_READY'
    $buildMarker = Join-Path $cloneArtifacts 'last-build.txt'
    if (!(Test-Path -LiteralPath $buildMarker) -or (Get-Item -LiteralPath $buildMarker).LastWriteTimeUtc -lt $buildStarted) {
        throw "The build did not produce a fresh output marker: $buildLog"
    }
    $buildDirectory = [IO.Path]::GetFullPath((Get-Content -LiteralPath $buildMarker -Raw).Trim())
    $worldBuildRoot = [IO.Path]::GetFullPath((Join-Path $cloneRoot 'WorldBuilds')).TrimEnd('\', '/')
    if (!$buildDirectory.StartsWith($worldBuildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $buildDirectory -PathType Container)) {
        throw "Unexpected world build directory: $buildDirectory"
    }
    New-Item -ItemType Directory -Path (Join-Path $creatorRoot 'Artifacts') -Force | Out-Null
    Copy-Item -LiteralPath $buildMarker -Destination (Join-Path $creatorRoot 'Artifacts\chili-island-last-build.txt') -Force
    Write-Output "Chili Island build: $buildDirectory"

    if ($Publish) {
        if (!$TokenFile) { $TokenFile = Join-Path $workspaceRoot 'KimchilyPublish\.local\token' }
        $TokenFile = [IO.Path]::GetFullPath($TokenFile)
        if (!(Test-Path -LiteralPath $TokenFile -PathType Leaf)) { throw 'Start the local publisher before publishing, or provide -TokenFile.' }
        $publishLog = Join-Path $cloneArtifacts 'chili-island-publish.log'
        $publishResult = Join-Path $cloneArtifacts 'chili-island-publish-result.json'
        $publishArguments = @('-publishBuildDirectory', (Quote-ChiliArgument $buildDirectory),
            '-publishServerUrl', (Quote-ChiliArgument $ServerUrl), '-publishTokenFile', (Quote-ChiliArgument $TokenFile),
            '-publishResultFile', (Quote-ChiliArgument $publishResult))
        $publishStarted = Invoke-ChiliUnity -Method 'Kimchily.Creator.Editor.WorldPublisherBatch.Publish' -Log $publishLog -Publisher -ExtraArguments $publishArguments -TimeoutMilliseconds 300000
        if (!(Test-Path -LiteralPath $publishResult) -or (Get-Item -LiteralPath $publishResult).LastWriteTimeUtc -lt $publishStarted) {
            throw "Publication did not write a fresh result: $publishLog"
        }
        $published = Get-Content -LiteralPath $publishResult -Raw | ConvertFrom-Json
        if (!$published.publishUrl) { throw "Publication result has no publish URL: $publishResult" }
        Write-Output ('Published: ' + $published.publishUrl)
        Write-Output ('Result: ' + $publishResult)
    }
}
finally { if ($runnerLock) { $runnerLock.Dispose() } }
