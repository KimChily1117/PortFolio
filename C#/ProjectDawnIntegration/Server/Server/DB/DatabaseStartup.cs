using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Server.DB
{
    public static class DatabaseStartup
    {
        public static void Verify(bool initializeDatabase)
        {
            using (AppDbContext db = new AppDbContext())
            {
                // Existing databases are never dropped or recreated.
                if (initializeDatabase)
                {
                    if(DatabaseSettings.IsMySql) {
                        // DDL requires a separate migration account; normal startup performs no DDL.
                        using(var script=new System.IO.StreamReader(typeof(DatabaseStartup).Assembly.GetManifestResourceStream("Server.MySqlInitial.sql")))
                        foreach(var statement in script.ReadToEnd().Split(';'))if(!string.IsNullOrWhiteSpace(statement))db.Database.ExecuteSqlRaw(statement);
                    } else {
                    bool created = db.Database.EnsureCreated();
                    using(var script=new System.IO.StreamReader(typeof(DatabaseStartup).Assembly.GetManifestResourceStream("Server.RaidRewards.sql")))db.Database.ExecuteSqlRaw(script.ReadToEnd());
                    Console.WriteLine($"[DB] Initialize complete. Created={created}, Provider=SqlServer");
                    }
                }

                db.Database.OpenConnection();
                db.Accounts.AsNoTracking().Take(1).ToList();
                db.Players.AsNoTracking().Take(1).ToList();
                db.Items.AsNoTracking().Take(1).ToList();
                db.MatchHistories.AsNoTracking().Take(1).ToList();
                db.MatchHistoryMembers.AsNoTracking().Take(1).ToList();
                if(DatabaseSettings.IsMySql) {
                    db.Database.ExecuteSqlRaw("SELECT Encounter FROM raidrewardencounter LIMIT 0");
                    db.Database.ExecuteSqlRaw("SELECT PlayerDbId,EquipmentTemplate,EquipmentItemDbId FROM raidrewardgrant LIMIT 0");
                } else db.Database.ExecuteSqlRaw("SELECT TOP(0) Encounter FROM dbo.RaidRewardEncounter; SELECT TOP(0) PlayerDbId,EquipmentTemplate,EquipmentItemDbId FROM dbo.RaidRewardGrant;");
            }
            Console.WriteLine("[DB] Ready. Provider="+DatabaseSettings.Provider);
        }
    }
}
