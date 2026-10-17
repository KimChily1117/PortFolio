param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('frontend','skill','battle','audio')][string]$Mode = 'frontend',
    [string]$Item = 'lobby',
    [switch]$Skip,
    [switch]$Pause,
    [switch]$Manual,
    [switch]$DriveInput,
    [switch]$Basic,
    [switch]$Fast,
    [switch]$NoCutscenes
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$binaryDirectory = Join-Path $projectRoot 'build-win\bin\AsterIdle\Release'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$capturePath = (Resolve-Path -LiteralPath $OutputDirectory).Path
$env:ASTER_MANUAL_BATTLE = if ($Manual -or $DriveInput) { '1' } else { $null }
$env:ASTER_INPUT_BATTLE = if ($DriveInput) { '1' } else { $null }
$env:ASTER_BASIC_BATTLE = if ($Basic) { '1' } else { $null }
$env:ASTER_FAST_BATTLE = if ($Fast) { '1' } else { $null }
$env:ASTER_CUTSCENES_OFF = if ($NoCutscenes) { '1' } else { $null }
$env:ASTER_SMOKE_SCENE = $Mode
$env:ASTER_CAPTURE_DIR = $capturePath
$env:ASTER_SMOKE_SAVE_DIR = Join-Path $projectRoot ('build-win\smoke-save\' + [guid]::NewGuid().ToString('N'))
$env:ASTER_FRONTEND_PAGE = $Item
$env:ASTER_SKILL_ID = $Item
$env:ASTER_SKIP_SKILL = if ($Skip) { '1' } else { $null }
$env:ASTER_PAUSE_SKILL = if ($Pause) { '1' } else { $null }
$env:ASTER_GALLERY_CAPTURE_DIR = $null
$env:ASTER_OPEN_GALLERY = $null
try {
    $process = Start-Process -FilePath (Join-Path $binaryDirectory 'AsterIdle.exe') -WorkingDirectory $binaryDirectory -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(90000)) {
        Stop-Process -Id $process.Id
        throw "Smoke process timed out: $Mode $Item"
    }
    if ($process.ExitCode -ne 0) { throw "Smoke failed with exit code $($process.ExitCode)" }
    $expected = if ($Mode -eq 'battle' -and $Basic) { 17 } elseif ($Mode -eq 'battle') { 7 } elseif ($Mode -eq 'skill') { 6 } else { 2 }
    $images = @(Get-ChildItem -LiteralPath $capturePath -Filter 'frame-*.png')
    if ($images.Count -ne $expected) { throw "Expected $expected captures, found $($images.Count)" }
    if ($Mode -eq 'battle' -and $Manual -and -not $DriveInput) {
        $lastState = Get-Content -LiteralPath (Join-Path $capturePath 'frame-7.txt') -Raw
        if ($lastState -notmatch 'turns=0 active=3 manual=1 phase=0') { throw 'Manual battle advanced without scripted input.' }
    }
    if ($Mode -eq 'battle' -and $Pause -and -not $Basic) {
        $before = Get-Content -LiteralPath (Join-Path $capturePath 'pause-before.txt') -Raw
        $after = Get-Content -LiteralPath (Join-Path $capturePath 'pause-after.txt') -Raw
        $final = Get-Content -LiteralPath (Join-Path $capturePath 'frame-7.txt') -Raw
        if ($before -notmatch 'paused=1' -or $after -notmatch 'paused=1' -or $final -notmatch 'paused=0') { throw 'Paused S3 Escape did not resume battle.' }
        if ($final -notmatch 'phase=1 result=1 paused=0') { throw 'Battle did not reach victory after paused S3 Escape.' }
    }
    if ($Mode -eq 'battle' -and $Basic) {
        $final = Get-Content -LiteralPath (Join-Path $capturePath 'frame-17.txt') -Raw
        if ($final -notmatch 'enemyActions=[1-9]') { throw 'No enemy attack was presented.' }
        if ($Pause) {
            $before = Get-Content -LiteralPath (Join-Path $capturePath 'pause-before.txt') -Raw
            $after = Get-Content -LiteralPath (Join-Path $capturePath 'pause-after.txt') -Raw
            if ($before -ne $after -or $before -notmatch 'result=1') { throw 'Normal action advanced while menu was open.' }
        }
    }
    Write-Output "PASS $Mode $Item : $($images.Count) frames in $capturePath"
} finally {
    foreach ($key in @('ASTER_SMOKE_SCENE','ASTER_CAPTURE_DIR','ASTER_SMOKE_SAVE_DIR','ASTER_FRONTEND_PAGE','ASTER_SKILL_ID','ASTER_SKIP_SKILL','ASTER_PAUSE_SKILL','ASTER_MANUAL_BATTLE','ASTER_INPUT_BATTLE','ASTER_BASIC_BATTLE','ASTER_FAST_BATTLE','ASTER_CUTSCENES_OFF')) {
        Remove-Item -LiteralPath "Env:\$key" -ErrorAction SilentlyContinue
    }
}
