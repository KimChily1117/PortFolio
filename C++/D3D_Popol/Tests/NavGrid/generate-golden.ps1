param(
    [string] $OutputPath = (Join-Path $PSScriptRoot 'Assets\golden-grid-v1.navgrid')
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$mapIdBytes = $utf8.GetBytes('golden-grid-v1')
$cells = [byte[]](0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0)

$canonicalStream = [System.IO.MemoryStream]::new()
$canonicalWriter = [System.IO.BinaryWriter]::new($canonicalStream)
$canonicalWriter.Write([uint32]1)
$canonicalWriter.Write([uint32]$mapIdBytes.Length)
$canonicalWriter.Write($mapIdBytes)
$canonicalWriter.Write([uint32]4)
$canonicalWriter.Write([uint32]3)
$canonicalWriter.Write([single]1.0)
$canonicalWriter.Write([single]-2.0)
$canonicalWriter.Write([single]0.0)
$canonicalWriter.Write([single]-1.0)
$canonicalWriter.Write([single]0.5)
$canonicalWriter.Write([uint32]1)
$canonicalWriter.Write([uint32]$cells.Length)
$canonicalWriter.Write($cells)
$canonicalWriter.Flush()
$canonical = $canonicalStream.ToArray()
$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($canonical)

$assetStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($assetStream)
$writer.Write([byte[]](0x4E, 0x56, 0x47, 0x31))
$writer.Write([uint32]1)
$writer.Write([uint32](84 + $mapIdBytes.Length))
$writer.Write([uint32]$mapIdBytes.Length)
$writer.Write($mapIdBytes)
$writer.Write([uint32]4)
$writer.Write([uint32]3)
$writer.Write([single]1.0)
$writer.Write([single]-2.0)
$writer.Write([single]0.0)
$writer.Write([single]-1.0)
$writer.Write([single]0.5)
$writer.Write([uint32]1)
$writer.Write([uint32]$cells.Length)
$writer.Write($hash)
$writer.Write($cells)
$writer.Flush()

$directory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
[System.IO.File]::WriteAllBytes($OutputPath, $assetStream.ToArray())

$hex = ([BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
Write-Output "Generated: $OutputPath"
Write-Output "SHA-256: $hex"
