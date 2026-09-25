# Dot-source this file from clone build/test runners. No global Hub settings are changed.
$script:KimchilyUnityToolsRoot = $PSScriptRoot

function Get-KimchilyUnityConfiguration {
    Get-Content -LiteralPath (Join-Path $script:KimchilyUnityToolsRoot 'unity-version.json') -Raw | ConvertFrom-Json
}

function Resolve-KimchilyUnityEditor {
    param([string]$UnityEditor = '', [switch]$AllowMissing)
    $configuration = Get-KimchilyUnityConfiguration
    $selected = $UnityEditor
    if ([string]::IsNullOrWhiteSpace($selected)) { $selected = $env:KIMCHILY_UNITY_EDITOR }
    if ([string]::IsNullOrWhiteSpace($selected)) { $selected = $env:KIMCHILY_UNITY_EDITOR_ROOT }
    if ([string]::IsNullOrWhiteSpace($selected) -and $configuration.editorRoot) {
        $selected = Join-Path $configuration.editorRoot 'Unity.exe'
    }
    if ([string]::IsNullOrWhiteSpace($selected)) {
        $selected = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $configuration.version + '\Editor\Unity.exe')
    }
    if (Test-Path -LiteralPath $selected -PathType Container) { $selected = Join-Path $selected 'Unity.exe' }
    $selected = [IO.Path]::GetFullPath($selected)
    if (-not (Test-Path -LiteralPath $selected -PathType Leaf)) {
        if ($AllowMissing) { return $selected }
        throw "Unity $($configuration.version) is not installed at $selected. Pass -UnityEditor or set KIMCHILY_UNITY_EDITOR."
    }
    $productVersion = (Get-Item -LiteralPath $selected).VersionInfo.ProductVersion
    if ($productVersion -notmatch ('^' + [regex]::Escape($configuration.version) + '(?:\b|_|\s|$)')) {
        throw "This clone requires Unity $($configuration.version); selected executable reports '$productVersion'."
    }
    return $selected
}

function Assert-KimchilyUnityProject {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)
    $workspace = [IO.Path]::GetFullPath((Join-Path $script:KimchilyUnityToolsRoot '..')).TrimEnd('\', '/')
    $project = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    if (-not $project.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The Unity project must be inside this clone workspace.'
    }
    # A junction can escape the lexical workspace. Build scripts reject one on the route.
    $cursor = Get-Item -LiteralPath $project -ErrorAction Stop
    while ($cursor -and $cursor.FullName.TrimEnd('\', '/') -ne $workspace) {
        if (($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Linked Unity project directories are not allowed: $($cursor.FullName)"
        }
        $cursor = $cursor.Parent
    }
    $configuration = Get-KimchilyUnityConfiguration
    $versionFile = Join-Path $project 'ProjectSettings\ProjectVersion.txt'
    $expected = '^m_EditorVersion:\s*' + [regex]::Escape($configuration.version) + '\s*$'
    if (-not (Select-String -LiteralPath $versionFile -Pattern $expected -Quiet)) {
        throw "Project version does not match clone Unity $($configuration.version): $versionFile"
    }
}
