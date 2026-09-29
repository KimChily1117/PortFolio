param(
    [string]$SourcePath = '',
    [string]$ScriptId = 'chili-portal-ts-v1',
    [string]$WorldId = 'chili-island',
    [string]$OutputDirectory = '',
    [switch]$SkipIdentity
)
$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webglRoot = [IO.Path]::GetFullPath((Join-Path $serverRoot '..'))
if (!$SourcePath) { $SourcePath = Join-Path $webglRoot 'KimchilyCreator/ServerScripts/chili-portal/PortalRules.ts' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $serverRoot 'games' }
# SDK가 설치·검증한 로컬 Node를 사용하므로 전역 Node 설치나 PATH 변경이 필요 없다.
$compiler = Join-Path $webglRoot 'KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler'
$readyPath = Join-Path $compiler '.tools/ready.json'
if (!(Test-Path -LiteralPath $readyPath)) { throw 'Unity Kimchily/TypeScript/Install or Repair Compiler를 먼저 실행해 주세요.' }
$ready = Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
$node = Join-Path $compiler ('.tools/' + $ready.nodeRelativePath)
$arguments = @((Join-Path $PSScriptRoot 'compile-script.cjs'), '--source', [IO.Path]::GetFullPath($SourcePath),
    '--id', $ScriptId, '--world', $WorldId, '--output', [IO.Path]::GetFullPath($OutputDirectory))
if (!$SkipIdentity) { $arguments += @('--identity', (Join-Path $webglRoot 'KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalRuleIdentity.ts')) }
& $node @arguments
if ($LASTEXITCODE -ne 0) { throw '서버 TypeScript 빌드가 실패했습니다.' }
