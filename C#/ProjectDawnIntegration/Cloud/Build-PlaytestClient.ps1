param([string]$BuildDirectory=(Join-Path (Split-Path $PSScriptRoot -Parent) 'Build'),[ValidatePattern('^$|^tls://[A-Za-z0-9.-]+$')][string]$Server='',[ValidateRange(1,65535)][int]$Port=28443)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root ('Artifacts/friend-playtest-'+(Get-Date -Format yyyyMMdd-HHmmss))
$stage=Join-Path $output 'ProjectDawn'
$allowed=@('ProjectDawnRaid.exe','UnityPlayer.dll','UnityCrashHandler64.exe','MonoBleedingEdge','ProjectDawnRaid_Data')
foreach($name in $allowed){if(-not(Test-Path -LiteralPath (Join-Path $BuildDirectory $name))){throw "Missing Unity build entry: $name"}}
New-Item -ItemType Directory -Path $stage -Force|Out-Null
foreach($name in $allowed){Copy-Item -LiteralPath (Join-Path $BuildDirectory $name) -Destination $stage -Recurse}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Playtest.cmd') -Destination (Join-Path $stage 'Start-ProjectDawn.cmd')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FRIEND-PLAYTEST-KO.md') -Destination (Join-Path $stage 'README-KO.md')
@{version=1;server=$Server;port=$Port}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $stage 'playtest.server.json') -Encoding utf8NoBOM
$files=@(Get-ChildItem -LiteralPath $stage -Recurse -File)
$forbidden=@($files|Where-Object {$_.Extension -in @('.invite','.pem','.pfx','.key','.sql','.bak') -or $_.Name -in @('local.json','invites.hashes.json','flow-server.config.json','playtest.connection.json')})
if($forbidden.Count){throw 'Unexpected private/server file found in client staging. Packaging stopped.'}
$manifest=@($files|ForEach-Object {@{path=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$manifest|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'files.json') -Encoding utf8NoBOM
$zip=Join-Path $output 'ProjectDawn-FriendTest.zip'
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
@{artifact=$zip;sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant();files=$files.Count;uncompressedBytes=($files|Measure-Object Length -Sum).Sum;serverEndpointStatus=$(if($Server){'configured-not-verified'}else{'pending-deployment'});authentication='member-password';createdUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding utf8NoBOM
Write-Output $output
