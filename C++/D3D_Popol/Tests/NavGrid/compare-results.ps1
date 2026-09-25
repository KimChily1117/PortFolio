param(
    [string] $CppResult = (Join-Path $PSScriptRoot 'Generated\cpp-result.json'),
    [string] $CSharpResult = (Join-Path $PSScriptRoot 'Generated\csharp-result.json')
)

$ErrorActionPreference = 'Stop'

$cppBytes = [System.IO.File]::ReadAllBytes($CppResult)
$csharpBytes = [System.IO.File]::ReadAllBytes($CSharpResult)

if ($cppBytes.Length -ne $csharpBytes.Length) {
    throw "Result sizes differ: C++=$($cppBytes.Length), C#=$($csharpBytes.Length)"
}

for ($index = 0; $index -lt $cppBytes.Length; ++$index) {
    if ($cppBytes[$index] -ne $csharpBytes[$index]) {
        throw "Results differ at byte offset $index."
    }
}

$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hash = $sha256.ComputeHash($cppBytes)
$hex = ([BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
Write-Output "Cross-language result JSON is byte-identical."
Write-Output "Result SHA-256: $hex"
