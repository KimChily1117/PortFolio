param([Parameter(Mandatory=$true)][ValidatePattern('^tls://[A-Za-z0-9.-]+$')][string]$Server,[Parameter(Mandatory=$true)][string]$CredentialDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$run=Join-Path $root ('Artifacts/portfolio-4p-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $run -Force|Out-Null
@{version=1;server=$Server;port=28443}|ConvertTo-Json|Set-Content "$run/playtest.server.json" -Encoding utf8NoBOM
$ids=@()
$epoch=[DateTime]::UtcNow.Ticks
foreach($i in 1..4){
 $credential=Join-Path $CredentialDirectory "p$i.json"
 if(-not(Test-Path -LiteralPath $credential)){throw "Missing private credential $i"}
 $out=Join-Path $run "p$i";New-Item -ItemType Directory -Path $out -Force|Out-Null
 $arguments='--flow --member --flow-auto --portfolio-demo --portfolio-slot {0} --record-four --member-endpoint "{1}" --member-credential-file "{2}" --style-index {0} --party-size 4 --output "{3}" -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile "{3}/player.log"' -f $i,"$run/playtest.server.json",$credential,$out
 $arguments+=' --portfolio-start-utc '+$epoch
 # User explicitly requested four visible clients and a recording.
 $player=Start-Process (Join-Path $root 'Build/ProjectDawnRaid.exe') -ArgumentList $arguments -PassThru
 $ids+=$player.Id
 Start-Sleep -Seconds 4
}
@{players=$ids;partySize=4;server=$Server;port=28443;path=$run;transport='public-tls';dbProvider='MySql';bossHpFixture=$false;input='opt-in ordinary-command automation';scope='one Windows PC / four Unity processes / existing AWS VM'}|ConvertTo-Json|Set-Content "$run/processes.json" -Encoding utf8NoBOM
Write-Output $run
