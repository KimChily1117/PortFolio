param([string]$UnityEditor = '')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build_world.ps1') -Target WebGL -UnityEditor $UnityEditor
