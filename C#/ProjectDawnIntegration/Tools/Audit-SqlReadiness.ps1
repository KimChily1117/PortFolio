param(
 [string]$DatabaseConfigPath='E:/task/Server/Server/Server/bin/Debug/netcoreapp3.1/config.json',
 [string]$OutputDirectory='Artifacts/sql-readiness'
)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
if(-not [IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $project $OutputDirectory}
$null=New-Item -ItemType Directory -Force -Path $OutputDirectory
$cs=$env:PROJECT_DAWN_DB_CONNECTION_STRING
if(-not $cs){$cs=(Get-Content -LiteralPath $DatabaseConfigPath -Raw|ConvertFrom-Json).connectionString}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new($cs)
function Read-Rows([string]$Sql){
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=15;$cmd.CommandText=$Sql
 $reader=$cmd.ExecuteReader()
 try {while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};[pscustomobject]$row}}
 finally{$reader.Dispose();$cmd.Dispose()}
}
try{
 $connection.Open()
 $report=[ordered]@{capturedUtc=[DateTime]::UtcNow.ToString('o');scope='Read-only SQL Server metadata and aggregate integrity checks. No account names, connection strings or character records exported.'}
 $report.engine=@(Read-Rows @'
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) AS productVersion,
 CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) AS edition,
 d.compatibility_level,d.recovery_model_desc,d.is_read_committed_snapshot_on,d.snapshot_isolation_state_desc,
 d.is_auto_close_on,d.is_auto_shrink_on,d.page_verify_option_desc,
 IS_SRVROLEMEMBER('sysadmin') AS runtimeConnectionIsSysadmin,IS_MEMBER('db_owner') AS runtimeConnectionIsDbOwner
FROM sys.databases d WHERE d.database_id=DB_ID()
'@)
 $report.rowCounts=@(Read-Rows @'
SELECT t.name AS tableName,SUM(p.rows) AS [rowCount] FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id
WHERE p.index_id IN(0,1) AND SCHEMA_NAME(t.schema_id)='dbo' GROUP BY t.name ORDER BY t.name
'@)
 $report.indexes=@(Read-Rows @'
SELECT t.name AS tableName,i.name AS indexName,i.is_unique,i.has_filter,i.filter_definition,
 c.name AS columnName,ic.key_ordinal,ic.is_included_column
FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,i.name,ic.key_ordinal,ic.index_column_id
'@)
 $report.constraints=@(Read-Rows @'
SELECT OBJECT_NAME(parent_object_id) AS tableName,name AS constraintName,'CHECK' AS kind,is_disabled,is_not_trusted,definition FROM sys.check_constraints
UNION ALL SELECT OBJECT_NAME(parent_object_id),name,'FK',is_disabled,is_not_trusted,NULL FROM sys.foreign_keys
'@)
 $report.integrity=@(Read-Rows @'
SELECT 'duplicate_owned_slots' AS checkName,COUNT_BIG(*) AS violationCount FROM
 (SELECT OwnerDbId,Slot FROM dbo.Item WHERE OwnerDbId IS NOT NULL GROUP BY OwnerDbId,Slot HAVING COUNT_BIG(*)>1) x
UNION ALL SELECT 'invalid_item_count_or_slot',COUNT_BIG(*) FROM dbo.Item WHERE Count<=0 OR Slot<0
UNION ALL SELECT 'orphan_item_owner',COUNT_BIG(*) FROM dbo.Item i LEFT JOIN dbo.Player p ON p.PlayerDbId=i.OwnerDbId WHERE i.OwnerDbId IS NOT NULL AND p.PlayerDbId IS NULL
UNION ALL SELECT 'duplicate_reward_slots',COUNT_BIG(*) FROM
 (SELECT Encounter,Slot FROM dbo.RaidRewardGrant GROUP BY Encounter,Slot HAVING COUNT_BIG(*)>1) x
UNION ALL SELECT 'duplicate_reward_item_reference',COUNT_BIG(*) FROM
 (SELECT EquipmentItemDbId FROM dbo.RaidRewardGrant WHERE EquipmentItemDbId IS NOT NULL GROUP BY EquipmentItemDbId HAVING COUNT_BIG(*)>1) x
UNION ALL SELECT 'reward_item_mismatch',COUNT_BIG(*) FROM dbo.RaidRewardGrant g LEFT JOIN dbo.Item i ON i.ItemDbId=g.EquipmentItemDbId
 WHERE g.EquipmentTemplate<>0 AND (i.ItemDbId IS NULL OR i.OwnerDbId IS NULL OR i.OwnerDbId<>g.PlayerDbId OR i.TemplateId<>g.EquipmentTemplate)
'@)
 $report.backupMetadata=@(Read-Rows @'
SELECT HAS_DBACCESS('msdb') AS canAccessMsdb,
 HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','BACKUP DATABASE') AS canBackupDatabase,
 HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DATABASE STATE') AS canViewDatabaseState
'@)
 $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDirectory 'audit.json') -Encoding utf8
 Write-Output ('[SQL_AUDIT_SAVED] '+(Join-Path $OutputDirectory 'audit.json'))
 $report.engine|Format-List
 $report.integrity|Format-Table -AutoSize
}finally{$connection.Dispose()}
