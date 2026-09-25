param([string]$StateDirectory = '', [switch]$Json)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'server-common.ps1')
$context = Get-PublisherContext $StateDirectory
try {
    $state = Get-PublisherState $context
    Write-PublisherResult $state -Json:$Json
    if ($state.status -in @('running', 'stopped')) { exit 0 }
    exit 1
}
catch { Write-PublisherFailure $context $_.Exception.Message -Json:$Json; exit 1 }
