param([Parameter(Mandatory = $true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Directory)
if (!(Test-Path -LiteralPath $target -PathType Container)) { throw 'TLS directory does not exist.' }
# Reject redirects before applying permissions to any path. This helper only
# handles the small, flat certificate directory and never follows a junction.
$parent = Get-Item -LiteralPath $target -Force
while ($null -ne $parent) {
    if ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'TLS paths cannot contain symbolic links or junctions.' }
    $parent = $parent.Parent
}
$entries = @(Get-ChildItem -LiteralPath $target -Force)
foreach ($entry in $entries) {
    if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The TLS directory must contain only regular certificate and metadata files.'
    }
}
$current = [Security.Principal.WindowsIdentity]::GetCurrent().User
$identities = @($current, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
$directoryAcl = [Security.AccessControl.DirectorySecurity]::new()
$directoryAcl.SetAccessRuleProtection($true, $false)
$directoryAcl.SetOwner($current)
foreach ($identity in $identities) {
    $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit', [Security.AccessControl.PropagationFlags]::None,
        [Security.AccessControl.AccessControlType]::Allow)
    $directoryAcl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $target -AclObject $directoryAcl
foreach ($entry in $entries) {
    $fileAcl = [Security.AccessControl.FileSecurity]::new()
    $fileAcl.SetAccessRuleProtection($true, $false)
    $fileAcl.SetOwner($current)
    foreach ($identity in $identities) {
        $fileAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,
            [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow))
    }
    Set-Acl -LiteralPath $entry.FullName -AclObject $fileAcl
}
Write-Output 'TLS_PRIVATE_ACL_READY'
