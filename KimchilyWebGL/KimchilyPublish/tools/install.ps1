$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& python -m pip install --target (Join-Path $projectRoot '.deps') -r (Join-Path $projectRoot 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Publisher dependency installation failed.' }
