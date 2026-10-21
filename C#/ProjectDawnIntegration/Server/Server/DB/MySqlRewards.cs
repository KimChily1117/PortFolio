using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Server.DB
{
    internal static class MySqlRewards
    {
        static void Add(DbCommand c,string name,object value){var p=c.CreateParameter();p.ParameterName=name;p.Value=value??DBNull.Value;c.Parameters.Add(p);}
        public static void Save(RaidRewardRecord record,string digest)
        {
            for(int attempt=0;;attempt++) {
                try { SaveTransaction(record,digest);return; }
#if NET10_0_OR_GREATER
                catch(MySql.Data.MySqlClient.MySqlException e) when((e.Number==1213||e.Number==1205)&&attempt<3) {
                    // Retry the entire rolled-back transaction, not an individual statement.
                    Thread.Sleep(25*(1<<attempt));
                }
#endif
                catch {throw;}
            }
        }
        static void SaveTransaction(RaidRewardRecord record,string digest)
        {
            using(var db=new AppDbContext()) {
                db.Database.OpenConnection();
                using(var tx=db.Database.BeginTransaction(IsolationLevel.ReadCommitted))
                using(var c=db.Database.GetDbConnection().CreateCommand()) {
                    c.Transaction=tx.GetDbTransaction();c.CommandTimeout=10;
                    Add(c,"@enc",record.Encounter);Add(c,"@digest",digest);Add(c,"@at",record.CompletedUtc);
                    // The unique primary key locks this receipt even if it did not exist yet.
                    // Duplicate requests wait for the winning transaction, then verify its digest.
                    c.CommandText="INSERT INTO raidrewardencounter(Encounter,Digest,CompletedUtc) VALUES(@enc,@digest,@at) ON DUPLICATE KEY UPDATE Encounter=Encounter";c.ExecuteNonQuery();
                    c.CommandText="SELECT Digest FROM raidrewardencounter WHERE Encounter=@enc FOR UPDATE";
                    if((string)c.ExecuteScalar()!=digest)throw new InvalidDataException("Reward receipt conflict");
                    c.CommandText="SELECT COUNT(*) FROM raidrewardgrant WHERE Encounter=@enc";
                    long count=Convert.ToInt64(c.ExecuteScalar());
                    if(count==record.Recipients.Count){tx.Commit();return;}
                    if(count!=0)throw new InvalidDataException("Incomplete reward receipt");
                    foreach(var r in record.Recipients.OrderBy(r=>r.CharacterId)) {
                        c.Parameters.Clear();Add(c,"@enc",record.Encounter);Add(c,"@char",r.CharacterId);Add(c,"@account",r.AccountId);
                        Add(c,"@slot",r.Slot);Add(c,"@name",r.Name);Add(c,"@eligible",r.Eligible);Add(c,"@amount",r.Amount);Add(c,"@reason",r.Reason);
                        Add(c,"@template",r.EquipmentTemplate);Add(c,"@equipmentName",r.EquipmentName??"");Add(c,"@icon",r.EquipmentIcon??"");
                        c.CommandText="SELECT PlayerDbId FROM player WHERE PlayerDbId=@char AND AccountDbId=@account FOR UPDATE";
                        if(c.ExecuteScalar()==null)throw new InvalidDataException("Reward owner mismatch");
                        c.CommandText="INSERT INTO raidrewardgrant(Encounter,PlayerDbId,Slot,Name,Eligible,Amount,Reason,EquipmentTemplate,EquipmentName,EquipmentIcon) VALUES(@enc,@char,@slot,@name,@eligible,@amount,@reason,@template,@equipmentName,@icon)";c.ExecuteNonQuery();
                        if(r.EquipmentTemplate!=0) {
                            c.CommandText="SELECT COALESCE(MAX(Slot)+1,0) FROM item WHERE OwnerDbId=@char";Add(c,"@bagSlot",Convert.ToInt32(c.ExecuteScalar()));
                            c.CommandText="INSERT INTO item(TemplateId,Count,Slot,Equipped,OwnerDbId) VALUES(@template,1,@bagSlot,0,@char)";c.ExecuteNonQuery();
                            c.CommandText="SELECT LAST_INSERT_ID()";Add(c,"@item",Convert.ToInt32(c.ExecuteScalar()));
                            c.CommandText="UPDATE raidrewardgrant SET EquipmentItemDbId=@item WHERE Encounter=@enc AND PlayerDbId=@char";c.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
        }
        public static S_RaidRewards History(int account,int character,long encounter)
        {
            using(var db=new AppDbContext()) {
                db.Database.OpenConnection();using(var c=db.Database.GetDbConnection().CreateCommand()) {
                    c.CommandTimeout=10;Add(c,"@account",account);Add(c,"@char",character);Add(c,"@enc",encounter);
                    c.CommandText="SELECT COALESCE(SUM(g.Amount),0) FROM raidrewardgrant g JOIN player p ON g.PlayerDbId=p.PlayerDbId WHERE p.AccountDbId=@account AND p.PlayerDbId=@char";
                    long total=Convert.ToInt64(c.ExecuteScalar());
                    c.CommandText="SELECT g.Encounter FROM raidrewardgrant g JOIN player p ON g.PlayerDbId=p.PlayerDbId WHERE p.AccountDbId=@account AND p.PlayerDbId=@char AND (@enc=0 OR g.Encounter=@enc) ORDER BY g.Encounter DESC LIMIT 1";
                    var found=c.ExecuteScalar();if(found==null)return null;
                    var result=new S_RaidRewards{Encounter=Convert.ToInt64(found),Status="History",TotalTokens=total};
                    c.Parameters.Clear();Add(c,"@enc",result.Encounter);
                    c.CommandText="SELECT Slot,PlayerDbId,Name,Eligible,Amount,Reason,EquipmentTemplate,EquipmentName,EquipmentIcon FROM raidrewardgrant WHERE Encounter=@enc ORDER BY Slot";
                    using(var r=c.ExecuteReader())while(r.Read())result.Entries.Add(new RaidReward{Slot=r.GetInt32(0),PlayerDbId=r.GetInt32(1),Name=r.GetString(2),Eligible=r.GetBoolean(3),Amount=r.GetInt32(4),Reason=r.GetString(5),EquipmentTemplate=r.GetInt32(6),EquipmentName=r.GetString(7),EquipmentIcon=r.GetString(8)});
                    return result;
                }
            }
        }
    }
}
