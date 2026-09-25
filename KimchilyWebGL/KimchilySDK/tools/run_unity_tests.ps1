param(
    [string]$UnityEditor = '',
    [ValidateSet('fixture-build', 'EditMode', 'PlayMode')]
    [string[]]$Stages = @('fixture-build', 'EditMode', 'PlayMode')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/unity.ps1')
$UnityEditor = Resolve-KimchilyUnityEditor -UnityEditor $UnityEditor
$sdkRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validationProject = Join-Path $sdkRoot 'ValidationProject'
Assert-KimchilyUnityProject -ProjectPath $validationProject
$artifactRoot = Join-Path $sdkRoot 'Artifacts'
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw 'Unity Editor executable not found.' }
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
function Invoke-UnityValidation([string]$Stage, [string[]]$Extra) {
    $log = Join-Path $artifactRoot ($Stage + '.log')
    $baseArgs = @('-batchmode', '-nographics', '-projectPath', ('"' + $validationProject + '"'),
                  '-logFile', ('"' + $log + '"'))
    Write-Output "Running Unity stage: $Stage"
    $process = Start-Process -FilePath $UnityEditor -ArgumentList ($baseArgs + $Extra) -WorkingDirectory $validationProject -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(900000)) {
        # Only stop the process started by this script, never other Editor sessions.
        $process.Kill()
        throw "$Stage timed out. Inspect $log"
    }
    if ($process.ExitCode -ne 0) { throw "$Stage failed with exit $($process.ExitCode). Inspect $log" }
}
if ($Stages -contains 'fixture-build') {
    Invoke-UnityValidation 'fixture-build' @('-executeMethod', 'Kimchily.Validation.VerificationBootstrap.BuildFixture', '-quit')
}
foreach ($platform in @('EditMode', 'PlayMode')) {
    if ($Stages -notcontains $platform) { continue }
    $result = Join-Path $artifactRoot ($platform.ToLowerInvariant() + '.xml')
    $started = [DateTime]::UtcNow
    Invoke-UnityValidation $platform.ToLowerInvariant() @('-runTests', '-testPlatform', $platform, '-testResults', ('"' + $result + '"'))
    if (-not (Test-Path -LiteralPath $result)) { throw "No test report generated: $result" }
    if ((Get-Item -LiteralPath $result).LastWriteTimeUtc -lt $started) { throw "Stale test report: $result" }
    [xml]$document = Get-Content -LiteralPath $result -Raw
    $run = $document.'test-run'
    $minimumCount = if ($platform -eq 'EditMode') { 6 } else { 20 }
    if ($run.result -ne 'Passed' -or [int]$run.failed -ne 0 -or
        [int]$run.skipped -ne 0 -or [int]$run.inconclusive -ne 0 -or
        [int]$run.passed -lt $minimumCount) {
        throw "Incomplete or failing test run: $result"
    }
    Write-Output "$platform passed: $($run.passed) tests."
}
Write-Output "Requested Unity validation stages completed. Reports: $artifactRoot"
