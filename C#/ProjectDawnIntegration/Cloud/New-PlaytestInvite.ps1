param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]{3,64}$')][string[]]$Accounts,[string]$OutputDirectory=(Join-Path $env:LOCALAPPDATA 'ProjectDawn/Invites'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDirectory|Out-Null
$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$acl=Get-Acl -LiteralPath $OutputDirectory
$acl.SetAccessRuleProtection($true,$false)
$acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $OutputDirectory -AclObject $acl
$roster=Join-Path $OutputDirectory 'invites.hashes.json'
$hashes=if(Test-Path -LiteralPath $roster){Get-Content -LiteralPath $roster -Raw|ConvertFrom-Json -AsHashtable}else{@{}}
foreach($account in $Accounts){if($hashes.ContainsKey($account)){throw "Invite already exists for $account. Retain it or rotate explicitly outside this generator."}}
foreach($account in $Accounts){
 $bytes=[byte[]]::new(32);[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
 $token=[Convert]::ToBase64String($bytes)
 $hash=[Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($token)))
 $hashes[$account]=$hash
 [IO.File]::WriteAllText((Join-Path $OutputDirectory ($account+'.invite')),$token)
}
$hashes|ConvertTo-Json|Set-Content -LiteralPath $roster
Write-Output "Private invite files created in $OutputDirectory. Tokens were not printed."
