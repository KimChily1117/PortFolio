param([Parameter(Mandatory=$true)][string]$Run)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $Run).Path
$flows=@();$receipts=@();$net=@();$ordered=$true;$images=$true
foreach($i in 1..4){
 $dir=Join-Path $root "p$i"
 $flows+=Get-Content -LiteralPath "$dir/flow-result.json" -Raw|ConvertFrom-Json
 $receipts+=Get-Content -LiteralPath "$dir/reward-receipt.json" -Raw|ConvertFrom-Json
 $net+=Get-Content -LiteralPath "$dir/network-result.json" -Raw|ConvertFrom-Json
 $audit=Get-Content -LiteralPath "$dir/flow-audit.txt" -Raw
 $ordered=$ordered -and $audit.IndexOf('Rewards Pending') -ge 0 -and $audit.IndexOf('Rewards Saved') -gt $audit.IndexOf('Rewards Pending')
 $images=$images -and (Get-Item -LiteralPath "$dir/sql-saved-rewards.png").Length -gt 20000
}
$encounter=[long]$receipts[0].encounter
$runInfo=Get-Content -LiteralPath "$root/processes.json" -Raw|ConvertFrom-Json
if($runInfo.dbProvider -eq 'MySql'){
 . (Join-Path $PSScriptRoot 'Use-MySqlEnvironment.ps1')
 try{
  & dotnet (Join-Path (Split-Path $PSScriptRoot -Parent) 'MySqlTools/bin/Debug/net10.0/MySqlTools.dll') receipt "$root/mysql-owned-items.json" $encounter
  if($LASTEXITCODE -ne 0){throw 'MySQL receipt query failed'}
  $dbRows=@(Get-Content -LiteralPath "$root/mysql-owned-items.json" -Raw|ConvertFrom-Json -AsHashtable)
 }finally{Remove-Item Env:PROJECT_DAWN_DB_CONNECTION_STRING -ErrorAction SilentlyContinue}
}else{
$cs=$env:PROJECT_DAWN_DB_CONNECTION_STRING
if(-not $cs){$cs=(Get-Content -LiteralPath 'E:/task/Server/Server/Server/bin/Debug/netcoreapp3.1/config.json' -Raw|ConvertFrom-Json).connectionString}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new($cs)
$dbRows=@()
try{
 $connection.Open();$cmd=$connection.CreateCommand()
 $null=$cmd.Parameters.Add('@enc',[System.Data.SqlDbType]::BigInt);$cmd.Parameters['@enc'].Value=$encounter
 $cmd.CommandText='SELECT g.PlayerDbId,g.Amount,g.EquipmentTemplate,g.EquipmentItemDbId,i.OwnerDbId,i.TemplateId,i.Count FROM dbo.RaidRewardGrant g JOIN dbo.Item i ON i.ItemDbId=g.EquipmentItemDbId WHERE g.Encounter=@enc'
 $reader=$cmd.ExecuteReader()
 while($reader.Read()){$dbRows+=@{character=$reader.GetInt32(0);amount=$reader.GetInt32(1);template=$reader.GetInt32(2);item=$reader.GetInt32(3);owner=$reader.GetInt32(4);itemTemplate=$reader.GetInt32(5);count=$reader.GetInt32(6)}}
 $reader.Close()
}finally{$connection.Dispose()}
}
$canonical=@($receipts|ForEach-Object{($_.entries|Sort-Object playerDbId|ConvertTo-Json -Depth 5 -Compress)})
$checks=[ordered]@{
 four_clients_returned=(@($flows|Where-Object{-not $_.completed -or $_.stage -ne 'Town'}).Count -eq 0)
 no_unity_errors=(@($flows|Where-Object{$_.errors -ne 0}).Count -eq 0)
 four_slots=((@($net.slot|Sort-Object)-join ',') -eq '0,1,2,3')
 same_receipts=(@($canonical|Select-Object -Unique).Count -eq 1)
 same_encounter=(@($receipts.encounter|Select-Object -Unique).Count -eq 1)
 pending_then_saved=$ordered
 actual_result_screenshots=$images
 eight_items_after_reward=(@($flows|Where-Object{$_.items -ne 8}).Count -eq 0)
 clear_end_reason=(@($net|Where-Object{$_.endReason -ne 'Clear'}).Count -eq 0)
 sql_four_unique_owned_items=($dbRows.Count -eq 4 -and @($dbRows|ForEach-Object{$_['item']}|Select-Object -Unique).Count -eq 4)
 sql_owner_template_amount_match=($dbRows.Count -eq 4 -and @($dbRows|Where-Object{$_.character -ne $_.owner -or $_.template -ne $_.itemTemplate -or $_.count -ne 1 -or $_.amount -ne 100}).Count -eq 0)
 appearance_unchanged=(@($flows|Where-Object{$_.raidLook -ne $_.returnedLook}).Count -eq 0)
}
$passed=@($checks.Values|Where-Object{-not $_}).Count -eq 0
@{passed=$passed;checks=$checks;encounter=$encounter.ToString();provider=$runInfo.dbProvider;delayMs=$runInfo.delayMs;scope='4 local Unity processes. Real SQL/attack/clear/grants. Test-only boss HP reduced at 15 sec; not normal-difficulty clear.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath "$root/reward-verification.json"
Get-Content -LiteralPath "$root/reward-verification.json"
if(-not $passed){throw 'Reward verification failed'}
