param(
 [Parameter(Mandatory)][string]$ClientZip,
 [Parameter(Mandatory)][ValidatePattern('^tls://[A-Za-z0-9.-]+$')][string]$Server,
 [ValidateRange(1,65535)][int]$Port=28443,
 [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]{3,64}$')][string[]]$Accounts,
 [Parameter(Mandatory)][string]$InviteDirectory,
 [string]$OutputDirectory=(Join-Path $env:LOCALAPPDATA ('ProjectDawn/FriendPackages-'+(Get-Date -Format yyyyMMdd-HHmmss)))
)
$ErrorActionPreference='Stop'
if([Uri]::CheckHostName($Server.Substring(6)) -eq [UriHostNameType]::Unknown){throw 'Invalid TLS host.'}
if(-not(Test-Path -LiteralPath $ClientZip -PathType Leaf)){throw 'Client ZIP not found.'}
if(@($Accounts|Sort-Object -Unique).Count -ne $Accounts.Count){throw 'Accounts must be unique.'}
New-Item -ItemType Directory -Force $OutputDirectory|Out-Null
$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$acl=Get-Acl -LiteralPath $OutputDirectory
$acl.SetAccessRuleProtection($true,$false)
$acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $OutputDirectory -AclObject $acl
foreach($account in $Accounts){
 if(Test-Path -LiteralPath (Join-Path $OutputDirectory ($account+'.zip'))){throw 'Output already exists; choose a new directory.'}
 if(-not(Test-Path -LiteralPath (Join-Path $InviteDirectory ($account+'.invite')) -PathType Leaf)){throw "Missing invite file for $account"}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($account in $Accounts){
 $token=[IO.File]::ReadAllText((Join-Path $InviteDirectory ($account+'.invite'))).Trim()
 if($token -notmatch '\A[A-Za-z0-9_+/=-]{40,256}\z'){throw "Invalid invite file for $account"}
 $destination=Join-Path $OutputDirectory ($account+'.zip')
 Copy-Item -LiteralPath $ClientZip -Destination $destination
 $archive=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Update)
 try {
  if(-not $archive.GetEntry('ProjectDawn/ProjectDawnRaid.exe') -or $archive.GetEntry('ProjectDawn/playtest.connection.json')){throw 'Expected a clean common client package.'}
  $entry=$archive.CreateEntry('ProjectDawn/playtest.connection.json')
  $writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
  try {$writer.Write((@{version=1;server=$Server;port=$Port;account=$account;invite=$token}|ConvertTo-Json -Compress))} finally {$writer.Dispose()}
 } finally {$archive.Dispose();$token=$null}
 $digest=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
 "$digest  $account.zip"|Set-Content -LiteralPath ($destination+'.sha256') -Encoding ascii
 Write-Output "Prepared personal package for $account. Share only with that recipient."
}
Write-Output $OutputDirectory
