param([string]$Protoc = 'E:\task\Server\Common\protoc-3.12.3-win64\bin\protoc.exe',
      [string]$Include = 'E:\task\Server\Common\protoc-3.12.3-win64\include')
$ErrorActionPreference = 'Stop'
$outputPath = Join-Path $PSScriptRoot '../Server/Server/Packet'
& $Protoc "--proto_path=$PSScriptRoot" "--proto_path=$Include" "--csharp_out=$outputPath" (Join-Path $PSScriptRoot 'Protocol.proto') (Join-Path $PSScriptRoot 'Raid.proto')
if ($LASTEXITCODE -ne 0) { throw 'Protocol generation failed' }
# Packet managers deliberately retain their authenticated dispatch guards and explicit registrations.
