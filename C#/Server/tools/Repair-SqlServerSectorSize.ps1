[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$ReportDirectory = (Join-Path $env:LOCALAPPDATA 'Temp\project-dawn-fix-20261001\sql-repair')
)

$ErrorActionPreference = 'Stop'
$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device'
$registryNativePath = 'HKLM\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device'
$valueName = 'ForcedPhysicalSectorSizeInBytes'
$resultPath = Join-Path $ReportDirectory 'repair-result.json'
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null

function Save-Result([string]$Status, [bool]$RebootRequired, [string]$Detail) {
    [ordered]@{
        status = $Status
        rebootRequired = $RebootRequired
        detail = $Detail
        occurredAt = (Get-Date).ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Output "$Status`: $Detail"
}

try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $systemDriveLetter = $env:SystemDrive.TrimEnd(':')
    $systemDisk = Get-Partition -DriveLetter $systemDriveLetter | Get-Disk
    $systemDisk | Select-Object Number, BusType, LogicalSectorSize, PhysicalSectorSize |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReportDirectory 'disk.json') -Encoding UTF8

    if (!$Apply) {
        Save-Result 'Diagnosed' $false "System disk: $($systemDisk.BusType), physical sector: $($systemDisk.PhysicalSectorSize) bytes. No registry changes made."
        exit 0
    }
    if (!$isAdmin) {
        throw 'Apply requires an administrator PowerShell process.'
    }
    if ($systemDisk.BusType -ne 'NVMe') {
        throw 'The Microsoft stornvme workaround is limited here to an NVMe system disk. Review this storage driver manually.'
    }

    $sectorInfo = & fsutil.exe fsinfo sectorinfo $env:SystemDrive 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'fsutil could not verify the system volume sector size.' }
    $sectorInfo | Set-Content -LiteralPath (Join-Path $ReportDirectory 'sectorinfo-before.txt') -Encoding UTF8
    $sectorSizes = @($sectorInfo | ForEach-Object {
        if ($_ -match '^\s*PhysicalBytesPerSectorFor(?:Atomicity|Performance)\s*:\s*(\d+)') {
            [long]$Matches[1]
        }
    })
    if ($sectorSizes.Count -ne 2) { throw 'Unexpected fsutil output. Registry changes were skipped.' }
    $largestSector = ($sectorSizes | Measure-Object -Maximum).Maximum
    if ($largestSector -le 4096) {
        Save-Result 'AlreadyCompatible' $false 'Volume already reports a SQL Server compatible sector size. Registry changes were skipped.'
        exit 0
    }

    $keyExists = Test-Path -LiteralPath $registryPath
    $valueExists = $false
    $oldValue = @()
    if ($keyExists) {
        $registryKey = Get-Item -LiteralPath $registryPath
        $valueExists = $registryKey.GetValueNames() -contains $valueName
        if ($valueExists) { $oldValue = @($registryKey.GetValue($valueName)) }
    }
    if ($valueExists) {
        if ($oldValue.Count -eq 1 -and $oldValue[0] -eq '* 4095') {
            Save-Result 'AlreadyConfigured' $true 'The 4 KB workaround is present, but the volume still reports a larger sector. Reboot before retrying SQL Server.'
            exit 0
        }
        throw 'An existing sector-size override needs manual review. It was not overwritten.'
    }

    $backupDirectory = Join-Path $ReportDirectory ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backupDirectory | Out-Null
    [ordered]@{ keyExists = $keyExists; valueExists = $valueExists; value = $oldValue } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupDirectory 'previous-value.json') -Encoding UTF8
    if ($keyExists) {
        & reg.exe export $registryNativePath (Join-Path $backupDirectory 'device-before.reg') /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Registry backup failed. No changes were applied.' }
    } else {
        New-Item -Path $registryPath -Force | Out-Null
    }

    New-ItemProperty -LiteralPath $registryPath -Name $valueName -PropertyType MultiString -Value @('* 4095') -Force | Out-Null
    $applied = (Get-Item -LiteralPath $registryPath).GetValue($valueName)
    if (@($applied).Count -ne 1 -or $applied[0] -ne '* 4095') { throw 'Registry verification failed.' }
    Save-Result 'Applied' $true "Applied the Microsoft 4 KB sector workaround. Backup: $backupDirectory. Save your work and reboot Windows manually."
} catch {
    Save-Result 'Failed' $false $_.Exception.Message
    exit 1
}
