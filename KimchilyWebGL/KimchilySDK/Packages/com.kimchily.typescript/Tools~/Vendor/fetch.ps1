param()
$ErrorActionPreference = 'Stop'
$vendorRoot = $PSScriptRoot
$packageRoot = [IO.Path]::GetFullPath((Join-Path $vendorRoot '../..'))
$pluginRoot = Join-Path $packageRoot 'Runtime/Plugins'
New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(
    @{id='Jint'; version='4.16.2'; framework='netstandard2.1'; archiveHash='46C99DFAA5BAA93D0418C7EE969023EC6E5910AA65CDAC4657D4D8986E9DB592'; dllHash='9140479D41924188B147563596FD8CE11D45924CC4180AE6ACB162C9766DF711'},
    @{id='Acornima'; version='1.7.0'; framework='netstandard2.1'; archiveHash='44C74D86BF468A71986380B787BF32FC6A38ECDC293396FD1056B0BB94DBF404'; dllHash='7A4E3E81699539161F8E8C8FD738C32FEACC1A0F34B69DFD90A4D1C33D57905B'},
    @{id='System.Runtime.CompilerServices.Unsafe'; version='6.0.0'; framework='netstandard2.0'; archiveHash='6C41B53E70E9EEE298CFF3A02CE5ACDD15B04125589BE0273F0566026720A762'; dllHash='01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9'}
)
$provenance = @()
foreach ($package in $packages) {
    $lowerId = $package.id.ToLowerInvariant()
    $url = "https://api.nuget.org/v3-flatcontainer/$lowerId/$($package.version)/$lowerId.$($package.version).nupkg"
    $archive = Join-Path $vendorRoot "$lowerId.$($package.version).nupkg"
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.archiveHash) { throw "Package SHA-256 mismatch: $($package.id)" }
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $entry = $zip.GetEntry("lib/$($package.framework)/$($package.id).dll")
        if ($null -eq $entry) { throw "Missing $($package.framework) DLL: $($package.id)" }
        $target = Join-Path $pluginRoot "$($package.id).dll"
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $package.dllHash) { throw "DLL SHA-256 mismatch: $($package.id)" }
        foreach ($file in $zip.Entries) {
            if ($file.FullName -match '(?i)(license|notice|nuspec)' -and $file.Length -gt 0) {
                $name = "$($package.id)-" + $file.FullName.Replace('/','_')
                [IO.Compression.ZipFileExtensions]::ExtractToFile($file, (Join-Path $vendorRoot $name), $true)
            }
        }
        $xml = $zip.GetEntry("lib/$($package.framework)/$($package.id).xml")
        if ($null -ne $xml) { [IO.Compression.ZipFileExtensions]::ExtractToFile($xml, (Join-Path $vendorRoot "$($package.id).xml"), $true) }
        $provenance += [ordered]@{ package=$package.id; version=$package.version; url=$url; framework=$package.framework; archiveSha256=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash; dllSha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash }
    } finally { $zip.Dispose() }
}
$provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $vendorRoot 'provenance.json') -Encoding UTF8
$notices = @(
    @{name='LICENSE-Jint.txt'; url='https://raw.githubusercontent.com/sebastienros/jint/v4.16.2/LICENSE.txt'},
    @{name='LICENSE-Acornima.txt'; url='https://raw.githubusercontent.com/adams85/acornima/v1.7.0/LICENSE'},
    @{name='NOTICE-Acornima.txt'; url='https://raw.githubusercontent.com/adams85/acornima/v1.7.0/NOTICE'}
)
foreach ($notice in $notices) {
    Invoke-WebRequest -UseBasicParsing -Uri $notice.url -OutFile (Join-Path $pluginRoot $notice.name)
}
Copy-Item -LiteralPath (Join-Path $vendorRoot 'System.Runtime.CompilerServices.Unsafe-LICENSE.TXT') -Destination (Join-Path $pluginRoot 'LICENSE-System.Runtime.CompilerServices.Unsafe.txt') -Force
Copy-Item -LiteralPath (Join-Path $vendorRoot 'System.Runtime.CompilerServices.Unsafe-THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $pluginRoot 'NOTICE-System.Runtime.CompilerServices.Unsafe.txt') -Force
