[CmdletBinding()]
param(
    [ValidateSet('Encrypt','Decrypt')][string]$Mode = 'Decrypt',
    [string]$InputPath = (Join-Path $PSScriptRoot 'profiles\Kimchily-RemotePC1.encrypted.json'),
    [string]$OutputPath = (Join-Path $env:USERPROFILE 'Downloads\Kimchily-RemotePC1.conf'),
    [Security.SecureString]$Password,
    [switch]$Force
)

# Portable to Windows PowerShell 5.1: no Python, module installation or Windows
# account-bound DPAPI key is needed on the second laptop. The default output is
# outside this Git checkout. The password is read without echoing it.
$ErrorActionPreference = 'Stop'
if (!$Password) { $Password = Read-Host 'VPN package password (provided separately, not in Git)' -AsSecureString }
$taskInput = [IO.Path]::GetFullPath($InputPath)
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
if ($taskInput -eq $taskOutput) { throw 'Input and output paths must differ.' }
if ((Test-Path -LiteralPath $taskOutput) -and !$Force) { throw 'Output exists. Choose another OutputPath or explicitly use -Force.' }
if ((Get-Item -LiteralPath $taskInput).Length -gt 1048576) { throw 'Unexpectedly large VPN package.' }

function New-RandomBytes([int]$Count) {
    $taskBytes = [byte[]]::new($Count)
    $taskRng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $taskRng.GetBytes($taskBytes) } finally { $taskRng.Dispose() }
    return ,$taskBytes
}

function Get-AuthenticationBytes($Package) {
    # Authenticate metadata and ciphertext, including the KDF parameters.
    $taskFields = @($Package.format, $Package.kdf, [string]$Package.iterations,
        $Package.cipher, $Package.mac, $Package.salt, $Package.iv, $Package.ciphertext)
    return ,[Text.Encoding]::UTF8.GetBytes(($taskFields -join "`n"))
}

$taskPasswordPointer = [IntPtr]::Zero
$taskPasswordBytes = $null
$taskDerived = $null
$taskPlaintext = $null
$taskAes = $null
$taskHmac = $null
$taskKdf = $null
try {
    $taskPasswordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    $taskPasswordBytes = [Text.Encoding]::UTF8.GetBytes([Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPasswordPointer))
    if ($Mode -eq 'Encrypt') {
        # Random IV/salt, independent encryption and authentication keys.
        $taskPackage = [ordered]@{
            format='kimchily-vpn-v1'; kdf='PBKDF2-SHA256'; iterations=600000
            cipher='AES-256-CBC'; mac='HMAC-SHA256'
            salt=[Convert]::ToBase64String((New-RandomBytes 32))
            iv=[Convert]::ToBase64String((New-RandomBytes 16))
            ciphertext=''; tag=''
        }
    } else {
        $taskPackage = Get-Content -LiteralPath $taskInput -Raw | ConvertFrom-Json
        if ($taskPackage.format -ne 'kimchily-vpn-v1' -or $taskPackage.kdf -ne 'PBKDF2-SHA256' -or
            $taskPackage.iterations -ne 600000 -or $taskPackage.cipher -ne 'AES-256-CBC' -or $taskPackage.mac -ne 'HMAC-SHA256') {
            throw 'Unsupported VPN package format.'
        }
    }
    $taskSalt = [Convert]::FromBase64String($taskPackage.salt)
    $taskIv = [Convert]::FromBase64String($taskPackage.iv)
    if ($taskSalt.Length -ne 32 -or $taskIv.Length -ne 16) { throw 'Invalid VPN package metadata.' }
    $taskKdf = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $taskPasswordBytes, $taskSalt, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256)
    $taskDerived = $taskKdf.GetBytes(64)
    $taskAes = [Security.Cryptography.Aes]::Create()
    $taskAes.KeySize = 256
    $taskAes.Mode = [Security.Cryptography.CipherMode]::CBC
    $taskAes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $taskAes.Key = [byte[]]$taskDerived[0..31]
    $taskAes.IV = $taskIv
    $taskHmac = [Security.Cryptography.HMACSHA256]::new([byte[]]$taskDerived[32..63])

    if ($Mode -eq 'Encrypt') {
        $taskPlaintext = [IO.File]::ReadAllBytes($taskInput)
        $taskTransform = $taskAes.CreateEncryptor()
        try { $taskCiphertext = $taskTransform.TransformFinalBlock($taskPlaintext, 0, $taskPlaintext.Length) }
        finally { $taskTransform.Dispose() }
        $taskPackage.ciphertext = [Convert]::ToBase64String($taskCiphertext)
        $taskPackage.tag = [Convert]::ToBase64String($taskHmac.ComputeHash((Get-AuthenticationBytes $taskPackage)))
        $taskOutputBytes = [Text.Encoding]::UTF8.GetBytes(($taskPackage | ConvertTo-Json))
    } else {
        # Verify the MAC before decrypting or writing anything. A wrong password
        # or modified package never leaves a partial plaintext configuration.
        $taskExpectedTag = $taskHmac.ComputeHash((Get-AuthenticationBytes $taskPackage))
        $taskReceivedTag = [Convert]::FromBase64String($taskPackage.tag)
        if ($taskReceivedTag.Length -ne $taskExpectedTag.Length) { throw 'Wrong password or damaged VPN package.' }
        $taskDifference = 0
        for ($taskIndex = 0; $taskIndex -lt $taskExpectedTag.Length; $taskIndex++) {
            $taskDifference = $taskDifference -bor ($taskExpectedTag[$taskIndex] -bxor $taskReceivedTag[$taskIndex])
        }
        if ($taskDifference -ne 0) { throw 'Wrong password or damaged VPN package.' }
        $taskCiphertext = [Convert]::FromBase64String($taskPackage.ciphertext)
        $taskTransform = $taskAes.CreateDecryptor()
        try { $taskPlaintext = $taskTransform.TransformFinalBlock($taskCiphertext, 0, $taskCiphertext.Length) }
        finally { $taskTransform.Dispose() }
        $taskProfileText = [Text.Encoding]::UTF8.GetString($taskPlaintext)
        if ($taskProfileText -notmatch '\[Interface\]' -or $taskProfileText -notmatch '(?m)^PrivateKey\s*=' -or $taskProfileText -notmatch '\[Peer\]') {
            throw 'Decrypted data is not a WireGuard profile.'
        }
        $taskOutputBytes = $taskPlaintext
    }
    New-Item -ItemType Directory -Path (Split-Path $taskOutput) -Force | Out-Null
    $taskFileMode = if ($Force) { [IO.FileMode]::Create } else { [IO.FileMode]::CreateNew }
    $taskStream = [IO.File]::Open($taskOutput, $taskFileMode, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $taskStream.Write($taskOutputBytes, 0, $taskOutputBytes.Length) } finally { $taskStream.Dispose() }
    Write-Output "$Mode completed: $taskOutput"
    if ($Mode -eq 'Decrypt') { Write-Output 'Import this file in WireGuard, then Activate. Do not commit the decrypted file.' }
} finally {
    if ($taskPasswordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPasswordPointer) }
    if ($taskPasswordBytes) { [Array]::Clear($taskPasswordBytes, 0, $taskPasswordBytes.Length) }
    if ($taskDerived) { [Array]::Clear($taskDerived, 0, $taskDerived.Length) }
    if ($taskPlaintext) { [Array]::Clear($taskPlaintext, 0, $taskPlaintext.Length) }
    if ($taskKdf) { $taskKdf.Dispose() }
    if ($taskAes) { $taskAes.Dispose() }
    if ($taskHmac) { $taskHmac.Dispose() }
}
