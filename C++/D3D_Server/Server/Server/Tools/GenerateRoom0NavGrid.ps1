param(
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\Server\Content\Navigation\room-0-nav-v1.navgrid')
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$mapIdBytes = $utf8.GetBytes('room-0-nav-v1')
$width = [uint32]145
$height = [uint32]145
$cells = [byte[]]::new($width * $height)

$canonicalStream = [System.IO.MemoryStream]::new()
$canonicalWriter = [System.IO.BinaryWriter]::new($canonicalStream)
$canonicalWriter.Write([uint32]1)
$canonicalWriter.Write([uint32]$mapIdBytes.Length)
$canonicalWriter.Write($mapIdBytes)
$canonicalWriter.Write($width)
$canonicalWriter.Write($height)
$canonicalWriter.Write([single]1.0)
$canonicalWriter.Write([single]0.0)
$canonicalWriter.Write([single]0.0)
$canonicalWriter.Write([single]0.0)
$canonicalWriter.Write([single]0.5)
$canonicalWriter.Write([uint32]1)
$canonicalWriter.Write([uint32]$cells.Length)
$canonicalWriter.Write($cells)
$canonicalWriter.Flush()
$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($canonicalStream.ToArray())

$assetStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($assetStream)
$writer.Write([byte[]](0x4E, 0x56, 0x47, 0x31))
$writer.Write([uint32]1)
$writer.Write([uint32](84 + $mapIdBytes.Length))
$writer.Write([uint32]$mapIdBytes.Length)
$writer.Write($mapIdBytes)
$writer.Write($width)
$writer.Write($height)
$writer.Write([single]1.0)
$writer.Write([single]0.0)
$writer.Write([single]0.0)
$writer.Write([single]0.0)
$writer.Write([single]0.5)
$writer.Write([uint32]1)
$writer.Write([uint32]$cells.Length)
$writer.Write($hash)
$writer.Write($cells)
$writer.Flush()

[System.IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
[System.IO.File]::WriteAllBytes($OutputPath, $assetStream.ToArray())
$hex = ([BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
Write-Output "Generated: $OutputPath"
Write-Output "Content SHA-256: $hex"