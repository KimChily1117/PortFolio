$ErrorActionPreference = 'Stop'
$archiveUrl = 'https://registry.npmjs.org/jsqr/-/jsqr-1.4.0.tgz'
$expectedIntegrity = 'dxLob7q65Xg2DvstYkRpkYtmKm2sPJ9oFhrhmudT1dZvNFFTlroai3AWSpLey/w5vMcLBXRgOJsbXpdN9HzU/A=='
$cache = Join-Path $PSScriptRoot '../tests/.vendor-cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$archive = Join-Path $cache 'jsqr-1.4.0.tgz'
Invoke-WebRequest -Uri $archiveUrl -OutFile $archive
$algorithm = [Security.Cryptography.SHA512]::Create()
try { $actualIntegrity = [Convert]::ToBase64String($algorithm.ComputeHash([IO.File]::ReadAllBytes($archive))) }
finally { $algorithm.Dispose() }
if ($actualIntegrity -ne $expectedIntegrity) { throw 'jsQR archive integrity differs from the pinned npm release.' }
& tar -xf $archive -C $cache 'package/dist/jsQR.js' 'package/LICENSE' 'package/package.json'
if ($LASTEXITCODE -ne 0) { throw 'Could not extract the pinned jsQR release.' }
Copy-Item -LiteralPath (Join-Path $cache 'package/dist/jsQR.js') -Destination (Join-Path $PSScriptRoot 'jsQR-1.4.0.js')
Copy-Item -LiteralPath (Join-Path $cache 'package/LICENSE') -Destination (Join-Path $PSScriptRoot 'jsQR-LICENSE.txt')
$provenance = [ordered]@{
  package = 'jsqr'; version = '1.4.0'; license = 'Apache-2.0'
  repository = 'https://github.com/cozmo/jsQR'; gitCommit = '49a9633931fb8030ac2fc9cecc121d6e5a19f9a3'
  archiveUrl = $archiveUrl; archiveIntegrity = "sha512-$expectedIntegrity"
  javascriptSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'jsQR-1.4.0.js') -Algorithm SHA256).Hash.ToLowerInvariant()
  licenseSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'jsQR-LICENSE.txt') -Algorithm SHA256).Hash.ToLowerInvariant()
  modified = $false
}
$provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'jsqr-provenance.json') -Encoding utf8
Write-Output 'JSQR_VENDOR_READY 1.4.0'
