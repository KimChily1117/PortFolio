$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& python -m pip install --target (Join-Path $projectRoot '.deps') -r (Join-Path $projectRoot 'requirements-tls.txt')
if ($LASTEXITCODE -ne 0) { throw 'Development certificate dependency installation failed.' }
