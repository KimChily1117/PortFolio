param([string]$LogPath)
$lines = Get-Content $LogPath
$startLine = $lines | Where-Object { $_ -match 'C_CreateRoomHandler!!!' } | Select-Object -First 1
$endLine = $lines | Where-Object { $_ -match '\[TRANSFER\] Enter pending room' } | Select-Object -Last 1
if (-not $startLine -or -not $endLine) {
    "DurationMs=NA StartFound=$([bool]$startLine) EndFound=$([bool]$endLine)"
    exit 0
}
$start = [DateTime]::Parse(($startLine -split ' ', 2)[0])
$end = [DateTime]::Parse(($endLine -split ' ', 2)[0])
"DurationMs=$([int][Math]::Round(($end-$start).TotalMilliseconds)) Start=$($start.ToString('O')) End=$($end.ToString('O'))"