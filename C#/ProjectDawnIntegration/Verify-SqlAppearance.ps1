param([Parameter(Mandatory=$true)][string]$RunDirectory,[string]$DatabaseConfigPath='E:\task\Server\Server\Server\bin\Debug\netcoreapp3.1\config.json')
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $RunDirectory).Path
$connectionString=$env:PROJECT_DAWN_DB_CONNECTION_STRING
if(-not $connectionString){$connectionString=(Get-Content -LiteralPath $DatabaseConfigPath -Raw|ConvertFrom-Json).connectionString}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new($connectionString)
try {
 $connection.Open();$rows=@()
 foreach($i in 1..4){
  $result=Get-Content -LiteralPath (Join-Path $root "p$i/flow-result.json") -Raw|ConvertFrom-Json
  if(-not $result.account.StartsWith('PD_Flow_')){throw 'Verification is restricted to explicit flow test accounts.'}
  $command=$connection.CreateCommand()
  $command.CommandText='SELECT p.Appearance FROM dbo.Player p JOIN dbo.Account a ON a.AccountDbId=p.AccountDbId WHERE a.AccountName=@account AND p.PlayerName=@character'
  $null=$command.Parameters.AddWithValue('@account',$result.account)
  $null=$command.Parameters.AddWithValue('@character',$result.character)
  $look=$command.ExecuteScalar()
  $matches=($look -is [string] -and $look.Length -gt 0 -and $look -ceq $result.loadedLook -and $look -ceq $result.raidLook -and $look -ceq $result.returnedLook)
  $rows+=@{character=$result.character;matchesLoginRaidReturn=$matches;look=$look}
 }
 $report=@{passed=(@($rows|Where-Object {-not $_.matchesLoginRaidReturn}).Count -eq 0);characters=$rows}
 $report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'sql-appearance-verification.json')
 Write-Output "[SQL_APPEARANCE_COMPARISON] passed=$($report.passed) characters=$($rows.Count)"
 if(-not $report.passed){throw 'SQL appearance differs from rendered flow evidence.'}
}finally{$connection.Dispose()}
