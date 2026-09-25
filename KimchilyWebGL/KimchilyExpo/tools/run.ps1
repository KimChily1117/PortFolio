param(
    [ValidateSet('start','install','typecheck','test','export','doctor','build-android','build-ios','eas-login','eas-init','eas-status','register-ios','list-ios-devices')][string]$Task = 'start',
    [string]$Node = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Node) {
    $configuredNode = Get-Command node -ErrorAction SilentlyContinue
    if ($configuredNode) { $Node = $configuredNode.Source }
    $version = if ($Node) { & $Node -p 'process.versions.node' } else { '0.0.0' }
    if ([version]$version -lt [version]'22.13.0') {
        $bundled = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
        if (Test-Path -LiteralPath $bundled) { $Node = $bundled }
    }
}
if (!$Node -or !(Test-Path -LiteralPath $Node) -or [version](& $Node -p 'process.versions.node') -lt [version]'22.13.0') {
    throw 'Install Node.js 22.13+ (Node 24 LTS recommended), or supply -Node with its executable path.'
}
$oldPath = $env:Path
$oldEasNoVcs = $env:EAS_NO_VCS
$oldEasProjectRoot = $env:EAS_PROJECT_ROOT
$exitCode = 0
Push-Location $projectRoot
try {
    $env:Path = (Split-Path $Node) + ';' + $env:Path
    $expo = Join-Path $projectRoot 'node_modules/expo/bin/cli'
    $eas = Join-Path $projectRoot 'node_modules/eas-cli/bin/run'
    if ($Task -in @('build-android','build-ios','eas-login','eas-init','eas-status','register-ios','list-ios-devices')) {
        # This preserved workspace is a filesystem clone. Archive only this app,
        # without asking EAS to initialize or commit the Unity workspace.
        $env:EAS_NO_VCS = '1'
        $env:EAS_PROJECT_ROOT = $projectRoot
    }
    switch ($Task) {
        'install' {
            $npm = Get-Command npm.cmd -ErrorAction Stop
            $npmCli = Join-Path (Split-Path $npm.Source) 'node_modules/npm/bin/npm-cli.js'
            & $Node $npmCli ci
        }
        'start' { & $Node $expo start --lan --go }
        'typecheck' { & $Node node_modules/typescript/bin/tsc --noEmit }
        'test' { & $Node --experimental-strip-types --test tests/*.test.ts }
        'export' { & $Node $expo export --platform all --output-dir dist-check }
        'doctor' { & $Node $expo install --check }
        'build-android' { & $Node $eas build --platform android --profile preview }
        'build-ios' { & $Node $eas build --platform ios --profile preview }
        'eas-login' { & $Node $eas login }
        'eas-init' { & $Node $eas init }
        'eas-status' { & $Node $eas whoami }
        'register-ios' { & $Node $eas device:create }
        'list-ios-devices' { & $Node $eas device:list }
    }
    $exitCode = $LASTEXITCODE
}
finally {
    $env:Path = $oldPath
    $env:EAS_NO_VCS = $oldEasNoVcs
    $env:EAS_PROJECT_ROOT = $oldEasProjectRoot
    Pop-Location
}
exit $exitCode
