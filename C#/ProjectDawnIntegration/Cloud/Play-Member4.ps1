param([Parameter(Mandatory=$true)][ValidatePattern('^tls://[A-Za-z0-9.-]+$')][string]$Server,[Parameter(Mandatory=$true)][string]$CredentialDirectory,[int]$ReturnAfter=100,[switch]$Chat,[switch]$Reentry,[ValidateRange(1,4)][int]$PartySize=4)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$run=Join-Path $root ('Artifacts/aws-flow-'+$PartySize+'p-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $run -Force|Out-Null
@{version=1;server=$Server;port=28443}|ConvertTo-Json|Set-Content (Join-Path $run 'playtest.server.json') -Encoding utf8NoBOM
$ids=@()
foreach($i in 1..$PartySize){
 $credential=Join-Path $CredentialDirectory "p$i.json"
 if(-not(Test-Path -LiteralPath $credential)){throw "Missing private QA credential for player $i"}
 $out=Join-Path $run "p$i";New-Item -ItemType Directory -Path $out -Force|Out-Null
 $arguments='--flow --member --flow-auto --member-endpoint "{0}" --member-credential-file "{1}" --style-index {2} --return-after {3} --output "{4}" -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile "{4}/player.log"' -f (Join-Path $run 'playtest.server.json'),$credential,$i,$ReturnAfter,$out
 if($Chat){$arguments+=' --chat-auto'}
 if($Reentry){$arguments+=' --reentry-auto'}
 $arguments+=' --party-size '+$PartySize
 $player=Start-Process (Join-Path $root 'Build/ProjectDawnRaid.exe') -ArgumentList $arguments -PassThru
 $ids+=$player.Id
 Start-Sleep -Seconds 2
}
@{players=$ids;partySize=$PartySize;server=$Server;port=28443;path=$run;transport='public-tls';dbProvider='MySql';returnAfter=$ReturnAfter}|ConvertTo-Json|Set-Content (Join-Path $run 'processes.json') -Encoding utf8NoBOM
Write-Output $run
