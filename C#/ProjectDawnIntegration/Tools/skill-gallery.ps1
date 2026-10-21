param(
    [string]$Skill = '',
    [ValidateRange(4,24)][int]$Fps = 12,
    [switch]$ShowCutscenes,
    [string]$Output = '',
    [switch]$Encode,
    [string]$Ffmpeg = ''
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$game = Join-Path $project 'Build/ProjectDawnRaid.exe'
if (-not (Test-Path -LiteralPath $game)) { throw 'Build the Unity client first.' }
if (-not $Output) { $Output = Join-Path $project ('Artifacts/skill-gallery-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath (Join-Path $Output 'skill-gallery')) { throw 'Choose a new output directory to preserve previous captures.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$gameArgs = @('--skill-gallery')
if ($Skill) {
    if ($Skill -notmatch '^[a-z0-9]+$') { throw 'Skill must be a catalog key such as bloodsword, frenzy, awaken3.' }
    $gameArgs += $Skill
}
$gameArgs += @('--gallery-fps', "$Fps", '--output', ('"' + $Output + '"'), '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800', '-logFile', ('"' + (Join-Path $Output 'unity.log') + '"'))
if ($ShowCutscenes) { $gameArgs += '--gallery-cutscenes' }
# This is a visible, interactive game recording; helpers are not started in visible windows.
$process = Start-Process -FilePath $game -ArgumentList $gameArgs -WorkingDirectory $project -PassThru
Write-Output "Recording Unity skills in a visible window. PID=$($process.Id), output=$Output"
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Gallery failed with exit code $($process.ExitCode). Inspect $Output/unity.log" }
$reviewArgs = @((Join-Path $PSScriptRoot 'skill-gallery-review.py'), (Join-Path $Output 'skill-gallery'))
if ($Encode) { $reviewArgs += '--encode' }
if ($Ffmpeg) { $reviewArgs += @('--ffmpeg', $Ffmpeg) }
& python @reviewArgs
if ($LASTEXITCODE -ne 0) { throw 'Gallery artifact verification failed.' }
Write-Output (Join-Path $Output 'skill-gallery/index.html')
