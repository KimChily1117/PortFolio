param([string]$DatabaseConfigPath='E:\task\Server\Server\Server\bin\Debug\netcoreapp3.1\config.json')
$ErrorActionPreference='Stop'
$connectionString=$env:PROJECT_DAWN_DB_CONNECTION_STRING
if(-not $connectionString){$connectionString=(Get-Content -LiteralPath $DatabaseConfigPath -Raw|ConvertFrom-Json).connectionString}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new($connectionString)
try {
 $connection.Open()
 $command=$connection.CreateCommand()
 $command.CommandText='SELECT COUNT_BIG(*) FROM dbo.Player'
 $before=$command.ExecuteScalar()
 $command.CommandText=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Server/Server/Migrations/20261018_RaidRewards.sql') -Raw
 $null=$command.ExecuteNonQuery()
 $command.CommandText='SELECT COUNT_BIG(*) FROM dbo.Player'
 $after=$command.ExecuteScalar()
 if($before -ne $after){throw 'Player count changed during schema verification.'}
 Write-Output "[RAID_REWARD_SCHEMA_READY] existingPlayers=$after; two additive receipt tables; no player rows rewritten"
} finally {$connection.Dispose()}
