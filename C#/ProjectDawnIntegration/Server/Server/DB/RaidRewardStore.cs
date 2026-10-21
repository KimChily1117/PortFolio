using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using Server.Data;

namespace Server.DB
{
    public sealed class RaidRewardRecord
    {
        public long Encounter;
        public DateTime CompletedUtc;
        public List<RaidRewardRecipient> Recipients=new List<RaidRewardRecipient>();
    }
    public sealed class RaidRewardRecipient
    {
        public int Slot, AccountId, CharacterId, Amount;
        // Ignore zero when hashing older journal receipts; their original digest must remain replayable.
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public int EquipmentTemplate;
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public string EquipmentName;
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public string EquipmentIcon;
        public string Name, Reason;
        public bool Eligible;
    }
    public static class RaidRewardStore
    {
        public const int ClearTokens=100;
        public static int RollEquipment()=>92001+RandomNumberGenerator.GetInt32(5);
        public static S_RaidRewards Packet(RaidRewardRecord record,string status,long total=-1)
        {
            var packet=new S_RaidRewards{Encounter=record.Encounter,Status=status,TotalTokens=total};
            packet.Entries.AddRange(record.Recipients.OrderBy(r=>r.Slot).Select(r=>new RaidReward{Slot=r.Slot,PlayerDbId=r.CharacterId,Name=r.Name,Eligible=r.Eligible,Amount=r.Amount,Reason=r.Reason,EquipmentTemplate=r.EquipmentTemplate,EquipmentName=r.EquipmentName??"",EquipmentIcon=r.EquipmentIcon??""}));
            return packet;
        }
        // The receipt is the balance source: no separate wallet increment can be repeated after an ambiguous commit.
        public static void Save(RaidRewardRecord record)
        {
            if(record.Encounter<=0 || record.Recipients.Count<1 || record.Recipients.Count>4 || record.Recipients.Select(r=>r.Slot).Distinct().Count()!=record.Recipients.Count ||
                record.Recipients.Select(r=>r.CharacterId).Distinct().Count()!=record.Recipients.Count || record.Recipients.Any(r=>r.CharacterId<=0||r.AccountId<=0||r.Slot<0||r.Slot>=record.Recipients.Count||r.Amount!=(r.Eligible?ClearTokens:0)))throw new InvalidDataException("Invalid server reward record");
            if(record.Recipients.Any(r=>r.EquipmentTemplate!=0&&(!r.Eligible||r.EquipmentTemplate<92001||r.EquipmentTemplate>92005)))throw new InvalidDataException("Invalid reward equipment");
            string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record)))).Replace("-","");
            if(DatabaseSettings.IsMySql){MySqlRewards.Save(record,digest);return;}
            using(var db=new AppDbContext())
            {
                db.Database.OpenConnection();using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var connection=db.Database.GetDbConnection();
                    using(var cmd=connection.CreateCommand())
                    {
                        cmd.Transaction=tx.GetDbTransaction();cmd.CommandTimeout=10;
                        Add(cmd,"@enc",record.Encounter);Add(cmd,"@digest",digest);Add(cmd,"@at",record.CompletedUtc);
                        cmd.CommandText="SELECT Digest FROM dbo.RaidRewardEncounter WITH (UPDLOCK,HOLDLOCK) WHERE Encounter=@enc";
                        var existing=cmd.ExecuteScalar();
                        if(existing!=null){if((string)existing!=digest)throw new InvalidDataException("Reward receipt conflict");tx.Commit();return;}
                        cmd.CommandText="INSERT dbo.RaidRewardEncounter(Encounter,Digest,CompletedUtc) VALUES(@enc,@digest,@at)";cmd.ExecuteNonQuery();
                        foreach(var r in record.Recipients)
                        {
                            cmd.Parameters.Clear();Add(cmd,"@enc",record.Encounter);Add(cmd,"@char",r.CharacterId);Add(cmd,"@account",r.AccountId);
                            Add(cmd,"@slot",r.Slot);Add(cmd,"@name",r.Name);Add(cmd,"@eligible",r.Eligible);Add(cmd,"@amount",r.Amount);Add(cmd,"@reason",r.Reason);
                            Add(cmd,"@template",r.EquipmentTemplate);Add(cmd,"@equipmentName",r.EquipmentName??"");Add(cmd,"@icon",r.EquipmentIcon??"");
                            cmd.CommandText="INSERT dbo.RaidRewardGrant(Encounter,PlayerDbId,Slot,Name,Eligible,Amount,Reason,EquipmentTemplate,EquipmentName,EquipmentIcon) SELECT @enc,PlayerDbId,@slot,@name,@eligible,@amount,@reason,@template,@equipmentName,@icon FROM dbo.Player WITH(UPDLOCK,HOLDLOCK) WHERE PlayerDbId=@char AND AccountDbId=@account";
                            if(cmd.ExecuteNonQuery()!=1)throw new InvalidDataException("Reward owner mismatch");
                            if(r.EquipmentTemplate!=0){
                                // Receipt and owned item commit together. Replaying a committed encounter returns above.
                                cmd.CommandText="INSERT dbo.Item(TemplateId,Count,Slot,Equipped,OwnerDbId) OUTPUT INSERTED.ItemDbId SELECT @template,1,COALESCE(MAX(Slot)+1,0),0,@char FROM dbo.Item WITH(UPDLOCK,HOLDLOCK) WHERE OwnerDbId=@char";
                                int item=Convert.ToInt32(cmd.ExecuteScalar());Add(cmd,"@item",item);
                                cmd.CommandText="UPDATE dbo.RaidRewardGrant SET EquipmentItemDbId=@item WHERE Encounter=@enc AND PlayerDbId=@char";cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                }
            }
        }
        private static void Add(System.Data.Common.DbCommand cmd,string name,object value){var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);}
        public static S_RaidRewards History(int account,int character,long encounter=0)
        {
            if(DatabaseSettings.IsMySql)return MySqlRewards.History(account,character,encounter);
            using(var db=new AppDbContext()){
                db.Database.OpenConnection();using(var cmd=db.Database.GetDbConnection().CreateCommand()){
                    cmd.CommandTimeout=10;Add(cmd,"@account",account);Add(cmd,"@char",character);Add(cmd,"@enc",encounter);
                    cmd.CommandText="SELECT COALESCE(SUM(CONVERT(bigint,g.Amount)),0) FROM dbo.RaidRewardGrant g JOIN dbo.Player p ON g.PlayerDbId=p.PlayerDbId WHERE p.AccountDbId=@account AND p.PlayerDbId=@char";
                    long total=Convert.ToInt64(cmd.ExecuteScalar());
                    cmd.CommandText="SELECT TOP(1) g.Encounter FROM dbo.RaidRewardGrant g JOIN dbo.Player p ON g.PlayerDbId=p.PlayerDbId WHERE p.AccountDbId=@account AND p.PlayerDbId=@char AND (@enc=0 OR g.Encounter=@enc) ORDER BY g.Encounter DESC";
                    object found=cmd.ExecuteScalar();if(found==null)return null;
                    var result=new S_RaidRewards{Encounter=(long)found,Status="History",TotalTokens=total};
                    cmd.Parameters.Clear();Add(cmd,"@enc",result.Encounter);
                    cmd.CommandText="SELECT Slot,PlayerDbId,Name,Eligible,Amount,Reason,EquipmentTemplate,EquipmentName,EquipmentIcon FROM dbo.RaidRewardGrant WHERE Encounter=@enc ORDER BY Slot";
                    using(var reader=cmd.ExecuteReader())while(reader.Read())result.Entries.Add(new RaidReward{Slot=reader.GetInt32(0),PlayerDbId=reader.GetInt32(1),Name=reader.GetString(2),Eligible=reader.GetBoolean(3),Amount=reader.GetInt32(4),Reason=reader.GetString(5),EquipmentTemplate=reader.GetInt32(6),EquipmentName=reader.GetString(7),EquipmentIcon=reader.GetString(8)});
                    return result;
                }
            }
        }
    }
    // Write-ahead receipts survive process restart. SQL commit, including all four recipients, is atomic/idempotent.
    public static class RaidRewardOutbox
    {
        private sealed class Pending {public RaidRewardRecord Record;public Action<bool> Done;public long RetryAt;}
        private static readonly object Gate=new object();
        private static readonly Dictionary<long,Pending> Queue=new Dictionary<long,Pending>();
        private static Timer timer;
        private static int working;
        public static string DirectoryPath => Environment.GetEnvironmentVariable("PROJECT_DAWN_REWARD_OUTBOX") ?? Path.Combine(AppContext.BaseDirectory,"raid-reward-outbox");
        public static void Start()
        {
            Directory.CreateDirectory(DirectoryPath);
            lock(Gate){foreach(var path in Directory.GetFiles(DirectoryPath,"*.pending.json")){
                var record=JsonConvert.DeserializeObject<RaidRewardRecord>(File.ReadAllText(path));
                Queue[record.Encounter]=new Pending{Record=record};
            }if(timer==null)timer=new Timer(_=>Flush(),null,100,1000);}
        }
        public static void Enqueue(RaidRewardRecord record,Action<bool> done)
        {
            Directory.CreateDirectory(DirectoryPath);
            string path=Path.Combine(DirectoryPath,record.Encounter+".pending.json");
            byte[] bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record));
            string temp=path+".tmp";
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            File.Move(temp,path,true);
            lock(Gate)Queue[record.Encounter]=new Pending{Record=record,Done=done};
        }
        public static void Flush()
        {
            if(Interlocked.Exchange(ref working,1)!=0)return;
            try{
                Pending[] pending;lock(Gate)pending=Queue.Values.Where(x=>Environment.TickCount64>=x.RetryAt).ToArray();
                foreach(var job in pending){try{
                    RaidRewardStore.Save(job.Record);
                    File.Delete(Path.Combine(DirectoryPath,job.Record.Encounter+".pending.json"));
                    lock(Gate)Queue.Remove(job.Record.Encounter);job.Done?.Invoke(true);
                }catch(Exception e){job.RetryAt=Environment.TickCount64+5000;Console.WriteLine("[RAID_REWARD_RETRY] "+job.Record.Encounter+" "+e.GetType().Name);}}
            }finally{Volatile.Write(ref working,0);}
        }
    }
}
