using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Server.DB;
using Microsoft.EntityFrameworkCore;

class Program
{
 static readonly string[] Tables={"Account","Player","Item","MatchHistory","MatchHistoryMember","RaidRewardEncounter","RaidRewardGrant"};
 static string Target=>Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING")??throw new Exception("Missing target connection");
 static void Add(DbCommand c,string n,object v){var p=c.CreateParameter();p.ParameterName=n;p.Value=v??DBNull.Value;c.Parameters.Add(p);}
 static DataTable Read(DbConnection c,string sql,System.Data.Common.DbTransaction tx=null){using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=30;using(var r=cmd.ExecuteReader()){var t=new DataTable();for(int i=0;i<r.FieldCount;i++)t.Columns.Add(r.GetName(i),r.GetFieldType(i));while(r.Read()){var values=new object[r.FieldCount];r.GetValues(values);t.Rows.Add(values);}return t;}}}
 static string Key(string t)=>t=="Account"?"AccountDbId":t=="Player"?"PlayerDbId":t=="Item"?"ItemDbId":t=="RaidRewardEncounter"?"Encounter":t=="RaidRewardGrant"?"Encounter,PlayerDbId":"Id";
 static string Hash(DataTable t){var rows=new List<string>();foreach(DataRow r in t.Rows){var cols=new List<string>();foreach(var x in r.ItemArray){if(x==DBNull.Value)cols.Add("null");else if(x is DateTime dt)cols.Add("date:"+(dt.Ticks/10));else if(x is bool b)cols.Add(b?"1":"0");else cols.Add(Convert.ToString(x,System.Globalization.CultureInfo.InvariantCulture));}rows.Add(JsonSerializer.Serialize(cols));}return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",rows))));}
 static void Migrate(string output)
 {
  var sourceCs=Environment.GetEnvironmentVariable("PROJECT_DAWN_SQLSERVER_SOURCE")??throw new Exception("Missing read-only source connection");
  using(var source=new SqlConnection(sourceCs))using(var target=new MySqlConnection(Target)) {
   source.Open();target.Open();
   var script=File.ReadAllText("Server/Server/Migrations/MySql/001_initial.sql");
   foreach(var statement in script.Split(';'))if(!string.IsNullOrWhiteSpace(statement)){using(var c=target.CreateCommand()){c.CommandText=statement;c.ExecuteNonQuery();}}
   foreach(var table in Tables)if(Convert.ToInt64(Read(target,$"SELECT COUNT(*) FROM `{table.ToLowerInvariant()}`").Rows[0][0])!=0)throw new Exception("Target must be empty; refusing overwrite");
   var reports=new List<object>();
   using(var st=source.BeginTransaction(IsolationLevel.Serializable))using(var tt=target.BeginTransaction(IsolationLevel.ReadCommitted)) {
    foreach(var table in Tables) {
     var data=Read(source,$"SELECT * FROM dbo.[{table}] WITH(HOLDLOCK) ORDER BY {Key(table)}",st);
     var cols=data.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray();
     foreach(DataRow row in data.Rows)using(var c=target.CreateCommand()) {
      c.Transaction=tt;c.CommandText=$"INSERT INTO `{table.ToLowerInvariant()}` ({string.Join(",",cols.Select(x=>"`"+x+"`"))}) VALUES({string.Join(",",cols.Select((x,i)=>"@p"+i))})";
      for(int i=0;i<cols.Length;i++){object value=row[i];if(value is DateTime dt)value=new DateTime(dt.Ticks/10*10,dt.Kind);Add(c,"@p"+i,value);}c.ExecuteNonQuery();
     }
     var copied=Read(target,$"SELECT {string.Join(",",cols.Select(x=>"`"+x+"`"))} FROM `{table.ToLowerInvariant()}` ORDER BY {Key(table)}",tt);
     string expected=Hash(data),actual=Hash(copied);
     if(expected!=actual||data.Rows.Count!=copied.Rows.Count)throw new Exception("Data verification failed: "+table);
     reports.Add(new {table,rows=data.Rows.Count,sha256=actual,equal=true});
    }
    tt.Commit();st.Commit();
   }
   File.WriteAllText(output,JsonSerializer.Serialize(new {passed=true,sourceReadOnly=true,datetimePrecision="100ns to microseconds; sub-microsecond digits truncated",tables=reports},new JsonSerializerOptions{WriteIndented=true}));
   Console.WriteLine("[MYSQL_MIGRATED] seven tables; source preserved; row counts and normalized content hashes match");
  }
 }
 static void Inspect(string output)
 {
  using(var c=new MySqlConnection(Target)){c.Open();var checks=new Dictionary<string,bool>();
   var settings=Read(c,"SELECT VERSION(),@@character_set_server,@@collation_server,@@session.time_zone,@@innodb_flush_log_at_trx_commit,@@sync_binlog");
   var grants=Read(c,"SHOW GRANTS FOR CURRENT_USER()").Rows.Cast<DataRow>().Select(x=>x[0].ToString()).ToArray();
   checks["runtime_no_global_admin_or_ddl"]=!grants.Any(x=>x.Contains("ALL PRIVILEGES")||x.Contains("CREATE")||x.Contains("GRANT OPTION"));
   checks["runtime_dml_grants"]=grants.Any(x=>x.Contains("SELECT")&&x.Contains("INSERT")&&x.Contains("UPDATE")&&x.Contains("DELETE"));
   checks["tls_active"]=Read(c,"SHOW SESSION STATUS LIKE 'Ssl_cipher'").Rows[0][1].ToString().Length>0;
   checks["all_tables_innodb"]=Convert.ToInt64(Read(c,"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND engine<>'InnoDB'").Rows[0][0])==0;
   checks["reward_owners_match"]=Convert.ToInt64(Read(c,"SELECT COUNT(*) FROM raidrewardgrant g LEFT JOIN item i ON i.ItemDbId=g.EquipmentItemDbId WHERE g.EquipmentTemplate<>0 AND (i.ItemDbId IS NULL OR i.OwnerDbId IS NULL OR i.OwnerDbId<>g.PlayerDbId OR i.TemplateId<>g.EquipmentTemplate)").Rows[0][0])==0;
   checks["durable_commit"]=Convert.ToInt32(settings.Rows[0][4])==1&&Convert.ToInt32(settings.Rows[0][5])==1;
   checks["utf8mb4"]=settings.Rows[0][1].ToString()=="utf8mb4";
   var plans=new Dictionary<string,string>();
   foreach(var q in new[]{("inventory","SELECT ItemDbId,Slot FROM item WHERE OwnerDbId=1 ORDER BY Slot"),("rewardHistory","SELECT Encounter,Amount FROM raidrewardgrant WHERE PlayerDbId=1 ORDER BY Encounter DESC LIMIT 20")})
    plans[q.Item1]=Read(c,"EXPLAIN FORMAT=JSON "+q.Item2).Rows[0][0].ToString();
   DatabaseStartup.Verify(false);
   checks["ef_login_inventory_schema"]=true;
   File.WriteAllText(output,JsonSerializer.Serialize(new{passed=checks.Values.All(x=>x),version=settings.Rows[0][0].ToString(),checks,queryPlans=plans},new JsonSerializerOptions{WriteIndented=true}));
   if(checks.Values.Any(x=>!x))throw new Exception("MySQL readiness failed");
   Console.WriteLine("[MYSQL_READY_VALIDATED] "+checks.Count+" checks");
  }
 }
 static void ConcurrentRewards(string output)
 {
  var recipients=new List<RaidRewardRecipient>();
  using(var db=new AppDbContext())for(int i=0;i<4;i++) {
   string name="PD_MysqlDb_"+DateTime.UtcNow.Ticks+"_"+i;
   var a=new AccountDb{AccountName=name};db.Accounts.Add(a);db.SaveChanges();
   var p=new PlayerDb{PlayerName=name,AccountDbId=a.AccountDbId,Level=1,Hp=100,MaxHp=100,Attack=100,Speed=6};db.Players.Add(p);db.SaveChanges();
   recipients.Add(new RaidRewardRecipient{Slot=i,AccountId=a.AccountDbId,CharacterId=p.PlayerDbId,Name=name,Eligible=true,Amount=100,Reason="MySql concurrency fixture",EquipmentTemplate=92001,EquipmentName="근원을 삼킨 도",EquipmentIcon="DawnEquipment/official-origin-katana"});
  }
  var record=new RaidRewardRecord{Encounter=DateTime.UtcNow.Ticks,CompletedUtc=DateTime.UtcNow,Recipients=recipients};
  System.Threading.Tasks.Parallel.For(0,16,_=>RaidRewardStore.Save(record));
  var checks=new Dictionary<string,bool>();
  using(var db=new AppDbContext()) {
   var ids=recipients.Select(x=>x.CharacterId).ToArray();
   checks["sixteen_first_writers_one_item_each"]=db.Items.Count(x=>ids.Contains(x.OwnerDbId.Value))==4;
   checks["sixteen_first_writers_one_token_grant"]=recipients.All(x=>RaidRewardStore.History(x.AccountId,x.CharacterId).TotalTokens==100);
  }
  var records=Enumerable.Range(1,8).Select(i=>new RaidRewardRecord{Encounter=record.Encounter+i,CompletedUtc=record.CompletedUtc,Recipients=recipients}).ToArray();
  System.Threading.Tasks.Parallel.ForEach(records,r=>RaidRewardStore.Save(r));
  using(var db=new AppDbContext())foreach(var r in recipients){var slots=db.Items.Where(i=>i.OwnerDbId==r.CharacterId).Select(i=>i.Slot).ToArray();checks["concurrent_encounters_unique_slots_"+r.Slot]=slots.Length==9&&slots.Distinct().Count()==9;}
  System.Threading.Tasks.Parallel.ForEach(records,r=>RaidRewardStore.Save(r));
  checks["replay_all_encounters_no_double_tokens"]=recipients.All(x=>RaidRewardStore.History(x.AccountId,x.CharacterId).TotalTokens==900);
  File.WriteAllText(output,JsonSerializer.Serialize(new{passed=checks.Values.All(x=>x),checks},new JsonSerializerOptions{WriteIndented=true}));
  if(checks.Values.Any(x=>!x))throw new Exception("Concurrent reward validation failed");Console.WriteLine("[MYSQL_CONCURRENCY_VALIDATED] "+checks.Count+" checks");
 }
 static void Fingerprint(string output)
 {
  using(var c=new MySqlConnection(Target)){c.Open();var result=new Dictionary<string,object>();foreach(var t in Tables){var d=Read(c,$"SELECT * FROM `{t.ToLowerInvariant()}` ORDER BY {Key(t)}");result[t]=new {rows=d.Rows.Count,sha256=Hash(d)};}File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));}
 }
 static void RepairLegacyHistory(string output)
 {
  // Restore only imported NULL-run rows affected by the pre-fix playtest.
  using(var s=new SqlConnection(Environment.GetEnvironmentVariable("PROJECT_DAWN_SQLSERVER_SOURCE")))using(var t=new MySqlConnection(Target)) {
   s.Open();t.Open();var original=Read(s,"SELECT * FROM dbo.MatchHistory ORDER BY Id");
   using(var tx=t.BeginTransaction()) {
    foreach(DataRow row in original.Rows)using(var c=t.CreateCommand()){
     c.Transaction=tx;var cols=original.Columns.Cast<DataColumn>().Select(x=>x.ColumnName).Where(x=>x!="Id").ToArray();
     c.CommandText="UPDATE matchhistory SET "+string.Join(",",cols.Select((x,i)=>"`"+x+"`=@p"+i))+" WHERE Id=@id AND ServerRunId IS NULL";
     Add(c,"@id",row["Id"]);for(int i=0;i<cols.Length;i++){object value=row[cols[i]];if(value is DateTime d)value=new DateTime(d.Ticks/10*10);Add(c,"@p"+i,value);}c.ExecuteNonQuery();
    }
    var copied=Read(t,"SELECT "+string.Join(",",original.Columns.Cast<DataColumn>().Select(x=>"`"+x.ColumnName+"`"))+" FROM matchhistory WHERE ServerRunId IS NULL ORDER BY Id",tx);
    if(Hash(original)!=Hash(copied))throw new Exception("Legacy history baseline mismatch");tx.Commit();
    File.WriteAllText(output,JsonSerializer.Serialize(new{passed=true,rows=original.Rows.Count,baselineRestored=true,newRunsUntouched=true}));
   }
  }
 }
 static void Receipt(string output,long encounter)
 {
  using(var c=new MySqlConnection(Target)){c.Open();using(var cmd=c.CreateCommand()){
   cmd.CommandText="SELECT g.PlayerDbId AS `character`,g.Amount AS amount,g.EquipmentTemplate AS template,g.EquipmentItemDbId AS item,i.OwnerDbId AS owner,i.TemplateId AS itemTemplate,i.Count AS count FROM raidrewardgrant g JOIN item i ON i.ItemDbId=g.EquipmentItemDbId WHERE g.Encounter=@enc";Add(cmd,"@enc",encounter);
   var rows=new List<Dictionary<string,object>>();using(var r=cmd.ExecuteReader())while(r.Read()){var row=new Dictionary<string,object>();for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.GetValue(i);rows.Add(row);}File.WriteAllText(output,JsonSerializer.Serialize(rows));
  }}
 }
 static int Main(string[] args){try{switch(args[0]){case "migrate":Migrate(args[1]);break;case "concurrency":ConcurrentRewards(args[1]);break;case "fingerprint":Fingerprint(args[1]);break;case "repair-imported-history":RepairLegacyHistory(args[1]);break;case "receipt":Receipt(args[1],long.Parse(args[2]));break;default:Inspect(args[1]);break;}return 0;}catch(Exception e){Console.Error.WriteLine("[MYSQL_TOOL_FAILED] "+e.GetType().Name+" "+(e is MySqlException m?m.Number.ToString():e is SqlException s?s.Number.ToString():e.Message));return 1;}}
}
